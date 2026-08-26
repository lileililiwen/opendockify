using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using OpenDockify.Integrations.Models;
using OpenDockify.Integrations.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class ServiceTokenTests : IDisposable
{
    private readonly string _databaseName = AutomationTestHarness.NewDatabaseName();
    private readonly SqliteConnection _connection;
    private readonly TestIntegrationsDbContext _db;
    private readonly ServiceTokenService _service;

    public ServiceTokenTests()
    {
        _connection = AutomationTestHarness.OpenConnection(_databaseName);
        _db = AutomationTestHarness.CreateContext(_connection);
        _db.Database.EnsureCreated();
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Integrations:TokenPepper"] = "unit-test-pepper",
        }).Build();
        _service = new ServiceTokenService(_db, config);
    }

    [Fact]
    public async Task Create_returns_one_time_clear_token_and_stores_only_a_verifier()
    {
        var owner = Guid.NewGuid();
        var issuance = await _service.CreateAsync(
            owner, "ci-runner", [AutomationScopes.DocumentsWrite], TimeSpan.FromDays(30));

        Assert.StartsWith("odk_", issuance.ClearToken);
        Assert.NotEqual(issuance.ClearToken, issuance.Token.TokenHash);
        Assert.Equal(64, issuance.Token.TokenHash.Length);
        Assert.Equal(owner, issuance.Token.OwnerId);

        var authenticated = await _service.AuthenticateAsync(issuance.ClearToken);
        Assert.NotNull(authenticated);
        Assert.Equal(issuance.Token.Id, authenticated.Token.Id);
        Assert.Equal(owner, authenticated.OwnerId);
        Assert.Equal([AutomationScopes.DocumentsWrite], authenticated.Scopes);

        // The clear value must not be recoverable from any stored column.
        var stored = await _db.ServiceTokens.SingleAsync(x => x.Id == issuance.Token.Id);
        Assert.DoesNotContain(issuance.ClearToken, new[] { stored.TokenHash, stored.Prefix });
    }

    [Fact]
    public async Task Authenticate_rejects_unknown_tampered_and_expired_tokens()
    {
        var issuance = await Issue();

        Assert.Null(await _service.AuthenticateAsync("odk_totally-unknown-value"));
        Assert.Null(await _service.AuthenticateAsync(issuance.ClearToken[..^2] + "zz"));
        Assert.Null(await _service.AuthenticateAsync(string.Empty));

        var expired = await _service.CreateAsync(
            Guid.NewGuid(), "expired", [AutomationScopes.TemplatesRead], TimeSpan.FromDays(1));
        expired.Token.ExpiresAtUtc = DateTime.UtcNow.AddHours(-1);
        await _db.SaveChangesAsync();
        Assert.Null(await _service.AuthenticateAsync(expired.ClearToken));
    }

    [Fact]
    public async Task Revocation_is_immediate_and_owner_scoped()
    {
        var owner = Guid.NewGuid();
        var issuance = await _service.CreateAsync(
            owner, "revocable", [AutomationScopes.DocumentsPreview], TimeSpan.FromDays(1));

        Assert.False(await _service.RevokeAsync(Guid.NewGuid(), issuance.Token.Id));
        Assert.NotNull(await _service.AuthenticateAsync(issuance.ClearToken));

        Assert.True(await _service.RevokeAsync(owner, issuance.Token.Id));
        Assert.Null(await _service.AuthenticateAsync(issuance.ClearToken));
        Assert.False(await _service.RevokeAsync(owner, issuance.Token.Id));
    }

    [Fact]
    public async Task Invalid_scope_names_are_rejected_at_creation()
    {
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            Guid.NewGuid(), "bad", ["documents:delete"], TimeSpan.FromDays(1)));
        await Assert.ThrowsAsync<ArgumentException>(() => _service.CreateAsync(
            Guid.NewGuid(), "empty", [], TimeSpan.FromDays(1)));
    }

    [Fact]
    public async Task Listing_is_owner_scoped_redacted_and_tracks_usage()
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var mine = await _service.CreateAsync(owner, "mine", [AutomationScopes.OperationsRead], TimeSpan.FromDays(7));
        await _service.CreateAsync(other, "theirs", [AutomationScopes.OperationsRead], TimeSpan.FromDays(7));

        await _service.AuthenticateAsync(mine.ClearToken);
        await _service.AuthenticateAsync(mine.ClearToken);

        var views = await _service.ListAsync(owner);
        var view = Assert.Single(views);
        Assert.Equal("mine", view.Name);
        Assert.Equal(mine.Token.Prefix, view.Prefix);
        Assert.Equal(2, view.UseCount);
        Assert.NotNull(view.LastUsedAtUtc);

        var json = JsonSerializer.Serialize(views, AutomationTestHarness.SerializerOptions);
        Assert.DoesNotContain("hash", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("secret", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("odk_", json, StringComparison.Ordinal);
    }

    private async Task<ServiceTokenIssuance> Issue()
    {
        return await _service.CreateAsync(
        Guid.NewGuid(), "issue", [AutomationScopes.TemplatesRead], TimeSpan.FromDays(1));
    }

    public void Dispose()
    {
        _db.Dispose();
    }
}
