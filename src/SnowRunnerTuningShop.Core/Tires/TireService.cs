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

namespace SnowRunnerTuningShop.Core.Tires;

public static class TireService
{
    // Attr values may contain '/' (e.g. Mesh="wheels/..."), so do not use [^>/].
    private static readonly Regex TruckTireOpenTagRegex = new(
        @"<TruckTire\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    // Quoted values may contain '/' (e.g. Mesh="wheels/..."). Using [^>]* swallows the
    // self-closing slash, so a rewrite of `<WheelFriction _template="X" />` becomes
    // `<WheelFriction _template="X" / IsIgnoreIce="true">` and the garage/truck store breaks.
    private static readonly Regex WheelFrictionTagRegex = new(
        @"<WheelFriction\b(?<attrs>(?:[^>/]|""[^""]*"")*)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex CompatibleWheelsRegex = new(
        @"<CompatibleWheels\b(?<attrs>[^>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TireDefinition> LoadTires(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        var templates = WheelFrictionTemplates.LoadFromPak(pakPath);
        using var archive = ZipFile.OpenRead(pakPath);
        var setUsage = BuildWheelSetUsage(archive, strings);
        var tires = new List<TireDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsWheelEntry(entryPath))
            {
                continue;
            }

            tires.AddRange(ParseTiresFromText(entryPath, ReadEntryText(entry), strings, setUsage, templates));
        }

        return tires
            .OrderBy(item => item.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static TireSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double onRoadMultiplier,
        double offRoadMultiplier,
        double mudMultiplier,
        bool? ignoreIceForAll = null)
    {
        ValidateMultiplier(onRoadMultiplier, nameof(onRoadMultiplier));
        ValidateMultiplier(offRoadMultiplier, nameof(offRoadMultiplier));
        ValidateMultiplier(mudMultiplier, nameof(mudMultiplier));

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        var templates = WheelFrictionTemplates.LoadFromPak(baselinePath);

        Dictionary<string, byte[]> replacements;
        var changedTires = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsWheelEntry(entryPath))
                {
                    continue;
                }

                var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
                if (baselineEntry is null)
                {
                    continue;
                }

