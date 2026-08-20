namespace OpenDockify.Auth.Services;

/// <summary>
/// Identity of the currently authenticated user, resolved from the validated
/// JWT's <c>sub</c> (user id) and <c>role</c> claims. Services MUST derive the
/// acting user from this context, never from client-supplied ids.
/// </summary>
public sealed class CurrentUserContext
{
    public Guid UserId { get; }

    public string Role { get; }

    public bool IsAdministrator => Role.Equals("Administrator", StringComparison.OrdinalIgnoreCase);

    public CurrentUserContext(Guid userId, string role)
    {
        UserId = userId;
        Role = role;
    }
}
