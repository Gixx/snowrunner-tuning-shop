using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>
/// Truck &lt;Sounds&gt; horn / engine bank assignment. Paths like <c>trucks/ford_f750/ford_f750_idle</c>
/// resolve to <c>[sound]/trucks/...</c> entries inside <c>shared_sound.pak</c> beside <c>initial.pak</c>.
/// </summary>
public static class TruckSoundsService
{
    /// <summary>Engine-related &lt;Sounds&gt; child tags (Honk is separate).</summary>
    public static readonly string[] EngineSoundTags =
    [
        "EngineStart",
        "EngineStop",
        "EngineIdle",
        "EngineIdle_2d",
        "EngineLow",
        "EngineLow_2d",
        "EngineHigh",
        "EngineHigh_2d",
        "EngineHeavy",
        "EngineHeavy_2d",
        "EngineRev",
        "EngineAccel",
        "EngineTrans",
        "EngineTurbo",
        "EngineStalling",
        "DamagedEngine",
    ];

    private static readonly HashSet<string> EngineTagSet = new(EngineSoundTags, StringComparer.OrdinalIgnoreCase);

    private static readonly Regex SoundsBlockRegex = new(
        @"(?is)(?<open><Sounds\b[^>]*>)(?<body>.*?)(?<close></Sounds>)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SoundChildRegex = new(
        @"<(?<tag>[A-Za-z_][A-Za-z0-9_]*)\b(?<attrs>[^<>]*?)\s*(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex SoundAttrRegex = new(
        @"Sound\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SoundSetPathRegex = new(
        @"^trucks/(?<set>[^/]+)/",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static string? TryResolveSharedSoundPakPath(string initialPakPath)
    {
        if (string.IsNullOrWhiteSpace(initialPakPath))
        {
            return null;
        }

        var dir = Path.GetDirectoryName(Path.GetFullPath(initialPakPath));
        if (string.IsNullOrWhiteSpace(dir))
        {
            return null;
        }

        var candidate = Path.Combine(dir, "shared_sound.pak");
        return File.Exists(candidate) ? candidate : null;
    }

    public static bool CanPreviewSounds(string initialPakPath) =>
        TryResolveSharedSoundPakPath(initialPakPath) is not null;

    /// <summary>Logical XML Sound path → WAV bytes from shared_sound.pak (files are RIFF WAVE with .pcm extension).</summary>
    public static bool TryReadSoundWav(string initialPakPath, string logicalSoundPath, out byte[] wavBytes)
    {
        wavBytes = [];
        if (string.IsNullOrWhiteSpace(logicalSoundPath))
        {
            return false;
        }

        var soundPak = TryResolveSharedSoundPakPath(initialPakPath);
        if (soundPak is null)
        {
            return false;
        }

        var normalized = logicalSoundPath.Replace('\\', '/').Trim().TrimStart('/');
        var entryPath = $"[sound]/{normalized}.pcm";
        using var archive = ZipFile.OpenRead(soundPak);
        var entry = PakEntryLocator.FindEntry(archive, entryPath);
        if (entry is null)
        {
            return false;
        }

        using var stream = entry.Open();
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        wavBytes = ms.ToArray();
        return wavBytes.Length >= 12
            && wavBytes[0] == (byte)'R'
            && wavBytes[1] == (byte)'I'
            && wavBytes[2] == (byte)'F'
            && wavBytes[3] == (byte)'F';
    }

    public static TruckSoundCatalog LoadCatalog(string pakPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pakPath);

        using var archive = ZipFile.OpenRead(pakPath);
        return LoadCatalogFromArchive(archive);
    }

    public static TruckSoundCatalog LoadCatalogFromArchive(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var sets = new Dictionary<string, TruckSoundSetDefinition>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var entryPath = PakEntryLocator.NormalizeEntryPath(entry.FullName);
            if (!IsTruckXmlEntry(entryPath))
            {
                continue;
            }

            var text = PartXmlHelpers.ReadEntryUtf8(entry);
            if (!TryGetSoundsBody(text, out var body))
            {
                continue;
            }

            foreach (Match match in SoundChildRegex.Matches(body))
            {
                var tag = match.Groups["tag"].Value;
                var soundMatch = SoundAttrRegex.Match(match.Groups["attrs"].Value);
                if (!soundMatch.Success)
                {
                    continue;
                }

                var soundPath = soundMatch.Groups["value"].Value.Trim();
                if (soundPath.Length == 0)
                {
                    continue;
                }

                var setMatch = SoundSetPathRegex.Match(soundPath.Replace('\\', '/'));
                if (!setMatch.Success)
                {
                    continue;
                }

                var setId = setMatch.Groups["set"].Value;
                if (!sets.TryGetValue(setId, out var def))
                {
                    def = new TruckSoundSetDefinition(setId);
                    sets[setId] = def;
                }

                if (tag.Equals("Honk", StringComparison.OrdinalIgnoreCase))
                {
                    def.HonkPath ??= soundPath;
                }
                else if (EngineTagSet.Contains(tag))
                {
                    def.EnginePaths.TryAdd(tag, soundPath);
                }
            }
        }

