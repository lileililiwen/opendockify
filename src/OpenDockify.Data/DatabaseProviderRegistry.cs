using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace OpenDockify.Data;
/// <summary>
/// Maps the <c>Database:Provider</c> setting to the EF Core provider extension
/// and provider-specific options. This is the single place where a new
/// provider is added; nothing else in the system knows about providers.
/// </summary>
public static class DatabaseProviderRegistry
{
    /// <summary>
    /// Parses the provider name from configuration, failing fast on unknown
    /// values with a message that lists the supported providers.
    /// </summary>
    public static DatabaseProvider Resolve(IConfiguration configuration)
    {
        var name = configuration["Database:Provider"]?.Trim().ToLowerInvariant();
        return name switch
        {
            null or "" or "sqlite" => DatabaseProvider.Sqlite,
            "postgres" or "postgresql" or "npgsql" => DatabaseProvider.Postgres,
            "mysql" or "mariadb" => DatabaseProvider.MySql,
            "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
            _ => throw new InvalidOperationException(
                $"Unsupported Database:Provider '{name}'. Supported values: " +
                "'sqlite', 'postgres', 'mysql', 'sqlserver'."),
        };
    }

    /// <summary>
    /// Applies the provider-specific <c>UseXxx</c> extension to the options
    /// builder. All providers use the same migrations assembly so one migration
    /// set works everywhere. <paramref name="mySqlServerVersion"/> overrides
    /// MySQL server auto-detection (useful on locked-down hosts).
    /// </summary>
    public static DbContextOptionsBuilder Configure(
        DbContextOptionsBuilder builder,
        DatabaseProvider provider,
        string connectionString,
        string? mySqlServerVersion)
    {
        const string migrationsAssembly = "OpenDockify.Data";

        switch (provider)
        {
            case DatabaseProvider.Sqlite:
                builder.UseSqlite(
                    connectionString,
                    o => o.MigrationsAssembly(migrationsAssembly));
                break;

            case DatabaseProvider.Postgres:
                builder.UseNpgsql(
                    connectionString,
                    o =>
                    {
                        o.MigrationsAssembly(migrationsAssembly);
                        o.EnableRetryOnFailure();
                    });
                break;

            case DatabaseProvider.MySql:
                builder.UseMySql(
                    connectionString,
                    ResolveMySqlServerVersion(connectionString, mySqlServerVersion),
                    o => o.MigrationsAssembly(migrationsAssembly));
                break;

            case DatabaseProvider.SqlServer:
                builder.UseSqlServer(
                    connectionString,
                    o => o.MigrationsAssembly(migrationsAssembly));
                break;

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(provider), provider, "Unknown database provider.");
        }

        return builder;
    }

    private static ServerVersion ResolveMySqlServerVersion(
        string connectionString, string? overrideVersion)
    {
        if (string.IsNullOrWhiteSpace(overrideVersion))
        {
            return ServerVersion.AutoDetect(connectionString);
        }

        return ServerVersion.Parse(overrideVersion);
    }
}
