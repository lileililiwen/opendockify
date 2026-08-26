using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenDockify.Finance.Services;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class AutomationFlowTests : IDisposable
{
    private static readonly Guid _ownerA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid _ownerB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");

    private readonly SqliteConnection _connection;
    private readonly TestIntegrationsDbContext _db;
    private readonly AutomationService _automation;
    private readonly DocumentService _documents;
    private readonly string _storageRoot;
    private readonly IdempotencyService _idempotency;

    public AutomationFlowTests()
    {
        _connection = AutomationTestHarness.OpenConnection(AutomationTestHarness.NewDatabaseName());
        _db = AutomationTestHarness.CreateContext(_connection);
        _db.Database.EnsureCreated();
        _storageRoot = Path.Combine(Path.GetTempPath(), $"opendockify-automation-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_storageRoot);

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Storage:DocumentsPath"] = _storageRoot,
            ["Integrations:TokenPepper"] = "unit-test-pepper",
        }).Build();

        var templates = new TemplateService(_db);
        var renderer = new FakePdfRenderer();
        var interestRates = new InterestRateService(new FixedConfigReader());
        _documents = new DocumentService(
            _db, templates, renderer, interestRates, new OwnerDocumentReadAuthorizer(_db), configuration);
        _idempotency = new IdempotencyService(_db, Options.Create(new IntegrationsOptions()));
        _automation = new AutomationService(_db, templates, _documents, _idempotency);
    }

    [Fact]
    public async Task Finalize_creates_document_outbox_and_idempotency_atomically_then_replays()
    {
        var template = await SeedTemplateAsync();
        var command = Command(template.Id, ("amount", "12000"));

        var first = await FinalizeAsync(_ownerA, "op-1", command);
        Assert.Equal(AutomationFinalizeKind.Success, first.Kind);
        Assert.Equal(201, first.StatusCode);
        Assert.NotNull(first.DocumentId);
        Assert.Equal(1, await _db.Documents.CountAsync());
        Assert.Equal(1, await _db.OutboxEvents.CountAsync());
        var @event = await _db.OutboxEvents.SingleAsync();
        Assert.Equal("document.finalized", @event.Type);
        Assert.Contains(first.DocumentId.Value.ToString(), @event.PayloadJson);
        var record = await _db.IdempotencyRecords.SingleAsync();
        Assert.Equal(201, record.StatusCode);
        var payload = JsonSerializer.Deserialize<JsonElement>(first.ResponseJson);
        Assert.Equal(record.Id, Guid.Parse(payload.GetProperty("operationId").GetString()!));

        var replay = await FinalizeAsync(_ownerA, "op-1", command);
        Assert.Equal(AutomationFinalizeKind.Replay, replay.Kind);
        Assert.Equal(first.ResponseJson, replay.ResponseJson);
        Assert.Equal(1, await _db.Documents.CountAsync());

        var conflict = await FinalizeAsync(_ownerA, "op-1", Command(template.Id, ("amount", "999")));
        Assert.Equal(AutomationFinalizeKind.Conflict, conflict.Kind);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(1, await _db.Documents.CountAsync());
    }

    [Fact]
    public async Task Invalid_payload_returns_structured_field_errors_and_creates_nothing()
    {
        var template = await SeedTemplateAsync();

        var missing = await FinalizeAsync(_ownerA, "op-missing", Command(template.Id));
        Assert.Equal(AutomationFinalizeKind.Validation, missing.Kind);
        Assert.Equal(422, missing.StatusCode);
        Assert.Contains("\"code\":\"validation_failed\"", missing.ResponseJson);
        Assert.Contains("amount", missing.ResponseJson);

        var negative = await FinalizeAsync(_ownerA, "op-negative", Command(template.Id, ("amount", "-5")));
        Assert.Equal(AutomationFinalizeKind.Validation, negative.Kind);
        Assert.Contains("must not be negative", negative.ResponseJson);

        Assert.Equal(0, await _db.Documents.CountAsync());
        Assert.Equal(0, await _db.OutboxEvents.CountAsync());
        Assert.Equal(0, await _db.IdempotencyRecords.CountAsync());
    }

    [Fact]
    public async Task Unknown_template_is_not_found_without_side_effects()
    {
        var result = await FinalizeAsync(_ownerA, "op-none", Command(Guid.NewGuid(), ("amount", "5")));

        Assert.Equal(AutomationFinalizeKind.NotFound, result.Kind);
        Assert.Equal(404, result.StatusCode);
        Assert.Empty(await _db.IdempotencyRecords.ToListAsync());
    }

    [Fact]
    public async Task Preview_reports_structured_errors_or_rendered_text()
    {
        var template = await SeedTemplateAsync();

        var invalid = await _automation.PreviewAsync(_ownerA, Command(template.Id, ("amount", "abc")));
        Assert.Null(invalid.RenderedText);
        Assert.NotNull(invalid.FieldErrors);
        Assert.Contains(invalid.FieldErrors, x => x.Field == "amount");

        var valid = await _automation.PreviewAsync(_ownerA, Command(template.Id, ("amount", "100")));
        // Currency values render as RMB uppercase; only assert the stable shell.
        Assert.NotNull(valid.RenderedText);
        Assert.Contains("CNY", valid.RenderedText);
        Assert.Equal("IOU", valid.TemplateName);
    }

    [Fact]
    public async Task Template_listing_respects_owner_visibility()
    {
        await SeedTemplateAsync();
        _db.Templates.Add(new Template
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerB,
            IsBuiltIn = false,
            IsPublic = false,
            Name = "B private",
            Category = "Test",
            Body = "x",
            DefinitionJson = ValidDefinitionJson(),
        });
        _db.Templates.Add(new Template
        {
            Id = Guid.NewGuid(),
            IsBuiltIn = true,
            IsPublic = false,
            Name = "Built-in",
            Category = "Test",
            Body = "x",
            DefinitionJson = ValidDefinitionJson(),
        });
        await _db.SaveChangesAsync();

        var forA = await _automation.ListTemplatesAsync(_ownerA);
        Assert.Contains(forA, x => x.Name == "IOU");
        Assert.DoesNotContain(forA, x => x.Name == "B private");
        Assert.Contains(forA, x => x.Name == "Built-in");

        var forB = await _automation.ListTemplatesAsync(_ownerB);
        Assert.DoesNotContain(forB, x => x.Name == "IOU");
    }

    [Fact]
    public async Task Operation_status_is_owner_scoped()
    {
        var template = await SeedTemplateAsync();
        var result = await FinalizeAsync(_ownerA, "op-status", Command(template.Id, ("amount", "10")));
        var operationId = Assert.Single(_db.IdempotencyRecords).Id;

        Assert.NotNull(await _automation.GetOperationAsync(_ownerA, operationId));
        Assert.Null(await _automation.GetOperationAsync(_ownerB, operationId));
        Assert.Null(await _automation.GetOperationAsync(_ownerA, Guid.NewGuid()));

        // The document itself is equally owner-scoped for retrieval.
        var documentAccess = await _documents.GetAsync(_ownerB, result.DocumentId!.Value);
        Assert.True(documentAccess.NotFound);
    }

    private async Task<AutomationFinalizeResult> FinalizeAsync(
        Guid ownerId, string key, GenerateCommand command)
    {
        var body = JsonSerializer.Serialize(new { templateId = command.TemplateId, values = command.Values },
            AutomationTestHarness.SerializerOptions);
        return await _automation.FinalizeAsync(
            ownerId,
            // Idempotency binds to (token, key): reuse one token per owner so
            // replays/conflicts resolve against the same record.
            tokenId: TokenIdFor(ownerId),
            key,
            "/api/v1/automation/finalize",
            requestDigest: Sha256(body),
            command);
    }

    private static readonly Dictionary<Guid, Guid> _tokenIds = [];

    private static Guid TokenIdFor(Guid ownerId)
    {
        if (!_tokenIds.TryGetValue(ownerId, out var tokenId))
        {
            tokenId = Guid.NewGuid();
            _tokenIds[ownerId] = tokenId;
        }

        return tokenId;
    }

    private async Task<Template> SeedTemplateAsync()
    {
        var template = new Template
        {
            Id = Guid.NewGuid(),
            OwnerId = _ownerA,
            IsBuiltIn = false,
            IsPublic = false,
            Name = "IOU",
            Category = "Finance",
            Body = "IOU of {{amount}} CNY",
            DefinitionJson = ValidDefinitionJson(),
        };
        _db.Templates.Add(template);
        _db.TemplateRevisions.Add(new TemplateRevision
        {
            Id = Guid.NewGuid(),
            TemplateId = template.Id,
            Revision = 1,
            Name = template.Name,
            Category = template.Category,
            Body = template.Body,
            DefinitionJson = template.DefinitionJson,
        });
        await _db.SaveChangesAsync();
        return template;
    }

    private static string ValidDefinitionJson()
    {
        return JsonSerializer.Serialize(new TemplateDefinition
        {
            Fields =
        {
            new FieldDefinition
            {
                Name = "amount",
                Label = "Amount",
                Type = FieldType.Currency,
                Required = true,
                Validation = new ValidationRule { NonNegative = true },
            },
        },
        }, TemplateDefinitionValidator.JsonOptions);
    }

    private static GenerateCommand Command(Guid templateId, params (string Key, string Value)[] values)
    {
        return new(
        templateId,
        values.ToDictionary(x => x.Key, x => x.Value),
        []);
    }

    private static string Sha256(string value)
    {
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(value)))
            .ToLowerInvariant();
    }

    private sealed class FakePdfRenderer : OpenDockify.Rendering.Services.IPdfRenderer
    {
        public async Task RenderAsync(string text, string outputPath, CancellationToken cancellationToken)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
            await File.WriteAllTextAsync(outputPath, "%PDF-fake", cancellationToken);
        }
    }

    private sealed class FixedConfigReader : OpenDockify.SystemConfig.Services.ISystemConfigReader
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(default(T));
        }
    }

    public void Dispose()
    {
        _db.Dispose();
        Directory.Delete(_storageRoot, recursive: true);
    }
}
