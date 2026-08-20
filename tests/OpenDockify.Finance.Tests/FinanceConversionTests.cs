using OpenDockify.Finance.Services;
using Xunit;

namespace OpenDockify.Finance.Tests;

public sealed class AmountToChineseTests
{
    [Theory]
    [InlineData(0, "零元整")]
    [InlineData(1234, "壹仟贰佰叁拾肆元整")]
    [InlineData(1234.5, "壹仟贰佰叁拾肆元伍角")]
    [InlineData(1234.56, "壹仟贰佰叁拾肆元伍角陆分")]
    [InlineData(0.05, "伍分")]
    [InlineData(0.5, "伍角")]
    [InlineData(1001, "壹仟零壹元整")]
    [InlineData(1000001, "壹佰万零壹元整")]
    [InlineData(1.999, "贰元整")]
    [InlineData(10, "壹拾元整")]
    [InlineData(11, "壹拾壹元整")]
    [InlineData(101, "壹佰零壹元整")]
    [InlineData(1010, "壹仟零壹拾元整")]
    [InlineData(100000000, "壹亿元整")]
    public void Convert_returns_expected_uppercase(decimal amount, string expected)
    {
        var result = AmountToChinese.Convert(amount);

        Assert.True(result.Succeeded, result.Error);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Convert_rejects_negative_amounts()
    {
        var result = AmountToChinese.Convert(-100);

        Assert.False(result.Succeeded);
        Assert.Null(result.Value);
        Assert.Contains("negative", result.Error);
    }
}

public sealed class InterestRateValidatorTests
{
    [Fact]
    public void Validate_rate_at_or_below_lpr_is_ok()
    {
        var result = InterestRateValidator.Validate(3.0m, 3.45m);
        Assert.Equal(RateLevel.Ok, result.Level);

        result = InterestRateValidator.Validate(3.45m, 3.45m);
        Assert.Equal(RateLevel.Ok, result.Level);
    }

    [Fact]
    public void Validate_rate_above_lpr_below_cap_is_over_lpr()
    {
        var result = InterestRateValidator.Validate(10.0m, 3.45m);

        Assert.Equal(RateLevel.OverLpr, result.Level);
        Assert.DoesNotContain("4倍", result.Message);
    }

    [Fact]
    public void Validate_rate_above_cap_is_over_cap()
    {
        var result = InterestRateValidator.Validate(15.0m, 3.45m);

        Assert.Equal(RateLevel.OverCap, result.Level);
        Assert.Contains("4倍", result.Message);
    }

    [Fact]
    public void Validate_rate_equal_to_cap_is_over_lpr_not_over_cap()
    {
        var result = InterestRateValidator.Validate(3.45m * 4, 3.45m);

        Assert.Equal(RateLevel.OverLpr, result.Level);
    }

    [Fact]
    public void Validate_changed_lpr_changes_levels()
    {
        // With LPR 5.0 the same 10% rate is within the 20% cap → OverLpr.
        var lower = InterestRateValidator.Validate(10.0m, 5.0m);
        Assert.Equal(RateLevel.OverLpr, lower.Level);

        // With LPR 2.0 the cap is 8% → 10% is OverCap.
        var higher = InterestRateValidator.Validate(10.0m, 2.0m);
        Assert.Equal(RateLevel.OverCap, higher.Level);
    }
}
