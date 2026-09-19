using OpenDockify.SystemConfig.Services;
using Platform.Caching.Contracts;
using Platform.Caching.Keys;

namespace OpenDockify.Finance.Services;

/// <summary>
/// Adapter that reads the one-year LPR from system configuration
/// (<c>Lpr.OneYearRate</c>, resolved env &gt; DB &gt; default) and delegates to
/// the pure <see cref="InterestRateValidator"/>. The rate is cached for
/// <c>Cache.LprTtlHours</c> (default 24h) under the app-scoped LPR key and can
/// be invalidated via <c>POST /api/admin/cache/invalidate</c> (scope
/// <c>lpr</c>). The cache is optional — without a store every call reads
/// configuration directly.
/// </summary>
public sealed class InterestRateService(
    ISystemConfigReader config,
    ICacheStore? cache = null,
    CacheKeyBuilder? keys = null)
{
    public async Task<InterestRateValidation> ValidateAsync(
        decimal annualRatePct,
        CancellationToken cancellationToken = default)
    {
        var lpr = await ReadLprAsync(cancellationToken);
        return InterestRateValidator.Validate(annualRatePct, lpr);
    }

    private async Task<decimal> ReadLprAsync(CancellationToken cancellationToken)
    {
        if (cache is null || keys is null)
        {
            return await config.GetAsync<decimal>(SettingKeys.LprOneYearRate, cancellationToken);
        }

        var ttlHours = await config.GetAsync<int>(SettingKeys.CacheLprTtlHours, cancellationToken);
        var ttl = TimeSpan.FromHours(ttlHours > 0 ? ttlHours : 24);
        var key = keys.ForApplication("lpr:one-year-rate");
        var read = await cache.GetAsync<decimal>(key, cancellationToken);
        if (read.Status == CacheReadStatus.Hit)
        {
            return read.Value;
        }

        var lpr = await config.GetAsync<decimal>(SettingKeys.LprOneYearRate, cancellationToken);
        await cache.SetAsync(
            key,
            lpr,
            new CacheEntryOptions { AbsoluteExpirationRelativeToNow = ttl, Tags = ["lpr"] },
            cancellationToken);
        return lpr;
    }
}
