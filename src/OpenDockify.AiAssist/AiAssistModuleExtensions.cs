using Microsoft.Extensions.DependencyInjection;
using OpenDockify.AiAssist.Services;

namespace OpenDockify.AiAssist;

/// <summary>
/// Constants used by the API composition root to attach platform resilience
/// handlers to the AI assist HttpClient. Keeping the name in one place keeps
/// the AI module and the platform wiring aligned.
/// </summary>
public static class AiAssistClientNames
{
    /// <summary>The named HttpClient used to call the configured LLM endpoint.</summary>
    public const string HttpClient = "ai-assist";
}

public static class AiAssistModuleExtensions
{
    public static IServiceCollection AddAiAssistModule(this IServiceCollection services)
    {
        services.AddScoped<ILlmClient, OpenAiCompatibleLlmClient>();
        services.AddScoped<AiUsageLogService>();
        services.AddScoped<AiAssistService>();
        return services;
    }
}
