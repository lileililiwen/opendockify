using System.Text.RegularExpressions;

namespace OpenDockify.AiAssist.Services;

/// <summary>
/// Output legal-advice classifier. Flags model output that reads like
/// actionable legal advice so the API can attach a warning banner. The
/// classifier never blocks — the drafting-tool disclaimer stays authoritative
/// and the polished text is always returned.
/// </summary>
public static class AiLegalAdviceClassifier
{
    private static readonly Regex _advicePattern = new(
        @"建议(你|您)?(起诉|上诉|仲裁|报警|维权|解除合同|拒绝履行)|应当承担(法律|违约|侵权)责任|不承担任何(法律)?责任|legal advice|you should sue|no legal (liability|validity)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase,
        TimeSpan.FromSeconds(1));

    public const string LegalAdviceWarning =
        "AI 输出疑似包含法律行动建议，仅供起草参考，不构成法律意见；正式签署前请咨询执业律师。";

    public static bool LooksLikeLegalAdvice(string? text)
    {
        return !string.IsNullOrEmpty(text) && _advicePattern.IsMatch(text);
    }
}
