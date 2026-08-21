using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.Strings;

public static class GameStringsReader
{
    private static readonly Regex StringEntryRegex = new(
        @"(?<key>UI_[A-Za-z0-9_]+)\s+""(?<value>(?:\\.|[^""])*)""",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static IReadOnlyDictionary<string, string> LoadFromPak(string pakPath, string language = "english")
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var entryPath = $"[strings]/strings_{language}.str";
        var entry = PakEntryLocator.FindEntry(archive, entryPath)
            ?? PakEntryLocator.FindEntry(archive, "[strings]/strings_english.str")
            ?? archive.Entries.FirstOrDefault(candidate =>
                PakEntryLocator.NormalizeEntryPath(candidate.FullName)
                    .EndsWith("/strings_english.str", StringComparison.OrdinalIgnoreCase))
            ?? throw new FileNotFoundException($"Language strings file was not found in pak: {entryPath}");

        using var stream = entry.Open();
        using var reader = new StreamReader(stream, Encoding.Unicode, detectEncodingFromByteOrderMarks: true);
        var text = reader.ReadToEnd();

        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (Match match in StringEntryRegex.Matches(text))
        {
            var key = match.Groups["key"].Value.Trim();
            if (key.Length == 0)
            {
                continue;
            }

            strings[key] = UnescapeString(match.Groups["value"].Value);
        }

        return strings;
    }

    public static string Resolve(IReadOnlyDictionary<string, string> strings, string? key, string fallback)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return fallback;
        }

        return strings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;
    }

    private static string UnescapeString(string value) =>
        value.Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal);
}
