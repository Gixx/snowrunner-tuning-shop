using Avalonia.Controls;

namespace SnowRunnerTuningShop.Desktop.Views;

internal static class TuningListFilter
{
    public static bool Matches(string? query, params string?[] fields)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return true;
        }

        var needle = query.Trim();
        foreach (var field in fields)
        {
            if (!string.IsNullOrEmpty(field)
                && field.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static void UpdatePlaceholderVisibility(TextBox filterBox, TextBlock? placeholder)
    {
        if (placeholder is null)
        {
            return;
        }

        placeholder.IsVisible = string.IsNullOrEmpty(filterBox.Text);
    }

    public static void ApplyFilter<T>(
        DataGrid grid,
        IReadOnlyList<T> allItems,
        Func<T, bool> matches)
    {
        grid.ItemsSource = allItems.Where(matches).ToList();
    }
}
