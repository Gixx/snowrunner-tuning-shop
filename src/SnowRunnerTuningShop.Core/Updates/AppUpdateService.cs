using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowRunnerTuningShop.Core.Updates;

public enum AppUpdateStatus
{
    UpToDate,
    UpdateAvailable,
    Failed,
}

public sealed record AppUpdateCheckResult(
    AppUpdateStatus Status,
    string InstalledVersion,
    string? LatestVersion,
    string ReleasePageUrl,
    string? InstallerUrl,
    string? ErrorMessage);

public static class AppUpdateService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private static AppUpdateCheckResult? _cache;
    private static DateTimeOffset _cachedAt;

    public static async Task<AppUpdateCheckResult> CheckAsync(
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (!forceRefresh
            && _cache is not null
            && DateTimeOffset.UtcNow - _cachedAt < TimeSpan.FromMinutes(10))
        {
            return _cache;
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, AppInfo.LatestReleaseApiUrl);
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var release = await JsonSerializer.DeserializeAsync<GitHubRelease>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);

            var latestTag = release?.TagName;
            if (!TryParseVersion(latestTag, out var latest)
                || !TryParseVersion(AppInfo.Version, out var installed))
            {
                return Cache(new AppUpdateCheckResult(
                    AppUpdateStatus.Failed,
                    AppInfo.Version,
                    latestTag,
                    AppInfo.LatestReleasePageUrl,
                    null,
                    "The GitHub release tag could not be parsed."));
            }

            var pageUrl = string.IsNullOrWhiteSpace(release?.HtmlUrl)
                ? AppInfo.LatestReleasePageUrl
                : release.HtmlUrl;
            var installerUrl = FindInstallerUrl(release?.Assets);

            if (latest <= installed)
            {
                return Cache(new AppUpdateCheckResult(
                    AppUpdateStatus.UpToDate,
                    AppInfo.Version,
                    FormatVersion(latest),
                    pageUrl,
                    installerUrl,
                    null));
            }

            return Cache(new AppUpdateCheckResult(
                AppUpdateStatus.UpdateAvailable,
                AppInfo.Version,
                FormatVersion(latest),
                pageUrl,
                installerUrl,
                null));
        }
        catch (Exception ex)
        {
            return Cache(new AppUpdateCheckResult(
                AppUpdateStatus.Failed,
                AppInfo.Version,
                null,
                AppInfo.LatestReleasePageUrl,
                null,
                ex.Message));
        }
    }

    public static bool IsSameVersion(string? left, string? right)
    {
        if (!TryParseVersion(left, out var a) || !TryParseVersion(right, out var b))
        {
            return string.Equals(left?.Trim(), right?.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        return a == b;
    }

    private static AppUpdateCheckResult Cache(AppUpdateCheckResult result)
    {
        _cache = result;
        _cachedAt = DateTimeOffset.UtcNow;
        return result;
    }

    private static string? FindInstallerUrl(GitHubAsset[]? assets)
    {
        if (assets is null || assets.Length == 0)
        {
            return null;
        }

        var setup = assets.FirstOrDefault(asset =>
            !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl)
            && asset.Name is not null
            && asset.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase)
            && asset.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));

        return setup?.BrowserDownloadUrl
            ?? assets.FirstOrDefault(asset => !string.IsNullOrWhiteSpace(asset.BrowserDownloadUrl))
                ?.BrowserDownloadUrl;
    }

    private static bool TryParseVersion(string? value, out Version version)
    {
        version = new Version(0, 0);
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = value.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return Version.TryParse(text, out version!);
    }

    private static string FormatVersion(Version version) =>
        version.Build < 0
            ? $"{version.Major}.{version.Minor}"
            : $"{version.Major}.{version.Minor}.{version.Build}";

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(8),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SnowRunnerTuningShop", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")]
        public string? TagName { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("assets")]
        public GitHubAsset[]? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("browser_download_url")]
        public string? BrowserDownloadUrl { get; set; }
    }
}
