using System.Windows;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class CrashReportWindow : Window
{
    private readonly CrashReport _report;
    private readonly string _logPath;
    private readonly bool _isTerminating;
    private CrashReportSubmission? _submission;

    public CrashReportWindow(CrashReport report, string logPath, bool isTerminating)
    {
        InitializeComponent();
        _report = report;
        _logPath = logPath;
        _isTerminating = isTerminating;

        SummaryText.Text = UiText.CrashReport.Summary(report.ExceptionType, report.Message);
        ReportTextBox.Text = report.FullText;
        LogPathText.Text = string.IsNullOrWhiteSpace(logPath)
            ? ""
            : UiText.CrashReport.LogSaved(logPath);
        CloseButton.Content = isTerminating
            ? UiText.CrashReport.CloseApp
            : UiText.CrashReport.Continue;
        EmailButton.Visibility = string.IsNullOrWhiteSpace(AppInfo.CrashReportEmail)
            ? Visibility.Collapsed
            : Visibility.Visible;

        Loaded += CrashReportWindow_Loaded;
    }

    private async void CrashReportWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= CrashReportWindow_Loaded;
        GitHubButton.IsEnabled = false;
        GitHubButton.Content = UiText.CrashReport.PreparingGitHub;

        try
        {
            _submission = await CrashReportService.PrepareSubmissionAsync(_report, _logPath);
            if (_submission.ExistingIssue is { } existing)
            {
                GitHubButton.Content = UiText.CrashReport.ViewExistingIssue(existing.Number);
            }
            else
            {
                GitHubButton.Content = UiText.CrashReport.OpenGitHubIssue;
            }
        }
        catch
        {
            GitHubButton.Content = UiText.CrashReport.OpenGitHubIssue;
            _submission = new CrashReportSubmission(
                _report,
                _logPath,
                null,
                CrashReportService.BuildNewIssueUrl(_report),
                CrashReportService.BuildMailToUrl(_report));
        }
        finally
        {
            GitHubButton.IsEnabled = true;
        }
    }

    private void CopyButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(_report.FullText);
            MessageBox.Show(
                UiText.CrashReport.Copied,
                UiText.CrashReport.Title,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.CrashReport.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void GitHubButton_Click(object sender, RoutedEventArgs e)
    {
        var url = _submission?.GitHubActionUrl ?? CrashReportService.BuildNewIssueUrl(_report);
        try
        {
            GlobalExceptionHandler.OpenUrl(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.CrashReport.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void EmailButton_Click(object sender, RoutedEventArgs e)
    {
        var url = _submission?.MailToUrl ?? CrashReportService.BuildMailToUrl(_report);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            GlobalExceptionHandler.OpenUrl(url);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.CrashReport.Title, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
        if (_isTerminating)
        {
            Application.Current.Shutdown();
        }
    }
}
