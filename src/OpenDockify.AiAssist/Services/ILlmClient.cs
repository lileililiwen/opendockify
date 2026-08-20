namespace OpenDockify.AiAssist.Services;

public sealed record LlmResult(bool Succeeded, string? Content, string? Error)
{
    public static LlmResult Success(string content)
    {
        return new(true, content, null);
    }

    public static LlmResult Failure(string error)
    {
        return new(false, null, error);
    }
}

/// <summary>
/// Abstraction over an LLM chat-completions endpoint. Implementations speak
/// the OpenAI-compatible protocol so both OpenAI and Ollama work.
/// </summary>
public interface ILlmClient
{
    Task<LlmResult> CompleteAsync(string systemPrompt, string userContent, CancellationToken cancellationToken);
}
