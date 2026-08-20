using Microsoft.Extensions.DependencyInjection;
using OpenDockify.SystemConfig.Services;

namespace OpenDockify.SystemConfig;

public static class SystemConfigModuleExtensions
{
    public static IServiceCollection AddSystemConfigModule(this IServiceCollection services)
    {
        services.AddSingleton<SettingCache>();
        services.AddScoped<IConfigurationStore, ConfigurationStore>();
        services.AddScoped<SystemConfigService>();
        services.AddScoped<ISystemConfigReader>(sp => sp.GetRequiredService<SystemConfigService>());
        return services;
    }
}
