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

    /// <summary>
    /// Tag name at the start of an element or close tag.
    /// </summary>
    private static readonly Regex TagNameRegex = new(
        @"^[A-Za-z_][A-Za-z0-9_]*",
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

        if (TryFindOrphanCloseAfterSelfClose(xml, out var orphanIndex))
        {
            failure = $"Orphan close tag after self-closing element near: {Snippet(xml, orphanIndex)}";
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

    /// <summary>
    /// Flags <c>&lt;Foo /&gt;&lt;/Foo&gt;</c> only when no unmatched open <c>&lt;Foo&gt;</c> exists
    /// (true orphan). Allows valid nesting like <c>&lt;Body&gt;…&lt;Body /&gt;&lt;/Body&gt;</c>
    /// where the close belongs to the parent.
    /// </summary>
    private static bool TryFindOrphanCloseAfterSelfClose(string xml, out int index)
    {
        var openStack = new Stack<string>(capacity: 64);
        var i = 0;
        while (i < xml.Length)
        {
            var lt = xml.IndexOf('<', i);
            if (lt < 0)
            {
                break;
            }

            // Skip comments / CDATA / declarations / processing instructions.
            // Comments often contain raw ">" inside (vanilla truck XML); do not stop at the first ">".
            if (lt + 1 < xml.Length && (xml[lt + 1] == '!' || xml[lt + 1] == '?'))
            {
                i = SkipMarkupNoise(xml, lt);
                continue;
            }

            var gt = xml.IndexOf('>', lt + 1);
            if (gt < 0)
            {
                break;
            }

            var isClose = lt + 1 < xml.Length && xml[lt + 1] == '/';
            var nameStart = isClose ? lt + 2 : lt + 1;
            if (nameStart >= gt)
            {
                i = gt + 1;
                continue;
            }

            var nameMatch = TagNameRegex.Match(xml, nameStart, gt - nameStart);
            if (!nameMatch.Success)
            {
                i = gt + 1;
                continue;
            }

            var name = nameMatch.Value;
            var selfClosing = !isClose && EndsWithSelfClose(xml, lt, gt);

            if (isClose)
            {
                if (openStack.Count > 0 &&
                    openStack.Peek().Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    // Closes the currently open element (may sit right after a
                    // same-named self-closing child — that is valid nesting).
                    openStack.Pop();
                }
                else if (IsImmediateSelfCloseBefore(xml, lt, name))
                {
                    // <Foo … /> followed by </Foo> with no matching open Foo.
                    index = FindPrecedingSelfCloseStart(xml, lt);
                    return true;
                }
            }
            else if (!selfClosing)
            {
                openStack.Push(name);
            }

            i = gt + 1;
        }

        index = -1;
        return false;
    }

    private static bool EndsWithSelfClose(string xml, int lt, int gt)
    {
        if (lt < 0)
        {
            return false;
        }

        for (var j = gt - 1; j > lt; j--)
        {
            var c = xml[j];
            if (char.IsWhiteSpace(c))
            {
                continue;
            }

            return c == '/';
        }

        return false;
    }

    private static bool IsImmediateSelfCloseBefore(string xml, int closeLt, string name)
    {
        var j = closeLt - 1;
        while (j >= 0 && char.IsWhiteSpace(xml[j]))
        {
            j--;
        }

        if (j < 0 || xml[j] != '>')
        {
            return false;
        }

        var prevGt = j;
        var prevLt = FindTagStart(xml, prevGt);
        if (!EndsWithSelfClose(xml, prevLt, prevGt) || prevLt + 1 >= prevGt)
        {
            return false;
        }

        var nameMatch = TagNameRegex.Match(xml, prevLt + 1, prevGt - prevLt - 1);
        return nameMatch.Success &&
               nameMatch.Value.Equals(name, StringComparison.OrdinalIgnoreCase);
    }

    private static int FindPrecedingSelfCloseStart(string xml, int closeLt)
    {
        var j = closeLt - 1;
        while (j >= 0 && char.IsWhiteSpace(xml[j]))
        {
            j--;
        }

        if (j < 0 || xml[j] != '>')
        {
            return closeLt;
        }

        var start = FindTagStart(xml, j);
        return start >= 0 ? start : closeLt;
    }

    private static int FindTagStart(string xml, int gt)
    {
        for (var j = gt; j >= 0; j--)
        {
            if (xml[j] == '<')
            {
                return j;
            }
        }

        return -1;
    }

    /// <summary>
    /// Advances past <c>&lt;!-- … --&gt;</c>, <c>&lt;![CDATA[ … ]]&gt;</c>, <c>&lt;!DOCTYPE …&gt;</c>,
    /// or <c>&lt;? … ?&gt;</c>. Falls back to the next <c>&gt;</c> only when no known terminator exists.
    /// </summary>
    private static int SkipMarkupNoise(string xml, int lt)
    {
        if (lt + 3 < xml.Length
            && xml[lt + 1] == '!'
            && xml[lt + 2] == '-'
            && xml[lt + 3] == '-')
        {
            var end = xml.IndexOf("-->", lt + 4, StringComparison.Ordinal);
            return end < 0 ? xml.Length : end + 3;
        }

        if (lt + 8 < xml.Length
            && xml.AsSpan(lt, 9).Equals("<![CDATA[", StringComparison.Ordinal))
        {
            var end = xml.IndexOf("]]>", lt + 9, StringComparison.Ordinal);
            return end < 0 ? xml.Length : end + 3;
        }

        if (lt + 1 < xml.Length && xml[lt + 1] == '?')
        {
            var end = xml.IndexOf("?>", lt + 2, StringComparison.Ordinal);
            return end < 0 ? xml.Length : end + 2;
        }

        var gt = xml.IndexOf('>', lt + 2);
        return gt < 0 ? xml.Length : gt + 1;
    }

    private static string Snippet(string xml, int index)
    {
        var start = Math.Max(0, index - 24);
        var length = Math.Min(80, xml.Length - start);
        return xml.AsSpan(start, length).ToString().Replace('\r', ' ').Replace('\n', ' ');
    }
}
