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

namespace SnowRunnerTuningShop.Core.Winch;

public static class WinchService
{
    private static readonly Regex WinchBlockRegex = new(
        @"<Winch\b(?<attrs>[^<>]*?)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyList<WinchDefinition> LoadWinches(string pakPath, string language = "english")
    {
        var strings = GameStringsReader.LoadFromPak(pakPath, language);
        using var archive = ZipFile.OpenRead(pakPath);
        var winches = new List<WinchDefinition>();

        foreach (var entry in archive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!IsWinchEntry(entryPath))
            {
                continue;
            }

            using var stream = entry.Open();
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var content = reader.ReadToEnd();
            winches.AddRange(ParseWinchesFromText(entryPath, content, strings));
        }

        return winches
            .OrderBy(winch => winch.Category, StringComparer.OrdinalIgnoreCase)
            .ThenBy(winch => winch.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static WinchSaveResult ApplyGlobalMultipliers(
        string pakPath,
        double lengthMultiplier,
        double strengthMultiplier,
        bool forceAutonomousAll = false)
    {
        ValidateMultiplier(lengthMultiplier, nameof(lengthMultiplier));
        ValidateMultiplier(strengthMultiplier, nameof(strengthMultiplier));

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);

        Dictionary<string, byte[]> replacements;
        var changedWinches = 0;

        using (var backupArchive = ZipFile.OpenRead(baselinePath))
        using (var currentArchive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var entry in currentArchive.Entries)
            {
                var entryPath = entry.FullName.Replace('\\', '/');
                if (!IsWinchEntry(entryPath))
                {
                    continue;
                }

                var backupText = PakVanillaText.Read(backupArchive, entry, ReadEntryText);
                var updatedText = ApplyMultipliersToText(
                    backupText,
                    lengthMultiplier,
                    strengthMultiplier,
                    forceAutonomousAll,
                    out _);

                var currentText = ReadEntryText(entry);
                if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
                {
                    replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                    changedWinches += CountWinchAttributeDifferences(currentText, updatedText);
                }
            }
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new WinchSaveResult(updatedFiles, changedWinches);
    }

    public static WinchSaveResult RestoreWinchesFromBaseline(string pakPath) =>
        ApplyGlobalMultipliers(pakPath, 1.0, 1.0, forceAutonomousAll: false);

    public static WinchSaveResult SaveWinchChanges(string pakPath, IReadOnlyList<WinchDefinition> winches)
    {
        ArgumentNullException.ThrowIfNull(winches);

        Dictionary<string, byte[]> replacements;
        var changedWinches = 0;

        using (var archive = ZipFile.OpenRead(pakPath))
        {
            var grouped = winches
                .GroupBy(winch => winch.EntryPath.Replace('\\', '/'), StringComparer.Ordinal)
                .ToArray();

            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            foreach (var group in grouped)
            {
                var entry = PakEntryLocator.FindEntry(archive, group.Key);
                if (entry is null)
                {
                    continue;
                }

                var text = ReadEntryText(entry);
                var updates = new Dictionary<string, WinchAttributeValues>(StringComparer.OrdinalIgnoreCase);

                foreach (var winch in group)
                {
                    updates[winch.Name] = new WinchAttributeValues(
                        winch.Length,
                        winch.StrengthMult,
                        winch.IsEngineIgnitionRequired);
                }

                if (!TryApplyWinchUpdatesToText(text, updates, out var updatedText, out var fileChangedWinches))
                {
                    continue;
                }

                changedWinches += fileChangedWinches;

                if (!string.Equals(text, updatedText, StringComparison.Ordinal))
                {
                    replacements[group.Key] = Encoding.UTF8.GetBytes(updatedText);
                }
            }
        }

        if (replacements.Count == 0)
        {
            return new WinchSaveResult(UpdatedFiles: 0, ChangedWinches: 0);
        }

        var updatedFiles = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new WinchSaveResult(updatedFiles, changedWinches);
    }

    private static bool IsWinchEntry(string entryPath)
    {
        return entryPath.Contains("/classes/winches/", StringComparison.OrdinalIgnoreCase)
            && entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);
    }

