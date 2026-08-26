using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Data;
using OpenDockify.Operations;
using OpenDockify.Operations.Services;

if (args.Length == 0)
{
    await Console.Error.WriteLineAsync("Usage: opendockify-operations <backup|validate|restore|integrity> [arguments]");
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
    .AddOperationsModule(configuration)
    .BuildServiceProvider();
using var scope = services.CreateScope();
var coordinator = scope.ServiceProvider.GetRequiredService<BackupCoordinator>();

try
{
    switch (args[0].ToLowerInvariant())
    {
        case "backup" when args.Length == 2:
            var backup = await coordinator.CreateAsync(ReadSecret(args[1]), default);
            await Console.Out.WriteLineAsync($"{backup.Path} {backup.Digest}");
            break;
        case "validate" when args.Length == 3:
            var validation = await coordinator.ValidateAsync(args[1], ReadSecret(args[2]), default);
            await Console.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(validation));
            return validation.IsValid ? 0 : 1;
        case "restore" when args.Length == 5:
            var operationId = await coordinator.RestoreAsync(args[1], ReadSecret(args[2]), args[3], args[4], default);
            await Console.Out.WriteLineAsync(operationId.ToString());
            break;
        case "integrity":
            var report = await coordinator.CheckIntegrityAsync(default);
            await Console.Out.WriteLineAsync(System.Text.Json.JsonSerializer.Serialize(report));
            return report.IsHealthy ? 0 : 1;
        default:
            await Console.Error.WriteLineAsync("Invalid command. Passphrase arguments are paths to regular, non-symlink secret files.");
            return 2;
    }
    return 0;
}
catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or ArgumentException)
{
    await Console.Error.WriteLineAsync(ex.Message);
    return 1;
}

static string ReadSecret(string path)
{
    var full = Path.GetFullPath(path);
    if (!File.Exists(full) || File.GetAttributes(full).HasFlag(FileAttributes.ReparsePoint))
        throw new InvalidOperationException("Passphrase file must be a regular non-symlink file.");
    return File.ReadAllText(full).TrimEnd();
}
