using System.Security.Cryptography;
using System.Text;

namespace OpenDockify.Integrations.Services;

/// <summary>
/// HMAC-SHA256 webhook signatures over the exact delivered bytes, the event
/// id, and a unix timestamp: <c>t=&lt;ts&gt;, v1=&lt;hex&gt;</c> where
/// <c>hex = HMAC(secret, "{eventId}.{timestamp}.{body}")</c>. Receivers reject
/// alteration and replay by recomputing both.
/// </summary>
public static class WebhookSigner
{
    public const string SignatureHeaderName = "X-OpenDockify-Signature";
    public const string EventIdHeaderName = "X-OpenDockify-Event-Id";
    public const string EventTypeHeaderName = "X-OpenDockify-Event-Type";
    public const string DeliveryHeaderName = "X-OpenDockify-Delivery";

    public static long CurrentTimestamp()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public static string Sign(string secret, string eventId, long timestampSeconds, string body)
    {
        return $"t={timestampSeconds}, v1={ComputeHexSignature(secret, eventId, timestampSeconds, body)}";
    }

    public static string ComputeHexSignature(string secret, string eventId, long timestampSeconds, string body)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var payload = $"{eventId}.{timestampSeconds}.{body}";
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    /// <summary>Reference verifier used by tests, docs examples, and receivers.</summary>
    public static bool Verify(
        string secret,
        string eventId,
        long timestampSeconds,
        string body,
        string headerValue,
        int maxAgeSeconds,
        long nowUnixSeconds)
    {
        if (nowUnixSeconds - timestampSeconds > maxAgeSeconds)
        {
            return false;
        }

        var parts = headerValue.Split(',', StringSplitOptions.TrimEntries);
        var provided = parts.FirstOrDefault(p => p.StartsWith("v1=", StringComparison.Ordinal))?[3..];
        if (string.IsNullOrEmpty(provided))
        {
            return false;
        }

        byte[] providedBytes;
        try
        {
            providedBytes = Convert.FromHexString(provided);
        }
        catch (FormatException)
        {
            return false;
        }

        var expected = ComputeHexSignature(secret, eventId, timestampSeconds, body);
        return CryptographicOperations.FixedTimeEquals(
            providedBytes,
            Convert.FromHexString(expected));
    }
}
