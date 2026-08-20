using OpenDockify.SystemConfig.Services;
using OpenDockify.Templates.Models;
using OpenDockify.Templates.Services;

namespace OpenDockify.AiAssist.Services;

public enum AiAssistStatus
{
    Ok,
    Disabled,
    RateLimited,
}

public sealed record AiAssistResult(string? Text, string? Warning, AiAssistStatus Status)
{
    public static AiAssistResult Succeeded(string text, string warning)
    {
        return new(text, warning, AiAssistStatus.Ok);
    }

    public static AiAssistResult Disabled()
    {
        return new(null, null, AiAssistStatus.Disabled);
    }

    public static AiAssistResult RateLimited()
    {
        return new(null, null, AiAssistStatus.RateLimited);
    }
}

/// <summary>
/// Orchestrates the gated AI polish flows. All entry points check
/// <c>Ai.Enabled</c> first (no LLM call when disabled) and an optional per-user
/// daily rate limit (<c>Ai.RateLimitPerDay</c>, default off). On LLM failure
/// the original text is returned with a warning (best-effort) and the call is
/// logged.
/// </summary>
public sealed class AiAssistService(
    ISystemConfigReader config,
    TemplateService templateService,
    ILlmClient llmClient,
    AiUsageLogService usageLog)
{
    private const string _sensitivityWarning =
        "AI 润色仅供辅助，请勿向公共大模型传输身份证号等敏感信息；隐私敏感场景建议使用本地 Ollama。";

    public async Task<AiAssistResult> PolishClauseAsync(
        Guid userId,
        Guid templateId,
        string draft,
        CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return AiAssistResult.Disabled();
        }

        if (await IsRateLimitedAsync(userId, cancellationToken))
        {
            return AiAssistResult.RateLimited();
        }

        var templateResult = await templateService.GetByIdAsync(userId, templateId, cancellationToken);
        if (templateResult.NotFound)
        {
            return new AiAssistResult(null, "模板不存在。", AiAssistStatus.Ok);
        }

        var definitionResult = TemplateDefinitionValidator.Validate(templateResult.Value!.DefinitionJson, templateResult.Value.Body);
        if (definitionResult.Error is not null)
        {
            return new AiAssistResult(null, definitionResult.Error, AiAssistStatus.Ok);
        }

        var systemPrompt = PromptBuilder.BuildClauseSystemPrompt(definitionResult.Definition!);
        var llm = await llmClient.CompleteAsync(systemPrompt, draft, cancellationToken);

        if (!llm.Succeeded)
        {
            await usageLog.LogAsync(userId, "polish-clause", draft, llm.Error, success: false, cancellationToken);
            return new AiAssistResult(draft, $"AI 不可用，已返回原文。{_sensitivityWarning}", AiAssistStatus.Ok);
        }

        var polished = AiGuard.StripFabricated(draft, llm.Content!);
        await usageLog.LogAsync(userId, "polish-clause", draft, polished, success: true, cancellationToken);

        return AiAssistResult.Succeeded(polished, _sensitivityWarning);
    }

    public async Task<AiAssistResult> PolishDocumentAsync(
        Guid userId,
        Guid templateId,
        string renderedText,
        IReadOnlyDictionary<string, string> values,
        IReadOnlyCollection<string> selectedClauseIds,
        CancellationToken cancellationToken = default)
    {
        if (!await IsEnabledAsync(cancellationToken))
        {
            return AiAssistResult.Disabled();
        }

        if (await IsRateLimitedAsync(userId, cancellationToken))
        {
            return AiAssistResult.RateLimited();
        }

        var templateResult = await templateService.GetByIdAsync(userId, templateId, cancellationToken);
        if (templateResult.NotFound)
        {
            return new AiAssistResult(null, "模板不存在。", AiAssistStatus.Ok);
        }

        var definitionResult = TemplateDefinitionValidator.Validate(templateResult.Value!.DefinitionJson, templateResult.Value.Body);
        if (definitionResult.Error is not null)
        {
            return new AiAssistResult(null, definitionResult.Error, AiAssistStatus.Ok);
        }

        var definition = definitionResult.Definition!;
        var renderedValues = RenderMandatoryValues(definition, values);

        var tokenized = AiGuard.TokenizeValues(renderedText, renderedValues);
        var systemPrompt = PromptBuilder.BuildDocumentSystemPrompt(definition);
        var llm = await llmClient.CompleteAsync(systemPrompt, tokenized, cancellationToken);

        if (!llm.Succeeded)
        {
            await usageLog.LogAsync(userId, "polish-document", renderedText, llm.Error, success: false, cancellationToken);
            return new AiAssistResult(renderedText, $"AI 不可用，已返回原文。{_sensitivityWarning}", AiAssistStatus.Ok);
        }

        var restored = AiGuard.RestoreMandatoryValues(llm.Content!, renderedValues);
        if (restored is null)
        {
            // A mandatory value token was dropped — fail safe to the original.
            await usageLog.LogAsync(userId, "polish-document", renderedText, "mandatory values not preserved", success: false, cancellationToken);
            return new AiAssistResult(renderedText, $"AI 未能保留全部必填字段值，已返回原文。{_sensitivityWarning}", AiAssistStatus.Ok);
        }

        var unselectedTitles = definition.Clauses
            .Where(c => !selectedClauseIds.Contains(c.Id))
            .Select(c => c.Title)
            .ToArray();
        var polished = AiGuard.RemoveUnselectedClauseTitles(restored, unselectedTitles);

        await usageLog.LogAsync(userId, "polish-document", renderedText, polished, success: true, cancellationToken);

        return AiAssistResult.Succeeded(polished, _sensitivityWarning);
    }

    private async Task<bool> IsEnabledAsync(CancellationToken cancellationToken)
    {
        var enabled = await config.GetAsync<bool>(SettingKeys.AiEnabled, cancellationToken);
        return enabled;
    }

    private async Task<bool> IsRateLimitedAsync(Guid userId, CancellationToken cancellationToken)
    {
        var limit = await config.GetAsync<int>(SettingKeys.AiRateLimitPerDay, cancellationToken);
        if (limit <= 0)
        {
            return false;
        }

        var today = await usageLog.CountTodayAsync(userId, cancellationToken);
        return today >= limit;
    }

    /// <summary>
    /// Computes the canonical rendered form (e.g. RMB uppercase for currency,
    /// Chinese date format) of every provided field value, using the same
    /// renderer the document generation used.
    /// </summary>
    private static Dictionary<string, string> RenderMandatoryValues(
        TemplateDefinition definition,
        IReadOnlyDictionary<string, string> values)
    {
        var rendered = new Dictionary<string, string>();
        foreach (var fieldName in definition.Fields.Select(f => f.Name))
        {
            if (!values.TryGetValue(fieldName, out var raw) || string.IsNullOrEmpty(raw))
            {
                continue;
            }

            var singleField = new Dictionary<string, string> { [fieldName] = raw };
            var result = TemplateRenderer.Render(definition, $"{{{{{fieldName}}}}}", singleField, []);
            if (result.Text is not null)
            {
                rendered[fieldName] = result.Text;
            }
        }

        return rendered;
    }
}
