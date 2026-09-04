using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Trailers;

public static class TrailerHitchXml
{
    private static readonly Regex GameDataOpenRegex = new(
        @"<GameData\b(?<attrs>[^>]*)>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex AttributeRegex = new(
        @"(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<value>[^""]*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex ParentFileRegex = new(
        @"<_parent\b[^>]*\bFile\s*=\s*""(?<file>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex InstallSocketRegex = new(
        @"<InstallSocket\b(?<attrs>[^>]*)/?>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> StoreHitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Trailer",
        "ScautTrailer",
        "Semitrailer",
        "LargeSemitrailer",
        "LogTrailer",
        "TrailerFarm",
        "TrailerPlanter",
        "SemitrailerOiltank",
        "LargeSemitrailerOiltank",
        "SemitrailerFoldableLog",
        "SemitrailerCat770g",
    };

    private static readonly HashSet<string> NonStoreHitchTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Train",
        "TrailerTrainRocket",
        "CargoCabin",
    };

    private const string DefaultSaddleHighOffset = "(8.719; 1.895; 0)";

    public static string EnsureStoreHitch(string text)
    {
        var sockets = InstallSocketRegex.Matches(text);
        foreach (Match socket in sockets)
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type) && StoreHitchTypes.Contains(type.Trim()))
            {
                return text;
            }
        }

        if (sockets.Count > 0)
        {
            var first = sockets[0];
            var attrs = ParseAttributes(first.Groups["attrs"].Value);
            attrs.TryGetValue("Type", out var type);
            type = type?.Trim() ?? "";

            if (string.IsNullOrEmpty(type))
            {
                return SetInstallSocketType(text, first, "LargeSemitrailer");
            }

            if (!NonStoreHitchTypes.Contains(type) || HasStoreHitchSocket(text))
            {
                return text;
            }

            if (!attrs.TryGetValue("Offset", out var offset) || string.IsNullOrWhiteSpace(offset))
            {
                offset = "(0; 0; 0)";
            }

            return InsertAfter(text, first, $"{Environment.NewLine}\t\t<InstallSocket Offset=\"{offset}\" Type=\"Trailer\" />");
        }

        if (!GameDataOpenRegex.IsMatch(text) || !ParentFileRegex.IsMatch(text))
        {
            return text;
        }

        return InsertBeforeGameDataClose(
            text,
            $"<InstallSocket Offset=\"{DefaultSaddleHighOffset}\" Type=\"LargeSemitrailer\" />");
    }

    private static bool HasStoreHitchSocket(string text)
    {
        foreach (Match socket in InstallSocketRegex.Matches(text))
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type) && StoreHitchTypes.Contains(type.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when the trailer can appear in the regular store hitch-wise.
    /// Train / rocket-platform sockets alone are not enough until EnsureStoreHitch adds a Trailer socket.
    /// </summary>
    public static bool IsStoreHitchReady(string text)
    {
        if (HasStoreHitchSocket(text))
        {
            return true;
        }

        var sockets = InstallSocketRegex.Matches(text);
        if (sockets.Count == 0)
        {
            // Parent-only XMLs need EnsureStoreHitch before the store can list them.
            return !ParentFileRegex.IsMatch(text);
        }

        foreach (Match socket in sockets)
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Type", out var type) || string.IsNullOrWhiteSpace(type))
            {
                continue;
            }

            type = type.Trim();
            if (StoreHitchTypes.Contains(type))
            {
                return true;
            }

            if (NonStoreHitchTypes.Contains(type))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasNonStoreHitchSocket(string text)
    {
        foreach (Match socket in InstallSocketRegex.Matches(text))
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type) && NonStoreHitchTypes.Contains(type.Trim()))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Removes Type=Trailer sockets we add beside Train/rocket hitches when undoing store availability.
    /// Never strips Trailer sockets that already existed in the baseline.
    /// </summary>
    public static string RemoveSupplementalStoreHitch(
        string text,
        string? baselineText = null,
        bool baselineHadStoreCompatibleHitch = false)
    {
        if (!HasNonStoreHitchSocket(text))
        {
            return text;
        }

        if (baselineText is not null)
        {
            if (HasStoreHitchSocket(baselineText))
            {
                return text;
            }

            var baselineTrailerKeys = CollectTrailerSocketKeys(baselineText);
            return RemoveTrailerSockets(text, attrs =>
            {
                var key = TrailerSocketKey(attrs);
                return !baselineTrailerKeys.Contains(key);
            });
        }

        if (baselineHadStoreCompatibleHitch)
        {
            return text;
        }

        return RemoveTrailerSockets(text, _ => true);
    }

    private static HashSet<string> CollectTrailerSocketKeys(string text)
    {
        var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match socket in InstallSocketRegex.Matches(text))
        {
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (attrs.TryGetValue("Type", out var type)
                && type.Trim().Equals("Trailer", StringComparison.OrdinalIgnoreCase))
            {
                keys.Add(TrailerSocketKey(attrs));
            }
        }

        return keys;
    }

    private static string TrailerSocketKey(IReadOnlyDictionary<string, string> attrs)
    {
        attrs.TryGetValue("Offset", out var offset);
        return string.IsNullOrWhiteSpace(offset) ? "(0; 0; 0)" : offset.Trim();
    }

    private static string RemoveTrailerSockets(
        string text,
        Func<Dictionary<string, string>, bool> shouldRemove)
    {
        var matches = InstallSocketRegex.Matches(text);
        for (var i = matches.Count - 1; i >= 0; i--)
        {
            var socket = matches[i];
            var attrs = ParseAttributes(socket.Groups["attrs"].Value);
            if (!attrs.TryGetValue("Type", out var type)
                || !type.Trim().Equals("Trailer", StringComparison.OrdinalIgnoreCase)
                || !shouldRemove(attrs))
            {
                continue;
            }

            var start = socket.Index;
            var length = socket.Length;
            while (start > 0 && (text[start - 1] == ' ' || text[start - 1] == '\t'))
            {
                start--;
                length++;
            }

            if (start > 0 && text[start - 1] == '\n')
            {
                start--;
                length++;
                if (start > 0 && text[start - 1] == '\r')
                {
                    start--;
                    length++;
                }
            }

            text = text.Remove(start, length);
        }

        return text;
    }

    private static string SetInstallSocketType(string text, Match socket, string type)
    {
        var attrs = socket.Groups["attrs"].Value.TrimEnd();
        if (attrs.EndsWith('/'))
        {
            attrs = attrs[..^1].TrimEnd();
        }

        if (!SetOrReplaceAttribute(ref attrs, "Type", type))
        {
            return text;
        }

        var replacement = $"<InstallSocket{attrs} />";
        return string.Concat(text.AsSpan(0, socket.Index), replacement, text.AsSpan(socket.Index + socket.Length));
    }

    private static string InsertAfter(string text, Match match, string insert)
    {
        var index = match.Index + match.Length;
        return string.Concat(text.AsSpan(0, index), insert, text.AsSpan(index));
    }

    private static string InsertBeforeGameDataClose(string text, string childXml)
    {
        var close = Regex.Match(text, @"</GameData>", RegexOptions.IgnoreCase);
        if (!close.Success)
        {
            return text;
        }

        return string.Concat(
            text[..close.Index],
            childXml,
            Environment.NewLine,
            "\t\t",
            text[close.Index..]);
    }

    private static bool SetOrReplaceAttribute(ref string attrs, string attributeName, string value)
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

    private static Dictionary<string, string> ParseAttributes(string attrs)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in AttributeRegex.Matches(attrs))
        {
            result[match.Groups["name"].Value] = match.Groups["value"].Value;
        }

        return result;
    }
}
