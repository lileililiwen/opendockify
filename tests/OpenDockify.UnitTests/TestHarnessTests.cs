using Xunit;

namespace OpenDockify.UnitTests;

/// <summary>
/// Harness validation only. Real unit tests arrive with the
/// <c>finance-conversion</c> change (AmountToChinese, InterestRateValidator)
/// and the template validator/renderer.
/// </summary>
public sealed class TestHarnessTests
{
    [Fact]
    public void Test_harness_runs()
    {
        Assert.True(true);
    }
}
