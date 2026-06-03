namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// System prompts для LLM, десериализованные из <c>prompts/{lang}.json</c>.
///
/// Отделены от <see cref="Locale"/> (UI-строки), потому что меняются по разным причинам:
/// промпты — при изменении поведения модели, UI — при изменении интерфейса.
/// </summary>
public sealed class Prompts
{
    public string AssistantBase { get; init; } = "";
    public string GenericAssistant { get; init; } = "";
    public string GroundingHard { get; init; } = "";
    public string GroundingSoft { get; init; } = "";
    public string ContextHeader { get; init; } = "";
    public string InterviewRules { get; init; } = "";
    public string InterviewResumeAddendum { get; init; } = "";
    public string InterviewStartCue { get; init; } = "";
    public Dictionary<string, string> RoleNames { get; init; } = new();
    public string UnknownRoleSuffix { get; init; } = "";
}
