using System.Windows;
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
}
