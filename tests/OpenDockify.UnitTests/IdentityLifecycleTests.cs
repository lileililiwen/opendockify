using System.Diagnostics.CodeAnalysis;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OpenDockify.Auth.Models;
using OpenDockify.Auth.Services;
using Platform.Identity.AspNetCore;
using Platform.Identity.Contracts;
using Xunit;
using PlatformAuditEvent = Platform.Identity.Contracts.IdentityAuditEvent;

namespace OpenDockify.UnitTests;

/// <summary>
/// Unit tests for the identity-lifecycle services introduced by
/// <c>platform-identity-hardening</c>. Tests run against a real SQLite
/// in-memory database; the platform's contract types are exercised end to
/// end (issue → rotate → reuse → revoke).
/// </summary>
public sealed class IdentityLifecycleTests
{
    [Fact]
    public void TotpValidator_round_trip_within_clock_window()
    {
        var secret = TotpValidator.NewSecret();
        var step = TotpValidator.DefaultStep;
        var counter = DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (long)step.TotalSeconds;
        var code = TestOtp(secret, counter, TotpValidator.DefaultDigits);

        Assert.True(TotpValidator.Validate(secret, code, toleranceSteps: 0));
        Assert.False(TotpValidator.Validate(secret, "000000"));
    }

    [Fact]
    public void TotpValidator_rejects_wrong_length()
    {
        var secret = TotpValidator.NewSecret();
        Assert.False(TotpValidator.Validate(secret, "abc"));
        Assert.False(TotpValidator.Validate(string.Empty, "000000"));
    }

