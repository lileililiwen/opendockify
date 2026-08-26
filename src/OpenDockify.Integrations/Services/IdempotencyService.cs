using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Models;

namespace OpenDockify.Integrations.Services;

public sealed record IdempotencyRequest(
    Guid TokenId,
    Guid OwnerId,
    string Key,
    string Route,
    string RequestDigest,
    Guid? OperationId = null);

/// <summary>Outcome produced by the wrapped mutation when it actually runs.</summary>
public sealed record MutationOutcome(
    int StatusCode,
    object Payload,
    Guid? DocumentId,
    bool Succeeded);

/// <summary>Result of an idempotent execution: either fresh, replayed, or a conflict.</summary>
public sealed record IdempotencyExecution(
    int StatusCode,
    string ResponseJson,
    bool Replayed,
    bool Conflict,
    Guid? DocumentId = null,
    Guid? OperationId = null)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public static readonly IdempotencyExecution ConflictResponse = new(
        409,
        JsonSerializer.Serialize(
            new AutomationError(new AutomationErrorDetail(
                "conflict_idempotency_key",
                "This idempotency key was already used with a different request body.",
                null)),
            _jsonOptions),
        false,
        true);
}

/// <summary>Canonical request-digest computation shared by automation endpoints.</summary>
public static class RequestDigests
{
    public static string Compute(ReadOnlySpan<byte> body)
    {
        return Convert.ToHexString(SHA256.HashData(body)).ToLowerInvariant();
    }
}

/// <summary>
/// Transactional idempotency: the mutation's saved entities, the idempotency
/// record, and the outbox event commit together. Concurrent duplicate inserts
/// are resolved by the unique (token, key) index into a replay or conflict.
/// </summary>
public class IdempotencyService(DbContext db, IOptions<IntegrationsOptions> options)
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<IdempotencyExecution> ExecuteAsync(
        IdempotencyRequest request,
        Func<CancellationToken, Task<MutationOutcome>> mutation,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindActiveAsync(request, cancellationToken);
        if (existing is not null)
        {
            return existing.RequestDigest == request.RequestDigest
                ? new IdempotencyExecution(existing.StatusCode, existing.ResponseJson, Replayed: true, Conflict: false, existing.DocumentId, existing.Id)
                : IdempotencyExecution.ConflictResponse;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var outcome = await mutation(cancellationToken);
            var record = new IdempotencyRecord
            {
                // The caller-supplied id becomes the public operation id so
                // finalize responses can reference it for status checks.
                Id = request.OperationId ?? Guid.NewGuid(),
                TokenId = request.TokenId,
                OwnerId = request.OwnerId,
                Key = request.Key,
                Route = request.Route,
                RequestDigest = request.RequestDigest,
                StatusCode = outcome.StatusCode,
                ResponseJson = JsonSerializer.Serialize(outcome.Payload, _jsonOptions),
                Outcome = outcome.Succeeded ? IdempotencyOutcome.Completed : IdempotencyOutcome.Failed,
                DocumentId = outcome.DocumentId,
                CreatedAtUtc = DateTime.UtcNow,
                ExpiresAtUtc = DateTime.UtcNow.AddDays(options.Value.GetIdempotencyRetentionDays()),
            };
            db.Set<IdempotencyRecord>().Add(record);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new IdempotencyExecution(record.StatusCode, record.ResponseJson, Replayed: false, Conflict: false, record.DocumentId, record.Id);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // A concurrent request with the same (token, key) committed first:
            // replay its stored response, or conflict on digest mismatch.
            await transaction.RollbackAsync(cancellationToken);
            var winner = await FindActiveAsync(request, cancellationToken);
            if (winner is null)
            {
                throw;
            }

            return winner.RequestDigest == request.RequestDigest
                ? new IdempotencyExecution(winner.StatusCode, winner.ResponseJson, Replayed: true, Conflict: false, winner.DocumentId, winner.Id)
                : IdempotencyExecution.ConflictResponse;
        }
    }

    private async Task<IdempotencyRecord?> FindActiveAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        return await FindActiveRecordAsync(request, cancellationToken);
    }

    /// <summary>Virtual so tests can inject the moment a concurrent writer wins.</summary>
    protected internal virtual async Task<IdempotencyRecord?> FindActiveRecordAsync(
        IdempotencyRequest request,
        CancellationToken cancellationToken)
    {
        var now = DateTime.UtcNow;
        var record = await db.Set<IdempotencyRecord>()
            .SingleOrDefaultAsync(x => x.TokenId == request.TokenId && x.Key == request.Key, cancellationToken);

        if (record is null)
        {
            return null;
        }

        if (record.ExpiresAtUtc <= now)
        {
            db.Set<IdempotencyRecord>().Remove(record);
            await db.SaveChangesAsync(cancellationToken);
            return null;
        }

        return record.Route == request.Route ? record : null;
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current.Message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
