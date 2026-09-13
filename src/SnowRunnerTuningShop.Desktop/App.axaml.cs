using SnowRunnerTuningShop;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop.Desktop;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        LanguageService.ApplySavedLanguage();
        ThemeService.ApplySavedTheme();
        GlobalExceptionHandler.Register();
        GlobalExceptionHandler.TryHookUiThread();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow();
        }

        base.OnFrameworkInitializationCompleted();
    }

    public static void ApplyThemeVariant(string themeMode)
    {
        if (Current is null)
        {
            return;
        }

        Current.RequestedThemeVariant = ThemeModes.Normalize(themeMode) switch
        {
            ThemeModes.Dark => Avalonia.Styling.ThemeVariant.Dark,
            ThemeModes.Light => Avalonia.Styling.ThemeVariant.Light,
            _ => Avalonia.Styling.ThemeVariant.Default,
        };
    }
}

internal static class ThemeService
{
    public static void ApplySavedTheme() =>
        App.ApplyThemeVariant(WorkspaceConfigStore.GetThemeMode());

    public static void ApplyAndSave(string themeMode)
    {
        var normalized = ThemeModes.Normalize(themeMode);
        WorkspaceConfigStore.SetThemeMode(normalized);
        App.ApplyThemeVariant(normalized);
    }
}