        return new TruckSoundCatalog(
            sets.Values
                .Where(set => set.HonkPath is not null || set.EnginePaths.Count > 0)
                .OrderBy(set => set.Id, StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public static TruckSoundAssignment ReadAssignment(string truckXml)
    {
        if (!TryGetSoundsBody(truckXml, out var body))
        {
            return new TruckSoundAssignment(null, null, null, null, null);
        }

        string? honkPath = null;
        string? idlePath = null;
        string? highPath = null;
        var engineVotes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (Match match in SoundChildRegex.Matches(body))
        {
            var tag = match.Groups["tag"].Value;
            var soundMatch = SoundAttrRegex.Match(match.Groups["attrs"].Value);
            if (!soundMatch.Success)
            {
                continue;
            }

            var soundPath = soundMatch.Groups["value"].Value.Trim();
            if (soundPath.Length == 0)
            {
                continue;
            }

            var setId = TryGetSoundSetId(soundPath);
            if (tag.Equals("Honk", StringComparison.OrdinalIgnoreCase))
            {
                honkPath = soundPath;
            }
            else if (tag.Equals("EngineIdle", StringComparison.OrdinalIgnoreCase))
            {
                idlePath = soundPath;
            }
            else if (tag.Equals("EngineHigh", StringComparison.OrdinalIgnoreCase))
            {
                highPath = soundPath;
            }

            if (EngineTagSet.Contains(tag) && setId is not null)
            {
                engineVotes[setId] = engineVotes.GetValueOrDefault(setId) + 1;
            }
        }

        var hornSet = TryGetSoundSetId(honkPath);
        string? engineSet = null;
        if (engineVotes.Count > 0)
        {
            engineSet = engineVotes
                .OrderByDescending(pair => pair.Value)
                .ThenBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .First()
                .Key;
        }

        return new TruckSoundAssignment(hornSet, engineSet, honkPath, idlePath, highPath);
    }

    public static string ApplyAssignment(
        string truckXml,
        TruckSoundCatalog catalog,
        string? hornSoundSetId,
        string? engineSoundSetId)
    {
        ArgumentNullException.ThrowIfNull(catalog);

        var updated = truckXml;
        if (!string.IsNullOrWhiteSpace(hornSoundSetId)
            && catalog.TryGet(hornSoundSetId, out var hornSet)
            && !string.IsNullOrWhiteSpace(hornSet.HonkPath))
        {
            updated = ApplySoundChild(updated, "Honk", hornSet.HonkPath!);
        }

        if (!string.IsNullOrWhiteSpace(engineSoundSetId)
            && catalog.TryGet(engineSoundSetId, out var engineSet))
        {
            foreach (var tag in EngineSoundTags)
            {
                if (!engineSet.EnginePaths.TryGetValue(tag, out var path) || string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                updated = ApplySoundChild(updated, tag, path);
            }
        }

        return updated;
    }

    /// <summary>Test hook.</summary>
    internal static string ApplyAssignmentForTests(
        string truckXml,
        IReadOnlyDictionary<string, string> enginePaths,
        string? honkPath)
    {
        var set = new TruckSoundSetDefinition("test");
        if (honkPath is not null)
        {
            set.HonkPath = honkPath;
        }

        foreach (var pair in enginePaths)
        {
            set.EnginePaths[pair.Key] = pair.Value;
        }

        var catalog = new TruckSoundCatalog([set]);
        return ApplyAssignment(truckXml, catalog, honkPath is null ? null : "test", enginePaths.Count == 0 ? null : "test");
    }

    public static string? ResolvePreviewPath(TruckSoundCatalog catalog, string? soundSetId, string tag)
    {
        if (string.IsNullOrWhiteSpace(soundSetId) || !catalog.TryGet(soundSetId, out var set))
        {
            return null;
        }

        if (tag.Equals("Honk", StringComparison.OrdinalIgnoreCase))
        {
            return set.HonkPath;
        }

        return set.EnginePaths.TryGetValue(tag, out var path) ? path : null;
    }

    private static string ApplySoundChild(string truckXml, string tagName, string soundPath)
    {
        var blockMatch = SoundsBlockRegex.Match(truckXml);
        if (!blockMatch.Success)
        {
            return truckXml;
        }

        var body = blockMatch.Groups["body"].Value;
        var tagRegex = new Regex(
            $@"<(?<tag>{Regex.Escape(tagName)})\b(?<attrs>[^<>]*?)\s*/?>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        string updatedBody;
        var existing = tagRegex.Match(body);
        if (existing.Success)
        {
            var attrs = existing.Groups["attrs"].Value;
            var newAttrs = SoundAttrRegex.IsMatch(attrs)
                ? SoundAttrRegex.Replace(attrs, $@"Sound=""{soundPath}""", 1)
                : attrs.TrimEnd() + $@" Sound=""{soundPath}""";
            var replacement = $"<{tagName}{newAttrs} />";
            updatedBody = tagRegex.Replace(body, replacement, 1);
        }
        else
        {
            var insert = $"{Environment.NewLine}\t\t\t<{tagName}\t\tSound=\"{soundPath}\" />";
            updatedBody = body.TrimEnd() + insert + Environment.NewLine + "\t\t";
        }

        return string.Concat(
            truckXml.AsSpan(0, blockMatch.Groups["body"].Index),
            updatedBody,
            truckXml.AsSpan(blockMatch.Groups["body"].Index + blockMatch.Groups["body"].Length));
    }

    private static bool TryGetSoundsBody(string truckXml, out string body)
    {
        var match = SoundsBlockRegex.Match(truckXml);
        body = match.Success ? match.Groups["body"].Value : "";
        return match.Success;
    }

    private static string? TryGetSoundSetId(string? soundPath)
    {
        if (string.IsNullOrWhiteSpace(soundPath))
        {
            return null;
        }

        var match = SoundSetPathRegex.Match(soundPath.Replace('\\', '/'));
        return match.Success ? match.Groups["set"].Value : null;
    }

    private static bool IsTruckXmlEntry(string entryPath)
    {
        if (!entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (entryPath.Contains("/tuning/", StringComparison.OrdinalIgnoreCase)
            || entryPath.Contains("/addons/", StringComparison.OrdinalIgnoreCase)
            || entryPath.Contains("/trailer", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // .../classes/trucks/name.xml (not nested folders)
        var marker = "/classes/trucks/";
        var idx = entryPath.LastIndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var rest = entryPath[(idx + marker.Length)..];
        return rest.Length > 0 && !rest.Contains('/');
    }
}

public sealed class TruckSoundSetDefinition
{
    public TruckSoundSetDefinition(string id)
    {
        Id = id;
    }

    public string Id { get; }

    public string? HonkPath { get; set; }

    public Dictionary<string, string> EnginePaths { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool HasHonk => !string.IsNullOrWhiteSpace(HonkPath);

    public bool HasEngine => EnginePaths.Count > 0;
}

public sealed class TruckSoundCatalog
{
    private readonly Dictionary<string, TruckSoundSetDefinition> _byId;

    public TruckSoundCatalog(IReadOnlyList<TruckSoundSetDefinition> sets)
    {
        Sets = sets;
        _byId = sets.ToDictionary(set => set.Id, StringComparer.OrdinalIgnoreCase);
    }

    public IReadOnlyList<TruckSoundSetDefinition> Sets { get; }

    public IEnumerable<string> HornSetIds =>
        Sets.Where(set => set.HasHonk).Select(set => set.Id);

    public IEnumerable<string> EngineSetIds =>
        Sets.Where(set => set.HasEngine).Select(set => set.Id);

    public bool TryGet(string id, out TruckSoundSetDefinition set) =>
        _byId.TryGetValue(id, out set!);
}

public sealed record TruckSoundAssignment(
    string? HornSoundSetId,
    string? EngineSoundSetId,
    string? HonkPath,
    string? EngineIdlePath,
    string? EngineHighPath = null);
