using System.Windows;
using System.Windows.Input;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop;

internal static class WorkspaceCommands
{
    public static bool TryRestoreFullBaseline(AppSession session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var pakPath = session.PakPath;
        if (!PakBaselineService.HasBaseline(pakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var confirm = MessageBox.Show(
            UiText.Main.RestoreFullBaselineConfirmMessage,
            UiText.Main.RestoreFullBaselineConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            using (OverrideCursor(Cursors.Wait))
            {
                PakBaselineService.RestorePakFromBaseline(pakPath);
                ReloadPak(session, pakPath);
            }

            MessageBox.Show(
                UiText.Main.RestoreFullBaselineMessage,
                UiText.Main.RestoreFullBaselineSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.BaselineErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public static bool TryRefreshBaselineFromGame(AppSession session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Workspace.RefreshBaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var health = WorkspaceHealthService.Evaluate(session.PakPath);
        var confirmMessage = health.Kind == WorkspaceHealthKind.GameUpdateDetected
            ? UiText.Workspace.RefreshBaselineGameUpdateConfirm
            : UiText.Workspace.RefreshBaselineUnknownConfirm;

        var confirm = MessageBox.Show(
            confirmMessage,
            UiText.Workspace.RefreshBaselineConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            using (OverrideCursor(Cursors.Wait))
            {
                PakBaselineService.RefreshBaselineFromWorkingPak(session.PakPath);
                session.NotifyBaselineChanged();
            }

            MessageBox.Show(
                UiText.Workspace.RefreshBaselineSuccessMessage,
                UiText.Workspace.RefreshBaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Workspace.RefreshBaselineTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    public static bool TryReapplySavedChanges(AppSession session)
    {
        if (session is null || string.IsNullOrWhiteSpace(session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Workspace.ReapplyTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return false;
        }

        var confirm = MessageBox.Show(
            UiText.Workspace.ReapplyConfirmMessage,
            UiText.Workspace.ReapplyConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return false;
        }

        try
        {
            TuningProfileReapplyResult result;
            using (OverrideCursor(Cursors.Wait))
            {
                result = TuningProfileService.ReapplySavedChanges(session.PakPath);
                ReloadPak(session, session.PakPath);
            }

            var image = result.MissingEntryPaths.Count > 0 || result.FailedEntryPaths.Count > 0
                ? MessageBoxImage.Warning
                : MessageBoxImage.Information;
            MessageBox.Show(
                UiText.Workspace.ReapplyReport(result),
                UiText.Workspace.ReapplySuccessTitle,
                MessageBoxButton.OK,
                image);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Workspace.ReapplyTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private static void ReloadPak(AppSession session, string pakPath)
    {
        var summary = InitialPakReader.ReadSummary(pakPath);
        session.SetPak(pakPath, summary);
        TuningProfileService.RecordWorkingPakOpened(pakPath);
    }

    private static IDisposable OverrideCursor(Cursor cursor)
    {
        var previous = Mouse.OverrideCursor;
        Mouse.OverrideCursor = cursor;
        return new CursorReset(previous);
    }

    private sealed class CursorReset(Cursor? previous) : IDisposable
    {
        public void Dispose() => Mouse.OverrideCursor = previous;
    }
}
