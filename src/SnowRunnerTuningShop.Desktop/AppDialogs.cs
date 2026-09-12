using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

internal static class AppDialogs
{
    public static Task ShowInfo(Window owner, string message, string title) =>
        Show(owner, message, title, confirmOnly: true);

    public static Task ShowWarning(Window owner, string message, string title) =>
        Show(owner, message, title, confirmOnly: true);

    public static Task ShowError(Window owner, string message, string title) =>
        Show(owner, message, title, confirmOnly: true);

    public static Task<bool> Confirm(Window owner, string message, string title) =>
        Show(owner, message, title, confirmOnly: false);

    public static async Task OpenUrl(Window owner, string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            await ShowError(owner, ex.Message, UiText.Settings.Title);
        }
    }

    private static async Task<bool> Show(Window owner, string message, string title, bool confirmOnly)
    {
        var dialog = new Window
        {
            Title = title,
            Width = 520,
            SizeToContent = SizeToContent.Height,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            ShowInTaskbar = false,
        };

        var result = false;
        var ok = new Button
        {
            Content = confirmOnly ? "OK" : "Yes",
            MinWidth = 88,
        };
        var cancel = new Button
        {
            Content = UiText.BugReport.Cancel,
            MinWidth = 88,
            IsVisible = !confirmOnly,
        };

        ok.Click += (_, _) =>
        {
            result = true;
            dialog.Close();
        };
        cancel.Click += (_, _) => dialog.Close();

        dialog.Content = new StackPanel
        {
            Margin = new Avalonia.Thickness(20),
            Spacing = 16,
            Children =
            {
                new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
                new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 8,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Children = { cancel, ok },
                },
            },
        };

        await dialog.ShowDialog(owner);
        return result;
    }
}
