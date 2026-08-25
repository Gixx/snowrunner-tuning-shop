namespace SnowRunnerTuningShop.Core.Models;

public enum TruckDriveLayout
{
    Rwd,
    AlwaysAwd,
    SelectableAwd,
}

public enum TruckDiffLockMode
{
    AlwaysOn,
    Switchable,
    Upgradeable,
    None,
}

/// <summary>Three-position global front steer preset (not a baseline multiplier).</summary>
public enum TruckFrontSteerGlobalMode
{
    Minimum = 0,
    Baseline = 1,
    Maximum = 2,
}

public sealed class TruckTuningDefinition
{
    public required string EntryPath { get; init; }

    public required string TruckId { get; init; }

    public string UiNameKey { get; init; } = "";

    public required string DisplayName { get; init; }

    public int FuelCapacity { get; set; }

    public int BaselineFuelCapacity { get; init; }

    /// <summary>Truck store price from GameData Price.</summary>
    public int Price { get; set; }

    public int BaselinePrice { get; init; }

    public TruckDiffLockMode DiffLock { get; set; }

    public string DiffLockTypeRaw { get; init; } = "";

    /// <summary>
    /// True when the baseline pak already has a diff-lock upgrade slot/addons for this truck.
    /// </summary>
    public bool HasNativeDiffLockOptions { get; init; }

    public TruckDriveLayout DriveLayout { get; set; }

    /// <summary>TruckData Responsiveness — steering wheel return speed.</summary>
    public double Responsiveness { get; set; }

    public double BaselineResponsiveness { get; init; }

    /// <summary>Front steering angle in degrees (0–90), or null when the truck has no front steer wheels.</summary>
    public double? FrontSteerAngle { get; set; }

    public double? BaselineFrontSteerAngle { get; init; }

    /// <summary>Rear counter-steer angle in degrees (−90–0), or null when the truck has no rear steer wheels.</summary>
    public double? RearSteerAngle { get; set; }

    public double? BaselineRearSteerAngle { get; init; }

    public bool HasFrontSteer { get; init; }

    public bool HasRearSteer { get; init; }
}

public sealed record TruckTuningSaveResult(int UpdatedFiles, int ChangedTrucks = 0);
