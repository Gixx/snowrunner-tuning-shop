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

    private static readonly LanguageOption[] All =
    [
        new("en", "English", "english", "english"),
        new("de", "Deutsch", "german", "german"),
        new("fr", "Français", "french", "french"),
        new("es", "Español", "spanish", "spanish"),
        new("pt", "Português", "portuguese", "portuguese"),
        new("pt-BR", "Português (Brasil)", "brazilian", "brazilianportuguese"),
        new("pl", "Polski", "polish", "polish"),
        new("ru", "Русский", "russian", "russian"),
        new("uk", "Українська", "ukrainian", "ukrainian"),
    ];

    public static IReadOnlyList<LanguageOption> Supported => All;

    public static string NormalizeUiCulture(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultUiCulture;
        }

        var text = value.Trim().Replace('_', '-');
        foreach (var option in All)
        {
            if (string.Equals(option.UiCulture, text, StringComparison.OrdinalIgnoreCase))
            {
                return option.UiCulture;
            }
        }

        if (text.StartsWith("pt-", StringComparison.OrdinalIgnoreCase))
        {
            return "pt-BR";
        }

        var two = text.Length >= 2 ? text[..2].ToLowerInvariant() : text.ToLowerInvariant();
        return All.FirstOrDefault(option =>
                string.Equals(option.UiCulture, two, StringComparison.OrdinalIgnoreCase))
            ?.UiCulture
            ?? DefaultUiCulture;
    }

    public static LanguageOption Get(string? uiCulture) =>
        All.FirstOrDefault(option =>
            string.Equals(option.UiCulture, NormalizeUiCulture(uiCulture), StringComparison.OrdinalIgnoreCase))
        ?? All[0];

    public static string UiCultureFromInnoLanguage(string? innoLanguage)
    {
        if (string.IsNullOrWhiteSpace(innoLanguage))
        {
            return DefaultUiCulture;
        }

        var match = All.FirstOrDefault(option =>
            string.Equals(option.InnoLanguage, innoLanguage, StringComparison.OrdinalIgnoreCase));
        return match?.UiCulture ?? DefaultUiCulture;
    }

    public static string DetectUiCultureFromSystem()
    {
        var ui = CultureInfo.CurrentUICulture;
        if (string.Equals(ui.Name, "pt-BR", StringComparison.OrdinalIgnoreCase))
        {
            return "pt-BR";
        }

        return NormalizeUiCulture(ui.TwoLetterISOLanguageName);
    }

    public static CultureInfo ToCultureInfo(string uiCulture)
    {
        var normalized = NormalizeUiCulture(uiCulture);
        try
        {
            return CultureInfo.GetCultureInfo(normalized);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo(DefaultUiCulture);
        }
    }
}
