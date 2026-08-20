namespace OpenDockify.Data;

/// <summary>
/// Supported EF Core database providers. Selection happens once at startup via
/// <see cref="DatabaseProviderRegistry"/> from the <c>Database:Provider</c>
/// setting — switching providers requires no recompile.
/// </summary>
public enum DatabaseProvider
{
    Sqlite,
    Postgres,
    MySql,
    SqlServer,
}
