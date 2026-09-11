namespace SnowRunnerTuningShop.Core.Tuning;

/// <summary>
/// Crane global-multiplier presets: arm force up to 10x, movement speed capped at 3x.
/// </summary>
public static class CraneMultiplierPresets
{
    public static readonly double[] ArmForceValues =
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
        6.0,
        8.0,
        10.0,
    ];

    public static readonly string[] ArmForceLabels =
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
        "6x",
        "8x",
        "10x",
    ];

    public static readonly double[] MovementSpeedValues =
    [
        0.2,
        0.25,
        1.0 / 3.0,
        0.5,
        1.0,
        2.0,
        3.0,
    ];

    public static readonly string[] MovementSpeedLabels =
    [
        "1/5",
        "1/4",
        "1/3",
        "1/2",
        "1 (baseline)",
        "2x",
        "3x",
    ];

    public const int BaselineIndex = 4;
    public const int ArmForceMaximumIndex = 11;
    public const int MovementSpeedMaximumIndex = 6;

    public static int ClampArmForceIndex(int index) =>
        Math.Clamp(index, 0, ArmForceMaximumIndex);

    public static int ClampMovementSpeedIndex(int index) =>
        Math.Clamp(index, 0, MovementSpeedMaximumIndex);

    public static double GetArmForceValue(int index) =>
        ArmForceValues[ClampArmForceIndex(index)];

    public static double GetMovementSpeedValue(int index) =>
        MovementSpeedValues[ClampMovementSpeedIndex(index)];

    public static string GetArmForceLabel(int index) =>
        ArmForceLabels[ClampArmForceIndex(index)];

    public static string GetMovementSpeedLabel(int index) =>
        MovementSpeedLabels[ClampMovementSpeedIndex(index)];
}
