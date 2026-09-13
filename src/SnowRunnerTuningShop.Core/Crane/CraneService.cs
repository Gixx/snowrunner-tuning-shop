using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Strings;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Crane;

public static class CraneService
{
    /// <summary>
    /// Crane addon XML often has sibling roots (<c>_templates</c> + <c>TruckAddon</c>),
    /// which XDocument rejects unless wrapped.
    /// </summary>
    private const string SyntheticRootName = "__crane_root__";

    private static readonly Regex AddonTypeRegex = new(
        $@"<AddonType\b{XmlTagRegex.AttrsMaybeSelfClose}\bName\s*=\s*""(?<type>Crane|LogCrane)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> IkSpeedAttributes = new(StringComparer.OrdinalIgnoreCase)
    {
        "CoeffEndMovementSpeedOY",
        "CoeffEndMovementSpeedOYWithLoad",
        "CoeffEndMovementSpeedXZ",
        "CoeffEndMovementSpeedXZWithLoad",
    };

    public static IReadOnlyList<CraneDefinition> LoadCranes(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        var cranes = new List<CraneDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsAddonXmlEntry(entryPath))
            {
                continue;
            }

            var content = PartXmlHelpers.ReadEntryUtf8(entry);
            var crane = TryParseCrane(entryPath, content, strings);
            if (crane is not null)
            {
                cranes.Add(crane);
            }
        }

        return cranes
            .OrderBy(crane => crane.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(crane => crane.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static CraneSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double armForceMultiplier,
        double movementSpeedMultiplier)
    {
        PartPakPipeline.ValidateMultiplier(armForceMultiplier, nameof(armForceMultiplier));
        PartPakPipeline.ValidateMultiplier(movementSpeedMultiplier, nameof(movementSpeedMultiplier));

        var result = PartPakPipeline.BuildBaselineReplacements(
            pakPath,
            IsAddonXmlEntry,
            (_, baselineText, currentText) =>
            {
                var source = IsCraneAddonText(baselineText) ? baselineText : currentText;
                return ApplyMultipliersToText(
                    source,
                    armForceMultiplier,
                    movementSpeedMultiplier,
                    out int _);
            },
            includeCurrentEntry: (_, currentText) => IsCraneAddonText(currentText));

        var updatedFiles = PartPakPipeline.CommitReplacements(pakPath, result.Replacements);
        return new CraneSaveResult(updatedFiles, result.ChangedItems);
    }

    public static CraneSaveResult RestoreCranesFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0);

