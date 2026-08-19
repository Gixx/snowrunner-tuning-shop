using System.IO.Compression;

namespace SnowRunnerTuningShop.Core.Pak;

public static class InitialPakWriter
{
    public static int ReplaceEntries(string pakPath, IReadOnlyDictionary<string, byte[]> replacements)
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

        var directory = Path.GetDirectoryName(pakPath)
            ?? throw new InvalidOperationException("Invalid pak path.");
        var tempPath = Path.Combine(directory, $"{Path.GetFileName(pakPath)}.{Guid.NewGuid():N}.tmp");

        try
        {
            using (var source = ZipFile.OpenRead(pakPath))
            using (var target = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in source.Entries)
                {
                    var entryName = entry.FullName.Replace('\\', '/');
                    var targetEntry = target.CreateEntry(entryName, CompressionLevel.Optimal);

                    using var output = targetEntry.Open();
                    if (normalizedReplacements.TryGetValue(entryName, out var replacement))
                    {
                        output.Write(replacement, 0, replacement.Length);
                    }
                    else
                    {
                        using var input = entry.Open();
                        input.CopyTo(output);
                    }
                }
            }

            if (File.Exists(pakPath))
            {
                var attributes = File.GetAttributes(pakPath);
                if ((attributes & FileAttributes.ReadOnly) != 0)
                {
                    File.SetAttributes(pakPath, attributes & ~FileAttributes.ReadOnly);
                }
            }

            File.Move(tempPath, pakPath, overwrite: true);
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
}
