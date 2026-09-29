using System.Globalization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Read/write cabin <c>CenterOfMassOffset</c> Y on truck <c>PhysicsModel</c> bodies.
/// Prefers <c>BoneCabin_cdt</c>; otherwise the heaviest body whose <c>ModelFrame</c> contains Cabin
/// (excluding ragdoll / prismatic / sunshield / mirror frames).
/// </summary>
public static class VehicleCabinCenterOfMassXml
{
    /// <summary>Slider Low endpoint — lower CoM, more stable.</summary>
    public const double MinY = -1.5;

    /// <summary>Slider High endpoint — higher CoM, tippier.</summary>
    public const double MaxY = 1.0;

    private static readonly Regex BodyOpenTagRegex = new(
        @"<Body\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CenterOfMassOffsetRegex = new(
        @"\bCenterOfMassOffset\s*=\s*""\((?<x>[^;]*);\s*(?<y>[^;]*);\s*(?<z>[^)]*)\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryReadCabinY(string text, out double y)
    {
        y = 0;
        if (!TryFindCabinBody(text, out _, out _, out y))
        {
            return false;
        }

        return true;
    }

    public static string ApplyCabinY(string text, double y)
    {
        var clamped = Math.Clamp(y, MinY, MaxY);
        if (!TryFindCabinBody(text, out var match, out var attrs, out var currentY))
        {
            return text;
        }

        if (Math.Abs(currentY - clamped) < 1e-9)
        {
            return text;
        }

        var com = CenterOfMassOffsetRegex.Match(attrs);
        if (!com.Success)
        {
            return text;
        }

        var xRaw = com.Groups["x"].Value.Trim();
        var zRaw = com.Groups["z"].Value.Trim();
        var yRaw = FormatAxis(clamped);
        var newAttrs = attrs[..com.Index]
            + BuildCenterOfMassAttribute(com.Value, xRaw, yRaw, zRaw)
            + attrs[(com.Index + com.Length)..];

        return text[..match.Index]
            + "<Body"
            + newAttrs
            + ">"
            + text[(match.Index + match.Length)..];
    }

    public static string FormatY(double y) => FormatAxis(Math.Clamp(y, MinY, MaxY));

    private static string BuildCenterOfMassAttribute(string originalMatch, string x, string y, string z)
    {
        // Keep original attribute name / quoting style; only swap the vector.
        var eq = originalMatch.IndexOf('=');
        var name = eq > 0 ? originalMatch[..eq] : "CenterOfMassOffset";
        return $"{name}=\"({x}; {y}; {z})\"";
    }

    private static bool TryFindCabinBody(
        string text,
        out Match bodyMatch,
        out string attrs,
        out double y)
    {
        bodyMatch = Match.Empty;
        attrs = "";
        y = 0;

        Match? best = null;
        var bestScore = double.NegativeInfinity;
        var bestY = 0.0;
        var bestAttrs = "";

        foreach (Match match in BodyOpenTagRegex.Matches(text))
        {
            var bodyAttrs = match.Groups["attrs"].Value;
            if (!TryScoreCabinBody(bodyAttrs, out var score, out var bodyY))
            {
                continue;
            }

            if (score > bestScore)
            {
                bestScore = score;
                best = match;
                bestY = bodyY;
                bestAttrs = bodyAttrs;
            }
        }

        if (best is null)
        {
            return false;
        }

        bodyMatch = best;
        attrs = bestAttrs;
        y = bestY;
        return true;
    }

    private static bool TryScoreCabinBody(string attrs, out double score, out double y)
    {
        score = 0;
        y = 0;

        var parsed = VehicleGameDataXml.ParseAttributes(attrs);
        if (!parsed.TryGetValue("ModelFrame", out var frame) || string.IsNullOrWhiteSpace(frame))
        {
            return false;
        }

        if (!IsCabinFrame(frame))
        {
            return false;
        }

        var com = CenterOfMassOffsetRegex.Match(attrs);
        if (!com.Success
            || !TryParseDouble(com.Groups["y"].Value, out y))
        {
            return false;
        }

        parsed.TryGetValue("Mass", out var massRaw);
        TryParseDouble(massRaw, out var mass);

        // Prefer the canonical cabin bone, then any other cabin-ish frame by mass.
        score = frame.Equals("BoneCabin_cdt", StringComparison.OrdinalIgnoreCase)
            ? 1_000_000 + mass
            : 1_000 + mass;
        return true;
    }

    private static bool IsCabinFrame(string frame)
    {
        if (frame.Contains("Cabin", StringComparison.OrdinalIgnoreCase) is false)
        {
            return false;
        }

        // Exclude auxiliary cabin-adjacent bones; Twist is only used if no BoneCabin_cdt wins.
        if (ContainsAny(frame, "Ragdoll", "Prismatic", "Sunshield", "Mirror", "Door", "Glass"))
        {
            return false;
        }

        return true;
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
}
