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

    /// <summary>
    /// Max wheel angular acceleration limiter. Smaller = slower acceleration.
    /// Null when the attribute is absent in XML (do not confuse with explicit 0).
    /// Vanilla is typically ~0.01–0.1; the app clamps writes to 0–10.
    /// </summary>
    public double? MaxDeltaAngVel { get; set; }

    /// <summary>True when MaxDeltaAngVel was present in the XML (or the user set a value).</summary>
    public bool HasMaxDeltaAngVel { get; set; }
}
