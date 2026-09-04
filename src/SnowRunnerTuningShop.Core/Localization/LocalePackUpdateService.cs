using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowRunnerTuningShop.Core.Localization;

public sealed record LocalePackFetchResult(
    bool Ok,
    IReadOnlyList<LocalePackSnapshot> Packs,
    string? ErrorMessage);

public static class LocalePackUpdateService
{
    private const string Branch = "main";

    private static readonly HttpClient Http = CreateClient();
    private static readonly HttpClient RawHttp = CreateRawClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string CatalogApiUrl =>
        $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/contents/assets/localization/{LocalePackNames.CatalogFileName}?ref={Branch}";

    public static async Task<LocalePackFetchResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var remote = await FetchCatalogAsync(cancellationToken).ConfigureAwait(false);
            var packs = LocalePackStore.BuildSnapshots(remote);
            return new LocalePackFetchResult(true, packs, null);
        }
        catch (Exception ex)
        {
            var packs = LocalePackStore.BuildSnapshots(null);
            return new LocalePackFetchResult(false, packs, ex.Message);
        }
    }

    public static async Task InstallAsync(string uiCulture, CancellationToken cancellationToken = default)
    {
        var remote = await FetchCatalogAsync(cancellationToken).ConfigureAwait(false);
        var entry = remote.FirstOrDefault(item =>
            string.Equals(item.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Language '{uiCulture}' is not in the remote catalog.");

        if (!IsCompatible(entry.MinAppVersion))
        {
            throw new InvalidOperationException(
                $"Language '{entry.DisplayName}' needs app version {entry.MinAppVersion} or newer.");
        }

        var json = await FetchFileAsync(entry.FileName, cancellationToken).ConfigureAwait(false);
        LocalePackStore.InstallOverlay(entry, json);
    }

    public static void Remove(string uiCulture) => LocalePackStore.RemoveOverlay(uiCulture);

    private static async Task<List<LocaleCatalogEntry>> FetchCatalogAsync(CancellationToken cancellationToken)
    {
        var json = await FetchGitHubFileAsync(CatalogApiUrl, cancellationToken).ConfigureAwait(false);
        var document = JsonSerializer.Deserialize<LocaleCatalogDocument>(json, JsonOptions);
        if (document?.Languages is null || document.Languages.Count == 0)
        {
            throw new InvalidOperationException("Remote language catalog is empty.");
        }

        return document.Languages
            .Where(entry => LocalePackNames.IsValidUiCulture(entry.UiCulture))
            .Select(entry => new LocaleCatalogEntry
            {
                UiCulture = entry.UiCulture.Trim(),
                DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName) ? entry.UiCulture.Trim() : entry.DisplayName.Trim(),
                GameLanguage = string.IsNullOrWhiteSpace(entry.GameLanguage) ? "english" : entry.GameLanguage.Trim(),
                InnoLanguage = string.IsNullOrWhiteSpace(entry.InnoLanguage) ? null : entry.InnoLanguage.Trim(),
                File = entry.File,
                Revision = entry.Revision <= 0 ? 1 : entry.Revision,
                MinAppVersion = string.IsNullOrWhiteSpace(entry.MinAppVersion) ? null : entry.MinAppVersion.Trim(),
            })
            .ToList();
    }

    private static async Task<string> FetchFileAsync(string fileName, CancellationToken cancellationToken)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName)
            || string.Equals(safeName, LocalePackNames.CatalogFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(safeName, LocalePackNames.KeysFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(safeName, LocalePackNames.OverlayManifestFileName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid locale file name.");
        }

        var url =
            $"https://api.github.com/repos/{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo}/contents/assets/localization/{Uri.EscapeDataString(safeName)}?ref={Branch}";
        return await FetchGitHubFileAsync(url, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<string> FetchGitHubFileAsync(string url, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var payload = await JsonSerializer.DeserializeAsync<GitHubContent>(stream, JsonOptions, cancellationToken)
            .ConfigureAwait(false);
        if (payload is null)
        {
            throw new InvalidOperationException("GitHub returned an empty language file.");
        }

        if (string.Equals(payload.Encoding, "base64", StringComparison.OrdinalIgnoreCase)
            && !string.IsNullOrWhiteSpace(payload.Content))
        {
            var raw = payload.Content.Replace("\n", "", StringComparison.Ordinal)
                .Replace("\r", "", StringComparison.Ordinal);
            return Encoding.UTF8.GetString(Convert.FromBase64String(raw));
        }

        if (string.IsNullOrWhiteSpace(payload.DownloadUrl))
        {
            throw new InvalidOperationException("GitHub language file has no download URL.");
        }

        return await RawHttp.GetStringAsync(payload.DownloadUrl, cancellationToken).ConfigureAwait(false);
    }

    private static bool IsCompatible(string? minAppVersion)
    {
        if (string.IsNullOrWhiteSpace(minAppVersion))
        {
            return true;
        }

        var text = minAppVersion.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        return !Version.TryParse(AppInfo.Version, out var installed)
            || !Version.TryParse(text, out var required)
            || installed >= required;
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(20),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SnowRunnerTuningShop", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private static HttpClient CreateRawClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SnowRunnerTuningShop", AppInfo.Version));
        return client;
    }

    private sealed class GitHubContent
    {
        [JsonPropertyName("encoding")]
        public string? Encoding { get; set; }

        [JsonPropertyName("content")]
        public string? Content { get; set; }

        [JsonPropertyName("download_url")]
        public string? DownloadUrl { get; set; }
    }
}
