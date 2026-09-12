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
            OpenUrl(url);
        }
        catch (Exception ex)
        {
            await ShowError(owner, ex.Message, UiText.Settings.Title);
        }
    }

    public static void OpenUrl(string url)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        if (OperatingSystem.IsWindows())
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            return;
        }

        if (OperatingSystem.IsMacOS())
        {
            Process.Start(new ProcessStartInfo("open", url)
            {
                UseShellExecute = false,
            });
            return;
        }

        // Linux: xdg-open often no-ops when launched from Avalonia/Cursor; Firefox works.
        if (TryStart("firefox", "--new-window", url)
            || TryStart("firefox", url)
            || TryStart("xdg-open", url))
        {
            return;
        }

        throw new InvalidOperationException(
            "Could not open the link. Install Firefox or ensure xdg-open is available.");
    }

    private static bool TryStart(string fileName, params string[] args)
    {
        try
        {
            var start = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            };
            foreach (var arg in args)
            {
                start.ArgumentList.Add(arg);
            }

            using var process = Process.Start(start);
            return process is not null;
        }
        catch (Exception)
        {
            return false;
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
