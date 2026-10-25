using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Cargo;

public static class CargoService
{
    public const int ComBiasLowIndex = 0;
    public const int ComBiasBaselineIndex = 2;
    public const int ComBiasHighIndex = 4;

    private static readonly Regex CargoTypeRegex = new(
        @"CargoType\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static IReadOnlyList<CargoDefinition> LoadCargos(string pakPath, string language = "english")
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
            var cargos = new List<CargoDefinition>();
            foreach (var entry in archive.Entries)
            {
                var entryPath = PartPakPipeline.NormalizeEntryPath(entry.FullName);
                if (!IsCargoXmlEntry(entryPath))
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                if (!VehiclePhysicsMassXml.TryReadTotalMass(text, out _))
                {
                    continue;
                }

                var baselineText = TryReadEntryText(baselineArchive, entryPath) ?? text;
                cargos.Add(ParseCargo(entryPath, text, baselineText, strings));
            }

            return cargos
                .OrderBy(cargo => cargo.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(cargo => cargo.Name, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        finally
        {
            baselineArchive?.Dispose();
        }
    }

    public static CargoSaveResult ApplyGlobal(
        string pakPath,
        double massMultiplier,
        int centerOfMassBiasIndex)
    {
        PartPakPipeline.ValidateMultiplier(massMultiplier, nameof(massMultiplier));
        var bias = Math.Clamp(centerOfMassBiasIndex, ComBiasLowIndex, ComBiasHighIndex);

        var result = PartPakPipeline.BuildBaselineReplacements(
            pakPath,
            IsCargoXmlEntry,
            (_, baselineText, _) => ApplyGlobalToText(baselineText, massMultiplier, bias),
            includeCurrentEntry: (_, currentText) =>
                VehiclePhysicsMassXml.TryReadTotalMass(currentText, out var _));

        var updatedFiles = PartPakPipeline.CommitReplacements(pakPath, result.Replacements);
        return new CargoSaveResult(updatedFiles, result.ChangedItems);
    }

    public static CargoSaveResult RestoreCargosFromBaseline(string pakPath) =>
        ApplyGlobal(pakPath, 1.0, ComBiasBaselineIndex);

    public static CargoSaveResult SaveCargoChanges(
        string pakPath,
        IReadOnlyList<CargoDefinition> cargos)
    {
        ArgumentNullException.ThrowIfNull(cargos);

        Dictionary<string, byte[]> replacements;
        var changedCargos = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var cargo in cargos)
            {
                var entryPath = PartPakPipeline.NormalizeEntryPath(cargo.EntryPath);
                var entry = PakEntryLocator.FindEntry(archive, entryPath);
                if (entry is null)
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                var updated = ApplyCargoTuning(text, cargo);
                if (!string.Equals(text, updated, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updated);
                    changedCargos++;
                }
            }
        }

        if (replacements.Count == 0)
        {
            return new CargoSaveResult(0, 0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new CargoSaveResult(updatedFiles, changedCargos);
    }

    /// <summary>Test helper: apply global mass / CoM bias to a single cargo XML string.</summary>
    public static string ApplyGlobalToTextForTests(
        string baselineText,
        double massMultiplier,
        int centerOfMassBiasIndex) =>
        ApplyGlobalToText(
            baselineText,
            massMultiplier,
            Math.Clamp(centerOfMassBiasIndex, ComBiasLowIndex, ComBiasHighIndex));

    public static bool IsCargoXmlEntry(string entryPath)
    {
        entryPath = PartPakPipeline.NormalizeEntryPath(entryPath);
        return entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            && entryPath.Contains("/classes/trucks/cargo/", StringComparison.OrdinalIgnoreCase);
    }

    private static string ApplyGlobalToText(
        string baselineText,
        double massMultiplier,
        int centerOfMassBiasIndex)
    {
        var updated = VehiclePhysicsMassXml.ScaleAllMasses(baselineText, massMultiplier);

        if (CargoRootBodyXml.TryReadCenterOfMassY(baselineText, out var baselineY))
        {
            var targetY = CargoRootBodyXml.ResolveBiasedY(baselineY, centerOfMassBiasIndex);
            updated = CargoRootBodyXml.ApplyCenterOfMassY(updated, targetY);
        }

        return updated;
    }

    private static string ApplyCargoTuning(string text, CargoDefinition cargo)
    {
        var updated = VehiclePhysicsMassXml.ApplyTotalMass(text, cargo.TotalMass);

        if (cargo.HasCenterOfMassY)
        {
            updated = CargoRootBodyXml.ApplyCenterOfMassY(updated, cargo.CenterOfMassY);
        }

        if (cargo.HasFriction)
        {
            updated = CargoRootBodyXml.ApplyFriction(updated, cargo.Friction);
        }

        return updated;
    }

    private static CargoDefinition ParseCargo(
        string entryPath,
        string text,
        string baselineText,
        IReadOnlyDictionary<string, string> strings)
    {
        var name = Path.GetFileNameWithoutExtension(entryPath);
        var cargoType = ExtractCargoType(text);
        VehiclePhysicsMassXml.TryReadTotalMass(text, out var totalMass);
        VehiclePhysicsMassXml.TryReadTotalMass(baselineText, out var baselineTotal);
        if (baselineTotal <= 0)
        {
            baselineTotal = totalMass;
        }

        var hasCom = CargoRootBodyXml.TryReadCenterOfMassY(text, out var comY);
        var baselineComY = CargoRootBodyXml.TryReadCenterOfMassY(baselineText, out var parsedBaselineCom)
            ? parsedBaselineCom
            : comY;

        var hasFriction = CargoRootBodyXml.TryReadFriction(text, out var friction);
        var baselineFriction = CargoRootBodyXml.TryReadFriction(baselineText, out var parsedBaselineFriction)
            ? parsedBaselineFriction
            : friction;

        var displayFallback = string.IsNullOrWhiteSpace(cargoType) ? name : cargoType;
        return new CargoDefinition
        {
            EntryPath = entryPath,
            Name = name,
            CargoType = cargoType,
            DisplayName = CargoTypeDisplayNames.ResolveDisplayName(strings, cargoType, displayFallback),
            SourceFile = Path.GetFileName(entryPath),
            TotalMass = totalMass,
            BaselineTotalMass = baselineTotal,
            HasCenterOfMassY = hasCom,
            CenterOfMassY = hasCom ? comY : 0,
            BaselineCenterOfMassY = hasCom ? baselineComY : 0,
            HasFriction = hasFriction,
            Friction = hasFriction ? friction : 0,
            BaselineFriction = hasFriction ? baselineFriction : 0,
        };
    }

    private static string ExtractCargoType(string text)
    {
        var match = CargoTypeRegex.Match(text);
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
