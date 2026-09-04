using System.IO.Compression;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Maps entry paths case-insensitively to the archive's canonical FullName (forward slashes).
/// Write paths must use the canonical casing so raw zip local records match.
/// </summary>
internal static class PakEntryNameMap
{
    public static Dictionary<string, string> ReadCanonicalNames(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        return FromArchive(archive);
    }

    public static Dictionary<string, string> FromArchive(ZipArchive archive)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in archive.Entries)
        {
            var canonical = PakEntryLocator.NormalizeEntryPath(entry.FullName);
            if (canonical.Length == 0)
            {
                continue;
            }

            map.TryAdd(canonical, canonical);
        }

        return map;
    }

    public static Dictionary<string, T> ToOrdinalIgnoreCaseDictionary<T>(
        IEnumerable<KeyValuePair<string, T>> pairs)
    {
        var result = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in pairs)
        {
            result[PakEntryLocator.NormalizeEntryPath(key)] = value;
        }

        return result;
    }
}
