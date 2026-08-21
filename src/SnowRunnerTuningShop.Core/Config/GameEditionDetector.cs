using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Config;

public sealed record GameEdition(string Id, string DisplayName);

public static class GameEditionDetector
{
    public static GameEdition Detect(string pakPath)
    {
        var normalized = pakPath.Replace('\\', '/').ToLowerInvariant();

        if (normalized.Contains("/steamapps/", StringComparison.Ordinal)
            || normalized.Contains("/steam/steamapps/", StringComparison.Ordinal))
        {
            return new GameEdition("steam", "Steam");
        }

        if (normalized.Contains("/gog galaxy/", StringComparison.Ordinal)
            || normalized.Contains("/gog games/", StringComparison.Ordinal)
            || normalized.Contains("/goggames/", StringComparison.Ordinal)
            || normalized.Contains("/gog/", StringComparison.Ordinal))
        {
            return new GameEdition("gog", "GOG");
        }

        if (normalized.Contains("/epic games/", StringComparison.Ordinal)
            || normalized.Contains("/epicgames/", StringComparison.Ordinal))
        {
            return new GameEdition("epic", "Epic");
        }

        if (normalized.Contains("/xboxgames/", StringComparison.Ordinal)
            || normalized.Contains("/microsoft.snowrunner", StringComparison.Ordinal)
            || normalized.Contains("/windowsapps/", StringComparison.Ordinal))
        {
            return new GameEdition("xbox", "Xbox");
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(pakPath)) ?? pakPath;
        var digest = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(directory.ToLowerInvariant())))
            .ToLowerInvariant()[..8];
        return new GameEdition($"custom_{digest}", "Custom");
    }

    public static string SanitizeEditionId(string editionId)
    {
        var safe = Regex.Replace(editionId.Trim().ToLowerInvariant(), @"[^a-z0-9_]+", "_");
        return string.IsNullOrWhiteSpace(safe) ? "custom" : safe;
    }
}
