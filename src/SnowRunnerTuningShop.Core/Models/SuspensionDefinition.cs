namespace SnowRunnerTuningShop.Core.Models;

public sealed class SuspensionDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    /// <summary>Suspension set id matching truck SuspensionSocket Type (filename without .xml).</summary>
    public required string SetId { get; init; }
    public string SetName { get; init; } = "";
    public string UsedBy { get; init; } = "";
    public string UsedByTooltip { get; init; } = "";
    public required string Category { get; init; }
    public int Price { get; init; }
    public double DamageCapacity { get; set; }
    public double? FrontHeight { get; set; }
    public double? FrontStrength { get; set; }
    public double? FrontDamping { get; set; }
    public bool HasFront { get; init; }
    public double? RearHeight { get; set; }
    public double? RearStrength { get; set; }
    public double? RearDamping { get; set; }
    public bool HasRear { get; init; }
}
