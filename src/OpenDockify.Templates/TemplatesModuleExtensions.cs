using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Templates.Services;

namespace OpenDockify.Templates;

public static class TemplatesModuleExtensions
{
    public static IServiceCollection AddTemplatesModule(this IServiceCollection services)
    {
        services.AddScoped<TemplateService>();
        services.AddScoped<TemplatePackageService>();
        return services;
    }
}
