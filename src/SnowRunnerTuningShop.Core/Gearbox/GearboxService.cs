using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;
using SnowRunnerTuningShop.Core.Localization;

namespace SnowRunnerTuningShop.Core.Gearbox;

public static class GearboxService
{
    /// <summary>Saber min for Gear / HighGear / ReverseGear AngVel.</summary>
    public const double MinAngVel = 0.1;

    /// <summary>Saber max for Gear / HighGear / ReverseGear AngVel.</summary>
    public const double MaxAngVel = 32.0;

    // Attrs must not accept '<' or the match can swallow the next tag (issue #6).
    private static readonly Regex GearboxOpenTagRegex = new(
        @"<Gearbox\b(?<attrs>[^<>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Longer names first; Gear\b still rejects Gearbox / GearboxParams.
    private static readonly Regex GearSpeedTagRegex = new(
        @"<(?<tag>HighGear|ReverseGear|Gear)\b(?<attrs>[^<>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GearboxSocketRegex = new(
        @"<GearboxSocket\b(?<attrs>[^<>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<GearboxDefinition> LoadGearboxes(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        var setUsage = BuildGearboxSetUsage(archive, strings);
        var gearboxes = new List<GearboxDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsGearboxEntry(entryPath))
            {
                continue;
            }

            gearboxes.AddRange(ParseGearboxesFromText(entryPath, PartXmlHelpers.ReadEntryUtf8(entry), strings, setUsage));
        }

        return gearboxes
            .OrderBy(gearbox => gearbox.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gearbox => gearbox.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(gearbox => gearbox.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static GearboxSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double fuelConsumptionMultiplier,
        double idleFuelModifierMultiplier,
        double awdConsumptionMultiplier)
    {
        PartPakPipeline.ValidateMultiplier(fuelConsumptionMultiplier, nameof(fuelConsumptionMultiplier));
        PartPakPipeline.ValidateMultiplier(idleFuelModifierMultiplier, nameof(idleFuelModifierMultiplier));
        PartPakPipeline.ValidateMultiplier(awdConsumptionMultiplier, nameof(awdConsumptionMultiplier));

        var result = PartPakPipeline.BuildBaselineReplacements(
            pakPath,
            IsGearboxEntry,
            (_, baselineText, _) => ApplyMultipliersToText(
                baselineText,
                fuelConsumptionMultiplier,
                idleFuelModifierMultiplier,
                awdConsumptionMultiplier),
            (_, currentText, updatedText) => CountNamedDifferences(currentText, updatedText));

        var updatedFiles = PartPakPipeline.CommitReplacements(pakPath, result.Replacements);
        return new GearboxSaveResult(updatedFiles, result.ChangedItems);
    }

    public static GearboxSaveResult RestoreGearboxesFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, 1.0);

