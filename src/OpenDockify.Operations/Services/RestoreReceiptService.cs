using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace OpenDockify.Operations.Services;

public sealed class RestoreReceiptService
{
    private readonly byte[] _key = RandomNumberGenerator.GetBytes(32);
    private readonly ConcurrentDictionary<string, DateTime> _issued = new(StringComparer.Ordinal);

    public string Issue(string digest)
    {
        var expires = DateTime.UtcNow.AddMinutes(30);
        var payload = $"{digest}.{new DateTimeOffset(expires).ToUnixTimeSeconds()}";
        var signature = Convert.ToHexString(HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
        var receipt = $"{payload}.{signature}";
        _issued[receipt] = expires;
        return receipt;
    }

    public bool Consume(string receipt, string digest)
    {
        if (!_issued.TryRemove(receipt, out var expires) || expires < DateTime.UtcNow)
            return false;
        var parts = receipt.Split('.');
        if (parts.Length != 3 || !string.Equals(parts[0], digest, StringComparison.Ordinal))
            return false;
        var payload = $"{parts[0]}.{parts[1]}";
        var expected = HMACSHA256.HashData(_key, Encoding.UTF8.GetBytes(payload));
        try
        { return CryptographicOperations.FixedTimeEquals(expected, Convert.FromHexString(parts[2])); }
        catch (FormatException) { return false; }
    }
}
