using System.Net;
using System.Net.Sockets;

namespace OpenDockify.Integrations.Services;

public interface IDnsResolver
{
    Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken);
}

public sealed class DnsResolver : IDnsResolver
{
    public async Task<IPAddress[]> ResolveAsync(string host, CancellationToken cancellationToken)
    {
        try
        {
            return await Dns.GetHostAddressesAsync(host, cancellationToken);
        }
        catch (SocketException)
        {
            return [];
        }
    }
}

/// <summary>The outcome of outbound-destination validation.</summary>
public sealed record RoutePlanResult(
    bool Allowed,
    string? BlockedReason,
    IPAddress? Address,
    Uri Url);

/// <summary>
/// SSRF-safe destination planning: HTTPS only; every resolved address (and
/// every redirect hop) is checked against prohibited ranges — loopback,
/// link-local (incl. cloud metadata), private, multicast, unspecified, shared
/// address space — unless the deployer explicitly allowlisted the hostname or
/// a CIDR range. The validated address is pinned for the connection so DNS
/// rebinding between check and connect cannot bypass it.
/// </summary>
public sealed class WebhookRoutePlanner(IReadOnlyList<string> allowlist, IDnsResolver resolver)
{
    private readonly List<(string? Host, byte[] Prefix, int PrefixLength)> _allowlist = ParseAllowlist(allowlist);

    public async Task<RoutePlanResult> PlanAsync(Uri url, CancellationToken cancellationToken = default)
    {
        if (url.Scheme != Uri.UriSchemeHttps)
        {
            return new RoutePlanResult(false, "scheme", null, url);
        }

        var host = url.IdnHost;
        if (_allowlist.Any(entry => entry.Host is not null && entry.Host == host))
        {
            // Deployer explicitly trusts this hostname: pin its first address.
            var trusted = await resolver.ResolveAsync(host, cancellationToken);
            var address = trusted.FirstOrDefault();
            return address is null
                ? new RoutePlanResult(false, "dns-resolution-failed", null, url)
                : new RoutePlanResult(true, null, address, url);
        }

        var addresses = await resolver.ResolveAsync(host, cancellationToken);
        if (addresses.Length == 0)
        {
            return new RoutePlanResult(false, "dns-resolution-failed", null, url);
        }

        foreach (var address in addresses)
        {
            var (allowed, reason) = EvaluateAddress(address);
            if (!allowed)
            {
                return new RoutePlanResult(false, reason, null, url);
            }
        }

        return new RoutePlanResult(true, null, addresses[0], url);
    }

    private (bool Allowed, string? Reason) EvaluateAddress(IPAddress address)
    {
        if (_allowlist.Any(entry => entry.Host is null && InSubnet(address, entry.Prefix, entry.PrefixLength)))
        {
            return (true, null);
        }

        var normalized = Normalize(address);
        if (IPAddress.IsLoopback(normalized))
        {
            return (false, "loopback");
        }

        if (normalized.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (normalized.IsIPv6LinkLocal || normalized.IsIPv6Multicast || normalized.IsIPv6SiteLocal)
            {
                return (false, "link-local");
            }

            if (normalized.Equals(IPAddress.IPv6Any))
            {
                return (false, "unspecified");
            }

            var bytes = normalized.GetAddressBytes();
            if (bytes[0] >= 0xfc && bytes[0] <= 0xfd)
            {
                return (false, "private"); // fc00::/7 unique local
            }

            if (bytes[0] == 0xff)
            {
                return (false, "multicast");
            }
        }
        else
        {
            var first = FirstOctet(normalized);
            var second = SecondOctet(normalized);
            if (first == 10
                || (first == 172 && second >= 16 && second <= 31)
                || (first == 192 && second == 168))
            {
                return (false, "private");
            }

            if (first == 169 && second == 254)
            {
                return (false, "link-local"); // includes cloud metadata 169.254.169.254
            }

            if (first == 100 && second >= 64 && second <= 127)
            {
                return (false, "shared-address-space");
            }

            if (first >= 224)
            {
                return (false, "multicast"); // multicast + reserved + broadcast
            }

            if (first == 0)
            {
                return (false, "unspecified");
            }
        }

        return (true, null);
    }

    private static IPAddress Normalize(IPAddress address)
    {
        return address.IsIPv4MappedToIPv6 ? address.MapToIPv4() : address;
    }

    private static int FirstOctet(IPAddress address)
    {
        return address.GetAddressBytes()[0];
    }

    private static int SecondOctet(IPAddress address)
    {
        return address.GetAddressBytes()[1];
    }

    private static bool InSubnet(IPAddress address, byte[] prefix, int prefixLength)
    {
        var normalized = Normalize(address).GetAddressBytes();
        if (normalized.Length != prefix.Length)
        {
            return false;
        }

        var fullBytes = prefixLength / 8;
        var remainderBits = prefixLength % 8;
        for (var i = 0; i < fullBytes; i++)
        {
            if (normalized[i] != prefix[i])
            {
                return false;
            }
        }

        if (remainderBits > 0 && fullBytes < prefix.Length)
        {
            var mask = (byte)(0xFF << (8 - remainderBits));
            if ((normalized[fullBytes] & mask) != (prefix[fullBytes] & mask))
            {
                return false;
            }
        }

        return true;
    }

    private static List<(string? Host, byte[] Prefix, int PrefixLength)> ParseAllowlist(
        IReadOnlyList<string> entries)
    {
        var parsed = new List<(string?, byte[], int)>();
        foreach (var entry in entries)
        {
            var trimmed = entry.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var slash = trimmed.IndexOf('/');
            if (slash > 0 && IPAddress.TryParse(trimmed[..slash], out var network))
            {
                var normalized = Normalize(network).GetAddressBytes();
                var length = int.TryParse(trimmed[(slash + 1)..], out var bits) ? bits : normalized.Length * 8;
                parsed.Add((null, normalized, Math.Min(length, normalized.Length * 8)));
            }
            else if (IPAddress.TryParse(trimmed, out var single))
            {
                var normalized = Normalize(single).GetAddressBytes();
                parsed.Add((null, normalized, normalized.Length * 8));
            }
            else
            {
                parsed.Add((trimmed.ToLowerInvariant(), [], 0));
            }
        }

        return parsed;
    }
}
