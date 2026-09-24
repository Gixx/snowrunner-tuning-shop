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

namespace SnowRunnerTuningShop.Core.Trailers;

public static class TrailerTuningService
{
    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ParentFileRegex = new(
        @"<_parent\b[^<>]*\bFile\s*=\s*""(?<file>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TrailerTuningDefinition> LoadTrailers(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        ZipArchive? baselineArchive = null;
        var baselineInfo = PakBaselineService.TryGetBaselineInfo(pakPath);
        if (baselineInfo is not null && File.Exists(baselineInfo.BaselinePath))
        {
            baselineArchive = ZipFile.OpenRead(baselineInfo.BaselinePath);
        }

        try
        {
            var workingById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var files = new List<(string Path, string Text)>();
            foreach (var entry in archive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTrailerEntry(entryPath))
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                files.Add((entryPath, text));
                workingById.TryAdd(Path.GetFileNameWithoutExtension(entryPath), text);
            }

            Dictionary<string, string>? baselineById = null;
            if (baselineArchive is not null)
            {
                baselineById = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var entry in baselineArchive.Entries)
                {
                    var entryPath = entry.FullName.Replace('\\', '/');
                    if (!IsTrailerEntry(entryPath))
                    {
                        continue;
                    }

                    baselineById.TryAdd(Path.GetFileNameWithoutExtension(entryPath), PartXmlHelpers.ReadEntryUtf8(entry));
                }
            }

            var trailers = new List<TrailerTuningDefinition>();
            foreach (var (entryPath, text) in files)
            {
                if (TryParseTrailer(baselineArchive, entryPath, text, strings, workingById, baselineById, out var trailer))
                {
                    trailers.Add(trailer);
                }
            }

