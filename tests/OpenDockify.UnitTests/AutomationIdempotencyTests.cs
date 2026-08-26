using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Models;
using OpenDockify.Integrations.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class AutomationIdempotencyTests : IDisposable
{
    private readonly string _databaseName = AutomationTestHarness.NewDatabaseName();
    private readonly SqliteConnection _connection;
    private readonly TestIntegrationsDbContext _db;
    private readonly IdempotencyService _service;

    public AutomationIdempotencyTests()
    {
        _connection = AutomationTestHarness.OpenConnection(_databaseName);
        _db = AutomationTestHarness.CreateContext(_connection);
        _db.Database.EnsureCreated();
        _service = new IdempotencyService(_db, Options.Create(new IntegrationsOptions()));
    }

    [Fact]
    public async Task Same_key_and_digest_replays_the_stored_response_without_rerunning_the_mutation()
    {
        var runs = 0;
        var request = Request("key-1", "digest-1");
        var first = await _service.ExecuteAsync(request, Mutation(201, () => ++runs));
        var second = await _service.ExecuteAsync(request, Mutation(201, () => ++runs));

        Assert.False(first.Replayed);
        Assert.Equal(1, runs);
        Assert.True(second.Replayed);
        Assert.Equal(first.StatusCode, second.StatusCode);
        Assert.Equal(first.ResponseJson, second.ResponseJson);
    }

    [Fact]
    public async Task Key_reuse_with_a_different_digest_conflicts_without_mutating()
    {
        var runs = 0;
        await _service.ExecuteAsync(Request("key-2", "digest-a"), Mutation(201, () => ++runs));
        var conflict = await _service.ExecuteAsync(Request("key-2", "digest-b"), Mutation(201, () => ++runs));

        Assert.True(conflict.Conflict);
        Assert.Equal(409, conflict.StatusCode);
        Assert.Equal(1, runs);
        Assert.Contains("conflict_idempotency_key", conflict.ResponseJson);
    }

    [Fact]
    public async Task Concurrent_duplicate_inserts_resolve_to_a_single_mutation()
    {
        // Deterministically simulate losing the insert race: a concurrent
        // writer commits the same (token, key) between our initial lookup and
        // our insert. The unique index must convert the failure into a replay
        // of the winner's stored response.
        var winner = new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            TokenId = _testTokenId,
            OwnerId = _testOwnerId,
            Key = "race-key",
            Route = "/api/v1/automation/finalize",
            RequestDigest = "race-digest",
            StatusCode = 201,
            ResponseJson = "{\"concurrent\":true}",
            Outcome = IdempotencyOutcome.Completed,
            CreatedAtUtc = DateTime.UtcNow,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(1),
        };
        _db.IdempotencyRecords.Add(winner);
        await _db.SaveChangesAsync();

        var racing = new RaceInjectingIdempotencyService(_db, Options.Create(new IntegrationsOptions()));
        var runs = 0;
        var execution = await racing.ExecuteAsync(
            Request("race-key", "race-digest"),
            _ =>
            {
                runs++;
                return Task.FromResult(new MutationOutcome(201, new { ok = true }, null, true));
            });

        Assert.True(execution.Replayed);
        Assert.Equal("{\"concurrent\":true}", execution.ResponseJson);
        // The loser's mutation ran but was rolled back with its failed insert:
        // exactly one stored outcome survives.
        Assert.Equal(1, runs);
        Assert.Equal(1, await _db.IdempotencyRecords.CountAsync(x => x.Key == "race-key"));
    }

    /// <summary>Skips exactly one lookup so the insert collides with a pre-seeded winner.</summary>
    private sealed class RaceInjectingIdempotencyService(DbContext db, IOptions<IntegrationsOptions> options)
        : IdempotencyService(db, options)
    {
        private bool _skippedFirstLookup;

        protected override async Task<IdempotencyRecord?> FindActiveRecordAsync(
            IdempotencyRequest request,
            CancellationToken cancellationToken)
        {
            if (_skippedFirstLookup)
            {
                return await base.FindActiveRecordAsync(request, cancellationToken);
            }

            _skippedFirstLookup = true;
            return null;
        }
    }

    [Fact]
    public async Task Expired_records_are_replaced_instead_of_replayed()
    {
        _db.IdempotencyRecords.Add(new IdempotencyRecord
        {
            Id = Guid.NewGuid(),
            TokenId = _testTokenId,
            OwnerId = _testOwnerId,
            Key = "stale",
            Route = "/api/v1/automation/finalize",
            RequestDigest = "old",
            StatusCode = 201,
            ResponseJson = "{}",
            CreatedAtUtc = DateTime.UtcNow.AddDays(-5),
            ExpiresAtUtc = DateTime.UtcNow.AddDays(-1),
        });
        await _db.SaveChangesAsync();

        var runs = 0;
        var execution = await _service.ExecuteAsync(Request("stale", "new"), Mutation(201, () => ++runs));

        Assert.False(execution.Replayed);
        Assert.Equal(1, runs);
    }

    [Fact]
    public void Finalize_success_contract_is_stable()
    {
        var payload = new AutomationDocumentPayload(
            Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Guid.Parse("00000000-0000-0000-0000-000000000002"),
            "Loan IOU",
            "Loan IOU",
            "a1b2c3",
            [],
            new DateTime(2026, 8, 26, 0, 0, 0, DateTimeKind.Utc));

        Assert.Equal(
            """{"documentId":"00000000-0000-0000-0000-000000000001","templateId":"00000000-0000-0000-0000-000000000002","templateName":"Loan IOU","title":"Loan IOU","contentSha256":"a1b2c3","warnings":[],"createdAtUtc":"2026-08-26T00:00:00Z"}""",
            JsonSerializer.Serialize(payload, AutomationTestHarness.SerializerOptions));
    }

    [Fact]
    public void Structured_error_contract_is_stable()
    {
        var error = new AutomationError(new AutomationErrorDetail(
            "validation_failed",
            "One or more fields are invalid.",
            [new DocumentFieldError("amount", "Field 'amount' must not be negative.")]));

        Assert.Equal(
            """{"error":{"code":"validation_failed","message":"One or more fields are invalid.","fields":[{"field":"amount","error":"Field \u0027amount\u0027 must not be negative."}]}}""",
            JsonSerializer.Serialize(error, AutomationTestHarness.SerializerOptions));
    }

    [Fact]
    public void Template_item_contract_is_stable()
    {
        var item = new AutomationTemplateItem(
            Guid.Parse("00000000-0000-0000-0000-000000000003"),
            "Loan IOU",
            "Finance",
            "",
            1);

        Assert.Equal(
            """{"id":"00000000-0000-0000-0000-000000000003","name":"Loan IOU","category":"Finance","description":"","currentRevision":1}""",
            JsonSerializer.Serialize(item, AutomationTestHarness.SerializerOptions));
    }

    private static readonly Guid _testTokenId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid _testOwnerId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static IdempotencyRequest Request(string key, string digest)
    {
        return new(
        _testTokenId, _testOwnerId, key, "/api/v1/automation/finalize", digest);
    }

    private static Func<CancellationToken, Task<MutationOutcome>> Mutation(int status, Func<int> counter)
    {
        return _ =>
        {
            counter();
            return Task.FromResult(new MutationOutcome(status, new { ok = true }, null, true));
        };
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
