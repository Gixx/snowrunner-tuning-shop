using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class CrashReportWindow : Window
{
    private readonly CrashReport _report;
    private readonly string _logPath;
    private readonly bool _isTerminating;
    private CrashReportSubmission? _submission;

    public CrashReportWindow()
        : this(
            CrashReportService.Build(new InvalidOperationException("Placeholder"), isTerminating: false),
            logPath: "",
            isTerminating: false)
    {
    }

    public CrashReportWindow(CrashReport report, string logPath, bool isTerminating)
    {
        InitializeComponent();
        _report = report;
        _logPath = logPath;
        _isTerminating = isTerminating;

        Title = UiText.CrashReport.Title;
        HeadingText.Text = UiText.CrashReport.Heading;
        SummaryText.Text = UiText.CrashReport.Summary(report.ExceptionType, report.Message);
        ReportTextBox.Text = report.FullText;
        LogPathText.Text = string.IsNullOrWhiteSpace(logPath)
            ? ""
            : UiText.CrashReport.LogSaved(logPath);
        CopyButton.Content = UiText.CrashReport.CopyReport;
        EmailButton.Content = UiText.CrashReport.EmailReport;
        EmailButton.IsVisible = !string.IsNullOrWhiteSpace(AppInfo.CrashReportEmail);
        CloseButton.Content = isTerminating
            ? UiText.CrashReport.CloseApp
            : UiText.CrashReport.Continue;

        Opened += CrashReportWindow_Opened;
    }

    private async void CrashReportWindow_Opened(object? sender, EventArgs e)
    {
        Opened -= CrashReportWindow_Opened;
        GitHubButton.IsEnabled = false;
        GitHubButton.Content = UiText.CrashReport.PreparingGitHub;

        try
        {
            _submission = await CrashReportService.PrepareSubmissionAsync(_report, _logPath);
            GitHubButton.Content = _submission.ExistingIssue is { } existing
                ? UiText.CrashReport.ViewExistingIssue(existing.Number)
                : UiText.CrashReport.OpenGitHubIssue;
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

    private async void CopyButton_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            if (Clipboard is not null)
            {
                await Clipboard.SetTextAsync(_report.FullText);
            }

            await AppDialogs.ShowInfo(this, UiText.CrashReport.Copied, UiText.CrashReport.Title);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(this, ex.Message, UiText.CrashReport.Title);
        }
    }

    private async void GitHubButton_Click(object? sender, RoutedEventArgs e)
    {
        var url = _submission?.GitHubActionUrl ?? CrashReportService.BuildNewIssueUrl(_report);
        try
        {
            AppDialogs.OpenUrl(url);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(this, ex.Message, UiText.CrashReport.Title);
        }
    }

    private async void EmailButton_Click(object? sender, RoutedEventArgs e)
    {
        var url = _submission?.MailToUrl ?? CrashReportService.BuildMailToUrl(_report);
        if (string.IsNullOrWhiteSpace(url))
        {
            return;
        }

        try
        {
            AppDialogs.OpenUrl(url);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(this, ex.Message, UiText.CrashReport.Title);
        }
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e)
    {
        Close();
        if (_isTerminating
            && Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
