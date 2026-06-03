namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Locale provider. The abstraction allows swapping the JSON source for a DB / Crowdin API / Redis
/// without touching consumers (ChatService).
///
/// .NET: similar to IStringLocalizerFactory but simpler — instead of individual keys
/// we return a fully typed <see cref="Locale"/> object.
/// </summary>
public interface ILocalizationProvider
{
    /// <summary>
    /// Returns the locale for a given language code. Falls back to the default language if not loaded.
    /// </summary>
    Locale Get(string? language);

    /// <summary>List of loaded languages (for health checks / diagnostics).</summary>
    IReadOnlyCollection<string> AvailableLanguages { get; }
}
