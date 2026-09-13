using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class SettingsView : UserControl
{
    private const string WebsiteUrl = "https://gixx.github.io/snowrunner-tuning-shop/";
    private const string PayPalDonateUrl = "https://paypal.me/GaborIvan";

    private AppSession? _session;
    private bool _suppressThemeHandler;
    private bool _suppressLanguageHandler;
    private bool _suppressUpdateChannelHandler;
    private AppUpdateCheckResult? _availableUpdate;
    private bool _initialized;

    public SettingsView()
    {
        InitializeComponent();
        ApplyStaticText();
        Loaded += SettingsView_Loaded;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => RefreshWorkspaceButtons();
        _session.BaselineChanged += (_, _) => RefreshWorkspaceButtons();
        _session.GameRunningChanged += (_, _) => RefreshWorkspaceButtons();
        RefreshWorkspaceButtons();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    private void ApplyStaticText()
    {
        TitleText.Text = UiText.Settings.Title;
        AppearanceTitleText.Text = UiText.Settings.AppearanceTitle;
        AppearanceHintText.Text = UiText.Settings.AppearanceHint;
        ThemeLabelText.Text = UiText.Settings.ThemeLabel;
        LanguageTitleText.Text = UiText.Settings.LanguageTitle;
        LanguageHintText.Text = UiText.Settings.LanguageHint;
        LanguageLabelText.Text = UiText.Settings.LanguageLabel;
        ManageLanguagesButton.Content = UiText.Settings.ManageLanguages;
        WorkspaceTitleText.Text = UiText.Settings.WorkspaceTitle;
        WorkspaceHintText.Text = UiText.Settings.WorkspaceHint;
        RefreshBaselineButton.Content = UiText.Workspace.RefreshBaseline;
        ReapplyButton.Content = UiText.Workspace.ReapplySavedChanges;
        RestoreFullBaselineButton.Content = UiText.Main.RestoreFullBaseline;
        AboutTitleText.Text = UiText.Settings.AboutTitle;
        AboutHintText.Text = UiText.Settings.AboutHint;
        UpdatesTitleText.Text = UiText.Settings.UpdatesTitle;
        UpdatesHintText.Text = UiText.Settings.UpdatesHint;
        UpdateChannelLabelText.Text = UiText.Settings.UpdateChannelLabel;
        InstalledVersionText.Text = UiText.Settings.InstalledVersion;
        CheckForUpdatesButton.Content = UiText.Settings.CheckForUpdates;
        DownloadUpdateButton.Content = UiText.Settings.DownloadUpdate;
        OpenWebsiteButton.Content = UiText.Settings.OpenWebsite;
        DonatePayPalButton.Content = UiText.Settings.DonatePayPal;
        ToolTip.SetTip(DonatePayPalButton, UiText.Settings.DonatePayPal);
        FeedbackTitleText.Text = UiText.Settings.FeedbackTitle;
        FeedbackHintText.Text = UiText.Settings.FeedbackHint;
        OpenIssueTrackerButton.Content = UiText.Settings.OpenIssueTracker;
        DebugCrashTitleText.Text = UiText.Settings.DebugCrashTitle;
        DebugCrashHintText.Text = UiText.Settings.DebugCrashHint;
        DebugCrashUiButton.Content = UiText.Settings.DebugCrashUiButton;
        DebugCrashVehicleButton.Content = UiText.Settings.DebugCrashVehicleButton;
    }

    private async void SettingsView_Loaded(object? sender, RoutedEventArgs e)
    {
#if DEBUG
        DebugCrashPanel.IsVisible = true;
#endif
        if (!_initialized)
        {
            _initialized = true;
            BindThemeCombo();
            BindLanguageCombo();
            BindUpdateChannelCombo();
        }

        RefreshWorkspaceButtons();
        await RefreshUpdateStatusAsync();
    }

    private void BindUpdateChannelCombo()
    {
        UpdateChannelCombo.ItemsSource = new LabeledOption[]
        {
            new(UiText.Settings.UpdateChannelStable, AppUpdateChannels.Stable),
            new(UiText.Settings.UpdateChannelBeta, AppUpdateChannels.Beta),
        };
        _suppressUpdateChannelHandler = true;
        UpdateChannelCombo.SelectedItem = FindOption(UpdateChannelCombo, WorkspaceConfigStore.GetUpdateChannel());
        _suppressUpdateChannelHandler = false;
    }

    private async void UpdateChannelCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressUpdateChannelHandler || UpdateChannelCombo.SelectedItem is not LabeledOption option)
        {
            return;
        }

        if (string.Equals(option.Value, WorkspaceConfigStore.GetUpdateChannel(), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        WorkspaceConfigStore.SetUpdateChannel(option.Value);
        await RefreshUpdateStatusAsync(forceRefresh: true);
    }

    private void BindThemeCombo()
    {
        ThemeCombo.ItemsSource = new LabeledOption[]
        {
            new(UiText.Settings.ThemeSystem, ThemeModes.System),
            new(UiText.Settings.ThemeDark, ThemeModes.Dark),
            new(UiText.Settings.ThemeLight, ThemeModes.Light),
        };
        _suppressThemeHandler = true;
        ThemeCombo.SelectedItem = FindOption(ThemeCombo, WorkspaceConfigStore.GetThemeMode());
        _suppressThemeHandler = false;
    }

    private void BindLanguageCombo()
    {
        LanguageCombo.ItemsSource = LanguageCatalog.Supported
            .Select(option => new LabeledOption(option.DisplayName, option.UiCulture))
            .ToArray();
        _suppressLanguageHandler = true;
        LanguageCombo.SelectedItem = FindOption(LanguageCombo, LanguageService.CurrentUiCulture);
        _suppressLanguageHandler = false;
    }

    private static LabeledOption? FindOption(ComboBox combo, string value) =>
        combo.Items.OfType<LabeledOption>()
            .FirstOrDefault(item => string.Equals(item.Value, value, StringComparison.OrdinalIgnoreCase));

    private void ThemeCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeHandler || ThemeCombo.SelectedItem is not LabeledOption option)
        {
            return;
        }

        ThemeService.ApplyAndSave(option.Value);
    }

    private async void LanguageCombo_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_suppressLanguageHandler || LanguageCombo.SelectedItem is not LabeledOption option)
        {
            return;
        }

        var previous = LanguageService.CurrentUiCulture;
        if (string.Equals(previous, option.Value, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        LanguageService.ApplyAndSave(option.Value);
        if (OwnerWindow is { } owner)
        {
            await AppDialogs.ShowInfo(
                owner,
                UiText.Settings.LanguageRestartMessage,
                UiText.Settings.LanguageRestartTitle);
        }
    }

    private async void ManageLanguagesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        var window = new LocaleManagerWindow();
        await window.ShowDialog(owner);
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

        RestoreFullBaselineButton.IsEnabled = hasBaseline && PakWriteUi.CanWrite(_session);
        RefreshBaselineButton.IsEnabled = health.CanRefreshBaseline;
        ToolTip.SetTip(RefreshBaselineButton, UiText.Workspace.RefreshBaselineTooltip(health));
        ReapplyButton.IsEnabled = health.CanReapply && PakWriteUi.CanWrite(_session);
        WorkspaceStatusTextBlock.Text = UiText.Workspace.StatusLine(health.Kind, health.ProfileEntryCount);
    }

    private async void RestoreFullBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryRestoreFullBaseline(owner, _session);
        RefreshWorkspaceButtons();
    }

    private async void RefreshBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryRefreshBaselineFromGame(owner, _session);
        RefreshWorkspaceButtons();
    }

    private async void ReapplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryReapplySavedChanges(owner, _session);
        RefreshWorkspaceButtons();
    }

    private async void OpenWebsiteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await AppDialogs.OpenUrl(owner, WebsiteUrl);
        }
    }

    private async void CheckForUpdatesButton_Click(object? sender, RoutedEventArgs e) =>
        await RefreshUpdateStatusAsync(forceRefresh: true);

    private async void DownloadUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        var url = _availableUpdate?.ReleasePageUrl ?? AppInfo.LatestReleasePageUrl;
        await AppDialogs.OpenUrl(owner, url);
    }

    private async Task RefreshUpdateStatusAsync(bool forceRefresh = false)
    {
        UpdateStatusTextBlock.Text = UiText.Settings.CheckingForUpdates;
        DownloadUpdateButton.IsVisible = false;
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
            DownloadUpdateButton.IsVisible = true;
            return;
        }

        _availableUpdate = null;
        DownloadUpdateButton.IsVisible = false;
        UpdateStatusTextBlock.Text = result.Status == AppUpdateStatus.Failed
            ? UiText.Settings.UpdateCheckFailed
            : UiText.Settings.UpToDate;
    }

    private async void DonatePayPalButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await AppDialogs.OpenUrl(owner, PayPalDonateUrl);
        }
    }

    private async void OpenIssueTrackerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is { } owner)
        {
            await AppDialogs.OpenUrl(owner, AppInfo.IssueTrackerUrl);
        }
    }

    private void DebugCrashUiButton_Click(object? sender, RoutedEventArgs e) =>
        DebugCrashTools.ThrowUiTestCrash();

    private void DebugCrashVehicleButton_Click(object? sender, RoutedEventArgs e) =>
        DebugCrashTools.ThrowVehiclePageTestCrash();

    public sealed record LabeledOption(string Label, string Value)
    {
        public override string ToString() => Label;
    }
}
