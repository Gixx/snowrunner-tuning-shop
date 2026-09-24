using SnowRunnerTuningShop.Core.Trucks;

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

/// <summary>Three-position global rear counter-steer preset (not a baseline multiplier).</summary>
public enum TruckRearSteerGlobalMode
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

    /// <summary>GameData Country — comma-separated store region codes.</summary>
    public string StoreCountries { get; set; } = "";

    public string BaselineStoreCountries { get; init; } = "";

    public bool IsRegionFree => TruckStoreRegions.HasAllStoreRegions(StoreCountries);

    /// <summary>GameData UnlockByRank (0–30). 0 removes the rank gate where the game accepts it.</summary>
    public int UnlockByRank { get; set; }

    public int BaselineUnlockByRank { get; init; }

    public TruckDiffLockMode DiffLock { get; set; }

    public string DiffLockTypeRaw { get; init; } = "";

    /// <summary>
    /// True when the baseline pak already has a diff-lock upgrade slot/addons for this truck.
    /// </summary>
    public bool HasNativeDiffLockOptions { get; init; }

    public TruckDriveLayout DriveLayout { get; set; }

    /// <summary>TruckData Responsiveness — steering-wheel input feel (not return-to-center).</summary>
    public double Responsiveness { get; set; }

    public double BaselineResponsiveness { get; init; }

    /// <summary>TruckData SteerSpeed — how quickly the wheels turn when steering.</summary>
    public double SteerSpeed { get; set; }

    public double BaselineSteerSpeed { get; init; }

    /// <summary>TruckData BackSteerSpeed — how quickly the wheels return to center.</summary>
    public double BackSteerSpeed { get; set; }

    public double BaselineBackSteerSpeed { get; init; }

    /// <summary>Primary chassis <c>PhysicsModel</c>/<c>Body Mass</c> (ImpactType=Truck when present).</summary>
    public bool HasMass { get; init; }

    public double Mass { get; set; }

    public double BaselineMass { get; init; }

    /// <summary>Per axle/wheel <c>SteeringAngle</c> editors (file order).</summary>
    public List<TruckSteerAxle> SteerAxles { get; set; } = [];

    public bool HasBaselineFrontSteer =>
        SteerAxles.Any(axle => axle.HadSteerInBaseline && axle.BaselineAngle is > 0);

    public bool HasBaselineRearSteer =>
        SteerAxles.Any(axle => axle.HadSteerInBaseline && axle.BaselineAngle is < 0);

    /// <summary>Sound set id for &lt;Honk&gt; (folder under trucks/…), e.g. ford_f750.</summary>
    public string? HornSoundSetId { get; set; }

    /// <summary>Sound set id for Engine* tags under &lt;Sounds&gt;.</summary>
    public string? EngineSoundSetId { get; set; }
}

public sealed record TruckTuningSaveResult(int UpdatedFiles, int ChangedTrucks = 0);
