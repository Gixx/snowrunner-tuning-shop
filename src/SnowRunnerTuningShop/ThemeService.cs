using System.Windows;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop;

internal static class ThemeService
{
    public static void ApplySavedTheme() =>
        Apply(WorkspaceConfigStore.GetThemeMode());

    public static void ApplyAndSave(string themeMode)
    {
        var normalized = ThemeModes.Normalize(themeMode);
        WorkspaceConfigStore.SetThemeMode(normalized);
        Apply(normalized);
    }

    public static void Apply(string themeMode)
    {
        if (Application.Current is null)
        {
            return;
        }

        Application.Current.ThemeMode = ThemeModes.Normalize(themeMode) switch
        {
            ThemeModes.Dark => ThemeMode.Dark,
            ThemeModes.Light => ThemeMode.Light,
            _ => ThemeMode.System,
        };
    }
}
