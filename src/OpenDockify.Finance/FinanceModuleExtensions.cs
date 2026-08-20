using Microsoft.Extensions.DependencyInjection;
using OpenDockify.Finance.Services;

namespace OpenDockify.Finance;

public static class FinanceModuleExtensions
{
    public static IServiceCollection AddFinanceModule(this IServiceCollection services)
    {
        services.AddScoped<InterestRateService>();
        return services;
    }
}
