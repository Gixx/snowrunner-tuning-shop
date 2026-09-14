namespace SnowRunnerTuningShop.Core.Updates;

public enum AppUpdateChannel
{
    Stable,
    Beta,
}

public static class AppUpdateChannels
{
    public const string Stable = "Stable";
    public const string Beta = "Beta";

    /// <summary>
    /// Map a stored/selected value to Stable or Beta.
    /// Null/empty is treated as Stable — use <see cref="Resolve"/> for unset-config derivation.
    /// </summary>
    public static string Normalize(string? value) =>
        value switch
        {
            Beta => Beta,
            _ => Stable,
        };

    /// <summary>
    /// Effective channel: explicit config wins; otherwise derive from the installed app version
    /// (prerelease build → Beta, stable build → Stable).
    /// </summary>
    public static string Resolve(string? configuredChannel, string? installedAppVersion)
    {
        if (!string.IsNullOrWhiteSpace(configuredChannel))
        {
            return Normalize(configuredChannel);
        }

        return DefaultForInstalledVersion(installedAppVersion);
    }

    public static string DefaultForInstalledVersion(string? installedAppVersion) =>
        AppSemVersion.TryParse(installedAppVersion, out var version) && version.IsPrerelease
            ? Beta
            : Stable;

    public static AppUpdateChannel Parse(string? value) =>
        Normalize(value) == Beta ? AppUpdateChannel.Beta : AppUpdateChannel.Stable;

    public static string ToConfigValue(AppUpdateChannel channel) =>
        channel == AppUpdateChannel.Beta ? Beta : Stable;
}
