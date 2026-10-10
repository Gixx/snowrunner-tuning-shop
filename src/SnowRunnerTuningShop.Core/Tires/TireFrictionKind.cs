namespace SnowRunnerTuningShop.Core.Tires;

/// <summary>
/// Tire friction template groups for bulk absolute On-road / Off-road / Mud edits.
/// Matches <c>WheelFriction _template</c> names (including Scout* / HeavyMudtires).
/// </summary>
[Flags]
public enum TireFrictionKind
{
    None = 0,
    Highway = 1 << 0,
    AllTerrain = 1 << 1,
    Offroad = 1 << 2,
    Mud = 1 << 3,
    Chained = 1 << 4,
}

public static class TireFrictionKinds
{
    /// <summary>Absolute friction presets offered in the Parts → Tires category UI.</summary>
    public static readonly double[] AbsolutePresets =
    [
        0.5, 1.0, 1.5, 2.0, 2.5, 3.0, 3.5, 4.0,
    ];

    public static bool IsAllowedAbsolute(double value) =>
        AbsolutePresets.Any(preset => Math.Abs(preset - value) < 1e-9);

    public static bool MatchesTemplate(string? templateName, TireFrictionKind kinds)
    {
        if (kinds == TireFrictionKind.None || string.IsNullOrWhiteSpace(templateName))
        {
            return false;
        }

        return Classify(templateName) is { } kind && kinds.HasFlag(kind);
    }

    public static TireFrictionKind? Classify(string? templateName)
    {
        if (string.IsNullOrWhiteSpace(templateName))
        {
            return null;
        }

        if (templateName.Equals("Highway", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("ScoutHighway", StringComparison.OrdinalIgnoreCase))
        {
            return TireFrictionKind.Highway;
        }

        if (templateName.Equals("Allterrain", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("ScoutAllterrain", StringComparison.OrdinalIgnoreCase))
        {
            return TireFrictionKind.AllTerrain;
        }

        if (templateName.Equals("Offroad", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("ScoutOffroad", StringComparison.OrdinalIgnoreCase))
        {
            return TireFrictionKind.Offroad;
        }

        if (templateName.Equals("Mudtires", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("ScoutMudtires", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("HeavyMudtires", StringComparison.OrdinalIgnoreCase))
        {
            return TireFrictionKind.Mud;
        }

        if (templateName.Equals("Chains", StringComparison.OrdinalIgnoreCase)
            || templateName.Equals("ScoutChains", StringComparison.OrdinalIgnoreCase))
        {
            return TireFrictionKind.Chained;
        }

        return null;
    }
}
