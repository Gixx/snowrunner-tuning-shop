namespace SnowRunnerTuningShop.Core.Models;

public sealed class EngineDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    public required string SetId { get; init; }
    public string SetName { get; init; } = "";
    public string UsedBy { get; init; } = "";
    public string UsedByTooltip { get; init; } = "";
    public required string Category { get; init; }
    public int Price { get; init; }
    public double Torque { get; set; }
    public double FuelConsumption { get; set; }
    public double DamageCapacity { get; set; }
    /// <summary>
    /// Engine RPM ramp speed. When absent in XML the game uses 0.04; we surface that default here.
    /// </summary>
    public double EngineResponsiveness { get; set; }
    /// <summary>True when EngineResponsiveness was present in the XML (or should be written back).</summary>
    public bool HasEngineResponsiveness { get; set; }
}
