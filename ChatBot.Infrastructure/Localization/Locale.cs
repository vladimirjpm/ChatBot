namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Десериализованная локаль из JSON-файла. POCO для удобного маппинга через System.Text.Json.
///
/// Шаблонные строки содержат плейсхолдеры в фигурных скобках: <c>{roleName}</c>, <c>{resume}</c>.
/// Подстановка через <see cref="Format"/>.
/// </summary>
public sealed class Locale
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

    /// <summary>
    /// UI-строки (фронтенд). Не используются в backend — храним как сырой JsonElement,
    /// чтобы не повторять схему в C#. Фронт сам разбирает.
    /// </summary>
    public System.Text.Json.JsonElement? Ui { get; init; }

    /// <summary>
    /// Подставляет значения плейсхолдеров формата <c>{key}</c> в шаблонной строке.
    /// .NET: аналог string.Format, но с именованными плейсхолдерами вместо позиционных.
    /// </summary>
    public static string Format(string template, IReadOnlyDictionary<string, string> args)
    {
        var result = template;
        foreach (var (k, v) in args)
            result = result.Replace("{" + k + "}", v);
        return result;
    }
}
