using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Platform.Persistence.EfCore.Migrator;

namespace OpenDockify.Data;

/// <summary>
/// Registers the platform <see cref="IMigrationRunner"/> against the
/// application-owned <see cref="AppDbContext"/>. The runner is the
/// boundary the API host, the CLI, and the readiness probe use to
/// inspect and apply EF Core migrations without owning the context
/// itself.
/// </summary>
public static class MigratorRegistration
{
    public static IServiceCollection AddPlatformMigrator(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IMigrationRunner, MigrationRunner>();
        return services;
    }

    /// <summary>
    /// Creates a fresh <see cref="AppDbContext"/> from the supplied
    /// service provider. Used by the migrator's request factory so the
    /// runner owns the context lifetime and the application owns its
    /// configuration.
    /// </summary>
    public static Task<DbContext> CreateAppContextAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var context = services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext();
        return Task.FromResult<DbContext>(context);
    }
}
