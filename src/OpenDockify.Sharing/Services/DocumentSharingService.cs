using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Models;
using OpenDockify.Generation.Models;
using OpenDockify.Generation.Services;
using OpenDockify.Sharing.Models;
using OpenDockify.SystemConfig.Services;
using OpenDockify.Templates.Models;

namespace OpenDockify.Sharing.Services;

public sealed record ShareOperationResult(bool Succeeded, bool NotFound, string? Error);
public sealed record CreatedShareLink(Guid Id, string Token, DateTimeOffset ExpiresAt, bool AllowDownload);
public sealed record PublicDocumentView(Guid LinkId, string Title, string TemplateName, string RenderedText, DateTime CreatedAt, bool AllowDownload, string PdfPath);
public sealed record ShareAuditPage(IReadOnlyList<ShareAuditEvent> Items, int Page, int PageSize, int TotalCount);
public sealed record ShareListItem(string Kind, Guid Id, string? Username, string? AccessLevel, bool? AllowDownload,
    DateTimeOffset? ExpiresAt, DateTimeOffset CreatedAt, DateTimeOffset? RevokedAt);

public sealed class SharingDocumentReadAuthorizer(DbContext db) : IDocumentReadAuthorizer
{
    public async Task<DocumentReadAccess> AuthorizeAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var owner = await db.Set<Document>().AnyAsync(x => x.Id == documentId && x.OwnerId == userId, cancellationToken);
        if (owner)
            return new(true, true, "owner");
        var level = await (from grant in db.Set<DocumentGrant>()
                           join document in db.Set<Document>() on grant.DocumentId equals document.Id
                           where grant.DocumentId == documentId && grant.GranteeId == userId && grant.RevokedAt == null
                           select (DocumentGrantLevel?)grant.AccessLevel).FirstOrDefaultAsync(cancellationToken);
        if (level is null)
        {
            if (await db.Set<Document>().AnyAsync(x => x.Id == documentId, cancellationToken))
            {
                db.Set<ShareAuditEvent>().Add(Audit(documentId, "authenticated-denied", "authenticated", userId, "authenticated", false));
                await db.SaveChangesAsync(cancellationToken);
            }
            return DocumentReadAccess.Denied;
        }
        db.Set<ShareAuditEvent>().Add(Audit(documentId, "authenticated-read", "grantee", userId, "authenticated", true));
        await db.SaveChangesAsync(cancellationToken);
        return new(true, false, level == DocumentGrantLevel.Review ? "review" : "view");
    }

    private static ShareAuditEvent Audit(Guid? documentId, string action, string category, Guid? actorId, string client, bool succeeded)
    {
        return new()
        { Id = Guid.NewGuid(), DocumentId = documentId, Action = action, ActorCategory = category, ActorId = actorId, CoarseClient = client, Succeeded = succeeded };
    }
}

