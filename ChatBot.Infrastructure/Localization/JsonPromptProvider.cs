using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ChatBot.Infrastructure.Localization;

/// <summary>
/// Загружает system prompts из <c>prompts/{lang}.json</c> при старте приложения.
/// Singleton — файлы читаются один раз и хранятся в памяти.
///
/// .NET: аналог JsonConfigurationProvider, но для промптов LLM.
/// </summary>
public sealed class JsonPromptProvider : IPromptProvider
{
    private readonly Dictionary<string, Prompts> _prompts = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _defaultLanguage;

    public JsonPromptProvider(string promptsDirectory, string defaultLanguage, ILogger<JsonPromptProvider> logger)
    {
        _defaultLanguage = defaultLanguage;

        if (!Directory.Exists(promptsDirectory))
            throw new DirectoryNotFoundException($"Prompts directory not found: {promptsDirectory}");

        var files = Directory.GetFiles(promptsDirectory, "*.json");
        if (files.Length == 0)
            throw new InvalidOperationException($"No *.json files found in {promptsDirectory}");

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        foreach (var file in files)
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            using var stream = File.OpenRead(file);
            var prompts = JsonSerializer.Deserialize<Prompts>(stream, options)
                ?? throw new InvalidOperationException($"Failed to deserialize {file}");
            _prompts[lang] = prompts;
            logger.LogInformation("Loaded prompts {Lang} from {File}", lang, file);
        }

        if (!_prompts.ContainsKey(_defaultLanguage))
            throw new InvalidOperationException(
                $"Default language '{_defaultLanguage}' not found among loaded prompts: {string.Join(", ", _prompts.Keys)}");
    }

    public Prompts Get(string? language)
    {
        if (!string.IsNullOrEmpty(language) && _prompts.TryGetValue(language, out var prompts))
            return prompts;
        return _prompts[_defaultLanguage];
    }
}
