using System.IO.Compression;

namespace SnowRunnerTuningShop.Core.Pak;

public static class PakEntryLocator
{
    public static ZipArchiveEntry? FindEntry(ZipArchive archive, string entryPath)
    {
        ArgumentNullException.ThrowIfNull(archive);

        var normalized = NormalizeEntryPath(entryPath);
        if (normalized.Length == 0)
        {
            return null;
        }

        var direct = archive.GetEntry(normalized);
        if (direct is not null)
        {
            return direct;
        }

        var alternateSeparator = normalized.Contains('\\', StringComparison.Ordinal)
            ? normalized.Replace('\\', '/')
            : normalized.Replace('/', '\\');

        direct = archive.GetEntry(alternateSeparator);
        if (direct is not null)
        {
            return direct;
        }

        foreach (var entry in archive.Entries)
        {
            if (EntryPathsEqual(entry.FullName, normalized))
            {
                return entry;
            }
        }

        return null;
    }

    public static string NormalizeEntryPath(string entryPath) =>
        entryPath.Replace('\\', '/').Trim('/');

    public static bool EntryPathsEqual(string left, string right) =>
        string.Equals(
            NormalizeEntryPath(left),
            NormalizeEntryPath(right),
            StringComparison.OrdinalIgnoreCase);
}
