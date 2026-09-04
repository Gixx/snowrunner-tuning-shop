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

namespace SnowRunnerTuningShop.Core.Suspension;

public static class SuspensionService
{
    private static readonly Regex SuspensionSetOpenTagRegex = new(
        @"<SuspensionSet\b(?<attrs>[^>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SuspensionOpenTagRegex = new(
        @"<Suspension\b(?<attrs>[^>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SuspensionSocketRegex = new(
        @"<SuspensionSocket\b(?<attrs>[^>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<SuspensionDefinition> LoadSuspensions(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        var setUsage = BuildSuspensionSetUsage(archive, strings);
        var suspensions = new List<SuspensionDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsSuspensionEntry(entryPath))
            {
                continue;
            }

            suspensions.AddRange(ParseSuspensionsFromText(entryPath, ReadEntryText(entry), strings, setUsage));
        }

        return suspensions
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static SuspensionSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double heightMultiplier,
        double strengthMultiplier,
        double dampingMultiplier,
        double damageCapacityMultiplier)
    {
        ValidateMultiplier(heightMultiplier, nameof(heightMultiplier));
        ValidateMultiplier(strengthMultiplier, nameof(strengthMultiplier));
        ValidateMultiplier(dampingMultiplier, nameof(dampingMultiplier));
        ValidateMultiplier(damageCapacityMultiplier, nameof(damageCapacityMultiplier));

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedSuspensions = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsSuspensionEntry(entryPath))
                {
                    continue;
                }

                var baselineText = PakVanillaText.Read(baselineArchive, entry, ReadEntryText);
                var updatedText = ApplyMultipliersToText(
                    baselineText,
                    heightMultiplier,
                    strengthMultiplier,
                    dampingMultiplier,
                    damageCapacityMultiplier);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedSuspensions += CountNamedDifferences(currentText, updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new SuspensionSaveResult(updatedFiles, changedSuspensions);
    }

    public static SuspensionSaveResult RestoreSuspensionsFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, 1.0, 1.0);

    public static SuspensionSaveResult SaveSuspensionChanges(
        string pakPath,
        IReadOnlyList<SuspensionDefinition> suspensions)
    {
        ArgumentNullException.ThrowIfNull(suspensions);

        Dictionary<string, byte[]> replacements;
        var changedSuspensions = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var grouped = suspensions
                .GroupBy(item => item.EntryPath.Replace('\\', '/'), StringComparer.Ordinal)
                .ToArray();

            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var entry = PakEntryLocator.FindEntry(archive, group.Key);
                if (entry is null)
                {
                    continue;
                }

                var text = ReadEntryText(entry);
                var updates = group.ToDictionary(
                    item => item.Name,
                    item => new SuspensionAttributeValues(
                        item.DamageCapacity,
                        item.FrontHeight,
                        item.FrontStrength,
                        item.FrontDamping,
                        item.HasFront,
                        item.RearHeight,
                        item.RearStrength,
                        item.RearDamping,
                        item.HasRear),
                    StringComparer.OrdinalIgnoreCase);

                if (!TryApplyUpdatesToText(text, updates, out var updatedText, out var fileChanged))
                {
                    continue;
                }

                changedSuspensions += fileChanged;
                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[group.Key] = Encoding.UTF8.GetBytes(updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new SuspensionSaveResult(updatedFiles, changedSuspensions);
    }

    private static bool IsSuspensionEntry(string entryPath) =>
        entryPath.Contains("/classes/suspensions/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruckEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/cargo/", StringComparison.OrdinalIgnoreCase);

    private static List<SuspensionDefinition> ParseSuspensionsFromText(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? setUsage = null)
    {
        var suspensions = new List<SuspensionDefinition>();
        if (!content.Contains("<SuspensionSet", StringComparison.OrdinalIgnoreCase))
        {
            return suspensions;
        }

        var normalizedPath = entryPath.Replace('\\', '/');
        var sourceFile = Path.GetFileName(normalizedPath);
        var setId = Path.GetFileNameWithoutExtension(normalizedPath);
        var setName = setId.StartsWith("s_", StringComparison.OrdinalIgnoreCase)
            ? setId[2..]
            : setId;
        var usedByNames = setUsage is not null && setUsage.TryGetValue(setId, out var names)
            ? names
            : Array.Empty<string>();

        foreach (Match match in SuspensionSetOpenTagRegex.Matches(content))
        {
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var blockEnd = IndexOfNextElementOpenTag(content, match.Index + 1, "SuspensionSet");
            if (blockEnd < 0)
            {
                blockEnd = content.Length;
            }

            var block = content[match.Index..blockEnd];
            var uiNameKey = ExtractUiNameKeyFromBlock(block)
                ?? (attrs.TryGetValue("UiName", out var attrUiName) ? attrUiName : null);

            ReadWheelValues(
                block,
                "front",
                out var hasFront,
                out var frontHeight,
                out var frontStrength,
                out var frontDamping);
            ReadWheelValues(
                block,
                "rear",
                out var hasRear,
                out var rearHeight,
                out var rearStrength,
                out var rearDamping);

            suspensions.Add(new SuspensionDefinition
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
                FrontHeight = frontHeight,
                FrontStrength = frontStrength,
                FrontDamping = frontDamping,
                HasFront = hasFront,
                RearHeight = rearHeight,
                RearStrength = rearStrength,
                RearDamping = rearDamping,
                HasRear = hasRear,
            });
        }

        return suspensions;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildSuspensionSetUsage(
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
            var text = ReadEntryText(entry);
            truckDisplayNames[truckId] = ResolveTruckDisplayName(truckId, text, strings);

            foreach (Match socketMatch in SuspensionSocketRegex.Matches(text))
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
                .OrderBy(display => display, StringComparer.OrdinalIgnoreCase)
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
            ? PartUsageMessages.NoTrucksSuspensionSet
            : string.Join(", ", vehicleNames);

    private static string ApplyMultipliersToText(
        string baselineText,
        double heightMultiplier,
        double strengthMultiplier,
        double dampingMultiplier,
        double damageCapacityMultiplier)
    {
        var heightBaseline = TuningMultiplierPresets.IsBaselineMultiplier(heightMultiplier);
        var strengthBaseline = TuningMultiplierPresets.IsBaselineMultiplier(strengthMultiplier);
        var dampingBaseline = TuningMultiplierPresets.IsBaselineMultiplier(dampingMultiplier);
        var damageBaseline = TuningMultiplierPresets.IsBaselineMultiplier(damageCapacityMultiplier);

        if (heightBaseline && strengthBaseline && dampingBaseline && damageBaseline)
        {
            return baselineText;
        }

        var withSets = SuspensionSetOpenTagRegex.Replace(baselineText, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var parsed = ParseAttributes(attrs);
            if (!parsed.ContainsKey("Name") || damageBaseline)
            {
                return match.Value;
            }

            var updatedAttrs = attrs;
            if (!TryScaleAttribute(ref updatedAttrs, "DamageCapacity", damageCapacityMultiplier))
            {
                return match.Value;
            }

            return $"<SuspensionSet{updatedAttrs}{self}>";
        });

        if (heightBaseline && strengthBaseline && dampingBaseline)
        {
            return withSets;
        }

        return SuspensionOpenTagRegex.Replace(withSets, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var updatedAttrs = attrs;
            var changed = false;

            if (!heightBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "Height", heightMultiplier);
            }

            if (!strengthBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "Strength", strengthMultiplier);
            }

            if (!dampingBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "Damping", dampingMultiplier);
            }

            if (!changed)
            {
                return match.Value;
            }

            return $"<Suspension{updatedAttrs}{self}>";
        });
    }

    private static bool TryApplyUpdatesToText(
        string content,
        IReadOnlyDictionary<string, SuspensionAttributeValues> updates,
        out string updatedText,
        out int changedSuspensions)
    {
        updatedText = content;
        changedSuspensions = 0;
        if (updates.Count == 0)
        {
            return false;
        }

        var matches = SuspensionSetOpenTagRegex.Matches(content);
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
            var blockEnd = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var block = content[match.Index..blockEnd];

            builder.Append(content, lastIndex, match.Index - lastIndex);

            if (attrs.TryGetValue("Name", out var name)
                && !string.IsNullOrWhiteSpace(name)
                && updates.TryGetValue(name, out var target)
                && TryApplyUpdatesToSetBlock(block, target, out var updatedBlock))
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
        changedSuspensions = localChanged;
        return true;
    }

    private static bool TryApplyUpdatesToSetBlock(
        string block,
        SuspensionAttributeValues target,
        out string updatedBlock)
    {
        updatedBlock = block;
        var changed = false;

        updatedBlock = SuspensionSetOpenTagRegex.Replace(updatedBlock, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var updatedAttrs = attrs;
            if (!SetOrReplaceAttribute(ref updatedAttrs, "DamageCapacity", FormatNumeric(target.DamageCapacity)))
            {
                return match.Value;
            }

            changed = true;
            return $"<SuspensionSet{updatedAttrs}{self}>";
        }, 1);

        updatedBlock = SuspensionOpenTagRegex.Replace(updatedBlock, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var parsed = ParseAttributes(attrs);
            if (!parsed.TryGetValue("WheelType", out var wheelType))
            {
                return match.Value;
            }

            double? height;
            double? strength;
            double? damping;
            if (wheelType.Equals("front", StringComparison.OrdinalIgnoreCase) && target.HasFront)
            {
                height = target.FrontHeight;
                strength = target.FrontStrength;
                damping = target.FrontDamping;
            }
            else if (wheelType.Equals("rear", StringComparison.OrdinalIgnoreCase) && target.HasRear)
            {
                height = target.RearHeight;
                strength = target.RearStrength;
                damping = target.RearDamping;
            }
            else
            {
                return match.Value;
            }

            var updatedAttrs = attrs;
            var localChanged = false;
            if (height.HasValue || AttributeExists(updatedAttrs, "Height"))
            {
                localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "Height", FormatNumeric(height ?? 0));
            }

            if (strength.HasValue || AttributeExists(updatedAttrs, "Strength"))
            {
                localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "Strength", FormatNumeric(strength ?? 0));
            }

            if (damping.HasValue || AttributeExists(updatedAttrs, "Damping"))
            {
                localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "Damping", FormatNumeric(damping ?? 0));
            }

            if (!localChanged)
            {
                return match.Value;
            }

            changed = true;
            return $"<Suspension{updatedAttrs}{self}>";
        });

        return changed;
    }

    private static int CountNamedDifferences(string currentText, string updatedText)
    {
        var current = ParseSuspensionsFromText("current", currentText)
            .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var changed = 0;

        foreach (var target in ParseSuspensionsFromText("target", updatedText))
        {
            if (!current.TryGetValue(target.Name, out var existing))
            {
                changed++;
                continue;
            }

            if (Math.Abs(existing.DamageCapacity - target.DamageCapacity) > 1e-6
                || !NullableDoubleEquals(existing.FrontHeight, target.FrontHeight)
                || !NullableDoubleEquals(existing.FrontStrength, target.FrontStrength)
                || !NullableDoubleEquals(existing.FrontDamping, target.FrontDamping)
                || !NullableDoubleEquals(existing.RearHeight, target.RearHeight)
                || !NullableDoubleEquals(existing.RearStrength, target.RearStrength)
                || !NullableDoubleEquals(existing.RearDamping, target.RearDamping))
            {
                changed++;
            }
        }

        return changed;
    }

    private static void ReadWheelValues(
        string block,
        string wheelType,
        out bool found,
        out double? height,
        out double? strength,
        out double? damping)
    {
        found = false;
        height = null;
        strength = null;
        damping = null;

        foreach (Match match in SuspensionOpenTagRegex.Matches(block))
        {
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("WheelType", out var type)
                || !type.Equals(wheelType, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            found = true;
            if (attrs.ContainsKey("Height"))
            {
                height = ParseDouble(attrs["Height"], 0);
            }

            if (attrs.ContainsKey("Strength"))
            {
                strength = ParseDouble(attrs["Strength"], 0);
            }

            if (attrs.ContainsKey("Damping"))
            {
                damping = ParseDouble(attrs["Damping"], 0);
            }

            return;
        }
    }

    private static string? ExtractUiNameKeyFromBlock(string block)
    {
        var match = Regex.Match(
            block,
            @"UiName\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

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

    private static Dictionary<string, string> ParseAttributes(string attributesText)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attributesText))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static bool TryScaleAttribute(ref string attributesText, string attributeName, double multiplier)
    {
        if (!TryGetAttributeValue(attributesText, attributeName, out var raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            return false;
        }

        return SetOrReplaceAttribute(ref attributesText, attributeName, FormatNumeric(parsed * multiplier));
    }

    private static bool TryGetAttributeValue(string attributesText, string attributeName, out string value)
    {
        var match = Regex.Match(
            attributesText,
            $@"\b{Regex.Escape(attributeName)}\s*=\s*""(?<value>[^""]*)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        value = match.Success ? match.Groups["value"].Value : "";
        return match.Success;
    }

    private static bool SetOrReplaceAttribute(ref string attributesText, string attributeName, string value)
    {
        var pattern = $@"\b{Regex.Escape(attributeName)}\s*=\s*""[^""]*""";
        var replacement = $"{attributeName}=\"{value}\"";
        if (Regex.IsMatch(attributesText, pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var updated = Regex.Replace(
                attributesText,
                pattern,
                replacement,
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            if (string.Equals(updated, attributesText, StringComparison.Ordinal))
            {
                return false;
            }

            attributesText = updated;
            return true;
        }

        attributesText = attributesText.TrimEnd() + $" {replacement}";
        return true;
    }

    private static string InferCategory(string entryPath) =>
        entryPath.Contains("/_dlc/", StringComparison.OrdinalIgnoreCase) ? "DLC" : "Base";

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string FormatNumeric(double value)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
        {
            return ((long)Math.Round(value, MidpointRounding.AwayFromZero)).ToString(CultureInfo.InvariantCulture);
        }

        return value.ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static void ValidateMultiplier(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Multiplier must be a positive number.");
        }
    }

    private readonly record struct SuspensionAttributeValues(
        double DamageCapacity,
        double? FrontHeight,
        double? FrontStrength,
        double? FrontDamping,
        bool HasFront,
        double? RearHeight,
        double? RearStrength,
        double? RearDamping,
        bool HasRear);

    private static bool AttributeExists(string attributesText, string attributeName) =>
        TryGetAttributeValue(attributesText, attributeName, out _);

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

public sealed record SuspensionSaveResult(int UpdatedFiles, int ChangedSuspensions);
