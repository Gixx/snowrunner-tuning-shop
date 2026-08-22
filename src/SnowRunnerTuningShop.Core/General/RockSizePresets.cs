namespace SnowRunnerTuningShop.Core.General;

public static class RockSizePresets
{
    public static readonly double[] Values =
    [
        0.0,
        0.25,
        0.5,
        0.75,
        1.0,
    ];

    public static readonly string[] Labels =
    [
        "No collision",
        "25%",
        "50%",
        "75%",
        "Vanilla (baseline)",
    ];

    public const int BaselineIndex = 4;
    public const int MinimumIndex = 0;
    public const int MaximumIndex = 4;

    public static int ClampIndex(int index) =>
        Math.Clamp(index, MinimumIndex, MaximumIndex);

    public static double GetValue(int index) =>
        Values[ClampIndex(index)];

    public static string GetLabel(int index) =>
        Labels[ClampIndex(index)];

    public static string FormatSliderCaption(int index) =>
        $"Rock physics: {GetLabel(index)}";

    public static bool IsBaselineScale(double scale) =>
        Math.Abs(scale - 1.0) < 1e-9;

    public static int FindNearestIndex(double scale)
    {
        var bestIndex = BaselineIndex;
        var bestDistance = double.MaxValue;
        for (var index = MinimumIndex; index <= MaximumIndex; index++)
        {
            var distance = Math.Abs(Values[index] - scale);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = index;
            }
        }

        return bestIndex;
    }
}
