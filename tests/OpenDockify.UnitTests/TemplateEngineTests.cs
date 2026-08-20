using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;
using Xunit;

namespace OpenDockify.UnitTests;

public sealed class TemplateDefinitionValidatorTests
{
    private const string _validDefinition = """
        {
          "fields": [
            { "name": "borrowerName", "label": "借款人姓名", "type": "string", "required": true },
            { "name": "amount", "label": "金额", "type": "currency", "required": true }
          ],
          "clauses": [
            { "id": "penalty", "title": "违约金", "text": "逾期应支付违约金。" }
          ]
        }
        """;

    private const string _validBody = "借款人{{borrowerName}}借款{{amount}}元。";

    [Fact]
    public void Validate_accepts_valid_definition()
    {
        var result = TemplateDefinitionValidator.Validate(_validDefinition, _validBody);

        Assert.True(result.IsValid, result.Error);
        Assert.Equal(2, result.Definition!.Fields.Count);
        Assert.Single(result.Definition.Clauses);
    }

    [Fact]
    public void Validate_rejects_unknown_field_type()
    {
        const string json = """
            { "fields": [ { "name": "x", "type": "money" } ], "clauses": [] }
            """;

        var result = TemplateDefinitionValidator.Validate(json, "");

        Assert.False(result.IsValid);
        Assert.Equal(DefinitionErrorKind.InvalidJson, result.Kind);
        Assert.Contains("Unknown field type", result.Error);
    }

    [Fact]
    public void Validate_rejects_duplicate_clause_ids()
    {
        const string json = """
            { "fields": [], "clauses": [
              { "id": "a", "title": "1", "text": "x" },
              { "id": "a", "title": "2", "text": "y" }
            ] }
            """;

        var result = TemplateDefinitionValidator.Validate(json, "");

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate clause id 'a'", result.Error);
    }

    [Fact]
    public void Validate_rejects_undeclared_placeholder()
    {
        var result = TemplateDefinitionValidator.Validate(_validDefinition, "引用{{missingVar}}占位符。");

        Assert.False(result.IsValid);
        Assert.Contains("undeclared field 'missingVar'", result.Error);
    }

    [Fact]
    public void Validate_rejects_oversized_definition()
    {
        var oversized = "{\"fields\":[],\"clauses\":[]}".PadRight(TemplateDefinitionValidator.MaxDefinitionChars + 1, ' ');

        var result = TemplateDefinitionValidator.Validate(oversized, "");

        Assert.False(result.IsValid);
        Assert.Equal(DefinitionErrorKind.TooLarge, result.Kind);
    }

    [Fact]
    public void Validate_rejects_malformed_json()
    {
        var result = TemplateDefinitionValidator.Validate("{ not json", "");

        Assert.False(result.IsValid);
        Assert.Equal(DefinitionErrorKind.InvalidJson, result.Kind);
    }

    [Fact]
    public void Validate_rejects_unknown_json_property()
    {
        const string json = """
            { "fields": [ { "name": "x", "type": "string", "surprise": true } ], "clauses": [] }
            """;

        var result = TemplateDefinitionValidator.Validate(json, "");

        Assert.False(result.IsValid);
        Assert.Equal(DefinitionErrorKind.InvalidJson, result.Kind);
    }
}

public sealed class TemplateRendererTests
{
    private static readonly string[] _selectedPenalty = ["penalty"];

    private static readonly string[] _unknownClause = ["does-not-exist"];

    private static TemplateDefinition Definition()
    {
        return new TemplateDefinition
        {
            Fields =
            {
                new FieldDefinition { Name = "borrowerName", Label = "借款人", Type = FieldType.Text, Required = true },
                new FieldDefinition { Name = "amount", Label = "金额", Type = FieldType.Currency, Required = true },
                new FieldDefinition { Name = "loanDate", Label = "日期", Type = FieldType.Date, Required = true },
            },
            Clauses =
            {
                new ClauseDefinition { Id = "penalty", Title = "违约金", Text = "逾期按{{amount}}的1%支付违约金。" },
            },
        };
    }

    [Fact]
    public void Render_substitutes_placeholders_with_values()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款人{{borrowerName}}借款{{amount}}元。",
            new Dictionary<string, string> { ["borrowerName"] = "张三", ["amount"] = "1234" },
            []);

        Assert.True(result.Text is not null, result.Error);
        Assert.Contains("借款人张三", result.Text);
        Assert.DoesNotContain("{{", result.Text);
    }

    [Fact]
    public void Render_currency_field_as_rmb_uppercase()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款金额{{amount}}。",
            new Dictionary<string, string> { ["amount"] = "1234" },
            []);

        Assert.True(result.Text is not null, result.Error);
        Assert.Contains("壹仟贰佰叁拾肆元整", result.Text);
    }

    [Fact]
    public void Render_date_field_in_chinese_format()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款日期{{loanDate}}。",
            new Dictionary<string, string> { ["loanDate"] = "2026-08-20" },
            []);

        Assert.True(result.Text is not null, result.Error);
        Assert.Contains("2026年8月20日", result.Text);
    }

    [Fact]
    public void Render_missing_value_errors_with_field_name()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款人{{borrowerName}}。",
            new Dictionary<string, string> { ["amount"] = "1234" },
            []);

        Assert.Null(result.Text);
        Assert.Contains("borrowerName", result.Error);
    }

    [Fact]
    public void Render_appends_selected_clauses()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款{{amount}}。",
            new Dictionary<string, string> { ["amount"] = "1234" },
            _selectedPenalty);

        Assert.True(result.Text is not null, result.Error);
        Assert.Contains("逾期按壹仟贰佰叁拾肆元整的1%支付违约金", result.Text);
    }

    [Fact]
    public void Render_omits_unselected_clauses()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款{{amount}}。",
            new Dictionary<string, string> { ["amount"] = "1234" },
            []);

        Assert.True(result.Text is not null, result.Error);
        Assert.DoesNotContain("违约金", result.Text);
    }

    [Fact]
    public void Render_rejects_unknown_clause_id()
    {
        var result = TemplateRenderer.Render(
            Definition(),
            "借款{{amount}}。",
            new Dictionary<string, string> { ["amount"] = "1234" },
            _unknownClause);

        Assert.Null(result.Text);
        Assert.Contains("does-not-exist", result.Error);
    }
}
