namespace SnowRunnerTuningShop.Core;

public static class AppInfo
{
    public const string Version = "1.1.2";

    public const string GitHubOwner = "Gixx";
    public const string GitHubRepo = "snowrunner-tuning-shop";

    public static string LatestReleasePageUrl =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
