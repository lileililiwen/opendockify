using OpenDockify.SystemConfig.Services;

namespace OpenDockify.Finance.Services;

/// <summary>
/// Adapter that reads the one-year LPR from system configuration
/// (<c>Lpr.OneYearRate</c>, resolved env &gt; DB &gt; default) and delegates to
/// the pure <see cref="InterestRateValidator"/>.
/// </summary>
public sealed class InterestRateService(ISystemConfigReader config)
{
    public async Task<InterestRateValidation> ValidateAsync(
        decimal annualRatePct,
        CancellationToken cancellationToken = default)
    {
        var lpr = await config.GetAsync<decimal>(SettingKeys.LprOneYearRate, cancellationToken);
        return InterestRateValidator.Validate(annualRatePct, lpr);
    }
}
