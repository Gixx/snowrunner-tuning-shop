namespace SnowRunnerTuningShop.Core.General;

public sealed class GeneralSettings
{
    public CameraCollisionState CameraCollisionState { get; init; }

    public int CameraEligibleModels { get; init; }

    public double RockSizeScale { get; init; }

    public int RockPlantFiles { get; init; }

    /// <summary>True when weak plants that had winch sockets in baseline no longer have any in the working pak.</summary>
    public bool WeakPlantWinchSocketsRemoved { get; init; }

    public int WeakPlantWinchBaselineCount { get; init; }

    public int WeakPlantWinchCurrentCount { get; init; }
}

public sealed record GeneralSaveResult(int UpdatedFiles);
