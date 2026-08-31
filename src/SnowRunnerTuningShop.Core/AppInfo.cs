namespace SnowRunnerTuningShop.Core;

public static class AppInfo
{
    public const string Version = "1.2.2";

    public const string GitHubOwner = "Gixx";
    public const string GitHubRepo = "snowrunner-tuning-shop";

    /// <summary>Optional crash-report destination. Leave empty to hide the email button.</summary>
    public const string CrashReportEmail = "";

    public static string IssueTrackerUrl =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/issues";

    public static string NewIssueUrl(string title, string body) =>
        IssueTrackerUrl
        + "/new?title="
        + Uri.EscapeDataString(title)
        + "&body="
        + Uri.EscapeDataString(body);

    public static string LatestReleasePageUrl =>
        $"https://github.com/{GitHubOwner}/{GitHubRepo}/releases/latest";

    public static string LatestReleaseApiUrl =>
        $"https://api.github.com/repos/{GitHubOwner}/{GitHubRepo}/releases/latest";
}
