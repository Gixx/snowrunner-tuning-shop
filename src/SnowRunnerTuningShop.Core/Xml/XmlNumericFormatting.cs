using System.Globalization;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Shared InvariantCulture numeric formatting for pak XML attributes.
/// Floats are capped at two decimal places so scaled presets (e.g. 1/3) do not
/// write long repeating fractions.
/// </summary>
public static class XmlNumericFormatting
{
    public const int DecimalPlaces = 2;

    public static double Round(double value) =>
        Math.Round(value, DecimalPlaces, MidpointRounding.AwayFromZero);

    /// <param name="preferInteger">Round to whole number (torque, damage, length, …).</param>
    /// <param name="keepTrailingDotZero">Emit <c>1.0</c> style for integer-like floats (StrengthMult, IK).</param>
    public static string Format(double value, bool preferInteger = false, bool keepTrailingDotZero = false)
    {
        if (preferInteger)
        {
            return ((long)Math.Round(value, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);
        }

        var rounded = Round(value);
        if (Math.Abs(rounded - Math.Round(rounded)) < 1e-9)
        {
            var integer = ((long)Math.Round(rounded, MidpointRounding.AwayFromZero))
                .ToString(CultureInfo.InvariantCulture);
            return keepTrailingDotZero ? integer + ".0" : integer;
        }

        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }
}
