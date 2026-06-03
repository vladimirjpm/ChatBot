namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Провайдер system prompts для LLM. Аналог <see cref="ILocalizationProvider"/> но для промптов.
///
/// .NET: аналогично IOptions&lt;T&gt; с поддержкой нескольких языков.
/// </summary>
public interface IPromptProvider
{
    /// <summary>Возвращает промпты для языка. Фолбэк на язык по умолчанию.</summary>
    Prompts Get(string? language);
}