    private static List<WinchDefinition> ParseWinchesFromText(
        string entryPath,
        string content,
        IReadOnlyDictionary<string, string>? strings = null)
    {
        var winches = new List<WinchDefinition>();
        if (!content.Contains("<Winch", StringComparison.OrdinalIgnoreCase))
        {
            return winches;
        }

        try
        {
            var document = XDocument.Parse(content);
            foreach (var element in document.Descendants().Where(node => node.Name.LocalName.Equals("Winch", StringComparison.OrdinalIgnoreCase)))
            {
                var name = element.Attribute("Name")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var uiNameKey = ExtractUiNameKey(element);
                var priceText = element.Descendants()
                    .FirstOrDefault(node => node.Name.LocalName.Equals("GameData", StringComparison.OrdinalIgnoreCase))
                    ?.Attribute("Price")
                    ?.Value;
                winches.Add(CreateWinchDefinition(
                    entryPath,
                    name,
                    uiNameKey,
                    strings,
                    element.Attribute("Length")?.Value,
                    element.Attribute("StrengthMult")?.Value,
                    element.Attribute("IsEngineIgnitionRequired")?.Value,
                    priceText));
            }

            return winches;
        }
        catch
        {
            foreach (Match match in WinchBlockRegex.Matches(content))
            {
                var attrs = ParseAttributes(match.Groups["attrs"].Value);
                if (!attrs.TryGetValue("Name", out var name) || string.IsNullOrWhiteSpace(name))
                {
                    continue;
                }

                var uiNameKey = ExtractUiNameKeyFromBlock(content, match.Index, name);
                var nextWinch = content.IndexOf("<Winch", match.Index + 1, StringComparison.OrdinalIgnoreCase);
                var blockEnd = nextWinch >= 0 ? nextWinch : content.Length;
                var block = content[match.Index..blockEnd];
                winches.Add(CreateWinchDefinition(
                    entryPath,
                    name,
                    uiNameKey,
                    strings,
                    attrs.GetValueOrDefault("Length"),
                    attrs.GetValueOrDefault("StrengthMult"),
                    attrs.GetValueOrDefault("IsEngineIgnitionRequired"),
                    PartXmlHelpers.ExtractPrice(block).ToString(CultureInfo.InvariantCulture)));
            }

            return winches;
        }
    }

    private static WinchDefinition CreateWinchDefinition(
        string entryPath,
        string name,
        string? uiNameKey,
        IReadOnlyDictionary<string, string>? strings,
        string? lengthValue,
        string? strengthValue,
        string? engineRequiredValue,
        string? priceValue)
    {
        var key = uiNameKey?.Trim() ?? "";
        var displayName = strings is null
            ? name
            : GameStringsReader.Resolve(strings, key, name);

        return new WinchDefinition
        {
            EntryPath = entryPath,
            Name = name,
            UiNameKey = key,
            DisplayName = displayName,
            SourceFile = Path.GetFileName(entryPath.Replace('\\', '/')),
            Category = InferCategory(entryPath, name),
            Price = int.TryParse(priceValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price)
                ? price
                : 0,
            Length = ParseDouble(lengthValue, 35),
            StrengthMult = ParseDouble(strengthValue, 10),
            IsEngineIgnitionRequired = ParseBool(engineRequiredValue),
        };
    }

    private static string? ExtractUiNameKey(XElement winchElement) =>
        winchElement.Descendants()
            .FirstOrDefault(node => node.Name.LocalName.Equals("UiDesc", StringComparison.OrdinalIgnoreCase))
            ?.Attribute("UiName")
            ?.Value
            ?.Trim();

