using System.IO;

namespace SnowRunnerTuningShop;

internal static class AppPaths
{
    public static string? TryFindExamplePak()
    {
        var current = AppContext.BaseDirectory;

        for (var depth = 0; depth < 8; depth++)
        {
            var candidate = Path.GetFullPath(Path.Combine(current, "example.data", "initial.pak"));
            if (File.Exists(candidate))
            {
                return candidate;
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
