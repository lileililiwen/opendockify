using Microsoft.Extensions.DependencyInjection;
using OpenDockify.AiAssist.Services;

namespace OpenDockify.AiAssist;

public static class AiAssistModuleExtensions
{
    public static IServiceCollection AddAiAssistModule(this IServiceCollection services)
    {
        services.AddHttpClient("ai-assist");
        services.AddScoped<ILlmClient, OpenAiCompatibleLlmClient>();
        services.AddScoped<AiUsageLogService>();
        services.AddScoped<AiAssistService>();
        return services;
    }
}
