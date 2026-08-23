using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class SettingsView : UserControl
{
    private const string WebsiteUrl = "https://gixx.github.io/snowrunner-tuning-shop/";
    private const string PayPalDonateUrl = "https://paypal.me/GaborIvan";
    private const string IssueTrackerUrl = "https://github.com/Gixx/snowrunner-tuning-shop/issues";

    private AppSession? _session;
    private bool _suppressThemeHandler;

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

    private void SettingsView_Loaded(object sender, RoutedEventArgs e)
    {
        if (ThemeCombo.Items.Count > 0)
        {
            return;
        }

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

        RefreshWorkspaceButtons();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeHandler || ThemeCombo.SelectedValue is not string themeMode)
        {
            return;
        }

        ThemeService.ApplyAndSave(themeMode);
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

    private void DonatePayPalButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(PayPalDonateUrl);

    private void OpenIssueTrackerButton_Click(object sender, RoutedEventArgs e) =>
        OpenUrl(IssueTrackerUrl);

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
}
