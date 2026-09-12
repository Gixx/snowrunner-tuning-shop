using Avalonia.Controls;
using Avalonia.Input;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Game;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

internal static class PakWriteUi
{
    public static bool CanWrite(AppSession? session) =>
        session is not null && !session.IsGameRunning;

    public static async Task<bool> TryProceed(Window owner, AppSession? session)
    {
        if (session?.IsGameRunning == true || SnowRunnerProcessGuard.IsRunning())
        {
            session?.SetGameRunning(true);
            await AppDialogs.ShowWarning(
                owner,
                UiText.Main.GameRunningMessage,
                UiText.Main.GameRunningTitle);
            return false;
        }

        return true;
    }

    public static IDisposable BeginBusyWrite(Window owner, params Button[] buttons)
    {
        var previous = owner.Cursor;
        owner.Cursor = new Cursor(StandardCursorType.Wait);

        var states = new (Button Button, bool WasEnabled)[buttons.Length];
        for (var i = 0; i < buttons.Length; i++)
        {
            var button = buttons[i];
            states[i] = (button, button.IsEnabled);
            button.IsEnabled = false;
        }

        return new BusyWriteScope(owner, previous, states);
    }

    private sealed class BusyWriteScope(
        Window owner,
        Cursor? previous,
        (Button Button, bool WasEnabled)[] buttons) : IDisposable
    {
        public void Dispose()
        {
            owner.Cursor = previous;
            foreach (var (button, wasEnabled) in buttons)
            {
                button.IsEnabled = wasEnabled;
            }
        }
    }
}
