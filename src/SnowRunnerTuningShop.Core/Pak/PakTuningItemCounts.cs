using System.IO.Compression;
using SnowRunnerTuningShop.Core.AddonCapacity;
using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Trailers;
using SnowRunnerTuningShop.Core.Winch;

namespace SnowRunnerTuningShop.Core.Pak;

public static class PakTuningItemCounts
{
    public static int Count(string pakPath, string categoryId)
    {
        if (string.IsNullOrWhiteSpace(pakPath) || !File.Exists(pakPath))
        {
            return 0;
        }

        return categoryId.ToLowerInvariant() switch
        {
            "winches" => WinchService.LoadWinches(pakPath).Count,
            "engines" => EngineService.LoadEngines(pakPath).Count,
            "gearboxes" => GearboxService.LoadGearboxes(pakPath).Count,
            "suspensions" => SuspensionService.LoadSuspensions(pakPath).Count,
            "wheels" => TireService.LoadTires(pakPath).Count,
            "addons" => AddonCapacityService.LoadAddons(pakPath).Count,
            "cranes" => CraneService.LoadCranes(pakPath).Count,
            "trailers" => TrailerTuningService.LoadTrailers(pakPath).Count,
            "trucks" => CountMatchingXmlEntries(pakPath, "trucks"),
            _ => 0,
        };
    }

    private static int CountMatchingXmlEntries(string pakPath, string categoryId)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var count = 0;
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (PakPaths.IsCategoryXmlEntry(categoryId, path))
            {
                count++;
            }
        }

        return count;
    }
}
