using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;

namespace SnowRunnerTuningShop.Core.Trucks;

public static class TruckTuningService
{
    public const double GlobalFrontSteerMinimumDegrees = 10;
    public const double GlobalFrontSteerMaximumDegrees = 60;

    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex TorqueTagRegex = new(
        @"<(?<tag>FrontWheel|RearWheel|FirstAxle|SecondAxle|ThirdAxle|FourthAxle|FrontAxle|RearAxle|MiddleAxle|MiddleWheel|Front|Rear)\b(?<attrs>[^>]*\bTorque\s*=\s*""[^""]*""[^>]*)(?<self>/?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AddonSocketsBlockRegex = new(
        @"<AddonSockets\b(?<attrs>[^>]*)>(?<body>.*?)</AddonSockets>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

    private static readonly Regex DiffLockInstalledRegex = new(
        @"DiffLockInstalled\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex SteeringAngleAttributeRegex = new(
        @"(?<prefix>SteeringAngle\s*=\s*"")(?<value>[^""]*)(?<suffix>"")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<TruckTuningDefinition> LoadTrucks(string pakPath, string language = "english")
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
            var trucks = new List<TruckTuningDefinition>();

            foreach (var entry in archive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTruckEntry(entryPath))
                {
                    continue;
                }

                var text = ReadEntryText(entry);
                if (TryParseTruck(archive, baselineArchive, entryPath, text, strings, out var truck))
                {
                    trucks.Add(truck);
                }
            }

            return trucks
                .OrderBy(truck => truck.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            baselineArchive?.Dispose();
        }
    }

    public static TruckTuningDefinition? FindByCatalog(
        IReadOnlyList<TruckTuningDefinition> trucks,
        string catalogDisplayName,
        string? catalogId = null)
    {
        if (trucks.Count == 0)
        {
            return null;
        }

        var nameKey = NormalizeKey(catalogDisplayName);
        if (nameKey.Length > 0)
        {
            var named = trucks.Where(truck => NormalizeKey(truck.DisplayName) == nameKey).ToArray();
            if (named.Length == 1)
            {
                return named[0];
            }
        }

        var idKey = NormalizeKey(catalogId);
        if (idKey.Length == 0)
        {
            return null;
        }

        var byId = trucks.Where(truck => NormalizeKey(truck.TruckId) == idKey).ToArray();
        return byId.Length == 1 ? byId[0] : null;
    }

