using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

internal static class GlobalExceptionHandler
{
    private static bool _registered;
    private static string? _lastFingerprint;
    private static DateTimeOffset _lastShownAt;

    public static void Register()
    {
        if (_registered)
        {
            return;
        }

        _registered = true;
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Handle(e.ExceptionObject as Exception, e.IsTerminating);
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Handle(e.Exception, isTerminating: false);
            e.SetObserved();
        };
        Dispatcher.UIThread.UnhandledException += (_, e) =>
        {
            Handle(e.Exception, isTerminating: false);
            e.Handled = true;
        };
    }

    public static void Handle(Exception? exception, bool isTerminating)
    {
        if (exception is null)
        {
            return;
        }

        var report = CrashReportService.Build(exception, isTerminating);
        if (_lastFingerprint == report.Fingerprint
            && DateTimeOffset.UtcNow - _lastShownAt < TimeSpan.FromSeconds(8))
        {
            return;
        }

        string logPath;
        try
        {
            logPath = CrashReportService.SaveToDisk(report);
        }
        catch
        {
            logPath = "";
        }

        _lastFingerprint = report.Fingerprint;
        _lastShownAt = DateTimeOffset.UtcNow;

        void Show()
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: { } window
                })
            {
                return;
            }

            var body = string.IsNullOrWhiteSpace(logPath)
                ? report.FullText
                : $"{report.FullText}\n\nSaved: {logPath}";
            _ = AppDialogs.ShowError(window, body, UiText.CrashReport.Title);
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(Show);
            return;
        }

        Show();
    }
}
