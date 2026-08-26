using Microsoft.Extensions.Options;
using OpenDockify.Operations.Configuration;

namespace OpenDockify.Operations.Services;

public sealed class BackupRetentionService(IOptions<BackupOptions> options)
{
    private readonly BackupOptions _options = options.Value;

    public IReadOnlyList<string> Apply(DateTime utcNow)
    {
        var root = Path.GetFullPath(_options.Directory);
        if (!Directory.Exists(root) || File.GetAttributes(root).HasFlag(FileAttributes.ReparsePoint))
            return [];
        var valid = Directory.EnumerateFiles(root, "*.odbak", SearchOption.TopDirectoryOnly)
            .Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            .Where(BackupBundleService.HasValidHeader)
            .Select(path => new FileInfo(path))
            .OrderByDescending(x => x.CreationTimeUtc)
            .ToArray();
        var removed = new List<string>();
        for (var index = 1; index < valid.Length; index++)
        {
            if (index < Math.Max(1, _options.RetainCount)
                && valid[index].CreationTimeUtc >= utcNow.AddDays(-Math.Max(1, _options.RetainDays)))
                continue;
            valid[index].Delete();
            removed.Add(valid[index].FullName);
        }
        return removed;
    }
}
