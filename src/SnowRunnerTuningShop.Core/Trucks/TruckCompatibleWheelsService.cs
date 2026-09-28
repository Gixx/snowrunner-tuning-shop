using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>
/// Per-truck CompatibleWheels editing: optional larger scales (+OffsetZ) and wheel-set Type assign/unassign.
/// Vanilla (baseline) scales stay locked; optional extras are capped at Scale 0.99.
/// </summary>
public static class TruckCompatibleWheelsService
{
    public const double MaxExtraScale = 0.99;
    public const double InchesPerScale = 79.0;
    private const double ScaleEpsilon = 0.0005;
    private const double MinStep = 0.03;
    private const double MaxStep = 0.05;
    private const double SingleSizeStep = 0.05;

    private static readonly Regex CompatibleWheelsRegex = new(
        @"<CompatibleWheels\b(?<attrs>[^<>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TruckTireOpenTagRegex = new(
        @"<TruckTire\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TruckWheelsOpenTagRegex = new(
        @"<TruckWheels\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UiNameRegex = new(
        @"UiName\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool HasCompatibleWheels(string pakPath, string truckEntryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        using var archive = ZipFile.OpenRead(pakPath);
        var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

        return CompatibleWheelsRegex.IsMatch(PartXmlHelpers.ReadEntryUtf8(truckEntry));
    }

    public static IReadOnlyList<string> GetAssignedSetIds(string pakPath, string truckEntryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        using var archive = ZipFile.OpenRead(pakPath);
        var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

        return ParseEntries(PartXmlHelpers.ReadEntryUtf8(truckEntry))
            .Select(e => e.Type)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static TruckWheelSizesSnapshot LoadSizes(string pakPath, string truckEntryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        using var workingArchive = ZipFile.OpenRead(pakPath);
        using var baselineArchive = ZipFile.OpenRead(baselinePath);

        var workingEntry = PakEntryLocator.FindEntry(workingArchive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);
        var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the baseline pak.", truckEntryPath);

        var workingText = PartXmlHelpers.ReadEntryUtf8(workingEntry);
        var baselineText = PartXmlHelpers.ReadEntryUtf8(baselineEntry);
        return BuildSizesSnapshot(
            workingEntry.FullName.Replace('\\', '/'),
            workingText,
            baselineText);
    }

    public static TruckWheelSetsSnapshot LoadSets(
        string pakPath,
        string truckEntryPath,
        string language = "english")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        using var archive = ZipFile.OpenRead(pakPath);
        var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

        var truckText = PartXmlHelpers.ReadEntryUtf8(truckEntry);
        var assigned = ParseEntries(truckText)
            .Select(e => e.Type)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var assignedLookup = new HashSet<string>(assigned, StringComparer.OrdinalIgnoreCase);
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        var catalog = LoadWheelSetCatalog(archive, strings);
        var dualRearIds = new HashSet<string>(
            catalog.Where(pair => pair.Value.HasWidthRear).Select(pair => pair.Key),
            StringComparer.OrdinalIgnoreCase);

        var rows = new List<TruckWheelSetRow>(catalog.Count);
        foreach (var (setId, tires) in catalog.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            var (noteKind, relatedSetId) = ResolveNote(setId, tires.HasWidthRear, dualRearIds);
            rows.Add(new TruckWheelSetRow(
                setId,
                tires.Names,
                tires.Labels,
                assignedLookup.Contains(setId),
                noteKind,
                relatedSetId));
        }

        var ordered = new List<TruckWheelSetRow>(rows.Count);
        foreach (var setId in assigned)
        {
            var row = rows.FirstOrDefault(r => r.SetId.Equals(setId, StringComparison.OrdinalIgnoreCase));
            if (row is not null)
            {
                ordered.Add(row);
            }
        }

        foreach (var row in rows)
        {
            if (!assignedLookup.Contains(row.SetId))
            {
                ordered.Add(row);
            }
        }

        return new TruckWheelSetsSnapshot(
            truckEntry.FullName.Replace('\\', '/'),
            ordered,
            CompatibleWheelsRegex.IsMatch(truckText));
    }

