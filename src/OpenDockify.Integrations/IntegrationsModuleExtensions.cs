using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OpenDockify.Integrations.Configuration;
using OpenDockify.Integrations.Services;

namespace OpenDockify.Integrations;

public static class IntegrationsModuleExtensions
{
    public static IServiceCollection AddIntegrationsModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<IntegrationsOptions>(configuration.GetSection(IntegrationsOptions.SectionName));

        services.AddScoped<ServiceTokenService>();
        services.AddScoped<IdempotencyService>();
        services.AddScoped<AutomationService>();
        services.AddSingleton<IDnsResolver, DnsResolver>();
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<IntegrationsOptions>>().Value;
            return new WebhookRoutePlanner(options.Webhooks.Allowlist, sp.GetRequiredService<IDnsResolver>());
        });
        services.AddSingleton<IWebhookSender, SsrfWebhookSender>();
        services.AddScoped<WebhookDeliveryService>();
        services.AddHostedService<WebhookDeliveryWorker>();

        // Service-token authentication for /api/v1/automation; JWT bearer
        // remains the default scheme for all other endpoints.
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, ServiceTokenHandler>(AutomationAuthorization.SchemeName, null);
        services.AddAuthorization(options => AutomationAuthorization.AddPolicies(options));
        services.AddSingleton<IAuthorizationHandler, AutomationScopeHandler>();

        return services;
    }
}
