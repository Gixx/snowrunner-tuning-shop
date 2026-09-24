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

namespace SnowRunnerTuningShop.Core.AddonCapacity;

public static class AddonCapacityService
{
    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex UiNameRegex = new(
        @"UiName\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly string[] CapacityAttributes =
    [
        "FuelCapacity",
        "WaterCapacity",
        "RepairsCapacity",
        "WheelRepairsCapacity",
    ];

    public static IReadOnlyList<AddonCapacityDefinition> LoadAddons(string pakPath, string language = "english")
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
            var addons = new List<AddonCapacityDefinition>();
            foreach (var entry in archive.Entries)
            {
                var entryPath = PartPakPipeline.NormalizeEntryPath(entry.FullName);
                if (!IsAddonXmlEntry(entryPath))
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                if (!HasAnyCapacityAttribute(text))
                {
                    continue;
                }

                var baselineText = TryReadEntryText(baselineArchive, entryPath) ?? text;
                var addon = ParseAddon(entryPath, text, baselineText, strings);
                if (addon is not null)
                {
                    addons.Add(addon);
                }
            }

            return addons
                .OrderBy(addon => addon.Category, StringComparer.OrdinalIgnoreCase)
                .ThenBy(addon => addon.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            baselineArchive?.Dispose();
        }
    }

    public static AddonCapacitySaveResult ApplyGlobalMultipliers(
        string pakPath,
        double fuelMultiplier,
        double waterMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier)
    {
        PartPakPipeline.ValidateMultiplier(fuelMultiplier, nameof(fuelMultiplier));
        PartPakPipeline.ValidateMultiplier(waterMultiplier, nameof(waterMultiplier));
        PartPakPipeline.ValidateMultiplier(repairsMultiplier, nameof(repairsMultiplier));
        PartPakPipeline.ValidateMultiplier(wheelsMultiplier, nameof(wheelsMultiplier));
        PartPakPipeline.ValidateMultiplier(priceMultiplier, nameof(priceMultiplier));

        var result = PartPakPipeline.BuildBaselineReplacements(
            pakPath,
            IsAddonXmlEntry,
            (_, baselineText, _) => ApplyGlobalMultipliersToText(
                baselineText,
                fuelMultiplier,
                waterMultiplier,
                repairsMultiplier,
                wheelsMultiplier,
                priceMultiplier),
            includeCurrentEntry: (_, currentText) => HasAnyCapacityAttribute(currentText));

        var updatedFiles = PartPakPipeline.CommitReplacements(pakPath, result.Replacements);
        return new AddonCapacitySaveResult(updatedFiles, result.ChangedItems);
    }