    public static CraneSaveResult SaveCraneChanges(string pakPath, IReadOnlyList<CraneDefinition> cranes)
    {
        ArgumentNullException.ThrowIfNull(cranes);

        Dictionary<string, byte[]> replacements;
        var changedCranes = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var crane in cranes)
            {
                var entryPath = crane.EntryPath.Replace('\\', '/');
                var entry = PakEntryLocator.FindEntry(archive, entryPath);
                if (entry is null)
                {
                    continue;
                }

                var text = PartXmlHelpers.ReadEntryUtf8(entry);
                if (!TryApplyCraneUpdatesToText(text, crane, out var updatedText))
                {
                    continue;
                }

                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedCranes++;
                }
            }
        }

        if (replacements.Count == 0)
        {
            return new CraneSaveResult(UpdatedFiles: 0, ChangedCranes: 0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new CraneSaveResult(updatedFiles, changedCranes);
    }

    /// <summary>Test helper: scale arm motors and ControlledIK speeds from baseline XML text.</summary>
    public static string ApplyMultipliersToTextForTests(
        string backupText,
        double armForceMultiplier,
        double movementSpeedMultiplier) =>
        ApplyMultipliersToText(backupText, armForceMultiplier, movementSpeedMultiplier, out _);

    private static string ApplyMultipliersToText(
        string backupText,
        double armForceMultiplier,
        double movementSpeedMultiplier,
        out int changedCount)
    {
        changedCount = 0;
        if (!IsCraneAddonText(backupText))
        {
            return backupText;
        }

        var forceIsBaseline = TuningMultiplierPresets.IsBaselineMultiplier(armForceMultiplier);
        var speedIsBaseline = TuningMultiplierPresets.IsBaselineMultiplier(movementSpeedMultiplier);
        if (forceIsBaseline && speedIsBaseline)
        {
            return backupText;
        }

        try
        {
            var document = ParseCraneDocument(backupText);
            var anyChanges = false;

            if (!forceIsBaseline)
            {
                anyChanges |= ScaleArmMotorForces(document, armForceMultiplier);
            }

            if (!speedIsBaseline)
            {
                anyChanges |= ScaleIkSpeeds(document, movementSpeedMultiplier);
            }

            if (!anyChanges)
            {
                return backupText;
            }

            changedCount = 1;
            return SerializeCraneDocument(document);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Crane addon XML is invalid or corrupted in the pak. " +
                "Use \"Restore entire pak...\" from the baseline panel, then apply multipliers again.",
                ex);
        }
    }

    private static bool TryApplyCraneUpdatesToText(
        string content,
        CraneDefinition target,
        out string updatedText)
    {
        updatedText = content;
        if (!IsCraneAddonText(content))
        {
            return false;
        }

        try
        {
            var document = ParseCraneDocument(content);
            var anyChanges = false;

            var currentForces = CollectArmMotorForces(document);
            if (currentForces.Count > 0 && target.AverageArmForce > 0)
            {
                var currentAverage = currentForces.Average();
                if (currentAverage > 1e-9
                    && Math.Abs(currentAverage - target.AverageArmForce) > 1e-6)
                {
                    var scale = target.AverageArmForce / currentAverage;
                    anyChanges |= ScaleArmMotorForces(document, scale);
                }
            }

            anyChanges |= SetIkSpeeds(
                document,
                target.SpeedOY,
                target.SpeedOYWithLoad,
                target.SpeedXZ,
                target.SpeedXZWithLoad);

            if (!anyChanges)
            {
                return false;
            }

            updatedText = SerializeCraneDocument(document);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Crane addon XML is invalid or corrupted in the pak. " +
                "Use \"Restore entire pak...\" from the baseline panel, then apply multipliers again.",
                ex);
        }
    }

    private static CraneDefinition? TryParseCrane(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings)
    {
        var addonType = ExtractAddonType(content);
        if (addonType is null)
        {
            return null;
        }

        try
        {
            var document = ParseCraneDocument(content);
            return CreateDefinition(entryPath, addonType, document, strings);
        }
        catch
        {
            return CreateDefinitionFromRegex(entryPath, addonType, content, strings);
        }
    }

    private static XDocument ParseCraneDocument(string content)
    {
        try
        {
            return XDocument.Parse(content, LoadOptions.PreserveWhitespace);
        }
        catch (System.Xml.XmlException)
        {
            // Sibling roots: <_templates>…</_templates><TruckAddon>…</TruckAddon>
            return XDocument.Parse(
                $"<{SyntheticRootName}>{content}</{SyntheticRootName}>",
                LoadOptions.PreserveWhitespace);
        }
    }

    private static string SerializeCraneDocument(XDocument document)
    {
        if (document.Root is null)
        {
            return string.Empty;
        }

        if (!document.Root.Name.LocalName.Equals(SyntheticRootName, StringComparison.Ordinal))
        {
            return document.ToString(SaveOptions.DisableFormatting);
        }

        var builder = new StringBuilder();
        foreach (var node in document.Root.Nodes())
        {
            builder.Append(node.ToString(SaveOptions.DisableFormatting));
        }

        return builder.ToString();
    }

    private static CraneDefinition CreateDefinition(
        string entryPath,
        string addonType,
        XDocument document,
        IReadOnlyDictionary<string, string>? strings)
    {
        var name = Path.GetFileNameWithoutExtension(entryPath.Replace('\\', '/'));
        var uiNameKey = document.Descendants()
            .FirstOrDefault(node => node.Name.LocalName.Equals("UiDesc", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("UiName")
            ?.Value
            ?.Trim() ?? "";
        var priceText = document.Descendants()
            .FirstOrDefault(node => node.Name.LocalName.Equals("GameData", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("Price")
            ?.Value;

        var forces = CollectArmMotorForces(document);
        ReadIkSpeeds(
            document,
            out var speedOY,
            out var speedOYWithLoad,
            out var speedXZ,
            out var speedXZWithLoad);

        var displayName = strings is null
            ? name
            : GameStringsReader.Resolve(strings, uiNameKey, name);

        return new CraneDefinition
        {
            EntryPath = entryPath,
            Name = name,
            UiNameKey = uiNameKey,
            DisplayName = displayName,
            SourceFile = Path.GetFileName(entryPath.Replace('\\', '/')),
            Category = InferCategory(entryPath, addonType),
            AddonType = addonType,
            Price = int.TryParse(priceText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price)
                ? price
                : 0,
            ArmMotorCount = forces.Count,
            AverageArmForce = forces.Count == 0 ? 0 : Math.Round(forces.Average(), MidpointRounding.AwayFromZero),
            SpeedOY = speedOY,
            SpeedOYWithLoad = speedOYWithLoad,
            SpeedXZ = speedXZ,
            SpeedXZWithLoad = speedXZWithLoad,
        };
    }

    private static CraneDefinition CreateDefinitionFromRegex(
        string entryPath,
        string addonType,
        string content,
        IReadOnlyDictionary<string, string>? strings)
    {
        var name = Path.GetFileNameWithoutExtension(entryPath.Replace('\\', '/'));
        var uiMatch = Regex.Match(
            content,
            @"UiName\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        var uiNameKey = uiMatch.Success ? uiMatch.Groups["value"].Value.Trim() : "";
        var displayName = strings is null
            ? name
            : GameStringsReader.Resolve(strings, uiNameKey, name);

        return new CraneDefinition
        {
            EntryPath = entryPath,
            Name = name,
            UiNameKey = uiNameKey,
            DisplayName = displayName,
            SourceFile = Path.GetFileName(entryPath.Replace('\\', '/')),
            Category = InferCategory(entryPath, addonType),
            AddonType = addonType,
            Price = PartXmlHelpers.ExtractPrice(content),
            ArmMotorCount = 0,
            AverageArmForce = 0,
            SpeedOY = 1,
            SpeedOYWithLoad = 0.5,
            SpeedXZ = 1,
            SpeedXZWithLoad = 0.5,
        };
    }

    private static bool ScaleArmMotorForces(XDocument document, double multiplier)
    {
        var anyChanges = false;
        foreach (var motor in EnumerateArmMotors(document))
        {
            var forceAttr = motor.Attribute("Force");
            if (forceAttr is null || string.IsNullOrWhiteSpace(forceAttr.Value))
            {
                continue;
            }

            if (!double.TryParse(forceAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var force))
            {
                continue;
            }

            var scaled = Math.Round(force * multiplier, MidpointRounding.AwayFromZero);
            var formatted = FormatForce(scaled);
            if (!string.Equals(forceAttr.Value, formatted, StringComparison.Ordinal))
            {
                forceAttr.Value = formatted;
                anyChanges = true;
            }
        }

        return anyChanges;
    }

    private static bool ScaleIkSpeeds(XDocument document, double multiplier)
    {
        var anyChanges = false;
        foreach (var ik in document.Descendants()
            .Where(node => node.Name.LocalName.Equals("ControlledIK", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var attributeName in IkSpeedAttributes)
            {
                var attribute = ik.Attribute(attributeName);
                if (attribute is null || string.IsNullOrWhiteSpace(attribute.Value))
                {
                    continue;
                }

                if (!double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                {
                    continue;
                }

                var scaled = XmlNumericFormatting.Round(value * multiplier);
                var formatted = FormatIk(scaled);
                if (!string.Equals(attribute.Value, formatted, StringComparison.Ordinal))
                {
                    attribute.Value = formatted;
                    anyChanges = true;
                }
            }
        }

        return anyChanges;
    }

    private static bool SetIkSpeeds(
        XDocument document,
        double speedOY,
        double speedOYWithLoad,
        double speedXZ,
        double speedXZWithLoad)
    {
        var targets = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
        {
            ["CoeffEndMovementSpeedOY"] = speedOY,
            ["CoeffEndMovementSpeedOYWithLoad"] = speedOYWithLoad,
            ["CoeffEndMovementSpeedXZ"] = speedXZ,
            ["CoeffEndMovementSpeedXZWithLoad"] = speedXZWithLoad,
        };

        var anyChanges = false;
        foreach (var ik in document.Descendants()
            .Where(node => node.Name.LocalName.Equals("ControlledIK", StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var (attributeName, targetValue) in targets)
            {
                var attribute = ik.Attribute(attributeName);
                if (attribute is null)
                {
                    continue;
                }

                if (double.TryParse(attribute.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var current)
                    && Math.Abs(current - targetValue) < 1e-9)
                {
                    continue;
                }

                var formatted = FormatIk(targetValue);
                if (!string.Equals(attribute.Value, formatted, StringComparison.Ordinal))
                {
                    attribute.Value = formatted;
                    anyChanges = true;
                }
            }
        }

        return anyChanges;
    }

    private static List<double> CollectArmMotorForces(XDocument document)
    {
        var forces = new List<double>();
        foreach (var motor in EnumerateArmMotors(document))
        {
            var raw = motor.Attribute("Force")?.Value;
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var force)
                && force > 0)
            {
                forces.Add(force);
            }
        }

        return forces;
    }

    private static IEnumerable<XElement> EnumerateArmMotors(XDocument document)
    {
        foreach (var constraint in document.Descendants()
            .Where(node => node.Name.LocalName.Equals("Constraint", StringComparison.OrdinalIgnoreCase)))
        {
            if (!IsArmConstraintName(constraint.Attribute("Name")?.Value))
            {
                continue;
            }

            foreach (var motor in constraint.Descendants()
                .Where(node => node.Name.LocalName.Equals("Motor", StringComparison.OrdinalIgnoreCase)))
            {
                var type = motor.Attribute("Type")?.Value;
                if (!string.IsNullOrWhiteSpace(type)
                    && !type.Equals("Position", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return motor;
            }
        }
    }

    private static void ReadIkSpeeds(
        XDocument document,
        out double speedOY,
        out double speedOYWithLoad,
        out double speedXZ,
        out double speedXZWithLoad)
    {
        speedOY = 1;
        speedOYWithLoad = 0.5;
        speedXZ = 1;
        speedXZWithLoad = 0.5;

        var ik = document.Descendants()
            .FirstOrDefault(node => node.Name.LocalName.Equals("ControlledIK", StringComparison.OrdinalIgnoreCase));
        if (ik is null)
        {
            return;
        }

        speedOY = ParseDouble(ik.Attribute("CoeffEndMovementSpeedOY")?.Value, speedOY);
        speedOYWithLoad = ParseDouble(ik.Attribute("CoeffEndMovementSpeedOYWithLoad")?.Value, speedOYWithLoad);
        speedXZ = ParseDouble(ik.Attribute("CoeffEndMovementSpeedXZ")?.Value, speedXZ);
        speedXZWithLoad = ParseDouble(ik.Attribute("CoeffEndMovementSpeedXZWithLoad")?.Value, speedXZWithLoad);
    }

    internal static bool IsArmConstraintName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (name.Contains("Anchor", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (name.Contains("Grappler", StringComparison.OrdinalIgnoreCase)
            && !name.Equals("GrapplerBase", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return name.Equals("Crane", StringComparison.OrdinalIgnoreCase)
            || name.StartsWith("Arm", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Cabin", StringComparison.OrdinalIgnoreCase)
            || name.Equals("GrapplerBase", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsAddonXmlEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/addons/", StringComparison.OrdinalIgnoreCase)
        && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    private static bool IsCraneAddonText(string content) =>
        ExtractAddonType(content) is not null;

    private static string? ExtractAddonType(string content)
    {
        var match = AddonTypeRegex.Match(content);
        return match.Success ? match.Groups["type"].Value : null;
    }

    private static string InferCategory(string entryPath, string addonType)
    {
        if (entryPath.Contains("/_dlc/", StringComparison.OrdinalIgnoreCase))
        {
            return $"DLC {addonType}";
        }

        return addonType;
    }


    private static double ParseDouble(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;

    private static string FormatForce(double value) =>
        XmlNumericFormatting.Format(value, preferInteger: true);

    private static string FormatIk(double value) =>
        XmlNumericFormatting.Format(value, preferInteger: false, keepTrailingDotZero: true);

}

public sealed record CraneSaveResult(int UpdatedFiles, int ChangedCranes);
