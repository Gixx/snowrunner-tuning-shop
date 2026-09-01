using System.Globalization;

namespace SnowRunnerTuningShop.Core.PhotoMode;

internal static class PhotoModeValueFormatting
{
    internal static string FormatDefaultValue(double value, bool isInteger, int fieldWidth)
    {
        if (isInteger)
        {
            return ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
        }

        var twoDecimal = value.ToString("0.00", CultureInfo.InvariantCulture);
        if (twoDecimal.Length <= fieldWidth)
        {
            return twoDecimal;
        }

        return value.ToString("0.##", CultureInfo.InvariantCulture);
    }

    internal static bool FitsInField(double value, bool isInteger, int fieldWidth) =>
        FormatDefaultValue(value, isInteger, fieldWidth).Length <= fieldWidth;
}
