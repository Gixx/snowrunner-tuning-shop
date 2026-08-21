using System.IO.Compression;
using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Models;

namespace SnowRunnerTuningShop.Core.Pak;

public static class InitialPakReader
{
    public static PakSummary ReadSummary(string pakPath)
    {
        if (string.IsNullOrWhiteSpace(pakPath))
        {
            throw new ArgumentException("Pak file path is required.", nameof(pakPath));
        }

        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("The specified initial.pak was not found.", pakPath);
        }

        using var archive = ZipFile.OpenRead(pakPath);
        var entries = archive.Entries;

        var topLevelFolders = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var dlcPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var categoryFiles = PakPaths.TuningCategories.ToDictionary(
            category => category,
            _ => new List<string>(),
            StringComparer.OrdinalIgnoreCase);

        var totalEntries = 0;
        var xmlEntries = 0;
        long uncompressedBytes = 0;

        foreach (var entry in entries)
        {
            totalEntries++;
            uncompressedBytes += entry.Length;

            var normalizedPath = entry.FullName.Replace('\\', '/');
            var topLevel = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(topLevel))
            {
                topLevelFolders[topLevel] = topLevelFolders.GetValueOrDefault(topLevel) + 1;
            }

            if (normalizedPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
            {
                xmlEntries++;
            }

            var dlcMarker = "/_dlc/";
            var dlcIndex = normalizedPath.IndexOf(dlcMarker, StringComparison.OrdinalIgnoreCase);
            if (dlcIndex >= 0)
            {
                var remainder = normalizedPath[(dlcIndex + dlcMarker.Length)..];
                var packageName = remainder.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(packageName))
                {
                    dlcPackages.Add(packageName);
                }
            }

            foreach (var category in PakPaths.TuningCategories)
            {
                var categoryPrefix = $"{PakPaths.ClassesPrefix}{category}/";
                if (normalizedPath.StartsWith(categoryPrefix, StringComparison.OrdinalIgnoreCase)
                    && normalizedPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
                {
                    categoryFiles[category].Add(normalizedPath);
                }
            }
        }

        var tuningCategories = PakPaths.TuningCategories
            .Select(category =>
            {
                var files = categoryFiles[category];
                files.Sort(StringComparer.OrdinalIgnoreCase);
                return new PakCategorySummary(
                    category,
                    PakTuningItemCounts.Count(pakPath, category),
                    files.Count,
                    files.Take(5).ToArray());
            })
            .ToArray();

        var pakInfo = new FileInfo(pakPath);

        return new PakSummary(
            pakInfo.FullName,
            pakInfo.Length,
            totalEntries,
            xmlEntries,
            dlcPackages.Count,
            uncompressedBytes,
            tuningCategories,
            topLevelFolders
                .OrderByDescending(pair => pair.Value)
                .Select(pair => $"{pair.Key} ({pair.Value})")
                .ToArray());
    }

    public static string ReadTextEntry(string pakPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var entry = PakEntryLocator.FindEntry(archive, entryPath)
            ?? throw new FileNotFoundException($"Entry was not found in pak: {entryPath}", entryPath);

        using var stream = entry.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
