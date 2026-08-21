namespace SnowRunnerTuningShop.Views;

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

    public static void UpdatePlaceholderVisibility(System.Windows.Controls.TextBox filterBox, System.Windows.UIElement? placeholder)
    {
        if (placeholder is null)
        {
            return;
        }

        placeholder.Visibility = string.IsNullOrEmpty(filterBox.Text)
            ? System.Windows.Visibility.Visible
            : System.Windows.Visibility.Collapsed;
    }
}
