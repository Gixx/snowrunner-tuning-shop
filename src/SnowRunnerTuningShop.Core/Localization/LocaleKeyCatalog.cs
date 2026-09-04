using System.Diagnostics;
using System.Text.Json;

namespace SnowRunnerTuningShop.Core.Localization;

/// <summary>
/// Canonical UI string key list. English (<c>en.json</c>) must contain every key.
/// Other locale files are measured against this list; missing keys fall back to English.
/// </summary>
public static class LocaleKeyCatalog
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly object Gate = new();
    private static IReadOnlyList<string>? _keys;
    private static HashSet<string>? _set;

    public static IReadOnlyList<string> RequiredKeys
    {
        get
        {
            EnsureLoaded();
            return _keys!;
        }
    }

    public static IReadOnlySet<string> RequiredKeySet
    {
        get
        {
            EnsureLoaded();
            return _set!;
        }
    }

    public static void Reload()
    {
        lock (Gate)
        {
            _keys = null;
            _set = null;
        }
    }

    public static IReadOnlyList<string> MissingFrom(IReadOnlyDictionary<string, string> locale)
    {
        if (locale.Count == 0)
        {
            return RequiredKeys;
        }

        return RequiredKeys
            .Where(key => !locale.ContainsKey(key) || string.IsNullOrEmpty(locale[key]))
            .ToArray();
    }

    public static IReadOnlyList<string> MissingFromEnglish(IReadOnlyDictionary<string, string> english)
    {
        return RequiredKeys
            .Where(key => !english.ContainsKey(key) || string.IsNullOrEmpty(english[key]))
            .ToArray();
    }

    [Conditional("DEBUG")]
    public static void TraceGaps(string uiCulture, IReadOnlyDictionary<string, string> locale)
    {
        var missing = MissingFrom(locale);
        if (missing.Count == 0)
        {
            return;
        }

        Debug.WriteLine(
            $"[LocaleKeyCatalog] {uiCulture}: {missing.Count} key(s) missing; UI will fall back to English. First: {string.Join(", ", missing.Take(12))}");
    }

    [Conditional("DEBUG")]
    public static void TraceEnglishGaps(IReadOnlyDictionary<string, string> english)
    {
        var missing = MissingFromEnglish(english);
        if (missing.Count == 0)
        {
            return;
        }

        Debug.WriteLine(
            $"[LocaleKeyCatalog] en.json is missing {missing.Count} catalog key(s): {string.Join(", ", missing.Take(20))}");
    }

    private static void EnsureLoaded()
    {
        lock (Gate)
        {
            if (_keys is not null)
            {
                return;
            }

            _keys = LoadKeys();
            _set = new HashSet<string>(_keys, StringComparer.Ordinal);
        }
    }

    private static IReadOnlyList<string> LoadKeys()
    {
        var fromCatalog = ReadKeysFile();
        if (fromCatalog.Count > 0)
        {
            return fromCatalog;
        }

        return ReadEnglishKeys();
    }

    private static IReadOnlyList<string> ReadKeysFile()
    {
        var path = FindBundledFile(LocalePackNames.KeysFileName);
        if (path is null)
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (!document.RootElement.TryGetProperty("keys", out var keysElement)
                || keysElement.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            var list = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in keysElement.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                var key = item.GetString();
                if (!string.IsNullOrWhiteSpace(key) && seen.Add(key))
                {
                    list.Add(key);
                }
            }

            list.Sort(StringComparer.Ordinal);
            return list;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> ReadEnglishKeys()
    {
        var path = FindBundledFile("en.json");
        if (path is null)
        {
            return [];
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            if (data is null || data.Count == 0)
            {
                return [];
            }

            return data.Keys.OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }
        catch
        {
            return [];
        }
    }

    private static string? FindBundledFile(string fileName)
    {
        var baseDir = AppContext.BaseDirectory;
        string[] roots =
        [
            Path.Combine(baseDir, "assets", "localization"),
            Path.Combine(baseDir, "localization"),
            baseDir,
        ];

        foreach (var root in roots)
        {
            var path = Path.Combine(root, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }
}
