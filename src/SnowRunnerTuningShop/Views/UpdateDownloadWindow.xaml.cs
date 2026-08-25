using System.Diagnostics;
using System.Globalization;
using System.Windows;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class UpdateDownloadWindow : Window
{
    private readonly string _installerUrl;
    private readonly string _destinationPath;
    private readonly string? _latestVersion;
    private readonly CancellationTokenSource _cts = new();
    private bool _downloadFinished;
    private bool _closeConfirmed;

    public UpdateDownloadWindow(AppUpdateCheckResult update)
    {
        ArgumentNullException.ThrowIfNull(update);
        if (string.IsNullOrWhiteSpace(update.InstallerUrl))
        {
            throw new ArgumentException("Installer URL is required.", nameof(update));
        }

        _installerUrl = update.InstallerUrl;
        _latestVersion = update.LatestVersion;
        _destinationPath = AppUpdateService.BuildInstallerTempPath(update.InstallerUrl, update.LatestVersion);

        InitializeComponent();
        StatusDetailText.Text = string.IsNullOrWhiteSpace(_latestVersion)
            ? UiText.UpdateDownload.DownloadingDetail
            : UiText.UpdateDownload.DownloadingDetailVersion(_latestVersion);
        Loaded += UpdateDownloadWindow_Loaded;
        Closing += UpdateDownloadWindow_Closing;
    }

    private async void UpdateDownloadWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= UpdateDownloadWindow_Loaded;
        await StartDownloadAsync();
    }

    private async Task StartDownloadAsync()
    {
        var progress = new Progress<AppUpdateDownloadProgress>(OnProgress);
        try
        {
            await AppUpdateService.DownloadInstallerAsync(
                _installerUrl,
                _destinationPath,
                progress,
                _cts.Token);

            ShowCompleted();
        }
        catch (OperationCanceledException)
        {
            _closeConfirmed = true;
            DialogResult = false;
            Close();
        }
        catch (Exception ex)
        {
            ShowFailed(ex.Message);
        }
    }

    private void OnProgress(AppUpdateDownloadProgress progress)
    {
        if (progress.Percent is { } percent)
        {
            DownloadProgressBar.IsIndeterminate = false;
            DownloadProgressBar.Value = percent;
            ProgressPercentText.Text = UiText.UpdateDownload.ProgressLabel(
                percent,
                FormatBytes(progress.BytesReceived),
                FormatBytes(progress.TotalBytes ?? 0));
        }
        else
        {
            DownloadProgressBar.IsIndeterminate = true;
            ProgressPercentText.Text = UiText.UpdateDownload.ProgressIndeterminate(
                FormatBytes(progress.BytesReceived));
        }
    }

    private void ShowCompleted()
    {
        _downloadFinished = true;
        StatusTitleText.Text = UiText.UpdateDownload.CompleteTitle;
        StatusDetailText.Text = UiText.UpdateDownload.CompleteDetail;
        DownloadProgressBar.IsIndeterminate = false;
        DownloadProgressBar.Value = 100;
        ProgressPercentText.Text = "100%";
        PrimaryButton.Content = UiText.UpdateDownload.UpdateAndRestart;
        PrimaryButton.Visibility = Visibility.Visible;
        SecondaryButton.Content = UiText.UpdateDownload.Close;
        SecondaryButton.IsCancel = true;
    }

    private void ShowFailed(string message)
    {
        _downloadFinished = true;
        StatusTitleText.Text = UiText.UpdateDownload.FailedTitle;
        StatusDetailText.Text = message;
        DownloadProgressBar.IsIndeterminate = false;
        PrimaryButton.Visibility = Visibility.Collapsed;
        SecondaryButton.Content = UiText.UpdateDownload.Close;
    }

    private void PrimaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_downloadFinished)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(_destinationPath)
            {
                UseShellExecute = true,
            });
            _closeConfirmed = true;
            DialogResult = true;
            Close();
            Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                UiText.UpdateDownload.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SecondaryButton_Click(object sender, RoutedEventArgs e)
    {
        if (_downloadFinished)
        {
            _closeConfirmed = true;
            DialogResult = false;
            Close();
            return;
        }

        _cts.Cancel();
        SecondaryButton.IsEnabled = false;
        StatusDetailText.Text = UiText.UpdateDownload.Cancelling;
    }

    private void UpdateDownloadWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closeConfirmed || _downloadFinished)
        {
            return;
        }

        e.Cancel = true;
        if (!_cts.IsCancellationRequested)
        {
            _cts.Cancel();
            SecondaryButton.IsEnabled = false;
            StatusDetailText.Text = UiText.UpdateDownload.Cancelling;
        }
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts.Dispose();
        base.OnClosed(e);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unit = 0;
        while (size >= 1024 && unit < units.Length - 1)
        {
            size /= 1024;
            unit++;
        }

        return string.Create(CultureInfo.InvariantCulture, $"{size:0.##} {units[unit]}");
    }
}