    public static TruckTuningSaveResult ApplySizes(
        string pakPath,
        string truckEntryPath,
        IReadOnlyList<double> enabledExtraScales,
        IReadOnlyList<TruckWheelOffsetEdit> offsets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);
        ArgumentNullException.ThrowIfNull(enabledExtraScales);
        ArgumentNullException.ThrowIfNull(offsets);

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        Dictionary<string, byte[]> replacements;
        using (var workingArchive = ZipFile.OpenRead(pakPath))
        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        {
            var workingEntry = PakEntryLocator.FindEntry(workingArchive, truckEntryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);
            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, truckEntryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the baseline pak.", truckEntryPath);

            var workingText = PartXmlHelpers.ReadEntryUtf8(workingEntry);
            var baselineText = PartXmlHelpers.ReadEntryUtf8(baselineEntry);
            var updated = ApplySizesToText(workingText, baselineText, enabledExtraScales, offsets);
            var key = workingEntry.FullName.Replace('\\', '/');
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (!string.Equals(workingText, updated, StringComparison.Ordinal))
            {
                replacements[key] = Encoding.UTF8.GetBytes(updated);
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles, updatedFiles > 0 ? 1 : 0);
    }

    public static TruckTuningSaveResult ApplySets(
        string pakPath,
        string truckEntryPath,
        IReadOnlyList<string> selectedSetIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);
        ArgumentNullException.ThrowIfNull(selectedSetIds);

        var normalizedSelected = NormalizeSelectedSetIds(selectedSetIds);
        if (normalizedSelected.Count == 0)
        {
            throw new InvalidOperationException("Select at least one wheel set.");
        }

        Dictionary<string, byte[]> replacements;
        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

            var catalog = LoadWheelSetCatalog(archive, strings: null);
            foreach (var setId in normalizedSelected)
            {
                if (!catalog.ContainsKey(setId))
                {
                    throw new InvalidOperationException($"Unknown wheel set '{setId}'.");
                }
            }