    [Fact]
    public async Task Refresh_rotate_reuse_revoke_family()
    {
        await using var fixture = new IdentityFixture();
        var (user, _) = await fixture.SeedUserAsync();
        var refresh = fixture.Services.GetRequiredService<IRefreshTokenService>();
        var audit = fixture.Services.GetRequiredService<IIdentityAuditHook>();

        var initial = await refresh.IssueAsync(user.Id.ToString(), "session-1", DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(initial.Succeeded);

        var rotated = await refresh.RotateAsync(initial.Value!.Handle);
        Assert.True(rotated.Succeeded);
        Assert.NotEqual(initial.Value.Handle, rotated.Value!.Handle);

        // Reuse of the original handle must revoke the family and report replayed.
        var reuse = await refresh.RotateAsync(initial.Value.Handle);
        Assert.False(reuse.Succeeded);
        Assert.Equal(IdentityLifecycleOutcome.Replayed, reuse.Outcome);

        // Subsequent rotation with the still-live rotated handle must fail (family revoked).
        var secondRotation = await refresh.RotateAsync(rotated.Value.Handle);
        Assert.False(secondRotation.Succeeded);

        await audit.RecordAsync(new PlatformAuditEvent("test.audit", user.Id.ToString(), true, DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task Logout_revokes_token_family()
    {
        await using var fixture = new IdentityFixture();
        var (user, _) = await fixture.SeedUserAsync();
        var refresh = fixture.Services.GetRequiredService<IRefreshTokenService>();

        var initial = await refresh.IssueAsync(user.Id.ToString(), "session-2", DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(initial.Succeeded);

        var revoke = await refresh.RevokeAsync(initial.Value!.Handle);
        Assert.Equal(IdentityLifecycleOutcome.Succeeded, revoke);

        var rotate = await refresh.RotateAsync(initial.Value.Handle);
        Assert.False(rotate.Succeeded);
    }

    [Fact]
    public async Task Recovery_initiate_is_enumeration_safe()
    {
        await using var fixture = new IdentityFixture();
        var (user, _) = await fixture.SeedUserAsync();
        var recovery = fixture.Services.GetRequiredService<IPasswordRecoveryService>();

        var known = await recovery.InitiateAsync(user.Username, default);
        var unknown = await recovery.InitiateAsync("ghost-user-xyz", default);
        Assert.True(known.Succeeded);
        Assert.True(unknown.Succeeded);
        Assert.NotEqual(known.Value!.ChallengeId, unknown.Value!.ChallengeId);
    }

    [Fact]
    public async Task Lockout_blocks_after_configured_failures()
    {
        await using var fixture = new IdentityFixture();
        var configuration = fixture.Services.GetRequiredService<IConfiguration>();
        var lockout = fixture.Services.GetRequiredService<LockoutService>();
        var username = "lockout-target";
        var ip = "10.0.0.1";

        for (var i = 0; i < LockoutService.DefaultMaxFailures; i++)
        {
            await lockout.RecordFailureAsync(username, ip, default);
        }

        Assert.True(await lockout.IsLockedOutAsync(username, ip, configuration, default));
        Assert.False(await lockout.IsLockedOutAsync(username, "10.0.0.2", configuration, default));
    }

    [Fact]
    public async Task Disabled_user_cannot_authenticate()
    {
        await using var fixture = new IdentityFixture();
        var (user, _) = await fixture.SeedUserAsync();
        var account = fixture.Services.GetRequiredService<AccountService>();
        user.IsDisabled = true;
        await fixture.DbContext.SaveChangesAsync();

        var result = await account.ValidateCredentialsAsync(user.Username, "test-password-123", default);
        Assert.Null(result);
    }

    [Fact]
    public async Task Password_change_revokes_active_refresh_family()
    {
        await using var fixture = new IdentityFixture();
        var (user, _) = await fixture.SeedUserAsync();
        var refresh = fixture.Services.GetRequiredService<IRefreshTokenService>();
        var store = fixture.Services.GetRequiredService<RefreshTokenStore>();
        var account = fixture.Services.GetRequiredService<AccountService>();

        var initial = await refresh.IssueAsync(user.Id.ToString(), "session-3", DateTimeOffset.UtcNow.AddDays(1));
        Assert.True(initial.Succeeded);

        var set = await account.SetPasswordAsync(user, "rotated-password-456", default);
        Assert.True(set.Succeeded);
        await store.RevokeUserFamiliesAsync(user.Id, "password_changed", default);

        var rotate = await refresh.RotateAsync(initial.Value!.Handle);
        Assert.False(rotate.Succeeded);
    }

    [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms", Justification = "TOTP per RFC 6238.")]
    private static string TestOtp(string base32Secret, long counter, int digits)
    {
        var key = Base32Decode(base32Secret);
        Span<byte> counterBytes = stackalloc byte[8];
        var c = counter;
        for (var i = 0; i < 8; i++)
        {
            counterBytes[7 - i] = (byte)(c & 0xFF);
            c >>= 8;
        }
        using var hmac = new System.Security.Cryptography.HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var modulus = (int)Math.Pow(10, digits);
        return (binary % modulus).ToString($"D{digits}", System.Globalization.CultureInfo.InvariantCulture);
    }

    private const string _base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static byte[] Base32Decode(string value)
    {
        var normalized = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var c in normalized)
        {
            var index = _base32Alphabet.IndexOf(c);
            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}

/// <summary>
/// In-memory SQLite harness that wires the OpenDockify.Auth module's
/// services against a real DbContext. The auth-module configuration is
/// provided through in-memory configuration (no JWT secret required — we
/// only exercise the lifecycle contracts).
/// </summary>
internal sealed class IdentityFixture : IAsyncDisposable
{
    public ServiceProvider Services { get; }
    public TestIdentityDbContext DbContext { get; }
    public SqliteConnection Connection { get; }

    public IdentityFixture()
    {
        Connection = new SqliteConnection($"Data Source=file:identity-{Guid.NewGuid():N}?mode=memory&cache=shared");
        Connection.Open();

        var options = new DbContextOptionsBuilder<TestIdentityDbContext>()
            .UseSqlite(Connection)
            .Options;
        DbContext = new TestIdentityDbContext(options);
        DbContext.Database.EnsureCreated();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Auth:LockoutMaxFailures"] = "5",
                ["Auth:LockoutWindowMinutes"] = "15",
                ["Auth:RecoveryTokenMinutes"] = "30",
            })
            .Build());
        services.AddScoped(_ => (DbContext)DbContext);
        services.AddScoped<AccountService>();
        services.AddScoped<LockoutService>();
        services.AddScoped<RefreshTokenStore>();
        services.AddScoped<IdentityAuditHook>();
        services.AddSingleton<SessionStore>();
        services.AddScoped<PasswordRecoveryService>();
        services.AddScoped<TwoFactorService>();
        services.AddScoped<AdminStore>();
        services.AddSingleton<IIdentityAuditHook>(sp => sp.GetRequiredService<IdentityAuditHook>());
        services.AddSingleton<ISessionStore>(sp => sp.GetRequiredService<SessionStore>());
        services.AddSingleton<IRefreshTokenStore>(sp => sp.GetRequiredService<RefreshTokenStore>());
        services.AddSingleton<IPasswordRecoveryService>(sp => sp.GetRequiredService<PasswordRecoveryService>());
        services.AddSingleton<ITwoFactorService>(sp => sp.GetRequiredService<TwoFactorService>());
        services.AddPlatformIdentityLifecycle();
        Services = services.BuildServiceProvider();
    }

    public async Task<(User User, string Password)> SeedUserAsync()
    {
        var password = "test-password-123";
        var account = Services.GetRequiredService<AccountService>();
        var result = await account.RegisterAsync(
            $"user-{Guid.NewGuid():N}".Substring(0, 20),
            password,
            "Test",
            UserRole.Regular,
            default);
        Assert.True(result.Succeeded, result.Error);
        return (result.User!, password);
    }

    public async ValueTask DisposeAsync()
    {
        await DbContext.DisposeAsync();
        await Services.DisposeAsync();
        await Connection.DisposeAsync();
    }
}

internal sealed class TestIdentityDbContext(DbContextOptions<TestIdentityDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<OpenDockify.Auth.Models.RefreshToken> RefreshTokens => Set<OpenDockify.Auth.Models.RefreshToken>();
    public DbSet<RecoveryToken> RecoveryTokens => Set<RecoveryToken>();
    public DbSet<TwoFactorSecret> TwoFactorSecrets => Set<TwoFactorSecret>();
    public DbSet<OpenDockify.Auth.Models.TwoFactorChallenge> TwoFactorChallenges => Set<OpenDockify.Auth.Models.TwoFactorChallenge>();
    public DbSet<LoginAttempt> LoginAttempts => Set<LoginAttempt>();
    public DbSet<OpenDockify.Auth.Models.IdentityAuditEvent> IdentityAuditEvents => Set<OpenDockify.Auth.Models.IdentityAuditEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(User).Assembly);
    }
}
