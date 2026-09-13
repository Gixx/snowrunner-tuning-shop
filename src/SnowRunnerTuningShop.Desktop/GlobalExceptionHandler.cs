using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Desktop.Views;

namespace SnowRunnerTuningShop.Desktop;

internal static class GlobalExceptionHandler
{
    private static bool _registered;
    private static bool _uiHooked;
    private static string? _lastFingerprint;
    private static DateTimeOffset _lastShownAt;
    private static bool _showing;

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
        TryHookUiThread();
    }

    /// <summary>Attach UI-thread hook once Avalonia's dispatcher exists.</summary>
    public static void TryHookUiThread()
    {
        if (_uiHooked)
        {
            return;
        }

        try
        {
            Dispatcher.UIThread.UnhandledException += (_, e) =>
            {
                Handle(e.Exception, isTerminating: false);
                e.Handled = true;
            };
            _uiHooked = true;
        }
        catch
        {
            // Dispatcher may not exist yet during very early Main failures.
        }
    }

    public static void Handle(Exception? exception, bool isTerminating)
    {
        if (exception is null || _showing)
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

        Console.Error.WriteLine(report.FullText);
        if (!string.IsNullOrWhiteSpace(logPath))
        {
            Console.Error.WriteLine($"Crash log saved to: {logPath}");
        }

        async void Show()
        {
            _showing = true;
            try
            {
                if (await TryShowAvaloniaDialogAsync(report, logPath, isTerminating).ConfigureAwait(true))
                {
                    return;
                }

                TryOsFallbackNotify(report, logPath);
            }
            finally
            {
                _showing = false;
            }
        }

        try
        {
            if (Avalonia.Application.Current is not null)
            {
                TryHookUiThread();
                if (!Dispatcher.UIThread.CheckAccess())
                {
                    Dispatcher.UIThread.Post(Show);
                    if (isTerminating)
                    {
                        Thread.Sleep(2500);
                    }

                    return;
                }

                Show();
                return;
            }
        }
        catch
        {
            // Fall through when Avalonia/dispatcher is unavailable.
        }

        TryOsFallbackNotify(report, logPath);
        if (isTerminating)
        {
            Thread.Sleep(800);
        }
    }

    private static async Task<bool> TryShowAvaloniaDialogAsync(
        CrashReport report,
        string logPath,
        bool isTerminating)
    {
        try
        {
            if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime
                {
                    MainWindow: Window owner
                })
            {
                var dialog = new CrashReportWindow(report, logPath, isTerminating);
                await dialog.ShowDialog(owner).ConfigureAwait(true);
                return true;
            }

            // Framework is up but MainWindow is missing — still try a top-level window.
            if (Avalonia.Application.Current is not null)
            {
                var dialog = new CrashReportWindow(report, logPath, isTerminating);
                var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                dialog.Closed += (_, _) => closed.TrySetResult();
                dialog.Show();
                if (isTerminating)
                {
                    await Task.WhenAny(closed.Task, Task.Delay(TimeSpan.FromMinutes(10))).ConfigureAwait(true);
                }

                return true;
            }
        }
        catch (Exception dialogEx)
        {
            Console.Error.WriteLine($"Crash report dialog failed: {dialogEx.Message}");
        }

        return false;
    }

    private static void TryOsFallbackNotify(CrashReport report, string logPath)
    {
        var summary = string.IsNullOrWhiteSpace(logPath)
            ? $"{report.ExceptionType}: {report.Message}"
            : $"{report.ExceptionType}: {report.Message}\n\nLog: {logPath}";

        TryStart("notify-send",
            "SnowRunner Tuning Shop — unexpected error",
            summary);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DISPLAY"))
            || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("WAYLAND_DISPLAY")))
        {
            TryStart("zenity",
                "--error",
                "--title=SnowRunner Tuning Shop — unexpected error",
                $"--text={summary}");
            TryStart("kdialog",
                "--error",
                summary,
                "--title",
                "SnowRunner Tuning Shop — unexpected error");
        }
    }

    private static void TryStart(string fileName, params string[] args)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            using var process = Process.Start(psi);
            process?.WaitForExit(4000);
        }
        catch
        {
            // Missing host tools (common inside Flatpak) — log path on stderr is enough.
        }
    }
}
