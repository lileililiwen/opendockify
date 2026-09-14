using System.Buffers.Binary;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace OpenDockify.Auth.Services;

/// <summary>
/// RFC 6238 TOTP generator/validator using HMAC-SHA1. The shared secret is
/// a 20-byte random value base32-encoded for transport. We allow a ±1 step
/// window (default) to absorb clock skew without weakening the algorithm.
/// </summary>
public static class TotpValidator
{
    public const int SecretByteLength = 20;
    public const int DefaultDigits = 6;
    public static readonly TimeSpan DefaultStep = TimeSpan.FromSeconds(30);

    /// <summary>Generates a base32-encoded 20-byte secret.</summary>
    public static string NewSecret()
    {
        Span<byte> buffer = stackalloc byte[SecretByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Base32Encode(buffer);
    }

    /// <summary>
    /// Validates a 6-digit TOTP code against the supplied base32 secret.
    /// </summary>
    /// <param name="base32Secret">The shared secret in base32 (no padding).</param>
    /// <param name="code">The user-supplied 6-digit code.</param>
    /// <param name="toleranceSteps">Number of ± steps accepted for clock skew (default 1).</param>
    /// <param name="step">The TOTP step (default 30 seconds).</param>
    /// <param name="now">Optional clock override; defaults to UTC now.</param>
    public static bool Validate(
        string base32Secret,
        string code,
        int toleranceSteps = 1,
        TimeSpan? step = null,
        DateTimeOffset? now = null)
    {
        if (string.IsNullOrWhiteSpace(base32Secret) || string.IsNullOrWhiteSpace(code))
        {
            return false;
        }

        if (!IsDigits(code))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = Base32Decode(base32Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var stepDuration = step ?? DefaultStep;
        var counter = (now ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds() / (long)stepDuration.TotalSeconds;
        for (var offset = -toleranceSteps; offset <= toleranceSteps; offset++)
        {
            if (ConstantTimeEquals(code, ComputeCode(key, counter + offset, DefaultDigits)))
            {
                return true;
            }
        }
        return false;
    }

    // RFC 6238 mandates HMAC-SHA1; SHA-256/512 are not interoperable with the
    // majority of authenticator apps. Suppress the weak-algorithm warning.
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "CA5350:Do Not Use Weak Cryptographic Algorithms",
        Justification = "TOTP per RFC 6238 mandates HMAC-SHA1.")]
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Security",
        "S4790:Use a stronger hashing algorithm",
        Justification = "TOTP per RFC 6238 mandates HMAC-SHA1.")]
    private static string ComputeCode(byte[] key, long counter, int digits)
    {
        Span<byte> counterBytes = stackalloc byte[8];
        BinaryPrimitives.WriteInt64BigEndian(counterBytes, counter);
        using var hmac = new HMACSHA1(key);
        var hash = hmac.ComputeHash(counterBytes.ToArray());
        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);
        var modulus = (int)Math.Pow(10, digits);
        var code = (binary % modulus).ToString($"D{digits}", CultureInfo.InvariantCulture);
        return code;
    }

    private static bool IsDigits(string value)
    {
        return value.All(c => c >= '0' && c <= '9');
    }

    private static bool ConstantTimeEquals(string a, string b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }

        var diff = 0;
        for (var i = 0; i < a.Length; i++)
        {
            diff |= a[i] ^ b[i];
        }
        return diff == 0;
    }

    private const string _base32Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    private static string Base32Encode(ReadOnlySpan<byte> data)
    {
        var builder = new StringBuilder((data.Length * 8 + 4) / 5);
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var b in data)
        {
            buffer = (buffer << 8) | b;
            bitsLeft += 8;
            while (bitsLeft >= 5)
            {
                bitsLeft -= 5;
                var index = (buffer >> bitsLeft) & 0x1F;
                builder.Append(_base32Alphabet[index]);
            }
        }
        if (bitsLeft > 0)
        {
            var index = (buffer << (5 - bitsLeft)) & 0x1F;
            builder.Append(_base32Alphabet[index]);
        }
        return builder.ToString();
    }

    private static byte[] Base32Decode(string value)
    {
        var normalized = value.Trim().Replace(" ", string.Empty).Replace("-", string.Empty).ToUpperInvariant();
        if (normalized.Length == 0)
        {
            throw new FormatException("Empty base32 value.");
        }

        var output = new List<byte>();
        var buffer = 0;
        var bitsLeft = 0;
        foreach (var c in normalized)
        {
            var index = _base32Alphabet.IndexOf(c);
            if (index < 0)
            {
                throw new FormatException($"Invalid base32 character '{c}'.");
            }

            buffer = (buffer << 5) | index;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                bitsLeft -= 8;
                output.Add((byte)((buffer >> bitsLeft) & 0xFF));
            }
        }
        return output.ToArray();
    }
}
