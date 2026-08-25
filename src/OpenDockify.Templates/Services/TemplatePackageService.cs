using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Templates.Models;

namespace OpenDockify.Templates.Services;

public sealed record TemplatePackageMetadata(string Name, string Category, string Description, string RiskNoticeText);
public sealed record TemplatePackageProvenance(string? SourceInstance, Guid? SourceStableId, bool BuiltInDerived);
public sealed record TemplatePackage(int FormatVersion, Guid StableId, int Revision, TemplatePackageMetadata Metadata,
    string Body, JsonElement Definition, TemplatePackageProvenance Provenance, string Digest);
public sealed record PackageValidationResult(bool Valid, string? Error, string? Receipt, string Digest,
    Guid? StableId, bool Conflict, DateTimeOffset? ExpiresAt);
public sealed record PackageImportResult(Template? Template, string? Error, bool Conflict);

public sealed class TemplatePackageService(DbContext db, TemplateService templates)
{
    public const int MaxPackageBytes = 1024 * 1024;
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web) { WriteIndented = false, MaxDepth = 32 };
    private static readonly ConcurrentDictionary<string, (string Digest, DateTimeOffset Expires)> _receipts = new();

    public async Task<(byte[]? Bytes, string? Error)> ExportAsync(Guid userId, Guid id, CancellationToken ct)
    {
        var found = await templates.GetByIdAsync(userId, id, ct);
        if (found.NotFound)
            return (null, "Template not found.");
        var template = found.Value!;
        using var definition = JsonDocument.Parse(template.DefinitionJson, new JsonDocumentOptions { MaxDepth = 32 });
        var canonicalDefinition = Canonicalize(definition.RootElement);
        var digest = ComputeDigest(template.StableId, template.CurrentRevision, template.Name, template.Category,
            template.Description, template.RiskNoticeText, template.Body, canonicalDefinition,
            template.SourceInstance, template.SourceStableId, template.IsBuiltIn);
        var package = new TemplatePackage(1, template.StableId, template.CurrentRevision,
            new(template.Name, template.Category, template.Description, template.RiskNoticeText), template.Body,
            canonicalDefinition, new(template.SourceInstance, template.SourceStableId, template.IsBuiltIn), digest);
        return (JsonSerializer.SerializeToUtf8Bytes(package, _json), null);
    }

    public async Task<PackageValidationResult> ValidateAsync(byte[] bytes, CancellationToken ct)
    {
        if (bytes.Length == 0 || bytes.Length > MaxPackageBytes)
            return Invalid("Package size must be between 1 byte and 1 MiB.");
        TemplatePackage? package;
        try
        { package = JsonSerializer.Deserialize<TemplatePackage>(bytes, _json); }
        catch (JsonException ex) { return Invalid($"Malformed package: {ex.Message}"); }
        if (package is null || package.FormatVersion != 1)
            return Invalid("Unsupported package format version.");
        if (package.Metadata is null || package.Provenance is null || package.Definition.ValueKind != JsonValueKind.Object ||
            package.StableId == Guid.Empty || package.Revision < 1 || string.IsNullOrWhiteSpace(package.Digest))
            return Invalid("Package is missing required fields.");
        if (string.IsNullOrWhiteSpace(package.Metadata.Name) || package.Metadata.Name.Length > 200 ||
            string.IsNullOrWhiteSpace(package.Metadata.Category) || package.Metadata.Category.Length > 100 ||
            package.Metadata.Description.Length > 500 || package.Metadata.RiskNoticeText.Length > 4000)
            return Invalid("Package metadata exceeds supported limits.");
        var canonicalDefinition = Canonicalize(package.Definition);
        var expected = ComputeDigest(package.StableId, package.Revision, package.Metadata.Name, package.Metadata.Category,
            package.Metadata.Description, package.Metadata.RiskNoticeText, package.Body, canonicalDefinition,
            package.Provenance.SourceInstance, package.Provenance.SourceStableId, package.Provenance.BuiltInDerived);
        if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(expected), Encoding.ASCII.GetBytes(package.Digest ?? "")))
            return Invalid("Package digest verification failed.");
        var definition = package.Definition.GetRawText();
        var validation = TemplateDefinitionValidator.Validate(definition, package.Body);
        if (validation.Error is not null)
            return Invalid(validation.Error);
        var conflict = await db.Set<Template>().AnyAsync(x => x.StableId == package.StableId, ct);
        var receipt = Convert.ToHexString(RandomNumberGenerator.GetBytes(24)).ToLowerInvariant();
        var expires = DateTimeOffset.UtcNow.AddMinutes(10);
        _receipts[receipt] = (expected, expires);
        return new(true, null, receipt, expected, package.StableId, conflict, expires);
    }

    public async Task<PackageImportResult> ImportAsync(Guid userId, byte[] bytes, string receipt, string policy, CancellationToken ct)
    {
        var validation = await ValidateAsync(bytes, ct);
        if (!validation.Valid)
            return new(null, validation.Error, false);
        if (validation.Receipt is not null)
            _receipts.TryRemove(validation.Receipt, out _);
        if (!_receipts.TryRemove(receipt, out var saved) || saved.Expires <= DateTimeOffset.UtcNow || saved.Digest != validation.Digest)
            return new(null, "Validation receipt is invalid or expired.", false);
        var package = JsonSerializer.Deserialize<TemplatePackage>(bytes, _json)!;
        var existing = await db.Set<Template>().SingleOrDefaultAsync(x => x.StableId == package.StableId, ct);
        if (existing is not null && policy == "reject")
            return new(null, "Template identity already exists.", true);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        if (existing is not null && policy == "new-revision" && existing.OwnerId == userId)
        {
            var result = await templates.UpdateAsync(userId, existing.Id, Draft(package), ct);
            if (result.Template is not null)
                await transaction.CommitAsync(ct);
            return new(result.Template, result.ErrorMessage, false);
        }
        if (existing is not null && policy == "new-revision" && existing.IsPublic && !existing.IsBuiltIn)
        {
            var result = await templates.AdminUpsertAsync(existing.Id, Draft(package), ct);
            if (result.Template is not null)
                await transaction.CommitAsync(ct);
            return new(result.Template, result.ErrorMessage, false);
        }
        if (policy != "create-copy" || (existing is null && policy == "new-revision"))
            return new(null, existing is null ? "Use create-copy for a new identity." : "An explicit conflict policy is required.", existing is not null);
        var created = await templates.CreateAsync(userId, Draft(package), ct);
        if (created.Template is not null)
        {
            created.Template.SourceInstance = package.Provenance.SourceInstance;
            created.Template.SourceStableId = package.StableId;
            var revision = await db.Set<TemplateRevision>()
                .SingleAsync(x => x.TemplateId == created.Template.Id && x.Revision == 1, ct);
            revision.SourceInstance = package.Provenance.SourceInstance;
            revision.SourceStableId = package.StableId;
            await db.SaveChangesAsync(ct);
        }
        if (created.Template is not null)
            await transaction.CommitAsync(ct);
        return new(created.Template, created.ErrorMessage, false);
    }

    private static TemplateDraft Draft(TemplatePackage p)
    {
        return new(p.Metadata.Name, p.Metadata.Category,
        p.Metadata.Description, p.Metadata.RiskNoticeText, p.Body, p.Definition.GetRawText());
    }

    private static PackageValidationResult Invalid(string error)
    {
        return new(false, error, null, "", null, false, null);
    }

    private static string ComputeDigest(Guid stableId, int revision, string name, string category, string description,
        string risk, string body, JsonElement definition, string? sourceInstance, Guid? sourceStableId, bool builtIn)
    {
        var canonical = JsonSerializer.SerializeToUtf8Bytes(new
        {
            formatVersion = 1,
            stableId,
            revision,
            metadata = new { name, category, description, riskNoticeText = risk },
            body,
            definition,
            provenance = new { sourceInstance, sourceStableId, builtInDerived = builtIn },
        }, _json);
        return Convert.ToHexString(SHA256.HashData(canonical)).ToLowerInvariant();
    }

    public static JsonElement Canonicalize(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
            WriteCanonical(writer, element);
        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in element.EnumerateObject().OrderBy(x => x.Name, StringComparer.Ordinal))
            {
                writer.WritePropertyName(property.Name);
                WriteCanonical(writer, property.Value);
            }
            writer.WriteEndObject();
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray();
            foreach (var item in element.EnumerateArray())
                WriteCanonical(writer, item);
            writer.WriteEndArray();
        }
        else
            element.WriteTo(writer);
    }
}
