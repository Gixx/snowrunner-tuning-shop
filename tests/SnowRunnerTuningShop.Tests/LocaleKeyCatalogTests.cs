using System.Text.Json;
using SnowRunnerTuningShop.Core.Localization;

namespace SnowRunnerTuningShop.Tests;

public sealed class LocaleKeyCatalogTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly HashSet<string> ExcludedLocaleFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "keys.json",
        "catalog.json",
    };

    public LocaleKeyCatalogTests()
    {
        LocaleKeyCatalog.Reload();
    }

    [Fact]
    public void Keys_catalog_is_non_empty()
    {
        Assert.NotEmpty(LocaleKeyCatalog.RequiredKeys);
    }

    [Fact]
    public void English_contains_every_catalog_key()
    {
        var english = LoadLocale("en.json");
        var missing = LocaleKeyCatalog.MissingFromEnglish(english);

        Assert.True(
            missing.Count == 0,
            "en.json is missing catalog key(s): " + string.Join(", ", missing.Take(20)));
    }

    public static TheoryData<string> ShippedLocaleFiles()
    {
        var data = new TheoryData<string>();
        foreach (var path in Directory.EnumerateFiles(GetLocalizationDirectory(), "*.json"))
        {
            var name = Path.GetFileName(path);
            if (ExcludedLocaleFiles.Contains(name))
            {
                continue;
            }

            data.Add(name);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(ShippedLocaleFiles))]
    public void Bundled_locale_files_parse_and_are_measured(string fileName)
    {
        var locale = LoadLocale(fileName);
        Assert.NotEmpty(locale);

        if (fileName.Equals("en.json", StringComparison.OrdinalIgnoreCase))
        {
            var missing = LocaleKeyCatalog.MissingFromEnglish(locale);
            Assert.True(
                missing.Count == 0,
                "en.json is missing catalog key(s): " + string.Join(", ", missing.Take(20)));
            return;
        }

        // Other languages may omit keys (English fallback). Ensure the catalog can measure them.
        _ = LocaleKeyCatalog.MissingFrom(locale);
    }

    private static IReadOnlyDictionary<string, string> LoadLocale(string fileName)
    {
        var path = Path.Combine(GetLocalizationDirectory(), fileName);
        Assert.True(File.Exists(path), $"Missing test asset: {path}");

        var json = File.ReadAllText(path);
        var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
        Assert.NotNull(data);
        return data;
    }

    private static string GetLocalizationDirectory()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets", "localization");
        Assert.True(Directory.Exists(path), $"Missing localization directory: {path}");
        return path;
    }
}
