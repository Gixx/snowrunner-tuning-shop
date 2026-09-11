using System.Globalization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Shared &lt;GameData&gt; / &lt;TruckData&gt; attribute edits for truck and trailer XML.
/// </summary>
public static class VehicleGameDataXml
{
    private static readonly Regex TruckDataOpenRegex = new(
        @"<TruckData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^<>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string SetGameDataAttribute(string text, string attributeName, string value) =>
        SetOpenTagAttribute(text, GameDataOpenRegex, "GameData", attributeName, value);

    public static string RemoveGameDataAttribute(string text, string attributeName)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var attrs = match.Groups["attrs"].Value;
        var pattern = $@"\s*\b{Regex.Escape(attributeName)}\s*=\s*""[^""]*""";
        var updatedAttrs = Regex.Replace(attrs, pattern, "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
        if (string.Equals(attrs, updatedAttrs, StringComparison.Ordinal))
        {
            return text;
        }

        var replacement = $"<GameData{updatedAttrs}>";
        return string.Concat(text.AsSpan(0, match.Index), replacement, text.AsSpan(match.Index + match.Length));
    }

    public static string ExtractGameDataAttribute(string text, string attributeName)
    {
        var match = GameDataOpenRegex.Match(text);
        if (!match.Success)
        {
            return "";
        }

        var attrs = ParseAttributes(match.Groups["attrs"].Value);
        return attrs.TryGetValue(attributeName, out var raw) ? raw : "";
    }

    public static int ExtractGameDataInt(string text, string attributeName, int fallback) =>
        int.TryParse(
            ExtractGameDataAttribute(text, attributeName),
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var parsed)
            ? parsed
            : fallback;

    public static string SetTruckDataAttribute(string text, string attributeName, string value) =>
        SetOpenTagAttribute(text, TruckDataOpenRegex, "TruckData", attributeName, value);

    public static string ApplyExistingTruckDataInt(string text, string attributeName, int value)
    {
        if (!TryGetTruckDataAttribute(text, attributeName, out _))
        {
            return text;
        }

        return SetTruckDataAttribute(text, attributeName, value.ToString(CultureInfo.InvariantCulture));
    }

    public static bool TryGetTruckDataAttribute(string text, string attributeName, out string value)
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

    public static bool SetOrReplaceAttribute(ref string attrs, string attributeName, string value)
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

    public static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }

    private static string SetOpenTagAttribute(
        string text,
        Regex openTagRegex,
        string tagName,
        string attributeName,
        string value)
    {
        var match = openTagRegex.Match(text);
        if (!match.Success)
        {
            return text;
        }

        var attrs = match.Groups["attrs"].Value;
        if (!SetOrReplaceAttribute(ref attrs, attributeName, value))
        {
            return text;
        }

        var replacement = $"<{tagName}{attrs}>";
        return string.Concat(text.AsSpan(0, match.Index), replacement, text.AsSpan(match.Index + match.Length));
    }
}
