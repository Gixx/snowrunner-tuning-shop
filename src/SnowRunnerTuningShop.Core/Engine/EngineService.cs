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

namespace SnowRunnerTuningShop.Core.Engine;

public static class EngineService
{
    /// <summary>Saber default when EngineResponsiveness is omitted from XML.</summary>
    public const double DefaultEngineResponsiveness = 0.04;

    /// <summary>Saber min for MaxDeltaAngVel.</summary>
    public const double MinMaxDeltaAngVel = 0;

    /// <summary>
    /// Practical editor max. Saber docs allow up to 1_000_000, but vanilla engines
    /// are typically ~0.01–0.1; 10 leaves headroom without absurd values.
    /// </summary>
    public const double MaxMaxDeltaAngVel = 10;

    private static readonly string[] DamageAndResponsivenessTags =
    [
        "Engine",
        "USTruckOldEngine",
        "USTruckOldHeavyEngine",
        "RUTruckOldEngine",
        "RUTruckOldHeavyEngine",
        "USTruckMilitaryNavistarEngine",
    ];

    // Attrs must not accept '<' or the match can swallow the next tag (issue #6).
    // '/' is reserved for an optional self-close before '>'.
    private static readonly Regex EngineOpenTagRegex = new(
        @"<Engine\b(?<attrs>[^<>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScalableOpenTagRegex = new(
        @"<(?<tag>Engine|USTruckOldEngine|USTruckOldHeavyEngine|RUTruckOldEngine|RUTruckOldHeavyEngine|USTruckMilitaryNavistarEngine)\b(?<attrs>[^<>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EngineSocketRegex = new(
        @"<EngineSocket\b(?<attrs>[^<>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<EngineDefinition> LoadEngines(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        var setUsage = BuildEngineSetUsage(archive, strings);
        var engines = new List<EngineDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsEngineEntry(entryPath))
            {
                continue;
            }