    public static TruckTuningSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double fuelMultiplier,
        TruckFrontSteerGlobalMode frontSteerMode,
        double responsivenessMultiplier,
        double priceMultiplier,
        bool alwaysOnDiffLock = false,
        bool alwaysOnAwd = false)
    {
        ValidateMultiplier(fuelMultiplier, nameof(fuelMultiplier));
        ValidateMultiplier(responsivenessMultiplier, nameof(responsivenessMultiplier));
        ValidateMultiplier(priceMultiplier, nameof(priceMultiplier));
        if (!Enum.IsDefined(frontSteerMode))
        {
            throw new ArgumentOutOfRangeException(nameof(frontSteerMode), "Unsupported front steer preset.");
        }

        return MutateDirectTrucksFromBaseline(
            pakPath,
            (workingArchive, entryPath, baselineText) => ApplyGlobalMultipliersToText(
                workingArchive,
                Path.GetFileNameWithoutExtension(entryPath),
                baselineText,
                fuelMultiplier,
                frontSteerMode,
                responsivenessMultiplier,
                priceMultiplier,
                alwaysOnDiffLock,
                alwaysOnAwd));
    }

    public static TruckTuningSaveResult ApplyGlobalStoreUnlocks(
        string pakPath,
        bool releaseRegionLock,
        bool unlockAllVehicles)
    {
        if (!releaseRegionLock && !unlockAllVehicles)
        {
            return new TruckTuningSaveResult(0);
        }

        return MutateDirectTrucksInPlace(
            pakPath,
            text =>
            {
                var updated = text;
                if (releaseRegionLock)
                {
                    updated = ApplyGameDataCountry(updated, TruckStoreRegions.AllCountriesAttributeValue);
                }

                if (unlockAllVehicles)
                {
                    updated = ApplyGameDataUnlockByRank(updated, 0);
                }

                return updated;
            });
    }

    public static TruckTuningSaveResult RestoreGlobalTuningFromBaseline(string pakPath) =>
        RestoreAllVehiclesFromBaseline(pakPath);

    /// <summary>Copies every direct truck XML from the baseline pak into the working pak.</summary>
    public static TruckTuningSaveResult RestoreAllVehiclesFromBaseline(string pakPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedTrucks = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTruckEntry(entryPath))
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
                changedTrucks++;
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles, changedTrucks);
    }

    private static TruckTuningSaveResult MutateDirectTrucksFromBaseline(
        string pakPath,
        Func<ZipArchive, string, string, string> transformBaselineText)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedTrucks = 0;

        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTruckEntry(entryPath))
                {
                    continue;
                }

                var baselineText = PakVanillaText.Read(baselineArchive, entry, ReadEntryText);
                var updatedText = transformBaselineText(currentArchive, entryPath, baselineText);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedTrucks++;
                }
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles, changedTrucks);
    }

    private static TruckTuningSaveResult MutateDirectTrucksInPlace(
        string pakPath,
        Func<string, string> transformText)
    {
        Dictionary<string, byte[]> replacements;
        var changedTrucks = 0;

        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsTruckEntry(entryPath))
                {
                    continue;
                }

                var currentText = ReadEntryText(entry);
                if (!GameDataOpenRegex.IsMatch(currentText))
                {
                    continue;
                }

                var updatedText = transformText(currentText);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedTrucks++;
                }
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles, changedTrucks);
    }

    public static TruckTuningSaveResult SaveTruckChanges(string pakPath, TruckTuningDefinition truck)
    {
        ArgumentNullException.ThrowIfNull(truck);

        Dictionary<string, byte[]> replacements;
        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var entry = PakEntryLocator.FindEntry(archive, truck.EntryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the pak.", truck.EntryPath);

            var text = ReadEntryText(entry);
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var updated = ApplyTuning(archive, text, truck);
            var truckKey = entry.FullName.Replace('\\', '/');
            if (!string.Equals(text, updated, StringComparison.Ordinal))
            {
                replacements[truckKey] = Encoding.UTF8.GetBytes(updated);
            }
        }

        var updatedFiles = replacements.Count == 0
            ? 0
            : InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new TruckTuningSaveResult(updatedFiles);
    }

    public static TruckTuningSaveResult RestoreTruckFromBaseline(string pakPath, string entryPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        byte[] baselineBytes;
        using (var baselineArchive = ZipFile.OpenRead(baselinePath))
        {
            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath)
                ?? throw new FileNotFoundException("Truck XML was not found in the baseline pak.", entryPath);
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
            return new TruckTuningSaveResult(0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(
            pakPath,
            new Dictionary<string, byte[]>(StringComparer.Ordinal) { [writeKey] = baselineBytes });
        return new TruckTuningSaveResult(updatedFiles);
    }

    private static bool TryParseTruck(
        ZipArchive archive,
        ZipArchive? baselineArchive,
        string entryPath,
        string text,
        IReadOnlyDictionary<string, string> strings,
        out TruckTuningDefinition truck)
    {
        var truckId = Path.GetFileNameWithoutExtension(entryPath);
        var truckData = TruckDataOpenRegex.Match(text);
        if (!truckData.Success)
        {
            truck = null!;
            return false;
        }

        var attrs = ParseAttributes(truckData.Groups["attrs"].Value);
        attrs.TryGetValue("FuelCapacity", out var fuelRaw);
        attrs.TryGetValue("DiffLockType", out var diffRaw);
        attrs.TryGetValue("Responsiveness", out var responsivenessRaw);
        var (frontSteerAngle, rearSteerAngle, hasFrontSteer, hasRearSteer) = ParseSteerAngles(text);
        var uiMatch = VehicleUiNameRegex.Match(text);
        var uiKey = uiMatch.Success ? uiMatch.Groups["value"].Value : "";

        var baselineText = TryReadTruckText(baselineArchive, entryPath);
        var baselineSteer = baselineText is not null
            ? ParseSteerAngles(baselineText)
            : ParseSteerAngles(text);
        var hasNativeDiffLockOptions = baselineText is not null
            ? HasNativeDiffLockInfrastructure(baselineArchive!, baselineText, truckId)
            : HasNativeDiffLockInfrastructure(archive, text, truckId);

        truck = new TruckTuningDefinition
        {
            EntryPath = entryPath,
            TruckId = truckId,
            UiNameKey = uiKey,
            DisplayName = GameStringsReader.Resolve(strings, uiKey, truckId),
            FuelCapacity = ParseInt(fuelRaw, 0),
            BaselineFuelCapacity = ReadBaselineInt(baselineText, text, "FuelCapacity"),
            Price = ExtractGameDataPrice(text),
            BaselinePrice = baselineText is not null ? ExtractGameDataPrice(baselineText) : ExtractGameDataPrice(text),
            StoreCountries = ExtractGameDataAttribute(text, "Country"),
            BaselineStoreCountries = baselineText is not null
                ? ExtractGameDataAttribute(baselineText, "Country")
                : ExtractGameDataAttribute(text, "Country"),
            UnlockByRank = ExtractGameDataUnlockByRank(text),
            BaselineUnlockByRank = baselineText is not null
                ? ExtractGameDataUnlockByRank(baselineText)
                : ExtractGameDataUnlockByRank(text),
            DiffLockTypeRaw = diffRaw ?? "",
            HasNativeDiffLockOptions = hasNativeDiffLockOptions,
            DiffLock = ResolveDiffLockMode(archive, text, diffRaw, hasNativeDiffLockOptions),
            DriveLayout = InferDriveLayout(text),
            Responsiveness = ParseDouble(responsivenessRaw, 0.4),
            BaselineResponsiveness = ReadBaselineDouble(baselineText, text, "Responsiveness", 0.4),
            FrontSteerAngle = frontSteerAngle,
            BaselineFrontSteerAngle = baselineSteer.Front,
            RearSteerAngle = rearSteerAngle,
            BaselineRearSteerAngle = baselineSteer.Rear,
            HasFrontSteer = hasFrontSteer,
            HasRearSteer = hasRearSteer,
        };
        return true;
    }

    private static string ApplyTuning(
        ZipArchive archive,
        string text,
        TruckTuningDefinition truck)
    {
        var updated = ApplyFuelCapacity(text, truck.FuelCapacity);
        updated = ApplyGameDataPrice(updated, truck.Price);
        updated = ApplyGameDataCountry(updated, truck.StoreCountries);
        updated = ApplyGameDataUnlockByRank(updated, truck.UnlockByRank);
        updated = ApplySteering(updated, truck);
        updated = ApplyDiffLock(archive, updated, truck);
        updated = ApplyDriveLayout(updated, truck.DriveLayout);
        return updated;
    }

    private static string ApplyGlobalMultipliersToText(
        ZipArchive archive,
        string truckId,
        string baselineText,
        double fuelMultiplier,
        TruckFrontSteerGlobalMode frontSteerMode,
        double responsivenessMultiplier,
        double priceMultiplier,
        bool alwaysOnDiffLock,
        bool alwaysOnAwd)
    {
        var truckData = TruckDataOpenRegex.Match(baselineText);
        if (!truckData.Success)
        {
            return ApplyGlobalDriveFlags(archive, truckId, baselineText, alwaysOnDiffLock, alwaysOnAwd);
        }

        var attrs = ParseAttributes(truckData.Groups["attrs"].Value);
        var updated = baselineText;

        if (attrs.TryGetValue("FuelCapacity", out var fuelRaw))
        {
            var baselineFuel = ParseInt(fuelRaw, 0);
            if (baselineFuel > 0)
            {
                var scaledFuel = (int)Math.Clamp(
                    Math.Round(baselineFuel * fuelMultiplier, MidpointRounding.AwayFromZero),
                    1,
                    10000);
                updated = ApplyFuelCapacity(updated, scaledFuel);
            }
        }

        var baselineResponsiveness = ParseDouble(
            attrs.TryGetValue("Responsiveness", out var responsivenessRaw) ? responsivenessRaw : null,
            0.4);
        var scaledResponsiveness = Math.Clamp(baselineResponsiveness * responsivenessMultiplier, 0, 1);
        updated = SetTruckDataAttribute(
            updated,
            "Responsiveness",
            FormatNumeric(scaledResponsiveness, preferInteger: false));

        var baselinePrice = ExtractGameDataPrice(baselineText);
        if (baselinePrice >= 0 && GameDataOpenRegex.IsMatch(baselineText))
        {
            var scaledPrice = (int)Math.Clamp(
                Math.Round(baselinePrice * priceMultiplier, MidpointRounding.AwayFromZero),
                0,
                9_999_999);
            updated = ApplyGameDataPrice(updated, scaledPrice);
        }

        var (_, _, hasFrontSteer, _) = ParseSteerAngles(baselineText);
        if (hasFrontSteer)
        {
            updated = frontSteerMode switch
            {
                TruckFrontSteerGlobalMode.Minimum => ApplyFrontSteerAngle(updated, GlobalFrontSteerMinimumDegrees),
                TruckFrontSteerGlobalMode.Maximum => ApplyFrontSteerAngle(updated, GlobalFrontSteerMaximumDegrees),
                _ => updated,
            };
        }

        return ApplyGlobalDriveFlags(archive, truckId, updated, alwaysOnDiffLock, alwaysOnAwd);
    }

    private static string ApplyGlobalDriveFlags(
        ZipArchive archive,
        string truckId,
        string truckXml,
        bool alwaysOnDiffLock,
        bool alwaysOnAwd)
    {
        var updated = truckXml;
        if (alwaysOnDiffLock)
        {
            updated = ApplyAlwaysOnDiffLock(archive, updated, truckId);
        }

        if (alwaysOnAwd)
        {
            updated = ApplyDriveLayout(updated, TruckDriveLayout.AlwaysAwd);
        }

        return updated;
    }

    private static string ApplyAlwaysOnDiffLock(ZipArchive archive, string truckXml, string truckId)
    {
        if (!HasNativeDiffLockInfrastructure(archive, truckXml, truckId))
        {
            return SetTruckDataAttribute(truckXml, "DiffLockType", "Always");
        }

        ResolveDiffLockAddonNames(archive, truckId, truckXml, out _, out var defaultAddon);
        truckXml = SetTruckDataAttribute(truckXml, "DiffLockType", "Always");
        return SetDiffLockDefaultAddon(truckXml, defaultAddon);
    }

    private static void ValidateMultiplier(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Multiplier must be a positive number.");
        }
    }

    private static TruckDiffLockMode ResolveDiffLockMode(
        ZipArchive archive,
        string truckXml,
        string? diffRaw,
        bool hasNativeDiffLockOptions)
    {
        if (!string.IsNullOrWhiteSpace(diffRaw)
            && diffRaw.Equals("Always", StringComparison.OrdinalIgnoreCase))
        {
            return TruckDiffLockMode.AlwaysOn;
        }

        if (!hasNativeDiffLockOptions)
        {
            return TruckDiffLockMode.None;
        }

        if (TryGetDiffLockDefaultAddonName(truckXml, out var addonName)
            && TryReadAddonText(archive, addonName, out var addonText))
        {
            var installed = DiffLockInstalledRegex.Match(addonText);
            if (installed.Success)
            {
                return installed.Groups["value"].Value.Equals("true", StringComparison.OrdinalIgnoreCase)
                    ? TruckDiffLockMode.Switchable
                    : TruckDiffLockMode.Upgradeable;
            }
        }

        return ParseDiffLockMode(diffRaw);
    }

    private static string ApplyFuelCapacity(string text, int fuelCapacity)
    {
        var match = TruckDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var attrs = match.Groups["attrs"].Value;
        if (!SetOrReplaceAttribute(ref attrs, "FuelCapacity", fuelCapacity.ToString(CultureInfo.InvariantCulture)))
        {
            return text;
        }

        var replacement = $"<TruckData{attrs}>";
        return string.Concat(text.AsSpan(0, match.Index), replacement, text.AsSpan(match.Index + match.Length));
    }

    private static string ApplyGameDataPrice(string text, int price)
    {
        return SetGameDataAttribute(text, "Price", price.ToString(CultureInfo.InvariantCulture));
    }

    private static string ApplyGameDataCountry(string text, string countries) =>
        SetGameDataAttribute(text, "Country", countries);

    private static string ApplyGameDataUnlockByRank(string text, int unlockByRank)
    {
        var clamped = Math.Clamp(unlockByRank, 0, 30);
        return SetGameDataAttribute(text, "UnlockByRank", clamped.ToString(CultureInfo.InvariantCulture));
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

    private static int ExtractGameDataPrice(string text) =>
        ParseInt(ExtractGameDataAttribute(text, "Price"), 0);

    private static int ExtractGameDataUnlockByRank(string text) =>
        Math.Clamp(ParseInt(ExtractGameDataAttribute(text, "UnlockByRank"), 1), 0, 30);

    private static string ExtractGameDataAttribute(string text, string attributeName)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return "";
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        return attrs.TryGetValue(attributeName, out var raw) ? raw : "";
    }

    private static int ReadBaselineInt(string? baselineText, string currentText, string attributeName)
    {
        if (baselineText is not null
            && TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseInt(baselineRaw, 0);
        }

        if (TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
        {
            return ParseInt(currentRaw, 0);
        }

        return 0;
    }

    private static double ReadBaselineDouble(
        string? baselineText,
        string currentText,
        string attributeName,
        double fallback)
    {
        if (baselineText is not null
            && TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseDouble(baselineRaw, fallback);
        }

        if (TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
        {
            return ParseDouble(currentRaw, fallback);
        }

        return fallback;
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

    private static string ApplySteering(string text, TruckTuningDefinition truck)
    {
        var updated = SetTruckDataAttribute(text, "Responsiveness", FormatNumeric(truck.Responsiveness, preferInteger: false));
        if (truck.HasFrontSteer && truck.FrontSteerAngle is { } frontAngle)
        {
            updated = ApplyFrontSteerAngle(updated, frontAngle);
        }

        if (truck.HasRearSteer && truck.RearSteerAngle is { } rearAngle)
        {
            updated = ApplyRearSteerAngle(updated, rearAngle);
        }

        return updated;
    }

    private static string ApplyFrontSteerAngle(string text, double angle)
    {
        var formatted = FormatNumeric(angle, preferInteger: false);
        return SteeringAngleAttributeRegex.Replace(
            text,
            match => ReplaceSteeringAngleIf(match, current => current >= 0, formatted));
    }

    private static string ApplyRearSteerAngle(string text, double angle)
    {
        var formatted = FormatNumeric(angle, preferInteger: false);
        return SteeringAngleAttributeRegex.Replace(
            text,
            match => ReplaceSteeringAngleIf(match, current => current < 0, formatted));
    }

    private static string ReplaceSteeringAngleIf(Match match, Func<double, bool> predicate, string formatted)
    {
        if (!double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current)
            || !predicate(current))
        {
            return match.Value;
        }

        return $"{match.Groups["prefix"].Value}{formatted}{match.Groups["suffix"].Value}";
    }

    private static List<double> ParseSteeringAngles(string text)
    {
        var angles = new List<double>();
        foreach (Match match in SteeringAngleAttributeRegex.Matches(text))
        {
            if (double.TryParse(match.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
            {
                angles.Add(parsed);
            }
        }

        return angles;
    }

    private static (double? Front, double? Rear, bool HasFront, bool HasRear) ParseSteerAngles(string text)
    {
        var angles = ParseSteeringAngles(text);
        var positive = angles.Where(angle => angle > 0).ToArray();
        var negative = angles.Where(angle => angle < 0).ToArray();

        return (
            positive.Length > 0 ? positive.Max() : null,
            negative.Length > 0 ? negative.Min() : null,
            positive.Length > 0,
            negative.Length > 0);
    }

    private static string ApplyDiffLock(
        ZipArchive archive,
        string truckXml,
        TruckTuningDefinition truck)
    {
        if (!truck.HasNativeDiffLockOptions)
        {
            var simpleType = truck.DiffLock switch
            {
                TruckDiffLockMode.AlwaysOn => "Always",
                _ => "None",
            };
            return SetTruckDataAttribute(truckXml, "DiffLockType", simpleType);
        }

        ResolveDiffLockAddonNames(
            archive,
            truck.TruckId,
            truckXml,
            out var installedAddon,
            out var defaultAddon);

        var diffType = truck.DiffLock switch
        {
            TruckDiffLockMode.AlwaysOn => "Always",
            TruckDiffLockMode.None => "None",
            TruckDiffLockMode.Upgradeable => "Uninstalled",
            TruckDiffLockMode.Switchable when IsInstalledStyle(truck.DiffLockTypeRaw) => truck.DiffLockTypeRaw,
            _ => "Installed",
        };
        truckXml = SetTruckDataAttribute(truckXml, "DiffLockType", diffType);

        var defaultAddonName = truck.DiffLock switch
        {
            TruckDiffLockMode.Switchable => installedAddon,
            TruckDiffLockMode.Upgradeable => defaultAddon,
            TruckDiffLockMode.AlwaysOn => defaultAddon,
            TruckDiffLockMode.None => defaultAddon,
            _ => defaultAddon,
        };

        return SetDiffLockDefaultAddon(truckXml, defaultAddonName);
    }

    private static bool HasNativeDiffLockInfrastructure(ZipArchive archive, string truckXml, string truckId)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            if (IsDiffLockSocketBlock(match.Groups["attrs"].Value, match.Groups["body"].Value))
            {
                return true;
            }
        }

        return FindAddonEntry(archive, truckId + "_diff_lock") is not null
            || FindAddonEntry(archive, truckId + "_diff_lock_default") is not null;
    }

    private static string? TryReadTruckText(ZipArchive? archive, string entryPath)
    {
        if (archive is null)
        {
            return null;
        }

        var entry = PakEntryLocator.FindEntry(archive, entryPath);
        return entry is null ? null : ReadEntryText(entry);
    }

    private static void ResolveDiffLockAddonNames(
        ZipArchive archive,
        string truckId,
        string truckXml,
        out string installedAddonName,
        out string defaultAddonName)
    {
        installedAddonName = truckId + "_diff_lock";
        defaultAddonName = truckId + "_diff_lock_default";

        if (TryGetDiffLockDefaultAddonName(truckXml, out var currentDefault))
        {
            if (currentDefault.EndsWith("_default", StringComparison.OrdinalIgnoreCase))
            {
                defaultAddonName = currentDefault;
                installedAddonName = currentDefault[..^"_default".Length];
            }
            else
            {
                installedAddonName = currentDefault;
                defaultAddonName = currentDefault + "_default";
            }
        }

        if (TryReadAddonText(archive, installedAddonName, out _))
        {
            return;
        }

        if (TryReadAddonText(archive, defaultAddonName, out _))
        {
            if (!defaultAddonName.EndsWith("_default", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            installedAddonName = defaultAddonName[..^"_default".Length];
        }
    }

    private static string SetDiffLockDefaultAddon(string truckXml, string defaultAddonName)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!IsDiffLockSocketBlock(attrs, body))
            {
                continue;
            }

            var updatedAttrs = attrs;
            if (!SetOrReplaceAttribute(ref updatedAttrs, "DefaultAddon", defaultAddonName))
            {
                return truckXml;
            }

            var replacement = $"<AddonSockets{updatedAttrs}>{body}</AddonSockets>";
            return string.Concat(
                truckXml.AsSpan(0, match.Index),
                replacement,
                truckXml.AsSpan(match.Index + match.Length));
        }

        return truckXml;
    }

    private static bool IsDiffLockSocketBlock(string attrs, string body) =>
        attrs.Contains("diff_lock", StringComparison.OrdinalIgnoreCase)
        || body.Contains("DiffLock", StringComparison.OrdinalIgnoreCase)
        || body.Contains("diff_lock", StringComparison.OrdinalIgnoreCase);

    private static bool TryGetDiffLockDefaultAddonName(string truckXml, out string addonName)
    {
        foreach (Match match in AddonSocketsBlockRegex.Matches(truckXml))
        {
            var attrs = match.Groups["attrs"].Value;
            var body = match.Groups["body"].Value;
            if (!IsDiffLockSocketBlock(attrs, body))
            {
                continue;
            }

            addonName = GetAttribute(attrs, "DefaultAddon");
            if (!string.IsNullOrWhiteSpace(addonName))
            {
                return true;
            }
        }

        addonName = "";
        return false;
    }

    private static bool TryReadAddonText(ZipArchive archive, string addonName, out string text)
    {
        var entry = FindAddonEntry(archive, addonName);
        if (entry is null)
        {
            text = "";
            return false;
        }

        text = ReadEntryText(entry);
        return true;
    }

    private static ZipArchiveEntry? FindAddonEntry(ZipArchive archive, string addonName)
    {
        var suffix = "/" + addonName + ".xml";
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            {
                return entry;
            }
        }

        return null;
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

    private static string NormalizeKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        var builder = new StringBuilder(value.Length);
        foreach (var ch in value)
        {
            var folded = FoldHomoglyph(ch);
            if (char.IsLetterOrDigit(folded))
            {
                builder.Append(char.ToLowerInvariant(folded));
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Game English strings sometimes use Cyrillic lookalikes (С/Е/М) in model names.
    /// </summary>
    private static char FoldHomoglyph(char ch) => ch switch
    {
        'А' or 'а' => 'A',
        'В' => 'B',
        'С' or 'с' => 'C',
        'Е' or 'е' or 'Ё' or 'ё' => 'E',
        'Н' => 'H',
        'К' or 'к' => 'K',
        'М' or 'м' => 'M',
        'О' or 'о' => 'O',
        'Р' or 'р' => 'P',
        'Т' => 'T',
        'Х' or 'х' => 'X',
        'У' or 'у' => 'Y',
        'І' or 'і' => 'I',
        _ => ch,
    };

    private static string ApplyDriveLayout(string text, TruckDriveLayout layout) =>
        TorqueTagRegex.Replace(text, match =>
        {
            var tag = match.Groups["tag"].Value;
            var attrs = match.Groups["attrs"].Value;
            var location = GetAttribute(attrs, "Location");
            if (!IsFrontDriveTag(tag, location))
            {
                return match.Value;
            }

            var current = GetAttribute(attrs, "Torque");
            var next = TargetFrontTorque(layout, current);
            if (current.Equals(next, StringComparison.OrdinalIgnoreCase))
            {
                return match.Value;
            }

            SetOrReplaceAttribute(ref attrs, "Torque", next);
            return $"<{tag}{attrs}{match.Groups["self"].Value}>";
        });

    private static string TargetFrontTorque(TruckDriveLayout layout, string current) =>
        layout switch
        {
            TruckDriveLayout.Rwd => "none",
            TruckDriveLayout.AlwaysAwd => "default",
            TruckDriveLayout.SelectableAwd =>
                current.Equals("connectable", StringComparison.OrdinalIgnoreCase) ? current : "full",
            _ => current,
        };

    private static TruckDriveLayout InferDriveLayout(string text)
    {
        var frontTorques = new List<string>();
        foreach (Match match in TorqueTagRegex.Matches(text))
        {
            var tag = match.Groups["tag"].Value;
            var attrs = match.Groups["attrs"].Value;
            var location = GetAttribute(attrs, "Location");
            if (!IsFrontDriveTag(tag, location))
            {
                continue;
            }

            frontTorques.Add(GetAttribute(attrs, "Torque"));
        }

        if (frontTorques.Count == 0)
        {
            return TruckDriveLayout.AlwaysAwd;
        }

        var hasNone = frontTorques.Any(value => value.Equals("none", StringComparison.OrdinalIgnoreCase));
        var hasFull = frontTorques.Any(value => value.Equals("full", StringComparison.OrdinalIgnoreCase));
        var hasConnectable = frontTorques.Any(value =>
            value.Equals("connectable", StringComparison.OrdinalIgnoreCase));

        // Torque="full" = cabin AWD switch available.
        // Torque="connectable" only maps to selectable/upgradeable AWD when a TransferBox
        // (or similar) addon socket exists — otherwise the garage shows AWD: No (e.g. Pacific P512).
        if (hasFull || (hasConnectable && HasTransferBoxUpgradePath(text)))
        {
            return TruckDriveLayout.SelectableAwd;
        }

        if (hasNone || hasConnectable)
        {
            return TruckDriveLayout.Rwd;
        }

        return TruckDriveLayout.AlwaysAwd;
    }

    /// <summary>
    /// True when the truck XML exposes an AWD / transfer-case upgrade socket
    /// (game "AWD: Capable"), not merely Torque="connectable" on a front axle.
    /// </summary>
    private static bool HasTransferBoxUpgradePath(string text)
    {
        foreach (Match match in Regex.Matches(
                     text,
                     @"Names\s*=\s*""(?<names>[^""]*)""",
                     RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            var names = match.Groups["names"].Value;
            if (names.Contains("TransferBox", StringComparison.OrdinalIgnoreCase)
                || names.Contains("TransferCase", StringComparison.OrdinalIgnoreCase)
                || names.Contains("AllWheel", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return text.Contains("AllWheelDrive", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsFrontDriveTag(string tag, string location)
    {
        if (IsRearDriveTag(tag, location))
        {
            return false;
        }

        if (tag.Equals("FrontWheel", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FrontAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("Front", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FirstAxle", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return location.StartsWith("front", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRearDriveTag(string tag, string location)
    {
        if (tag.Equals("RearWheel", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("RearAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("Rear", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (tag.Equals("FrontWheel", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FrontAxle", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("Front", StringComparison.OrdinalIgnoreCase)
            || tag.Equals("FirstAxle", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (location.StartsWith("rear", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (location.StartsWith("front", StringComparison.OrdinalIgnoreCase)
            || location.Equals("middle", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private static TruckDiffLockMode ParseDiffLockMode(string? raw) =>
        (raw ?? "").Trim() switch
        {
            var value when value.Equals("Always", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.AlwaysOn,
            var value when value.Equals("None", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.None,
            var value when value.Equals("Uninstalled", StringComparison.OrdinalIgnoreCase) => TruckDiffLockMode.Upgradeable,
            _ => TruckDiffLockMode.Switchable,
        };

    private static bool IsInstalledStyle(string raw) =>
        raw.Equals("Installed", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("Switchable", StringComparison.OrdinalIgnoreCase)
        || raw.Equals("Connected", StringComparison.OrdinalIgnoreCase);

    private static bool IsTruckEntry(string entryPath)
    {
        if (!entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        const string marker = "/classes/trucks/";
        var index = entryPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var relative = entryPath[(index + marker.Length)..];
        return relative.Length > 0
            && !relative.Contains('/')
            && !relative.Contains('\\');
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

    private static string GetAttribute(string attrs, string attributeName)
    {
        var parsed = ParseAttributes(attrs);
        return parsed.TryGetValue(attributeName, out var value) ? value : "";
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
