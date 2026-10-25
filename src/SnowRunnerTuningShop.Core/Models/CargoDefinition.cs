namespace SnowRunnerTuningShop.Core.Models;

/// <summary>Cargo TruckAddon under <c>classes/trucks/cargo/</c>.</summary>
public sealed class CargoDefinition
{
    public required string EntryPath { get; init; }

    /// <summary>XML file stem (stable id — never match by localized display name).</summary>
    public required string Name { get; init; }

    public string DisplayName { get; init; } = "";

    public string CargoType { get; init; } = "";

    public required string SourceFile { get; init; }

    public double TotalMass { get; set; }

    public double BaselineTotalMass { get; init; }

    public bool HasCenterOfMassY { get; init; }

    public double CenterOfMassY { get; set; }

    public double BaselineCenterOfMassY { get; init; }

    public bool HasFriction { get; init; }

    public double Friction { get; set; }

    public double BaselineFriction { get; init; }
}

public sealed record CargoSaveResult(int UpdatedFiles, int ChangedCargos = 0);
