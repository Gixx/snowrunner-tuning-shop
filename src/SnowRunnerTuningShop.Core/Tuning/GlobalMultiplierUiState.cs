using SnowRunnerTuningShop.Core.Cargo;

namespace SnowRunnerTuningShop.Core.Tuning;

/// <summary>
/// Last-used global multiplier / checkbox UI state for one category, keyed by active preset.
/// Independent of individual row edits on vehicles/parts/trailers.
/// </summary>
public sealed class GlobalMultiplierUiDocument
{
    public Dictionary<string, GlobalMultiplierUiPresetState> ByPresetId { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GlobalMultiplierUiPresetState
{
    public EngineGlobalUiState? Engines { get; set; }
    public GearboxGlobalUiState? Gearboxes { get; set; }
    public SuspensionGlobalUiState? Suspensions { get; set; }
    public WinchGlobalUiState? Winches { get; set; }
    public TireGlobalUiState? Tires { get; set; }
    public CraneGlobalUiState? Cranes { get; set; }
    public AddonGlobalUiState? Addons { get; set; }
    public TrailerGlobalUiState? Trailers { get; set; }
    public CargoGlobalUiState? Cargo { get; set; }
    public VehicleGlobalUiState? Vehicles { get; set; }
}

public sealed class EngineGlobalUiState
{
    public int TorqueIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int FuelIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int DamageIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int ResponsivenessIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class GearboxGlobalUiState
{
    public int FuelIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int IdleIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int AwdIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int DamageIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class SuspensionGlobalUiState
{
    public int HeightIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int StrengthIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int DampingIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int DamageIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class WinchGlobalUiState
{
    public int LengthIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int StrengthIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public bool AutonomousAll { get; set; }
}

public sealed class TireGlobalUiState
{
    public int DamageIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public bool IgnoreIceAll { get; set; }
}

public sealed class CraneGlobalUiState
{
    public int ArmForceIndex { get; set; } = CraneMultiplierPresets.BaselineIndex;
    public int MovementSpeedIndex { get; set; } = CraneMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class AddonGlobalUiState
{
    public int FuelIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int WaterIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int RepairsIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int WheelsIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class TrailerGlobalUiState
{
    public int FuelIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int RepairsIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int WheelsIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int MassIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
}

public sealed class CargoGlobalUiState
{
    public int MassIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int ComBiasIndex { get; set; } = CargoService.ComBiasBaselineIndex;
}

public sealed class VehicleGlobalUiState
{
    public int FuelIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int ResponsivenessIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int PriceIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public int MassIndex { get; set; } = TuningMultiplierPresets.BaselineIndex;
    public bool ReleaseRegionLock { get; set; }
    public bool UnlockAllVehicles { get; set; }
    public bool FastTuningLowerCom { get; set; }
    public bool FastTuningBetterTurning { get; set; }
    public bool FastTuningFasterSteering { get; set; }
    public bool FastTuningLargerTires { get; set; }
    public bool AlwaysOnDiffLock { get; set; }
    public bool AlwaysOnAwd { get; set; }
}
