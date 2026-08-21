namespace SnowRunnerTuningShop.Core.Models;

public sealed class GearboxDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    /// <summary>Gearbox set id matching truck GearboxSocket Type (filename without .xml).</summary>
    public required string SetId { get; init; }
    public string SetName { get; init; } = "";
    public string UsedBy { get; init; } = "";
    public string UsedByTooltip { get; init; } = "";
    public required string Category { get; init; }
    public int Price { get; init; }
    public double FuelConsumption { get; set; }
    public double IdleFuelModifier { get; set; }
    public double AwdConsumptionModifier { get; set; }
    public bool HasAwdConsumptionModifier { get; set; }
}
