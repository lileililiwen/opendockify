using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.Data;
using Platform.Persistence.EfCore.Migrator;

namespace OpenDockify.Operations.Cli.Seeding;

/// <summary>
/// CLI-side adapter that bridges the platform
/// <see cref="IMigrationSeeder"/> contract to the application's
/// existing <see cref="ISeedRegistry"/>. The migrator hands back the
/// freshly-migrated <see cref="AppDbContext"/>; we use its service
/// provider to resolve the registered seeders and run them in the
/// same order the API host uses.
/// </summary>
internal sealed partial class SeedRegistrySeederAdapter : IMigrationSeeder
{
    private readonly ISeedRegistry _registry;
    private readonly ILogger _logger;

    public SeedRegistrySeederAdapter(ISeedRegistry registry, ILogger logger)
    {
        _registry = registry;
        _logger = logger;
    }

    public async Task SeedAsync(DbContext context, CancellationToken cancellationToken = default)
    {
        var appContext = context as AppDbContext
            ?? throw new InvalidOperationException("Seed context must be the application AppDbContext.");
        var scopeFactory = appContext.GetService<IServiceScopeFactory>()
            ?? throw new InvalidOperationException("Context has no IServiceScopeFactory.");
        await using var scope = scopeFactory.CreateAsyncScope();
        foreach (var seeder in _registry.Seeders)
        {
            Log.Running(_logger, seeder.GetType().Name);
            await seeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(1, LogLevel.Information, "Running seeder {Seeder}.")]
        public static partial void Running(ILogger logger, string seeder);
    }
}
