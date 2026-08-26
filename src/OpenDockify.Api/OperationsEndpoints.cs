using Microsoft.EntityFrameworkCore;
using OpenDockify.Operations.Models;
using OpenDockify.Operations.Services;

namespace OpenDockify.Api;

public static class OperationsEndpoints
{
    public static IEndpointRouteBuilder MapOperationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/operations").RequireAuthorization("RequireAdmin");

        group.MapPost("/backups", async (BackupPassphraseRequest request, BackupCoordinator coordinator, CancellationToken ct) =>
        {
            var result = await coordinator.CreateAsync(request.Passphrase, ct);
            return Results.Ok(new { result.OperationId, result.Path, result.Digest });
        });

        group.MapPost("/validate", async (BackupPathRequest request, BackupCoordinator coordinator, CancellationToken ct) =>
        {
            var result = await coordinator.ValidateAsync(request.Path, request.Passphrase, ct);
            return result.IsValid ? Results.Ok(result) : Results.BadRequest(result);
        });

        group.MapPost("/restore", async (RestoreRequest request, BackupCoordinator coordinator, CancellationToken ct) =>
        {
            try
            {
                var operationId = await coordinator.RestoreAsync(request.Path, request.Passphrase, request.Digest, request.Receipt, ct);
                return Results.Ok(new { operationId, state = "verified" });
            }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (InvalidDataException ex) { return Results.BadRequest(new { error = ex.Message }); }
            catch (Exception) { return Results.Problem("Restore failed; rollback was attempted and normal traffic remains blocked if rollback verification failed."); }
        });

        group.MapGet("/integrity", async (BackupCoordinator coordinator, CancellationToken ct) =>
            Results.Ok(await coordinator.CheckIntegrityAsync(ct)));

        group.MapPost("/digests/backfill", async (DigestBackfillRequest request, DocumentDigestBackfillService service, CancellationToken ct) =>
            Results.Ok(await service.RunAsync(request.MaximumDocuments, ct)));

        group.MapGet("/status", async (DbContext db, CancellationToken ct) =>
            Results.Ok(await db.Set<BackupOperation>().AsNoTracking()
                .OrderByDescending(x => x.StartedAtUtc).Take(25).ToListAsync(ct)));

        return endpoints;
    }
}

public sealed record BackupPassphraseRequest(string Passphrase);
public sealed record BackupPathRequest(string Path, string Passphrase);
public sealed record RestoreRequest(string Path, string Passphrase, string Digest, string Receipt);
public sealed record DigestBackfillRequest(int MaximumDocuments = 100);
