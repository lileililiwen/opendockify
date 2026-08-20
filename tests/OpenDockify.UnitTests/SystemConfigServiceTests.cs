using OpenDockify.SystemConfig.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class SettingKeysTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    public void TryValidate_bool_accepts_true_false(string raw)
    {
        var definition = SettingKeys.Find(SettingKeys.AiEnabled)!;

        var ok = SettingKeys.TryValidate(definition, raw, out var json, out _);

        Assert.True(ok);
        Assert.Equal(raw, json);
    }

    [Fact]
    public void TryValidate_bool_rejects_non_bool()
    {
        var definition = SettingKeys.Find(SettingKeys.AiEnabled)!;

        var ok = SettingKeys.TryValidate(definition, "yes", out _, out var error);

        Assert.False(ok);
        Assert.Contains("true or false", error);
    }

    [Fact]
    public void TryValidate_number_accepts_decimal()
    {
        var definition = SettingKeys.Find(SettingKeys.LprOneYearRate)!;

        var ok = SettingKeys.TryValidate(definition, "3.45", out var json, out _);

        Assert.True(ok);
        Assert.Equal("3.45", json);
    }

    [Fact]
    public void TryValidate_number_rejects_out_of_range()
    {
        var definition = SettingKeys.Find(SettingKeys.LprOneYearRate)!;

        var ok = SettingKeys.TryValidate(definition, "150", out _, out var error);

        Assert.False(ok);
        Assert.Contains("0 and 100", error);
    }

    [Fact]
    public void TryValidate_url_rejects_non_http()
    {
        var definition = SettingKeys.Find(SettingKeys.AiEndpoint)!;

        var ok = SettingKeys.TryValidate(definition, "ftp://example.com", out _, out var error);

        Assert.False(ok);
        Assert.Contains("http(s) URL", error);
    }

    [Fact]
    public void TryValidate_text_returns_quoted_json()
    {
        var definition = SettingKeys.Find(SettingKeys.AiModel)!;

        var ok = SettingKeys.TryValidate(definition, "gpt-4o", out var json, out _);

        Assert.True(ok);
        Assert.Equal("\"gpt-4o\"", json);
    }

    [Fact]
    public void Find_rejects_unknown_keys()
    {
        Assert.Null(SettingKeys.Find("Unknown.Key"));
    }
}

public sealed class SecretRedactorTests
{
    [Fact]
    public void MaskSecret_shows_last_four_chars()
    {
        var masked = SecretRedactor.MaskSecret("sk-very-secret-1234");

        Assert.Equal("••••1234", masked);
        Assert.DoesNotContain("sk-very-secret", masked);
    }

    [Fact]
    public void MaskSecret_handles_short_values()
    {
        Assert.Equal("••••ab", SecretRedactor.MaskSecret("ab"));
        Assert.Equal("••••", SecretRedactor.MaskSecret(null));
    }

    [Fact]
    public void Redact_replaces_secret_in_text()
    {
        var redacted = SecretRedactor.Redact("request to sk-abc123 done", "sk-abc123");

        Assert.DoesNotContain("sk-abc123", redacted);
        Assert.Contains("••••", redacted);
    }
}
