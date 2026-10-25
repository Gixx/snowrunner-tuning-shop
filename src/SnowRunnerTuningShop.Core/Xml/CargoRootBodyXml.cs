using System.Globalization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Read/write root PhysicsModel Body <c>CenterOfMassOffset</c> Y and <c>Friction</c> on cargo addons.
/// </summary>
public static class CargoRootBodyXml
{
    /// <summary>Global Low endpoint — lower CoM, more stable.</summary>
    public const double MinY = -0.6;

    /// <summary>Global High endpoint — higher CoM, tippier.</summary>
    public const double MaxY = 0.3;

    public const double MinFriction = 0;
    public const double MaxFriction = 10;

    private static readonly Regex PhysicsModelRootBodyRegex = new(
        @"<PhysicsModel\b[^>]*>\s*(?:<NetSync\b[^>]*/>\s*)*<Body\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex CenterOfMassOffsetRegex = new(
        @"\bCenterOfMassOffset\s*=\s*""\((?<x>[^;]*);\s*(?<y>[^;]*);\s*(?<z>[^)]*)\)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex FrictionRegex = new(
        @"(?<prefix>\bFriction\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool TryReadCenterOfMassY(string text, out double y)
    {
        y = 0;
        if (!TryGetRootBody(text, out _, out var attrs))
        {
            return false;
        }

        var com = CenterOfMassOffsetRegex.Match(attrs);
        return com.Success && TryParseDouble(com.Groups["y"].Value, out y);
    }

    public static string ApplyCenterOfMassY(string text, double y)
    {
        var clamped = Math.Clamp(y, MinY, MaxY);
        if (!TryGetRootBody(text, out var match, out var attrs))
        {
            return text;
        }

        var com = CenterOfMassOffsetRegex.Match(attrs);
        if (!com.Success)
        {
            return text;
        }

        if (!TryParseDouble(com.Groups["y"].Value, out var currentY)
            || Math.Abs(currentY - clamped) < 1e-9)
        {
            return text;
        }

        var xRaw = com.Groups["x"].Value.Trim();
        var zRaw = com.Groups["z"].Value.Trim();
        var yRaw = XmlNumericFormatting.Format(clamped);
        var eq = com.Value.IndexOf('=');
        var name = eq > 0 ? com.Value[..eq] : "CenterOfMassOffset";
        var replacement = $"{name}=\"({xRaw}; {yRaw}; {zRaw})\"";
        var newAttrs = attrs[..com.Index] + replacement + attrs[(com.Index + com.Length)..];
        return ReplaceRootBodyAttrs(text, match.Groups["attrs"], newAttrs);
    }

    public static bool TryReadFriction(string text, out double friction)
    {
        friction = 0;
        if (!TryGetRootBody(text, out _, out var attrs))
        {
            return false;
        }

        var match = FrictionRegex.Match(attrs);
        return match.Success && TryParseDouble(match.Groups["value"].Value, out friction);
    }

    public static string ApplyFriction(string text, double friction)
    {
        var clamped = Math.Clamp(friction, MinFriction, MaxFriction);
        if (!TryGetRootBody(text, out var match, out var attrs))
        {
            return text;
        }

        var frictionMatch = FrictionRegex.Match(attrs);
        if (!frictionMatch.Success)
        {
            return text;
        }

        if (!TryParseDouble(frictionMatch.Groups["value"].Value, out var current)
            || Math.Abs(current - clamped) < 1e-9)
        {
            return text;
        }

        var formatted = XmlNumericFormatting.Format(clamped);
        var replacement =
            frictionMatch.Groups["prefix"].Value
            + formatted
            + frictionMatch.Groups["suffix"].Value;
        var newAttrs = attrs[..frictionMatch.Index]
            + replacement
            + attrs[(frictionMatch.Index + frictionMatch.Length)..];
        return ReplaceRootBodyAttrs(text, match.Groups["attrs"], newAttrs);
    }

    /// <summary>
    /// Remap baseline Y toward MinY (Low) or MaxY (High). Bias index 0=Low … 2=Baseline … 4=High.
    /// </summary>
    public static double ResolveBiasedY(double baselineY, int biasIndex)
    {
        var index = Math.Clamp(biasIndex, 0, 4);
        if (index == 2)
        {
            return Math.Clamp(baselineY, MinY, MaxY);
        }

        var t = (index - 2) / 2.0;
        if (t < 0)
        {
            return Lerp(baselineY, MinY, -t);
        }

        return Lerp(baselineY, MaxY, t);
    }

    private static bool TryGetRootBody(string text, out Match match, out string attrs)
    {
        match = PhysicsModelRootBodyRegex.Match(text);
        attrs = match.Success ? match.Groups["attrs"].Value : "";
        return match.Success;
    }

    private static string ReplaceRootBodyAttrs(string text, Group attrsGroup, string newAttrs) =>
        text[..attrsGroup.Index] + newAttrs + text[(attrsGroup.Index + attrsGroup.Length)..];

    private static double Lerp(double a, double b, double t) =>
        a + ((b - a) * Math.Clamp(t, 0, 1));

    private static bool TryParseDouble(string raw, out double value) =>
        double.TryParse(raw.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}
