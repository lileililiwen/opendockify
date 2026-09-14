using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Issues signed JWTs (HMAC-SHA256) from config (<c>Jwt:Issuer</c>,
/// <c>Jwt:Audience</c>, <c>Jwt:Secret</c>, <c>Jwt:ExpiryMinutes</c>). Claims:
/// <c>sub</c> = user id, <c>role</c> = role.
/// </summary>
public sealed class JwtTokenService
{
    public const int MinSecretBytes = 32;

    private readonly IConfiguration _configuration;
    private readonly JsonWebTokenHandler _handler = new();

    public JwtTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
        ValidateConfig();
    }

    public string IssueToken(Guid userId, string role)
    {
        var secret = _configuration["Jwt:Secret"]
            ?? throw new InvalidOperationException("Jwt:Secret is not configured.");

        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(GetExpiryMinutes());

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            Subject = new ClaimsIdentity(
            [
                new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
                new Claim(ClaimTypes.Role, role),
            ]),
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            IssuedAt = now.UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
                SecurityAlgorithms.HmacSha256),
        };

        return _handler.CreateToken(descriptor);
    }

    public int GetExpiryMinutes()
    {
        return int.TryParse(_configuration["Jwt:ExpiryMinutes"], out var minutes)
            ? minutes
            : DefaultExpiryMinutes;
    }

    public const int DefaultExpiryMinutes = 15;

    private void ValidateConfig()
    {
        var secret = _configuration["Jwt:Secret"];
        if (string.IsNullOrEmpty(secret))
        {
            throw new InvalidOperationException(
                "Jwt:Secret is not configured. Set it in appsettings or via the Jwt__Secret environment variable. It MUST be at least 32 bytes long.");
        }

        if (Encoding.UTF8.GetByteCount(secret) < MinSecretBytes)
        {
            throw new InvalidOperationException(
                $"Jwt:Secret must be at least {MinSecretBytes} bytes long. For local development only, a sample value is provided in appsettings.Development.json.");
        }
    }
}
