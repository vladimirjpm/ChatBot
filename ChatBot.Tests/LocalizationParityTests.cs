using System.Text.Json;
using Xunit;

namespace ChatBot.Tests;

/// <summary>
/// Verifies that all locales have the same set of keys. Guards against the scenario
/// "added a string to ru.json, forgot en.json — in production the key itself shows as the value".
///
/// .NET: equivalent of tests that verify .resx file parity across cultures.
/// </summary>
public class LocalizationParityTests
{
    [Fact]
    public void All_Locales_Have_Same_Keys_As_Reference()
    {
        // Reference locale is ru.json (the default). All others must mirror it.
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
                $"{lang}.json is missing keys (present in {referenceLang}): {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0,
                $"{lang}.json has extra keys (absent in {referenceLang}): {string.Join(", ", extra)}");
        }
    }

    /// <summary>Walks up from the test binary path to the repo root, looking for the <c>locales</c> folder.</summary>
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
        throw new DirectoryNotFoundException("locales directory not found above the test binary");
    }

    private static JsonDocument LoadKeys(string file)
    {
        using var stream = File.OpenRead(file);
        return JsonDocument.Parse(stream);
    }

    /// <summary>Recursively walks a JSON object and returns all keys in dot notation (e.g. ui.modeLabel).</summary>
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
