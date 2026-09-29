using System.Globalization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Longitudinal weight balance via a shared <c>CenterOfMassOffset</c> X delta on significant truck bodies.
/// Front = negative X (Saber: closer to the front), Rear = positive X. Each body keeps its baseline X
/// plus the same delta so relative chassis/cabin spacing stays intact.
/// </summary>
public static class VehicleLongitudinalBalanceXml
{
    /// <summary>Slider Front endpoint — shift CoM toward the nose.</summary>
    public const double MinDelta = -1.0;

    /// <summary>Slider Rear endpoint — shift CoM toward the tail.</summary>
    public const double MaxDelta = 1.0;

    private const double MinSignificantMass = 50.0;

    private static readonly Regex BodyOpenTagRegex = new(
        @"<Body\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CenterOfMassOffsetRegex = new(
        @"\bCenterOfMassOffset\s*=\s*""\((?<x>[^;]*);\s*(?<y>[^;]*);\s*(?<z>[^)]*)\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryReadDelta(string workingXml, string baselineXml, out double delta)
    {
        delta = 0;
        var baselineBodies = CollectEditableBodies(baselineXml);
        if (baselineBodies.Count == 0)
        {
            return false;
        }

        var workingBodies = CollectEditableBodies(workingXml);
        double weightedDelta = 0;
        double totalMass = 0;
        foreach (var (key, baseline) in baselineBodies)
        {
            if (!workingBodies.TryGetValue(key, out var working))
            {
                continue;
            }

            var mass = Math.Max(baseline.Mass, MinSignificantMass);
            weightedDelta += (working.X - baseline.X) * mass;
            totalMass += mass;
        }

        if (totalMass <= 0)
        {
            return false;
        }

        delta = Math.Clamp(weightedDelta / totalMass, MinDelta, MaxDelta);
        return true;
    }

    public static bool HasEditableBodies(string xml) => CollectEditableBodies(xml).Count > 0;

    public static string ApplyDelta(string workingXml, string baselineXml, double delta)
    {
        var clamped = Math.Clamp(delta, MinDelta, MaxDelta);
        var baselineBodies = CollectEditableBodies(baselineXml);
        if (baselineBodies.Count == 0)
        {
            return workingXml;
        }

        // Rewrite from the end so earlier match indices stay valid.
        var matches = BodyOpenTagRegex.Matches(workingXml);
        var result = workingXml;
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var attrs = match.Groups["attrs"].Value;
            if (!TryParseEditableBody(attrs, out var key, out var currentX, out _, out _, out _))
            {
                continue;
            }

            if (!baselineBodies.TryGetValue(key, out var baseline))
            {
                continue;
            }

            var targetX = baseline.X + clamped;
            if (Math.Abs(currentX - targetX) < 1e-9)
            {
                continue;
            }

            var com = CenterOfMassOffsetRegex.Match(attrs);
            if (!com.Success)
            {
                continue;
            }

            var yRaw = com.Groups["y"].Value.Trim();
            var zRaw = com.Groups["z"].Value.Trim();
            var newAttrs = attrs[..com.Index]
                + BuildCenterOfMassAttribute(
                    com.Value,
                    FormatAxis(targetX),
                    yRaw,
                    zRaw)
                + attrs[(com.Index + com.Length)..];

            result = result[..match.Index]
                + "<Body"
                + newAttrs
                + ">"
                + result[(match.Index + match.Length)..];
        }

        return result;
    }

    public static string FormatDelta(double delta) =>
        FormatAxis(Math.Clamp(delta, MinDelta, MaxDelta));

    private static Dictionary<string, BodyAxes> CollectEditableBodies(string xml)
    {
        var result = new Dictionary<string, BodyAxes>(StringComparer.OrdinalIgnoreCase);
        var rootIndex = 0;
        foreach (Match match in BodyOpenTagRegex.Matches(xml))
        {
            var attrs = match.Groups["attrs"].Value;
            if (!TryParseEditableBody(attrs, out var key, out var x, out var y, out var z, out var mass))
            {
                // Root bodies without ModelFrame still need a stable key.
                if (TryParseSignificantCom(attrs, out x, out y, out z, out mass)
                    && !VehicleGameDataXml.ParseAttributes(attrs).ContainsKey("ModelFrame"))
                {
                    key = $"#root:{rootIndex}";
                    result[key] = new BodyAxes(x, y, z, mass);
                }

                rootIndex++;
                continue;
            }

            result[key] = new BodyAxes(x, y, z, mass);
            rootIndex++;
        }

        return result;
    }

    private static bool TryParseEditableBody(
        string attrs,
        out string key,
        out double x,
        out double y,
        out double z,
        out double mass)
    {
        key = "";
        x = y = z = mass = 0;

        var parsed = VehicleGameDataXml.ParseAttributes(attrs);
        if (!parsed.TryGetValue("ModelFrame", out var frame) || string.IsNullOrWhiteSpace(frame))
        {
            return false;
        }

        if (!IsEditableFrame(frame))
        {
            return false;
        }

        if (!TryParseSignificantCom(attrs, out x, out y, out z, out mass))
        {
            return false;
        }

        key = frame.Trim();
        return true;
    }

    private static bool TryParseSignificantCom(
        string attrs,
        out double x,
        out double y,
        out double z,
        out double mass)
    {
        x = y = z = mass = 0;
        var com = CenterOfMassOffsetRegex.Match(attrs);
        if (!com.Success
            || !TryParseDouble(com.Groups["x"].Value, out x)
            || !TryParseDouble(com.Groups["y"].Value, out y)
            || !TryParseDouble(com.Groups["z"].Value, out z))
        {
            return false;
        }

        var parsed = VehicleGameDataXml.ParseAttributes(attrs);
        parsed.TryGetValue("Mass", out var massRaw);
        if (!TryParseDouble(massRaw, out mass) || mass < MinSignificantMass)
        {
            return false;
        }

        return true;
    }

    private static bool IsEditableFrame(string frame)
    {
        if (ContainsAny(frame, "Ragdoll", "Prismatic", "Sunshield", "Mirror", "Door", "Glass",
                "Key", "Hook", "Wire", "Curtain", "Belt", "Trigger", "Radio"))
        {
            return false;
        }

        return frame.Contains("Cabin", StringComparison.OrdinalIgnoreCase)
            || frame.Contains("Chassis", StringComparison.OrdinalIgnoreCase)
            || frame.Contains("Suspension", StringComparison.OrdinalIgnoreCase)
            || frame.Contains("Body", StringComparison.OrdinalIgnoreCase)
            || frame.Contains("Back", StringComparison.OrdinalIgnoreCase)
            || frame.Contains("Twist", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        foreach (var needle in needles)
        {
            if (value.Contains(needle, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string BuildCenterOfMassAttribute(string originalMatch, string x, string y, string z)
    {
        var eq = originalMatch.IndexOf('=');
        var name = eq > 0 ? originalMatch[..eq] : "CenterOfMassOffset";
        return $"{name}=\"({x}; {y}; {z})\"";
    }

    private static string FormatAxis(double value)
    {
        var rounded = Math.Round(value, 2, MidpointRounding.AwayFromZero);
        if (Math.Abs(rounded) < 1e-9)
        {
            return "0";
        }

        return rounded.ToString("0.##", CultureInfo.InvariantCulture);
    }

    private static bool TryParseDouble(string? raw, out double value) =>
        double.TryParse(
            raw?.Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);

    private readonly record struct BodyAxes(double X, double Y, double Z, double Mass);
}
