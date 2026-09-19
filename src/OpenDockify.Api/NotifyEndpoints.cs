using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Platform.Jobs;
using Platform.Mailing;

namespace OpenDockify.Api;

public static class NotifyEndpoints
{
    public static IEndpointRouteBuilder MapNotifyEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/api/admin/mail").RequireAuthorization("RequireAdmin");
        admin.MapGet("/status", (IMailService mail, IOptions<NotifyOptions> options) =>
        {
            var provider = mail.GetType().Name;
            var status = mail is Platform.Mailing.Smtp.SmtpMailService smtp ? smtp.Status.ToString() : "dev-fallback:Healthy";
            return Results.Ok(new
            {
                enabled = options.Value.Enabled,
                provider,
                status,
                from = options.Value.FromAddress,
            });
        });
        return endpoints;
    }

    public static IServiceProvider RegisterNotifyRecurringJobs(this IServiceProvider services)
    {
        var registry = services.GetRequiredService<IRecurringJobRegistry>();
        registry.Register(RecurringJobAttribute.GetDescriptor(typeof(PlatformWebhookDispatchJobHandler)));
        return services;
    }
}
