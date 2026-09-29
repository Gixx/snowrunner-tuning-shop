using System.Linq;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Plant <c>WinchSocket</c> helpers. Weak/breakable plants (SmallTree, bushes, …) expose winch
/// points that snap instantly under load and clutter quick-winch; big trees and lying trunks keep theirs.
/// </summary>
public static class PlantWinchSocketXml
{
    private static readonly HashSet<string> WeakPlantTemplates = new(StringComparer.OrdinalIgnoreCase)
    {
        "SmallTree",
        "SmallTreeRoot",
        "Bush",
        "BushRoot",
        "SwampBranch",
        "WoodChunk",
    };

    private static readonly Regex PlantBrandTemplateRegex = new(
        @"<PlantBrand\b(?<attrs>[^>]*)>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex TemplateAttrRegex = new(
        @"\b_template\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WinchSocketTagRegex = new(
        @"<WinchSocket\b[^>]*/>\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex EmptyGameDataRegex = new(
        @"<GameData\b[^>]*>\s*</GameData>\s*",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GameDataBlockRegex = new(
        @"<GameData\b(?<attrs>[^>]*)>(?<inner>.*?)</GameData>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex PlantBrandCloseRegex = new(
        @"</PlantBrand\s*>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsWeakPlant(string plantXml)
    {
        var brand = PlantBrandTemplateRegex.Match(plantXml);
        if (!brand.Success)
        {
            return false;
        }

        var template = TemplateAttrRegex.Match(brand.Groups["attrs"].Value);
        return template.Success && WeakPlantTemplates.Contains(template.Groups["value"].Value.Trim());
    }

    public static bool HasWinchSocket(string plantXml) => WinchSocketTagRegex.IsMatch(plantXml);

    public static string StripWinchSockets(string plantXml)
    {
        if (!HasWinchSocket(plantXml))
        {
            return plantXml;
        }

        var updated = WinchSocketTagRegex.Replace(plantXml, "");
        updated = EmptyGameDataRegex.Replace(updated, "");
        return updated;
    }

    /// <summary>
    /// Restores <c>WinchSocket</c> entries from <paramref name="baselineXml"/> into
    /// <paramref name="workingXml"/> (weak plants only; surgical — does not replace the whole file).
    /// </summary>
    public static string RestoreWinchSockets(string workingXml, string baselineXml)
    {
        if (!IsWeakPlant(baselineXml) || !HasWinchSocket(baselineXml))
        {
            return workingXml;
        }

        var baselineGameData = GameDataBlockRegex.Match(baselineXml);
        if (!baselineGameData.Success)
        {
            return workingXml;
        }

        var winchOnlyInner = string.Concat(
            WinchSocketTagRegex.Matches(baselineGameData.Groups["inner"].Value)
                .Select(m => m.Value.TrimEnd() + "\r\n\t\t"));

        if (string.IsNullOrWhiteSpace(winchOnlyInner))
        {
            return workingXml;
        }

        var workingGameData = GameDataBlockRegex.Match(workingXml);
        if (workingGameData.Success)
        {
            var inner = workingGameData.Groups["inner"].Value;
            if (WinchSocketTagRegex.IsMatch(inner))
            {
                // Already has sockets — replace winch tags with baseline ones, keep other GameData kids.
                var withoutWinch = WinchSocketTagRegex.Replace(inner, "");
                var newInner = withoutWinch.TrimEnd() + "\r\n\t\t" + winchOnlyInner.TrimEnd() + "\r\n\t";
                return workingXml[..workingGameData.Index]
                    + "<GameData"
                    + workingGameData.Groups["attrs"].Value
                    + ">"
                    + newInner
                    + "</GameData>"
                    + workingXml[(workingGameData.Index + workingGameData.Length)..];
            }

            var insertedInner = inner.TrimEnd() + "\r\n\t\t" + winchOnlyInner.TrimEnd() + "\r\n\t";
            return workingXml[..workingGameData.Index]
                + "<GameData"
                + workingGameData.Groups["attrs"].Value
                + ">"
                + insertedInner
                + "</GameData>"
                + workingXml[(workingGameData.Index + workingGameData.Length)..];
        }

        var close = PlantBrandCloseRegex.Match(workingXml);
        if (!close.Success)
        {
            return workingXml;
        }

        var block = "\t<GameData>\r\n\t\t" + winchOnlyInner.TrimEnd() + "\r\n\t</GameData>\r\n";
        return workingXml[..close.Index] + block + workingXml[close.Index..];
    }
}