    private static string? ExtractUiNameKeyFromBlock(string content, int winchStartIndex, string winchName)
    {
        var searchStart = winchStartIndex;
        var nextWinch = content.IndexOf("<Winch", searchStart + 1, StringComparison.OrdinalIgnoreCase);
        var blockEnd = nextWinch >= 0 ? nextWinch : content.Length;
        var block = content[searchStart..blockEnd];
        var match = Regex.Match(
            block,
            @"UiName\s*=\s*""(?<value>[^""]+)""",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private readonly record struct WinchAttributeValues(
        double Length,
        double StrengthMult,
        bool IsEngineIgnitionRequired);

    private static string ApplyMultipliersToText(
        string backupText,
        double lengthMultiplier,
        double strengthMultiplier,
        bool forceAutonomousAll,
        out int changedCount)
    {
        changedCount = 0;

        if (!backupText.Contains("<Winch", StringComparison.OrdinalIgnoreCase))
        {
            return backupText;
        }

        var lengthIsBaseline = TuningMultiplierPresets.IsBaselineMultiplier(lengthMultiplier);
        var strengthIsBaseline = TuningMultiplierPresets.IsBaselineMultiplier(strengthMultiplier);
        if (lengthIsBaseline && strengthIsBaseline && !forceAutonomousAll)
        {
            return backupText;
        }

        try
        {
            var document = XDocument.Parse(backupText, LoadOptions.PreserveWhitespace);
            var anyChanges = false;

            foreach (var element in document.Descendants().Where(node =>
                node.Name.LocalName.Equals("Winch", StringComparison.OrdinalIgnoreCase)))
            {
                if (string.IsNullOrWhiteSpace(element.Attribute("Name")?.Value))
                {
                    continue;
                }

                var lengthAttr = element.Attribute("Length");
                var strengthAttr = element.Attribute("StrengthMult");
                var engineAttr = element.Attribute("IsEngineIgnitionRequired");

                var targetLength = ScaleWinchAttributeValue(
                    lengthAttr?.Value,
                    lengthMultiplier,
                    isStrengthMult: false);
                var targetStrength = ScaleWinchAttributeValue(
                    strengthAttr?.Value,
                    strengthMultiplier,
                    isStrengthMult: true);
                var targetEngine = forceAutonomousAll
                    ? "false"
                    : engineAttr?.Value ?? "true";

                var changed = false;
                if (!lengthIsBaseline)
                {
                    changed |= SetWinchAttribute(element, "Length", targetLength);
                }

                if (!strengthIsBaseline)
                {
                    changed |= SetWinchAttribute(element, "StrengthMult", targetStrength);
                }

                if (forceAutonomousAll)
                {
                    changed |= SetWinchAttribute(element, "IsEngineIgnitionRequired", targetEngine);
                }

                if (changed)
                {
                    changedCount++;
                    anyChanges = true;
                }
            }

            if (!anyChanges)
            {
                return backupText;
            }

            return document.ToString(SaveOptions.DisableFormatting);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Winch XML is invalid or corrupted in the pak. " +
                "Use \"Restore entire pak...\" from the baseline panel, then apply multipliers again.",
                ex);
        }
    }

    private static string ScaleWinchAttributeValue(string? rawValue, double multiplier, bool isStrengthMult)
    {
        if (TuningMultiplierPresets.IsBaselineMultiplier(multiplier))
        {
            return rawValue ?? (isStrengthMult ? "1.0" : "14");
        }

        var parsed = ParseDouble(rawValue, isStrengthMult ? 10 : 35);
        var scaled = Math.Round(parsed * multiplier, 2, MidpointRounding.AwayFromZero);
        return FormatNumeric(scaled, isStrengthMult);
    }

    private static bool TryApplyWinchUpdatesToText(
        string content,
        IReadOnlyDictionary<string, WinchAttributeValues> updates,
        out string updatedText,
        out int changedWinches)
    {
        updatedText = content;
        changedWinches = 0;

        if (updates.Count == 0 || !content.Contains("<Winch", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        try
        {
            var document = XDocument.Parse(content, LoadOptions.PreserveWhitespace);
            var anyChanges = false;

            foreach (var element in document.Descendants().Where(node =>
                node.Name.LocalName.Equals("Winch", StringComparison.OrdinalIgnoreCase)))
            {
                var name = element.Attribute("Name")?.Value?.Trim();
                if (string.IsNullOrWhiteSpace(name) || !updates.TryGetValue(name, out var target))
                {
                    continue;
                }

                var changed = false;
                changed |= SetWinchAttribute(
                    element,
                    "Length",
                    FormatNumeric(target.Length, isStrengthMult: false));
                changed |= SetWinchAttribute(
                    element,
                    "StrengthMult",
                    FormatNumeric(target.StrengthMult, isStrengthMult: true));
                changed |= SetWinchAttribute(
                    element,
                    "IsEngineIgnitionRequired",
                    target.IsEngineIgnitionRequired ? "true" : "false");

                if (changed)
                {
                    changedWinches++;
                    anyChanges = true;
                }
            }

            if (!anyChanges)
            {
                return false;
            }

            updatedText = document.ToString(SaveOptions.DisableFormatting);
            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                "Winch XML is invalid or corrupted in the pak. " +
                "Use \"Restore entire pak...\" from the baseline panel, then apply multipliers again.",
                ex);
        }
    }

    private static int CountWinchAttributeDifferences(string currentText, string updatedText)
    {
        var currentWinches = ParseWinchesFromText("current", currentText).ToDictionary(
            winch => winch.Name,
            winch => winch,
            StringComparer.OrdinalIgnoreCase);

        var changed = 0;
        foreach (var targetWinch in ParseWinchesFromText("target", updatedText))
        {
            if (!currentWinches.TryGetValue(targetWinch.Name, out var currentWinch))
            {
                changed++;
                continue;
            }

            if (Math.Abs(currentWinch.Length - targetWinch.Length) > 1e-9
                || Math.Abs(currentWinch.StrengthMult - targetWinch.StrengthMult) > 1e-9
                || currentWinch.IsEngineIgnitionRequired != targetWinch.IsEngineIgnitionRequired)
            {
                changed++;
            }
        }

        return changed;
    }

    private static bool SetWinchAttribute(XElement element, string attributeName, string value)
    {
        var attribute = element.Attribute(attributeName);
        if (attribute is null)
        {
            element.SetAttributeValue(attributeName, value);
            return true;
        }

        if (string.Equals(attribute.Value, value, StringComparison.Ordinal))
        {
            return false;
        }

        attribute.Value = value;
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

    private static string ReadEntryText(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static string InferCategory(string entryPath, string winchName)
    {
        if (entryPath.Contains("/_dlc/", StringComparison.OrdinalIgnoreCase))
        {
            return "DLC";
        }

        if (winchName.Contains("scout", StringComparison.OrdinalIgnoreCase))
        {
            return "Scout";
        }

        if (winchName.Contains("heavy", StringComparison.OrdinalIgnoreCase))
        {
            return "Heavy";
        }

        if (winchName.Contains("medium", StringComparison.OrdinalIgnoreCase))
        {
            return "Medium";
        }

        return "Default";
    }

    private static double ParseDouble(string? value, double fallback)
    {
        return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }

    private static bool ParseBool(string? value)
    {
        return string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatNumeric(double value, bool isStrengthMult)
    {
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
        {
            var integer = ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture);
            return isStrengthMult ? $"{integer}.0" : integer;
        }

        var text = value.ToString("0.######", CultureInfo.InvariantCulture);
        if (isStrengthMult && !text.Contains('.', StringComparison.Ordinal))
        {
            text += ".0";
        }

        return text;
    }

    private static void ValidateMultiplier(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Multiplier must be a positive number.");
        }
    }
}

public sealed record WinchSaveResult(int UpdatedFiles, int ChangedWinches);
