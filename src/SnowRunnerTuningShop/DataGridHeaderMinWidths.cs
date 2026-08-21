using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SnowRunnerTuningShop;

/// <summary>
/// Keeps DataGrid column MinWidth at least as wide as the header label,
/// so headers stay readable after user resize and across languages.
/// </summary>
internal static class DataGridHeaderMinWidths
{
    private const double HeaderHorizontalPadding = 24; // matches DataGridColumnHeader Padding 12,8
    private const double ResizeGripAllowance = 14;

    public static void Apply(DataGrid dataGrid)
    {
        ArgumentNullException.ThrowIfNull(dataGrid);

        if (!dataGrid.IsLoaded)
        {
            return;
        }

        var fontSize = dataGrid.FontSize > 0 ? dataGrid.FontSize : SystemFonts.MessageFontSize;
        var typeface = new Typeface(
            dataGrid.FontFamily,
            dataGrid.FontStyle,
            dataGrid.FontWeight,
            dataGrid.FontStretch);
        var pixelsPerDip = VisualTreeHelper.GetDpi(dataGrid).PixelsPerDip;

        foreach (var column in dataGrid.Columns)
        {
            var headerText = column.Header?.ToString();
            if (string.IsNullOrWhiteSpace(headerText))
            {
                continue;
            }

            var formatted = new FormattedText(
                headerText,
                CultureInfo.CurrentUICulture,
                dataGrid.FlowDirection,
                typeface,
                fontSize,
                Brushes.Black,
                pixelsPerDip);

            var minWidth = Math.Ceiling(formatted.Width) + HeaderHorizontalPadding + ResizeGripAllowance;
            if (column.MinWidth < minWidth)
            {
                column.MinWidth = minWidth;
            }

            if (column.Width.IsAbsolute && column.Width.Value < column.MinWidth)
            {
                column.Width = new DataGridLength(column.MinWidth);
            }
        }
    }
}