                var baselineText = ReadEntryText(baselineEntry);
                var updatedText = ApplyMultipliersToText(
                    baselineText,
                    templates,
                    onRoadMultiplier,
                    offRoadMultiplier,
                    mudMultiplier,
                    ignoreIceForAll);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedTires += CountNamedDifferences(currentText, updatedText, templates);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TireSaveResult(updatedFiles, changedTires);
    }

    public static TireSaveResult RestoreTiresFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, 1.0);

    public static TireSaveResult SaveTireChanges(string pakPath, IReadOnlyList<TireDefinition> tires)
    {
        ArgumentNullException.ThrowIfNull(tires);

        Dictionary<string, byte[]> replacements;
        var changedTires = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var grouped = tires
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
                    item => new TireFrictionValues(
                        item.OnRoadFriction,
                        item.OffRoadFriction,
                        item.MudFriction,
                        item.IgnoreIce),
                    StringComparer.OrdinalIgnoreCase);

                if (!TryApplyUpdatesToText(text, updates, out var updatedText, out var fileChanged))
                {
                    continue;
                }

                changedTires += fileChanged;
                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[group.Key] = Encoding.UTF8.GetBytes(updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TireSaveResult(updatedFiles, changedTires);
    }

    private static bool IsWheelEntry(string entryPath) =>
        entryPath.Contains("/classes/wheels/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruckEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/cargo/", StringComparison.OrdinalIgnoreCase);

    private static List<TireDefinition> ParseTiresFromText(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? setUsage,
        IReadOnlyDictionary<string, WheelFrictionTemplates.FrictionValues> templates)
    {
        var tires = new List<TireDefinition>();
        if (!content.Contains("<TruckTire", StringComparison.OrdinalIgnoreCase))
        {
            return tires;
        }

        var normalizedPath = entryPath.Replace('\\', '/');
        var sourceFile = Path.GetFileName(normalizedPath);
        var setId = Path.GetFileNameWithoutExtension(normalizedPath);
        var setName = StripWheelsPrefix(setId);
        var usedByNames = setUsage is not null && setUsage.TryGetValue(setId, out var names)
            ? names
            : Array.Empty<string>();

        var matches = TruckTireOpenTagRegex.Matches(content);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            if (IsInsideTemplatesSection(content, match.Index))
            {
                continue;
            }

            var blockEnd = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var block = content[match.Index..blockEnd];
            var frictionMatch = WheelFrictionTagRegex.Match(block);
            if (!frictionMatch.Success)
            {
                continue;
            }

            var frictionAttrs = ParseAttributes(frictionMatch.Groups["attrs"].Value);
            frictionAttrs.TryGetValue("_template", out var frictionTemplate);
            var resolved = WheelFrictionTemplates.Resolve(templates, frictionTemplate, frictionAttrs);
            var uiNameKey = ExtractUiNameKeyFromBlock(block);

            tires.Add(new TireDefinition
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
                UsedBy = PartXmlHelpers.FormatUsedBy(usedByNames),
                UsedByTooltip = PartXmlHelpers.FormatUsedByTooltip(
                    usedByNames,
                    "No trucks reference this wheel set."),
                UsedByVehicles = usedByNames,
                Category = InferCategory(entryPath),
                Price = PartXmlHelpers.ExtractPrice(block),
                FrictionTemplate = frictionTemplate ?? "",
                OnRoadFriction = resolved.BodyFrictionAsphalt,
                OffRoadFriction = resolved.BodyFriction,
                MudFriction = resolved.SubstanceFriction,
                IgnoreIce = resolved.IsIgnoreIce,
            });
        }

        return tires;
    }

    private static bool IsInsideTemplatesSection(string content, int index)
    {
        var templatesStart = content.LastIndexOf("<_templates", index, StringComparison.OrdinalIgnoreCase);
        if (templatesStart < 0)
        {
            return false;
        }

        var templatesEnd = content.IndexOf("</_templates>", templatesStart, StringComparison.OrdinalIgnoreCase);
        return templatesEnd < 0 || templatesEnd > index;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildWheelSetUsage(
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

            foreach (Match socketMatch in CompatibleWheelsRegex.Matches(text))
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

    private static string StripWheelsPrefix(string setId)
    {
        if (setId.StartsWith("wheels_", StringComparison.OrdinalIgnoreCase))
        {
            return setId["wheels_".Length..];
        }

        if (setId.StartsWith("wheel_", StringComparison.OrdinalIgnoreCase))
        {
            return setId["wheel_".Length..];
        }

        return setId;
    }

    private static string ApplyMultipliersToText(
        string baselineText,
        IReadOnlyDictionary<string, WheelFrictionTemplates.FrictionValues> templates,
        double onRoadMultiplier,
        double offRoadMultiplier,
        double mudMultiplier,
        bool? ignoreIceForAll)
    {
        var onRoadBaseline = TuningMultiplierPresets.IsBaselineMultiplier(onRoadMultiplier);
        var offRoadBaseline = TuningMultiplierPresets.IsBaselineMultiplier(offRoadMultiplier);
        var mudBaseline = TuningMultiplierPresets.IsBaselineMultiplier(mudMultiplier);

        if (onRoadBaseline && offRoadBaseline && mudBaseline && ignoreIceForAll is null)
        {
            return baselineText;
        }

        var matches = TruckTireOpenTagRegex.Matches(baselineText);
        if (matches.Count == 0)
        {
            return baselineText;
        }

        var builder = new StringBuilder(baselineText.Length);
        var lastIndex = 0;
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var blockEnd = i + 1 < matches.Count ? matches[i + 1].Index : baselineText.Length;
            var block = baselineText[match.Index..blockEnd];
            builder.Append(baselineText, lastIndex, match.Index - lastIndex);

            if (IsInsideTemplatesSection(baselineText, match.Index))
            {
                builder.Append(block);
            }
            else
            {
                builder.Append(ApplyMultipliersToTireBlock(
                    block,
                    templates,
                    onRoadMultiplier,
                    offRoadMultiplier,
                    mudMultiplier,
                    ignoreIceForAll,
                    onRoadBaseline,
                    offRoadBaseline,
                    mudBaseline));
            }

            lastIndex = blockEnd;
        }

        builder.Append(baselineText, lastIndex, baselineText.Length - lastIndex);
        return builder.ToString();
    }

    private static string ApplyMultipliersToTireBlock(
        string block,
        IReadOnlyDictionary<string, WheelFrictionTemplates.FrictionValues> templates,
        double onRoadMultiplier,
        double offRoadMultiplier,
        double mudMultiplier,
        bool? ignoreIceForAll,
        bool onRoadBaseline,
        bool offRoadBaseline,
        bool mudBaseline)
    {
        return WheelFrictionTagRegex.Replace(block, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var parsed = ParseAttributes(attrs);
            parsed.TryGetValue("_template", out var templateName);
            var resolved = WheelFrictionTemplates.Resolve(templates, templateName, parsed);

            var onRoad = onRoadBaseline ? resolved.BodyFrictionAsphalt : resolved.BodyFrictionAsphalt * onRoadMultiplier;
            var offRoad = offRoadBaseline ? resolved.BodyFriction : resolved.BodyFriction * offRoadMultiplier;
            var mud = mudBaseline ? resolved.SubstanceFriction : resolved.SubstanceFriction * mudMultiplier;
            var ignoreIce = ignoreIceForAll ?? resolved.IsIgnoreIce;

            var frictionUnchanged = Math.Abs(onRoad - resolved.BodyFrictionAsphalt) < 1e-9
                && Math.Abs(offRoad - resolved.BodyFriction) < 1e-9
                && Math.Abs(mud - resolved.SubstanceFriction) < 1e-9;
            var iceUnchanged = ignoreIceForAll is null || ignoreIce == resolved.IsIgnoreIce;
            if (frictionUnchanged && iceUnchanged)
            {
                return match.Value;
            }

            var updatedAttrs = attrs;
            if (!frictionUnchanged)
            {
                SetOrReplaceAttribute(ref updatedAttrs, "BodyFrictionAsphalt", FormatNumeric(onRoad));
                SetOrReplaceAttribute(ref updatedAttrs, "BodyFriction", FormatNumeric(offRoad));
                SetOrReplaceAttribute(ref updatedAttrs, "SubstanceFriction", FormatNumeric(mud));
            }

            if (ignoreIceForAll is not null)
            {
                ApplyIgnoreIceAttribute(ref updatedAttrs, ignoreIce);
            }

            return $"<WheelFriction{updatedAttrs}{self}>";
        }, 1);
    }

    private static bool TryApplyUpdatesToText(
        string content,
        IReadOnlyDictionary<string, TireFrictionValues> updates,
        out string updatedText,
        out int changedTires)
    {
        updatedText = content;
        changedTires = 0;
        if (updates.Count == 0)
        {
            return false;
        }

        var matches = TruckTireOpenTagRegex.Matches(content);
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

            if (!IsInsideTemplatesSection(content, match.Index)
                && attrs.TryGetValue("Name", out var name)
                && !string.IsNullOrWhiteSpace(name)
                && updates.TryGetValue(name, out var target)
                && TryApplyUpdatesToTireBlock(block, target, out var updatedBlock))
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
        changedTires = localChanged;
        return true;
    }

    private static bool TryApplyUpdatesToTireBlock(
        string block,
        TireFrictionValues target,
        out string updatedBlock)
    {
        updatedBlock = block;
        var changed = false;

        updatedBlock = WheelFrictionTagRegex.Replace(updatedBlock, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var updatedAttrs = attrs;
            var localChanged = false;

            // Game UI: On-road / Off-road / Mud
            localChanged |= SetOrReplaceAttribute(
                ref updatedAttrs,
                "BodyFrictionAsphalt",
                FormatNumeric(target.OnRoadFriction));
            localChanged |= SetOrReplaceAttribute(
                ref updatedAttrs,
                "BodyFriction",
                FormatNumeric(target.OffRoadFriction));
            localChanged |= SetOrReplaceAttribute(
                ref updatedAttrs,
                "SubstanceFriction",
                FormatNumeric(target.MudFriction));
            localChanged |= ApplyIgnoreIceAttribute(ref updatedAttrs, target.IgnoreIce);

            if (!localChanged)
            {
                return match.Value;
            }

            changed = true;
            return $"<WheelFriction{updatedAttrs}{self}>";
        }, 1);

        return changed;
    }

    private static int CountNamedDifferences(
        string currentText,
        string updatedText,
        IReadOnlyDictionary<string, WheelFrictionTemplates.FrictionValues> templates)
    {
        var current = ParseTiresFromText("current", currentText, null, null, templates)
            .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);
        var changed = 0;

        foreach (var target in ParseTiresFromText("target", updatedText, null, null, templates))
        {
            if (!current.TryGetValue(target.Name, out var existing))
            {
                changed++;
                continue;
            }

            if (Math.Abs(existing.OnRoadFriction - target.OnRoadFriction) > 1e-6
                || Math.Abs(existing.OffRoadFriction - target.OffRoadFriction) > 1e-6
                || Math.Abs(existing.MudFriction - target.MudFriction) > 1e-6
                || existing.IgnoreIce != target.IgnoreIce)
            {
                changed++;
            }
        }

        return changed;
    }

    private static string? ExtractUiNameKeyFromBlock(string block)
    {
        var match = Regex.Match(
            block,
            @"UiName\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
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

    private static bool ApplyIgnoreIceAttribute(ref string attributesText, bool ignoreIce) =>
        ignoreIce
            ? SetOrReplaceAttribute(ref attributesText, "IsIgnoreIce", "true")
            : RemoveAttribute(ref attributesText, "IsIgnoreIce");

    private static bool RemoveAttribute(ref string attributesText, string attributeName)
    {
        var pattern = $@"\s*\b{Regex.Escape(attributeName)}\s*=\s*""[^""]*""";
        var updated = Regex.Replace(
            attributesText,
            pattern,
            string.Empty,
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (string.Equals(updated, attributesText, StringComparison.Ordinal))
        {
            return false;
        }

        attributesText = updated;
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

    private readonly record struct TireFrictionValues(
        double OnRoadFriction,
        double OffRoadFriction,
        double MudFriction,
        bool IgnoreIce);
}

public sealed record TireSaveResult(int UpdatedFiles, int ChangedTires);