    public static AddonCapacitySaveResult RestoreAddonsFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, 1.0, 1.0, 1.0);

    public static AddonCapacitySaveResult SaveAddonChanges(
        string pakPath,
        IReadOnlyList<AddonCapacityDefinition> addons)
    {
        ArgumentNullException.ThrowIfNull(addons);

        Dictionary<string, byte[]> replacements;
        var changedAddons = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var addon in addons)
            {
                var entryPath = PartPakPipeline.NormalizeEntryPath(addon.EntryPath);
                var entry = PakEntryLocator.FindEntry(archive, entryPath);
                if (entry is null)
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                var updated = ApplyAddonTuning(text, addon);
                if (!string.Equals(text, updated, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updated);
                    changedAddons++;
                }
            }
        }

        if (replacements.Count == 0)
        {
            return new AddonCapacitySaveResult(0, 0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new AddonCapacitySaveResult(updatedFiles, changedAddons);
    }

    /// <summary>Test helper: apply global capacity multipliers to a single addon XML string.</summary>
    public static string ApplyGlobalMultipliersToTextForTests(
        string baselineText,
        double fuelMultiplier,
        double waterMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier) =>
        ApplyGlobalMultipliersToText(
            baselineText,
            fuelMultiplier,
            waterMultiplier,
            repairsMultiplier,
            wheelsMultiplier,
            priceMultiplier);

    public static bool IsAddonXmlEntry(string entryPath)
    {
        entryPath = PartPakPipeline.NormalizeEntryPath(entryPath);
        return entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && entryPath.Contains("/classes/trucks/addons/", StringComparison.OrdinalIgnoreCase);
    }

    public static bool HasAnyCapacityAttribute(string text)
    {
        if (!TruckDataOpenRegex.IsMatch(text))
        {
            return false;
        }

        foreach (var attribute in CapacityAttributes)
        {
            if (VehicleGameDataXml.TryGetTruckDataAttribute(text, attribute, out _))
            {
                return true;
            }
        }

        return false;
    }

    private static AddonCapacityDefinition? ParseAddon(
        string entryPath,
        string text,
        string baselineText,
        IReadOnlyDictionary<string, string> strings)
    {
        var match = TruckDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return null;
        }

        var attrs = VehicleGameDataXml.ParseAttributes(match.Groups["attrs"].Value);
        var hasFuel = TryParsePresentInt(attrs, "FuelCapacity", out var fuel);
        var hasWater = TryParsePresentInt(attrs, "WaterCapacity", out var water);
        var hasRepairs = TryParsePresentInt(attrs, "RepairsCapacity", out var repairs);
        var hasWheels = TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels);
        if (!hasFuel && !hasWater && !hasRepairs && !hasWheels)
        {
            return null;
        }

        var name = Path.GetFileNameWithoutExtension(entryPath);
        var uiKey = ExtractUiName(text);
        var hasPrice = !string.IsNullOrEmpty(VehicleGameDataXml.ExtractGameDataAttribute(text, "Price"));
        var price = VehicleGameDataXml.ExtractGameDataInt(text, "Price", 0);
        var baselinePrice = VehicleGameDataXml.ExtractGameDataInt(baselineText, "Price", price);

        return new AddonCapacityDefinition
        {
            EntryPath = entryPath,
            Name = name,
            UiNameKey = uiKey,
            DisplayName = GameStringsReader.Resolve(strings, uiKey, name),
            SourceFile = Path.GetFileName(entryPath),
            Category = VehicleGameDataXml.ExtractGameDataAttribute(text, "Category"),
            HasPrice = hasPrice,
            Price = price,
            BaselinePrice = baselinePrice,
            HasFuel = hasFuel,
            FuelCapacity = fuel,
            BaselineFuelCapacity = ReadBaselineCapacity(baselineText, "FuelCapacity", fuel),
            HasWater = hasWater,
            WaterCapacity = water,
            BaselineWaterCapacity = ReadBaselineCapacity(baselineText, "WaterCapacity", water),
            HasRepairs = hasRepairs,
            RepairsCapacity = repairs,
            BaselineRepairsCapacity = ReadBaselineCapacity(baselineText, "RepairsCapacity", repairs),
            HasWheels = hasWheels,
            WheelRepairsCapacity = wheels,
            BaselineWheelRepairsCapacity = ReadBaselineCapacity(baselineText, "WheelRepairsCapacity", wheels),
        };
    }

    private static string ApplyAddonTuning(string text, AddonCapacityDefinition addon)
    {
        var updated = text;
        if (addon.HasFuel)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "FuelCapacity",
                Math.Clamp(addon.FuelCapacity, 0, 10_000));
        }

        if (addon.HasWater)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "WaterCapacity",
                Math.Clamp(addon.WaterCapacity, 0, 10_000));
        }

        if (addon.HasRepairs)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "RepairsCapacity",
                Math.Clamp(addon.RepairsCapacity, 0, 10_000));
        }

        if (addon.HasWheels)
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "WheelRepairsCapacity",
                Math.Clamp(addon.WheelRepairsCapacity, 0, 99));
        }

        if (addon.HasPrice)
        {
            updated = VehicleGameDataXml.SetGameDataAttribute(
                updated,
                "Price",
                Math.Clamp(addon.Price, 0, 9_999_999).ToString(CultureInfo.InvariantCulture));
        }

        return updated;
    }

    private static string ApplyGlobalMultipliersToText(
        string baselineText,
        double fuelMultiplier,
        double waterMultiplier,
        double repairsMultiplier,
        double wheelsMultiplier,
        double priceMultiplier)
    {
        if (!HasAnyCapacityAttribute(baselineText))
        {
            return baselineText;
        }

        var match = TruckDataOpenRegex.Match(baselineText);
        if (!match.Success)
        {
            return baselineText;
        }

        var attrs = VehicleGameDataXml.ParseAttributes(match.Groups["attrs"].Value);
        var updated = baselineText;

        if (TryParsePresentInt(attrs, "FuelCapacity", out var fuel))
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "FuelCapacity",
                Scale(fuel, fuelMultiplier, 0, 10_000));
        }

        if (TryParsePresentInt(attrs, "WaterCapacity", out var water))
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "WaterCapacity",
                Scale(water, waterMultiplier, 0, 10_000));
        }

        if (TryParsePresentInt(attrs, "RepairsCapacity", out var repairs))
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "RepairsCapacity",
                Scale(repairs, repairsMultiplier, 0, 10_000));
        }

        if (TryParsePresentInt(attrs, "WheelRepairsCapacity", out var wheels))
        {
            updated = VehicleGameDataXml.ApplyExistingTruckDataInt(
                updated,
                "WheelRepairsCapacity",
                Scale(wheels, wheelsMultiplier, 0, 99));
        }

        var baselinePrice = VehicleGameDataXml.ExtractGameDataInt(baselineText, "Price", 0);
        if (baselinePrice > 0 && !TuningMultiplierPresets.IsBaselineMultiplier(priceMultiplier))
        {
            updated = VehicleGameDataXml.SetGameDataAttribute(
                updated,
                "Price",
                Scale(baselinePrice, priceMultiplier, 0, 9_999_999).ToString(CultureInfo.InvariantCulture));
        }

        return updated;
    }

    private static int Scale(int baseline, double multiplier, int min, int max)
    {
        if (TuningMultiplierPresets.IsBaselineMultiplier(multiplier))
        {
            return Math.Clamp(baseline, min, max);
        }

        return (int)Math.Clamp(
            Math.Round(baseline * multiplier, MidpointRounding.AwayFromZero),
            min,
            max);
    }

    private static bool TryParsePresentInt(
        IReadOnlyDictionary<string, string> attrs,
        string attributeName,
        out int value)
    {
        value = 0;
        if (!attrs.TryGetValue(attributeName, out var raw))
        {
            return false;
        }

        return int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static int ReadBaselineCapacity(string baselineText, string attributeName, int fallback)
    {
        if (VehicleGameDataXml.TryGetTruckDataAttribute(baselineText, attributeName, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static string ExtractUiName(string text)
    {
        var match = UiNameRegex.Match(text);
        return match.Success ? match.Groups["value"].Value : "";
    }

    private static string? TryReadEntryText(ZipArchive? archive, string entryPath)
    {
        if (archive is null)
        {
            return null;
        }

        var entry = PakEntryLocator.FindEntry(archive, entryPath);
        return entry is null ? null : PartXmlHelpers.ReadEntryUtf8(entry);
    }
}
