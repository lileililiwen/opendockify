using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Generation.Services;
using OpenDockify.Sharing.Services;

namespace OpenDockify.Sharing;

public static class SharingModuleExtensions
{
    public static IServiceCollection AddSharingModule(this IServiceCollection services)
    {
        services.AddScoped<DocumentSharingService>();
        services.AddScoped<IDocumentReadAuthorizer, SharingDocumentReadAuthorizer>();
        services.AddHostedService<SharingCleanupService>();
        return services;
    }
}
