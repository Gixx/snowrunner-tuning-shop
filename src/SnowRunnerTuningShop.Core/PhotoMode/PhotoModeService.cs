using System.IO.Compression;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.PhotoMode;

public static class PhotoModeService
{
    public const string CacheBlockEntry = "initial.cache_block";

    public static PhotoModeSettings LoadSettings(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var cacheEntry = archive.GetEntry(CacheBlockEntry)
            ?? throw new PhotoModeLoadException($"Missing {CacheBlockEntry} in pak.");

        byte[] cacheBytes;
        using (var stream = cacheEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            cacheBytes = memory.ToArray();
        }

        var settings = PhotoModeCacheBlockEditor.ReadSettings(cacheBytes);
        var releaseEntry = archive.GetEntry(PhotoModeSslBundleEditor.ReleaseBundle);
        if (releaseEntry is not null)
        {
            using var stream = releaseEntry.Open();
            using var memory = new MemoryStream();
            stream.CopyTo(memory);
            var timeIndex = PhotoModeSslBundleEditor.ReadTimeIndex(memory.ToArray());
            settings = settings.With(timeIndex: timeIndex);
        }

        return settings;
    }

    public static PhotoModeSaveResult ApplySettings(string pakPath, PhotoModeSettings settings)
    {
        _ = PakBaselineService.RequireBaseline(pakPath);
        PakBaselineService.EnsureWritableWorkingPak(pakPath);

        Dictionary<string, byte[]> replacements;
        using (var archive = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);

            var cacheEntry = archive.GetEntry(CacheBlockEntry)
                ?? throw new PhotoModeLoadException($"Missing {CacheBlockEntry} in pak.");
            byte[] cacheBytes;
            using (var stream = cacheEntry.Open())
            using (var memory = new MemoryStream())
            {
                stream.CopyTo(memory);
                cacheBytes = memory.ToArray();
            }

            var updatedCache = PhotoModeCacheBlockEditor.ApplySettings(cacheBytes, settings);
            if (!cacheBytes.AsSpan().SequenceEqual(updatedCache))
            {
                replacements[CacheBlockEntry] = updatedCache;
            }

            foreach (var bundlePath in PhotoModeSslBundleEditor.BundlePaths)
            {
                var entry = archive.GetEntry(bundlePath);
                if (entry is null)
                {
                    continue;
                }

                byte[] bundleBytes;
                using (var stream = entry.Open())
                using (var memory = new MemoryStream())
                {
                    stream.CopyTo(memory);
                    bundleBytes = memory.ToArray();
                }

                var updatedBundle = PhotoModeSslBundleEditor.WriteTimeIndex(bundleBytes, settings.TimeIndex);
                if (!bundleBytes.AsSpan().SequenceEqual(updatedBundle))
                {
                    replacements[bundlePath] = updatedBundle;
                }
            }
        }

        if (replacements.Count == 0)
        {
            return new PhotoModeSaveResult(0);
        }

        var updated = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new PhotoModeSaveResult(updated);
    }

    public static PhotoModeSaveResult RestoreBaseline(string pakPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        PakBaselineService.EnsureWritableWorkingPak(pakPath);

        Dictionary<string, byte[]> replacements;
        using (var baseline = ZipFile.OpenRead(baselinePath))
        using (var working = ZipFile.OpenRead(pakPath))
        {
            replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            AddIfDifferent(baseline, working, CacheBlockEntry, replacements);

            foreach (var bundlePath in PhotoModeSslBundleEditor.BundlePaths)
            {
                AddIfDifferent(baseline, working, bundlePath, replacements);
            }
        }

        if (replacements.Count == 0)
        {
            return new PhotoModeSaveResult(0);
        }

        var updated = InitialPakWriter.ReplaceEntries(pakPath, replacements);
        return new PhotoModeSaveResult(updated);
    }

    private static void AddIfDifferent(
        ZipArchive baseline,
        ZipArchive working,
        string entryPath,
        Dictionary<string, byte[]> replacements)
    {
        var baselineEntry = baseline.GetEntry(entryPath);
        var workingEntry = working.GetEntry(entryPath);
        if (baselineEntry is null || workingEntry is null)
        {
            return;
        }

        byte[] baselineBytes;
        byte[] workingBytes;
        using (var stream = baselineEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            baselineBytes = memory.ToArray();
        }

        using (var stream = workingEntry.Open())
        using (var memory = new MemoryStream())
        {
            stream.CopyTo(memory);
            workingBytes = memory.ToArray();
        }

        if (!workingBytes.AsSpan().SequenceEqual(baselineBytes))
        {
            replacements[entryPath] = baselineBytes;
        }
    }
}
