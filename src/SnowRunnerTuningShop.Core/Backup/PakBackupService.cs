namespace SnowRunnerTuningShop.Core.Backup;

public static class PakBackupService
{
    public static string CreateBackup(string pakPath)
    {
        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("Pak file to back up was not found.", pakPath);
        }

        var directory = Path.GetDirectoryName(pakPath)
            ?? throw new InvalidOperationException("Invalid pak path.");

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupPath = Path.Combine(directory, $"initial.pak.backup-{timestamp}");

        File.Copy(pakPath, backupPath, overwrite: false);
        return backupPath;
    }
}
