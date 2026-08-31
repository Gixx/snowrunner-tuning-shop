using SnowRunnerTuningShop.Core.Constants;

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

    private static readonly HashSet<string> PhotoModeEntries = new(StringComparer.OrdinalIgnoreCase)
    {
        "initial.cache_block",
        "[ssl_cache]/initial_release.sslbundle",
        "[ssl_cache]/initial_debug.sslbundle",
        "[ssl_cache]/initial_profile.sslbundle",
    };

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

        if (PhotoModeEntries.Contains(normalized))
        {
            return true;
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
                ? IsTruckEntry(normalized)
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
}
