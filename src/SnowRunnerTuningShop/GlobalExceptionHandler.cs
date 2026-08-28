using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Views;

namespace SnowRunnerTuningShop;

public static class GlobalExceptionHandler
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
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;
        TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;

        if (Application.Current is not null)
        {
            Application.Current.DispatcherUnhandledException += OnDispatcherUnhandledException;
        }
    }

    public static void Handle(Exception? exception, bool isTerminating)
    {
        if (exception is null)
        {
            return;
        }

        var report = CrashReportService.Build(exception, isTerminating);
        if (ShouldSuppressDuplicate(report.Fingerprint))
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

        if (Application.Current?.Dispatcher is { } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(() => ShowDialog(report, logPath, isTerminating));
            return;
        }

        ShowDialog(report, logPath, isTerminating);
    }

    private static bool ShouldSuppressDuplicate(string fingerprint)
    {
        if (_lastFingerprint != fingerprint)
        {
            return false;
        }

        return DateTimeOffset.UtcNow - _lastShownAt < TimeSpan.FromSeconds(8);
    }

    private static void ShowDialog(CrashReport report, string logPath, bool isTerminating)
    {
        try
        {
            var owner = Application.Current?.MainWindow;
            if (owner is { IsLoaded: true, IsVisible: true })
            {
                var dialog = new CrashReportWindow(report, logPath, isTerminating)
                {
                    Owner = owner,
                };
                dialog.ShowDialog();
                return;
            }

            new CrashReportWindow(report, logPath, isTerminating).ShowDialog();
        }
        catch (Exception dialogEx)
        {
            MessageBox.Show(
                $"{report.Message}{Environment.NewLine}{Environment.NewLine}{report.FullText}{Environment.NewLine}{Environment.NewLine}Dialog error: {dialogEx.Message}",
                "SnowRunner Tuning Shop — unexpected error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Handle(e.Exception, isTerminating: false);
        e.Handled = true;
    }

    private static void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        Handle(e.ExceptionObject as Exception, e.IsTerminating);
    }

    private static void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        Handle(e.Exception, isTerminating: false);
        e.SetObserved();
    }

    internal static void OpenUrl(string url)
    {
        Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }
}
