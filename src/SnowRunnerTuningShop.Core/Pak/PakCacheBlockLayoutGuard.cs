namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// SnowRunner maps fixed offsets inside initial.pak. initial.cache_block must stay
/// before the localization string tables, not at the end of the archive.
/// </summary>
internal static class PakCacheBlockLayoutGuard
{
    internal const string CacheBlockEntry = "initial.cache_block";
    private const string ExpectedFollowingEntry = "[strings]/strings_brazilian_portuguese.str";

    internal static void EnsureValidLayout(string pakPath)
    {
        if (string.IsNullOrWhiteSpace(pakPath) || !File.Exists(pakPath))
        {
            throw new FileNotFoundException("Pak file was not found.", pakPath);
        }

        var pakBytes = File.ReadAllBytes(pakPath);
        var entries = PakRawZipReplacer.ReadCentralDirectoryForPatching(pakBytes);
        var index = -1;
        for (var i = 0; i < entries.Count; i++)
        {
            if (string.Equals(entries[i].Name, CacheBlockEntry, StringComparison.Ordinal))
            {
                index = i;
                break;
            }
        }

        if (index < 0)
        {
            throw new InvalidDataException($"Missing {CacheBlockEntry} in pak.");
        }

        if (index >= entries.Count - 2)
        {
            throw new InvalidOperationException(
                $"{CacheBlockEntry} was moved to the end of initial.pak. SnowRunner will hang on the loading screen. " +
                "Restore initial.pak from a backup or use Restore full baseline on the Home page, then reapply your tuning profile.");
        }

        var followingEntry = entries[index + 1].Name;
        if (!string.Equals(followingEntry, ExpectedFollowingEntry, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{CacheBlockEntry} is in the wrong place in initial.pak (next entry is {followingEntry}, expected {ExpectedFollowingEntry}). " +
                "Restore initial.pak from a backup or use Restore full baseline on the Home page, then reapply your tuning profile.");
        }
    }
}
