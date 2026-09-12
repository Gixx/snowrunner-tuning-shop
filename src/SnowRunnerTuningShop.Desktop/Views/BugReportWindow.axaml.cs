using Avalonia.Controls;
using Avalonia.Interactivity;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class BugReportWindow : Window
{
    private readonly string? _profilePath;
    private bool _sending;

    public BugReportWindow()
    {
        InitializeComponent();
        Title = UiText.BugReport.Title;
        HeadingText.Text = UiText.BugReport.Heading;
        DescriptionLabelText.Text = UiText.BugReport.DescriptionLabel;
        IncludeProfileCheckBox.Content = UiText.BugReport.IncludeProfile;
        ProfilePrivacyNoteText.Text = UiText.BugReport.ProfilePrivacyNote;
        SubmitHintText.Text = UiText.BugReport.SubmitHint;
        CancelButton.Content = UiText.BugReport.Cancel;
        SendButton.Content = UiText.BugReport.Send;

        _profilePath = BugReportService.TryGetActiveProfilePath();
        IncludeProfileCheckBox.IsEnabled = _profilePath is not null;
        IncludeProfileCheckBox.IsChecked = false;
        if (_profilePath is null)
        {
            ToolTip.SetTip(IncludeProfileCheckBox, UiText.BugReport.NoProfileTooltip);
        }

        UpdateCharCount();
    }

    private void DescriptionBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        UpdateCharCount();

    private void UpdateCharCount()
    {
        var length = DescriptionBox.Text?.Length ?? 0;
        CharCountText.Text = UiText.BugReport.CharCount(length, BugReportService.MaxDescriptionLength);
        SendButton.IsEnabled = !_sending && length > 0;
    }

    private void CancelButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_sending)
        {
            return;
        }

        Close();
    }

    private async void SendButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_sending)
        {
            return;
        }

        var description = DescriptionBox.Text ?? string.Empty;
        if (string.IsNullOrWhiteSpace(description))
        {
            await AppDialogs.ShowInfo(this, UiText.BugReport.EmptyDescription, UiText.BugReport.Title);
            return;
        }

        if (!BugReportSecrets.IsMailtrapConfigured)
        {
            await AppDialogs.ShowWarning(this, UiText.BugReport.NotConfigured, UiText.BugReport.Title);
            return;
        }

        var includeProfile = IncludeProfileCheckBox.IsChecked == true && _profilePath is not null;
        _sending = true;
        SendButton.IsEnabled = false;
        SendButton.Content = UiText.BugReport.Sending;
        try
        {
            await BugReportService.SendAsync(description, includeProfile);
            await AppDialogs.ShowInfo(this, UiText.BugReport.SendSucceeded, UiText.BugReport.Title);
            Close();
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(this, UiText.BugReport.SendFailed(ex.Message), UiText.BugReport.Title);
        }
        finally
        {
            _sending = false;
            SendButton.Content = UiText.BugReport.Send;
            UpdateCharCount();
        }
    }
}
