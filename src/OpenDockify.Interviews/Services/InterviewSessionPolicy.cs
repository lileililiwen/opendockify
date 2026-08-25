using OpenDockify.Interviews.Models;

namespace OpenDockify.Interviews.Services;

public static class InterviewSessionPolicy
{
    public static bool CanAccess(InterviewSession session, Guid ownerId, DateTimeOffset now)
    {
        return session.OwnerId == ownerId && session.ExpiresAt > now;
    }

    public static bool MatchesTemplateRevision(InterviewSession session, DateTimeOffset templateUpdatedAt)
    {
        return session.TemplateRevisionStamp == templateUpdatedAt;
    }
}
