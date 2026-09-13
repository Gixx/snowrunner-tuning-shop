using Avalonia.Controls;
using Avalonia.Input;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Game;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

internal static class PakWriteUi
{
    public static bool CanWrite(AppSession? session) =>
        session is not null && !session.IsGameRunning;

    public static bool CanRestore(AppSession? session, string? pakPath, bool writesAllowed) =>
        writesAllowed
        && !string.IsNullOrWhiteSpace(pakPath)
        && CanWrite(session)
        && PakBaselineService.HasBaseline(pakPath);

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

    /// <summary>
    /// Shared click preamble: game-running gate, empty pak path, optional baseline check.
    /// </summary>
    public static async Task<bool> TryBeginWrite(
        Window owner,
        AppSession? session,
        string? pakPath,
        bool writesAllowed,
        bool requireBaseline,
        Action? onMissingPak = null)
    {
        if (!writesAllowed)
        {
            return false;
        }

        if (!await TryProceed(owner, session))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(pakPath))
        {
            onMissingPak?.Invoke();
            return false;
        }

        if (requireBaseline && !PakBaselineService.HasBaseline(pakPath))
        {
            await AppDialogs.ShowWarning(
                owner,
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle);
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
