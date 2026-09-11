using System.Diagnostics.CodeAnalysis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using SnowRunnerTuningShop.Core.Game;

namespace SnowRunnerTuningShop;

/// <summary>Polls for SnowRunner and keeps <see cref="AppSession.IsGameRunning"/> in sync.</summary>
internal sealed class GameRunningMonitor : IDisposable
{
    private readonly AppSession _session;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public GameRunningMonitor(AppSession session)
    {
        _session = session;
        _timer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(2),
        };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }

    private void Poll()
    {
        try
        {
            _session.SetGameRunning(SnowRunnerProcessGuard.IsRunning());
        }
        catch
        {
            // Never break the UI timer on probe failures.
        }
    }
}

/// <summary>Shared UI helpers for pak-write buttons and click guards.</summary>
internal static class PakWriteUi
{
    public static bool CanWrite(AppSession? session) =>
        session is not null && !session.IsGameRunning;

    public static bool CanRestore(AppSession? session, string? pakPath, bool writesAllowed) =>
        writesAllowed
        && !string.IsNullOrWhiteSpace(pakPath)
        && CanWrite(session)
        && Core.Backup.PakBaselineService.HasBaseline(pakPath);

    public static bool TryProceed(AppSession? session)
    {
        if (session?.IsGameRunning == true || SnowRunnerProcessGuard.IsRunning())
        {
            session?.SetGameRunning(true);
            MessageBox.Show(
                Localization.UiText.Main.GameRunningMessage,
                Localization.UiText.Main.GameRunningTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    /// <summary>
    /// Shared click preamble: game-running gate, empty pak path, optional baseline check.
    /// </summary>
    public static bool TryBeginWrite(
        AppSession? session,
        [NotNullWhen(true)] string? pakPath,
        bool writesAllowed,
        bool requireBaseline,
        Action? onMissingPak = null)
    {
        if (!writesAllowed)
        {
            return false;
        }

        if (!TryProceed(session))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(pakPath))
        {
            onMissingPak?.Invoke();
            return false;
        }

        if (requireBaseline && !Core.Backup.PakBaselineService.HasBaseline(pakPath))
        {
            MessageBox.Show(
                Localization.UiText.Main.BaselineMissingShort,
                Localization.UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        return true;
    }

    public static IDisposable BeginBusyWrite(params Button[] buttons)
    {
        var previousCursor = Mouse.OverrideCursor;
        Mouse.OverrideCursor = Cursors.Wait;

        var states = new (Button Button, bool WasEnabled)[buttons.Length];
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            states[i] = (button, button.IsEnabled);
            button.IsEnabled = false;
        }

        return new BusyWriteScope(previousCursor, states);
    }

    private sealed class BusyWriteScope(Cursor? previousCursor, (Button Button, bool WasEnabled)[] buttons) : IDisposable
    {
        public void Dispose()
        {
            Mouse.OverrideCursor = previousCursor;
            foreach (var (button, wasEnabled) in buttons)
            {
                button.IsEnabled = wasEnabled;
            }
        }
    }
}
