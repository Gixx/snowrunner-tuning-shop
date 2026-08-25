namespace SnowRunnerTuningShop.Core.Tuning;

public sealed class TuningFieldRange
{
    public required double Min { get; init; }

    public required double Max { get; init; }

    public double? Baseline { get; init; }

    public string? UnitSuffix { get; init; }

    public static TuningFieldRange FuelLiters(double? baseline) => new()
    {
        Min = 1,
        Max = 10_000,
        Baseline = baseline,
        UnitSuffix = " L",
    };

    public static TuningFieldRange StorePrice(double? baseline) => new()
    {
        Min = 0,
        Max = 9_999_999,
        Baseline = baseline,
    };

    public static TuningFieldRange Responsiveness(double? baseline) => new()
    {
        Min = 0,
        Max = 1,
        Baseline = baseline,
    };

    public static TuningFieldRange FrontSteerDegrees(double? baseline) => new()
    {
        Min = 0,
        Max = 90,
        Baseline = baseline,
        UnitSuffix = "°",
    };

    public static TuningFieldRange RearSteerDegrees(double? baseline) => new()
    {
        Min = -90,
        Max = 0,
        Baseline = baseline,
        UnitSuffix = "°",
    };
}
