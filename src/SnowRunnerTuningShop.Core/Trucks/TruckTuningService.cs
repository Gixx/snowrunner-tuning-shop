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

public static class TruckTuningService
{
    public const double GlobalFrontSteerMinimumDegrees = 10;
    public const double GlobalFrontSteerMaximumDegrees = 60;

    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex VehicleUiNameRegex = new(
        @"UiName\s*=\s*""(?<value>UI_VEHICLE_[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex TorqueTagRegex = new(
        @"<(?<tag>FrontWheel|RearWheel|FirstAxle|SecondAxle|ThirdAxle|FourthAxle|FrontAxle|RearAxle|MiddleAxle|MiddleWheel|Front|Rear)\b(?<attrs>[^<>]*\bTorque\s*=\s*""[^""]*""[^<>]*)(?<self>/?)>",
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

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
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

    /// <summary>
    /// Resolves a catalog card to a pak truck by XML file id (<c>pakId</c> / catalog id), never by UI language.
    /// </summary>
    public static TruckTuningDefinition? FindByCatalog(
        IReadOnlyList<TruckTuningDefinition> trucks,
        string catalogId,
        string? pakId = null) =>
        PakFileId.Find(trucks, truck => truck.TruckId, truck => truck.EntryPath, pakId, catalogId);

    public static TruckTuningSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double fuelMultiplier,
        TruckFrontSteerGlobalMode frontSteerMode,
        double responsivenessMultiplier,
        double priceMultiplier,
        bool alwaysOnDiffLock = false,
        bool alwaysOnAwd = false)
    {
        PartPakPipeline.ValidateMultiplier(fuelMultiplier, nameof(fuelMultiplier));
        PartPakPipeline.ValidateMultiplier(responsivenessMultiplier, nameof(responsivenessMultiplier));
        PartPakPipeline.ValidateMultiplier(priceMultiplier, nameof(priceMultiplier));
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

                var baselineText = PakVanillaText.Read(baselineArchive, entry, PartXmlHelpers.ReadEntryUtf8);
                var updatedText = transformBaselineText(currentArchive, entryPath, baselineText);

                var currentText = PartXmlHelpers.ReadEntryUtf8(entry);
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

                var currentText = PartXmlHelpers.ReadEntryUtf8(entry);
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

            var text = PartXmlHelpers.ReadEntryUtf8(entry);
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

        var attrs = VehicleGameDataXml.ParseAttributes(truckData.Groups["attrs"].Value);
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
            ? TruckDiffLockXml.HasNativeDiffLockInfrastructure(baselineArchive!, baselineText, truckId)
            : TruckDiffLockXml.HasNativeDiffLockInfrastructure(archive, text, truckId);

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
            StoreCountries = VehicleGameDataXml.ExtractGameDataAttribute(text, "Country"),
            BaselineStoreCountries = baselineText is not null
                ? VehicleGameDataXml.ExtractGameDataAttribute(baselineText, "Country")
                : VehicleGameDataXml.ExtractGameDataAttribute(text, "Country"),
            UnlockByRank = ExtractGameDataUnlockByRank(text),
            BaselineUnlockByRank = baselineText is not null
                ? ExtractGameDataUnlockByRank(baselineText)
                : ExtractGameDataUnlockByRank(text),
            DiffLockTypeRaw = diffRaw ?? "",
            HasNativeDiffLockOptions = hasNativeDiffLockOptions,
            DiffLock = TruckDiffLockXml.ResolveDiffLockMode(archive, text, diffRaw, hasNativeDiffLockOptions),
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
        updated = TruckDiffLockXml.ApplyDiffLock(archive, updated, truck);
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

        var attrs = VehicleGameDataXml.ParseAttributes(truckData.Groups["attrs"].Value);
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
        updated = VehicleGameDataXml.SetTruckDataAttribute(
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
            updated = TruckDiffLockXml.ApplyAlwaysOnDiffLock(archive, updated, truckId);
        }

        if (alwaysOnAwd)
        {
            updated = ApplyDriveLayout(updated, TruckDriveLayout.AlwaysAwd);
        }

        return updated;
    }

    private static string ApplyFuelCapacity(string text, int fuelCapacity) =>
        VehicleGameDataXml.SetTruckDataAttribute(text, "FuelCapacity", fuelCapacity.ToString(CultureInfo.InvariantCulture));

    private static string ApplyGameDataPrice(string text, int price)
    {
        return VehicleGameDataXml.SetGameDataAttribute(text, "Price", price.ToString(CultureInfo.InvariantCulture));
    }

    private static string ApplyGameDataCountry(string text, string countries) =>
        VehicleGameDataXml.SetGameDataAttribute(text, "Country", countries);

    private static string ApplyGameDataUnlockByRank(string text, int unlockByRank)
    {
        var clamped = Math.Clamp(unlockByRank, 0, 30);
        return VehicleGameDataXml.SetGameDataAttribute(text, "UnlockByRank", clamped.ToString(CultureInfo.InvariantCulture));
    }

    private static int ExtractGameDataPrice(string text) =>
        ParseInt(VehicleGameDataXml.ExtractGameDataAttribute(text, "Price"), 0);

    private static int ExtractGameDataUnlockByRank(string text) =>
        Math.Clamp(ParseInt(VehicleGameDataXml.ExtractGameDataAttribute(text, "UnlockByRank"), 1), 0, 30);

    private static int ReadBaselineInt(string? baselineText, string currentText, string attributeName)
    {
        if (baselineText is not null
            && VehicleGameDataXml.TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseInt(baselineRaw, 0);
        }

        if (VehicleGameDataXml.TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
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
            && VehicleGameDataXml.TryGetTruckDataAttribute(baselineText, attributeName, out var baselineRaw))
        {
            return ParseDouble(baselineRaw, fallback);
        }

        if (VehicleGameDataXml.TryGetTruckDataAttribute(currentText, attributeName, out var currentRaw))
        {
            return ParseDouble(currentRaw, fallback);
        }

        return fallback;
    }

    private static string ApplySteering(string text, TruckTuningDefinition truck)
    {
        var updated = VehicleGameDataXml.SetTruckDataAttribute(text, "Responsiveness", FormatNumeric(truck.Responsiveness, preferInteger: false));
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

    private static string? TryReadTruckText(ZipArchive? archive, string entryPath)
    {
        if (archive is null)
        {
            return null;
        }

        var entry = PakEntryLocator.FindEntry(archive, entryPath);
        return entry is null ? null : PartXmlHelpers.ReadEntryUtf8(entry);
    }

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

            VehicleGameDataXml.SetOrReplaceAttribute(ref attrs, "Torque", next);
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

    private static string GetAttribute(string attrs, string attributeName)
    {
        var parsed = VehicleGameDataXml.ParseAttributes(attrs);
        return parsed.TryGetValue(attributeName, out var value) ? value : "";
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

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
