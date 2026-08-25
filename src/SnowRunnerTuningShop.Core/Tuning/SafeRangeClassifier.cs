using System.Globalization;

namespace SnowRunnerTuningShop.Core.Tuning;

public enum SafeRangeZone
{
    Normal,
    Warning,
    Extreme,
    Invalid,
}

public static class SafeRangeClassifier
{
    private const double WarningLowRatio = 0.5;
    private const double WarningHighRatio = 2.0;
    private const double NormalLowRatio = 0.8;
    private const double NormalHighRatio = 1.25;

    public static SafeRangeZone Classify(double value, TuningFieldRange range)
    {
        if (value < range.Min || value > range.Max)
        {
            return SafeRangeZone.Invalid;
        }

        if (range.Baseline is { } baseline && Math.Abs(baseline) > 1e-9)
        {
            var ratio = value / baseline;
            if (ratio >= NormalLowRatio && ratio <= NormalHighRatio)
            {
                return SafeRangeZone.Normal;
            }

            if (ratio >= WarningLowRatio && ratio <= WarningHighRatio)
            {
                return SafeRangeZone.Warning;
            }

            return SafeRangeZone.Extreme;
        }

        var span = range.Max - range.Min;
        if (span <= 0)
        {
            return SafeRangeZone.Normal;
        }

        var position = (value - range.Min) / span;
        if (position is >= 0.25 and <= 0.75)
        {
            return SafeRangeZone.Normal;
        }

        if (position is >= 0.05 and <= 0.95)
        {
            return SafeRangeZone.Warning;
        }

        return SafeRangeZone.Extreme;
    }

    public static bool TryParseValue(string? text, out double value) =>
        double.TryParse(
            text?.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
}
