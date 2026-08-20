using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using OpenDockify.Auth.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class JwtTokenServiceTests
{
    private static IConfiguration Config(string? secret = null, string? expiry = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Jwt:Issuer"] = "test",
            ["Jwt:Audience"] = "test",
            ["Jwt:Secret"] = secret ?? "test-secret-that-is-at-least-thirty-two-bytes-long",
            ["Jwt:ExpiryMinutes"] = expiry ?? "480",
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    [Fact]
    public void IssueToken_includes_sub_and_role_claims()
    {
        var service = new JwtTokenService(Config());
        var token = service.IssueToken(Guid.Parse("092998d9-699f-4110-aed8-070e0c798daa"), "Administrator");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Contains(jwt.Claims, c => c.Type == JwtRegisteredClaimNames.Sub && c.Value == "092998d9-699f-4110-aed8-070e0c798daa");
        Assert.Contains(jwt.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Administrator");
        Assert.Equal("test", jwt.Issuer);
        Assert.Equal("test", jwt.Audiences.Single());
    }

    [Fact]
    public void IssueToken_honours_expiry_config()
    {
        var service = new JwtTokenService(Config(expiry: "1"));
        var token = service.IssueToken(Guid.NewGuid(), "Regular");

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddSeconds(30));
        Assert.True(jwt.ValidTo < DateTime.UtcNow.AddMinutes(5));
    }

    [Fact]
    public void Constructor_throws_when_secret_missing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(
            new Dictionary<string, string?> { ["Jwt:Secret"] = "" }).Build();

        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(config));
    }

    [Fact]
    public void Constructor_throws_when_secret_too_short()
    {
        Assert.Throws<InvalidOperationException>(() => new JwtTokenService(Config(secret: "too-short")));
    }
}
