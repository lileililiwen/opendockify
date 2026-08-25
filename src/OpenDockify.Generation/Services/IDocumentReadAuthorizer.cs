using Microsoft.EntityFrameworkCore;

namespace OpenDockify.Generation.Services;

public sealed record DocumentReadAccess(bool Allowed, bool IsOwner, string AccessLevel)
{
    public static DocumentReadAccess Denied { get; } = new(false, false, "none");
}

public interface IDocumentReadAuthorizer
{
    Task<DocumentReadAccess> AuthorizeAsync(Guid userId, Guid documentId, CancellationToken cancellationToken);
}

public sealed class OwnerDocumentReadAuthorizer(Microsoft.EntityFrameworkCore.DbContext db) : IDocumentReadAuthorizer
{
    public async Task<DocumentReadAccess> AuthorizeAsync(Guid userId, Guid documentId, CancellationToken cancellationToken)
    {
        var owned = await db.Set<Models.Document>()
            .AnyAsync(x => x.Id == documentId && x.OwnerId == userId, cancellationToken);
        return owned ? new(true, true, "owner") : DocumentReadAccess.Denied;
    }
}
