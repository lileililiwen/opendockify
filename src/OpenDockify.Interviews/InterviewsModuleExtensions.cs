using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Interviews.Services;

namespace OpenDockify.Interviews;

public static class InterviewsModuleExtensions
{
    public static IServiceCollection AddInterviewsModule(this IServiceCollection services)
    {
        services.AddScoped<InterviewSessionService>();
        services.AddHostedService<ExpiredInterviewCleanupService>();
        return services;
    }
}
