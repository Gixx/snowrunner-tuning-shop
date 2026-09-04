using System.IO.Compression;
using SnowRunnerTuningShop.Core.Game;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Pak;

public static class InitialPakWriter
{
    /// <summary>
    /// Replaces existing pak entries and adds any replacement keys that are not already in the archive.
    /// Untouched entries keep their original compressed bytes (required for SnowRunner).
    /// </summary>
    public static int ReplaceEntries(
        string pakPath,
        IReadOnlyDictionary<string, byte[]> replacements,
        bool syncProfile = true,
        IProgress<PakWriteProgress>? writeProgress = null)
    {
        SnowRunnerProcessGuard.ThrowIfRunning();

        if (string.IsNullOrWhiteSpace(pakPath))
        {
            throw new ArgumentException("Pak file path is required.", nameof(pakPath));
        }

        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("Pak file was not found.", pakPath);
        }

        if (replacements.Count == 0)
        {
            return 0;
        }

        var normalizedReplacements = PakEntryNameMap.ToOrdinalIgnoreCaseDictionary(replacements);

        byte[]? cacheBlockBytes = null;
        if (normalizedReplacements.Remove(PakCacheBlockLayoutGuard.CacheBlockEntry, out var cacheBytes))
        {
            cacheBlockBytes = cacheBytes;
        }

