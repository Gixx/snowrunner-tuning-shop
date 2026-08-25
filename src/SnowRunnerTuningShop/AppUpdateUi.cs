using System.Diagnostics;
using System.Windows;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Localization;
using SnowRunnerTuningShop.Views;

namespace SnowRunnerTuningShop;

public static class AppUpdateUi
{
    public static void StartDownload(Window? owner, AppUpdateCheckResult? update)
    {
        if (update is null
            || update.Status != AppUpdateStatus.UpdateAvailable
            || string.IsNullOrWhiteSpace(update.InstallerUrl))
        {
            OpenUrl(update?.ReleasePageUrl ?? AppInfo.LatestReleasePageUrl);
            return;
        }

        var dialog = new UpdateDownloadWindow(update)
        {
            Owner = owner ?? Application.Current.MainWindow,
        };
        dialog.ShowDialog();
    }

    private static void OpenUrl(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Settings.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
