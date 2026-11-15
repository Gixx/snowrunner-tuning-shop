using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Cargo unit model helpers (<c>classes/models/cargo_unit_*.xml</c>).
/// Winchable cargo duplicates each <c>CraneSocket</c> as a <c>WinchSocket</c>.
/// Cranable logs add conventional-crane sockets to dynamic log units that lack them.
/// </summary>
public static class CargoUnitSocketXml
{
    private static readonly Regex CraneSocketRegex = new(
        @"<CraneSocket\b(?<attrs>[^>]*)/>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex WinchSocketRegex = new(
        @"<WinchSocket\b(?<attrs>[^>]*)/>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex PosAttrRegex = new(
        @"\bPos\s*=\s*""(?<pos>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex GameDataBlockRegex = new(
        @"<GameData\b(?<attrs>[^>]*)>(?<inner>.*?)</GameData>",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex LoadTypeAttrRegex = new(
        @"\bLoadType\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DynamicLogFileNameRegex = new(
        @"^cargo_unit_(?:burnt_)?log_(?:short|medium|long)\.xml$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Community-mod crane points for short / medium / long (and burnt medium) logs.</summary>
    private static readonly IReadOnlyDictionary<string, string[]> CraneSocketsByLoadType =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["CargoLogsShort"] =
            [
                "(0;0.171;0)",
                "(1.365;0.171;0)",
                "(-1.365;0.171;0)",
            ],
            ["CargoLogsMedium"] =
            [
                "(0;0.171;0)",
                "(2.4;0.171;0)",
                "(-2.4;0.171;0)",
            ],
            ["CargoLogsMedium_Burnt"] =
            [
                "(0;0.171;0)",
                "(2.4;0.171;0)",
                "(-2.4;0.171;0)",
            ],
            ["CargoLogsLong"] =
            [
                "(0;0.171;0)",
                "(4.0;0.171;0)",
                "(-4.0;0.171;0)",
            ],
        };

    public static bool IsCargoUnitEntry(string entryPath)
    {
        var normalized = entryPath.Replace('\\', '/');
        if (!normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains("/classes/models/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalized);
        return fileName.StartsWith("cargo_unit_", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsDynamicLogCargoUnit(string entryPath)
    {
        if (!IsCargoUnitEntry(entryPath))
        {
            return false;
        }

        return DynamicLogFileNameRegex.IsMatch(Path.GetFileName(entryPath.Replace('\\', '/')));
    }

    public static bool HasCraneSocket(string xml) => CraneSocketRegex.IsMatch(xml);

    public static bool HasWinchSocket(string xml) => WinchSocketRegex.IsMatch(xml);

    public static int CountCraneSockets(string xml) => CraneSocketRegex.Matches(xml).Count;

    public static int CountWinchSockets(string xml) => WinchSocketRegex.Matches(xml).Count;

    /// <summary>
    /// True when every <c>CraneSocket</c> Pos also has a matching <c>WinchSocket</c>.
    /// Files with no crane sockets are not considered winchable targets (returns true).
    /// </summary>
    public static bool HasMatchingWinchSocketsForCranes(string xml)
    {
        var cranePositions = ExtractPositions(CraneSocketRegex, xml);
        if (cranePositions.Count == 0)
        {
            return true;
        }

        var winchPositions = ExtractPositions(WinchSocketRegex, xml);
        return cranePositions.All(winchPositions.Contains);
    }

    public static string EnsureLogCraneSockets(string xml)
    {
        if (HasCraneSocket(xml))
        {
            return xml;
        }

        var loadType = TryReadLoadType(xml);
        if (loadType is null
            || !CraneSocketsByLoadType.TryGetValue(loadType, out var positions)
            || positions.Length == 0)
        {
            return xml;
        }

        var gameData = GameDataBlockRegex.Match(xml);
        if (!gameData.Success)
        {
            return xml;
        }

        var sb = new StringBuilder();
        sb.Append(gameData.Groups["inner"].Value.TrimEnd());
        foreach (var pos in positions)
        {
            sb.Append("\r\n\t\t<CraneSocket Pos=\"");
            sb.Append(pos);
            sb.Append("\" />");
        }

        sb.Append("\r\n\t");

        return xml[..gameData.Index]
            + "<GameData"
            + gameData.Groups["attrs"].Value
            + ">"
            + sb
            + "</GameData>"
            + xml[(gameData.Index + gameData.Length)..];
    }

    public static string EnsureWinchSocketsFromCrane(string xml)
    {
        if (!HasCraneSocket(xml))
        {
            return xml;
        }

        var winchPositions = ExtractPositions(WinchSocketRegex, xml);
        var result = xml;
        // Walk original crane matches left-to-right; re-find after each insert by rebuilding from current text.
        var offset = 0;
        foreach (Match crane in CraneSocketRegex.Matches(xml))
        {
            var pos = TryReadPos(crane.Groups["attrs"].Value);
            if (pos is null)
            {
                continue;
            }

            var normalized = NormalizePos(pos);
            if (winchPositions.Contains(normalized))
            {
                continue;
            }

            var insertAt = crane.Index + crane.Length + offset;
            var winchTag = "\r\n\t\t<WinchSocket" + crane.Groups["attrs"].Value + "/>";
            result = result[..insertAt] + winchTag + result[insertAt..];
            offset += winchTag.Length;
            winchPositions.Add(normalized);
        }

        return result;
    }

    /// <summary>
    /// Builds the desired cargo-unit XML from baseline: optional log crane sockets, then optional winch duplicates.
    /// </summary>
    public static string BuildDesired(
        string baselineXml,
        string entryPath,
        bool cranableLogs,
        bool winchableCargo)
    {
        var updated = baselineXml;
        if (cranableLogs && IsDynamicLogCargoUnit(entryPath))
        {
            updated = EnsureLogCraneSockets(updated);
        }

        if (winchableCargo)
        {
            updated = EnsureWinchSocketsFromCrane(updated);
        }

        return updated;
    }

    private static string? TryReadLoadType(string xml)
    {
        var gameData = GameDataBlockRegex.Match(xml);
        if (!gameData.Success)
        {
            return null;
        }

        var match = LoadTypeAttrRegex.Match(gameData.Groups["attrs"].Value);
        return match.Success ? match.Groups["value"].Value.Trim() : null;
    }

    private static HashSet<string> ExtractPositions(Regex socketRegex, string xml)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match match in socketRegex.Matches(xml))
        {
            var pos = TryReadPos(match.Groups["attrs"].Value);
            if (pos is not null)
            {
                set.Add(NormalizePos(pos));
            }
        }

        return set;
    }

    private static string? TryReadPos(string attrs)
    {
        var match = PosAttrRegex.Match(attrs);
        return match.Success ? match.Groups["pos"].Value : null;
    }

    private static string NormalizePos(string pos) =>
        string.Join(
            ";",
            pos.Trim()
                .Trim('(', ')')
                .Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(part =>
                {
                    if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    {
                        return value.ToString("0.###", CultureInfo.InvariantCulture);
                    }

                    return part;
                }));
}
