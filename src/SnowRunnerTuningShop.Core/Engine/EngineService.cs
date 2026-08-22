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

namespace SnowRunnerTuningShop.Core.Engine;

public static class EngineService
{
    /// <summary>Saber default when EngineResponsiveness is omitted from XML.</summary>
    public const double DefaultEngineResponsiveness = 0.04;

    private static readonly string[] DamageAndResponsivenessTags =
    [
        "Engine",
        "USTruckOldEngine",
        "USTruckOldHeavyEngine",
        "RUTruckOldEngine",
        "RUTruckOldHeavyEngine",
        "USTruckMilitaryNavistarEngine",
    ];

    private static readonly Regex EngineOpenTagRegex = new(
        @"<Engine\b(?<attrs>[^>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ScalableOpenTagRegex = new(
        @"<(?<tag>Engine|USTruckOldEngine|USTruckOldHeavyEngine|RUTruckOldEngine|RUTruckOldHeavyEngine|USTruckMilitaryNavistarEngine)\b(?<attrs>[^>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EngineSocketRegex = new(
        @"<EngineSocket\b(?<attrs>[^>]*)/?>",
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

            engines.AddRange(ParseEnginesFromText(entryPath, ReadEntryText(entry), strings, setUsage));
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
        ValidateMultiplier(torqueMultiplier, nameof(torqueMultiplier));
        ValidateMultiplier(fuelConsumptionMultiplier, nameof(fuelConsumptionMultiplier));
        ValidateMultiplier(damageCapacityMultiplier, nameof(damageCapacityMultiplier));
        ValidateMultiplier(engineResponsivenessMultiplier, nameof(engineResponsivenessMultiplier));

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedEngines = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsEngineEntry(entryPath))
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
                    torqueMultiplier,
                    fuelConsumptionMultiplier,
                    damageCapacityMultiplier,
                    engineResponsivenessMultiplier);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedEngines += CountNamedEngineDifferences(currentText, updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new EngineSaveResult(updatedFiles, changedEngines);
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

                var text = ReadEntryText(entry);
                var updates = group.ToDictionary(
                    engine => engine.Name,
                    engine => new EngineAttributeValues(
                        engine.Torque,
                        engine.FuelConsumption,
                        engine.DamageCapacity,
                        engine.EngineResponsiveness,
                        engine.HasEngineResponsiveness),
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
                    "No trucks reference this engine set."),
                Category = InferCategory(entryPath),
                Price = PartXmlHelpers.ExtractPrice(block),
                Torque = ParseDouble(attrs.GetValueOrDefault("Torque"), 0),
                FuelConsumption = ParseDouble(attrs.GetValueOrDefault("FuelConsumption"), 0),
                DamageCapacity = ParseDouble(attrs.GetValueOrDefault("DamageCapacity"), 0),
                EngineResponsiveness = hasResponsiveness
                    ? ParseDouble(attrs["EngineResponsiveness"], DefaultEngineResponsiveness)
                    : DefaultEngineResponsiveness,
                HasEngineResponsiveness = hasResponsiveness,
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
            var text = ReadEntryText(entry);
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
                if (!TryScaleAttribute(ref updatedAttrs, "EngineResponsiveness", engineResponsivenessMultiplier, preferInteger: false))
                {
                    var scaledDefault = Math.Round(
                        DefaultEngineResponsiveness * engineResponsivenessMultiplier,
                        6,
                        MidpointRounding.AwayFromZero);
                    changed |= SetOrReplaceAttribute(
                        ref updatedAttrs,
                        "EngineResponsiveness",
                        FormatNumeric(scaledDefault, preferInteger: false));
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

        var localChanged = 0;
        var result = EngineOpenTagRegex.Replace(content, match =>
        {
            var attrs = match.Groups["attrs"].Value;
            var self = match.Groups["self"].Value;
            var parsed = ParseAttributes(attrs);
            if (!parsed.TryGetValue("Name", out var name)
                || string.IsNullOrWhiteSpace(name)
                || !updates.TryGetValue(name, out var target))
            {
                return match.Value;
            }

            var updatedAttrs = attrs;
            var changed = false;
            changed |= SetOrReplaceAttribute(ref updatedAttrs, "Torque", FormatNumeric(target.Torque, preferInteger: true));
            changed |= SetOrReplaceAttribute(ref updatedAttrs, "FuelConsumption", FormatNumeric(target.FuelConsumption, preferInteger: false));
            changed |= SetOrReplaceAttribute(ref updatedAttrs, "DamageCapacity", FormatNumeric(target.DamageCapacity, preferInteger: true));

            if (ShouldWriteEngineResponsiveness(target, updatedAttrs))
            {
                changed |= SetOrReplaceAttribute(
                    ref updatedAttrs,
                    "EngineResponsiveness",
                    FormatNumeric(target.EngineResponsiveness, preferInteger: false));
            }

            if (!changed)
            {
                return match.Value;
            }

            localChanged++;
            return $"<Engine{updatedAttrs}{self}>";
        });

        if (localChanged == 0)
        {
            return false;
        }

        updatedText = result;
        changedEngines = localChanged;
        return true;
    }

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

            if (Math.Abs(existing.Torque - target.Torque) > 1e-6
                || Math.Abs(existing.FuelConsumption - target.FuelConsumption) > 1e-6
                || Math.Abs(existing.DamageCapacity - target.DamageCapacity) > 1e-6
                || Math.Abs(existing.EngineResponsiveness - target.EngineResponsiveness) > 1e-6)
            {
                changed++;
            }
        }

        return changed;
    }

    private static bool TryScaleAttribute(ref string attrs, string attributeName, double multiplier, bool preferInteger)
    {
        if (!TryGetAttributeValue(attrs, attributeName, out var rawValue))
        {
            return false;
        }

        var scaled = Math.Round(ParseDouble(rawValue, 0) * multiplier, preferInteger ? 0 : 6, MidpointRounding.AwayFromZero);
        return SetOrReplaceAttribute(ref attrs, attributeName, FormatNumeric(scaled, preferInteger));
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

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string InferCategory(string entryPath) =>
        entryPath.Contains("/_dlc/", StringComparison.OrdinalIgnoreCase) ? "DLC" : "Base";

    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string FormatNumeric(double value, bool preferInteger)
    {
        if (preferInteger || Math.Abs(value - Math.Round(value)) < 1e-9)
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

    private readonly record struct EngineAttributeValues(
        double Torque,
        double FuelConsumption,
        double DamageCapacity,
        double EngineResponsiveness,
        bool HasEngineResponsiveness);
}

public sealed record EngineSaveResult(int UpdatedFiles, int ChangedEngines);
