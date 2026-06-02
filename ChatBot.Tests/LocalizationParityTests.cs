using System.Text.Json;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Проверяет что все локали имеют одинаковый набор ключей. Защита от ситуации
/// «добавили строку в ru.json, забыли en.json — в проде вылезет ключ-как-плейсхолдер».
///
/// .NET: эквивалент тестов на соответствие .resx файлов между культурами.
/// </summary>
public class LocalizationParityTests
{
    [Fact]
    public void All_Locales_Have_Same_Keys_As_Reference()
    {
        // Эталон — ru.json (как дефолтный). Остальные должны его повторять.
        var referenceLang = "ru";
        var localesDir = FindLocalesDirectory();

        var refDoc = LoadKeys(Path.Combine(localesDir, $"{referenceLang}.json"));
        var refKeys = FlattenKeys(refDoc.RootElement).ToHashSet();

        foreach (var file in Directory.GetFiles(localesDir, "*.json"))
        {
            var lang = Path.GetFileNameWithoutExtension(file);
            if (lang == referenceLang) continue;

            using var doc = LoadKeys(file);
            var keys = FlattenKeys(doc.RootElement).ToHashSet();

            var missing = refKeys.Except(keys).ToList();
            var extra = keys.Except(refKeys).ToList();

            Assert.True(missing.Count == 0,
                $"В {lang}.json не хватает ключей (есть в {referenceLang}): {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0,
                $"В {lang}.json есть лишние ключи (нет в {referenceLang}): {string.Join(", ", extra)}");
        }
    }

    /// <summary>Идём от текущего пути теста к корню репо, ищем папку <c>locales</c>.</summary>
    private static string FindLocalesDirectory()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            var candidate = Path.Combine(dir, "locales");
            if (Directory.Exists(candidate) && Directory.GetFiles(candidate, "*.json").Length > 0)
                return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new DirectoryNotFoundException("Папка locales не найдена выше тестового бинарника");
    }

    private static JsonDocument LoadKeys(string file)
    {
        using var stream = File.OpenRead(file);
        return JsonDocument.Parse(stream);
    }

    /// <summary>
    /// Рекурсивно обходит JSON-объект, возвращает все ключи в дотовой нотации (ui.modeLabel).
    /// </summary>
    private static IEnumerable<string> FlattenKeys(JsonElement element, string prefix = "")
    {
        if (element.ValueKind != JsonValueKind.Object) yield break;
        foreach (var prop in element.EnumerateObject())
        {
            var path = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
            if (prop.Value.ValueKind == JsonValueKind.Object)
            {
                foreach (var nested in FlattenKeys(prop.Value, path))
                    yield return nested;
            }
            else
            {
                yield return path;
            }
        }
    }
}
