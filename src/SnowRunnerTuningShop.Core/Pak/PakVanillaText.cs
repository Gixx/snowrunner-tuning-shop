using System.IO.Compression;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Vanilla XML for global multiplier applies. After a game update the working pak
/// can contain new DLC files that are not in the last baseline snapshot; those must
/// still be scaled instead of skipped.
/// </summary>
public static class PakVanillaText
{
    public static string Read(
        ZipArchive baselineArchive,
        ZipArchiveEntry currentEntry,
        Func<ZipArchiveEntry, string> readText)
    {
        ArgumentNullException.ThrowIfNull(baselineArchive);
        ArgumentNullException.ThrowIfNull(currentEntry);
        ArgumentNullException.ThrowIfNull(readText);

        var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, currentEntry.FullName);
        return readText(baselineEntry ?? currentEntry);
    }
}