    public static GearboxSaveResult SaveGearboxChanges(string pakPath, IReadOnlyList<GearboxDefinition> gearboxes)
    {
        ArgumentNullException.ThrowIfNull(gearboxes);

        Dictionary<string, byte[]> replacements;
        var changedGearboxes = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var grouped = gearboxes
                .GroupBy(gearbox => gearbox.EntryPath.Replace('\\', '/'), StringComparer.Ordinal)
                .ToArray();

            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var entry = PakEntryLocator.FindEntry(archive, group.Key);
                if (entry is null)
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                var updates = group.ToDictionary(
                    gearbox => gearbox.Name,
                    gearbox => new GearboxAttributeValues(
                        gearbox.Price,
                        gearbox.DamageCapacity,
                        gearbox.FuelConsumption,
                        gearbox.IdleFuelModifier,
                        gearbox.AwdConsumptionModifier,
                        gearbox.MaxGearAngVel,
                        gearbox.HighGearAngVel,
                        gearbox.ReverseGearAngVel),
                    StringComparer.OrdinalIgnoreCase);

                if (!TryApplyUpdatesToText(text, updates, out var updatedText, out var fileChanged))
                {
                    continue;
                }

                changedGearboxes += fileChanged;
                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[group.Key] = Encoding.UTF8.GetBytes(updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new GearboxSaveResult(updatedFiles, changedGearboxes);
    }

    private static bool IsGearboxEntry(string entryPath) =>
        entryPath.Contains("/classes/gearboxes/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruckEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/cargo/", StringComparison.OrdinalIgnoreCase);

    private static List<GearboxDefinition> ParseGearboxesFromText(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? setUsage = null)
    {
        var gearboxes = new List<GearboxDefinition>();
        if (!content.Contains("<Gearbox", StringComparison.OrdinalIgnoreCase))
        {
            return gearboxes;
        }

        var normalizedPath = entryPath.Replace('\\', '/');
        var sourceFile = Path.GetFileName(normalizedPath);
        var setId = Path.GetFileNameWithoutExtension(normalizedPath);
        var setName = setId.StartsWith("gearboxes_", StringComparison.OrdinalIgnoreCase)
            ? setId["gearboxes_".Length..]
            : setId;
        var usedByNames = setUsage is not null && setUsage.TryGetValue(setId, out var names)
            ? names
            : Array.Empty<string>();

        foreach (Match match in GearboxOpenTagRegex.Matches(content))
        {
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var uiNameKey = ExtractUiNameKeyFromBlock(content, match.Index);
            var hasAwd = attrs.ContainsKey("AWDConsumptionModifier");
            var blockEnd = IndexOfNextElementOpenTag(content, match.Index + 1, "Gearbox");
            if (blockEnd < 0)
            {
                blockEnd = content.Length;
            }

            var block = content[match.Index..blockEnd];
            var (maxGearAngVel, highGearAngVel, reverseGearAngVel) = ExtractAngVels(block);
            gearboxes.Add(new GearboxDefinition
            {
                EntryPath = entryPath,
                Name = name,
                UiNameKey = uiNameKey ?? "",
                DisplayName = strings is null
                    ? name
                    : GameStringsReader.Resolve(strings, uiNameKey ?? "", name),
                SourceFile = sourceFile,
                SetId = setId,
                SetName = setName,
                UsedBy = FormatUsedBy(usedByNames),
                UsedByTooltip = FormatUsedByTooltip(usedByNames),
                Category = InferCategory(entryPath),
                Price = PartXmlHelpers.ExtractPrice(block),
                DamageCapacity = ParseDouble(attrs.GetValueOrDefault("DamageCapacity"), 0),
                FuelConsumption = ParseDouble(attrs.GetValueOrDefault("FuelConsumption"), 0),
                IdleFuelModifier = ParseDouble(attrs.GetValueOrDefault("IdleFuelModifier"), 0),
                AwdConsumptionModifier = hasAwd
                    ? ParseDouble(attrs["AWDConsumptionModifier"], 0)
                    : null,
                MaxGearAngVel = maxGearAngVel,
                HighGearAngVel = highGearAngVel,
                ReverseGearAngVel = reverseGearAngVel,
            });
        }

        return gearboxes;
    }

    private static (double? MaxGear, double? HighGear, double? ReverseGear) ExtractAngVels(string block)
    {
        double? maxGear = null;
        double? highGear = null;
        double? reverseGear = null;

        foreach (Match match in GearSpeedTagRegex.Matches(block))
        {
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("AngVel", out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            var angVel = ParseDouble(raw, 0);
            var tag = match.Groups["tag"].Value;
            if (tag.Equals("Gear", StringComparison.OrdinalIgnoreCase))
            {
                maxGear = maxGear is null ? angVel : Math.Max(maxGear.Value, angVel);
            }
            else if (tag.Equals("HighGear", StringComparison.OrdinalIgnoreCase))
            {
                highGear = angVel;
            }
            else if (tag.Equals("ReverseGear", StringComparison.OrdinalIgnoreCase))
            {
                reverseGear = angVel;
            }
        }

        return (maxGear, highGear, reverseGear);
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildGearboxSetUsage(
        ZipArchive archive,
        IReadOnlyDictionary<string, string> strings)
    {
        var setToTruckIds = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        var truckDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsTruckEntry(entryPath))
            {
                continue;
            }

            var truckId = Path.GetFileNameWithoutExtension(entryPath);
            var text = PartXmlHelpers.ReadEntryUtf8(entry);
            truckDisplayNames[truckId] = ResolveTruckDisplayName(truckId, text, strings);

            foreach (Match socketMatch in GearboxSocketRegex.Matches(text))
            {
                var attrs = ParseAttributes(socketMatch.Groups["attrs"].Value);
                if (!attrs.TryGetValue("Type", out var typeAttr) || string.IsNullOrWhiteSpace(typeAttr))
                {
                    continue;
                }

                foreach (var part in typeAttr.Split(','))
                {
                    var setId = part.Trim();
                    if (setId.Length == 0)
                    {
                        continue;
                    }

                    if (!setToTruckIds.TryGetValue(setId, out var trucks))
                    {
                        trucks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        setToTruckIds[setId] = trucks;
                    }

                    trucks.Add(truckId);
                }
            }
        }

        var result = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (setId, truckIds) in setToTruckIds)
        {
            result[setId] = truckIds
                .Select(id => truckDisplayNames.TryGetValue(id, out var display) ? display : id)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        return result;
    }

    private static string ResolveTruckDisplayName(
        string truckId,
        string truckXml,
        IReadOnlyDictionary<string, string> strings)
    {
        var match = VehicleUiNameRegex.Match(truckXml);
        if (match.Success)
        {
            return GameStringsReader.Resolve(strings, match.Groups["value"].Value, truckId);
        }

        return truckId;
    }

    private static string FormatUsedBy(IReadOnlyList<string> vehicleNames)
    {
        if (vehicleNames.Count == 0)
        {
            return "—";
        }

        if (vehicleNames.Count <= 3)
        {
            return string.Join(", ", vehicleNames);
        }

        return $"{string.Join(", ", vehicleNames.Take(3))} (+{vehicleNames.Count - 3})";
    }

    private static string FormatUsedByTooltip(IReadOnlyList<string> vehicleNames) =>
        vehicleNames.Count == 0
            ? PartUsageMessages.NoTrucksGearboxSet
            : string.Join(", ", vehicleNames);

    internal static string ApplyMultipliersToTextForTests(
        string baselineText,
        double fuelConsumptionMultiplier,
        double idleFuelModifierMultiplier,
        double awdConsumptionMultiplier) =>
        ApplyMultipliersToText(
            baselineText,
            fuelConsumptionMultiplier,
            idleFuelModifierMultiplier,
            awdConsumptionMultiplier);

    private static string ApplyMultipliersToText(
        string baselineText,
        double fuelConsumptionMultiplier,
        double idleFuelModifierMultiplier,
        double awdConsumptionMultiplier)
    {
        var fuelBaseline = TuningMultiplierPresets.IsBaselineMultiplier(fuelConsumptionMultiplier);
        var idleBaseline = TuningMultiplierPresets.IsBaselineMultiplier(idleFuelModifierMultiplier);
        var awdBaseline = TuningMultiplierPresets.IsBaselineMultiplier(awdConsumptionMultiplier);

        if (fuelBaseline && idleBaseline && awdBaseline)
        {
            return baselineText;
        }

        return GearboxOpenTagRegex.Replace(baselineText, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var parsed = ParseAttributes(attrs);
            if (!parsed.ContainsKey("Name"))
            {
                return match.Value;
            }

            var updatedAttrs = attrs;
            var changed = false;

            if (!fuelBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "FuelConsumption", fuelConsumptionMultiplier);
            }

            if (!idleBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "IdleFuelModifier", idleFuelModifierMultiplier);
            }

            if (!awdBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "AWDConsumptionModifier", awdConsumptionMultiplier);
            }

