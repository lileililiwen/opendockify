using System.Security.Cryptography;
using System.Text;

namespace OpenDockify.Auth.Services;

/// <summary>
/// Shared helpers for the identity-lifecycle services. Centralises the rules
/// for opaque-handle generation and verification so the refresh-token and
/// recovery flows cannot drift apart.
/// </summary>
internal static class TokenObfuscator
{
    private const int _handleByteLength = 32;

    /// <summary>Generates a 256-bit opaque handle as a URL-safe base64 string.</summary>
    public static string NewHandle()
    {
        Span<byte> buffer = stackalloc byte[_handleByteLength];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    /// <summary>SHA-256 of the supplied value, lowercased hex.</summary>
    public static string Sha256Hex(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Generates a single-use numeric recovery code as a hyphenated string.
    /// Designed to be read over the phone if necessary.
    /// </summary>
    public static string NewRecoveryCode()
    {
        Span<byte> buffer = stackalloc byte[6];
        RandomNumberGenerator.Fill(buffer);
        var value = BitConverter.ToUInt32(buffer[..4]);
        var grouped = (value % 1_000_000_000u).ToString("D9", System.Globalization.CultureInfo.InvariantCulture);
        return $"{grouped[..3]}-{grouped[3..6]}-{grouped[6..]}";
    }
}
