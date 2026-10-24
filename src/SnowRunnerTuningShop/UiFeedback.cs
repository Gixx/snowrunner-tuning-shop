using System.Windows;

namespace SnowRunnerTuningShop;

/// <summary>Shared success feedback for WPF write actions (non-blocking toast).</summary>
internal static class UiFeedback
{
    /// <summary>
    /// Shows a bottom snackbar on the main window. Title is ignored (message already includes counts).
    /// Falls back to a modal only if the main window toast host is unavailable.
    /// </summary>
    public static void ShowSuccess(string message, string? title = null)
    {
        var text = FormatToastMessage(message);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        if (Application.Current?.MainWindow is MainWindow main)
        {
            main.ShowToast(text);
            return;
        }

        MessageBox.Show(
            text,
            string.IsNullOrWhiteSpace(title) ? "SnowRunner Tuning Shop" : title,
            MessageBoxButton.OK,
            MessageBoxImage.Information);
    }

    /// <summary>Collapse multi-line dialog copy into a single snackbar-friendly paragraph.</summary>
    public static string FormatToastMessage(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "";
        }

        return message
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n\n", " — ", StringComparison.Ordinal)
            .Replace('\n', ' ')
            .Trim();
    }
}
