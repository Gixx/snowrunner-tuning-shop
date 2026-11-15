namespace SnowRunnerTuningShop.Core.Tuning;

/// <summary>
/// Shared global-multiplier stops: 0.1–1.0 @ 0.1, 1–2 @ 0.2, 2–3 @ 0.25, 3–5 @ 0.5.
/// </summary>
public static class TuningMultiplierPresets
{
    public static readonly double[] Values =
    [
        0.1,
        0.2,
        0.3,
        0.4,
        0.5,
        0.6,
        0.7,
        0.8,
        0.9,
        1.0,
        1.2,
        1.4,
        1.6,
        1.8,
        2.0,
        2.25,
        2.5,
        2.75,
        3.0,
        3.5,
        4.0,
        4.5,
        5.0,
    ];

    public static readonly string[] Labels =
    [
        "0.1x",
        "0.2x",
        "0.3x",
        "0.4x",
        "0.5x",
        "0.6x",
        "0.7x",
        "0.8x",
        "0.9x",
        "1 (baseline)",
        "1.2x",
        "1.4x",
        "1.6x",
        "1.8x",
        "2x",
        "2.25x",
        "2.5x",
        "2.75x",
        "3x",
        "3.5x",
        "4x",
        "4.5x",
        "5x",
    ];

    public const int BaselineIndex = 9;
    public const int MinimumIndex = 0;
    public const int MaximumIndex = 22;

    /// <summary>Index of 2x — upper bound for Mass sliders (0.1× … 2×).</summary>
    public const int TwoTimesIndex = 14;

    public static int ClampIndex(int index) =>
        Math.Clamp(index, MinimumIndex, MaximumIndex);

    public static int ClampMassIndex(int index) =>
        Math.Clamp(index, MinimumIndex, TwoTimesIndex);

    public static double GetValue(int index) =>
        Values[ClampIndex(index)];

    public static string GetLabel(int index) =>
        Labels[ClampIndex(index)];

    public static string FormatSliderCaption(string prefix, int index) =>
        $"{prefix}: {GetLabel(index)}";

    public static bool IsBaselineMultiplier(double multiplier) =>
        Math.Abs(multiplier - 1.0) < 1e-9;
}
