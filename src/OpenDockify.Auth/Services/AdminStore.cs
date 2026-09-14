using Microsoft.EntityFrameworkCore;
using OpenDockify.Auth.Models;
using Platform.Admin.Contracts;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Application-owned <see cref="IAdminStore"/> backed by the
/// <c>Users</c> table. Implements the read projections required by the
/// <c>Admin.Contracts</c> catalog plus the <c>enable/disable</c>
/// mutation used by the operators.
/// </summary>
public sealed class AdminStore : IAdminStore
{
    private const int _maximumPageSize = 100;

    private readonly DbContext _db;
    private readonly TimeProvider _time;

    public AdminStore(DbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async ValueTask<AdminPage<AdminUser>> GetUsersAsync(AdminQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query.Normalize(_maximumPageSize, new HashSet<string>(StringComparer.Ordinal) { "username", "created" });
        var dbQuery = _db.Set<User>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = $"%{normalized.Search}%";
            dbQuery = dbQuery.Where(u => EF.Functions.Like(u.Username, term) || EF.Functions.Like(u.DisplayName, term));
        }
        dbQuery = normalized.SortBy switch
        {
            "created" => normalized.Descending
                ? dbQuery.OrderByDescending(u => u.CreatedAt)
                : dbQuery.OrderBy(u => u.CreatedAt),
            _ => normalized.Descending
                ? dbQuery.OrderByDescending(u => u.Username)
                : dbQuery.OrderBy(u => u.Username),
        };
        var total = await dbQuery.LongCountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(u => new AdminUser(
                u.Id.ToString(),
                u.Username,
                u.DisplayName,
                null,
                !u.IsDisabled,
                new[] { u.Role.ToString() }))
            .ToListAsync(cancellationToken);
        return new AdminPage<AdminUser>(items, normalized.Page, normalized.PageSize, total);
    }

    public ValueTask<AdminPage<AdminRole>> GetRolesAsync(AdminQuery query, CancellationToken cancellationToken = default)
    {
        var items = new List<AdminRole>
        {
            new("administrator", "Administrator", null, Array.Empty<string>()),
            new("regular", "Regular", null, Array.Empty<string>()),
        };
        IReadOnlyList<AdminRole> readOnly = items;
        return ValueTask.FromResult(new AdminPage<AdminRole>(readOnly, 1, items.Count, items.Count));
    }

    public async ValueTask<IReadOnlyList<AdminPermission>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        var items = await Task.FromResult(new[]
        {
            new AdminPermission(AdminPermissions.UsersRead, "users", "read"),
            new AdminPermission(AdminPermissions.UsersManage, "users", "manage"),
        });
        return items;
    }

    public ValueTask<AdminPage<AdminSession>> GetSessionsAsync(AdminQuery query, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new AdminPage<AdminSession>(Array.Empty<AdminSession>(), 1, _maximumPageSize, 0));
    }

    public async ValueTask<AdminPage<AdminAuditEntry>> GetAuditAsync(AdminQuery query, CancellationToken cancellationToken = default)
    {
        var normalized = query.Normalize(_maximumPageSize, new HashSet<string>(StringComparer.Ordinal) { "occurred", "action" });
        var dbQuery = _db.Set<Models.IdentityAuditEvent>().AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalized.Search))
        {
            var term = $"%{normalized.Search}%";
            dbQuery = dbQuery.Where(a => EF.Functions.Like(a.Action, term));
        }
        dbQuery = normalized.SortBy switch
        {
            "action" => normalized.Descending
                ? dbQuery.OrderByDescending(a => a.Action)
                : dbQuery.OrderBy(a => a.Action),
            _ => normalized.Descending
                ? dbQuery.OrderByDescending(a => a.OccurredAt)
                : dbQuery.OrderBy(a => a.OccurredAt),
        };
        var total = await dbQuery.LongCountAsync(cancellationToken);
        var items = await dbQuery
            .Skip((normalized.Page - 1) * normalized.PageSize)
            .Take(normalized.PageSize)
            .Select(a => new AdminAuditEntry(
                a.Id.ToString(),
                a.Action,
                a.SubjectId ?? string.Empty,
                a.SubjectId,
                null,
                a.OccurredAt,
                null,
                a.Metadata,
                null))
            .ToListAsync(cancellationToken);
        return new AdminPage<AdminAuditEntry>(items, normalized.Page, normalized.PageSize, total);
    }

    public ValueTask<IReadOnlyList<AdminProviderStatus>> GetProviderStatusesAsync(CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult<IReadOnlyList<AdminProviderStatus>>(new[]
        {
            new AdminProviderStatus("local", true, "Local credential provider"),
        });
    }

    public ValueTask<AdminPage<AdminSubscriptionSummary>> GetSubscriptionSummariesAsync(AdminQuery query, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(new AdminPage<AdminSubscriptionSummary>(Array.Empty<AdminSubscriptionSummary>(), 1, _maximumPageSize, 0));
    }

    public async ValueTask<AdminMutationResult> SetUserEnabledAsync(
        string userId,
        bool enabled,
        string actorId,
        string? tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!Guid.TryParse(userId, out var id))
        {
            return AdminMutationResult.Failure("invalid_id");
        }

        var user = await _db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user == null)
        {
            return AdminMutationResult.Failure("not_found");
        }

        if (user.IsDisabled == !enabled)
        {
            return AdminMutationResult.Success();
        }

        user.IsDisabled = !enabled;
        await _db.SaveChangesAsync(cancellationToken);

        if (!enabled)
        {
            var store = new RefreshTokenStore(_db, _time);
            await store.RevokeUserFamiliesAsync(user.Id, "admin_disabled", cancellationToken);
        }

        return AdminMutationResult.Success();
    }

    public ValueTask<AdminMutationResult> RevokeSessionAsync(string sessionId, string actorId, string? tenantId, CancellationToken cancellationToken = default)
    {
        return ValueTask.FromResult(AdminMutationResult.Success());
    }
}
