using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Detects the community "Real Life" pak mod, which injects the phrase
/// "Real Life Mod" into game string tables (and heavily rewrites truck XML).
/// </summary>
public static class RealLifeModDetector
{
    public const string MarkerPhrase = "Real Life Mod";

    private static readonly string[] StringEntryPaths =
    [
        "[strings]/strings_english.str",
        "[strings]/strings_russian.str",
    ];

    private static readonly Regex VersionRegex = new(
        @"Real Life Mod\s+v(?<version>\d+(?:\.\d+)*)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static bool IsDetected(string? pakPath) =>
        TryDetect(pakPath, out _);

    public static bool TryDetect(string? pakPath, out string? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(pakPath) || !File.Exists(pakPath))
        {
            return false;
        }

        try
        {
            using var archive = ZipFile.OpenRead(pakPath);
            foreach (var entryPath in StringEntryPaths)
            {
                var entry = PakEntryLocator.FindEntry(archive, entryPath);
                if (entry is null)
                {
                    continue;
                }

                using var reader = new StreamReader(
                    entry.Open(),
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true);
                var text = reader.ReadToEnd();
                if (text.IndexOf(MarkerPhrase, StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                var match = VersionRegex.Match(text);
                if (match.Success)
                {
                    version = match.Groups["version"].Value;
                }

                return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }
}
