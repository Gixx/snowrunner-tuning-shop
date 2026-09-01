using System.IO.Compression;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
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

    public static IReadOnlyList<PhotoModeSliderConstraint> LoadSliderConstraints(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var cacheEntry = archive.GetEntry(CacheBlockEntry)
            ?? throw new PhotoModeLoadException($"Missing {CacheBlockEntry} in pak.");

        using var stream = cacheEntry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return PhotoModeSliderConstraints.Resolve(memory.ToArray());
    }

    public static PhotoModeSaveResult ApplySettings(
        string pakPath,
        PhotoModeSettings settings,
        bool saveProfile = true)
    {
        _ = PakBaselineService.RequireBaseline(pakPath);
        PakBaselineService.EnsureWritableWorkingPak(pakPath);
        EnsureCacheBlockLayoutOrThrow(pakPath);

        settings = settings.With(timeIndex: ReadTimeIndexFromPak(pakPath));

        var updatedCount = 0;
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
            PhotoModeCacheBlockEditor.ValidateAppliedSettings(updatedCache, settings);
            if (!cacheBytes.AsSpan().SequenceEqual(updatedCache))
            {
                replacements[CacheBlockEntry] = updatedCache;
            }
        }

        if (replacements.Count > 0)
        {
            updatedCount += InitialPakWriter.ReplaceEntries(pakPath, replacements, syncProfile: false);
        }

        if (saveProfile)
        {
            PhotoModeProfileService.SaveProfile(pakPath, settings);
        }

        return new PhotoModeSaveResult(updatedCount);
    }

    private static int ReadTimeIndexFromPak(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var releaseEntry = archive.GetEntry(PhotoModeSslBundleEditor.ReleaseBundle);
        if (releaseEntry is null)
        {
            return PhotoModeTimeIndex.GameDefault;
        }

        using var stream = releaseEntry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return PhotoModeSslBundleEditor.ReadTimeIndex(memory.ToArray());
    }

    public static PhotoModeSaveResult RestoreBaseline(string pakPath)
    {
        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        PakBaselineService.EnsureWritableWorkingPak(pakPath);

        var entriesToRestore = new List<string>();
        using (var baseline = ZipFile.OpenRead(baselinePath))
        using (var working = ZipFile.OpenRead(pakPath))
        {
            AddIfDifferentEntry(baseline, working, CacheBlockEntry, entriesToRestore);

            foreach (var bundlePath in PhotoModeSslBundleEditor.BundlePaths)
            {
                AddIfDifferentEntry(baseline, working, bundlePath, entriesToRestore);
            }
        }

        if (entriesToRestore.Count == 0)
        {
            ClearSavedProfileIfKnown(pakPath);
            return new PhotoModeSaveResult(0);
        }

        var updated = InitialPakWriter.CopyEntriesFromPak(
            pakPath,
            baselinePath,
            entriesToRestore,
            syncProfile: false);
        ClearSavedProfileIfKnown(pakPath);
        return new PhotoModeSaveResult(updated);
    }

    private static void ClearSavedProfileIfKnown(string pakPath)
    {
        var editionId = WorkspaceConfigStore.TryResolveEditionId(pakPath);
        if (!string.IsNullOrWhiteSpace(editionId))
        {
            PhotoModeProfileService.ClearProfile(editionId);
        }
    }

    private static void AddIfDifferentEntry(
        ZipArchive baseline,
        ZipArchive working,
        string entryPath,
        List<string> entriesToRestore)
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
            entriesToRestore.Add(entryPath);
        }
    }

    private static void EnsureCacheBlockLayoutOrThrow(string pakPath)
    {
        try
        {
            PakCacheBlockLayoutGuard.EnsureValidLayout(pakPath);
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException)
        {
            throw new PhotoModeLoadException(ex.Message);
        }
    }
}
