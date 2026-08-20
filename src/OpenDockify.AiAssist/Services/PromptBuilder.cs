using System.Text;
using OpenDockify.Templates.Models;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// Builds the system prompts for clause/document polish, injecting the
/// template's declared fields, types, and clause ids as context and strictly
/// forbidding fabrication of amounts, ID numbers, dates, or unselected
/// clauses.
/// </summary>
public static class PromptBuilder
{
    public static string BuildClauseSystemPrompt(TemplateDefinition definition)
    {
        return $"""
            你是一名中文法律文书起草助手。用户会提交一段草拟条款文本，请将其润色为规范、严谨的合同条款语言，保留原意与语气。

            严格要求：
            1. 不得虚构任何金额、身份证号、日期或其他数字——只可使用输入中出现的数值。
            2. 不得添加用户未提供或未选择的条款内容。
            3. 不得改变任何既有的事实性信息。
            4. 输出直接为润色后的条款文本，不要任何解释或前缀。

            模板字段上下文（不得引入未列出的字段）：
            {FormatFields(definition)}

            可选条款 id（仅这些可以被提及）：{string.Join(", ", definition.Clauses.Select(c => c.Id))}
            """;
    }

    public static string BuildDocumentSystemPrompt(TemplateDefinition definition)
    {
        return $"""
            你是一名中文法律文书润色助手。用户提交一份已生成的合同文档全文，请润色其文字表达，使其更加通顺、严谨，但不得改动任何事实。

            严格要求：
            1. 文本中以 ⟦字段名⟧ 形式出现的占位符是强制性字段值，必须原样保留，不得修改、删除或替换其内容。
            2. 不得虚构任何金额、身份证号、日期或其他数字。
            3. 不得添加与字段无关的新事实，不得引入未选条款内容。
            4. 输出直接为润色后的全文，不要任何解释或前缀。

            模板字段上下文：
            {FormatFields(definition)}
            """;
    }

    private static string FormatFields(TemplateDefinition definition)
    {
        var sb = new StringBuilder();
        foreach (var field in definition.Fields)
        {
            var label = string.IsNullOrWhiteSpace(field.Label) ? field.Name : field.Label;
            sb.Append("- ")
              .Append(field.Name)
              .Append('（')
              .Append(label)
              .Append("，类型 ")
              .Append(TypeName(field.Type))
              .Append('，')
              .Append(field.Required ? "必填" : "可选")
              .AppendLine("）");
        }

        return sb.ToString().TrimEnd();
    }

    private static string TypeName(FieldType type)
    {
        return type switch
        {
            FieldType.Text => "string",
            FieldType.Number => "number",
            FieldType.Currency => "currency",
            FieldType.Date => "date",
            _ => "unknown",
        };
    }
}
