namespace SnowRunnerTuningShop.Core.General;

public enum CameraCollisionMode
{
    /// <summary>Restore ClipCamera values from the workspace baseline.</summary>
    Baseline,

    /// <summary>Game-style camera clipping against map objects (ClipCamera="true").</summary>
    CollisionsOn,

    /// <summary>Camera passes through map objects (ClipCamera="false").</summary>
    CollisionsOff,
}

public enum CameraCollisionState
{
    Unknown,
    CollisionsOn,
    CollisionsOff,
    Mixed,
    Empty,
}
