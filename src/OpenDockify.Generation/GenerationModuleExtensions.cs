using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OpenDockify.Generation.Services;

namespace OpenDockify.Generation;

public static class GenerationModuleExtensions
{
    public static IServiceCollection AddGenerationModule(this IServiceCollection services)
    {
        services.AddScoped<DocumentService>();
        services.TryAddScoped<IDocumentReadAuthorizer, OwnerDocumentReadAuthorizer>();
        return services;
    }
}
