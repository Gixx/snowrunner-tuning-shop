using System.Globalization;
using System.Windows.Data;

namespace SnowRunnerTuningShop.Converters;

/// <summary>
/// Formats and parses integers with invariant digits (LostFocus-friendly).
/// </summary>
public sealed class InvariantIntConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is not int and not long and not short and not byte)
        {
            return Binding.DoNothing;
        }

        return System.Convert.ToInt64(value, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim() ?? string.Empty;
        if (text.Length == 0 || text == "-")
        {
            return Binding.DoNothing;
        }

        if (!int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            && !int.TryParse(text, NumberStyles.Integer, CultureInfo.CurrentCulture, out parsed))
        {
            return Binding.DoNothing;
        }

        return parsed;
    }
}
