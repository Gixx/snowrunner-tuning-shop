using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.Profile;

public static class TuningProfilePaths
{
    private static readonly string[] RockMeshEntryNames =
    [
        "[meshes]/plants_small_rock_a",
        "[meshes]/plants_small_rock_a_rus",
        "[meshes]/plants_small_rock_b",
        "[meshes]/plants_small_rock_b_rus",
        "[meshes]/plants_small_rock_c",
        "[meshes]/plants_small_rock_c_rus",
        "[meshes]/plants_small_forest_rock_a",
        "[meshes]/plants_small_forest_rock_b",
        "[meshes]/plants_small_forest_rock_c",
    ];

    private static readonly HashSet<string> RockMeshEntries = new(RockMeshEntryNames, StringComparer.OrdinalIgnoreCase);

    public static bool IsTrackedEntry(string entryPath)
    {
        if (TuningProfileMarker.IsMarkerEntry(entryPath))
        {
            return false;
        }

        var normalized = entryPath.Replace('\\', '/');
        if (RockMeshEntries.Contains(normalized))
        {
            return true;
        }

        if (normalized.Equals(PakCacheBlockLayoutGuard.CacheBlockEntry, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.StartsWith("[ssl_cache]/", StringComparison.OrdinalIgnoreCase)
            && normalized.EndsWith(".sslbundle", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!normalized.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Contains("/classes/models/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (normalized.Contains("/classes/plants/", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var category in PakPaths.TuningCategories)
        {
            if (!normalized.Contains($"/classes/{category}/", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            return category.Equals("trucks", StringComparison.OrdinalIgnoreCase)
                ? IsTruckEntry(normalized) || IsTrailerEntry(normalized)
                : true;
        }

        return false;
    }

    private static bool IsTruckEntry(string entryPath)
    {
        const string marker = "/classes/trucks/";
        var index = entryPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var relative = entryPath[(index + marker.Length)..];
        return relative.Length > 0
            && !relative.Contains('/')
            && !relative.Contains('\\');
    }

    private static bool IsTrailerEntry(string entryPath) =>
        entryPath.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase);
}
