namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>Bulk vehicle-list “Fast tuning” flags. Each option applies only where working XML still matches baseline.</summary>
public sealed class TruckFastTuningOptions
{
    public bool LowerCenterOfMass { get; init; }

    public bool BetterTurningCurve { get; init; }

    public bool FasterSteering { get; init; }

    public bool LargerTires { get; init; }

    public bool AlwaysOnDiffLock { get; init; }

    public bool AlwaysOnAwd { get; init; }

    public bool AnySelected =>
        LowerCenterOfMass
        || BetterTurningCurve
        || FasterSteering
        || LargerTires
        || AlwaysOnDiffLock
        || AlwaysOnAwd;
}
