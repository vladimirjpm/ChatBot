namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// UI-строки из <c>locales/{lang}.json</c>. POCO для десериализации через System.Text.Json.
///
/// Содержит только строки интерфейса. System prompts для LLM хранятся отдельно в <see cref="Prompts"/>.
/// </summary>
public sealed class Locale
{
    /// <summary>
    /// UI-строки (фронтенд). Хранятся как raw JsonElement — не дублируем схему в C#,
    /// фронтенд парсит их напрямую.
    /// </summary>
    public System.Text.Json.JsonElement? Ui { get; init; }

    /// <summary>
    /// Подставляет значения плейсхолдеров вида <c>{key}</c> в строку шаблона.
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
