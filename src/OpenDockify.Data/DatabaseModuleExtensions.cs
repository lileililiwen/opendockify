using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenDockify.Data.Audit;
using Platform.Auditing.Contracts;
using Platform.Auditing.Contracts.DependencyInjection;
using Platform.Auditing.EfCore.DependencyInjection;
using Platform.Jobs;
using Platform.Persistence.EfCore.Migrator;

namespace OpenDockify.Data;

/// <summary>
/// Composition-root entry point for the Data module. Registers the EF Core
/// <see cref="AppDbContext"/> for the provider selected by
/// <c>Database:Provider</c> (SQLite default) and exposes the startup
/// migrate-and-seed routine.
/// </summary>
public static partial class DatabaseModuleExtensions
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

        services.AddPooledDbContextFactory<AppDbContext>((sp, builder) =>
        {
            DatabaseProviderRegistry.Configure(builder, provider, connectionString, mySqlVersion);
            // Opt-in entity-change capture: publishes masked `entity.*`
            // events for IAuditedEntity types through the audit pipeline.
            // No application entity implements IAuditedEntity yet, so this
            // is a no-op until the first entity opts in; wiring it now
            // satisfies the observability-audit-jobs interceptor boundary.
            foreach (var interceptor in sp.GetServices<ISaveChangesInterceptor>())
            {
                builder.AddInterceptors(interceptor);
            }
        });

        services.AddScoped<AppDbContext>(sp =>
            sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext());

        // Module services depend on the base DbContext (never OpenDockify.Data)
        // per Agents.md §2; resolve it to the AppDbContext instance.
        services.AddScoped<DbContext>(sp => sp.GetRequiredService<AppDbContext>());

        services.AddSingleton<ISeedRegistry, SeedRegistry>();

        return services;
    }

    /// <summary>
    /// Registers the platform-audit ingestion path: the contract pipeline
    /// (recorder, masker, enricher) and the application-owned
    /// <see cref="EntityAuditSink"/> that persists every audit event to
    /// the <c>AuditEvents</c> table. The retention service and its
    /// recurring-job handler are also registered.
    /// </summary>
    public static IServiceCollection AddAuditModule(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        services.AddPlatformAuditing(o =>
        {
            o.FailurePolicy = Platform.Auditing.Contracts.AuditFailurePolicy.FailOpen;
            o.PublishMode = Platform.Auditing.Contracts.AuditPublishMode.Synchronous;
        });
        services.AddPlatformAuditingEfCore();
        // Replace (not TryAdd) the platform's InMemoryAuditSink: the
        // contracts pipeline was just registered with its default sink,
        // so TryAdd would silently keep the in-memory one and no audit
        // event would ever reach the AuditEvents table.
        services.RemoveAll<IAuditSink>();
        services.AddSingleton<IAuditSink, EntityAuditSink>();
        services.Configure<AuditRetentionOptions>(configuration.GetSection(AuditRetentionOptions.SectionName));
        services.AddScoped<AuditRetentionService>();
        services.AddScoped<AuditRetentionJobHandler>();
        return services;
    }

    /// <summary>
    /// Applies EF Core migrations via the platform
    /// <see cref="IMigrationRunner"/>, then runs every registered
    /// application seeder through the same seeder-callback seam. The
    /// <c>ISeedRegistry</c> registered by <see cref="AddDatabaseModule"/>
    /// is wrapped as <see cref="Platform.Persistence.EfCore.Migrator.IMigrationSeeder"/>
    /// so existing <c>IDbSeeder</c> registrations continue to run.
    /// </summary>
    public static async Task<MigrationRunResult> MigrateAndSeedAsync(
        this IServiceProvider services,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(services);
        var runner = services.GetRequiredService<IMigrationRunner>();
        var factory = services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        var scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        var seeders = services.GetRequiredService<ISeedRegistry>().Seeders;
        var loggerFactory = services.GetRequiredService<ILoggerFactory>();
        var request = new MigrationRunnerRequest(
            CreateContext: ct =>
            {
                ct.ThrowIfCancellationRequested();
                // The pooled factory owns context lifetime; the runner
                // disposes each context it creates. No scope is created
                // here so nothing leaks when the runner disposes.
                return Task.FromResult<DbContext>(factory.CreateDbContext());
            },
            SeedAfterApply: true,
            Seeder: new SeedRegistryAdapter(
                seeders,
                scopeFactory,
                loggerFactory.CreateLogger("OpenDockify.Data.Seed")));
        var result = await runner.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
        var logger = loggerFactory.CreateLogger("OpenDockify.Data");
        if (!result.Succeeded)
        {
            Log.MigrationFailed(
                logger,
                result.Failure?.Category.ToString(),
                result.Failure?.Diagnostic,
                result.Failure?.ExceptionType);
            throw new InvalidOperationException(
                $"Database migration failed ({result.Failure?.Category}: {result.Failure?.Diagnostic}).");
        }

        Log.MigrationSucceeded(logger, result.AppliedMigrations.Count, result.SeedExecuted);
        return result;
    }

    private static partial class Log
    {
        [LoggerMessage(2, LogLevel.Critical,
            "Database migration failed ({Category}: {Diagnostic}, {ExceptionType}).")]
        public static partial void MigrationFailed(
            ILogger logger, string? category, string? diagnostic, string? exceptionType);

        [LoggerMessage(3, LogLevel.Information,
            "Database migration applied {Applied} migration(s); seed executed: {Seeded}.")]
        public static partial void MigrationSucceeded(ILogger logger, int applied, bool seeded);
    }

    private sealed class SeedRegistryAdapter : IMigrationSeeder
    {
        private readonly IReadOnlyList<IDbSeeder> _seeders;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger _logger;

        public SeedRegistryAdapter(
            IReadOnlyList<IDbSeeder> seeders,
            IServiceScopeFactory scopeFactory,
            ILogger logger)
        {
            _seeders = seeders;
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public async Task SeedAsync(DbContext context, CancellationToken cancellationToken = default)
        {
            // Each application seeder needs the full service provider
            // (configuration, AccountService, child scopes). Seeders run
            // in a fresh async scope from the host's scope factory — not
            // from the migrator-owned context, which the runner disposes.
            await using var scope = _scopeFactory.CreateAsyncScope();
            foreach (var seeder in _seeders)
            {
                _runningSeederAdapter(_logger, seeder.GetType().Name, null);
                await seeder.SeedAsync(scope.ServiceProvider, cancellationToken);
            }
        }

        private static readonly Action<ILogger, string, Exception?> _runningSeederAdapter =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(1, "RunningSeeder"),
                "Running seeder {Seeder}.");
    }

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
