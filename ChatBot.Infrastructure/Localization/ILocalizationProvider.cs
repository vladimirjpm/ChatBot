namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Поставщик локалей. Абстракция нужна чтобы можно было заменить JSON-источник
/// на БД / Crowdin API / Redis без правок в потребителях (ChatService).
///
/// .NET: эквивалент IStringLocalizerFactory, только проще — не дробим на ключи,
/// а возвращаем целиком типизированный <see cref="Locale"/>.
/// </summary>
public interface ILocalizationProvider
{
    /// <summary>
    /// Возвращает локаль по коду языка. Fallback на дефолтный язык если такой не загружен.
    /// </summary>
    Locale Get(string? language);

    /// <summary>Список загруженных языков (для health-чека / диагностики).</summary>
    IReadOnlyCollection<string> AvailableLanguages { get; }
}
