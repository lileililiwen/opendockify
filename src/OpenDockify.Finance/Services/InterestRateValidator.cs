namespace OpenDockify.Finance.Services;

public enum RateLevel
{
    Ok,
    OverLpr,
    OverCap,
}

public sealed record InterestRateValidation(RateLevel Level, string Message);

/// <summary>
/// Validates an annual interest rate (in percent) against the one-year LPR
/// reference. Advisory only: callers surface a warning but never block
/// document generation.
/// </summary>
public static class InterestRateValidator
{
    /// <summary>
    /// Judicial protection cap multiplier — the current rule (最高人民法院
    /// 民间借贷司法解释) protects interest up to 4× the one-year LPR. A one-line
    /// change here adjusts the rule; deliberately not a DB setting for MVP.
    /// </summary>
    public const decimal JudicialProtectionCapMultiplier = 4.0m;

    public static InterestRateValidation Validate(decimal annualRatePct, decimal lprPct)
    {
        if (annualRatePct <= lprPct)
        {
            return new InterestRateValidation(RateLevel.Ok, $"年利率{annualRatePct}%未超过一年期LPR参考值（{lprPct}%）。");
        }

        var cap = lprPct * JudicialProtectionCapMultiplier;
        if (annualRatePct > cap)
        {
            return new InterestRateValidation(
                RateLevel.OverCap,
                $"年利率{annualRatePct}%已超过一年期LPR（{lprPct}%）4倍的法律保护上限（{cap}%），超出部分可能不受法律保护。");
        }

        return new InterestRateValidation(
            RateLevel.OverLpr,
            $"年利率{annualRatePct}%已超过一年期LPR参考值（{lprPct}%），请注意利率合规风险。");
    }
}