            var truckText = PartXmlHelpers.ReadEntryUtf8(truckEntry);
            var updated = ApplySetsToText(truckText, normalizedSelected);
            var key = truckEntry.FullName.Replace('\\', '/');
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            if (!string.Equals(truckText, updated, StringComparison.Ordinal))
            {
                replacements[key] = Encoding.UTF8.GetBytes(updated);
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles, updatedFiles > 0 ? 1 : 0);
    }

    /// <summary>Test hook: size/offset rewrite against baseline + working XML text.</summary>
    internal static string ApplySizesToTextForTests(
        string workingXml,
        string baselineXml,
        IReadOnlyList<double> enabledExtraScales,
        IReadOnlyList<TruckWheelOffsetEdit> offsets) =>
        ApplySizesToText(workingXml, baselineXml, enabledExtraScales, offsets);

    /// <summary>Test hook: set assign/unassign rewrite.</summary>
    internal static string ApplySetsToTextForTests(
        string truckXml,
        IReadOnlyList<string> selectedSetIds) =>
        ApplySetsToText(truckXml, NormalizeSelectedSetIds(selectedSetIds));

    internal static TruckWheelSizesSnapshot BuildSizesSnapshotForTests(string workingXml, string baselineXml) =>
        BuildSizesSnapshot("test.xml", workingXml, baselineXml);

    internal static IReadOnlyList<double> ComputeExtraScaleCandidatesForTests(IReadOnlyList<double> vanillaScales) =>
        ComputeExtraScaleCandidates(vanillaScales);

    internal static string FormatInchesLabelForTests(double scale) => FormatInchesLabel(scale);

    private static TruckWheelSizesSnapshot BuildSizesSnapshot(
        string truckEntryPath,
        string workingXml,
        string baselineXml)
    {
        var baselineEntries = ParseEntries(baselineXml);
        var workingEntries = ParseEntries(workingXml);
        var vanillaScales = baselineEntries
            .Select(e => e.Scale)
            .Distinct(ScaleEqualityComparer.Instance)
            .OrderBy(s => s)
            .ToArray();
        var candidates = ComputeExtraScaleCandidates(vanillaScales);
        var workingScales = workingEntries
            .Select(e => e.Scale)
            .Distinct(ScaleEqualityComparer.Instance)
            .ToArray();
        var assignedTypes = workingEntries
            .Select(e => e.Type)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var sizeOptions = new List<TruckWheelSizeOption>(vanillaScales.Length + candidates.Count);
        foreach (var scale in vanillaScales)
        {
            sizeOptions.Add(new TruckWheelSizeOption(
                scale,
                FormatInchesLabel(scale),
                IsVanilla: true,
                IsEnabled: true,
                IsLocked: true));
        }

        foreach (var scale in candidates)
        {
            var enabled = workingScales.Any(s => ScalesEqual(s, scale));
            sizeOptions.Add(new TruckWheelSizeOption(
                scale,
                FormatInchesLabel(scale),
                IsVanilla: false,
                IsEnabled: enabled,
                IsLocked: false));
        }

        var offsetRows = new List<TruckWheelOffsetRow>();
        var defaultOffsetByType = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in assignedTypes)
        {
            defaultOffsetByType[type] = DefaultOffsetForType(workingEntries, type);
        }

        foreach (var scale in candidates)
        {
            var enabled = workingScales.Any(s => ScalesEqual(s, scale));
            if (!enabled)
            {
                continue;
            }

            foreach (var type in assignedTypes)
            {
                var existing = workingEntries.FirstOrDefault(e =>
                    ScalesEqual(e.Scale, scale)
                    && e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
                var offset = existing?.OffsetZ ?? defaultOffsetByType[type];
                offsetRows.Add(new TruckWheelOffsetRow(scale, FormatInchesLabel(scale), type, offset));
            }
        }

        return new TruckWheelSizesSnapshot(
            truckEntryPath,
            sizeOptions,
            offsetRows,
            defaultOffsetByType,
            assignedTypes.Length,
            CompatibleWheelsRegex.IsMatch(workingXml));
    }

    private static string ApplySizesToText(
        string workingXml,
        string baselineXml,
        IReadOnlyList<double> enabledExtraScales,
        IReadOnlyList<TruckWheelOffsetEdit> offsets)
    {
        var baselineEntries = ParseEntries(baselineXml);
        if (baselineEntries.Count == 0)
        {
            throw new InvalidOperationException("This truck has no CompatibleWheels in the baseline.");
        }

        var vanillaScales = baselineEntries
            .Select(e => e.Scale)
            .Distinct(ScaleEqualityComparer.Instance)
            .ToArray();
        var candidates = ComputeExtraScaleCandidates(vanillaScales);
        var selectedExtras = NormalizeExtraScales(enabledExtraScales, candidates);

        var workingEntries = ParseEntries(workingXml);
        var types = workingEntries
            .Select(e => e.Type)
            .Where(t => t.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(t => t, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (types.Length == 0)
        {
            throw new InvalidOperationException("This truck has no CompatibleWheels types.");
        }

        var offsetLookup = BuildOffsetLookup(offsets);
        var desiredExtras = new List<WheelEntry>();
        foreach (var scale in selectedExtras)
        {
            foreach (var type in types)
            {
                var key = OffsetKey(scale, type);
                double? offset;
                if (offsetLookup.TryGetValue(key, out var edited))
                {
                    offset = edited;
                }
                else
                {
                    var existing = workingEntries.FirstOrDefault(e =>
                        ScalesEqual(e.Scale, scale)
                        && e.Type.Equals(type, StringComparison.OrdinalIgnoreCase));
                    offset = existing?.OffsetZ ?? DefaultOffsetForType(workingEntries, type);
                }

                desiredExtras.Add(new WheelEntry(type, scale, offset));
            }
        }

        return RewriteCompatibleWheels(
            workingXml,
            keep: e => vanillaScales.Any(v => ScalesEqual(v, e.Scale)),
            extrasToEnsure: desiredExtras);
    }

    private static string ApplySetsToText(string truckXml, IReadOnlyList<string> selectedSetIds)
    {
        if (selectedSetIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one wheel set.");
        }

        var existing = ParseEntries(truckXml);
        if (existing.Count == 0)
        {
            throw new InvalidOperationException("This truck has no CompatibleWheels.");
        }

        var selectedLookup = new HashSet<string>(selectedSetIds, StringComparer.OrdinalIgnoreCase);
        var scales = existing
            .Select(e => e.Scale)
            .Distinct(ScaleEqualityComparer.Instance)
            .OrderBy(s => s)
            .ToArray();

        // Template OffsetZ per scale: prefer an entry we are keeping, else any existing at that scale.
        var offsetByScale = new Dictionary<double, double?>(ScaleEqualityComparer.Instance);
        foreach (var scale in scales)
        {
            var kept = existing.FirstOrDefault(e =>
                ScalesEqual(e.Scale, scale) && selectedLookup.Contains(e.Type));
            if (kept is not null)
            {
                offsetByScale[scale] = kept.OffsetZ;
                continue;
            }

            var any = existing.FirstOrDefault(e => ScalesEqual(e.Scale, scale));
            offsetByScale[scale] = any?.OffsetZ;
        }

        var extrasToEnsure = new List<WheelEntry>();
        foreach (var setId in selectedSetIds)
        {
            foreach (var scale in scales)
            {
                var existingForPair = existing.FirstOrDefault(e =>
                    ScalesEqual(e.Scale, scale)
                    && e.Type.Equals(setId, StringComparison.OrdinalIgnoreCase));
                if (existingForPair is not null)
                {
                    continue;
                }

                offsetByScale.TryGetValue(scale, out var offset);
                extrasToEnsure.Add(new WheelEntry(setId, scale, offset));
            }
        }

        return RewriteCompatibleWheels(
            truckXml,
            keep: e => selectedLookup.Contains(e.Type),
            extrasToEnsure: extrasToEnsure);
    }

    private static string RewriteCompatibleWheels(
        string truckXml,
        Func<WheelEntry, bool> keep,
        IReadOnlyList<WheelEntry> extrasToEnsure)
    {
        var matches = CompatibleWheelsRegex.Matches(truckXml);
        if (matches.Count == 0)
        {
            throw new InvalidOperationException("This truck has no CompatibleWheels.");
        }

        // Remove from the end so indices stay valid.
        var result = truckXml;
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var match = matches[i];
            var entry = ParseEntry(match.Groups["attrs"].Value);
            if (keep(entry))
            {
                continue;
            }

            result = RemoveTagAndTrailingWhitespace(result, match.Index, match.Length);
        }

        // Re-parse after removals for upserts.
        matches = CompatibleWheelsRegex.Matches(result);
        var present = matches
            .Select(m => ParseEntry(m.Groups["attrs"].Value))
            .ToList();

        var toInsert = new List<string>();
        foreach (var desired in extrasToEnsure)
        {
            var existingIndex = present.FindIndex(e =>
                ScalesEqual(e.Scale, desired.Scale)
                && e.Type.Equals(desired.Type, StringComparison.OrdinalIgnoreCase));
            if (existingIndex >= 0)
            {
                var current = present[existingIndex];
                if (NullableDoubleEquals(current.OffsetZ, desired.OffsetZ))
                {
                    continue;
                }

                // Replace the matching tag's OffsetZ in-place.
                var match = CompatibleWheelsRegex.Matches(result)
                    .Cast<Match>()
                    .First(m =>
                    {
                        var e = ParseEntry(m.Groups["attrs"].Value);
                        return ScalesEqual(e.Scale, desired.Scale)
                            && e.Type.Equals(desired.Type, StringComparison.OrdinalIgnoreCase);
                    });
                var replacement = FormatTag(desired);
                result = string.Concat(
                    result.AsSpan(0, match.Index),
                    replacement,
                    result.AsSpan(match.Index + match.Length));
                present[existingIndex] = desired;
                continue;
            }

            toInsert.Add(FormatTag(desired));
            present.Add(desired);
        }

        if (toInsert.Count == 0)
        {
            return result;
        }

        matches = CompatibleWheelsRegex.Matches(result);
        var last = matches[^1];
        var indent = DetectIndent(result, last.Index);
        var insertion = new StringBuilder();
        foreach (var tag in toInsert)
        {
            insertion.Append('\n').Append(indent).Append(tag);
        }

        var insertAt = last.Index + last.Length;
        return string.Concat(result.AsSpan(0, insertAt), insertion.ToString(), result.AsSpan(insertAt));
    }

    private static string RemoveTagAndTrailingWhitespace(string text, int index, int length)
    {
        var end = index + length;
        // Eat following newline + indent so we do not leave blank clutter; keep one newline if preceding has content.
        while (end < text.Length && (text[end] == ' ' || text[end] == '\t'))
        {
            end++;
        }

        if (end < text.Length && text[end] == '\r')
        {
            end++;
        }

        if (end < text.Length && text[end] == '\n')
        {
            end++;
        }

        // Also trim spaces left on the same line before the tag when the whole line becomes empty.
        var start = index;
        var lineStart = text.LastIndexOf('\n', Math.Max(0, index - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var onlyWhitespaceBefore = true;
        for (var i = lineStart; i < index; i++)
        {
            if (text[i] is not (' ' or '\t'))
            {
                onlyWhitespaceBefore = false;
                break;
            }
        }

        if (onlyWhitespaceBefore)
        {
            start = lineStart;
        }

        return string.Concat(text.AsSpan(0, start), text.AsSpan(end));
    }

    private static string DetectIndent(string text, int tagIndex)
    {
        var lineStart = text.LastIndexOf('\n', Math.Max(0, tagIndex - 1));
        lineStart = lineStart < 0 ? 0 : lineStart + 1;
        var indentEnd = tagIndex;
        return text[lineStart..indentEnd];
    }

    private static string FormatTag(WheelEntry entry)
    {
        var scale = FormatScale(entry.Scale);
        if (entry.OffsetZ is double offset)
        {
            return $"<CompatibleWheels OffsetZ=\"{FormatOffset(offset)}\" Scale=\"{scale}\" Type=\"{entry.Type}\" />";
        }

        return $"<CompatibleWheels Scale=\"{scale}\" Type=\"{entry.Type}\" />";
    }

    private static string FormatScale(double scale) =>
        scale.ToString("0.###", CultureInfo.InvariantCulture);

    private static string FormatOffset(double offset) =>
        offset.ToString("0.###", CultureInfo.InvariantCulture);

    public static string FormatInchesLabel(double scale)
    {
        var inches = Math.Round(scale * InchesPerScale, MidpointRounding.AwayFromZero);
        return string.Create(CultureInfo.InvariantCulture, $"{inches}\"");
    }

    private static IReadOnlyList<double> ComputeExtraScaleCandidates(IReadOnlyList<double> vanillaScales)
    {
        if (vanillaScales.Count == 0)
        {
            return [];
        }

        var sorted = vanillaScales
            .Distinct(ScaleEqualityComparer.Instance)
            .OrderBy(s => s)
            .ToArray();
        var max = sorted[^1];
        double step;
        if (sorted.Length >= 2)
        {
            var gaps = new List<double>(sorted.Length - 1);
            for (var i = 1; i < sorted.Length; i++)
            {
                gaps.Add(sorted[i] - sorted[i - 1]);
            }

            gaps.Sort();
            step = gaps[gaps.Count / 2];
            step = Math.Clamp(step, MinStep, MaxStep);
        }
        else
        {
            step = SingleSizeStep;
        }

        var result = new List<double>(2);
        for (var i = 1; i <= 2; i++)
        {
            var candidate = Math.Round(max + (step * i), 3, MidpointRounding.AwayFromZero);
            if (candidate <= MaxExtraScale + ScaleEpsilon)
            {
                // Clamp display/storage to MaxExtraScale if floating edge.
                if (candidate > MaxExtraScale)
                {
                    candidate = MaxExtraScale;
                }

                if (!result.Any(s => ScalesEqual(s, candidate))
                    && !sorted.Any(s => ScalesEqual(s, candidate)))
                {
                    result.Add(candidate);
                }
            }
        }

        return result;
    }

    private static List<double> NormalizeExtraScales(
        IReadOnlyList<double> enabledExtraScales,
        IReadOnlyList<double> candidates)
    {
        var result = new List<double>();
        foreach (var raw in enabledExtraScales)
        {
            // FirstOrDefault on double returns 0 when missing — use NaN sentinel.
            var found = candidates.FirstOrDefault(c => ScalesEqual(c, raw), double.NaN);
            if (double.IsNaN(found) || found > MaxExtraScale + ScaleEpsilon)
            {
                continue;
            }

            if (!result.Any(s => ScalesEqual(s, found)))
            {
                result.Add(found);
            }
        }

        result.Sort();
        return result;
    }

    private static Dictionary<string, double?> BuildOffsetLookup(IReadOnlyList<TruckWheelOffsetEdit> offsets)
    {
        var result = new Dictionary<string, double?>(StringComparer.OrdinalIgnoreCase);
        foreach (var edit in offsets)
        {
            result[OffsetKey(edit.Scale, edit.Type)] = edit.OffsetZ;
        }

        return result;
    }

    private static string OffsetKey(double scale, string type) =>
        string.Create(CultureInfo.InvariantCulture, $"{scale:0.###}|{type}");

    private static double? DefaultOffsetForType(IReadOnlyList<WheelEntry> entries, string type)
    {
        WheelEntry? best = null;
        foreach (var entry in entries)
        {
            if (!entry.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (best is null || entry.Scale > best.Scale)
            {
                best = entry;
            }
        }

        return best?.OffsetZ;
    }

    private static List<WheelEntry> ParseEntries(string truckXml)
    {
        var result = new List<WheelEntry>();
        foreach (Match match in CompatibleWheelsRegex.Matches(truckXml))
        {
            result.Add(ParseEntry(match.Groups["attrs"].Value));
        }

        return result;
    }

    private static WheelEntry ParseEntry(string attrs)
    {
        var parsed = ParseAttributes(attrs);
        parsed.TryGetValue("Type", out var type);
        parsed.TryGetValue("Scale", out var scaleRaw);
        parsed.TryGetValue("OffsetZ", out var offsetRaw);

        if (!TryParseDouble(scaleRaw, out var scale))
        {
            scale = 0;
        }

        double? offset = null;
        if (TryParseDouble(offsetRaw, out var offsetValue))
        {
            offset = offsetValue;
        }

        return new WheelEntry(type?.Trim() ?? "", scale, offset);
    }

    internal static IReadOnlyList<string> ExtractTireLabelsForTests(
        string content,
        IReadOnlyDictionary<string, string>? strings) =>
        ExtractTires(content, strings).Labels;

    internal static (TruckWheelSetNoteKind Kind, string? RelatedSetId) ResolveNoteForTests(
        string setId,
        bool hasWidthRear,
        IEnumerable<string> dualRearSetIds) =>
        ResolveNote(
            setId,
            hasWidthRear,
            new HashSet<string>(dualRearSetIds, StringComparer.OrdinalIgnoreCase));

    private static Dictionary<string, WheelSetCatalogEntry> LoadWheelSetCatalog(
        ZipArchive archive,
        IReadOnlyDictionary<string, string>? strings)
    {
        var catalog = new Dictionary<string, WheelSetCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsWheelSetEntry(entryPath))
            {
                continue;
            }

            var setId = Path.GetFileNameWithoutExtension(entryPath);
            var content = PartXmlHelpers.ReadEntryUtf8(entry);
            var tires = ExtractTires(content, strings);
            if (tires.Names.Count == 0)
            {
                continue;
            }

            catalog[setId] = tires with { HasWidthRear = HasWidthRearAttribute(content) };
        }

        return catalog;
    }

    private static bool HasWidthRearAttribute(string content)
    {
        var match = TruckWheelsOpenTagRegex.Match(content);
        if (!match.Success)
        {
            return false;
        }

        return match.Groups["attrs"].Value.Contains("WidthRear", StringComparison.OrdinalIgnoreCase);
    }

    private static (TruckWheelSetNoteKind Kind, string? RelatedSetId) ResolveNote(
        string setId,
        bool hasWidthRear,
        IReadOnlySet<string> dualRearIds)
    {
        if (setId.EndsWith("_front", StringComparison.OrdinalIgnoreCase))
        {
            var sibling = setId[..^"_front".Length];
            if (sibling.Length > 0 && dualRearIds.Contains(sibling))
            {
                return (TruckWheelSetNoteKind.SingleWidthDualVariant, sibling);
            }
        }

        if (hasWidthRear)
        {
            return (TruckWheelSetNoteKind.DualRear, null);
        }

        return (TruckWheelSetNoteKind.None, null);
    }

    private static WheelSetCatalogEntry ExtractTires(
        string content,
        IReadOnlyDictionary<string, string>? strings)
    {
        var names = new List<string>();
        var labels = new List<string>();
        var matches = TruckTireOpenTagRegex.Matches(content);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            if (IsInsideTemplatesSection(content, match.Index))
            {
                continue;
            }

            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            name = name.Trim();
            var next = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var block = content[match.Index..next];
            var uiMatch = UiNameRegex.Match(block);
            var uiKey = uiMatch.Success ? uiMatch.Groups["value"].Value.Trim() : "";
            var label = strings is null
                ? name
                : GameStringsReader.Resolve(strings, uiKey, name);

            if (!names.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                names.Add(name);
                labels.Add(label);
            }
        }

        return new WheelSetCatalogEntry(names, labels, HasWidthRear: false);
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

    private static bool IsWheelSetEntry(string entryPath) =>
        entryPath.Contains("/classes/wheels/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static List<string> NormalizeSelectedSetIds(IReadOnlyList<string> selectedSetIds)
    {
        var result = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in selectedSetIds)
        {
            var setId = raw?.Trim() ?? "";
            if (setId.Length == 0 || !seen.Add(setId))
            {
                continue;
            }

            result.Add(setId);
        }

        return result;
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

    private static bool TryParseDouble(string? raw, out double value)
    {
        value = 0;
        return !string.IsNullOrWhiteSpace(raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static bool ScalesEqual(double a, double b) => Math.Abs(a - b) < ScaleEpsilon;

    private static bool NullableDoubleEquals(double? a, double? b)
    {
        if (a is null && b is null)
        {
            return true;
        }

        if (a is null || b is null)
        {
            return false;
        }

        return Math.Abs(a.Value - b.Value) < ScaleEpsilon;
    }

    private sealed record WheelEntry(string Type, double Scale, double? OffsetZ);

    private sealed class ScaleEqualityComparer : IEqualityComparer<double>
    {
        public static readonly ScaleEqualityComparer Instance = new();

        public bool Equals(double x, double y) => ScalesEqual(x, y);

        public int GetHashCode(double obj) => Math.Round(obj, 3, MidpointRounding.AwayFromZero).GetHashCode();
    }
}

public sealed record TruckWheelSizeOption(
    double Scale,
    string InchesLabel,
    bool IsVanilla,
    bool IsEnabled,
    bool IsLocked);

public sealed record TruckWheelOffsetRow(
    double Scale,
    string InchesLabel,
    string Type,
    double? OffsetZ);

public sealed record TruckWheelOffsetEdit(
    double Scale,
    string Type,
    double? OffsetZ);

public sealed record TruckWheelSizesSnapshot(
    string TruckEntryPath,
    IReadOnlyList<TruckWheelSizeOption> Sizes,
    IReadOnlyList<TruckWheelOffsetRow> OffsetRows,
    IReadOnlyDictionary<string, double?> DefaultOffsetByType,
    int AssignedSetCount,
    bool HasCompatibleWheels);

public enum TruckWheelSetNoteKind
{
    None = 0,
    /// <summary>Set declares WidthRear — front single, rear twin tires.</summary>
    DualRear = 1,
    /// <summary>Filename ends with _front and a dual-rear sibling set exists.</summary>
    SingleWidthDualVariant = 2,
}

public sealed record TruckWheelSetRow(
    string SetId,
    IReadOnlyList<string> TireNames,
    IReadOnlyList<string> TireLabels,
    bool IsAssigned,
    TruckWheelSetNoteKind NoteKind = TruckWheelSetNoteKind.None,
    string? RelatedSetId = null)
{
    public string TireNamesText => string.Join(", ", TireLabels);
}

public sealed record TruckWheelSetsSnapshot(
    string TruckEntryPath,
    IReadOnlyList<TruckWheelSetRow> Sets,
    bool HasCompatibleWheels)
{
    public int AssignedCount => Sets.Count(set => set.IsAssigned);
}

internal sealed record WheelSetCatalogEntry(
    IReadOnlyList<string> Names,
    IReadOnlyList<string> Labels,
    bool HasWidthRear);
