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

public sealed record AppUpdateDownloadProgress(long BytesReceived, long? TotalBytes)
{
    public double? Percent =>
        TotalBytes is > 0
            ? Math.Clamp(100.0 * BytesReceived / TotalBytes.Value, 0, 100)
            : null;
}

public static class AppUpdateService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly HttpClient DownloadHttp = CreateDownloadClient();
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

    public static string BuildInstallerTempPath(string? installerUrl, string? latestVersion)
    {
        var fileName = TryGetFileNameFromUrl(installerUrl);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            var version = string.IsNullOrWhiteSpace(latestVersion) ? "latest" : latestVersion.Trim();
            fileName = $"SnowRunnerTuningShop-v{version}-Setup.exe";
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalid, '_');
        }

        var folder = Path.Combine(Path.GetTempPath(), "SnowRunnerTuningShop", "updates");
        Directory.CreateDirectory(folder);
        return Path.Combine(folder, fileName);
    }

    public static async Task DownloadInstallerAsync(
        string installerUrl,
        string destinationPath,
        IProgress<AppUpdateDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installerUrl);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var tempPath = destinationPath + ".partial";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, installerUrl);
            using var response = await DownloadHttp
                .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using var destination = new FileStream(
                tempPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                options: FileOptions.Asynchronous | FileOptions.SequentialScan);

            var buffer = new byte[81920];
            long received = 0;
            progress?.Report(new AppUpdateDownloadProgress(received, total));

            while (true)
            {
                var read = await source.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken)
                    .ConfigureAwait(false);
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
                received += read;
                progress?.Report(new AppUpdateDownloadProgress(received, total));
            }

            await destination.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            TryDelete(tempPath);
            throw;
        }

        if (File.Exists(destinationPath))
        {
            File.Delete(destinationPath);
        }

        File.Move(tempPath, destinationPath);
    }

    private static string? TryGetFileNameFromUrl(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)
            || !Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return null;
        }

        var name = Path.GetFileName(uri.LocalPath);
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // Best-effort cleanup of partial downloads.
        }
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

    private static HttpClient CreateDownloadClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(30),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SnowRunnerTuningShop", AppInfo.Version));
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