            if (!changed)
            {
                return match.Value;
            }

            return $"<Gearbox{updatedAttrs}{self}>";
        });
    }

    private static bool TryApplyUpdatesToText(
        string content,
        IReadOnlyDictionary<string, GearboxAttributeValues> updates,
        out string updatedText,
        out int changedGearboxes)
    {
        updatedText = content;
        changedGearboxes = 0;
        if (updates.Count == 0)
        {
            return false;
        }

        var matches = GearboxOpenTagRegex.Matches(content);
        if (matches.Count == 0)
        {
            return false;
        }

        var builder = new StringBuilder(content.Length);
        var lastIndex = 0;
        var localChanged = 0;

        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            var blockEnd = IndexOfNextElementOpenTag(content, match.Index + 1, "Gearbox");
            if (blockEnd < 0)
            {
                blockEnd = content.Length;
            }

            var block = content[match.Index..blockEnd];
            builder.Append(content, lastIndex, match.Index - lastIndex);

            if (attrs.TryGetValue("Name", out var name)
                && !string.IsNullOrWhiteSpace(name)
                && updates.TryGetValue(name, out var target)
                && TryApplyUpdatesToGearboxBlock(block, target, out var updatedBlock))
            {
                builder.Append(updatedBlock);
                localChanged++;
            }
            else
            {
                builder.Append(block);
            }

            lastIndex = blockEnd;
        }

        builder.Append(content, lastIndex, content.Length - lastIndex);

        if (localChanged == 0)
        {
            return false;
        }

        updatedText = builder.ToString();
        changedGearboxes = localChanged;
        return true;
    }

    private static bool TryApplyUpdatesToGearboxBlock(
        string block,
        GearboxAttributeValues target,
        out string updatedBlock)
    {
        updatedBlock = block;
        var changed = false;

        updatedBlock = GearboxOpenTagRegex.Replace(updatedBlock, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var updatedAttrs = attrs;
            var localChanged = false;
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "DamageCapacity", XmlNumericFormatting.Format(target.DamageCapacity));
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "FuelConsumption", XmlNumericFormatting.Format(target.FuelConsumption));
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "IdleFuelModifier", XmlNumericFormatting.Format(target.IdleFuelModifier));

            if (target.AwdConsumptionModifier.HasValue || AttributeExists(updatedAttrs, "AWDConsumptionModifier"))
            {
                localChanged |= SetOrReplaceAttribute(
                    ref updatedAttrs,
                    "AWDConsumptionModifier",
                    XmlNumericFormatting.Format(target.AwdConsumptionModifier ?? 0));
            }

            if (!localChanged)
            {
                return match.Value;
            }

            changed = true;
            return $"<Gearbox{updatedAttrs}{self}>";
        }, 1);

        changed |= PartXmlHelpers.TrySetPrice(ref updatedBlock, target.Price);
        changed |= TryApplyAngVelUpdates(ref updatedBlock, target);
        return changed;
    }

    private static bool TryApplyAngVelUpdates(ref string block, GearboxAttributeValues target)
    {
        var changed = false;
        changed |= TryScaleGearAngVels(ref block, target.MaxGearAngVel);
        changed |= TrySetSingleGearAngVel(ref block, "HighGear", target.HighGearAngVel);
        changed |= TrySetSingleGearAngVel(ref block, "ReverseGear", target.ReverseGearAngVel);
        return changed;
    }

    private static bool TryScaleGearAngVels(ref string block, double? targetMax)
    {
        if (targetMax is null)
        {
            return false;
        }

        var clampedTarget = ClampAngVel(targetMax.Value);
        var gearMatches = new List<(Match Match, double AngVel)>();
        foreach (Match match in GearSpeedTagRegex.Matches(block))
        {
            if (!match.Groups["tag"].Value.Equals("Gear", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("AngVel", out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                continue;
            }

            gearMatches.Add((match, ParseDouble(raw, 0)));
        }

        if (gearMatches.Count == 0)
        {
            return false;
        }

        var currentMax = gearMatches.Max(item => item.AngVel);
        if (Math.Abs(currentMax - clampedTarget) <= 1e-6)
        {
            return false;
        }

        var scale = currentMax > 1e-9 ? clampedTarget / currentMax : 1.0;
        var changed = false;
        for (var i = gearMatches.Count - 1; i >= 0; i--)
        {
            var (match, angVel) = gearMatches[i];
            var next = currentMax > 1e-9
                ? ClampAngVel(angVel * scale)
                : clampedTarget;
            if (Math.Abs(next - angVel) <= 1e-6)
            {
                continue;
            }

            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            if (!SetOrReplaceAttribute(ref attrs, "AngVel", XmlNumericFormatting.Format(next)))
            {
                continue;
            }

            var replacement = $"<{match.Groups["tag"].Value}{attrs}{self}>";
            block = string.Concat(
                block.AsSpan(0, match.Index),
                replacement,
                block.AsSpan(match.Index + match.Length));
            changed = true;
        }

        return changed;
    }

    private static bool TrySetSingleGearAngVel(ref string block, string tagName, double? targetAngVel)
    {
        if (targetAngVel is null)
        {
            return false;
        }

        var clamped = ClampAngVel(targetAngVel.Value);
        Match? targetMatch = null;
        foreach (Match match in GearSpeedTagRegex.Matches(block))
        {
            if (match.Groups["tag"].Value.Equals(tagName, StringComparison.OrdinalIgnoreCase))
            {
                targetMatch = match;
                break;
            }
        }

        if (targetMatch is null)
        {
            return false;
        }

        var attrs = targetMatch.Groups["attrs"].Value;
        var self = targetMatch.Groups["self"].Value;
        var parsed = ParseAttributes(attrs);
        if (parsed.TryGetValue("AngVel", out var currentRaw)
            && Math.Abs(ParseDouble(currentRaw, 0) - clamped) <= 1e-6)
        {
            return false;
        }

        if (!SetOrReplaceAttribute(ref attrs, "AngVel", XmlNumericFormatting.Format(clamped)))
        {
            return false;
        }

        var replacement = $"<{targetMatch.Groups["tag"].Value}{attrs}{self}>";
        block = string.Concat(
            block.AsSpan(0, targetMatch.Index),
            replacement,
            block.AsSpan(targetMatch.Index + targetMatch.Length));
        return true;
    }

    private static double ClampAngVel(double value) =>
        Math.Clamp(value, MinAngVel, MaxAngVel);

    /// <summary>Test hook: apply gearbox field updates (including AngVel) to XML text.</summary>
    internal static string ApplyGearboxUpdatesToTextForTests(
        string content,
        string gearboxName,
        int price,
        double fuelConsumption,
        double idleFuelModifier,
        double? awdConsumptionModifier,
        double? maxGearAngVel,
        double? highGearAngVel,
        double? reverseGearAngVel,
        double damageCapacity = 0)
    {
        var updates = new Dictionary<string, GearboxAttributeValues>(StringComparer.OrdinalIgnoreCase)
        {
            [gearboxName] = new GearboxAttributeValues(
                price,
                damageCapacity,
                fuelConsumption,
                idleFuelModifier,
                awdConsumptionModifier,
                maxGearAngVel,
                highGearAngVel,
                reverseGearAngVel),
        };

        return TryApplyUpdatesToText(content, updates, out var updated, out _)
            ? updated
            : content;
    }

    private static int CountNamedDifferences(string currentText, string updatedText)
    {
        var current = ParseGearboxesFromText("current", currentText)
            .ToDictionary(gearbox => gearbox.Name, StringComparer.OrdinalIgnoreCase);
        var changed = 0;

        foreach (var target in ParseGearboxesFromText("target", updatedText))
        {
            if (!current.TryGetValue(target.Name, out var existing))
            {
                changed++;
                continue;
            }

            if (existing.Price != target.Price
                || Math.Abs(existing.DamageCapacity - target.DamageCapacity) > 1e-6
                || Math.Abs(existing.FuelConsumption - target.FuelConsumption) > 1e-6
                || Math.Abs(existing.IdleFuelModifier - target.IdleFuelModifier) > 1e-6
                || !NullableDoubleEquals(existing.AwdConsumptionModifier, target.AwdConsumptionModifier)
                || !NullableDoubleEquals(existing.MaxGearAngVel, target.MaxGearAngVel)
                || !NullableDoubleEquals(existing.HighGearAngVel, target.HighGearAngVel)
                || !NullableDoubleEquals(existing.ReverseGearAngVel, target.ReverseGearAngVel))
            {
                changed++;
            }
        }

        return changed;
    }

    private static bool TryScaleAttribute(ref string attrs, string attributeName, double multiplier)
    {
        if (!TryGetAttributeValue(attrs, attributeName, out var rawValue))
        {
            return false;
        }

        var scaled = XmlNumericFormatting.Round(ParseDouble(rawValue, 0) * multiplier);
        return SetOrReplaceAttribute(ref attrs, attributeName, XmlNumericFormatting.Format(scaled));
    }

    private static bool SetOrReplaceAttribute(ref string attrs, string attributeName, string value)
    {
        var pattern = $@"(?<prefix>\b{Regex.Escape(attributeName)}\s*=\s*"")(?<value>[^""]*)(?<suffix>"")";
        var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var match = regex.Match(attrs);
        if (match.Success)
        {
            if (string.Equals(match.Groups["value"].Value, value, StringComparison.Ordinal))
            {
                return false;
            }

            attrs = regex.Replace(attrs, $"{match.Groups["prefix"].Value}{value}{match.Groups["suffix"].Value}", 1);
            return true;
        }

        attrs = string.IsNullOrWhiteSpace(attrs)
            ? $" {attributeName}=\"{value}\""
            : $"{attrs.TrimEnd()} {attributeName}=\"{value}\"";
        return true;
    }

    private static bool TryGetAttributeValue(string attrs, string attributeName, out string value)
    {
        var parsed = ParseAttributes(attrs);
        if (parsed.TryGetValue(attributeName, out var found))
        {
            value = found;
            return true;
        }

        value = "";
        return false;
    }

    private static bool AttributeExists(string attrs, string attributeName) =>
        TryGetAttributeValue(attrs, attributeName, out _);

    private static string? ExtractUiNameKeyFromBlock(string content, int startIndex)
    {
        var nextGearbox = IndexOfNextElementOpenTag(content, startIndex + 1, "Gearbox");
        var blockEnd = nextGearbox >= 0 ? nextGearbox : content.Length;
        var block = content[startIndex..blockEnd];
        var match = Regex.Match(
            block,
            @"UiName\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    /// <summary>
    /// Finds the next real element open tag (e.g. &lt;Gearbox ...&gt;), ignoring lookalikes
    /// like &lt;GearboxParams&gt; / &lt;GearboxVariants&gt;.
    /// </summary>
    private static int IndexOfNextElementOpenTag(string content, int startIndex, string tagName)
    {
        var needle = "<" + tagName;
        var index = Math.Max(0, startIndex);

        while (index < content.Length)
        {
            index = content.IndexOf(needle, index, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
            {
                return -1;
            }

            var after = index + needle.Length;
            if (after >= content.Length)
            {
                return index;
            }

            var next = content[after];
            if (char.IsWhiteSpace(next) || next is '/' or '>')
            {
                return index;
            }

            index = after;
        }

        return -1;
    }

    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }


    private static string InferCategory(string entryPath) =>
        entryPath.Contains("/_dlc/", StringComparison.OrdinalIgnoreCase) ? "DLC" : "Base";

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private readonly record struct GearboxAttributeValues(
        int Price,
        double DamageCapacity,
        double FuelConsumption,
        double IdleFuelModifier,
        double? AwdConsumptionModifier,
        double? MaxGearAngVel,
        double? HighGearAngVel,
        double? ReverseGearAngVel);

    private static bool NullableDoubleEquals(double? left, double? right)
    {
        if (left is null && right is null)
        {
            return true;
        }

        if (left is null || right is null)
        {
            return false;
        }

        return Math.Abs(left.Value - right.Value) <= 1e-6;
    }
}

public sealed record GearboxSaveResult(int UpdatedFiles, int ChangedGearboxes);
