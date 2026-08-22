using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Pak;
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
        _session.PakChanged += (_, _) => RefreshRestoreButton();
        _session.BaselineChanged += (_, _) => RefreshRestoreButton();
        RefreshRestoreButton();
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

        RefreshRestoreButton();
    }

    private void ThemeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressThemeHandler || ThemeCombo.SelectedValue is not string themeMode)
        {
            return;
        }

        ThemeService.ApplyAndSave(themeMode);
    }

    private void RefreshRestoreButton()
    {
        var hasBaseline = _session?.HasPak == true
            && !string.IsNullOrWhiteSpace(_session.PakPath)
            && PakBaselineService.HasBaseline(_session.PakPath);
        RestoreFullBaselineButton.IsEnabled = hasBaseline;
    }

    private void RestoreFullBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Main.RestoreFullBaselineConfirmMessage,
            UiText.Main.RestoreFullBaselineConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var pakPath = _session.PakPath;
            PakBaselineService.RestorePakFromBaseline(pakPath);
            var summary = InitialPakReader.ReadSummary(pakPath);
            _session.SetPak(pakPath, summary);
            MessageBox.Show(
                UiText.Main.RestoreFullBaselineMessage,
                UiText.Main.RestoreFullBaselineSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.BaselineErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
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
