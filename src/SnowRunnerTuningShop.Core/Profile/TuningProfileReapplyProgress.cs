namespace SnowRunnerTuningShop.Core.Profile;

public enum TuningProfileReapplyPhase
{
    Preparing,
    StagingPak,
    WritingPak,
    Finalizing,
}

public sealed record TuningProfileReapplyProgress(
    TuningProfileReapplyPhase Phase,
    int Current,
    int Total,
    string? EntryPath = null);

public static class TuningProfileEntryCategories
{
    public const string Engines = "engines";
    public const string Gearboxes = "gearboxes";
    public const string Suspensions = "suspensions";
    public const string Winches = "winches";
    public const string Tires = "tires";
    public const string Vehicles = "vehicles";
    public const string Rocks = "rocks";
    public const string General = "general";
    public const string PhotoMode = "photo-mode";
    public const string Pak = "pak";

    public static string Resolve(string? entryPath)
    {
        if (string.IsNullOrWhiteSpace(entryPath))
        {
            return Pak;
        }

        var normalized = entryPath.Replace('\\', '/').ToLowerInvariant();
        if (normalized.Contains("/classes/engines/", StringComparison.Ordinal))
        {
            return Engines;
        }

        if (normalized.Contains("/classes/gearboxes/", StringComparison.Ordinal))
        {
            return Gearboxes;
        }

        if (normalized.Contains("/classes/suspensions/", StringComparison.Ordinal))
        {
            return Suspensions;
        }

        if (normalized.Contains("/classes/winches/", StringComparison.Ordinal))
        {
            return Winches;
        }

        if (normalized.Contains("/classes/wheels/", StringComparison.Ordinal)
            || normalized.Contains("/classes/tires/", StringComparison.Ordinal))
        {
            return Tires;
        }

        if (normalized.Contains("/classes/trucks/", StringComparison.Ordinal))
        {
            return Vehicles;
        }

        if (normalized.Contains("/classes/plants/", StringComparison.Ordinal)
            || normalized.Contains("[meshes]/plants_small_", StringComparison.Ordinal))
        {
            return Rocks;
        }

        if (normalized.Contains("/classes/cameras/", StringComparison.Ordinal)
            || normalized.Contains("/classes/models/", StringComparison.Ordinal))
        {
            return General;
        }

        if (normalized.Contains("[ssl_cache]/", StringComparison.Ordinal)
            || normalized.Contains("initial.cache_block", StringComparison.Ordinal))
        {
            return PhotoMode;
        }

        return Pak;
    }
}
