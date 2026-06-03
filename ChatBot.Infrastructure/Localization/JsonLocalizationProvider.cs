using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Loads locales from <c>{lang}.json</c> files in the given directory at process startup.
/// Used as a singleton — files are read once; everything is kept in memory afterward.
///
/// .NET: equivalent of <c>IConfiguration</c> with JsonConfigurationProvider but without sections —
/// each file is one <see cref="Locale"/>.
/// </summary>
public sealed class JsonLocalizationProvider : ILocalizationProvider
{
    private readonly Dictionary<string, Locale> _locales = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultLanguage;

    public IReadOnlyCollection<string> AvailableLanguages => _locales.Keys;

    /// <summary>
    /// Scans <paramref name="localesDirectory"/> for <c>*.json</c> files; the name without extension
    /// becomes the language code. Throws on startup if the directory is empty or JSON is invalid —
    /// fail-fast as per configuration best practice.
    /// </summary>
    public JsonLocalizationProvider(string localesDirectory, string defaultLanguage, ILogger<JsonLocalizationProvider> logger)
    {
        _defaultLanguage = defaultLanguage;

        if (!Directory.Exists(localesDirectory))
            throw new DirectoryNotFoundException($"Locales directory not found: {localesDirectory}");

        var files = Directory.GetFiles(localesDirectory, "*.json");
        if (files.Length == 0)
            throw new InvalidOperationException($"No *.json files found in {localesDirectory}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            using var stream = File.OpenRead(file);
            var locale = JsonSerializer.Deserialize<Locale>(stream, options)
                ?? throw new InvalidOperationException($"Failed to deserialize {file}");
            _locales[lang] = locale;
            logger.LogInformation("Loaded locale {Lang} from {File}", lang, file);
        }

        if (!_locales.ContainsKey(_defaultLanguage))
            throw new InvalidOperationException(
                $"Default language '{_defaultLanguage}' not found among loaded locales: {string.Join(", ", _locales.Keys)}");
    }

    public Locale Get(string? language)
    {
        if (!string.IsNullOrEmpty(language) && _locales.TryGetValue(language, out var locale))
            return locale;
        return _locales[_defaultLanguage];
    }
}
