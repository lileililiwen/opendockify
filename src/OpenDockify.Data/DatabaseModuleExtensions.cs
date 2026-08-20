using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace OpenDockify.Data;

/// <summary>
/// Composition-root entry point for the Data module. Registers the EF Core
/// <see cref="AppDbContext"/> for the provider selected by
/// <c>Database:Provider</c> (SQLite default) and exposes the startup
/// migrate-and-seed routine.
/// </summary>
public static class DatabaseModuleExtensions
{
    /// <summary>
    /// Registers <see cref="AppDbContext"/> against the configured provider.
    /// SQLite is the default (zero external service); Postgres, MySQL/MariaDB,
    /// and SQL Server are selectable by configuration with no recompile.
    /// </summary>
    public static IServiceCollection AddDatabaseModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = DatabaseProviderRegistry.Resolve(configuration);
        var connectionString = configuration.GetConnectionString("Default")
            ?? DefaultConnectionString(provider);
        var mySqlVersion = configuration["Database:MySqlServerVersion"];

        services.AddDbContext<AppDbContext>(builder =>
            DatabaseProviderRegistry.Configure(builder, provider, connectionString, mySqlVersion));

        // Module services depend on the base DbContext (never OpenDockify.Data)
        // per Agents.md §2; resolve it to the AppDbContext instance.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<ISeedRegistry, SeedRegistry>();

        return services;
    }

    /// <summary>
    /// Applies EF Core migrations at startup, then runs every registered
    /// seeder. Each seeder MUST be idempotent (a no-op when its data already
    /// exists). Self-hosters should not need to run <c>dotnet ef</c> manually.
    /// </summary>
    public static async Task MigrateAndSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
            .CreateLogger("OpenDockify.Data");

        await db.Database.MigrateAsync(cancellationToken);

        foreach (var seeder in scope.ServiceProvider.GetRequiredService<ISeedRegistry>().Seeders)
        {
            _runningSeeder(logger, seeder.GetType().Name, null);
            await seeder.SeedAsync(scope.ServiceProvider, cancellationToken);
        }
    }

    private static readonly Action<ILogger, string, Exception?> _runningSeeder =
        LoggerMessage.Define<string>(
            LogLevel.Information,
            new EventId(1, "RunningSeeder"),
            "Running seeder {Seeder}.");

    // These defaults are for local development only and are documented as such
    // in the README ("Quick start"). Production deployments MUST provide a real
    // connection string via ConnectionStrings:Default or the environment.
#pragma warning disable S2068 // hard-coded credential: local dev default, not a real secret
    private static string DefaultConnectionString(DatabaseProvider provider)
    {
        return provider switch
        {
            DatabaseProvider.Sqlite => "Data Source=opendockify.db",
            DatabaseProvider.Postgres => "Host=localhost;Database=opendockify;Username=opendockify;Password=opendockify_dev",
            DatabaseProvider.MySql => "Server=localhost;Database=opendockify;User=opendockify;Password=opendockify_dev",
            DatabaseProvider.SqlServer => "Server=localhost;Database=opendockify;User Id=opendockify;Password=opendockify_dev;TrustServerCertificate=True",
            _ => throw new ArgumentOutOfRangeException(nameof(provider)),
        };
    }
#pragma warning restore S2068
}
