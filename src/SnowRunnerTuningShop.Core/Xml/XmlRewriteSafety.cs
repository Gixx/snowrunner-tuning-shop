using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

/// <summary>
/// Structural checks for XML text after tuning rewrites.
/// Catches game-breaking patterns (e.g. attribute after a self-closing slash)
/// without requiring strict XDocument validity of every vanilla fragment.
/// </summary>
public static class XmlRewriteSafety
{
    /// <summary>
    /// Misplaced self-close: <c>... / Attr="…"</c> (attribute after <c>/</c>).
    /// </summary>
    private static readonly Regex SlashBeforeAttributeRegex = new(
        @"/\s+[A-Za-z_][A-Za-z0-9_]*\s*=",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Open tag that still has content after a <c>/</c> before <c>&gt;</c>.
    /// </summary>
    private static readonly Regex SlashNotAtEndOfOpenTagRegex = new(
        @"<[A-Za-z_][^>]*?/\s+[^>]+>",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static bool TryValidate(string xml, out string? failure)
    {
        if (string.IsNullOrEmpty(xml))
        {
            failure = "XML text is empty.";
            return false;
        }

        var slashBeforeAttr = SlashBeforeAttributeRegex.Match(xml);
        if (slashBeforeAttr.Success)
        {
            failure = $"Misplaced self-close before attribute near: {Snippet(xml, slashBeforeAttr.Index)}";
            return false;
        }

        var slashMidTag = SlashNotAtEndOfOpenTagRegex.Match(xml);
        if (slashMidTag.Success)
        {
            failure = $"Self-close slash is not at end of open tag near: {Snippet(xml, slashMidTag.Index)}";
            return false;
        }

        failure = null;
        return true;
    }

    public static void EnsureSafe(string xml, string context)
    {
        if (!TryValidate(xml, out var failure))
        {
            throw new InvalidOperationException($"{context}: {failure}");
        }
    }

    private static string Snippet(string xml, int index)
    {
        var start = Math.Max(0, index - 24);
        var length = Math.Min(80, xml.Length - start);
        return xml.AsSpan(start, length).ToString().Replace('\r', ' ').Replace('\n', ' ');
    }
}
