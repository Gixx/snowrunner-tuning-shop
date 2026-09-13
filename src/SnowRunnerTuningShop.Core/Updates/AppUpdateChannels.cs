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

    public static string Normalize(string? value) =>
        value switch
        {
            Beta => Beta,
            _ => Stable,
        };

    public static AppUpdateChannel Parse(string? value) =>
        Normalize(value) == Beta ? AppUpdateChannel.Beta : AppUpdateChannel.Stable;

    public static string ToConfigValue(AppUpdateChannel channel) =>
        channel == AppUpdateChannel.Beta ? Beta : Stable;
}
