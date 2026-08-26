using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using OpenDockify.Operations.Configuration;

namespace OpenDockify.Operations.Services;

public sealed class DatabaseSnapshotService(DbContext db, IConfiguration configuration, IOptions<BackupOptions> options)
{
    private readonly BackupOptions _options = options.Value;

    public string ProviderName => configuration["Database:Provider"]?.Trim().ToLowerInvariant() switch
    {
        null or "" or "sqlite" => "sqlite",
        "postgres" or "postgresql" or "npgsql" => "postgres",
        "mysql" or "mariadb" => "mysql",
        "sqlserver" or "mssql" => "sqlserver",
        var value => throw new InvalidOperationException($"Unsupported database provider '{value}'."),
    };

    public async Task CreateSnapshotAsync(string destination, CancellationToken ct)
    {
        Directory.CreateDirectory(destination);
        if (ProviderName == "sqlite")
        {
            var source = (SqliteConnection)db.Database.GetDbConnection();
            await source.OpenAsync(ct);
            try
            {
                await using var target = new SqliteConnection($"Data Source={Path.Combine(destination, "database.sqlite")}");
                await target.OpenAsync(ct);
                source.BackupDatabase(target);
            }
            finally { await source.CloseAsync(); }
            return;
        }

        if (string.IsNullOrWhiteSpace(_options.NativeDumpExecutable))
            throw new InvalidOperationException($"No version-checked native dump adapter is configured for {ProviderName}.");
        await RunNativeAsync(_options.NativeDumpExecutable, "--version", null, ct);
        await RunNativeAsync(
            _options.NativeDumpExecutable,
            configuration.GetConnectionString("Default") ?? string.Empty,
            Path.Combine(destination, "database.native"),
            ct);
    }

    private static async Task RunNativeAsync(string executable, string connection, string? output, CancellationToken ct)
    {
        var start = new ProcessStartInfo(executable) { RedirectStandardOutput = output is null, UseShellExecute = false };
        if (output is not null)
        {
            start.ArgumentList.Add(connection);
            start.ArgumentList.Add(output);
        }
        else
            start.ArgumentList.Add(connection);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Native database tool did not start.");
        await process.WaitForExitAsync(ct);
        if (process.ExitCode != 0)
            throw new InvalidOperationException("Native database snapshot tool failed.");
    }
}