            engines.AddRange(ParseEnginesFromText(entryPath, PartXmlHelpers.ReadEntryUtf8(entry), strings, setUsage));
        }

        return engines
            .OrderBy(engine => engine.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(engine => engine.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(engine => engine.SetName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static EngineSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double torqueMultiplier,
        double fuelConsumptionMultiplier,
        double damageCapacityMultiplier,
        double engineResponsivenessMultiplier)
    {
        PartPakPipeline.ValidateMultiplier(torqueMultiplier, nameof(torqueMultiplier));
        PartPakPipeline.ValidateMultiplier(fuelConsumptionMultiplier, nameof(fuelConsumptionMultiplier));
        PartPakPipeline.ValidateMultiplier(damageCapacityMultiplier, nameof(damageCapacityMultiplier));
        PartPakPipeline.ValidateMultiplier(engineResponsivenessMultiplier, nameof(engineResponsivenessMultiplier));

        var result = PartPakPipeline.BuildBaselineReplacements(
            pakPath,
            IsEngineEntry,
            (_, baselineText, _) => ApplyMultipliersToText(
                baselineText,
                torqueMultiplier,
                fuelConsumptionMultiplier,
                damageCapacityMultiplier,
                engineResponsivenessMultiplier),
            (_, currentText, updatedText) => CountNamedEngineDifferences(currentText, updatedText));

        var updatedFiles = PartPakPipeline.CommitReplacements(pakPath, result.Replacements);
        return new EngineSaveResult(updatedFiles, result.ChangedItems);
    }

    public static EngineSaveResult RestoreEnginesFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, 1.0, 1.0);

    public static EngineSaveResult SaveEngineChanges(string pakPath, IReadOnlyList<EngineDefinition> engines)
    {
        ArgumentNullException.ThrowIfNull(engines);

        Dictionary<string, byte[]> replacements;
        var changedEngines = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var grouped = engines
                .GroupBy(engine => engine.EntryPath.Replace('\\', '/'), StringComparer.Ordinal)
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
                    engine => engine.Name,
                    engine => new EngineAttributeValues(
                        engine.Price,
                        engine.Torque,
                        engine.FuelConsumption,
                        engine.DamageCapacity,
                        engine.EngineResponsiveness,
                        engine.HasEngineResponsiveness,
                        engine.MaxDeltaAngVel,
                        engine.HasMaxDeltaAngVel),
                    StringComparer.OrdinalIgnoreCase);

                if (!TryApplyEngineUpdatesToText(text, updates, out var updatedText, out var fileChanged))
                {
                    continue;
                }

                changedEngines += fileChanged;
                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[group.Key] = Encoding.UTF8.GetBytes(updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new EngineSaveResult(updatedFiles, changedEngines);
    }

    private static bool IsEngineEntry(string entryPath) =>
        entryPath.Contains("/classes/engines/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruckEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase)
        && !entryPath.Contains("/classes/trucks/cargo/", StringComparison.OrdinalIgnoreCase);

    private static List<EngineDefinition> ParseEnginesFromText(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings = null,
        IReadOnlyDictionary<string, IReadOnlyList<string>>? setUsage = null)
    {
        var engines = new List<EngineDefinition>();
        if (!content.Contains("<Engine", StringComparison.OrdinalIgnoreCase))
        {
            return engines;
        }

        var normalizedPath = entryPath.Replace('\\', '/');
        var sourceFile = Path.GetFileName(normalizedPath);
        var setId = Path.GetFileNameWithoutExtension(normalizedPath);
        var setName = setId.StartsWith("e_", StringComparison.OrdinalIgnoreCase) ? setId[2..] : setId;
        var usedByNames = setUsage is not null && setUsage.TryGetValue(setId, out var names)
            ? names
            : Array.Empty<string>();

        var matches = EngineOpenTagRegex.Matches(content);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            var blockEnd = IndexOfNextElementOpenTag(content, match.Index + 1, "Engine");
            if (blockEnd < 0)
            {
                blockEnd = content.Length;
            }

            var block = content[match.Index..blockEnd];
            var uiNameKey = ExtractUiNameKeyFromBlock(content, match.Index);
            var hasResponsiveness = attrs.ContainsKey("EngineResponsiveness");
            engines.Add(new EngineDefinition
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
                    PartUsageMessages.NoTrucksEngineSet),
                Category = InferCategory(entryPath),
                Price = PartXmlHelpers.ExtractPrice(block),
                Torque = ParseDouble(attrs.GetValueOrDefault("Torque"), 0),
                FuelConsumption = ParseDouble(attrs.GetValueOrDefault("FuelConsumption"), 0),
                DamageCapacity = ParseDouble(attrs.GetValueOrDefault("DamageCapacity"), 0),
                EngineResponsiveness = hasResponsiveness
                    ? ParseDouble(attrs["EngineResponsiveness"], DefaultEngineResponsiveness)
                    : DefaultEngineResponsiveness,
                HasEngineResponsiveness = hasResponsiveness,
                MaxDeltaAngVel = attrs.ContainsKey("MaxDeltaAngVel")
                    ? ParseDouble(attrs["MaxDeltaAngVel"], 0)
                    : null,
                HasMaxDeltaAngVel = attrs.ContainsKey("MaxDeltaAngVel"),
            });
        }

        return engines;
    }

    private static Dictionary<string, IReadOnlyList<string>> BuildEngineSetUsage(
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

            foreach (Match socketMatch in EngineSocketRegex.Matches(text))
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

    internal static string ApplyMultipliersToTextForTests(
        string baselineText,
        double torqueMultiplier,
        double fuelConsumptionMultiplier,
        double damageCapacityMultiplier,
        double engineResponsivenessMultiplier) =>
        ApplyMultipliersToText(
            baselineText,
            torqueMultiplier,
            fuelConsumptionMultiplier,
            damageCapacityMultiplier,
            engineResponsivenessMultiplier);

    private static string ApplyMultipliersToText(
        string baselineText,
        double torqueMultiplier,
        double fuelConsumptionMultiplier,
        double damageCapacityMultiplier,
        double engineResponsivenessMultiplier)
    {
        var torqueBaseline = TuningMultiplierPresets.IsBaselineMultiplier(torqueMultiplier);
        var fuelBaseline = TuningMultiplierPresets.IsBaselineMultiplier(fuelConsumptionMultiplier);
        var damageBaseline = TuningMultiplierPresets.IsBaselineMultiplier(damageCapacityMultiplier);
        var responsivenessBaseline = TuningMultiplierPresets.IsBaselineMultiplier(engineResponsivenessMultiplier);

        if (torqueBaseline && fuelBaseline && damageBaseline && responsivenessBaseline)
        {
            return baselineText;
        }

        return ScalableOpenTagRegex.Replace(baselineText, match =>
        {
            var tag = match.Groups["tag"].Value;
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var isEngineTag = tag.Equals("Engine", StringComparison.OrdinalIgnoreCase);
            var updatedAttrs = attrs;
            var changed = false;

            if (isEngineTag && !torqueBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "Torque", torqueMultiplier, preferInteger: true);
            }

            if (isEngineTag && !fuelBaseline)
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "FuelConsumption", fuelConsumptionMultiplier, preferInteger: false);
            }

            if (!damageBaseline && IsDamageOrResponsivenessTag(tag))
            {
                changed |= TryScaleAttribute(ref updatedAttrs, "DamageCapacity", damageCapacityMultiplier, preferInteger: true);
            }

            if (!responsivenessBaseline && IsDamageOrResponsivenessTag(tag))
            {
                if (TryScaleAttribute(
                        ref updatedAttrs,
                        "EngineResponsiveness",
                        engineResponsivenessMultiplier,
                        preferInteger: false,
                        keepTrailingDotZero: true))
                {
                    changed = true;
                }
                else if (!AttributeExists(updatedAttrs, "EngineResponsiveness"))
                {
                    var scaledDefault = XmlNumericFormatting.Round(
                        DefaultEngineResponsiveness * engineResponsivenessMultiplier);
                    changed |= SetOrReplaceAttribute(
                        ref updatedAttrs,
                        "EngineResponsiveness",
                        FormatEngineResponsiveness(scaledDefault));
                }
            }

            if (!changed)
            {
                return match.Value;
            }

            return $"<{tag}{updatedAttrs}{self}>";
        });
    }

    private static bool TryApplyEngineUpdatesToText(
        string content,
        IReadOnlyDictionary<string, EngineAttributeValues> updates,
        out string updatedText,
        out int changedEngines)
    {
        updatedText = content;
        changedEngines = 0;
        if (updates.Count == 0)
        {
            return false;
        }

        var matches = EngineOpenTagRegex.Matches(content);
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
            var blockEnd = IndexOfNextElementOpenTag(content, match.Index + 1, "Engine");
            if (blockEnd < 0)
            {
                blockEnd = content.Length;
            }

            var block = content[match.Index..blockEnd];
            builder.Append(content, lastIndex, match.Index - lastIndex);

            if (attrs.TryGetValue("Name", out var name)
                && !string.IsNullOrWhiteSpace(name)
                && updates.TryGetValue(name, out var target)
                && TryApplyUpdatesToEngineBlock(block, target, out var updatedBlock))
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
        changedEngines = localChanged;
        return true;
    }

    private static bool TryApplyUpdatesToEngineBlock(
        string block,
        EngineAttributeValues target,
        out string updatedBlock)
    {
        updatedBlock = block;
        var changed = false;

        updatedBlock = EngineOpenTagRegex.Replace(updatedBlock, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var updatedAttrs = attrs;
            var localChanged = false;
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "Torque", FormatNumeric(target.Torque, preferInteger: true));
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "FuelConsumption", FormatNumeric(target.FuelConsumption, preferInteger: false));
            localChanged |= SetOrReplaceAttribute(ref updatedAttrs, "DamageCapacity", FormatNumeric(target.DamageCapacity, preferInteger: true));

            if (ShouldWriteMaxDeltaAngVel(target, updatedAttrs))
            {
                localChanged |= SetOrReplaceAttribute(
                    ref updatedAttrs,
                    "MaxDeltaAngVel",
                    FormatNumeric(ClampMaxDeltaAngVel(target.MaxDeltaAngVel ?? 0), preferInteger: false));
            }

            if (ShouldWriteEngineResponsiveness(target, updatedAttrs))
            {
                localChanged |= SetOrReplaceAttribute(
                    ref updatedAttrs,
                    "EngineResponsiveness",
                    FormatEngineResponsiveness(target.EngineResponsiveness));
            }

            if (!localChanged)
            {
                return match.Value;
            }

            changed = true;
            return $"<Engine{updatedAttrs}{self}>";
        }, 1);

        changed |= PartXmlHelpers.TrySetPrice(ref updatedBlock, target.Price);
        return changed;
    }

    /// <summary>Test hook: apply named engine field updates (including Price / MaxDeltaAngVel) to XML text.</summary>
    internal static string ApplyEngineUpdatesToTextForTests(
        string content,
        string engineName,
        int price,
        double torque,
        double fuelConsumption,
        double damageCapacity,
        double engineResponsiveness,
        bool hasEngineResponsiveness = true,
        double? maxDeltaAngVel = null)
    {
        var updates = new Dictionary<string, EngineAttributeValues>(StringComparer.OrdinalIgnoreCase)
        {
            [engineName] = new EngineAttributeValues(
                price,
                torque,
                fuelConsumption,
                damageCapacity,
                engineResponsiveness,
                hasEngineResponsiveness,
                maxDeltaAngVel,
                HasMaxDeltaAngVel: maxDeltaAngVel.HasValue),
        };

        return TryApplyEngineUpdatesToText(content, updates, out var updated, out _)
            ? updated
            : content;
    }

    private static double ClampMaxDeltaAngVel(double value) =>
        Math.Clamp(value, MinMaxDeltaAngVel, MaxMaxDeltaAngVel);

    private static bool ShouldWriteMaxDeltaAngVel(EngineAttributeValues target, string attrs) =>
        target.MaxDeltaAngVel.HasValue;

    private static bool ShouldWriteEngineResponsiveness(EngineAttributeValues target, string attrs) =>
        target.HasEngineResponsiveness
        || AttributeExists(attrs, "EngineResponsiveness")
        || Math.Abs(target.EngineResponsiveness - DefaultEngineResponsiveness) > 1e-6;

    private static int CountNamedEngineDifferences(string currentText, string updatedText)
    {
        var current = ParseEnginesFromText("current", currentText)
            .ToDictionary(engine => engine.Name, StringComparer.OrdinalIgnoreCase);
        var changed = 0;

        foreach (var target in ParseEnginesFromText("target", updatedText))
        {
            if (!current.TryGetValue(target.Name, out var existing))
            {
                changed++;
                continue;
            }

            if (existing.Price != target.Price
                || Math.Abs(existing.Torque - target.Torque) > 1e-6
                || Math.Abs(existing.FuelConsumption - target.FuelConsumption) > 1e-6
                || Math.Abs(existing.DamageCapacity - target.DamageCapacity) > 1e-6
                || Math.Abs(existing.EngineResponsiveness - target.EngineResponsiveness) > 1e-6
                || !NullableDoubleEquals(existing.MaxDeltaAngVel, target.MaxDeltaAngVel))
            {
                changed++;
            }
        }

        return changed;
    }

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

        return Math.Abs(left.Value - right.Value) <= 1e-9;
    }

    private static bool TryScaleAttribute(
        ref string attrs,
        string attributeName,
        double multiplier,
        bool preferInteger,
        bool keepTrailingDotZero = false)
    {
        if (!TryGetAttributeValue(attrs, attributeName, out var rawValue))
        {
            return false;
        }

        var scaled = Math.Round(
            ParseDouble(rawValue, 0) * multiplier,
            preferInteger ? 0 : XmlNumericFormatting.DecimalPlaces,
            MidpointRounding.AwayFromZero);
        return SetOrReplaceAttribute(
            ref attrs,
            attributeName,
            FormatNumeric(scaled, preferInteger, keepTrailingDotZero));
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

    private static bool IsDamageOrResponsivenessTag(string tag) =>
        DamageAndResponsivenessTags.Any(candidate => candidate.Equals(tag, StringComparison.OrdinalIgnoreCase));

    private static string? ExtractUiNameKeyFromBlock(string content, int engineStartIndex)
    {
        var nextEngine = IndexOfNextElementOpenTag(content, engineStartIndex + 1, "Engine");
        var blockEnd = nextEngine >= 0 ? nextEngine : content.Length;
        var block = content[engineStartIndex..blockEnd];
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

    private static string FormatNumeric(double value, bool preferInteger, bool keepTrailingDotZero = false) =>
        XmlNumericFormatting.Format(value, preferInteger, keepTrailingDotZero);

    /// <summary>
    /// EngineResponsiveness is a float in vanilla XML (e.g. <c>0.04</c>, <c>1.0</c>).
    /// Prefer <c>1.0</c> over bare <c>1</c> — same class of issue as TruckData Responsiveness (#7).
    /// </summary>
    private static string FormatEngineResponsiveness(double value) =>
        FormatNumeric(value, preferInteger: false, keepTrailingDotZero: true);


    private readonly record struct EngineAttributeValues(
        int Price,
        double Torque,
        double FuelConsumption,
        double DamageCapacity,
        double EngineResponsiveness,
        bool HasEngineResponsiveness,
        double? MaxDeltaAngVel,
        bool HasMaxDeltaAngVel);
}

public sealed record EngineSaveResult(int UpdatedFiles, int ChangedEngines);
