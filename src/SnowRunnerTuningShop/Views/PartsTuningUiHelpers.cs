using System.Windows.Controls;

namespace SnowRunnerTuningShop.Views;

/// <summary>
/// Shared Parts-tab write-gate / grid helpers (CODE_REVIEW P1 #8).
/// </summary>
internal static class PartsTuningUiHelpers
{
    public static void SetPartWriteButtonStates(
        AppSession? session,
        string? pakPath,
        bool writesAllowed,
        Button applyMultipliers,
        Button saveIndividual,
        Button restore)
    {
        var hasPak = !string.IsNullOrWhiteSpace(pakPath);
        applyMultipliers.IsEnabled = hasPak && writesAllowed;
        saveIndividual.IsEnabled = hasPak && writesAllowed;
        restore.IsEnabled = PakWriteUi.CanRestore(session, pakPath, writesAllowed);
    }

    public static void CommitGridEdits(DataGrid grid)
    {
        grid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
        grid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);
    }

    public static void ClearWriteButtons(Button applyMultipliers, Button saveIndividual, Button restore)
    {
        applyMultipliers.IsEnabled = false;
        saveIndividual.IsEnabled = false;
        restore.IsEnabled = false;
    }
}
