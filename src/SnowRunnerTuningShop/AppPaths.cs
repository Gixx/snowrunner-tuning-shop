using System.IO;

namespace SnowRunnerTuningShop;

internal static class AppPaths
{
    public static string? TryFindExamplePak()
    {
        var root = TryFindRepoRoot();
        if (root is null)
        {
            return null;
        }

        var candidate = Path.Combine(root, "example.data", "initial.pak");
        return File.Exists(candidate) ? candidate : null;
    }

    public static string? TryFindVehiclesAssetsDirectory()
    {
        var root = TryFindRepoRoot();
        if (root is not null)
        {
            var fromRepo = Path.Combine(root, "assets", "vehicles");
            if (Directory.Exists(fromRepo) && File.Exists(Path.Combine(fromRepo, "catalog.json")))
            {
                return fromRepo;
            }
        }

        var fromOutput = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "assets", "vehicles"));
        if (Directory.Exists(fromOutput) && File.Exists(Path.Combine(fromOutput, "catalog.json")))
        {
            return fromOutput;
        }

        return null;
    }

    private static string? TryFindRepoRoot()
    {
        var current = AppContext.BaseDirectory;

        for (var depth = 0; depth < 10; depth++)
        {
            var hasSolution = File.Exists(Path.Combine(current, "SnowRunnerTuningShop.slnx"))
                || Directory.Exists(Path.Combine(current, "assets", "vehicles"))
                || Directory.Exists(Path.Combine(current, "example.data"));

            if (hasSolution)
            {
                return current;
            }

            var parent = Directory.GetParent(current);
            if (parent is null)
            {
                break;
            }

            current = parent.FullName;
        }

        return null;
    }
}
