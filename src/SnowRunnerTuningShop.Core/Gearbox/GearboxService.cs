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

namespace SnowRunnerTuningShop.Core.Gearbox;

public static class GearboxService
{
    private static readonly Regex GearboxOpenTagRegex = new(
        @"<Gearbox\b(?<attrs>[^>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex GearboxSocketRegex = new(
        @"<GearboxSocket\b(?<attrs>[^>]*)/?>",
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

            gearboxes.AddRange(ParseGearboxesFromText(entryPath, ReadEntryText(entry), strings, setUsage));
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
        ValidateMultiplier(fuelConsumptionMultiplier, nameof(fuelConsumptionMultiplier));
        ValidateMultiplier(idleFuelModifierMultiplier, nameof(idleFuelModifierMultiplier));
        ValidateMultiplier(awdConsumptionMultiplier, nameof(awdConsumptionMultiplier));

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedGearboxes = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsGearboxEntry(entryPath))
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
                    fuelConsumptionMultiplier,
                    idleFuelModifierMultiplier,
                    awdConsumptionMultiplier);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedGearboxes += CountNamedDifferences(currentText, updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new GearboxSaveResult(updatedFiles, changedGearboxes);
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

                var text = ReadEntryText(entry);
                var updates = group.ToDictionary(
                    gearbox => gearbox.Name,
                    gearbox => new GearboxAttributeValues(
                        gearbox.FuelConsumption,
                        gearbox.IdleFuelModifier,
                        gearbox.AwdConsumptionModifier),
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
                FuelConsumption = ParseDouble(attrs.GetValueOrDefault("FuelConsumption"), 0),
                IdleFuelModifier = ParseDouble(attrs.GetValueOrDefault("IdleFuelModifier"), 0),
                AwdConsumptionModifier = hasAwd
                    ? ParseDouble(attrs["AWDConsumptionModifier"], 0)
                    : null,
            });
        }

        return gearboxes;
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
            var text = ReadEntryText(entry);
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
            ? "No trucks reference this gearbox set."
            : string.Join(", ", vehicleNames);

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

        var localChanged = 0;
        var result = GearboxOpenTagRegex.Replace(content, match =>
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
            changed |= SetOrReplaceAttribute(ref updatedAttrs, "FuelConsumption", FormatNumeric(target.FuelConsumption));
            changed |= SetOrReplaceAttribute(ref updatedAttrs, "IdleFuelModifier", FormatNumeric(target.IdleFuelModifier));

            if (target.AwdConsumptionModifier.HasValue || AttributeExists(updatedAttrs, "AWDConsumptionModifier"))
            {
                changed |= SetOrReplaceAttribute(
                    ref updatedAttrs,
                    "AWDConsumptionModifier",
                    FormatNumeric(target.AwdConsumptionModifier ?? 0));
            }

            if (!changed)
            {
                return match.Value;
            }

            localChanged++;
            return $"<Gearbox{updatedAttrs}{self}>";
        });

        if (localChanged == 0)
        {
            return false;
        }

        updatedText = result;
        changedGearboxes = localChanged;
        return true;
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

            if (Math.Abs(existing.FuelConsumption - target.FuelConsumption) > 1e-6
                || Math.Abs(existing.IdleFuelModifier - target.IdleFuelModifier) > 1e-6
                || !NullableDoubleEquals(existing.AwdConsumptionModifier, target.AwdConsumptionModifier))
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

        var scaled = Math.Round(ParseDouble(rawValue, 0) * multiplier, 6, MidpointRounding.AwayFromZero);
        return SetOrReplaceAttribute(ref attrs, attributeName, FormatNumeric(scaled));
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

    private readonly record struct GearboxAttributeValues(
        double FuelConsumption,
        double IdleFuelModifier,
        double? AwdConsumptionModifier);

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
