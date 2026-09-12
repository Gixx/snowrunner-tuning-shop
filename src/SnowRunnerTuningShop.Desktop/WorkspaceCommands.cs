using Avalonia.Controls;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Desktop.Views;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

internal static class WorkspaceCommands
{
    public static async Task<bool> TryRestoreFullBaseline(Window owner, AppSession session)
    {
        if (!await PakWriteUi.TryProceed(owner, session))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.PakPath))
        {
            await AppDialogs.ShowWarning(owner, UiText.Main.BaselineMissingShort, UiText.Main.BaselineTitle);
            return false;
        }

        var pakPath = session.PakPath;
        if (!PakBaselineService.HasBaseline(pakPath))
        {
            await AppDialogs.ShowWarning(owner, UiText.Main.BaselineMissingShort, UiText.Main.BaselineTitle);
            return false;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Main.RestoreFullBaselineConfirmMessage,
                UiText.Main.RestoreFullBaselineConfirmTitle))
        {
            return false;
        }

        try
        {
            using (PakWriteUi.BeginBusyWrite(owner))
            {
                await Task.Run(() => PakBaselineService.RestorePakFromBaseline(pakPath));
                ReloadPak(session, pakPath);
            }

            await AppDialogs.ShowInfo(
                owner,
                UiText.Main.RestoreFullBaselineMessage,
                UiText.Main.RestoreFullBaselineSuccessTitle);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.BaselineErrorTitle);
            return false;
        }
    }

    public static async Task<bool> TryRefreshBaselineFromGame(Window owner, AppSession session)
    {
        if (string.IsNullOrWhiteSpace(session.PakPath))
        {
            await AppDialogs.ShowWarning(
                owner,
                UiText.Main.BaselineMissingShort,
                UiText.Workspace.RefreshBaselineTitle);
            return false;
        }

        var health = WorkspaceHealthService.Evaluate(session.PakPath);
        var confirmMessage = health.Kind == WorkspaceHealthKind.GameUpdateDetected
            ? UiText.Workspace.RefreshBaselineGameUpdateConfirm
            : UiText.Workspace.RefreshBaselineUnknownConfirm;

        if (!await AppDialogs.Confirm(
                owner,
                confirmMessage,
                UiText.Workspace.RefreshBaselineConfirmTitle))
        {
            return false;
        }

        try
        {
            using (PakWriteUi.BeginBusyWrite(owner))
            {
                var pakPath = session.PakPath;
                await Task.Run(() => PakBaselineService.RefreshBaselineFromWorkingPak(pakPath));
                session.NotifyBaselineChanged();
            }

            await AppDialogs.ShowInfo(
                owner,
                UiText.Workspace.RefreshBaselineSuccessMessage,
                UiText.Workspace.RefreshBaselineTitle);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Workspace.RefreshBaselineTitle);
            return false;
        }
    }

    public static async Task<bool> TryReapplySavedChanges(Window owner, AppSession session)
    {
        if (!await PakWriteUi.TryProceed(owner, session))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(session.PakPath))
        {
            await AppDialogs.ShowWarning(
                owner,
                UiText.Main.BaselineMissingShort,
                UiText.Workspace.ReapplyTitle);
            return false;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Workspace.ReapplyConfirmMessage,
                UiText.Workspace.ReapplyConfirmTitle))
        {
            return false;
        }

        try
        {
            var dialog = new ReapplyProgressWindow(session.PakPath);
            await dialog.ShowDialog(owner);

            if (!dialog.Succeeded || dialog.Result is not { } result)
            {
                if (!string.IsNullOrWhiteSpace(dialog.ErrorMessage))
                {
                    await AppDialogs.ShowError(owner, dialog.ErrorMessage, UiText.Workspace.ReapplyTitle);
                }

                return false;
            }

            ReloadPak(session, session.PakPath);
            await AppDialogs.ShowInfo(
                owner,
                UiText.Workspace.ReapplyReport(result),
                UiText.Workspace.ReapplySuccessTitle);
            return true;
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Workspace.ReapplyTitle);
            return false;
        }
    }

    private static void ReloadPak(AppSession session, string pakPath)
    {
        var summary = InitialPakReader.ReadSummary(pakPath);
        session.SetPak(pakPath, summary);
        TuningProfileService.RecordWorkingPakOpened(pakPath);
    }
}