            return trailers
                .OrderBy(trailer => trailer.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            baselineArchive?.Dispose();
        }
    }

    public static TrailerTuningDefinition? FindByCatalog(
        IReadOnlyList<TrailerTuningDefinition> trailers,
        string catalogId) =>
        PakFileId.Find(trailers, trailer => trailer.TrailerId, trailer => trailer.EntryPath, catalogId);

    public static TrailerTuningSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double fuelMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier,
        double massMultiplier)
    {
        PartPakPipeline.ValidateMultiplier(fuelMultiplier, nameof(fuelMultiplier));
        PartPakPipeline.ValidateMultiplier(repairsMultiplier, nameof(repairsMultiplier));
        PartPakPipeline.ValidateMultiplier(wheelsMultiplier, nameof(wheelsMultiplier));
        PartPakPipeline.ValidateMultiplier(priceMultiplier, nameof(priceMultiplier));
        PartPakPipeline.ValidateMultiplier(massMultiplier, nameof(massMultiplier));

        return MutateTrailersFromBaseline(
            pakPath,
            baselineText => ApplyGlobalMultipliersToText(
                baselineText,
                fuelMultiplier,
                repairsMultiplier,
                wheelsMultiplier,
                priceMultiplier,
                massMultiplier));
    }

    public static TrailerTuningSaveResult MakeQuestTrailersPurchasable(string pakPath)
    {
        Dictionary<string, byte[]> replacements;
        var changedTrailers = 0;

        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            var files = new List<(string Path, string Text)>();
            var byId = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTrailerEntry(entryPath))
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                if (!GameDataOpenRegex.IsMatch(text))
                {
                    continue;
                }

                files.Add((entryPath, text));
                byId.TryAdd(Path.GetFileNameWithoutExtension(entryPath), text);
            }

            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var (entryPath, text) in files)
            {
                var trailerId = Path.GetFileNameWithoutExtension(entryPath);
                var updated = text;
                if (ResolveIsQuest(text, byId))
                {
                    updated = VehicleGameDataXml.SetGameDataAttribute(updated, "IsQuest", "false");
                }

                updated = TrailerHitchXml.EnsureStoreHitch(updated);
                updated = TrailerStoreUiFix.ApplyXml(trailerId, updated);
                if (!string.Equals(text, updated, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updated);
                    changedTrailers++;
                }
            }

            TrailerStoreUiFix.AddStringTableReplacements(currentArchive, replacements);
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TrailerTuningSaveResult(updatedFiles, changedTrailers);
    }

    public static TrailerTuningSaveResult RestoreAllTrailersFromBaseline(string pakPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedTrailers = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTrailerEntry(entryPath))
                {
                    continue;
                }

                var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
                if (baselineEntry is null)
                {
                    continue;
                }

                var baselineBytes = ReadEntryBytes(baselineEntry);
                var currentBytes = ReadEntryBytes(entry);
                if (currentBytes.AsSpan().SequenceEqual(baselineBytes))
                {
                    continue;
                }

                replacements[entryPath] = baselineBytes;
                changedTrailers++;
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TrailerTuningSaveResult(updatedFiles, changedTrailers);
    }

    public static TrailerTuningSaveResult SaveTrailerChanges(string pakPath, TrailerTuningDefinition trailer)
    {
        ArgumentNullException.ThrowIfNull(trailer);

        Dictionary<string, byte[]> replacements;
        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var entry = PakEntryLocator.FindEntry(archive, trailer.EntryPath)
                ?? throw new FileNotFoundException("Trailer XML was not found in the pak.", trailer.EntryPath);

            var text = PartXmlHelpers.ReadEntryUtf8(entry);
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var baselineText = TryReadBaselineEntryText(pakPath, trailer.EntryPath);
            var updated = ApplyTuning(text, trailer, baselineText);
            var key = entry.FullName.Replace('\\', '/');
            if (!string.Equals(text, updated, StringComparison.Ordinal))
            {
                replacements[key] = Encoding.UTF8.GetBytes(updated);
            }

            if (TrailerStoreUiFix.AppliesTo(trailer.TrailerId))
            {
                TrailerStoreUiFix.AddStringTableReplacements(archive, replacements);
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TrailerTuningSaveResult(updatedFiles);
    }

    public static TrailerTuningSaveResult RestoreTrailerFromBaseline(string pakPath, string entryPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        byte[] baselineBytes;
        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        {
            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath)
                ?? throw new FileNotFoundException("Trailer XML was not found in the baseline pak.", entryPath);
            baselineBytes = ReadEntryBytes(baselineEntry);
        }

        byte[]? currentBytes = null;
        string writeKey;
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            var currentEntry = PakEntryLocator.FindEntry(currentArchive, entryPath);
            if (currentEntry is not null)
            {
                currentBytes = ReadEntryBytes(currentEntry);
                writeKey = currentEntry.FullName.Replace('\\', '/');
            }
            else
            {
                writeKey = entryPath.Replace('\\', '/');
            }
        }

        if (currentBytes is not null && currentBytes.AsSpan().SequenceEqual(baselineBytes))
        {
            return new TrailerTuningSaveResult(0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(
            pakPath,
            new Dictionary<string, byte[]>(StringComparer.Ordinal) { [writeKey] = baselineBytes });
        return new TrailerTuningSaveResult(updatedFiles);
    }

    public static bool IsTrailerEntry(string entryPath)
    {
        if (!entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryParseTrailer(
        ZipArchive? baselineArchive,
        string entryPath,
        string text,
        IReadOnlyDictionary<string, string> strings,
        IReadOnlyDictionary<string, string> workingById,
        IReadOnlyDictionary<string, string>? baselineById,
        out TrailerTuningDefinition trailer)
    {
        var truckData = TruckDataOpenRegex.Match(text);
        var hasGameData = GameDataOpenRegex.IsMatch(text);
        if (!truckData.Success && !hasGameData)
        {
            trailer = null!;
            return false;
        }

        var trailerId = Path.GetFileNameWithoutExtension(entryPath);
        var attrs = truckData.Success
            ? VehicleGameDataXml.ParseAttributes(truckData.Groups["attrs"].Value)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasFuel = TryParsePresentInt(attrs, "FuelCapacity", out var fuel);
        var hasRepairs = TryParsePresentInt(attrs, "RepairsCapacity", out var repairs);
        var hasWheels = TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels);
        var hasWater = TryParsePresentInt(attrs, "WaterCapacity", out var water);
        var uiKey = ExtractUiName(text);
        var baselineText = TryReadText(baselineArchive, entryPath);
        var isQuest = ResolveIsQuest(text, workingById);
        var baselineIsQuest = ResolveIsQuest(baselineText ?? text, baselineById ?? workingById);
        var hasStoreHitch = TrailerHitchXml.IsStoreHitchReady(text);
        var baselineHasStoreHitch = TrailerHitchXml.IsStoreHitchReady(baselineText ?? text);
        var hasMass = VehiclePhysicsMassXml.TryReadPrimaryMass(text, out var mass);
        var baselineMass = VehiclePhysicsMassXml.TryReadPrimaryMass(baselineText ?? text, out var parsedBaselineMass)
            ? parsedBaselineMass
            : mass;

        trailer = new TrailerTuningDefinition
        {
            EntryPath = entryPath,
            TrailerId = trailerId,
            DisplayName = GameStringsReader.Resolve(strings, uiKey, trailerId),
            HasGameData = hasGameData,
            Price = VehicleGameDataXml.ExtractGameDataInt(text, "Price", 0),
            BaselinePrice = VehicleGameDataXml.ExtractGameDataInt(baselineText ?? text, "Price", 0),
            UnlockByRank = Math.Clamp(VehicleGameDataXml.ExtractGameDataInt(text, "UnlockByRank", 1), 0, 30),
            BaselineUnlockByRank = Math.Clamp(VehicleGameDataXml.ExtractGameDataInt(baselineText ?? text, "UnlockByRank", 1), 0, 30),
            IsQuest = isQuest,
            BaselineIsQuest = baselineIsQuest,
            HasStoreCompatibleHitch = hasStoreHitch,
            BaselineHasStoreCompatibleHitch = baselineHasStoreHitch,
            HasMass = hasMass,
            Mass = hasMass ? mass : 0,
            BaselineMass = hasMass ? baselineMass : 0,
            HasFuel = hasFuel,
            FuelCapacity = fuel,
            BaselineFuelCapacity = ReadBaselineInt(baselineText, text, "FuelCapacity", fuel),
            HasRepairs = hasRepairs,
            RepairsCapacity = repairs,
            BaselineRepairsCapacity = ReadBaselineInt(baselineText, text, "RepairsCapacity", repairs),
            HasWheels = hasWheels,
            WheelRepairsCapacity = wheels,
            BaselineWheelRepairsCapacity = ReadBaselineInt(baselineText, text, "WheelRepairsCapacity", wheels),
            HasWater = hasWater,
            WaterCapacity = water,
            BaselineWaterCapacity = ReadBaselineInt(baselineText, text, "WaterCapacity", water),
        };
        return true;
    }

    private static string ApplyTuning(string text, TrailerTuningDefinition trailer, string? baselineText = null)
    {
        var updated = text;
        if (trailer.HasFuel)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "FuelCapacity", trailer.FuelCapacity);
        }

        if (trailer.HasRepairs)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "RepairsCapacity", trailer.RepairsCapacity);
        }

        if (trailer.HasWheels)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "WheelRepairsCapacity", trailer.WheelRepairsCapacity);
        }

        if (trailer.HasWater)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "WaterCapacity", trailer.WaterCapacity);
        }

        if (trailer.HasMass)
        {
            updated = VehiclePhysicsMassXml.ApplyPrimaryMass(updated, trailer.Mass);
        }

        if (trailer.HasGameData)
        {
            updated = VehicleGameDataXml.SetGameDataAttribute(updated, "Price", trailer.Price.ToString(CultureInfo.InvariantCulture));
            updated = VehicleGameDataXml.SetGameDataAttribute(
                updated,
                "UnlockByRank",
                Math.Clamp(trailer.UnlockByRank, 0, 30).ToString(CultureInfo.InvariantCulture));

            if (trailer.MakeAvailableInStore)
            {
                updated = VehicleGameDataXml.SetGameDataAttribute(updated, "IsQuest", "false");
                updated = TrailerHitchXml.EnsureStoreHitch(updated);
            }
            else if (trailer.IsQuest)
            {
                updated = VehicleGameDataXml.SetGameDataAttribute(updated, "IsQuest", "true");
                updated = TrailerHitchXml.RemoveSupplementalStoreHitch(
                    updated,
                    baselineText,
                    trailer.BaselineHasStoreCompatibleHitch);
            }
            else if (trailer.BaselineIsQuest)
            {
                updated = VehicleGameDataXml.SetGameDataAttribute(updated, "IsQuest", "false");
            }
            else
            {
                updated = VehicleGameDataXml.RemoveGameDataAttribute(updated, "IsQuest");
                updated = TrailerHitchXml.RemoveSupplementalStoreHitch(
                    updated,
                    baselineText,
                    trailer.BaselineHasStoreCompatibleHitch);
            }
        }

        return TrailerStoreUiFix.ApplyXml(trailer.TrailerId, updated);
    }

    private static string ApplyGlobalMultipliersToText(
        string baselineText,
        double fuelMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier,
        double massMultiplier)
    {
        var match = TruckDataOpenRegex.Match(baselineText);
        var updated = baselineText;

        if (match.Success)
        {
            var attrs = VehicleGameDataXml.ParseAttributes(match.Groups["attrs"].Value);

            if (TryParsePresentInt(attrs, "FuelCapacity", out var fuel) && fuel > 0)
            {
                updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "FuelCapacity", Scale(fuel, fuelMultiplier, 1, 10_000));
            }

            if (TryParsePresentInt(attrs, "RepairsCapacity", out var repairs) && repairs > 0)
            {
                updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "RepairsCapacity", Scale(repairs, repairsMultiplier, 0, 10_000));
            }

            if (TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels) && wheels > 0)
            {
                updated = VehicleGameDataXml.ApplyExistingTruckDataInt(updated, "WheelRepairsCapacity", Scale(wheels, wheelsMultiplier, 0, 99));
            }
        }

        if (GameDataOpenRegex.IsMatch(updated))
        {
            var baselinePrice = VehicleGameDataXml.ExtractGameDataInt(baselineText, "Price", 0);
            if (baselinePrice > 0)
            {
                updated = VehicleGameDataXml.SetGameDataAttribute(
                    updated,
                    "Price",
                    Scale(baselinePrice, priceMultiplier, 0, 9_999_999).ToString(CultureInfo.InvariantCulture));
            }
        }

        if (!TuningMultiplierPresets.IsBaselineMultiplier(massMultiplier))
        {
            updated = VehiclePhysicsMassXml.ScaleAllMasses(updated, massMultiplier);
        }

        return updated;
    }

    private static TrailerTuningSaveResult MutateTrailersFromBaseline(
        string pakPath,
        Func<string, string> transformBaselineText)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedTrailers = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTrailerEntry(entryPath))
                {
                    continue;
                }

                var baselineText = PakVanillaText.Read(baselineArchive, entry, PartXmlHelpers.ReadEntryUtf8);
                var updatedText = transformBaselineText(baselineText);
                var currentText = PartXmlHelpers.ReadEntryUtf8(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedTrailers++;
                }
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TrailerTuningSaveResult(updatedFiles, changedTrailers);
    }

    private static int Scale(int baseline, double multiplier, int min, int max) =>
        (int)Math.Clamp(Math.Round(baseline * multiplier, MidpointRounding.AwayFromZero), min, max);

    private static int ReadBaselineInt(string? baselineText, string currentText, string attributeName, int fallback)
    {
        if (baselineText is not null && VehicleGameDataXml.TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseInt(baselineRaw, fallback);
        }

        if (VehicleGameDataXml.TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
        {
            return ParseInt(currentRaw, fallback);
        }

        return fallback;
    }

    private static bool? TryExtractGameDataBool(string text, string attributeName)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var attrs = VehicleGameDataXml.ParseAttributes(match.Groups["attrs"].Value);
        if (!attrs.TryGetValue(attributeName, out var raw))
        {
            return null;
        }

        return raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw.Equals("1", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ResolveIsQuest(string text, IReadOnlyDictionary<string, string> textsById)
    {
        var current = text;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (true)
        {
            var flag = TryExtractGameDataBool(current, "IsQuest");
            if (flag.HasValue)
            {
                return flag.Value;
            }

            var parentId = ExtractParentId(current);
            if (string.IsNullOrEmpty(parentId) || !seen.Add(parentId))
            {
                return false;
            }

            if (!textsById.TryGetValue(parentId, out current))
            {
                return false;
            }
        }
    }

    private static string ExtractParentId(string text)
    {
        var match = ParentFileRegex.Match(text);
        if (!match.Success)
        {
            return "";
        }

        return Path.GetFileNameWithoutExtension(match.Groups["file"].Value);
    }

    private static string? TryReadBaselineEntryText(string workingPakPath, string entryPath)
    {
        var baselineInfo = PakBaselineService.TryGetBaselineInfo(workingPakPath);
        if (baselineInfo is null || !File.Exists(baselineInfo.BaselinePath))
        {
            return null;
        }

        using var baselineArchive = ZipFile.OpenRead(baselineInfo.BaselinePath);
        return TryReadText(baselineArchive, entryPath);
    }

    private static string ExtractUiName(string text)
    {
        var match = Regex.Match(text, @"UiName\s*=\s*""(?<value>UI_[^""]+)""", RegexOptions.IgnoreCase);
        return match.Success ? match.Groups["value"].Value : "";
    }

    private static string? TryReadText(ZipArchive? archive, string entryPath)
    {
        if (archive is null)
        {
            return null;
        }

        var entry = PakEntryLocator.FindEntry(archive, entryPath);
        return entry is null ? null : PartXmlHelpers.ReadEntryUtf8(entry);
    }

    private static bool TryParsePresentInt(Dictionary<string, string> attrs, string name, out int value)
    {
        value = 0;
        if (!attrs.TryGetValue(name, out var raw))
        {
            return false;
        }

        value = ParseInt(raw, 0);
        return true;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
