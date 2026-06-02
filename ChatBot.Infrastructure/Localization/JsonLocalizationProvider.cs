using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Загружает локали из JSON-файлов <c>{lang}.json</c> в заданной папке при старте процесса.
/// Используется как singleton — файлы читаются один раз, дальше всё в памяти.
///
/// .NET: эквивалент <c>IConfiguration</c> с JsonConfigurationProvider, но без секций —
/// каждый файл это один <see cref="Locale"/>.
/// </summary>
public sealed class JsonLocalizationProvider : ILocalizationProvider
{
    private readonly Dictionary<string, Locale> _locales = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultLanguage;

    public IReadOnlyCollection<string> AvailableLanguages => _locales.Keys;

    /// <summary>
    /// Сканирует <paramref name="localesDirectory"/> на файлы <c>*.json</c>, имя без расширения
    /// становится кодом языка. Падает на старте если папка пуста или JSON некорректный —
    /// fail-fast по best practice для конфигурации.
    /// </summary>
    public JsonLocalizationProvider(string localesDirectory, string defaultLanguage, ILogger<JsonLocalizationProvider> logger)
    {
        _defaultLanguage = defaultLanguage;

        if (!Directory.Exists(localesDirectory))
            throw new DirectoryNotFoundException($"Папка локалей не найдена: {localesDirectory}");

        var files = Directory.GetFiles(localesDirectory, "*.json");
        if (files.Length == 0)
            throw new InvalidOperationException($"В {localesDirectory} нет ни одного *.json");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            using var stream = File.OpenRead(file);
            var locale = JsonSerializer.Deserialize<Locale>(stream, options)
                ?? throw new InvalidOperationException($"Не удалось десериализовать {file}");
            _locales[lang] = locale;
            logger.LogInformation("Загружена локаль {Lang} из {File}", lang, file);
        }

        if (!_locales.ContainsKey(_defaultLanguage))
            throw new InvalidOperationException(
                $"Дефолтный язык '{_defaultLanguage}' не найден среди загруженных: {string.Join(", ", _locales.Keys)}");
    }

    public Locale Get(string? language)
    {
        if (!string.IsNullOrEmpty(language) && _locales.TryGetValue(language, out var locale))
            return locale;
        return _locales[_defaultLanguage];
    }
}
