using System.Globalization;
using System.Windows.Data;

namespace SnowRunnerTuningShop.Converters;

/// <summary>
/// Formats and parses doubles with '.' (and the current-culture decimal separator).
/// Pair with UpdateSourceTrigger=LostFocus so incomplete edits like "3." or "3.0"
/// are not rewritten while typing.
/// </summary>
public sealed class InvariantDoubleConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (!TryToDouble(value, out var number))
        {
            return Binding.DoNothing;
        }

        return Format(number);
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var text = (value as string)?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return IsNullableTarget(targetType) ? null! : Binding.DoNothing;
        }

        if (IsIncompleteNumber(text) || !TryParse(text, out var parsed))
        {
            return Binding.DoNothing;
        }

        return parsed;
    }

    private static bool TryToDouble(object value, out double number)
    {
        switch (value)
        {
            case double d:
                number = d;
                return true;
            case float f:
                number = f;
                return true;
            case int i:
                number = i;
                return true;
            case long l:
                number = l;
                return true;
            default:
                number = 0;
                return false;
        }
    }

    private static bool TryParse(string text, out double parsed) =>
        double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out parsed)
        || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out parsed);

    private static string Format(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
        {
            return ((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static bool IsIncompleteNumber(string text) =>
        text is "-" or "." or ","
        || text.EndsWith('.')
        || text.EndsWith(',')
        || text.EndsWith("e", StringComparison.OrdinalIgnoreCase)
        || text.EndsWith("e+", StringComparison.OrdinalIgnoreCase)
        || text.EndsWith("e-", StringComparison.OrdinalIgnoreCase);

    private static bool IsNullableTarget(Type targetType) =>
        !targetType.IsValueType || Nullable.GetUnderlyingType(targetType) is not null;
}
