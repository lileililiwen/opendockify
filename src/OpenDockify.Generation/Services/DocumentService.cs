using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenDockify.Auth.Services;
using OpenDockify.Finance.Services;
using OpenDockify.Generation.Models;
using OpenDockify.Rendering.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Platform.Caching.Contracts;
using Platform.Caching.Keys;
using Platform.Storage.Contracts;
using Platform.Storage.Keys;

namespace OpenDockify.Generation.Services;

public enum GenerationErrorKind
{
    None,
    NotFound,
    Forbidden,
    Validation,
    RenderError,
}

public sealed record GenerateResult(
    Document? Document,
    string? TemplateName,
    IReadOnlyList<string> Warnings,
    GenerationErrorKind ErrorKind,
    string? Error)
{
    public static GenerateResult Success(Document document, string templateName, IReadOnlyList<string> warnings)
    {
        return new(document, templateName, warnings, GenerationErrorKind.None, null);
    }

    public static GenerateResult Failure(GenerationErrorKind kind, string error)
    {
        return new(null, null, [], kind, error);
    }
}

public sealed record DocumentPreviewResult(
    string? RenderedText,
    string? TemplateName,
    Guid? TemplateRevisionId,
    IReadOnlyList<string> Warnings,
    GenerationErrorKind ErrorKind,
    string? Error)
{
    public static DocumentPreviewResult Success(
        string renderedText,
        string templateName,
        IReadOnlyList<string> warnings)
    {
        return new DocumentPreviewResult(renderedText, templateName, null, warnings, GenerationErrorKind.None, null);
    }

    public static DocumentPreviewResult Failure(GenerationErrorKind kind, string error)
    {
        return new DocumentPreviewResult(null, null, null, [], kind, error);
    }
}

public sealed record GenerateCommand(
    Guid TemplateId,
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);