        if (normalizedReplacements.Count == 0 && cacheBlockBytes is null)
        {
            return 0;
        }

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"SnowRunnerTuningShop-{Guid.NewGuid():N}.pak.tmp");

        ClearReadOnlyAttribute(pakPath);

        try
        {
            writeProgress?.Report(new PakWriteProgress(PakWritePhase.Copying, 0, 1));
            File.Copy(pakPath, tempPath, overwrite: true);
            writeProgress?.Report(new PakWriteProgress(PakWritePhase.Copying, 1, 1));

            var existingEntries = PakEntryNameMap.ReadCanonicalNames(tempPath);
            var replacementKeys = normalizedReplacements.Keys.ToArray();
            var toReplace = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            var toAdd = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            for (var index = 0; index < replacementKeys.Length; index++)
            {
                var key = replacementKeys[index];
                if (existingEntries.TryGetValue(key, out var canonicalName))
                {
                    toReplace[canonicalName] = normalizedReplacements[key];
                }
                else
                {
                    toAdd[PakEntryLocator.NormalizeEntryPath(key)] = normalizedReplacements[key];
                }

                var current = index + 1;
                if (current == 1
                    || current == replacementKeys.Length
                    || current % 200 == 0)
                {
                    writeProgress?.Report(new PakWriteProgress(
                        PakWritePhase.Preparing,
                        current,
                        replacementKeys.Length,
                        key));
                }
            }

            if (toReplace.Count > 0)
            {
                PakRawZipReplacer.ReplaceEntries(tempPath, toReplace, writeProgress);
            }

            if (cacheBlockBytes is not null)
            {
                if (!PakInPlaceZipPatcher.TryReplaceEntry(
                        tempPath,
                        PakCacheBlockLayoutGuard.CacheBlockEntry,
                        cacheBlockBytes))
                {
                    throw new InvalidOperationException(
                        "Could not update initial.cache_block in place. The compressed entry would grow and shift " +
                        "later pak data, which crashes SnowRunner. Try a smaller change, restore photo mode, or pick a " +
                        "value with the same character length as the original default.");
                }
            }

            if (toAdd.Count > 0)
            {
                AddEntries(tempPath, toAdd);
            }

            ClearReadOnlyAttribute(pakPath);

            try
            {
                writeProgress?.Report(new PakWriteProgress(PakWritePhase.Saving, 0, 1));
                File.Move(tempPath, pakPath, overwrite: true);
                writeProgress?.Report(new PakWriteProgress(PakWritePhase.Saving, 1, 1));
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Cannot update initial.pak because another program is using it. " +
                    "Close SnowRunner if it is running, then try again.",
                    ex);
            }

            if (syncProfile)
            {
                TuningProfileService.SyncAfterPakWrite(pakPath);
            }

            return toReplace.Count + toAdd.Count + (cacheBlockBytes is not null ? 1 : 0);
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Copies raw zip records for the given entries from another pak (typically the baseline).
    /// Used for restore flows so entries keep their original compressed bytes.
    /// </summary>
    public static int CopyEntriesFromPak(
        string targetPakPath,
        string sourcePakPath,
        IReadOnlyCollection<string> entryPaths,
        bool syncProfile = true)
    {
        SnowRunnerProcessGuard.ThrowIfRunning();

        if (string.IsNullOrWhiteSpace(targetPakPath))
        {
            throw new ArgumentException("Pak file path is required.", nameof(targetPakPath));
        }

        if (!File.Exists(targetPakPath))
        {
            throw new FileNotFoundException("Pak file was not found.", targetPakPath);
        }

        if (string.IsNullOrWhiteSpace(sourcePakPath) || !File.Exists(sourcePakPath))
        {
            throw new FileNotFoundException("Source pak file was not found.", sourcePakPath);
        }

        if (entryPaths.Count == 0)
        {
            return 0;
        }

        var normalizedPaths = entryPaths
            .Select(path => PakEntryLocator.NormalizeEntryPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"SnowRunnerTuningShop-{Guid.NewGuid():N}.pak.tmp");

        ClearReadOnlyAttribute(targetPakPath);

        try
        {
            File.Copy(targetPakPath, tempPath, overwrite: true);
            var canonicalByRequest = PakEntryNameMap.ReadCanonicalNames(tempPath);
            var resolvedPaths = new List<string>(normalizedPaths.Length);
            foreach (var path in normalizedPaths)
            {
                if (!canonicalByRequest.TryGetValue(path, out var canonical))
                {
                    throw new FileNotFoundException($"Pak entry was not found in target: {path}", targetPakPath);
                }

                resolvedPaths.Add(canonical);
            }

            PakRawZipReplacer.CopyEntriesFromSource(tempPath, sourcePakPath, resolvedPaths);
            ClearReadOnlyAttribute(targetPakPath);

            try
            {
                File.Move(tempPath, targetPakPath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Cannot update initial.pak because another program is using it. " +
                    "Close SnowRunner if it is running, then try again.",
                    ex);
            }

            if (syncProfile)
            {
                TuningProfileService.SyncAfterPakWrite(targetPakPath);
            }

            return normalizedPaths.Length;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    /// <summary>
    /// Rebuilds the pak without the specified entries, keeping untouched local records verbatim.
    /// </summary>
    public static int RemoveEntries(
        string pakPath,
        IReadOnlyCollection<string> entryPaths,
        bool syncProfile = true)
    {
        SnowRunnerProcessGuard.ThrowIfRunning();

        if (string.IsNullOrWhiteSpace(pakPath))
        {
            throw new ArgumentException("Pak file path is required.", nameof(pakPath));
        }

        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("Pak file was not found.", pakPath);
        }

        if (entryPaths.Count == 0)
        {
            return 0;
        }

        var normalizedPaths = entryPaths
            .Select(path => PakEntryLocator.NormalizeEntryPath(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"SnowRunnerTuningShop-{Guid.NewGuid():N}.pak.tmp");

        ClearReadOnlyAttribute(pakPath);

        try
        {
            File.Copy(pakPath, tempPath, overwrite: true);
            var removedCount = PakRawZipReplacer.RemoveEntries(tempPath, normalizedPaths);
            if (removedCount == 0)
            {
                return 0;
            }

            ClearReadOnlyAttribute(pakPath);

            try
            {
                File.Move(tempPath, pakPath, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new InvalidOperationException(
                    "Cannot update initial.pak because another program is using it. " +
                    "Close SnowRunner if it is running, then try again.",
                    ex);
            }

            if (syncProfile)
            {
                TuningProfileService.SyncAfterPakWrite(pakPath);
            }

            return removedCount;
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static void AddEntries(string pakPath, IReadOnlyDictionary<string, byte[]> entries)
    {
        if (entries.Count == 0)
        {
            return;
        }

        try
        {
            PakRawZipReplacer.AddEntries(pakPath, entries);
        }
        catch (IOException ex)
        {
            throw new InvalidOperationException(
                "Cannot update initial.pak because another program is using it. " +
                "Close SnowRunner if it is running, then try again.",
                ex);
        }
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
