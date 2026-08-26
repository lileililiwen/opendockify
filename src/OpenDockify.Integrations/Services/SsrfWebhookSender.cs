using System.Net;
using System.Net.Sockets;

namespace OpenDockify.Integrations.Services;

public sealed record WebhookSendOutcome(bool Success, int? StatusCode, string? Error);

/// <summary>
/// Performs one pre-planned webhook request. Implementations MUST NOT follow
/// redirects automatically; redirect hops are re-planned by the caller so
/// every hop is validated.
/// </summary>
public interface IWebhookSender
{
    Task<WebhookSendOutcome> SendAsync(
        Uri url,
        IPAddress pinnedAddress,
        IReadOnlyDictionary<string, string> headers,
        byte[] body,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

/// <summary>
/// Real outbound transport: connects only to the address validated by
/// <see cref="WebhookRoutePlanner"/> (pinned via the connect callback), never
/// follows redirects, and enforces a per-attempt timeout.
/// </summary>
public sealed class SsrfWebhookSender : IWebhookSender
{
    public async Task<WebhookSendOutcome> SendAsync(
        Uri url,
        IPAddress pinnedAddress,
        IReadOnlyDictionary<string, string> headers,
        byte[] body,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            ConnectTimeout = timeout,
            // Pin the connection to the validated address: even if DNS were
            // re-resolved between planning and connect, traffic cannot be
            // redirected into a prohibited range.
            ConnectCallback = async (_, ct) =>
            {
                var socket = new Socket(pinnedAddress.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                await socket.ConnectAsync(pinnedAddress, url.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            },
        };
        using var client = new HttpClient(handler) { Timeout = timeout };

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new ByteArrayContent(body),
            };
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            foreach (var (name, value) in headers)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }

            using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new WebhookSendOutcome((int)response.StatusCode is >= 200 and <= 299, (int)response.StatusCode, null);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new WebhookSendOutcome(false, null, "timeout");
        }
        catch (HttpRequestException ex)
        {
            return new WebhookSendOutcome(false, null, ex.Message);
        }
    }
}
