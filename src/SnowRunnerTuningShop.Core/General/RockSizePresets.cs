namespace SnowRunnerTuningShop.Core.General;

/// <summary>
/// Rock physics scale as 0–100% (slider percent → scale 0.0–1.0).
/// </summary>
public static class RockSizePresets
{
    public const int MinimumPercent = 0;
    public const int MaximumPercent = 100;
    public const int BaselinePercent = 100;

    public static int ClampPercent(int percent) =>
        Math.Clamp(percent, MinimumPercent, MaximumPercent);

    public static double PercentToScale(int percent) =>
        ClampPercent(percent) / 100.0;

    public static int ScaleToPercent(double scale) =>
        ClampPercent((int)Math.Round(Math.Clamp(scale, 0.0, 1.0) * 100.0, MidpointRounding.AwayFromZero));

    public static bool IsBaselineScale(double scale) =>
        Math.Abs(scale - 1.0) < 1e-9;

    public static string FormatPercentLabel(int percent)
    {
        percent = ClampPercent(percent);
        if (percent == MinimumPercent)
        {
            return "No collision";
        }

        if (percent == BaselinePercent)
        {
            return "Vanilla (baseline)";
        }

        return $"{percent}%";
    }
}
