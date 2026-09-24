using System.Globalization;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Tuning;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Read/write <c>Mass</c> on truck/trailer <c>PhysicsModel</c> bodies.
/// Detail edits set the primary chassis mass and scale nested masses proportionally;
/// global multipliers scale every <c>Mass</c> under each <c>PhysicsModel</c>.
/// </summary>
public static class VehiclePhysicsMassXml
{
    public const double MinMass = 0.01;
    public const double MaxMass = 200_000;

    private static readonly Regex PhysicsModelBlockRegex = new(
        @"<PhysicsModel\b(?<attrs>[^>]*)>(?<inner>.*?)</PhysicsModel>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex MassAttributeRegex = new(
        @"(?<prefix>\bMass\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex BodyOpenTagRegex = new(
        @"<Body\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex ImpactTypeTruckRegex = new(
        @"ImpactType\s*=\s*""Truck""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryReadPrimaryMass(string text, out double mass)
    {
        mass = 0;
        foreach (Match block in PhysicsModelBlockRegex.Matches(text))
        {
            var inner = block.Groups["inner"].Value;
            if (TryReadPreferredBodyMass(inner, preferTruckImpact: true, out mass))
            {
                return true;
            }

            if (TryReadPreferredBodyMass(inner, preferTruckImpact: false, out mass))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets primary chassis mass and scales every other <c>Mass</c> in <c>PhysicsModel</c> by the same ratio.
    /// </summary>
    public static string ApplyPrimaryMass(string text, double newMass)
    {
        var clamped = Math.Clamp(newMass, MinMass, MaxMass);
        if (!TryReadPrimaryMass(text, out var current) || current <= 0)
        {
            return text;
        }

        var ratio = clamped / current;
        if (Math.Abs(ratio - 1.0) < 1e-12)
        {
            return text;
        }

        return ScaleAllMasses(text, ratio);
    }

    /// <summary>Multiplies every <c>Mass</c> under each <c>PhysicsModel</c> (skips baseline 1.0).</summary>
    public static string ScaleAllMasses(string text, double multiplier)
    {
        if (TuningMultiplierPresets.IsBaselineMultiplier(multiplier))
        {
            return text;
        }

        PartPakPipeline.ValidateMultiplier(multiplier, nameof(multiplier));

        return PhysicsModelBlockRegex.Replace(
            text,
            match =>
            {
                var inner = MassAttributeRegex.Replace(
                    match.Groups["inner"].Value,
                    massMatch => ScaleMassMatch(massMatch, multiplier));
                return $"<PhysicsModel{match.Groups["attrs"].Value}>{inner}</PhysicsModel>";
            });
    }

    public static string FormatMass(double value) =>
        XmlNumericFormatting.Format(Math.Clamp(value, MinMass, MaxMass));

    private static string ScaleMassMatch(Match massMatch, double multiplier)
    {
        if (!TryParseMass(massMatch.Groups["value"].Value, out var current))
        {
            return massMatch.Value;
        }

        if (Math.Abs(current) < 1e-12)
        {
            return massMatch.Value;
        }

        var scaled = Math.Clamp(current * multiplier, MinMass, MaxMass);
        return $"{massMatch.Groups["prefix"].Value}{FormatMass(scaled)}{massMatch.Groups["suffix"].Value}";
    }

    private static bool TryReadPreferredBodyMass(string physicsInner, bool preferTruckImpact, out double mass)
    {
        mass = 0;
        foreach (Match bodyTag in BodyOpenTagRegex.Matches(physicsInner))
        {
            var attrs = bodyTag.Groups["attrs"].Value;
            if (preferTruckImpact && !ImpactTypeTruckRegex.IsMatch(attrs))
            {
                continue;
            }

            var massMatch = MassAttributeRegex.Match(attrs);
            if (!massMatch.Success || !TryParseMass(massMatch.Groups["value"].Value, out var parsed) || parsed <= 0)
            {
                continue;
            }

            mass = parsed;
            return true;
        }

        return false;
    }

    private static bool TryParseMass(string raw, out double value) =>
        double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
