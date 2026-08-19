using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Backup;

public sealed record PakBaselineInfo(
    string BaselinePath,
    DateTime LastWriteTimeUtc,
    long FileSizeBytes,
    string SourceDescription);

public sealed record ExternalPakBackupCandidate(
    string FilePath,
    DateTime LastWriteTimeUtc,
    long FileSizeBytes,
    string SourceDescription);

public static class PakBaselineService
{
    public const string BaselineSuffix = ".baseline";
    public const string PythonEditorDataDirName = "snowrunner_save_editor_data";
    public const string PythonInitialPakCategory = "initial_pak";
    public const string PythonBackupPrefix = "initial_pak__backup-";

    public static string GetBaselinePath(string pakPath) => pakPath + BaselineSuffix;

    public static bool HasBaseline(string pakPath) => File.Exists(GetBaselinePath(pakPath));

    public static PakBaselineInfo? TryGetBaselineInfo(string pakPath)
    {
        var baselinePath = GetBaselinePath(pakPath);
        if (!File.Exists(baselinePath))
        {
            return null;
        }

        var info = new FileInfo(baselinePath);
        return new PakBaselineInfo(
            info.FullName,
            info.LastWriteTimeUtc,
            info.Length,
            "App baseline");
    }

    public static string RequireBaseline(string pakPath)
    {
        var baselinePath = GetBaselinePath(pakPath);
        if (!File.Exists(baselinePath))
        {
            throw new InvalidOperationException(
                "No baseline is configured for this initial.pak. " +
                "Set a baseline from an unmodified initial.pak or import the oldest Python editor backup.");
        }

        return baselinePath;
    }

    public static PakBaselineInfo SetBaselineFromFile(string pakPath, string sourceFilePath)
    {
        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("Pak file was not found.", pakPath);
        }

        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Baseline source file was not found.", sourceFilePath);
        }

        var baselinePath = GetBaselinePath(pakPath);
        File.Copy(sourceFilePath, baselinePath, overwrite: true);

        var info = new FileInfo(baselinePath);
        return new PakBaselineInfo(
            info.FullName,
            info.LastWriteTimeUtc,
            info.Length,
            Path.GetFileName(sourceFilePath));
    }

    public static void ClearBaseline(string pakPath)
    {
        var baselinePath = GetBaselinePath(pakPath);
        if (File.Exists(baselinePath))
        {
            File.Delete(baselinePath);
        }
    }

    public static string RestorePakFromBaseline(string pakPath)
    {
        var baselinePath = RequireBaseline(pakPath);
        var backupPath = PakBackupService.CreateBackup(pakPath);
        File.Copy(baselinePath, pakPath, overwrite: true);
        return backupPath;
    }

    public static IReadOnlyList<ExternalPakBackupCandidate> FindPythonEditorBackups(string pakPath)
    {
        var results = new List<ExternalPakBackupCandidate>();
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var backupRoot in GetPythonBackupRoots(pakPath))
        {
            if (!Directory.Exists(backupRoot))
            {
                continue;
            }

            foreach (var backupFolder in Directory.EnumerateDirectories(backupRoot, $"{PythonBackupPrefix}*"))
            {
                var candidatePath = Path.Combine(backupFolder, "initial.pak");
                if (!File.Exists(candidatePath))
                {
                    continue;
                }

                try
                {
                    candidatePath = Path.GetFullPath(candidatePath);
                }
                catch
                {
                    continue;
                }

                if (!seenPaths.Add(candidatePath))
                {
                    continue;
                }

                var info = new FileInfo(candidatePath);
                results.Add(new ExternalPakBackupCandidate(
                    info.FullName,
                    info.LastWriteTimeUtc,
                    info.Length,
                    Path.GetFileName(backupFolder)));
            }
        }

        return results
            .OrderBy(candidate => candidate.LastWriteTimeUtc)
            .ThenBy(candidate => candidate.SourceDescription, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static ExternalPakBackupCandidate? TryGetOldestPythonEditorBackup(string pakPath)
    {
        return FindPythonEditorBackups(pakPath).FirstOrDefault();
    }

    public static PakBaselineInfo ImportOldestPythonEditorBackup(string pakPath)
    {
        var oldest = TryGetOldestPythonEditorBackup(pakPath)
            ?? throw new FileNotFoundException(
                "No Python editor initial.pak backups were found for this file.");

        return SetBaselineFromFile(pakPath, oldest.FilePath) with
        {
            SourceDescription = $"Python editor backup ({oldest.SourceDescription})",
        };
    }

    private static IEnumerable<string> GetPythonBackupRoots(string pakPath)
    {
        var roots = new List<string>();
        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(userProfile))
        {
            AppendPythonBackupCategoryRoots(
                roots,
                Path.Combine(userProfile, PythonEditorDataDirName, PythonInitialPakCategory),
                pakPath);
        }

        var pakDirectory = Path.GetDirectoryName(Path.GetFullPath(pakPath));
        if (!string.IsNullOrWhiteSpace(pakDirectory))
        {
            roots.Add(pakDirectory);
            AppendPythonBackupCategoryRoots(
                roots,
                Path.Combine(pakDirectory, "backups", PythonInitialPakCategory),
                pakPath);
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase);
    }

    private static void AppendPythonBackupCategoryRoots(List<string> roots, string categoryRoot, string pakPath)
    {
        roots.Add(Path.Combine(categoryRoot, CreatePythonSourceLabel(pakPath)));
        AppendExistingDirectoryRoots(roots, categoryRoot);
    }

    private static void AppendExistingDirectoryRoots(List<string> roots, string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        roots.Add(directoryPath);

        foreach (var childDirectory in Directory.EnumerateDirectories(directoryPath))
        {
            roots.Add(childDirectory);
        }
    }

    internal static string CreatePythonSourceLabel(string path)
    {
        var raw = path.Trim();
        var fullPath = Path.GetFullPath(string.IsNullOrWhiteSpace(raw) ? "source" : raw);
        var baseName = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        var safe = Regex.Replace(baseName ?? "source", @"[^A-Za-z0-9_.-]+", "_").Trim('.', '_', '-');
        if (string.IsNullOrWhiteSpace(safe))
        {
            safe = "source";
        }

        var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(fullPath))).ToLowerInvariant()[..12];
        return $"{safe}_{digest}";
    }
}
