using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>
/// Per-truck EngineSocket Type list (engine set files under classes/engines).
/// MVP: assign/unassign existing sets; does not create or edit set XML contents.
/// </summary>
public static class TruckEngineSetsService
{
    private static readonly Regex EngineSocketRegex = new(
        @"<EngineSocket\b(?<attrs>[^<>]*?)\s*/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex EngineOpenTagRegex = new(
        @"<Engine\b(?<attrs>[^<>/]*?)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UiNameRegex = new(
        @"UiName\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<string> GetAssignedSetIds(string pakPath, string truckEntryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        using var archive = ZipFile.OpenRead(pakPath);
        var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

        var (assignedSetIds, _) = ParseEngineSocket(PartXmlHelpers.ReadEntryUtf8(truckEntry));
        return assignedSetIds;
    }

    public static bool HasEngineSocket(string pakPath, string truckEntryPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(truckEntryPath);

        using var archive = ZipFile.OpenRead(pakPath);
        var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
            ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

        return EngineSocketRegex.IsMatch(PartXmlHelpers.ReadEntryUtf8(truckEntry));
    }

    public static TruckEngineSetsSnapshot Load(
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
        var (assignedSetIds, defaultEngine) = ParseEngineSocket(truckText);
        var hasEngineSocket = EngineSocketRegex.IsMatch(truckText);
        var assignedLookup = new HashSet<string>(assignedSetIds, StringComparer.OrdinalIgnoreCase);
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        var catalog = LoadEngineSetCatalog(archive, strings);

        var rows = new List<TruckEngineSetRow>(catalog.Count);
        foreach (var (setId, engines) in catalog.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            rows.Add(new TruckEngineSetRow(
                setId,
                engines.Names,
                engines.Labels,
                assignedLookup.Contains(setId)));
        }

        // Keep assigned sets first (truck Type order), then the rest A–Z.
        var ordered = new List<TruckEngineSetRow>(rows.Count);
        foreach (var setId in assignedSetIds)
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

        return new TruckEngineSetsSnapshot(
            truckEntry.FullName.Replace('\\', '/'),
            defaultEngine,
            ordered,
            hasEngineSocket);
    }

    public static TruckTuningSaveResult Apply(
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
            throw new InvalidOperationException("Select at least one engine set.");
        }

        Dictionary<string, byte[]> replacements;
        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var truckEntry = PakEntryLocator.FindEntry(archive, truckEntryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truckEntryPath);

            var truckText = PartXmlHelpers.ReadEntryUtf8(truckEntry);
            var catalog = LoadEngineSetCatalog(archive, strings: null);
            var setEngines = catalog.ToDictionary(
                pair => pair.Key,
                pair => (IReadOnlyList<string>)pair.Value.Names,
                StringComparer.OrdinalIgnoreCase);

            foreach (var setId in normalizedSelected)
            {
                if (!setEngines.ContainsKey(setId))
                {
                    throw new InvalidOperationException($"Unknown engine set '{setId}'.");
                }
            }

            var updated = ApplyEngineSocketSets(truckText, setEngines, normalizedSelected);
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

    /// <summary>Test hook: rewrite EngineSocket Type/Default in truck XML text.</summary>
    internal static string ApplyEngineSocketSetsForTests(
        string truckXml,
        IReadOnlyDictionary<string, IReadOnlyList<string>> setEngines,
        IReadOnlyList<string> selectedSetIds) =>
        ApplyEngineSocketSets(truckXml, setEngines, NormalizeSelectedSetIds(selectedSetIds));

    internal static (IReadOnlyList<string> SetIds, string? DefaultEngine) ParseEngineSocketForTests(string truckXml) =>
        ParseEngineSocket(truckXml);

    internal static IReadOnlyList<string> ExtractEngineLabelsForTests(
        string content,
        IReadOnlyDictionary<string, string>? strings) =>
        ExtractEngines(content, strings).Labels;

