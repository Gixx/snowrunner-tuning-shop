using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Strings;

public static class GameStringsWriter
{
    private static readonly UnicodeEncoding Utf16Le = new(bigEndian: false, byteOrderMark: true);

    private static readonly Regex StringEntryRegex = new(
        @"(?<key>UI_[A-Za-z0-9_]+)\s+""(?<value>(?:\\.|[^""])*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, string> Parse(string text)
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in StringEntryRegex.Matches(text))
        {
            var key = match.Groups["key"].Value.Trim();
            if (key.Length == 0)
            {
                continue;
            }

            strings[key] = Unescape(match.Groups["value"].Value);
        }

        return strings;
    }

    public static string Decode(byte[] fileBytes)
    {
        var text = Encoding.Unicode.GetString(fileBytes);
        return text.StartsWith('\uFEFF') ? text[1..] : text;
    }

    public static bool TryUpsert(
        byte[] fileBytes,
        IReadOnlyDictionary<string, string> values,
        out byte[] updatedBytes)
    {
        updatedBytes = fileBytes;
        if (fileBytes.Length < 2 || values.Count == 0)
        {
            return false;
        }

        var text = Decode(fileBytes);
        var changed = false;
        foreach (var (key, value) in values)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            var escaped = Escape(value);
            var pattern = $@"(?<prefix>{Regex.Escape(key)}\s+"")(?<value>(?:\\.|[^""])*)(?<suffix>"")";
            var regex = new Regex(pattern, RegexOptions.CultureInvariant);
            var match = regex.Match(text);
            if (match.Success)
            {
                if (string.Equals(match.Groups["value"].Value, escaped, StringComparison.Ordinal))
                {
                    continue;
                }

                text = regex.Replace(text, $"{match.Groups["prefix"].Value}{escaped}{match.Groups["suffix"].Value}", 1);
                changed = true;
                continue;
            }

            var lineEnding = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            if (text.Length > 0 && !text.EndsWith('\n'))
            {
                text += lineEnding;
            }

            text += $"{key}\t\t\t\t\"{escaped}\"{lineEnding}";
            changed = true;
        }

        if (!changed)
        {
            return false;
        }

        var preamble = Utf16Le.GetPreamble();
        var body = Utf16Le.GetBytes(text);
        updatedBytes = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, updatedBytes, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, updatedBytes, preamble.Length, body.Length);
        return true;
    }

    private static string Escape(string value) =>
        value.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal);

    private static string Unescape(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
}
