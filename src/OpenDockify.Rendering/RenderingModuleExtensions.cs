using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Rendering.Services;

namespace OpenDockify.Rendering;

public static class RenderingModuleExtensions
{
    public static IServiceCollection AddRenderingModule(this IServiceCollection services)
    {
        services.AddSingleton<IPdfRenderer, QuestPdfRenderer>();
        services.AddSingleton<IPdfOverlay, DefaultPdfOverlay>();
        return services;
    }
}
