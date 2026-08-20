using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Generation.Services;

namespace OpenDockify.Generation;

public static class GenerationModuleExtensions
{
    public static IServiceCollection AddGenerationModule(this IServiceCollection services)
    {
        services.AddScoped<DocumentService>();
        return services;
    }
}