    private static string ApplyEngineSocketSets(
        string truckXml,
        IReadOnlyDictionary<string, IReadOnlyList<string>> setEngines,
        IReadOnlyList<string> selectedSetIds)
    {
        if (selectedSetIds.Count == 0)
        {
            throw new InvalidOperationException("Select at least one engine set.");
        }

        var match = EngineSocketRegex.Match(truckXml);
        if (!match.Success)
        {
            throw new InvalidOperationException("This truck has no EngineSocket.");
        }

        var attrs = match.Groups["attrs"].Value.TrimEnd();
        if (attrs.EndsWith('/'))
        {
            attrs = attrs[..^1].TrimEnd();
        }

        var parsed = ParseAttributes(attrs);
        parsed.TryGetValue("Default", out var currentDefault);

        var availableEngines = new List<string>();
        foreach (var setId in selectedSetIds)
        {
            if (!setEngines.TryGetValue(setId, out var engines))
            {
                throw new InvalidOperationException($"Unknown engine set '{setId}'.");
            }

            foreach (var engine in engines)
            {
                if (!availableEngines.Contains(engine, StringComparer.OrdinalIgnoreCase))
                {
                    availableEngines.Add(engine);
                }
            }
        }

        if (availableEngines.Count == 0)
        {
            throw new InvalidOperationException("Selected engine sets contain no engines.");
        }

        var nextDefault = availableEngines.Any(name =>
                string.Equals(name, currentDefault, StringComparison.OrdinalIgnoreCase))
            ? currentDefault!
            : availableEngines[0];

        var typeValue = string.Join(", ", selectedSetIds);
        var updatedAttrs = attrs;
        VehicleGameDataXml.SetOrReplaceAttribute(ref updatedAttrs, "Type", typeValue);
        VehicleGameDataXml.SetOrReplaceAttribute(ref updatedAttrs, "Default", nextDefault);

        var replacement = $"<EngineSocket{updatedAttrs} />";
        return string.Concat(
            truckXml.AsSpan(0, match.Index),
            replacement,
            truckXml.AsSpan(match.Index + match.Length));
    }

    private static (IReadOnlyList<string> SetIds, string? DefaultEngine) ParseEngineSocket(string truckXml)
    {
        var match = EngineSocketRegex.Match(truckXml);
        if (!match.Success)
        {
            return (Array.Empty<string>(), null);
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        attrs.TryGetValue("Default", out var defaultEngine);
        attrs.TryGetValue("Type", out var typeAttr);
        var setIds = SplitTypeList(typeAttr);
        return (setIds, string.IsNullOrWhiteSpace(defaultEngine) ? null : defaultEngine.Trim());
    }

    private static Dictionary<string, EngineSetCatalogEntry> LoadEngineSetCatalog(
        ZipArchive archive,
        IReadOnlyDictionary<string, string>? strings)
    {
        var catalog = new Dictionary<string, EngineSetCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsEngineSetEntry(entryPath))
            {
                continue;
            }

            var setId = Path.GetFileNameWithoutExtension(entryPath);
            var engines = ExtractEngines(PartXmlHelpers.ReadEntryUtf8(entry), strings);
            if (engines.Names.Count == 0)
            {
                continue;
            }

            catalog[setId] = engines;
        }

        return catalog;
    }

    private static EngineSetCatalogEntry ExtractEngines(
        string content,
        IReadOnlyDictionary<string, string>? strings)
    {
        var names = new List<string>();
        var labels = new List<string>();
        var matches = EngineOpenTagRegex.Matches(content);
        for (var i = 0; i < matches.Count; i++)
        {
            var match = matches[i];
            var attrs = ParseAttributes(match.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
            {
                continue;
            }

            name = name.Trim();
            names.Add(name);

            var next = i + 1 < matches.Count ? matches[i + 1].Index : content.Length;
            var block = content[match.Index..next];
            var uiMatch = UiNameRegex.Match(block);
            var uiKey = uiMatch.Success ? uiMatch.Groups["value"].Value.Trim() : "";
            labels.Add(
                strings is null
                    ? name
                    : GameStringsReader.Resolve(strings, uiKey, name));
        }

        return new EngineSetCatalogEntry(names, labels);
    }

    private static bool IsEngineSetEntry(string entryPath) =>
        entryPath.Contains("/classes/engines/", StringComparison.OrdinalIgnoreCase)
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

    private static List<string> SplitTypeList(string? typeAttr)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(typeAttr))
        {
            return result;
        }

        foreach (var part in typeAttr.Split(','))
        {
            var setId = part.Trim();
            if (setId.Length > 0)
            {
                result.Add(setId);
            }
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

    private sealed record EngineSetCatalogEntry(
        IReadOnlyList<string> Names,
        IReadOnlyList<string> Labels);
}

public sealed record TruckEngineSetRow(
    string SetId,
    IReadOnlyList<string> EngineNames,
    IReadOnlyList<string> EngineLabels,
    bool IsAssigned)
{
    public string EngineNamesText => string.Join(", ", EngineLabels);
}

public sealed record TruckEngineSetsSnapshot(
    string TruckEntryPath,
    string? DefaultEngineName,
    IReadOnlyList<TruckEngineSetRow> Sets,
    bool HasEngineSocket)
{
    public int AssignedCount => Sets.Count(set => set.IsAssigned);
}
