using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using OpenDockify.AiAssist.Models;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// Persists AI usage logs with PII-redacted snippets (18-digit ID numbers and
/// long digit runs are masked before storage; raw values never persist).
/// </summary>
public sealed class AiUsageLogService(DbContext db)
{
    private static readonly Regex _sensitivePattern = new(
        @"\d{17}[\dXx]|\d{6,}",
        RegexOptions.Compiled,
        TimeSpan.FromSeconds(1));

    private const int _maxSnippetLength = 2000;

    public async Task LogAsync(
        Guid userId,
        string action,
        string? requestSnippet,
        string? responseSnippet,
        bool success,
        CancellationToken cancellationToken)
    {
        var log = new AiUsageLog
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Action = action,
            RequestSnippet = RedactSensitive(requestSnippet),
            ResponseSnippet = RedactSensitive(responseSnippet),
            Success = success,
            Timestamp = DateTime.UtcNow,
        };

        db.Set<AiUsageLog>().Add(log);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AiUsageLog>> ListRecentAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        return await db.Set<AiUsageLog>()
            .OrderByDescending(l => l.Timestamp)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public async Task<int> CountTodayAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var startOfDay = DateTime.UtcNow.Date;
        var startOfTomorrow = startOfDay.AddDays(1);

        return await db.Set<AiUsageLog>()
            .CountAsync(l => l.UserId == userId && l.Timestamp >= startOfDay && l.Timestamp < startOfTomorrow, cancellationToken);
    }

    public static string RedactSensitive(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var truncated = text.Length > _maxSnippetLength ? text[.._maxSnippetLength] : text;
        return _sensitivePattern.Replace(truncated, "***");
    }
}
