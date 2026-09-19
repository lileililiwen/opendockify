using OpenDockify.AiAssist.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class AiGuardTests
{
    [Fact]
    public void StripFabricated_removes_amount_not_present_in_input()
    {
        const string original = "乙方逾期还款的，应向甲方支付违约金。";
        const string llmOutput = "乙方逾期还款的，应向甲方支付违约金人民币50000元整。";

        var result = AiGuard.StripFabricated(original, llmOutput);

        Assert.DoesNotContain("50000", result);
        Assert.Contains("***", result);
        Assert.Contains("违约金", result);
    }

    [Fact]
    public void StripFabricated_removes_fabricated_18_digit_id()
    {
        const string original = "借款人张三确认上述借款事实。";
        const string llmOutput = "借款人张三（身份证号11010519491231002X）确认上述借款事实。";

        var result = AiGuard.StripFabricated(original, llmOutput);

        Assert.DoesNotContain("11010519491231002X", result);
        Assert.DoesNotContain("11010519491231002", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void StripFabricated_keeps_numbers_present_in_input()
    {
        const string original = "借款金额为1234元。";
        const string llmOutput = "借款金额为1234元，逾期按1%支付违约金。";

        var result = AiGuard.StripFabricated(original, llmOutput);

        Assert.Contains("1234", result);
        Assert.Contains("1%", result);
    }

    [Fact]
    public void Tokenize_then_restore_round_trips_byte_for_byte()
    {
        var renderedValues = new Dictionary<string, string>
        {
            ["borrowerName"] = "张三",
            ["amount"] = "壹仟贰佰叁拾肆元整",
        };
        const string original = "借款人张三于2026年8月20日向出借人借款壹仟贰佰叁拾肆元整。";

        var tokenized = AiGuard.TokenizeValues(original, renderedValues);
        var restored = AiGuard.RestoreMandatoryValues(tokenized, renderedValues);

        Assert.NotNull(restored);
        Assert.Equal(original, restored);
    }

    [Fact]
    public void Tokenize_hides_raw_values_from_llm()
    {
        var renderedValues = new Dictionary<string, string>
        {
            ["borrowerName"] = "张三",
            ["amount"] = "壹仟贰佰叁拾肆元整",
        };

        var tokenized = AiGuard.TokenizeValues("借款人张三借款壹仟贰佰叁拾肆元整。", renderedValues);

        Assert.DoesNotContain("张三", tokenized);
        Assert.DoesNotContain("壹仟贰佰叁拾肆元整", tokenized);
        Assert.Contains("⟦borrowerName⟧", tokenized);
        Assert.Contains("⟦amount⟧", tokenized);
    }

    [Fact]
    public void RestoreMandatoryValues_returns_null_when_token_dropped()
    {
        var renderedValues = new Dictionary<string, string>
        {
            ["borrowerName"] = "张三",
            ["amount"] = "壹仟贰佰叁拾肆元整",
        };

        var restored = AiGuard.RestoreMandatoryValues("借款⟦amount⟧元。", renderedValues);

        Assert.Null(restored);
    }

    [Fact]
    public void RestoreMandatoryValues_fails_safe_when_llm_alters_value()
    {
        var renderedValues = new Dictionary<string, string>
        {
            ["borrowerName"] = "张三",
            ["amount"] = "壹仟贰佰叁拾肆元整",
        };
        const string llmOutput = "借款金额为⟦amount⟧元，借款人⟦borrowerName⟧。";

        var restored = AiGuard.RestoreMandatoryValues(llmOutput, renderedValues);

        Assert.Equal("借款金额为壹仟贰佰叁拾肆元整元，借款人张三。", restored);
    }

    [Fact]
    public void RemoveUnselectedClauseTitles_strips_unselected_clause_content()
    {
        const string polished = "本协议自签署之日起生效。\n\n违约金条款：逾期还款应支付违约金。";

        var result = AiGuard.RemoveUnselectedClauseTitles(polished, ["违约金条款"]);

        Assert.DoesNotContain("违约金条款", result);
        Assert.Contains("本协议自签署之日起生效", result);
    }

    [Fact]
    public void RemoveUnselectedClauseTitles_leaves_selected_clause_untouched()
    {
        const string polished = "本协议自签署之日起生效。";

        var result = AiGuard.RemoveUnselectedClauseTitles(polished, []);

        Assert.Equal(polished, result);
    }
}

public sealed class AiUsageLogRedactionTests
{
    [Fact]
    public void RedactSensitive_masks_18_digit_id_number()
    {
        var result = AiUsageLogService.RedactSensitive("身份证号11010519491231002X");

        Assert.DoesNotContain("11010519491231002X", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void RedactSensitive_masks_long_digit_runs()
    {
        var result = AiUsageLogService.RedactSensitive("账号123456789012");

        Assert.DoesNotContain("123456789012", result);
        Assert.Contains("***", result);
    }

    [Fact]
    public void RedactSensitive_keeps_short_numbers()
    {
        var result = AiUsageLogService.RedactSensitive("利率3.45%");

        Assert.Equal("利率3.45%", result);
    }

    [Fact]
    public void RedactSensitive_truncates_oversized_snippets()
    {
        var input = new string('a', 2500);

        var result = AiUsageLogService.RedactSensitive(input);

        Assert.True(result.Length <= 500);
    }
}
