using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.General;

public static class GeneralService
{
    private const string ModelsSegment = "classes/models/";

    private static readonly Regex PrimaryModelTagRegex = new(
        @"<(?<tag>[A-Za-z_][\w:.-]*)\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ClipCameraAttributeRegex = new(
        @"(?<prefix>\bClipCamera\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex MassAttributeRegex = new(
        @"(?<prefix>\bMass\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex NoSoftContactsAttributeRegex = new(
        @"(?<prefix>\bNoSoftContacts\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex BreakOffThresholdAttributeRegex = new(
        @"(?<prefix>\bBreakOffThreshold\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SmallRockTemplateRegex = new(
        @"_template\s*=\s*""SmallRock""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TrailRockFileNameRegex = new(
        @"(small_rock|small_forest_rock|burnt_small_rock)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex DamageMultAttributeRegex = new(
        @"(?<prefix>\bDamageMult\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Base-game plants with bundled no-stones mod XML overrides.</summary>
    private static readonly string[] ModPlantFiles =
    [
        "small_rock_a.xml",
        "small_rock_a_rus.xml",
        "small_rock_b.xml",
        "small_rock_b_rus.xml",
        "small_rock_c.xml",
        "small_rock_c_rus.xml",
        "small_forest_rock_a.xml",
        "small_forest_rock_b.xml",
        "small_forest_rock_c.xml",
    ];

    private static readonly string[] RockMeshFiles =
    [
        "plants_small_rock_a",
        "plants_small_rock_a_rus",
        "plants_small_rock_b",
        "plants_small_rock_b_rus",
        "plants_small_rock_c",
        "plants_small_rock_c_rus",
        "plants_small_forest_rock_a",
        "plants_small_forest_rock_b",
        "plants_small_forest_rock_c",
    ];

    public static GeneralSettings LoadSettings(string pakPath, string? noStonesAssetsDirectory = null)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var camera = AnalyzeCameraCollisions(archive);
        var rockScale = EstimateRockSizeScale(pakPath, noStonesAssetsDirectory);

        return new GeneralSettings
        {
            CameraCollisionState = camera.State,
            CameraEligibleModels = camera.EligibleModels,
            RockSizeScale = rockScale,
            RockPlantFiles = ListTrailRockPlantPaths(archive).Count(),
        };
    }

    public static GeneralSaveResult ApplyCameraCollisions(string pakPath, CameraCollisionMode mode)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        Dictionary<string, byte[]> replacements;

        using (var currentArchive = ZipFile.OpenRead(pakPath))
        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        {
            replacements = BuildCameraCollisionReplacements(currentArchive, baselineArchive, mode);
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new GeneralSaveResult(updatedFiles);
    }

    public static GeneralSaveResult ApplyRockSize(
        string pakPath,
        double scale,
        string noStonesAssetsDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(noStonesAssetsDirectory);
        if (scale is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(scale), "Rock size scale must be between 0 and 1.");
        }

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        var modAssets = LoadNoStonesAssets(noStonesAssetsDirectory);
        Dictionary<string, byte[]> replacements;

        using (var currentArchive = ZipFile.OpenRead(pakPath))
        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        {
            replacements = BuildRockSizeReplacements(currentArchive, baselineArchive, modAssets, scale);
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new GeneralSaveResult(updatedFiles);
    }

    public static GeneralSaveResult RestoreCameraCollisionsFromBaseline(string pakPath) =>
        ApplyCameraCollisions(pakPath, CameraCollisionMode.Baseline);

    public static GeneralSaveResult RestoreRockSizeFromBaseline(string pakPath, string noStonesAssetsDirectory) =>
        ApplyRockSize(pakPath, 1.0, noStonesAssetsDirectory);

    private static Dictionary<string, byte[]> BuildCameraCollisionReplacements(
        ZipArchive currentArchive,
        ZipArchive baselineArchive,
        CameraCollisionMode mode)
    {
        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var entryPath in ListModelEntryPaths(currentArchive))
        {
            var currentText = ReadEntryText(currentArchive, entryPath);
            if (!IsCameraEligibleModel(currentText))
            {
                continue;
            }

            string updatedText;
            switch (mode)
            {
                case CameraCollisionMode.Baseline:
                    var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
                    if (baselineEntry is null)
                    {
                        continue;
                    }

                    updatedText = RestoreClipCameraFromBaseline(currentText, ReadEntryText(baselineEntry));
                    break;
                case CameraCollisionMode.CollisionsOn:
                    updatedText = SetClipCameraValue(currentText, "true");
                    break;
                case CameraCollisionMode.CollisionsOff:
                    updatedText = SetClipCameraValue(currentText, "false");
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unsupported camera collision mode.");
            }

            if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
            {
                replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
            }
        }

