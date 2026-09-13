using Avalonia.Controls;

namespace SnowRunnerTuningShop.Desktop.Views;

/// <summary>
/// Shared Parts-tab write-gate / grid helpers.
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
        // Avalonia DataGrid typically commits on LostFocus; no CommitEdit API.
        _ = grid;
    }

    public static void ClearWriteButtons(Button applyMultipliers, Button saveIndividual, Button restore)
    {
        applyMultipliers.IsEnabled = false;
        saveIndividual.IsEnabled = false;
        restore.IsEnabled = false;
    }

    public static void SetColumnHeaders(DataGrid grid, params string[] headers)
    {
        var count = Math.Min(grid.Columns.Count, headers.Length);
        for (var i = 0; i < count; i++)
        {
            grid.Columns[i].Header = headers[i];
        }
    }
}
