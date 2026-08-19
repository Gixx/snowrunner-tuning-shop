namespace SnowRunnerTuningShop.Core.Winch;

public static class WinchMultiplierPresets
{
    public static readonly double[] Values =
    [
        0.2,
        0.25,
        1.0 / 3.0,
        0.5,
        1.0,
        2.0,
        3.0,
        4.0,
        5.0,
    ];

    public static readonly string[] Labels =
    [
        "1/5",
        "1/4",
        "1/3",
        "1/2",
        "1 (baseline)",
        "2x",
        "3x",
        "4x",
        "5x",
    ];

    public const int BaselineIndex = 4;
    public const int MinimumIndex = 0;
    public const int MaximumIndex = 8;

    public static int ClampIndex(int index) =>
        Math.Clamp(index, MinimumIndex, MaximumIndex);

    public static double GetValue(int index) =>
        Values[ClampIndex(index)];

    public static string GetLabel(int index) =>
        Labels[ClampIndex(index)];

    public static string FormatSliderCaption(string prefix, int index) =>
        $"{prefix}: {GetLabel(index)}";

    public static bool IsBaselineMultiplier(double multiplier) =>
        Math.Abs(multiplier - 1.0) < 1e-9;
}