public sealed record DocumentListPage(
    IReadOnlyList<DocumentLibraryItem> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record DocumentLibraryItem(
    Guid Id,
    Guid TemplateId,
    string TemplateName,
    string Title,
    bool IsArchived,
    Guid? ParentId,
    DocumentStatus Status,
    OpenDockify.Esign.Models.SigningStatus SigningStatus,
    DateTime CreatedAt);

public sealed record DocumentLibraryDetail(
    Document Document,
    string TemplateName,
    string Title,
    bool IsOwner = true,
    string AccessLevel = "owner");

public enum DocumentMetadataErrorKind
{
    None,
    NotFound,
    Validation,
}

public sealed record DocumentMetadataResult(
    DocumentLibraryDetail? Detail,
    DocumentMetadataErrorKind ErrorKind,
    string? Error)
{
    public static DocumentMetadataResult Success(DocumentLibraryDetail detail)
    {
        return new DocumentMetadataResult(detail, DocumentMetadataErrorKind.None, null);
    }

    public static DocumentMetadataResult Failure(DocumentMetadataErrorKind kind, string error)
    {
        return new DocumentMetadataResult(null, kind, error);
    }
}

public sealed record DocumentVersionHistoryResult(
    bool NotFound,
    IReadOnlyList<DocumentLibraryItem> Items);

/// <summary>Parameter snapshot stored on each document record.</summary>
public sealed record DocumentSnapshot(
    Dictionary<string, string> Values,
    List<string> SelectedClauseIds);

/// <summary>
/// Orchestrates document generation: load accessible template → validate values
/// (required, type, non-negative, range — blocking) → collect finance warnings
/// (interest-rate vs LPR, non-blocking) → render → append risk notice → PDF →
/// upload to <see cref="IObjectStorage"/> → persist immutable record. Re-edit
/// creates a new record with <see cref="Document.ParentId"/> set; the original
/// never changes.
///
/// Identical successful renders are served from the platform cache (SHA-256
/// of the normalized inputs under the app prefix, 10-minute default TTL, tag
/// <c>renders</c>). The cache is optional — without a store every preview
/// renders directly. Keys, tags, and logs never contain raw field values.
/// </summary>
public sealed partial class DocumentService(
    DbContext db,
    TemplateService templateService,
    IPdfRenderer pdfRenderer,
    InterestRateService interestRateService,
    IDocumentReadAuthorizer readAuthorizer,
    IConfiguration configuration,
    IObjectStorage objectStorage,
    ICacheStore? cacheStore = null,
    CacheKeyBuilder? cacheKeys = null,
    ILogger<DocumentService>? logger = null)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<GenerateResult> GenerateAsync(
        Guid userId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var preview = await PreviewAsync(userId, command, cancellationToken);
        if (preview.ErrorKind != GenerationErrorKind.None)
        {
            return GenerateResult.Failure(preview.ErrorKind, preview.Error ?? "Preview failed.");
        }

        var fullText = preview.RenderedText!;
        var templateName = preview.TemplateName!;
        var document = new Document
        {
            Id = Guid.NewGuid(),
            OwnerId = userId,
            TemplateId = command.TemplateId,
            TemplateRevisionId = preview.TemplateRevisionId,
            Title = templateName,
            IsArchived = false,
            Status = DocumentStatus.Generated,
            SnapshotJson = SerializeSnapshot(command),
            RenderedText = fullText,
            CreatedAt = DateTime.UtcNow,
        };

        var documentsPath = configuration["Storage:DocumentsPath"] ?? "/app/data/documents";
        document.PdfPath = Path.Combine(documentsPath, userId.ToString(), $"{document.Id}.pdf");
        document.PdfStorageKey = $"pdfs/{userId:N}/{document.Id:N}.pdf";

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"opendockify-render-{document.Id:N}.pdf");
        try
        {
            await pdfRenderer.RenderAsync(fullText, tempPath, cancellationToken);
            await using var pdf = File.OpenRead(tempPath);
            document.ContentSha256 = Convert.ToHexString(
                await System.Security.Cryptography.SHA256.HashDataAsync(pdf, cancellationToken))
                .ToLowerInvariant();
            await UploadPdfAsync(document, tempPath, cancellationToken);
        }
        catch (Exception ex)
        {
            return GenerateResult.Failure(GenerationErrorKind.RenderError, $"PDF rendering failed: {ex.Message}");
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }

        db.Set<Document>().Add(document);
        await db.SaveChangesAsync(cancellationToken);

        return GenerateResult.Success(document, templateName, preview.Warnings);
    }

    private async Task UploadPdfAsync(Document document, string sourcePath, CancellationToken cancellationToken)
    {
        var key = new StorageObjectKey(document.PdfStorageKey!);
        await using var input = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            81920,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        var info = new FileInfo(sourcePath);
        var request = new StorageUploadRequest(key, input, "application/pdf", info.Length);
        var outcome = await objectStorage.UploadAsync(request, cancellationToken);
        if (outcome.Status != StorageOutcomeStatus.Succeeded)
        {
            throw new InvalidOperationException(
                outcome.Failure is null
                    ? "PDF upload to object storage failed."
                    : $"PDF upload to object storage failed: {outcome.Failure.Message}");
        }
    }

    public async Task<StorageReadResult?> TryOpenPdfAsync(Document document, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(document.PdfStorageKey))
        {
            var result = await objectStorage.DownloadAsync(new StorageObjectKey(document.PdfStorageKey), cancellationToken);
            if (result.Status == StorageOutcomeStatus.Succeeded)
            {
                return result.Value;
            }
        }

        if (!string.IsNullOrEmpty(document.PdfPath) && File.Exists(document.PdfPath))
        {
            var info = new FileInfo(document.PdfPath);
            var stream = new FileStream(
                document.PdfPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            return new StorageReadResult(
                stream,
                new StorageObjectMetadata(info.Length, "application/pdf", new DateTimeOffset(info.LastWriteTimeUtc, TimeSpan.Zero), null));
        }

        return null;
    }

    /// <summary>
    /// Runs the authoritative access, validation, warning, and text-rendering
    /// pipeline without creating a PDF or persistent document.
    /// </summary>
    public async Task<DocumentPreviewResult> PreviewAsync(
        Guid userId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var templateResult = await templateService.GetByIdAsync(userId, command.TemplateId, cancellationToken);
        if (templateResult.NotFound)
        {
            return DocumentPreviewResult.Failure(GenerationErrorKind.NotFound, "Template not found.");
        }

        var template = templateResult.Value!;
        var revisionId = await db.Set<TemplateRevision>()
            .Where(x => x.TemplateId == template.Id && x.Revision == template.CurrentRevision)
            .Select(x => (Guid?)x.Id)
            .SingleOrDefaultAsync(cancellationToken);
        var definitionValidation = TemplateDefinitionValidator.Validate(template.DefinitionJson, template.Body);
        if (definitionValidation.Error is not null)
        {
            return DocumentPreviewResult.Failure(GenerationErrorKind.Validation, definitionValidation.Error);
        }

        var definition = definitionValidation.Definition!;
        var validationErrors = ValidateValues(definition, command);
        var warnings = new List<string>();

        if (validationErrors.Count > 0)
        {
            return DocumentPreviewResult.Failure(GenerationErrorKind.Validation, string.Join(" ", validationErrors));
        }

        await CollectInterestWarningsAsync(definition, command.Values, warnings, cancellationToken);

        var rendered = await RenderCachedAsync(
            userId,
            template,
            definition,
            command,
            cancellationToken);
        if (rendered is null)
        {
            return DocumentPreviewResult.Failure(GenerationErrorKind.Validation, "Rendering failed.");
        }

        var fullText = AppendRiskNotice(rendered, template.RiskNoticeText);
        return new DocumentPreviewResult(fullText, template.Name, revisionId, warnings, GenerationErrorKind.None, null);
    }

    /// <summary>
    /// Re-edits a document: creates a NEW record with the same template and
    /// the supplied (changed) values, linking <c>parentId</c> to the original.
    /// The original record is never modified.
    /// </summary>
    public async Task<GenerateResult> ReEditAsync(
        Guid userId,
        Guid documentId,
        GenerateCommand command,
        CancellationToken cancellationToken = default)
    {
        var original = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == documentId, cancellationToken);

        if (original is null || original.OwnerId != userId)
        {
            return GenerateResult.Failure(GenerationErrorKind.NotFound, "Document not found.");
        }

        var result = await GenerateAsync(userId, command, cancellationToken);
        if (result.Document is not null)
        {
            result.Document.ParentId = original.Id;
            result.Document.Title = DocumentLibraryPolicy.ResolveTitle(original.Title, result.TemplateName ?? "Document");
            await db.SaveChangesAsync(cancellationToken);
        }

        return result;
    }

    public async Task<DocumentListPage> ListAsync(
        Guid userId,
        int page,
        int pageSize,
        DocumentLibraryQuery libraryQuery,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var filtered = from document in db.Set<Document>()
                       join template in db.Set<Template>() on document.TemplateId equals template.Id
                       where document.OwnerId == userId
                       select new { Document = document, TemplateName = template.Name };

        filtered = libraryQuery.Archive switch
        {
            DocumentArchiveFilter.Active => filtered.Where(item => !item.Document.IsArchived),
            DocumentArchiveFilter.Archived => filtered.Where(item => item.Document.IsArchived),
            _ => filtered,
        };

        if (libraryQuery.Search is string search)
        {
            var normalizedSearch = search.ToLowerInvariant();
#pragma warning disable CA1304, CA1311, CA1862 // EF translates ToLower/Contains to provider SQL; StringComparison is not translatable.
            filtered = filtered.Where(item =>
                item.Document.Title.ToLower().Contains(normalizedSearch)
                || item.TemplateName.ToLower().Contains(normalizedSearch));
#pragma warning restore CA1304, CA1311, CA1862
        }

        var ordered = libraryQuery.Sort switch
        {
            DocumentLibrarySort.Oldest => filtered
                .OrderBy(item => item.Document.CreatedAt)
                .ThenBy(item => item.Document.Id),
            DocumentLibrarySort.Title => filtered
                .OrderBy(item => item.Document.Title == string.Empty ? item.TemplateName : item.Document.Title)
                .ThenBy(item => item.Document.Id),
            _ => filtered
                .OrderByDescending(item => item.Document.CreatedAt)
                .ThenBy(item => item.Document.Id),
        };

        var total = await filtered.CountAsync(cancellationToken);
        var items = await ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(item => new DocumentLibraryItem(
                item.Document.Id,
                item.Document.TemplateId,
                item.TemplateName,
                item.Document.Title == string.Empty ? item.TemplateName : item.Document.Title,
                item.Document.IsArchived,
                item.Document.ParentId,
                item.Document.Status,
                item.Document.SigningStatus,
                item.Document.CreatedAt))
            .ToListAsync(cancellationToken);

        return new DocumentListPage(items, page, pageSize, total, (int)Math.Ceiling(total / (double)pageSize));
    }

    public async Task<OwnedResourceResult<DocumentLibraryDetail>> GetAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await readAuthorizer.AuthorizeAsync(userId, id, cancellationToken);
        if (!access.Allowed)
        {
            return OwnedResourceResult.Missing<DocumentLibraryDetail>();
        }

        var detail = await (from document in db.Set<Document>()
                            join template in db.Set<Template>() on document.TemplateId equals template.Id
                            where document.Id == id
                            select new DocumentLibraryDetail(
                                document,
                                template.Name,
                                document.Title == string.Empty ? template.Name : document.Title,
                                access.IsOwner,
                                access.AccessLevel))
            .SingleOrDefaultAsync(cancellationToken);

        if (detail is null)
        {
            return OwnedResourceResult.Missing<DocumentLibraryDetail>();
        }

        return OwnedResourceResult.Found(detail);
    }

    public async Task<DocumentMetadataResult> UpdateMetadataAsync(
        Guid userId,
        Guid id,
        string? title,
        bool isArchived,
        CancellationToken cancellationToken = default)
    {
        var titleValidation = DocumentLibraryPolicy.ValidateTitle(title);
        if (titleValidation.Error is not null)
        {
            return DocumentMetadataResult.Failure(DocumentMetadataErrorKind.Validation, titleValidation.Error);
        }

        var document = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == id && d.OwnerId == userId, cancellationToken);
        if (document is null)
        {
            return DocumentMetadataResult.Failure(DocumentMetadataErrorKind.NotFound, "Document not found.");
        }

        document.Title = titleValidation.Title!;
        document.IsArchived = isArchived;
        await db.SaveChangesAsync(cancellationToken);

        var templateName = await db.Set<Template>()
            .Where(template => template.Id == document.TemplateId)
            .Select(template => template.Name)
            .SingleAsync(cancellationToken);
        return DocumentMetadataResult.Success(new DocumentLibraryDetail(document, templateName, document.Title));
    }

    public async Task<DocumentVersionHistoryResult> GetVersionHistoryAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var access = await readAuthorizer.AuthorizeAsync(userId, id, cancellationToken);
        if (!access.Allowed)
        {
            return new DocumentVersionHistoryResult(true, []);
        }

        var root = await db.Set<Document>().Where(x => x.Id == id).Select(x => new { x.OwnerId }).SingleOrDefaultAsync(cancellationToken);
        if (root is null)
        {
            return new DocumentVersionHistoryResult(true, []);
        }

        var documents = await (from document in db.Set<Document>()
                               join template in db.Set<Template>() on document.TemplateId equals template.Id
                               where document.OwnerId == root.OwnerId
                               select new DocumentLibraryItem(
                                   document.Id,
                                   document.TemplateId,
                                   template.Name,
                                   document.Title == string.Empty ? template.Name : document.Title,
                                   document.IsArchived,
                                   document.ParentId,
                                   document.Status,
                                   document.SigningStatus,
                                   document.CreatedAt))
            .ToListAsync(cancellationToken);

        if (!documents.Any(document => document.Id == id))
        {
            return new DocumentVersionHistoryResult(true, []);
        }

        var links = documents
            .Select(document => new DocumentVersionLink(document.Id, document.ParentId, document.CreatedAt))
            .ToList();
        var connected = DocumentLibraryPolicy.FindConnectedVersions(links, id);
        var documentsById = documents.ToDictionary(document => document.Id);
        return new DocumentVersionHistoryResult(
            false,
            connected.Select(link => documentsById[link.Id]).ToList());
    }

    public async Task<OwnedResourceResult<Document>> DeleteAsync(
        Guid userId,
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var document = await db.Set<Document>()
            .SingleOrDefaultAsync(d => d.Id == id, cancellationToken);

        if (document is null || document.OwnerId != userId)
        {
            return OwnedResourceResult.Missing<Document>();
        }

        db.Set<Document>().Remove(document);
        await db.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrEmpty(document.PdfStorageKey))
        {
            await objectStorage.DeleteAsync(new StorageObjectKey(document.PdfStorageKey), cancellationToken);
        }
        else if (!string.IsNullOrEmpty(document.PdfPath) && File.Exists(document.PdfPath))
        {
            File.Delete(document.PdfPath);
        }

        return OwnedResourceResult.Found(document);
    }

    private static string AppendRiskNotice(string renderedText, string riskNotice)
    {
        if (string.IsNullOrWhiteSpace(riskNotice))
        {
            return renderedText;
        }

        return $"{renderedText}\n\n{riskNotice}";
    }

    private static string SerializeSnapshot(GenerateCommand command)
    {
        var snapshot = new DocumentSnapshot(command.Values, command.SelectedClauseIds);
        return JsonSerializer.Serialize(snapshot, _jsonOptions);
    }

    private static List<string> ValidateValues(TemplateDefinition definition, GenerateCommand command)
    {
        // Structured validation is shared with the automation API so both
        // surfaces enforce identical rules and messages.
        return [.. DocumentFieldValidation.Validate(definition, command).Select(error => error.Error)];
    }

    private async Task<string?> RenderCachedAsync(
        Guid userId,
        Template template,
        TemplateDefinition definition,
        GenerateCommand command,
        CancellationToken cancellationToken)
    {
        if (cacheStore is null || cacheKeys is null)
        {
            return TemplateRenderer.Render(definition, template.Body, command.Values, command.SelectedClauseIds).Text;
        }

        var normalized = NormalizeRenderInputs(
            userId,
            template.Id,
            template.DefinitionJson,
            template.Body,
            command.Values,
            command.SelectedClauseIds);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalized))).ToLowerInvariant();
        var key = cacheKeys.ForApplication($"render:{digest}");
        var read = await cacheStore.GetAsync<string>(key, cancellationToken);
        if (read.Status == CacheReadStatus.Hit && read.Value is not null)
        {
            Log.RenderCacheHit(logger ?? NullLogger<DocumentService>.Instance, SafeKeyPrefix(key.Value), read.Value.Length);
            return read.Value;
        }

        var render = TemplateRenderer.Render(definition, template.Body, command.Values, command.SelectedClauseIds);
        if (render.Text is null)
        {
            return null;
        }

        var ttlMinutes = int.TryParse(configuration["Cache:RenderTtlMinutes"], out var minutes) && minutes > 0
            ? minutes
            : 10;
        await cacheStore.SetAsync(
            key,
            render.Text,
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ttlMinutes), Tags = ["renders"] },
            cancellationToken);
        return render.Text;
    }

    private static string NormalizeRenderInputs(
        Guid userId,
        Guid templateId,
        string definitionJson,
        string body,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string> selectedClauseIds)
    {
        var sb = new StringBuilder();
        sb.Append(userId.ToString("N")).Append('|');
        sb.Append(templateId.ToString("N")).Append('|');
        sb.Append(definitionJson).Append('|').Append(body).Append('|');
        foreach (var pair in values.OrderBy(p => p.Key, StringComparer.Ordinal))
        {
            sb.Append(pair.Key).Append('=').Append(pair.Value).Append(';');
        }

        sb.Append('|');
        foreach (var id in selectedClauseIds.OrderBy(id => id, StringComparer.Ordinal))
        {
            sb.Append(id).Append(',');
        }

        return sb.ToString();
    }

    private static string SafeKeyPrefix(string keyValue)
    {
        return keyValue.Length <= 16 ? keyValue : keyValue[..16];
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Debug, "Render cache hit key={KeyPrefix} chars={Chars}.")]
        public static partial void RenderCacheHit(ILogger logger, string keyPrefix, int chars);
    }

    private async Task CollectInterestWarningsAsync(
        TemplateDefinition definition,
        Dictionary<string, string> values,
        List<string> warnings,
        CancellationToken cancellationToken)
    {
        foreach (var field in definition.Fields)
        {
            if (field.Validation?.IsInterestRate != true)
            {
                continue;
            }

            if (!values.TryGetValue(field.Name, out var raw) || !decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var rate))
            {
                continue;
            }

            var validation = await interestRateService.ValidateAsync(rate, cancellationToken);
            if (validation.Level != RateLevel.Ok)
            {
                warnings.Add(validation.Message);
            }
        }
    }
}
