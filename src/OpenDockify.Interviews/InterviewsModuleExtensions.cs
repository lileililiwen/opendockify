using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Interviews.Services;
using Platform.Jobs;
using Platform.Jobs.DependencyInjection;

namespace OpenDockify.Interviews;

public static class InterviewsModuleExtensions
{
    public static IServiceCollection AddInterviewsModule(this IServiceCollection services)
    {
        services.AddScoped<InterviewSessionService>();
        services.AddPlatformJobs();
        services.AddScoped<ExpiredInterviewCleanupJobHandler>();
        return services;
    }

    public static IServiceProvider RegisterInterviewRecurringJobs(this IServiceProvider services)
    {
        var registry = services.GetRequiredService<IRecurringJobRegistry>();
        registry.Register(Platform.Jobs.RecurringJobAttribute.GetDescriptor(typeof(ExpiredInterviewCleanupJobHandler)));
        return services;
    }
}
