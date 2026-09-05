using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class BugReportWindow : Window
{
    private readonly string? _profilePath;
    private bool _sending;

    public BugReportWindow()
    {
        InitializeComponent();
        _profilePath = BugReportService.TryGetActiveProfilePath();
        IncludeProfileCheckBox.IsEnabled = _profilePath is not null;
        IncludeProfileCheckBox.IsChecked = false;
        if (_profilePath is null)
        {
            IncludeProfileCheckBox.ToolTip = UiText.BugReport.NoProfileTooltip;
        }

        UpdateCharCount();
    }

    private void DescriptionBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateCharCount();

    private void UpdateCharCount()
    {
        var length = DescriptionBox.Text?.Length ?? 0;
        CharCountText.Text = UiText.BugReport.CharCount(length, BugReportService.MaxDescriptionLength);
        SendButton.IsEnabled = !_sending && length > 0;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sending)
        {
            return;
        }

        DialogResult = false;
        Close();
    }

    private async void SendButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sending)
        {
            return;
        }

        var description = DescriptionBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            MessageBox.Show(
                UiText.BugReport.EmptyDescription,
                UiText.BugReport.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (!BugReportSecrets.IsMailtrapConfigured)
        {
            MessageBox.Show(
                UiText.BugReport.NotConfigured,
                UiText.BugReport.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var includeProfile = IncludeProfileCheckBox.IsChecked == true && _profilePath is not null;
        _sending = true;
        SendButton.IsEnabled = false;
        SendButton.Content = UiText.BugReport.Sending;
        try
        {
            await BugReportService.SendAsync(description, includeProfile);
            MessageBox.Show(
                UiText.BugReport.SendSucceeded,
                UiText.BugReport.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                UiText.BugReport.SendFailed(ex.Message),
                UiText.BugReport.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _sending = false;
            SendButton.Content = UiText.BugReport.Send;
            UpdateCharCount();
        }
    }
}
