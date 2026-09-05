using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.Tires;

/// <summary>
/// Resolves WheelFriction _template values from [media]/_templates/trucks.xml.
/// Game UI maps: On-road = BodyFrictionAsphalt, Off-road = BodyFriction, Mud = SubstanceFriction.
/// </summary>
public static class WheelFrictionTemplates
{
    private static readonly Regex FrictionTemplateRegex = new(
        @"<(?<name>ScoutOffroad|ScoutMudtires|ScoutHighway|ScoutChains|ScoutAllterrain|Offroad|Mudtires|Highway|HeavyMudtires|Chains|Allterrain)\b(?<attrs>[^<>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public readonly record struct FrictionValues(
        double BodyFriction,
        double BodyFrictionAsphalt,
        double SubstanceFriction,
        bool IsIgnoreIce);

    public static IReadOnlyDictionary<string, FrictionValues> LoadFromPak(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var entry = PakEntryLocator.FindEntry(archive, "[media]/_templates/trucks.xml")
            ?? archive.Entries.FirstOrDefault(candidate =>
                PakEntryLocator.NormalizeEntryPath(candidate.FullName)
                    .EndsWith("/_templates/trucks.xml", StringComparison.OrdinalIgnoreCase));

        if (entry is null)
        {
            return new Dictionary<string, FrictionValues>(StringComparer.OrdinalIgnoreCase);
        }

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();

        // Prefer the dedicated <WheelFriction> ... </WheelFriction> section when present.
        var sectionMatch = Regex.Match(
            text,
            @"(?is)<WheelFriction\b[^<>]*>(?<body>.*?)</WheelFriction>");
        var searchText = sectionMatch.Success ? sectionMatch.Groups["body"].Value : text;

        var result = new Dictionary<string, FrictionValues>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in FrictionTemplateRegex.Matches(searchText))
        {
            var name = match.Groups["name"].Value;
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.ContainsKey("BodyFriction")
                && !attrs.ContainsKey("BodyFrictionAsphalt")
                && !attrs.ContainsKey("SubstanceFriction"))
            {
                continue;
            }

            result[name] = new FrictionValues(
                ParseDouble(attrs.GetValueOrDefault("BodyFriction"), 0),
                ParseDouble(attrs.GetValueOrDefault("BodyFrictionAsphalt"), 0),
                ParseDouble(attrs.GetValueOrDefault("SubstanceFriction"), 0),
                ParseBool(attrs.GetValueOrDefault("IsIgnoreIce")));
        }

        return result;
    }

    public static FrictionValues Resolve(
        IReadOnlyDictionary<string, FrictionValues> templates,
        string? templateName,
        IReadOnlyDictionary<string, string> explicitAttrs)
    {
        var baseline = default(FrictionValues);
        if (!string.IsNullOrWhiteSpace(templateName))
        {
            templates.TryGetValue(templateName, out baseline);
        }

        var body = explicitAttrs.TryGetValue("BodyFriction", out var bodyRaw)
            ? ParseDouble(bodyRaw, 0)
            : baseline.BodyFriction;
        var asphalt = explicitAttrs.TryGetValue("BodyFrictionAsphalt", out var asphaltRaw)
            ? ParseDouble(asphaltRaw, 0)
            : baseline.BodyFrictionAsphalt;
        var substance = explicitAttrs.TryGetValue("SubstanceFriction", out var substanceRaw)
            ? ParseDouble(substanceRaw, 0)
            : baseline.SubstanceFriction;
        var ignoreIce = explicitAttrs.TryGetValue("IsIgnoreIce", out var ignoreRaw)
            ? ParseBool(ignoreRaw)
            : baseline.IsIgnoreIce;

        return new FrictionValues(body, asphalt, substance, ignoreIce);
    }

    private static Dictionary<string, string> ParseAttributes(string attributesText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributesText))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static bool ParseBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed;
}