        return replacements;
    }

    private static Dictionary<string, byte[]> BuildRockSizeReplacements(
        ZipArchive currentArchive,
        ZipArchive baselineArchive,
        IReadOnlyDictionary<string, byte[]> modAssets,
        double scale)
    {
        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        foreach (var entryPath in ListTrailRockPlantPaths(currentArchive))
        {
            var plantFile = Path.GetFileName(entryPath);
            var modKey = $"plants/{plantFile}";

            var currentEntry = PakEntryLocator.FindEntry(currentArchive, entryPath);
            if (currentEntry is null)
            {
                continue;
            }

            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
            var baselineText = ReadEntryText(baselineEntry ?? currentEntry);
            var modText = modAssets.TryGetValue(modKey, out var modBytes)
                ? Encoding.UTF8.GetString(modBytes)
                : CreateSyntheticNoStonesPlantText(baselineText);

            var updatedText = scale switch
            {
                <= 0.0000001 => modText,
                >= 0.9999999 => baselineText,
                _ => BlendRockPlantXml(modText, baselineText, scale),
            };

            var currentText = ReadEntryText(currentEntry);
            if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
            {
                replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
            }
        }

        foreach (var meshFile in RockMeshFiles)
        {
            var entryPath = $"[meshes]/{meshFile}";
            var modKey = $"[meshes]/{meshFile}";
            if (!modAssets.TryGetValue(modKey, out var modBytes))
            {
                continue;
            }

            var currentEntry = PakEntryLocator.FindEntry(currentArchive, entryPath);
            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
            if (currentEntry is null || baselineEntry is null)
            {
                continue;
            }

            var baselineBytes = ReadEntryBytes(baselineEntry);
            var updatedBytes = scale >= 0.9999999 ? baselineBytes : modBytes;
            var currentBytes = ReadEntryBytes(currentEntry);
            if (!currentBytes.AsSpan().SequenceEqual(updatedBytes))
            {
                replacements[entryPath] = updatedBytes;
            }
        }

        return replacements;
    }

    private static (CameraCollisionState State, int EligibleModels) AnalyzeCameraCollisions(ZipArchive archive)
    {
        var eligible = 0;
        var disabled = 0;
        var enabled = 0;

        foreach (var entryPath in ListModelEntryPaths(archive))
        {
            var text = ReadEntryText(archive, entryPath);
            var state = CollectClipCameraState(text);
            if (!state.Eligible)
            {
                continue;
            }

            eligible++;
            if (state.Disabled)
            {
                disabled++;
            }
            else
            {
                enabled++;
            }
        }

        if (eligible == 0)
        {
            return (CameraCollisionState.Empty, 0);
        }

        if (disabled == 0)
        {
            return (CameraCollisionState.CollisionsOn, eligible);
        }

        if (enabled == 0)
        {
            return (CameraCollisionState.CollisionsOff, eligible);
        }

        return (CameraCollisionState.Mixed, eligible);
    }

    private static double EstimateRockSizeScale(string pakPath, string? noStonesAssetsDirectory)
    {
        var baselinePath = PakBaselineService.TryGetBaselineInfo(pakPath)?.BaselinePath;
        if (baselinePath is null || !File.Exists(baselinePath))
        {
            return 1.0;
        }

        using var currentArchive = ZipFile.OpenRead(pakPath);
        using var baselineArchive = ZipFile.OpenRead(baselinePath);

        var referencePath = ListTrailRockPlantPaths(baselineArchive)
            .FirstOrDefault(path => path.EndsWith("/small_rock_a.xml", StringComparison.OrdinalIgnoreCase))
            ?? ListTrailRockPlantPaths(baselineArchive).FirstOrDefault();
        if (referencePath is null)
        {
            return 1.0;
        }

        var currentEntry = PakEntryLocator.FindEntry(currentArchive, referencePath);
        var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, referencePath);
        if (currentEntry is null || baselineEntry is null)
        {
            return 1.0;
        }

        var currentMass = TryReadBodyMass(ReadEntryText(currentEntry));
        var baselineMass = TryReadBodyMass(ReadEntryText(baselineEntry));
        if (currentMass is null || baselineMass is null || baselineMass.Value <= 0)
        {
            return 1.0;
        }

        return Math.Clamp(currentMass.Value / baselineMass.Value, 0, 1);
    }

    private static IReadOnlyDictionary<string, byte[]> LoadNoStonesAssets(string assetsDirectory)
    {
        var root = Path.Combine(assetsDirectory, "no-stones");
        if (!Directory.Exists(root))
        {
            throw new DirectoryNotFoundException($"No-stones assets were not found at {root}.");
        }

        var assets = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var plantFile in ModPlantFiles)
        {
            var path = Path.Combine(root, "plants", plantFile);
            if (File.Exists(path))
            {
                assets[$"plants/{plantFile}"] = File.ReadAllBytes(path);
            }
        }

        var meshRoot = Path.Combine(root, "[meshes]");
        if (Directory.Exists(meshRoot))
        {
            foreach (var meshFile in RockMeshFiles)
            {
                var path = Path.Combine(meshRoot, meshFile);
                if (File.Exists(path))
                {
                    assets[$"[meshes]/{meshFile}"] = File.ReadAllBytes(path);
                }
            }
        }

        return assets;
    }

    private static IEnumerable<string> ListTrailRockPlantPaths(ZipArchive archive)
    {
        var paths = new List<string>();
        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                || !entryPath.Contains("/classes/plants/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var text = ReadEntryText(entry);
            if (IsTrailRockPlant(entryPath, text))
            {
                paths.Add(entryPath);
            }
        }

        return paths
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);
    }

    private static bool IsTrailRockPlant(string entryPath, string text)
    {
        var fileName = Path.GetFileName(entryPath);
        if (!TrailRockFileNameRegex.IsMatch(fileName))
        {
            return false;
        }

        return SmallRockTemplateRegex.IsMatch(text);
    }

    private static string CreateSyntheticNoStonesPlantText(string baselineText)
    {
        var updated = baselineText;
        updated = ReplaceFirstAttributeValue(updated, MassAttributeRegex, "0");
        updated = ReplaceFirstAttributeValue(updated, NoSoftContactsAttributeRegex, "false");
        updated = ReplaceFirstAttributeValue(updated, BreakOffThresholdAttributeRegex, "0");
        updated = ReplaceFirstAttributeValue(updated, DamageMultAttributeRegex, "0");
        return updated;
    }

    private static string ReplaceFirstAttributeValue(string text, Regex attributeRegex, string value)
    {
        if (!attributeRegex.IsMatch(text))
        {
            return text;
        }

        return attributeRegex.Replace(
            text,
            match => $"{match.Groups["prefix"].Value}{value}{match.Groups["suffix"].Value}",
            1);
    }

    private static IEnumerable<string> ListModelEntryPaths(ZipArchive archive) =>
        archive.Entries
            .Select(entry => entry.FullName.Replace('\\', '/'))
            .Where(path => path.Contains(ModelsSegment, StringComparison.OrdinalIgnoreCase)
                && path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase);

    private static bool IsCameraEligibleModel(string text) =>
        CollectClipCameraState(text).Eligible;

    private static (bool Eligible, bool Disabled) CollectClipCameraState(string text)
    {
        var openTag = FindPrimaryModelOpenTag(text);
        if (openTag is null)
        {
            return (false, false);
        }

        var match = ClipCameraAttributeRegex.Match(openTag);
        if (!match.Success)
        {
            return (true, false);
        }

        var value = match.Groups["value"].Value.Trim();
        return (true, value.Equals("false", StringComparison.OrdinalIgnoreCase));
    }

    private static string RestoreClipCameraFromBaseline(string currentText, string baselineText)
    {
        var baselineTag = FindPrimaryModelOpenTag(baselineText);
        if (baselineTag is null)
        {
            return currentText;
        }

        var baselineMatch = ClipCameraAttributeRegex.Match(baselineTag);
        if (baselineMatch.Success)
        {
            return SetClipCameraValue(currentText, baselineMatch.Groups["value"].Value);
        }

        return RemoveClipCameraAttribute(currentText);
    }

    private static string SetClipCameraValue(string text, string clipValue)
    {
        var tagMatch = FindPrimaryModelTagMatch(text);
        if (tagMatch is null)
        {
            return text;
        }

        var openTag = tagMatch.Value;
        var desired = clipValue.Trim();
        string updatedOpenTag;
        if (ClipCameraAttributeRegex.IsMatch(openTag))
        {
            updatedOpenTag = ClipCameraAttributeRegex.Replace(
                openTag,
                match => $"{match.Groups["prefix"].Value}{desired}{match.Groups["suffix"].Value}",
                1);
        }
        else if (openTag.EndsWith("/>", StringComparison.Ordinal))
        {
            var prefix = openTag[..^2];
            var spacer = prefix.EndsWith(' ') || prefix.EndsWith('\t') ? "" : " ";
            updatedOpenTag = $"{prefix}{spacer}ClipCamera=\"{desired}\"/>";
        }
        else
        {
            var prefix = openTag[..^1];
            var spacer = prefix.EndsWith(' ') || prefix.EndsWith('\t') ? "" : " ";
            updatedOpenTag = $"{prefix}{spacer}ClipCamera=\"{desired}\">";
        }

        return string.Concat(text.AsSpan(0, tagMatch.Index), updatedOpenTag, text.AsSpan(tagMatch.Index + tagMatch.Length));
    }

    private static string RemoveClipCameraAttribute(string text)
    {
        var tagMatch = FindPrimaryModelTagMatch(text);
        if (tagMatch is null || !ClipCameraAttributeRegex.IsMatch(tagMatch.Value))
        {
            return text;
        }

        var updatedOpenTag = ClipCameraAttributeRegex.Replace(tagMatch.Value, "", 1);
        updatedOpenTag = Regex.Replace(updatedOpenTag, @"\s{2,}", " ");
        return string.Concat(text.AsSpan(0, tagMatch.Index), updatedOpenTag, text.AsSpan(tagMatch.Index + tagMatch.Length));
    }

    private static Match? FindPrimaryModelTagMatch(string text)
    {
        foreach (Match match in PrimaryModelTagRegex.Matches(text))
        {
            var tagName = match.Groups["tag"].Value;
            if (tagName.Equals("_templates", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return match;
        }

        return null;
    }

    private static string? FindPrimaryModelOpenTag(string text) =>
        FindPrimaryModelTagMatch(text)?.Value;

    private static string BlendRockPlantXml(string modText, string baselineText, double scale)
    {
        var updated = baselineText;
        updated = ReplaceScaledAttribute(updated, MassAttributeRegex, modText, baselineText, scale, preferInteger: true);
        updated = ReplaceScaledAttribute(updated, BreakOffThresholdAttributeRegex, modText, baselineText, scale, preferInteger: true);
        updated = ReplaceBooleanAttributeTowardMod(updated, NoSoftContactsAttributeRegex, modText, baselineText, scale);
        return updated;
    }

    private static string ReplaceScaledAttribute(
        string targetText,
        Regex attributeRegex,
        string modText,
        string baselineText,
        double scale,
        bool preferInteger)
    {
        var modValue = TryReadRegexDouble(attributeRegex, modText);
        var baselineValue = TryReadRegexDouble(attributeRegex, baselineText);
        if (modValue is null || baselineValue is null)
        {
            return targetText;
        }

        var blended = modValue.Value + (scale * (baselineValue.Value - modValue.Value));
        var formatted = preferInteger && Math.Abs(blended - Math.Round(blended)) < 0.000001
            ? Math.Round(blended).ToString(CultureInfo.InvariantCulture)
            : blended.ToString("0.######", CultureInfo.InvariantCulture);

        if (!attributeRegex.IsMatch(targetText))
        {
            return targetText;
        }

        return attributeRegex.Replace(
            targetText,
            match => $"{match.Groups["prefix"].Value}{formatted}{match.Groups["suffix"].Value}",
            1);
    }

    private static string ReplaceBooleanAttributeTowardMod(
        string targetText,
        Regex attributeRegex,
        string modText,
        string baselineText,
        double scale)
    {
        var modValue = TryReadRegexString(attributeRegex, modText);
        var baselineValue = TryReadRegexString(attributeRegex, baselineText);
        if (modValue is null || baselineValue is null || !attributeRegex.IsMatch(targetText))
        {
            return targetText;
        }

        var desired = scale >= 0.9999999
            ? baselineValue
            : modValue;
        return attributeRegex.Replace(
            targetText,
            match => $"{match.Groups["prefix"].Value}{desired}{match.Groups["suffix"].Value}",
            1);
    }

    private static double? TryReadBodyMass(string text) =>
        TryReadRegexDouble(MassAttributeRegex, text);

    private static double? TryReadRegexDouble(Regex attributeRegex, string text)
    {
        var match = attributeRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        return double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static string? TryReadRegexString(Regex attributeRegex, string text)
    {
        var match = attributeRegex.Match(text);
        return match.Success ? match.Groups["value"].Value : null;
    }

    private static string ReadEntryText(ZipArchive archive, string entryPath)
    {
        var entry = PakEntryLocator.FindEntry(archive, entryPath)
            ?? throw new FileNotFoundException("Pak entry was not found.", entryPath);
        return ReadEntryText(entry);
    }

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
