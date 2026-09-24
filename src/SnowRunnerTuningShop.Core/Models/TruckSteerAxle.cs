namespace SnowRunnerTuningShop.Core.Models;

/// <summary>
/// One wheel/axle open tag that can carry <c>SteeringAngle</c>.
/// Indexed by occurrence among axle/wheel tags in file order.
/// </summary>
public sealed class TruckSteerAxle
{
    public int OccurrenceIndex { get; init; }

    public required string Tag { get; init; }

    public string Location { get; init; } = "";

    /// <summary>
    /// Max Wheel <c>Pos</c> X for this template when known (larger ≈ further forward).
    /// </summary>
    public double? ForwardPos { get; init; }

    /// <summary>1-based display order after front→middle→rear, frontmost-first sorting.</summary>
    public int DisplayOrder { get; set; }

    /// <summary>True when the baseline pak already had SteeringAngle on this tag.</summary>
    public bool HadSteerInBaseline { get; init; }

    public double? BaselineAngle { get; init; }

    /// <summary>
    /// Current edit value. Null or 0 on save restores baseline (never strip vanilla)
    /// or removes an injected attribute for non-baseline axles.
    /// </summary>
    public double? Angle { get; set; }
}
