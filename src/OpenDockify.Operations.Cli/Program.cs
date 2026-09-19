using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenDockify.Data;
using OpenDockify.Operations;
using OpenDockify.Operations.Cli.Seeding;
using OpenDockify.Operations.Services;
using Platform.Persistence.EfCore.Migrator;
using Platform.Storage.Contracts;
using Platform.Storage.Local;

if (args.Length == 0)
{
    await Console.Error.WriteLineAsync(
        "Usage: opendockify-operations <backup|validate|restore|integrity|migrate> [arguments]");
    return 2;
}

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection()
    .AddLogging()
    .AddSingleton<IConfiguration>(configuration)
    .AddDatabaseModule(configuration)
    .AddPlatformMigrator()
    .AddOperationsModule(configuration);

if (!string.Equals(args[0], "migrate", StringComparison.OrdinalIgnoreCase))
{
    RegisterStorage(services, configuration);
}

await using var sp = services.BuildServiceProvider();

try
{
    return args[0].ToLowerInvariant() switch
    {
        "backup" when args.Length == 2 => await BackupAsync(args[1], sp),
        "validate" when args.Length == 3 => await ValidateAsync(args[1], args[2], sp),
        "restore" when args.Length == 5 => await RestoreAsync(args[1], args[2], args[3], args[4], sp),
        "integrity" => await IntegrityAsync(sp),
        "migrate" => await MigrateAsync(args, sp),
        _ => Usage(),
    };
}
catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

static void RegisterStorage(IServiceCollection services, IConfiguration configuration)
{
    var provider = (configuration["Storage:Provider"] ?? "local").Trim().ToLowerInvariant();
    if (provider != "local" && provider != "s3")
    {
        throw new InvalidOperationException($"Unsupported Storage:Provider '{provider}'.");
    }

    var storageRoot = configuration["Storage:Local:RootPath"];
    if (string.IsNullOrWhiteSpace(storageRoot))
    {
        storageRoot = Path.Combine(Directory.GetCurrentDirectory(), "objects");
    }

    var limits = new StorageOptions();
    if (long.TryParse(configuration["Storage:MaximumObjectBytes"], out var maxBytes) && maxBytes > 0)
    {
        limits = new StorageOptions
        {
            MaximumObjectBytes = maxBytes,
            MaximumPresignLifetime = limits.MaximumPresignLifetime,
            OperationTimeout = limits.OperationTimeout,
        };
    }

    services.AddSingleton(limits);
    services.AddSingleton<IObjectStorage>(_ => new LocalFileStorage(storageRoot, limits));
}

static async Task<int> BackupAsync(string passphraseFile, IServiceProvider sp)
{
    await using var scope = sp.CreateAsyncScope();
    var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();
    var backup = await coordinator.CreateAsync(ReadSecret(passphraseFile), default);
    await Console.Out.WriteLineAsync($"{backup.Path} {backup.Digest}");
    return 0;
}

static async Task<int> ValidateAsync(string path, string passphraseFile, IServiceProvider sp)
{
    await using var scope = sp.CreateAsyncScope();
    var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();
    var validation = await coordinator.ValidateAsync(path, ReadSecret(passphraseFile), default);
    await Console.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(validation));
    return validation.IsValid ? 0 : 1;
}

static async Task<int> RestoreAsync(string path, string passphraseFile, string database, string storageRoot, IServiceProvider sp)
{
    await using var scope = sp.CreateAsyncScope();
    var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();
    var operationId = await coordinator.RestoreAsync(path, ReadSecret(passphraseFile), database, storageRoot, default);
    await Console.Out.WriteLineAsync(operationId.ToString());
    return 0;
}

static async Task<int> IntegrityAsync(IServiceProvider sp)
{
    await using var scope = sp.CreateAsyncScope();
    var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();
    var report = await coordinator.CheckIntegrityAsync(default);
    await Console.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report));
    return report.IsHealthy ? 0 : 1;
}

static async Task<int> MigrateAsync(string[] args, IServiceProvider sp)
{
    var commandArgs = args.Skip(1).ToArray();
    var seedRegistry = sp.GetRequiredService<OpenDockify.Data.ISeedRegistry>();
    return await MigrationConsoleRunner.RunAsync(
        commandArgs,
        _ => new MigrationRunnerRequest(
            CreateContext: ct => Task.FromResult<DbContext>(sp.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContext()),
            SeedAfterApply: false,
            Seeder: new SeedRegistrySeederAdapter(seedRegistry, sp.GetRequiredService<ILoggerFactory>().CreateLogger("OpenDockify.Operations.Cli.Seed"))));
}

static int Usage()
{
    Console.Error.WriteLine(
        "Invalid command. Passphrase arguments are paths to regular, non-symlink secret files.");
    Console.Error.WriteLine(
        "  opendockify-operations migrate [apply|list-pending] [--seed]");
    return 2;
}

static string ReadSecret(string path)
{
    var full = Path.GetFullPath(path);
    if (!File.Exists(full) || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
        throw new InvalidOperationException("Passphrase file must be a regular non-symlink file.");
    return File.ReadAllText(full).TrimEnd();
}
