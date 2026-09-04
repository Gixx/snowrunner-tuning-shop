using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;

namespace SnowRunnerTuningShop.Core.Trailers;

public static class TrailerTuningService
{
    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ParentFileRegex = new(
        @"<_parent\b[^>]*\bFile\s*=\s*""(?<file>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InstallSocketRegex = new(
        @"<InstallSocket\b(?<attrs>[^>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StoreHitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trailer",
        "ScautTrailer",
        "Semitrailer",
        "LargeSemitrailer",
        "LogTrailer",
        "TrailerFarm",
        "TrailerPlanter",
        "SemitrailerOiltank",
        "LargeSemitrailerOiltank",
        "SemitrailerFoldableLog",
        "SemitrailerCat770g",
    };

    private static readonly HashSet<string> NonStoreHitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Train",
        "TrailerTrainRocket",
        "CargoCabin",
    };

    private const string DefaultSaddleHighOffset = "(8.719; 1.895; 0)";

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

                var text = ReadEntryText(entry);
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

                    baselineById.TryAdd(Path.GetFileNameWithoutExtension(entryPath), ReadEntryText(entry));
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
        double priceMultiplier)
    {
        ValidateMultiplier(fuelMultiplier, nameof(fuelMultiplier));
        ValidateMultiplier(repairsMultiplier, nameof(repairsMultiplier));
        ValidateMultiplier(wheelsMultiplier, nameof(wheelsMultiplier));
        ValidateMultiplier(priceMultiplier, nameof(priceMultiplier));

        return MutateTrailersFromBaseline(
            pakPath,
            baselineText => ApplyGlobalMultipliersToText(
                baselineText,
                fuelMultiplier,
                repairsMultiplier,
                wheelsMultiplier,
                priceMultiplier));
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

                var text = ReadEntryText(entry);
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
                    updated = SetGameDataAttribute(updated, "IsQuest", "false");
                }

                updated = EnsureStoreHitch(updated);
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

            var text = ReadEntryText(entry);
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var updated = ApplyTuning(text, trailer);
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
            ? ParseAttributes(truckData.Groups["attrs"].Value)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var hasFuel = TryParsePresentInt(attrs, "FuelCapacity", out var fuel);
        var hasRepairs = TryParsePresentInt(attrs, "RepairsCapacity", out var repairs);
        var hasWheels = TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels);
        var hasWater = TryParsePresentInt(attrs, "WaterCapacity", out var water);
        var uiKey = ExtractUiName(text);
        var baselineText = TryReadText(baselineArchive, entryPath);
        var isQuest = ResolveIsQuest(text, workingById);
        var baselineIsQuest = ResolveIsQuest(baselineText ?? text, baselineById ?? workingById);

        trailer = new TrailerTuningDefinition
        {
            EntryPath = entryPath,
            TrailerId = trailerId,
            DisplayName = GameStringsReader.Resolve(strings, uiKey, trailerId),
            HasGameData = hasGameData,
            Price = ExtractGameDataInt(text, "Price", 0),
            BaselinePrice = ExtractGameDataInt(baselineText ?? text, "Price", 0),
            UnlockByRank = Math.Clamp(ExtractGameDataInt(text, "UnlockByRank", 1), 0, 30),
            BaselineUnlockByRank = Math.Clamp(ExtractGameDataInt(baselineText ?? text, "UnlockByRank", 1), 0, 30),
            IsQuest = isQuest,
            BaselineIsQuest = baselineIsQuest,
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

    private static string ApplyTuning(string text, TrailerTuningDefinition trailer)
    {
        var updated = text;
        if (trailer.HasFuel)
        {
            updated = ApplyExistingTruckDataInt(updated, "FuelCapacity", trailer.FuelCapacity);
        }

        if (trailer.HasRepairs)
        {
            updated = ApplyExistingTruckDataInt(updated, "RepairsCapacity", trailer.RepairsCapacity);
        }

        if (trailer.HasWheels)
        {
            updated = ApplyExistingTruckDataInt(updated, "WheelRepairsCapacity", trailer.WheelRepairsCapacity);
        }

        if (trailer.HasWater)
        {
            updated = ApplyExistingTruckDataInt(updated, "WaterCapacity", trailer.WaterCapacity);
        }

        if (trailer.HasGameData)
        {
            updated = SetGameDataAttribute(updated, "Price", trailer.Price.ToString(CultureInfo.InvariantCulture));
            updated = SetGameDataAttribute(
                updated,
                "UnlockByRank",
                Math.Clamp(trailer.UnlockByRank, 0, 30).ToString(CultureInfo.InvariantCulture));
            updated = SetGameDataAttribute(updated, "IsQuest", trailer.IsQuest ? "true" : "false");
            if (!trailer.IsQuest)
            {
                updated = EnsureStoreHitch(updated);
            }
        }

        return TrailerStoreUiFix.ApplyXml(trailer.TrailerId, updated);
    }

    private static string ApplyGlobalMultipliersToText(
        string baselineText,
        double fuelMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier)
    {
        var match = TruckDataOpenRegex.Match(baselineText);
        if (!match.Success)
        {
            return baselineText;
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        var updated = baselineText;

        if (TryParsePresentInt(attrs, "FuelCapacity", out var fuel) && fuel > 0)
        {
            updated = ApplyExistingTruckDataInt(updated, "FuelCapacity", Scale(fuel, fuelMultiplier, 1, 10_000));
        }

        if (TryParsePresentInt(attrs, "RepairsCapacity", out var repairs) && repairs > 0)
        {
            updated = ApplyExistingTruckDataInt(updated, "RepairsCapacity", Scale(repairs, repairsMultiplier, 0, 10_000));
        }

        if (TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels) && wheels > 0)
        {
            updated = ApplyExistingTruckDataInt(updated, "WheelRepairsCapacity", Scale(wheels, wheelsMultiplier, 0, 99));
        }

        if (GameDataOpenRegex.IsMatch(updated))
        {
            var baselinePrice = ExtractGameDataInt(baselineText, "Price", 0);
            if (baselinePrice > 0)
            {
                updated = SetGameDataAttribute(
                    updated,
                    "Price",
                    Scale(baselinePrice, priceMultiplier, 0, 9_999_999).ToString(CultureInfo.InvariantCulture));
            }
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

                var baselineText = PakVanillaText.Read(baselineArchive, entry, ReadEntryText);
                var updatedText = transformBaselineText(baselineText);
                var currentText = ReadEntryText(entry);
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

    private static void ValidateMultiplier(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Multiplier must be a positive number.");
        }
    }

    private static string ApplyExistingTruckDataInt(string text, string attributeName, int value)
    {
        if (!TryGetTruckDataAttribute(text, attributeName, out _))
        {
            return text;
        }

        return SetTruckDataAttribute(text, attributeName, value.ToString(CultureInfo.InvariantCulture));
    }

    private static string SetTruckDataAttribute(string text, string attributeName, string value)
    {
        var match = TruckDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var attrs = match.Groups["attrs"].Value;
        if (!SetOrReplaceAttribute(ref attrs, attributeName, value))
        {
            return text;
        }

        var replacement = $"<TruckData{attrs}>";
        return string.Concat(text.AsSpan(0, match.Index), replacement, text.AsSpan(match.Index + match.Length));
    }

    private static string SetGameDataAttribute(string text, string attributeName, string value)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var attrs = match.Groups["attrs"].Value;
        if (!SetOrReplaceAttribute(ref attrs, attributeName, value))
        {
            return text;
        }

        var replacement = $"<GameData{attrs}>";
        return string.Concat(text.AsSpan(0, match.Index), replacement, text.AsSpan(match.Index + match.Length));
    }

    private static bool TryGetTruckDataAttribute(string text, string attributeName, out string value)
    {
        value = "";
        var match = TruckDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return false;
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        if (!attrs.TryGetValue(attributeName, out var raw))
        {
            return false;
        }

        value = raw;
        return true;
    }

    private static int ReadBaselineInt(string? baselineText, string currentText, string attributeName, int fallback)
    {
        if (baselineText is not null && TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseInt(baselineRaw, fallback);
        }

        if (TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
        {
            return ParseInt(currentRaw, fallback);
        }

        return fallback;
    }

    private static int ExtractGameDataInt(string text, string attributeName, int fallback)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return fallback;
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        return attrs.TryGetValue(attributeName, out var raw) ? ParseInt(raw, fallback) : fallback;
    }

    private static bool? TryExtractGameDataBool(string text, string attributeName)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
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

    private static string EnsureStoreHitch(string text)
    {
        var sockets = InstallSocketRegex.Matches(text);
        foreach (Match socket in sockets)
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type) && StoreHitchTypes.Contains(type.Trim()))
            {
                return text;
            }
        }

        if (sockets.Count > 0)
        {
            var first = sockets[0];
            var attrs = ParseAttributes(first.Groups["attrs"].Value);
            attrs.TryGetValue("Type", out var type);
            type = type?.Trim() ?? "";

            if (string.IsNullOrEmpty(type))
            {
                return SetInstallSocketType(text, first, "LargeSemitrailer");
            }

            if (!NonStoreHitchTypes.Contains(type) || HasStoreHitchSocket(text))
            {
                return text;
            }

            if (!attrs.TryGetValue("Offset", out var offset) || string.IsNullOrWhiteSpace(offset))
            {
                offset = "(0; 0; 0)";
            }

            return InsertAfter(text, first, $"{Environment.NewLine}\t\t<InstallSocket Offset=\"{offset}\" Type=\"Trailer\" />");
        }

        if (!GameDataOpenRegex.IsMatch(text) || !ParentFileRegex.IsMatch(text))
        {
            return text;
        }

        return InsertBeforeGameDataClose(
            text,
            $"<InstallSocket Offset=\"{DefaultSaddleHighOffset}\" Type=\"LargeSemitrailer\" />");
    }

    private static bool HasStoreHitchSocket(string text)
    {
        foreach (Match socket in InstallSocketRegex.Matches(text))
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type) && StoreHitchTypes.Contains(type.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    private static string SetInstallSocketType(string text, Match socket, string type)
    {
        var attrs = socket.Groups["attrs"].Value.TrimEnd();
        if (attrs.EndsWith('/'))
        {
            attrs = attrs[..^1].TrimEnd();
        }

        if (!SetOrReplaceAttribute(ref attrs, "Type", type))
        {
            return text;
        }

        var replacement = $"<InstallSocket{attrs} />";
        return string.Concat(text.AsSpan(0, socket.Index), replacement, text.AsSpan(socket.Index + socket.Length));
    }

    private static string InsertAfter(string text, Match match, string insert)
    {
        var index = match.Index + match.Length;
        return string.Concat(text.AsSpan(0, index), insert, text.AsSpan(index));
    }

    private static string InsertBeforeGameDataClose(string text, string childXml)
    {
        var close = Regex.Match(text, @"</GameData>", RegexOptions.IgnoreCase);
        if (!close.Success)
        {
            return text;
        }

        return string.Concat(
            text[..close.Index],
            childXml,
            Environment.NewLine,
            "\t\t",
            text[close.Index..]);
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
        return entry is null ? null : ReadEntryText(entry);
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

    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static int ParseInt(string? value, int fallback) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

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
