using System.Globalization;
using System.IO;
using System.Text.Json;
using SnowRunnerTuningShop.Core.Localization;

namespace SnowRunnerTuningShop.Localization;

public static class StringResources
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static readonly object Gate = new();
    private static Dictionary<string, string> _base = new(StringComparer.Ordinal);
    private static Dictionary<string, string> _overlay = new(StringComparer.Ordinal);
    private static string _culture = LanguageCatalog.DefaultUiCulture;
    private static bool _initialized;

    public static void Reload()
    {
        lock (Gate)
        {
            LocaleKeyCatalog.Reload();
            _initialized = false;
            EnsureLoaded();
            LoadOverlay(_culture);
        }
    }

    public static void SetCulture(string uiCulture)
    {
        lock (Gate)
        {
            _culture = LanguageCatalog.NormalizeUiCulture(uiCulture);
            EnsureLoaded();
            LoadOverlay(_culture);
        }
    }

    public static string Get(string key, string? fallback = null)
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (IsDebugKeysCulture(_culture))
            {
                return key;
            }

            if (_overlay.TryGetValue(key, out var overlayValue) && !string.IsNullOrEmpty(overlayValue))
            {
                return overlayValue;
            }

            if (_base.TryGetValue(key, out var baseValue) && !string.IsNullOrEmpty(baseValue))
            {
                return baseValue;
            }
        }

        return fallback ?? key;
    }

    /// <summary>Always English, including in debug-keys mode. Used for crash reports.</summary>
    public static string GetEnglish(string key, string? fallback = null)
    {
        lock (Gate)
        {
            EnsureLoaded();
            if (_base.TryGetValue(key, out var baseValue) && !string.IsNullOrEmpty(baseValue))
            {
                return baseValue;
            }
        }

        return fallback ?? key;
    }

    public static string Format(string key, string fallback, params object?[] args)
    {
        if (IsDebugKeysCulture(_culture))
        {
            return key;
        }

        var template = Get(key, fallback);
        try
        {
            return string.Format(CultureInfo.CurrentUICulture, template, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, fallback, args);
        }
    }

    public static string FormatEnglish(string key, string fallback, params object?[] args)
    {
        var template = GetEnglish(key, fallback);
        try
        {
            return string.Format(CultureInfo.InvariantCulture, template, args);
        }
        catch (FormatException)
        {
            return string.Format(CultureInfo.InvariantCulture, fallback, args);
        }
    }

    private static void EnsureLoaded()
    {
        if (_initialized)
        {
            return;
        }

        _base = LoadFile(LanguageCatalog.DefaultUiCulture);
        _initialized = true;
        LocaleKeyCatalog.TraceEnglishGaps(_base);
    }

    private static void LoadOverlay(string uiCulture)
    {
        if (string.Equals(uiCulture, LanguageCatalog.DefaultUiCulture, StringComparison.OrdinalIgnoreCase)
            || IsDebugKeysCulture(uiCulture))
        {
            _overlay = new Dictionary<string, string>(StringComparer.Ordinal);
            return;
        }

        _overlay = LoadFile(uiCulture);
        LocaleKeyCatalog.TraceGaps(uiCulture, _overlay);
    }

    private static bool IsDebugKeysCulture(string uiCulture) =>
        string.Equals(uiCulture, LanguageCatalog.DebugUiCulture, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> LoadFile(string uiCulture)
    {
        var path = FindLocaleFile(uiCulture);
        if (path is null || !File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }

        try
        {
            var json = File.ReadAllText(path);
            var data = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions);
            return data is null
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : new Dictionary<string, string>(data, StringComparer.Ordinal);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.Ordinal);
        }
    }

    private static string? FindLocaleFile(string uiCulture) =>
        LocalePackStore.ResolveLocalePath(LanguageCatalog.NormalizeUiCulture(uiCulture));
}
