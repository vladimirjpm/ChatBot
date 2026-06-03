namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Deserialized locale from a JSON file. POCO for easy mapping via System.Text.Json.
///
/// Template strings contain curly-brace placeholders: <c>{roleName}</c>, <c>{resume}</c>.
/// Substitution is done via <see cref="Format"/>.
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
    /// UI strings (frontend). Not used in the backend — stored as raw JsonElement
    /// to avoid duplicating the schema in C#. The frontend parses it directly.
    /// </summary>
    public System.Text.Json.JsonElement? Ui { get; init; }

    /// <summary>
    /// Substitutes placeholder values of the form <c>{key}</c> in a template string.
    /// .NET: like string.Format but with named placeholders instead of positional ones.
    /// </summary>
    public static string Format(string template, IReadOnlyDictionary<string, string> args)
    {
        var result = template;
        foreach (var (k, v) in args)
            result = result.Replace("{" + k + "}", v);
        return result;
    }
}
