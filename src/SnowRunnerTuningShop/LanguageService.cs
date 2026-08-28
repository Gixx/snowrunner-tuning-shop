using System.Globalization;
using System.IO;
using System.Text.Json;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop;

internal static class LanguageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static void ApplySavedLanguage()
    {
        TryConsumeInstallLanguage();
        var uiCulture = WorkspaceConfigStore.GetUiCulture();
        Apply(uiCulture, persist: false);
    }

    public static void ApplyAndSave(string uiCulture)
    {
        var normalized = LanguageCatalog.NormalizeUiCulture(uiCulture);
        WorkspaceConfigStore.SetUiCulture(normalized);
        Apply(normalized, persist: false);
    }

    public static string CurrentUiCulture => LanguageCatalog.NormalizeUiCulture(WorkspaceConfigStore.GetUiCulture());

    public static bool Apply(string uiCulture, bool persist)
    {
        var normalized = LanguageCatalog.NormalizeUiCulture(uiCulture);
        if (persist)
        {
            WorkspaceConfigStore.SetUiCulture(normalized);
        }

        var culture = LanguageCatalog.ToCultureInfo(normalized);
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        Thread.CurrentThread.CurrentCulture = culture;
        Thread.CurrentThread.CurrentUICulture = culture;

        var option = LanguageCatalog.Get(normalized);
        AppLanguage.Current = option.GameLanguage;
        StringResources.SetCulture(normalized);
        return true;
    }

    private static void TryConsumeInstallLanguage()
    {
        var path = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "install-language.json");
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(path);
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("uiCulture", out var cultureProp))
            {
                var culture = cultureProp.GetString();
                if (!string.IsNullOrWhiteSpace(culture))
                {
                    WorkspaceConfigStore.SetUiCulture(culture);
                }
            }
        }
        catch
        {
            // Ignore malformed installer seed; keep existing config.
        }
        finally
        {
            try
            {
                File.Delete(path);
            }
            catch
            {
                // Best effort.
            }
        }
    }
}
