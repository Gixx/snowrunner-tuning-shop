using System.Globalization;

namespace SnowRunnerTuningShop.Core.Localization;

public sealed record LanguageOption(
    string UiCulture,
    string DisplayName,
    string GameLanguage,
    string InnoLanguage);

/// <summary>Supported app UI cultures and matching SnowRunner pak string file ids.</summary>
public static class LanguageCatalog
{
    public const string DefaultUiCulture = "en";
    public const string DebugUiCulture = "debug";
    public const string DebugDisplayName = "DEBUG (keys)";

    public static bool IncludeDebugLanguage
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public static IReadOnlyList<LanguageOption> Supported
    {
        get
        {
            var installed = LocalePackStore.GetInstalledOptions();
            if (!IncludeDebugLanguage || Has(installed, DebugUiCulture))
            {
                return installed;
            }

            return installed
                .Append(new LanguageOption(DebugUiCulture, DebugDisplayName, "english", ""))
                .ToArray();
        }
    }

    public static void Reload() => LocalePackStore.Reload();

    public static string NormalizeUiCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultUiCulture;
        }

        var text = value.Trim().Replace('_', '-');
        if (string.Equals(text, DebugUiCulture, StringComparison.OrdinalIgnoreCase))
        {
            return IncludeDebugLanguage ? DebugUiCulture : DefaultUiCulture;
        }

        var installed = Supported;
        foreach (var option in installed)
        {
            if (string.Equals(option.UiCulture, text, StringComparison.OrdinalIgnoreCase))
            {
                return option.UiCulture;
            }
        }

        if (IsTraditionalChinese(text) && Has(installed, "zh-TW"))
        {
            return "zh-TW";
        }

        if (IsChinese(text) && Has(installed, "zh-CN"))
        {
            return "zh-CN";
        }

        if (text.StartsWith("pt-", StringComparison.OrdinalIgnoreCase) && Has(installed, "pt-BR"))
        {
            return "pt-BR";
        }

        var two = text.Length >= 2 ? text[..2].ToLowerInvariant() : text.ToLowerInvariant();
        var twoMatch = installed.FirstOrDefault(option =>
            string.Equals(option.UiCulture, two, StringComparison.OrdinalIgnoreCase));
        return twoMatch?.UiCulture ?? DefaultUiCulture;
    }

    public static LanguageOption Get(string? uiCulture)
    {
        var installed = Supported;
        var normalized = NormalizeUiCulture(uiCulture);
        return installed.FirstOrDefault(option =>
                   string.Equals(option.UiCulture, normalized, StringComparison.OrdinalIgnoreCase))
               ?? installed.FirstOrDefault(option =>
                   string.Equals(option.UiCulture, DefaultUiCulture, StringComparison.OrdinalIgnoreCase))
               ?? new LanguageOption(DefaultUiCulture, "English", "english", "english");
    }

    public static string UiCultureFromInnoLanguage(string? innoLanguage)
    {
        if (string.IsNullOrWhiteSpace(innoLanguage))
        {
            return DefaultUiCulture;
        }

        var match = Supported.FirstOrDefault(option =>
            string.Equals(option.InnoLanguage, innoLanguage, StringComparison.OrdinalIgnoreCase));
        return match?.UiCulture ?? DefaultUiCulture;
    }

    public static string DetectUiCultureFromSystem()
    {
        var ui = CultureInfo.CurrentUICulture;
        var name = string.IsNullOrWhiteSpace(ui.Name) ? ui.TwoLetterISOLanguageName : ui.Name;
        return NormalizeUiCulture(name);
    }

    public static CultureInfo ToCultureInfo(string uiCulture)
    {
        var normalized = NormalizeUiCulture(uiCulture);
        if (string.Equals(normalized, DebugUiCulture, StringComparison.OrdinalIgnoreCase))
        {
            return CultureInfo.InvariantCulture;
        }

        try
        {
            return CultureInfo.GetCultureInfo(normalized);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(DefaultUiCulture);
        }
    }

    private static bool Has(IReadOnlyList<LanguageOption> installed, string uiCulture) =>
        installed.Any(option =>
            string.Equals(option.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));

    private static bool IsTraditionalChinese(string text)
    {
        var n = text.ToLowerInvariant();
        return n.StartsWith("zh-hant", StringComparison.Ordinal)
            || n.StartsWith("zh-tw", StringComparison.Ordinal)
            || n.StartsWith("zh-hk", StringComparison.Ordinal)
            || n.StartsWith("zh-mo", StringComparison.Ordinal);
    }

    private static bool IsChinese(string text)
    {
        var n = text.ToLowerInvariant();
        return n == "zh" || n.StartsWith("zh-", StringComparison.Ordinal);
    }
}
