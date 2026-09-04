using System.Diagnostics;

namespace SnowRunnerTuningShop.Core.Game;

/// <summary>
/// Detects a running SnowRunner process so the app can refuse writes to initial.pak.
/// </summary>
public static class SnowRunnerProcessGuard
{
    private static readonly string[] ProcessNames =
    [
        "SnowRunner",
        "SnowRunner_BE",
    ];

    public static bool IsRunning()
    {
        foreach (var name in ProcessNames)
        {
            try
            {
                var processes = Process.GetProcessesByName(name);
                try
                {
                    if (processes.Length > 0)
                    {
                        return true;
                    }
                }
                finally
                {
                    foreach (var process in processes)
                    {
                        process.Dispose();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                // Process exited while enumerating.
            }
            catch (System.ComponentModel.Win32Exception)
            {
                // Access denied for a process — treat as not conclusive; keep checking others.
            }
        }

        return false;
    }

    public static void ThrowIfRunning()
    {
        if (IsRunning())
        {
            throw new InvalidOperationException(
                "SnowRunner is running. Close the game before changing initial.pak.");
        }
    }
}
