using System.Globalization;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Models;

namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>
/// Per-axle <c>SteeringAngle</c> read/write for truck XML.
/// Vanilla-steered axles never lose their attribute; newly enabled axles are rear-only (0…−60).
/// </summary>
public static class TruckSteerXml
{
    public const double GlobalFrontSteerMinimumDegrees = 10;
    public const double GlobalFrontSteerMaximumDegrees = 60;
    public const double GlobalRearSteerMinimumDegrees = -10;
    public const double GlobalRearSteerMaximumDegrees = -60;

    public const double VanillaFrontMinDegrees = 0.01;
    public const double VanillaFrontMaxDegrees = 90;
    public const double VanillaRearMinDegrees = -90;
    public const double VanillaRearMaxDegrees = -0.01;

    public const double AddedRearMinDegrees = -60;
    public const double AddedRearMaxDegrees = 0;

    private static readonly Regex AxleWheelOpenTagRegex = new(
        @"<(?<tag>FrontWheel|RearWheel|FirstAxle|SecondAxle|ThirdAxle|FourthAxle|FrontAxle|RearAxle|MiddleAxle|MiddleWheel)\b(?<attrs>[^>]*)(?<self>/?)>"
        + @"|<(?<tag>Front|Rear)\b(?<attrs>[^>]*\bTorque\s*=\s*""[^""]*""[^>]*)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SteeringAngleAttributeRegex = new(
        @"(?<prefix>\bSteeringAngle\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WheelOpenTagRegex = new(
        @"<Wheel\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static List<TruckSteerAxle> ParseSteerAxles(string currentText, string? baselineText)
    {
        var currentMetas = IndexAnglesList(currentText);
        var baselineMetas = IndexAnglesList(baselineText ?? currentText);
        var forwardByTag = IndexTemplateForwardPositions(currentText);
        var count = currentMetas.Count;
        var result = new List<TruckSteerAxle>(count);

        for (var i = 0; i < count; i++)
        {
            var current = currentMetas[i];
            var baseline = i < baselineMetas.Count ? baselineMetas[i] : null;
            var hadBaseline = baseline?.Angle is not null;
            double? forwardPos = forwardByTag.TryGetValue(current.Tag, out var x) ? x : null;
            result.Add(new TruckSteerAxle
            {
                OccurrenceIndex = i,
                Tag = current.Tag,
                Location = current.Location,
                ForwardPos = forwardPos,
                HadSteerInBaseline = hadBaseline,
                BaselineAngle = baseline?.Angle,
                Angle = current.Angle ?? baseline?.Angle,
            });
        }

        // Display: front → middle → rear; within a group frontmost first (Wheel Pos X, else tag hint).
        // OccurrenceIndex stays file-order for stable XML writes.
        var ordered = result
            .OrderBy(ClassifyAxleGroup)
            .ThenByDescending(axle => axle.ForwardPos ?? TagForwardHint(axle.Tag))
            .ThenBy(axle => axle.OccurrenceIndex)
            .ToList();
        for (var display = 0; display < ordered.Count; display++)
        {
            ordered[display].DisplayOrder = display + 1;
        }

        return ordered;
    }

    /// <summary>
    /// Max longitudinal <c>Pos</c> X among <c>Wheel</c> rows that use this template
    /// (SnowRunner: larger X ≈ further forward).
    /// </summary>
    internal static Dictionary<string, double> IndexTemplateForwardPositions(string text)
    {
        var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in WheelOpenTagRegex.Matches(text))
        {
            var attrs = ParseAttributeMap(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("_template", out var tag) || string.IsNullOrWhiteSpace(tag))
            {
                continue;
            }

            if (!attrs.TryGetValue("Pos", out var pos) || !TryParsePosX(pos, out var x))
            {
                continue;
            }

            if (!result.TryGetValue(tag, out var existing) || x > existing)
            {
                result[tag] = x;
            }
        }

        return result;
    }

    private static bool TryParsePosX(string pos, out double x)
    {
        x = 0;
        // "(2.972; 0.547; 1.02)" or "(2.972, 0.547, 1.02)"
        var start = pos.IndexOf('(');
        var end = pos.IndexOfAny([';', ',', ' '], start + 1);
        if (start < 0 || end <= start + 1)
        {
            return false;
        }

        return double.TryParse(
            pos.AsSpan(start + 1, end - start - 1).Trim(),
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out x);
    }

