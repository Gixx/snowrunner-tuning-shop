namespace SnowRunnerTuningShop.Core.Models;

public sealed class CraneDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    public required string Category { get; init; }
    public required string AddonType { get; init; }
    public int Price { get; init; }
    public int ArmMotorCount { get; init; }
    public double AverageArmForce { get; set; }
    public double SpeedOY { get; set; }
    public double SpeedOYWithLoad { get; set; }
    public double SpeedXZ { get; set; }
    public double SpeedXZWithLoad { get; set; }
}
