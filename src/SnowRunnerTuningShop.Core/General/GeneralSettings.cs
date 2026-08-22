namespace SnowRunnerTuningShop.Core.General;

public sealed class GeneralSettings
{
    public CameraCollisionState CameraCollisionState { get; init; }

    public int CameraEligibleModels { get; init; }

    public double RockSizeScale { get; init; }

    public int RockPlantFiles { get; init; }
}

public sealed record GeneralSaveResult(int UpdatedFiles);
