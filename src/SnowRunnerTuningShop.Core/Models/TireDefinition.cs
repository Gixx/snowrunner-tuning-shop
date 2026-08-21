namespace SnowRunnerTuningShop.Core.Models;

public sealed class TireDefinition
{
    public required string EntryPath { get; init; }
    public required string Name { get; init; }
    public string UiNameKey { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public required string SourceFile { get; init; }
    /// <summary>Wheel set id matching truck CompatibleWheels Type (filename without .xml).</summary>
    public required string SetId { get; init; }
    public string SetName { get; init; } = "";
    public string UsedBy { get; init; } = "";
    public string UsedByTooltip { get; init; } = "";
    public required string Category { get; init; }
    public int Price { get; init; }
    public string FrictionTemplate { get; init; } = "";

    /// <summary>Game UI "On-road" — XML BodyFrictionAsphalt.</summary>
    public double OnRoadFriction { get; set; }

    /// <summary>Game UI "Off-road" — XML BodyFriction.</summary>
    public double OffRoadFriction { get; set; }

    /// <summary>Game UI "Mud" — XML SubstanceFriction.</summary>
    public double MudFriction { get; set; }

    /// <summary>XML IsIgnoreIce on WheelFriction (chains / ice grip).</summary>
    public bool IgnoreIce { get; set; }
}
