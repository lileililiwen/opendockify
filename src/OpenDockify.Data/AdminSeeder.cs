using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Auth.Models;
using OpenDockify.Auth.Services;

namespace OpenDockify.Data;

/// <summary>
/// Seeds the initial administrator account when the Users table is empty.
/// Credentials come from <c>Seed:AdminUsername</c> / <c>Seed:AdminPassword</c>
/// (env-overridable; defaults admin/admin123 for local development only).
/// Idempotent: never creates a duplicate admin on restart.
///
/// Lives in OpenDockify.Data (the central reference) because it combines the
/// Auth module's entities/services with the seed registry — domain modules
/// must not reference OpenDockify.Data, so composite seeders live here.
/// </summary>
public sealed class AdminSeeder : IDbSeeder
{
    public int Order => 100;

    public async Task SeedAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var db = services.GetRequiredService<AppDbContext>();
        if (await db.Set<User>().AnyAsync(cancellationToken))
        {
            return;
        }

        var config = services.GetRequiredService<IConfiguration>();
        var account = services.GetRequiredService<AccountService>();

        var username = config["Seed:AdminUsername"]
            ?? throw new InvalidOperationException("Seed:AdminUsername is not configured.");
        var password = config["Seed:AdminPassword"]
            ?? throw new InvalidOperationException("Seed:AdminPassword is not configured.");

        await account.RegisterAsync(
            username,
            password,
            "Administrator",
            UserRole.Administrator,
            cancellationToken);
    }
}
