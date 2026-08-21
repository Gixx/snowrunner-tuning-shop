using System.IO.Compression;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tires;
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
            "trucks" => CountTrucks(pakPath),
            _ => 0,
        };
    }

    private static int CountTrucks(string pakPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var count = 0;
        foreach (var entry in archive.Entries)
        {
            var path = entry.FullName.Replace('\\', '/');
            if (IsTruckEntry(path))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsTruckEntry(string entryPath)
    {
        if (!entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

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