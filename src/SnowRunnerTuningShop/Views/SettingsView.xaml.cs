using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class SettingsView : UserControl
{
    private const string WebsiteUrl = "https://gixx.github.io/snowrunner-tuning-shop/";
    private const string PayPalDonateUrl = "https://paypal.me/GaborIvan";
    private static string IssueTrackerUrl => AppInfo.IssueTrackerUrl;

    private AppSession? _session;
    private bool _suppressThemeHandler;
    private bool _suppressLanguageHandler;
    private AppUpdateCheckResult? _availableUpdate;

    public SettingsView()
    {
        InitializeComponent();
        Loaded += SettingsView_Loaded;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => RefreshWorkspaceButtons();
        _session.BaselineChanged += (_, _) => RefreshWorkspaceButtons();
        RefreshWorkspaceButtons();
    }

    private async void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
#if DEBUG
        DebugCrashPanel.Visibility = Visibility.Visible;
#endif
        if (ThemeCombo.Items.Count == 0)
        {
            ThemeCombo.DisplayMemberPath = nameof(LabeledTheme.Label);
            ThemeCombo.SelectedValuePath = nameof(LabeledTheme.Value);
            ThemeCombo.ItemsSource = new LabeledTheme[]
            {
                new(UiText.Settings.ThemeSystem, ThemeModes.System),
                new(UiText.Settings.ThemeDark, ThemeModes.Dark),
                new(UiText.Settings.ThemeLight, ThemeModes.Light),
            };

            _suppressThemeHandler = true;
            ThemeCombo.SelectedValue = WorkspaceConfigStore.GetThemeMode();
            _suppressThemeHandler = false;
        }

        if (LanguageCombo.Items.Count == 0)
        {
            BindLanguageCombo();
        }

        RefreshWorkspaceButtons();
        await RefreshUpdateStatusAsync();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeHandler || ThemeCombo.SelectedValue is not string themeMode)
        {
            return;
        }

        ThemeService.ApplyAndSave(themeMode);
    }

    private void LanguageCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageHandler || LanguageCombo.SelectedValue is not string uiCulture)
        {
            return;
        }

        var previous = LanguageService.CurrentUiCulture;
        if (string.Equals(previous, uiCulture, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LanguageService.ApplyAndSave(uiCulture);
        MessageBox.Show(
            UiText.Settings.LanguageRestartMessage,
            UiText.Settings.LanguageRestartTitle,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    private void BindLanguageCombo()
    {
        LanguageCombo.DisplayMemberPath = nameof(LabeledLanguage.Label);
        LanguageCombo.SelectedValuePath = nameof(LabeledLanguage.Value);
        _suppressLanguageHandler = true;
        LanguageCombo.ItemsSource = LanguageCatalog.Supported
            .Select(option => new LabeledLanguage(option.DisplayName, option.UiCulture))
            .ToArray();
        LanguageCombo.SelectedValue = LanguageService.CurrentUiCulture;
        _suppressLanguageHandler = false;
    }

    private void ManageLanguagesButton_Click(object sender, RoutedEventArgs e)
    {
        var window = new LocaleManagerWindow
        {
            Owner = Window.GetWindow(this),
        };
        window.ShowDialog();
        LanguageCatalog.Reload();
        StringResources.Reload();
        LanguageService.RefreshRuntimeStrings();
        BindLanguageCombo();
    }

    private void RefreshWorkspaceButtons()
    {
        var health = WorkspaceHealthService.Evaluate(_session?.PakPath);
        var hasBaseline = _session?.HasPak == true
            && !string.IsNullOrWhiteSpace(_session.PakPath)
            && health.Kind != WorkspaceHealthKind.NotReady;

        RestoreFullBaselineButton.IsEnabled = hasBaseline;
        RefreshBaselineButton.IsEnabled = health.CanRefreshBaseline;
        ReapplyButton.IsEnabled = health.CanReapply;
        WorkspaceStatusTextBlock.Text = UiText.Workspace.StatusLine(health.Kind, health.ProfileEntryCount);
    }

    private void RestoreFullBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        WorkspaceCommands.TryRestoreFullBaseline(_session);
    }

    private void RefreshBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        WorkspaceCommands.TryRefreshBaselineFromGame(_session);
    }

    private void ReapplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        WorkspaceCommands.TryReapplySavedChanges(_session);
    }

    private void OpenWebsiteButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(WebsiteUrl);

    private async void CheckForUpdatesButton_Click(object sender, RoutedEventArgs e) =>
        await RefreshUpdateStatusAsync(forceRefresh: true);

    private void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        AppUpdateUi.StartDownload(Window.GetWindow(this), _availableUpdate);
    }

    private async Task RefreshUpdateStatusAsync(bool forceRefresh = false)
    {
        UpdateStatusTextBlock.Text = UiText.Settings.CheckingForUpdates;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        CheckForUpdatesButton.IsEnabled = false;

        try
        {
            var result = await AppUpdateService.CheckAsync(forceRefresh);
            ApplyUpdateResult(result);
        }
        catch
        {
            _availableUpdate = null;
            UpdateStatusTextBlock.Text = UiText.Settings.UpdateCheckFailed;
        }
        finally
        {
            CheckForUpdatesButton.IsEnabled = true;
        }
    }

    private void ApplyUpdateResult(AppUpdateCheckResult result)
    {
        if (result.Status == AppUpdateStatus.UpdateAvailable
            && !string.IsNullOrWhiteSpace(result.LatestVersion))
        {
            _availableUpdate = result;
            UpdateStatusTextBlock.Text = UiText.Settings.UpdateAvailableStatus(result.LatestVersion);
            DownloadUpdateButton.Visibility = Visibility.Visible;
            return;
        }

        _availableUpdate = null;
        DownloadUpdateButton.Visibility = Visibility.Collapsed;
        UpdateStatusTextBlock.Text = result.Status == AppUpdateStatus.Failed
            ? UiText.Settings.UpdateCheckFailed
            : UiText.Settings.UpToDate;
    }

    private void DonatePayPalButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(PayPalDonateUrl);

    private void OpenIssueTrackerButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(IssueTrackerUrl);

    private void DebugCrashUiButton_Click(object sender, RoutedEventArgs e) =>
        Diagnostics.DebugCrashTools.ThrowUiTestCrash();

    private void DebugCrashVehicleButton_Click(object sender, RoutedEventArgs e) =>
        Diagnostics.DebugCrashTools.ThrowVehiclePageTestCrash();

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

    private sealed record LabeledTheme(string Label, string Value);

    private sealed record LabeledLanguage(string Label, string Value);
}
