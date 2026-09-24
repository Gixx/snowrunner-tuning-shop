using SnowRunnerTuningShop.Core.Trucks;

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

    public static TuningFieldRange Responsiveness(double? baseline) => UnitInterval(baseline);

    public static TuningFieldRange SteerSpeed(double? baseline) => UnitInterval(baseline);

    public static TuningFieldRange BackSteerSpeed(double? baseline) => UnitInterval(baseline);

    public static TuningFieldRange UnitInterval(double? baseline) => new()
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

    public static TuningFieldRange AddedRearSteerDegrees(double? baseline) => new()
    {
        Min = TruckSteerXml.AddedRearMinDegrees,
        Max = TruckSteerXml.AddedRearMaxDegrees,
        Baseline = baseline,
        UnitSuffix = "°",
    };

    public static TuningFieldRange VanillaFrontSteerDegrees(double? baseline) => new()
    {
        Min = TruckSteerXml.VanillaFrontMinDegrees,
        Max = TruckSteerXml.VanillaFrontMaxDegrees,
        Baseline = baseline,
        UnitSuffix = "°",
    };

    public static TuningFieldRange VanillaRearSteerDegrees(double? baseline) => new()
    {
        Min = TruckSteerXml.VanillaRearMinDegrees,
        Max = TruckSteerXml.VanillaRearMaxDegrees,
        Baseline = baseline,
        UnitSuffix = "°",
    };

    public static TuningFieldRange RepairParts(double? baseline) => new()
    {
        Min = 0,
        Max = 10_000,
        Baseline = baseline,
    };

    public static TuningFieldRange SpareWheels(double? baseline) => new()
    {
        Min = 0,
        Max = 99,
        Baseline = baseline,
    };

    public static TuningFieldRange WaterLiters(double? baseline) => FuelLiters(baseline);

    public static TuningFieldRange PhysicsMass(double? baseline) => new()
    {
        Min = Xml.VehiclePhysicsMassXml.MinMass,
        Max = Xml.VehiclePhysicsMassXml.MaxMass,
        Baseline = baseline,
    };
}
