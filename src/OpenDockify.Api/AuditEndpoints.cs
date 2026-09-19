using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using OpenDockify.Data;
using OpenDockify.Data.Models;

namespace OpenDockify.Api;

/// <summary>
/// Admin-only audit endpoints. Requires the <c>RequireAdmin</c> policy
/// (role Administrator). The <c>GET /audit</c> projection reads the
/// platform <see cref="AuditEvent"/> table; events older than the
/// configured retention window are excluded by the application
/// query (defense-in-depth alongside the recurring retention purge).
/// </summary>
public static class AuditEndpoints
{
    private const int _maximumPageSize = 200;
    private const int _defaultPageSize = 50;

    public static IEndpointRouteBuilder MapAuditEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/admin/audit").RequireAuthorization("RequireAdmin");

        group.MapGet("", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            int? page,
            int? pageSize,
            string? action,
            string? category,
            string? subject,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var normalizedPage = Math.Max(1, page ?? 1);
            var normalizedSize = Math.Clamp(pageSize ?? _defaultPageSize, 1, _maximumPageSize);

            var query = db.AuditEvents.AsNoTracking();
            if (from is { } fromBound)
                query = query.Where(a => a.OccurredAt >= fromBound.UtcDateTime);
            if (to is { } toBound)
                query = query.Where(a => a.OccurredAt <= toBound.UtcDateTime);
            if (!string.IsNullOrWhiteSpace(action))
                query = query.Where(a => a.Action == action);
            if (!string.IsNullOrWhiteSpace(category))
                query = query.Where(a => a.Category == category);
            if (!string.IsNullOrWhiteSpace(subject))
                query = query.Where(a => a.SubjectId == subject);

            var total = await query.LongCountAsync(ct);
            var items = await query
                .OrderByDescending(a => a.OccurredAt)
                .Skip((normalizedPage - 1) * normalizedSize)
                .Take(normalizedSize)
                .ToListAsync(ct);
            return Results.Ok(new
            {
                page = normalizedPage,
                pageSize = normalizedSize,
                total,
                items = items.Select(a => new
                {
                    id = a.Id,
                    action = a.Action,
                    category = a.Category,
                    outcome = a.Outcome,
                    severity = a.Severity,
                    occurredAt = a.OccurredAt,
                    correlationId = a.CorrelationId,
                    subjectId = a.SubjectId,
                    tenantId = a.TenantId,
                    source = a.Source,
                    metadata = ParseMetadata(a.MetadataJson),
                }),
            });
        });

        group.MapGet("/export", async (
            DateTimeOffset? from,
            DateTimeOffset? to,
            string? format,
            AppDbContext db,
            CancellationToken ct) =>
        {
            var query = db.AuditEvents.AsNoTracking();
            if (from is { } fromBound)
                query = query.Where(a => a.OccurredAt >= fromBound.UtcDateTime);
            if (to is { } toBound)
                query = query.Where(a => a.OccurredAt <= toBound.UtcDateTime);
            var rows = await query.OrderBy(a => a.OccurredAt).Take(10_000).ToListAsync(ct);
            var selected = (format ?? "jsonl").ToLowerInvariant();
            if (selected == "csv")
            {
                return Results.File(BuildCsv(rows), "text/csv", $"audit-{DateTime.UtcNow:yyyyMMddHHmmss}.csv");
            }
            return Results.File(BuildJsonl(rows), "application/x-ndjson", $"audit-{DateTime.UtcNow:yyyyMMddHHmmss}.jsonl");
        });

        return endpoints;
    }

    private static readonly char[] _csvSpecialCharacters = { ',', '"', '\n', '\r' };

    private static Dictionary<string, string>? ParseMetadata(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static byte[] BuildJsonl(IReadOnlyList<AuditEvent> rows)
    {
        using var ms = new MemoryStream();
        foreach (var row in rows)
        {
            using var writer = new Utf8JsonWriter(ms, new JsonWriterOptions { Indented = false });
            writer.WriteStartObject();
            writer.WriteString("id", row.Id.ToString());
            writer.WriteString("action", row.Action);
            writer.WriteString("category", row.Category);
            writer.WriteString("outcome", row.Outcome);
            writer.WriteString("severity", row.Severity);
            writer.WriteString("occurredAt", row.OccurredAt.ToString("o"));
            writer.WriteString("subjectId", row.SubjectId);
            writer.WriteString("tenantId", row.TenantId);
            writer.WriteString("source", row.Source);
            writer.WriteString("correlationId", row.CorrelationId);
            writer.WriteEndObject();
            writer.Flush();
            ms.WriteByte((byte)'\n');
        }
        return ms.ToArray();
    }

    private static byte[] BuildCsv(IReadOnlyList<AuditEvent> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("id,action,category,outcome,severity,occurredAt,subjectId,tenantId,source,correlationId");
        foreach (var row in rows)
        {
            sb.Append(row.Id).Append(',')
              .Append(Escape(row.Action)).Append(',')
              .Append(Escape(row.Category)).Append(',')
              .Append(Escape(row.Outcome)).Append(',')
              .Append(Escape(row.Severity)).Append(',')
              .Append(row.OccurredAt.ToString("o")).Append(',')
              .Append(Escape(row.SubjectId)).Append(',')
              .Append(Escape(row.TenantId)).Append(',')
              .Append(Escape(row.Source)).Append(',')
              .Append(Escape(row.CorrelationId))
              .AppendLine();
        }
        return System.Text.Encoding.UTF8.GetBytes(sb.ToString());
    }

    private static string Escape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;
        if (value.IndexOfAny(_csvSpecialCharacters) < 0)
            return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
