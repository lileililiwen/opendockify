using System.Buffers.Binary;
using System.Formats.Tar;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using OpenDockify.Operations.Configuration;
using OpenDockify.Operations.Models;

namespace OpenDockify.Operations.Services;

public sealed class BackupBundleService(IOptions<BackupOptions> options)
{
    private static readonly byte[] _magic = "ODBKUP01"u8.ToArray();
    private const int _formatVersion = 1;
    private const int _chunkSize = 64 * 1024;
    private readonly BackupOptions _options = options.Value;

    public async Task<string> CreateAsync(
        string sourceDirectory,
        string outputPath,
        string passphrase,
        string databaseProvider,
        CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        var sourceRoot = Path.GetFullPath(sourceDirectory);
        var files = Directory.EnumerateFiles(sourceRoot, "*", SearchOption.AllDirectories)
            .Where(path => !File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
            .Select(path => (Full: path, Relative: NormalizeRelative(sourceRoot, path)))
            .OrderBy(x => x.Relative, StringComparer.Ordinal)
            .ToArray();

        var manifestFiles = new List<BackupManifestFile>(files.Length);
        foreach (var file in files)
        {
            var info = new FileInfo(file.Full);
            if (info.Length > _options.MaximumEntryBytes)
                throw new InvalidDataException($"Backup entry exceeds configured limit: {file.Relative}");
            await using var input = File.OpenRead(file.Full);
            manifestFiles.Add(new(file.Relative, info.Length, await HashAsync(input, cancellationToken)));
        }

        var manifest = new BackupManifest(
            _formatVersion,
            typeof(BackupBundleService).Assembly.GetName().Version?.ToString() ?? "0.0.0",
            databaseProvider,
            DateTime.UtcNow,
            manifestFiles);

        var tempTar = Path.Combine(Path.GetTempPath(), $"opendockify-{Guid.NewGuid():N}.tar");
        try
        {
            await WriteTarAsync(tempTar, files, manifest, cancellationToken);
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
            await EncryptAsync(tempTar, outputPath, passphrase, cancellationToken);
            await using var bundle = File.OpenRead(outputPath);
            return await HashAsync(bundle, cancellationToken);
        }
        catch
        {
            if (File.Exists(outputPath))
                File.Delete(outputPath);
            throw;
        }
        finally
        {
            if (File.Exists(tempTar))
                File.Delete(tempTar);
        }
    }

    public async Task<(BackupManifest Manifest, string Digest, string ExtractedDirectory)> ValidateAndExtractAsync(
        string bundlePath,
        string passphrase,
        CancellationToken cancellationToken = default)
    {
        ValidatePassphrase(passphrase);
        var extracted = Path.Combine(Path.GetTempPath(), $"opendockify-restore-{Guid.NewGuid():N}");
        var tarPath = Path.Combine(Path.GetTempPath(), $"opendockify-{Guid.NewGuid():N}.tar");
        Directory.CreateDirectory(extracted);
        try
        {
            await DecryptAsync(bundlePath, tarPath, passphrase, cancellationToken);
            var manifest = await ExtractAndValidateTarAsync(tarPath, extracted, cancellationToken);
            await using var bundle = File.OpenRead(bundlePath);
            return (manifest, await HashAsync(bundle, cancellationToken), extracted);
        }
        catch
        {
            Directory.Delete(extracted, true);
            throw;
        }
        finally
        {
            if (File.Exists(tarPath))
                File.Delete(tarPath);
        }
    }

    public static bool HasValidHeader(string path)
    {
        if (!File.Exists(path) || new FileInfo(path).Length < _magic.Length + sizeof(int))
            return false;
        Span<byte> header = stackalloc byte[_magic.Length + sizeof(int)];
        using var input = File.OpenRead(path);
        return input.Read(header) == header.Length
            && header[.._magic.Length].SequenceEqual(_magic)
            && BinaryPrimitives.ReadInt32LittleEndian(header[_magic.Length..]) == _formatVersion;
    }

    private static async Task WriteTarAsync(
        string tarPath,
        IEnumerable<(string Full, string Relative)> files,
        BackupManifest manifest,
        CancellationToken ct)
    {
        await using var output = File.Create(tarPath);
        await using var writer = new TarWriter(output, leaveOpen: false);
        var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest);
        var manifestEntry = new PaxTarEntry(TarEntryType.RegularFile, "manifest.json")
        {
            DataStream = new MemoryStream(manifestBytes, writable: false),
            ModificationTime = DateTimeOffset.UtcNow,
        };
        await writer.WriteEntryAsync(manifestEntry, ct);
        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            await using var data = new FileStream(file.Full, FileMode.Open, FileAccess.Read, FileShare.Read);
            var entry = new PaxTarEntry(TarEntryType.RegularFile, $"data/{file.Relative}")
            {
                DataStream = data,
                ModificationTime = File.GetLastWriteTimeUtc(file.Full),
            };
            await writer.WriteEntryAsync(entry, ct);
        }
    }

    private async Task<BackupManifest> ExtractAndValidateTarAsync(string tarPath, string destination, CancellationToken ct)
    {
        BackupManifest? manifest = null;
        var extracted = new Dictionary<string, (long Size, string Hash)>(StringComparer.Ordinal);
        long total = 0;
        await using var input = File.OpenRead(tarPath);
        await using var reader = new TarReader(input);
        while (await reader.GetNextEntryAsync(copyData: false, ct) is { } entry)
        {
            if (entry.EntryType is not TarEntryType.RegularFile || entry.DataStream is null)
                throw new InvalidDataException("Only regular files are allowed in backup bundles.");
            var normalized = entry.Name.Replace('\\', '/');
            if (normalized.StartsWith('/') || normalized.Split('/').Any(x => x is ".." or ""))
                throw new InvalidDataException("Unsafe backup entry path.");
            if (entry.Length < 0 || entry.Length > _options.MaximumEntryBytes || total + entry.Length > _options.MaximumBundleBytes)
                throw new InvalidDataException("Backup expands beyond configured limits.");
            total += entry.Length;
            if (normalized == "manifest.json")
            {
                manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(entry.DataStream, cancellationToken: ct)
                    ?? throw new InvalidDataException("Manifest is missing or malformed.");
                continue;
            }
            if (!normalized.StartsWith("data/", StringComparison.Ordinal))
                throw new InvalidDataException("Unexpected backup entry.");
            var relative = normalized[5..];
            var target = SafeCombine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await using var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[64 * 1024];
            int read;
            long size = 0;
            while ((read = await entry.DataStream.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
            }
            extracted.Add(relative, (size, Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()));
        }

        if (manifest is null || manifest.FormatVersion != _formatVersion)
            throw new InvalidDataException("Unsupported backup format version.");
        if (manifest.Files.Count != extracted.Count)
            throw new InvalidDataException("Manifest does not match bundle contents.");
        foreach (var expected in manifest.Files)
        {
            if (!extracted.TryGetValue(expected.Path, out var actual)
                || actual.Size != expected.Size
                || !CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(actual.Hash), Convert.FromHexString(expected.Sha256)))
                throw new InvalidDataException($"Checksum validation failed for {expected.Path}.");
        }
        return manifest;
    }

    private static async Task EncryptAsync(string inputPath, string outputPath, string passphrase, CancellationToken ct)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var noncePrefix = RandomNumberGenerator.GetBytes(8);
        var key = await DeriveKeyAsync(passphrase, salt, ct);
        try
        {
            await using var input = File.OpenRead(inputPath);
            await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await output.WriteAsync(_magic, ct);
            await WriteIntAsync(output, _formatVersion, ct);
            await output.WriteAsync(salt, ct);
            await output.WriteAsync(noncePrefix, ct);
            var plain = new byte[_chunkSize];
            var cipher = new byte[_chunkSize];
            var tag = new byte[16];
            uint counter = 0;
            using var aes = new AesGcm(key, 16);
            int read;
            while ((read = await input.ReadAsync(plain, ct)) > 0)
            {
                await WriteIntAsync(output, read, ct);
                var nonce = Nonce(noncePrefix, counter);
                var aad = Aad(counter, read);
                aes.Encrypt(nonce, plain.AsSpan(0, read), cipher.AsSpan(0, read), tag, aad);
                await output.WriteAsync(cipher.AsMemory(0, read), ct);
                await output.WriteAsync(tag, ct);
                counter++;
            }
            await WriteIntAsync(output, 0, ct);
            await output.FlushAsync(ct);
        }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private async Task DecryptAsync(string inputPath, string outputPath, string passphrase, CancellationToken ct)
    {
        await using var input = File.OpenRead(inputPath);
        var magic = new byte[_magic.Length];
        await ReadExactlyAsync(input, magic, ct);
        if (!magic.SequenceEqual(_magic) || await ReadIntAsync(input, ct) != _formatVersion)
            throw new InvalidDataException("Unsupported backup header.");
        var salt = new byte[16];
        var prefix = new byte[8];
        await ReadExactlyAsync(input, salt, ct);
        await ReadExactlyAsync(input, prefix, ct);
        var key = await DeriveKeyAsync(passphrase, salt, ct);
        try
        {
            await using var output = new FileStream(outputPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            using var aes = new AesGcm(key, 16);
            uint counter = 0;
            long total = 0;
            while (true)
            {
                var length = await ReadIntAsync(input, ct);
                if (length == 0)
                    break;
                if (length < 0 || length > _chunkSize || total + length > _options.MaximumBundleBytes)
                    throw new InvalidDataException("Invalid encrypted chunk length.");
                var cipher = new byte[length];
                var plain = new byte[length];
                var tag = new byte[16];
                await ReadExactlyAsync(input, cipher, ct);
                await ReadExactlyAsync(input, tag, ct);
                aes.Decrypt(Nonce(prefix, counter), cipher, tag, plain, Aad(counter, length));
                await output.WriteAsync(plain, ct);
                total += length;
                counter++;
            }
            if (input.Position != input.Length)
                throw new InvalidDataException("Trailing backup data detected.");
        }
        catch (CryptographicException ex) { throw new InvalidDataException("Backup authentication failed.", ex); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }

    private static async Task<byte[]> DeriveKeyAsync(string passphrase, byte[] salt, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var argon = new Argon2id(Encoding.UTF8.GetBytes(passphrase))
        {
            Salt = salt,
            DegreeOfParallelism = 2,
            Iterations = 3,
            MemorySize = 64 * 1024,
        };
        return await argon.GetBytesAsync(32);
    }

    private static byte[] Nonce(byte[] prefix, uint counter)
    {
        var nonce = new byte[12];
        prefix.CopyTo(nonce, 0);
        BinaryPrimitives.WriteUInt32BigEndian(nonce.AsSpan(8), counter);
        return nonce;
    }

    private static byte[] Aad(uint counter, int length)
    {
        var aad = new byte[8];
        BinaryPrimitives.WriteUInt32BigEndian(aad, counter);
        BinaryPrimitives.WriteInt32BigEndian(aad.AsSpan(4), length);
        return aad;
    }

    private static string NormalizeRelative(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        if (relative.StartsWith('/') || relative.Split('/').Any(x => x is ".." or ""))
            throw new InvalidDataException("Unsafe source entry path.");
        return relative;
    }

    private static string SafeCombine(string root, string relative)
    {
        var target = Path.GetFullPath(Path.Combine(root, relative));
        var prefix = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (!target.StartsWith(prefix, StringComparison.Ordinal))
            throw new InvalidDataException("Unsafe backup entry path.");
        return target;
    }

    private static void ValidatePassphrase(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length < 12)
            throw new ArgumentException("Backup passphrase must be at least 12 characters.", nameof(value));
    }

    private static async Task<string> HashAsync(Stream stream, CancellationToken ct)
    {
        return Convert.ToHexString(await SHA256.HashDataAsync(stream, ct)).ToLowerInvariant();
    }

    private static async Task WriteIntAsync(Stream stream, int value, CancellationToken ct)
    {
        var bytes = new byte[4];
        BinaryPrimitives.WriteInt32LittleEndian(bytes, value);
        await stream.WriteAsync(bytes, ct);
    }

    private static async Task<int> ReadIntAsync(Stream stream, CancellationToken ct)
    {
        var bytes = new byte[4];
        await ReadExactlyAsync(stream, bytes, ct);
        return BinaryPrimitives.ReadInt32LittleEndian(bytes);
    }

    private static async Task ReadExactlyAsync(Stream stream, byte[] buffer, CancellationToken ct)
    {
        var offset = 0;
        while (offset < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(offset), ct);
            if (read == 0)
                throw new InvalidDataException("Backup is truncated.");
            offset += read;
        }
    }
}