    private static Dictionary<string, string> ParseAttributeMap(string attrs)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            map[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return map;
    }

    /// <summary>Fallback when Wheel Pos is missing — FirstAxle ahead of SecondAxle, etc.</summary>
    internal static double TagForwardHint(string tag) =>
        tag.ToLowerInvariant() switch
        {
            "frontwheel" or "frontaxle" or "front" or "firstaxle" => 4,
            "secondaxle" => 3,
            "thirdaxle" or "middleaxle" or "middlewheel" => 2,
            "fourthaxle" => 1,
            "rearwheel" or "rearaxle" or "rear" => 0,
            _ => 0,
        };

    /// <summary>0 = front, 1 = middle, 2 = rear.</summary>
    internal static int ClassifyAxleGroup(TruckSteerAxle axle)
    {
        var tag = axle.Tag;
        var location = axle.Location ?? "";

        // Tag Middle* wins over Location="rear" (common on mid axles).
        if (tag.Equals("MiddleAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("MiddleWheel", StringComparison.OrdinalIgnoreCase)
            || location.StartsWith("middle", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        if (location.StartsWith("front", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FrontWheel", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FrontAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FirstAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("Front", StringComparison.OrdinalIgnoreCase))
        {
            return 0;
        }

        if (location.StartsWith("rear", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("RearWheel", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("RearAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("Rear", StringComparison.OrdinalIgnoreCase))
        {
            return 2;
        }

        // Fallback: positive baseline angle → front-ish, negative → rear, else middle
        return axle.BaselineAngle switch
        {
            > 0 => 0,
            < 0 => 2,
            _ => 1,
        };
    }

    public static string ApplySteerAxles(string text, IReadOnlyList<TruckSteerAxle> axles)
    {
        ArgumentNullException.ThrowIfNull(axles);
        if (axles.Count == 0)
        {
            return text;
        }

        var byIndex = axles.ToDictionary(axle => axle.OccurrenceIndex);
        var index = 0;
        return AxleWheelOpenTagRegex.Replace(text, match =>
        {
            var occurrence = index++;
            if (!byIndex.TryGetValue(occurrence, out var axle))
            {
                return match.Value;
            }

            return ApplyAxleMatch(match, axle);
        });
    }

    public static string ApplyGlobalSteerPresets(
        string text,
        TruckFrontSteerGlobalMode frontMode,
        TruckRearSteerGlobalMode rearMode)
    {
        if (frontMode == TruckFrontSteerGlobalMode.Baseline
            && rearMode == TruckRearSteerGlobalMode.Baseline)
        {
            return text;
        }

        // Global presets always operate relative to the text passed in (baseline when used from ApplyGlobalMultipliers).
        var axles = ParseSteerAxles(text, text);
        foreach (var axle in axles)
        {
            if (!axle.HadSteerInBaseline || axle.BaselineAngle is not { } baseline)
            {
                continue;
            }

            if (baseline > 0 && frontMode != TruckFrontSteerGlobalMode.Baseline)
            {
                axle.Angle = frontMode == TruckFrontSteerGlobalMode.Minimum
                    ? GlobalFrontSteerMinimumDegrees
                    : GlobalFrontSteerMaximumDegrees;
            }
            else if (baseline < 0 && rearMode != TruckRearSteerGlobalMode.Baseline)
            {
                axle.Angle = rearMode == TruckRearSteerGlobalMode.Minimum
                    ? GlobalRearSteerMinimumDegrees
                    : GlobalRearSteerMaximumDegrees;
            }
        }

        return ApplySteerAxles(text, axles);
    }

    public static bool HasBaselineFrontSteer(IEnumerable<TruckSteerAxle> axles) =>
        axles.Any(axle => axle.HadSteerInBaseline && axle.BaselineAngle is > 0);

    public static bool HasBaselineRearSteer(IEnumerable<TruckSteerAxle> axles) =>
        axles.Any(axle => axle.HadSteerInBaseline && axle.BaselineAngle is < 0);

    public static string FormatSteerAngle(double value) =>
        Xml.XmlNumericFormatting.Format(value, preferInteger: false);

    private static string ApplyAxleMatch(Match match, TruckSteerAxle axle)
    {
        var attrs = match.Groups["attrs"].Value;
        var hasAttr = TryReadSteeringAngle(attrs, out _);

        if (axle.HadSteerInBaseline)
        {
            var target = ResolveVanillaTarget(axle);
            var formatted = FormatSteerAngle(target);
            if (hasAttr)
            {
                attrs = ReplaceSteeringAngle(attrs, formatted);
            }
            else
            {
                attrs = AppendSteeringAngle(attrs, formatted);
            }
        }
        else
        {
            if (IsEmptyOrZero(axle.Angle))
            {
                if (!hasAttr)
                {
                    return match.Value;
                }

                attrs = RemoveSteeringAngle(attrs);
            }
            else
            {
                var target = Math.Clamp(axle.Angle!.Value, AddedRearMinDegrees, AddedRearMaxDegrees);
                if (target >= 0)
                {
                    // Positive not allowed on newly added axles — treat as clear.
                    if (!hasAttr)
                    {
                        return match.Value;
                    }

                    attrs = RemoveSteeringAngle(attrs);
                }
                else
                {
                    var formatted = FormatSteerAngle(target);
                    attrs = hasAttr ? ReplaceSteeringAngle(attrs, formatted) : AppendSteeringAngle(attrs, formatted);
                }
            }
        }

        return $"<{match.Groups["tag"].Value}{attrs}{match.Groups["self"].Value}>";
    }

    private static double ResolveVanillaTarget(TruckSteerAxle axle)
    {
        if (IsEmptyOrZero(axle.Angle))
        {
            return axle.BaselineAngle ?? 0;
        }

        var value = axle.Angle!.Value;
        if (axle.BaselineAngle is > 0)
        {
            return Math.Clamp(value, VanillaFrontMinDegrees, VanillaFrontMaxDegrees);
        }

        if (axle.BaselineAngle is < 0)
        {
            return Math.Clamp(value, VanillaRearMinDegrees, VanillaRearMaxDegrees);
        }

        return value;
    }

    private static bool IsEmptyOrZero(double? angle) =>
        angle is null || Math.Abs(angle.Value) < 1e-12;

    private static List<AxleMeta> IndexAnglesList(string text)
    {
        var list = new List<AxleMeta>();
        foreach (Match match in AxleWheelOpenTagRegex.Matches(text))
        {
            var attrs = match.Groups["attrs"].Value;
            double? angle = null;
            if (TryReadSteeringAngle(attrs, out var parsed))
            {
                angle = parsed;
            }

            list.Add(new AxleMeta(
                match.Groups["tag"].Value,
                GetAttribute(attrs, "Location"),
                angle));
        }

        return list;
    }

    private static bool TryReadSteeringAngle(string attrs, out double value)
    {
        value = 0;
        var match = SteeringAngleAttributeRegex.Match(attrs);
        if (!match.Success)
        {
            return false;
        }

        return double.TryParse(
            match.Groups["value"].Value,
            NumberStyles.Float,
            CultureInfo.InvariantCulture,
            out value);
    }

    private static string ReplaceSteeringAngle(string attrs, string formatted) =>
        SteeringAngleAttributeRegex.Replace(
            attrs,
            m => $"{m.Groups["prefix"].Value}{formatted}{m.Groups["suffix"].Value}",
            1);

    private static string AppendSteeringAngle(string attrs, string formatted)
    {
        if (attrs.Length > 0 && !char.IsWhiteSpace(attrs[^1]))
        {
            return attrs + $" SteeringAngle=\"{formatted}\"";
        }

        return attrs + $"SteeringAngle=\"{formatted}\" ";
    }

    private static string RemoveSteeringAngle(string attrs)
    {
        var updated = SteeringAngleAttributeRegex.Replace(attrs, "", 1);
        return Regex.Replace(updated, @"[ \t]{2,}", " ");
    }

    private static string GetAttribute(string attrs, string name)
    {
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            if (match.Groups["name"].Value.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return match.Groups["value"].Value;
            }
        }

        return "";
    }

    private sealed record AxleMeta(string Tag, string Location, double? Angle);
}
