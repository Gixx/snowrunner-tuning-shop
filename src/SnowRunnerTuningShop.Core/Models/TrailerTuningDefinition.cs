namespace SnowRunnerTuningShop.Core.Models;

public sealed class TrailerTuningDefinition
{
    public required string EntryPath { get; init; }

    public required string TrailerId { get; init; }

    public required string DisplayName { get; init; }

    public bool HasGameData { get; init; }

    public int Price { get; set; }

    public int BaselinePrice { get; init; }

    public int UnlockByRank { get; set; }

    public int BaselineUnlockByRank { get; init; }

    /// <summary>
    /// GameData IsQuest. True hides the trailer from the trailer store even when Price is set.
    /// </summary>
    public bool IsQuest { get; set; }

    public bool BaselineIsQuest { get; init; }

    public bool HasFuel { get; init; }

    public int FuelCapacity { get; set; }

    public int BaselineFuelCapacity { get; init; }

    public bool HasRepairs { get; init; }

    public int RepairsCapacity { get; set; }

    public int BaselineRepairsCapacity { get; init; }

    public bool HasWheels { get; init; }

    public int WheelRepairsCapacity { get; set; }

    public int BaselineWheelRepairsCapacity { get; init; }

    public bool HasWater { get; init; }

    public int WaterCapacity { get; set; }

    public int BaselineWaterCapacity { get; init; }
}

public sealed record TrailerTuningSaveResult(int UpdatedFiles, int ChangedTrailers = 0);
