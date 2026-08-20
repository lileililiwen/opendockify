using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OpenDockify.SystemConfig.Services;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// OpenAI-compatible chat-completions client. Endpoint, API key, and model are
/// read from system config at call time (<c>Ai.Endpoint</c>, <c>Ai.ApiKey</c>,
/// <c>Ai.Model</c>, <c>Ai.TimeoutSeconds</c>) — never from source code. One
/// retry on failure; a clear error is returned when the endpoint is missing or
/// unreachable.
/// </summary>
public sealed class OpenAiCompatibleLlmClient(
    ISystemConfigReader config,
    IHttpClientFactory httpClientFactory) : ILlmClient
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<LlmResult> CompleteAsync(
        string systemPrompt,
        string userContent,
        CancellationToken cancellationToken)
    {
        var endpoint = await config.GetAsync<string>(SettingKeys.AiEndpoint, cancellationToken);
        var apiKey = await config.GetAsync<string>(SettingKeys.AiApiKey, cancellationToken);
        var model = await config.GetAsync<string>(SettingKeys.AiModel, cancellationToken);
        var timeoutSeconds = await config.GetAsync<int?>(SettingKeys.AiTimeoutSeconds, cancellationToken) ?? 30;

        if (string.IsNullOrWhiteSpace(endpoint))
        {
            return LlmResult.Failure("AI endpoint is not configured.");
        }

        if (string.IsNullOrWhiteSpace(model))
        {
            return LlmResult.Failure("AI model is not configured.");
        }

        var url = endpoint.TrimEnd('/');
        if (!url.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase))
        {
            url += "/chat/completions";
        }

        var requestBody = new ChatCompletionsRequest(model, [new ChatMessage("system", systemPrompt), new ChatMessage("user", userContent)]);

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, timeoutSeconds)));

        // One retry on transport-level or non-success failures.
        Exception? lastError = null;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var httpClient = httpClientFactory.CreateClient("ai-assist");
                using var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = JsonContent.Create(requestBody, options: _jsonOptions),
                };

                if (!string.IsNullOrEmpty(apiKey))
                {
                    request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseContentRead, timeoutCts.Token);

                if (!response.IsSuccessStatusCode)
                {
                    lastError = new InvalidOperationException($"LLM endpoint returned {(int)response.StatusCode}.");
                    continue;
                }

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var parsed = JsonSerializer.Deserialize<ChatCompletionsResponse>(content, _jsonOptions);
                var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content;

                return string.IsNullOrWhiteSpace(text)
                    ? LlmResult.Failure("LLM returned an empty response.")
                    : LlmResult.Success(text);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                lastError = new TimeoutException("LLM request timed out.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                lastError = ex;
            }
        }

        return LlmResult.Failure($"LLM request failed: {lastError?.Message ?? "unknown error"}");
    }
}

internal sealed record ChatCompletionsRequest(string Model, List<ChatMessage> Messages);

internal sealed record ChatMessage(string Role, string Content);

internal sealed record ChatCompletionsResponse(List<ChatChoice>? Choices);

internal sealed record ChatChoice(ChatResponseMessage? Message);

internal sealed class ChatResponseMessage
{
    [JsonPropertyName("content")]
    public string? Content { get; set; }
}