public sealed class DocumentSharingService(DbContext db, ISystemConfigReader settings, IConfiguration configuration)
{
    public async Task<(DocumentGrant? Grant, string? Error, bool NotFound)> CreateGrantAsync(Guid ownerId, Guid documentId, string username, string level, CancellationToken ct)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return (null, null, true);
        var grantee = await db.Set<User>().SingleOrDefaultAsync(x => x.Username == username, ct);
        if (grantee is null)
            return (null, "User not found.", false);
        if (grantee.Id == ownerId)
            return (null, "The owner already has access.", false);
        if (!Enum.TryParse<DocumentGrantLevel>(level, true, out var parsed))
            return (null, "Access level must be view or review.", false);
        var active = await db.Set<DocumentGrant>().Where(x => x.DocumentId == documentId && x.GranteeId == grantee.Id && x.RevokedAt == null).ToListAsync(ct);
        foreach (var item in active)
            item.RevokedAt = DateTimeOffset.UtcNow;
        var grant = new DocumentGrant { Id = Guid.NewGuid(), DocumentId = documentId, OwnerId = ownerId, GranteeId = grantee.Id, AccessLevel = parsed };
        db.Set<DocumentGrant>().Add(grant);
        db.Set<ShareAuditEvent>().Add(Audit(documentId, "grant-created", "owner", ownerId, "authenticated", true));
        await db.SaveChangesAsync(ct);
        return (grant, null, false);
    }

    public async Task<ShareOperationResult> RevokeGrantAsync(Guid ownerId, Guid documentId, Guid grantId, CancellationToken ct)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return new(false, true, null);
        var grant = await db.Set<DocumentGrant>().SingleOrDefaultAsync(x => x.Id == grantId && x.DocumentId == documentId && x.OwnerId == ownerId, ct);
        if (grant is null)
            return new(false, true, null);
        grant.RevokedAt ??= DateTimeOffset.UtcNow;
        db.Set<ShareAuditEvent>().Add(Audit(documentId, "grant-revoked", "owner", ownerId, "authenticated", true));
        await db.SaveChangesAsync(ct);
        return new(true, false, null);
    }

    public async Task<IReadOnlyList<ShareListItem>?> ListSharesAsync(Guid ownerId, Guid documentId, CancellationToken ct)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return null;
        var grants = await (from g in db.Set<DocumentGrant>()
                            join u in db.Set<User>() on g.GranteeId equals u.Id
                            where g.DocumentId == documentId
                            select new ShareListItem("grant", g.Id, u.Username, g.AccessLevel == DocumentGrantLevel.Review ? "review" : "view", null, null, g.CreatedAt, g.RevokedAt)).ToListAsync(ct);
        var links = await db.Set<ExternalShareLink>().Where(x => x.DocumentId == documentId)
            .Select(x => new ShareListItem("link", x.Id, null, null, x.AllowDownload, x.ExpiresAt, x.CreatedAt, x.RevokedAt)).ToListAsync(ct);
        return grants.Concat(links).OrderByDescending(x => x.CreatedAt).ToList();
    }

    public async Task<(CreatedShareLink? Link, string? Error, bool NotFound)> CreateLinkAsync(Guid ownerId, Guid documentId, int lifetimeHours, bool allowDownload, CancellationToken ct, string? password = null)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return (null, null, true);
        if (!await IsEnabledAsync(ct))
            return (null, "External sharing is disabled.", false);
        var max = await settings.GetAsync<int>(SettingKeys.SharingMaximumLifetimeHours, ct);
        if (max <= 0)
            max = 72;
        if (lifetimeHours < 1 || lifetimeHours > max)
            return (null, $"Lifetime must be between 1 and {max} hours.", false);
        if (password is not null && (password.Length < 8 || password.Length > 128))
            return (null, "Link password must be 8-128 characters.", false);
        var secret = RandomNumberGenerator.GetBytes(32);
        var entity = new ExternalShareLink { Id = Guid.NewGuid(), DocumentId = documentId, OwnerId = ownerId, SecretHash = Hash(secret), AllowDownload = allowDownload, ExpiresAt = DateTimeOffset.UtcNow.AddHours(lifetimeHours) };
        if (password is not null)
        {
            var (hash, salt) = ShareLinkPasswordHelper.HashNew(password);
            entity.PasswordHash = hash;
            entity.PasswordSalt = salt;
        }
        db.Set<ExternalShareLink>().Add(entity);
        db.Set<ShareAuditEvent>().Add(Audit(documentId, "link-created", "owner", ownerId, "authenticated", true));
        await db.SaveChangesAsync(ct);
        return (new(entity.Id, $"{entity.Id:N}.{Base64Url(secret)}", entity.ExpiresAt, allowDownload), null, false);
    }

    public async Task<ShareOperationResult> RevokeLinkAsync(Guid ownerId, Guid documentId, Guid linkId, CancellationToken ct)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return new(false, true, null);
        var link = await db.Set<ExternalShareLink>().SingleOrDefaultAsync(x => x.Id == linkId && x.DocumentId == documentId && x.OwnerId == ownerId, ct);
        if (link is null)
            return new(false, true, null);
        link.RevokedAt ??= DateTimeOffset.UtcNow;
        db.Set<ShareAuditEvent>().Add(Audit(documentId, "link-revoked", "owner", ownerId, "authenticated", true));
        await db.SaveChangesAsync(ct);
        return new(true, false, null);
    }

    public async Task<PublicDocumentView?> ResolvePublicAsync(string token, bool download, string coarseClient, CancellationToken ct, string? password = null)
    {
        if (!await IsEnabledAsync(ct) || !TryParseToken(token, out var id, out var secret))
        { await DeniedAsync(null, coarseClient, ct); return null; }
        var link = await db.Set<ExternalShareLink>().SingleOrDefaultAsync(x => x.Id == id, ct);
        var valid = link is not null && link.RevokedAt is null && link.ExpiresAt > DateTimeOffset.UtcNow && (!download || link.AllowDownload)
            && CryptographicOperations.FixedTimeEquals(link.SecretHash, Hash(secret));
        if (!valid)
        { await DeniedAsync(link?.DocumentId, coarseClient, ct); return null; }
        var activeLink = link!;
        if (activeLink.PasswordLockedUntil is not null && activeLink.PasswordLockedUntil > DateTimeOffset.UtcNow)
        { await DeniedAsync(activeLink.DocumentId, coarseClient, ct); return null; }
        if (activeLink.PasswordHash is not null)
        {
            if (string.IsNullOrEmpty(password) || activeLink.PasswordSalt is null
                || !ShareLinkPasswordHelper.Verify(password, activeLink.PasswordHash, activeLink.PasswordSalt))
            {
                activeLink.PasswordFailedAttempts++;
                if (activeLink.PasswordFailedAttempts >= 5)
                {
                    activeLink.PasswordLockedUntil = DateTimeOffset.UtcNow.AddMinutes(15);
                    activeLink.PasswordFailedAttempts = 0;
                }
                await db.SaveChangesAsync(ct);
                await DeniedAsync(activeLink.DocumentId, coarseClient, ct);
                return null;
            }

            activeLink.PasswordFailedAttempts = 0;
            activeLink.PasswordLockedUntil = null;
            await db.SaveChangesAsync(ct);
        }
        var view = await (from d in db.Set<Document>()
                          join t in db.Set<Template>() on d.TemplateId equals t.Id
                          where d.Id == activeLink.DocumentId
                          select new PublicDocumentView(activeLink.Id, d.Title == "" ? t.Name : d.Title, t.Name, d.RenderedText, d.CreatedAt, activeLink.AllowDownload, d.PdfPath)).SingleOrDefaultAsync(ct);
        if (view is null)
        { await DeniedAsync(activeLink.DocumentId, coarseClient, ct); return null; }
        db.Set<ShareAuditEvent>().Add(Audit(activeLink.DocumentId, download ? "public-download" : "public-view", "external", null, coarseClient, true));
        await db.SaveChangesAsync(ct);
        return view;
    }

    public async Task<ShareAuditPage?> GetAuditAsync(Guid ownerId, Guid documentId, int page, int pageSize, CancellationToken ct)
    {
        if (!await IsOwnerAsync(ownerId, documentId, ct))
            return null;
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var query = db.Set<ShareAuditEvent>().Where(x => x.DocumentId == documentId);
        var total = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtcTicks).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(items, page, pageSize, total);
    }

    private Task<bool> IsOwnerAsync(Guid ownerId, Guid documentId, CancellationToken ct)
    {
        return db.Set<Document>().AnyAsync(x => x.Id == documentId && x.OwnerId == ownerId, ct);
    }

    private async Task<bool> IsEnabledAsync(CancellationToken ct)
    {
        return await settings.GetAsync<bool>(SettingKeys.SharingEnabled, ct);
    }

    private async Task DeniedAsync(Guid? documentId, string client, CancellationToken ct) { db.Set<ShareAuditEvent>().Add(Audit(documentId, "public-denied", "external", null, client, false)); await db.SaveChangesAsync(ct); }
    private byte[] Hash(byte[] secret)
    {
        // Sharing:HashKey is required (no JWT-secret fallback per notify-rate-quota).
        var key = configuration["Sharing:HashKey"];
        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException("Sharing:HashKey is required. Set Sharing__HashKey to a secret distinct from Jwt:Secret.");
        }

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(key), secret);
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static bool TryParseToken(string token, out Guid id, out byte[] secret)
    {
        id = Guid.Empty;
        secret = [];
        var parts = token.Split('.', 2);
        if (parts.Length != 2 || !Guid.TryParseExact(parts[0], "N", out id))
            return false;
        try
        {
            var value = parts[1].Replace('-', '+').Replace('_', '/');
            value += new string('=', (4 - value.Length % 4) % 4);
            secret = Convert.FromBase64String(value);
            return secret.Length == 32;
        }
        catch (FormatException)
        {
            return false;
        }
    }
    private static ShareAuditEvent Audit(Guid? documentId, string action, string category, Guid? actorId, string client, bool succeeded)
    {
        return new() { Id = Guid.NewGuid(), DocumentId = documentId, Action = action, ActorCategory = category, ActorId = actorId, CoarseClient = client.Length > 128 ? client[..128] : client, Succeeded = succeeded };
    }
}
