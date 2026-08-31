using System.IO.Compression;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Pak;

public static class InitialPakWriter
{
    /// <summary>
    /// Replaces existing pak entries and adds any replacement keys that are not already in the archive.
    /// </summary>
    public static int ReplaceEntries(
        string pakPath,
        IReadOnlyDictionary<string, byte[]> replacements,
        bool syncProfile = true)
    {
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

        var normalizedReplacements = replacements.ToDictionary(
            pair => pair.Key.Replace('\\', '/'),
            pair => pair.Value,
            StringComparer.Ordinal);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"SnowRunnerTuningShop-{Guid.NewGuid():N}.pak.tmp");

        ClearReadOnlyAttribute(pakPath);

        try
        {
            var pendingAdds = new Dictionary<string, byte[]>(normalizedReplacements, StringComparer.Ordinal);

            using (var source = ZipFile.OpenRead(pakPath))
            using (var target = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var entryName = entry.FullName.Replace('\\', '/');
                    var targetEntry = target.CreateEntry(entryName, CompressionLevel.Optimal);

                    using var output = targetEntry.Open();
                    if (pendingAdds.TryGetValue(entryName, out var replacement))
                    {
                        output.Write(replacement, 0, replacement.Length);
                        pendingAdds.Remove(entryName);
                    }
                    else
                    {
                        using var input = entry.Open();
                        input.CopyTo(output);
                    }
                }

                foreach (var (entryName, bytes) in pendingAdds)
                {
                    var targetEntry = target.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var output = targetEntry.Open();
                    output.Write(bytes, 0, bytes.Length);
                }
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

            return normalizedReplacements.Count;
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
    /// Rebuilds the pak without the specified entries.
    /// </summary>
    public static int RemoveEntries(
        string pakPath,
        IReadOnlyCollection<string> entryPaths,
        bool syncProfile = true)
    {
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

        var removedPaths = new HashSet<string>(
            entryPaths.Select(path => PakEntryLocator.NormalizeEntryPath(path)),
            StringComparer.OrdinalIgnoreCase);

        var tempPath = Path.Combine(
            Path.GetTempPath(),
            $"SnowRunnerTuningShop-{Guid.NewGuid():N}.pak.tmp");

        ClearReadOnlyAttribute(pakPath);
        var removedCount = 0;

        try
        {
            using (var source = ZipFile.OpenRead(pakPath))
            using (var target = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var entryName = entry.FullName.Replace('\\', '/');
                    if (removedPaths.Contains(entryName))
                    {
                        removedCount++;
                        continue;
                    }

                    var targetEntry = target.CreateEntry(entryName, CompressionLevel.Optimal);
                    using var output = targetEntry.Open();
                    using var input = entry.Open();
                    input.CopyTo(output);
                }
            }

            if (removedCount == 0)
            {
                return 0;
            }

            if (File.Exists(pakPath))
            {
                ClearReadOnlyAttribute(pakPath);
            }

            File.Move(tempPath, pakPath, overwrite: true);

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
