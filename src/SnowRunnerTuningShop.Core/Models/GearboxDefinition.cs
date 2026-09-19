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
    public double DamageCapacity { get; set; }
    public double FuelConsumption { get; set; }
    public double IdleFuelModifier { get; set; }
    /// <summary>Null when AWDConsumptionModifier is absent in XML.</summary>
    public double? AwdConsumptionModifier { get; set; }
    public bool HasAwdConsumptionModifier => AwdConsumptionModifier.HasValue;

    /// <summary>Highest <c>Gear</c> AngVel (Auto top speed). Null when no Gear tags.</summary>
    public double? MaxGearAngVel { get; set; }

    /// <summary><c>HighGear</c> AngVel (H). Null when the tag is absent.</summary>
    public double? HighGearAngVel { get; set; }

    /// <summary><c>ReverseGear</c> AngVel (R). Null when the tag is absent.</summary>
    public double? ReverseGearAngVel { get; set; }
}
