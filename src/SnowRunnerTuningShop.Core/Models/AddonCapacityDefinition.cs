namespace SnowRunnerTuningShop.Core.Models;

/// <summary>
/// Truck frame/wheel addon with at least one TruckData capacity attribute
/// (Fuel / Water / Repairs / Spare wheels).
/// </summary>
public sealed class AddonCapacityDefinition
{
    public required string EntryPath { get; init; }

    /// <summary>XML file stem (stable id — never match by localized display name).</summary>
    public required string Name { get; init; }

    public string UiNameKey { get; init; } = "";

    public string DisplayName { get; init; } = "";

    public required string SourceFile { get; init; }

    /// <summary>GameData Category, e.g. frame_addons / wheel_addon.</summary>
    public string Category { get; init; } = "";

    public bool HasPrice { get; init; }

    public int Price { get; set; }

    public int BaselinePrice { get; init; }

    public bool HasFuel { get; init; }

    public int FuelCapacity { get; set; }

    public int BaselineFuelCapacity { get; init; }

    public bool HasWater { get; init; }

    public int WaterCapacity { get; set; }

    public int BaselineWaterCapacity { get; init; }

    public bool HasRepairs { get; init; }

    public int RepairsCapacity { get; set; }

    public int BaselineRepairsCapacity { get; init; }

    public bool HasWheels { get; init; }

    public int WheelRepairsCapacity { get; set; }

    public int BaselineWheelRepairsCapacity { get; init; }
}

public sealed record AddonCapacitySaveResult(int UpdatedFiles, int ChangedAddons = 0);
