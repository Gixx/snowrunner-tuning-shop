using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowRunnerTuningShop.Core.Diagnostics;

public sealed record GitHubIssueMatch(int Number, string HtmlUrl, string Title);

public sealed record CrashReportSubmission(
    CrashReport Report,
    string LogFilePath,
    GitHubIssueMatch? ExistingIssue,
    string GitHubActionUrl,
    string? MailToUrl);

public static class CrashReportService
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public static string LogsDirectory =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnowRunnerTuningShop",
            "logs");

    public static CrashReport Build(Exception exception, bool isTerminating) =>
        CrashReportBuilder.Build(exception, isTerminating);

    public static string SaveToDisk(CrashReport report)
    {
        Directory.CreateDirectory(LogsDirectory);
        var fileName = $"crash-{report.OccurredAtUtc:yyyyMMdd-HHmmss}-{report.Fingerprint}.txt";
        var path = Path.Combine(LogsDirectory, fileName);
        File.WriteAllText(path, report.FullText, Encoding.UTF8);
        return path;
    }

    public static async Task<CrashReportSubmission> PrepareSubmissionAsync(
        CrashReport report,
        string logFilePath,
        CancellationToken cancellationToken = default)
    {
        var existing = await FindExistingIssueAsync(report.Fingerprint, cancellationToken).ConfigureAwait(false);
        var actionUrl = existing is not null
            ? existing.HtmlUrl
            : BuildNewIssueUrl(report);
        var mailTo = BuildMailToUrl(report);

        return new CrashReportSubmission(
            report,
            logFilePath,
            existing,
            actionUrl,
            mailTo);
    }

    public static async Task<GitHubIssueMatch?> FindExistingIssueAsync(
        string fingerprint,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(fingerprint))
        {
            return null;
        }

        try
        {
            var query =
                $"repo:{AppInfo.GitHubOwner}/{AppInfo.GitHubRepo} is:issue is:open {fingerprint} in:body";
            var url =
                $"https://api.github.com/search/issues?q={Uri.EscapeDataString(query)}&per_page=5";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync<GitHubSearchResponse>(stream, JsonOptions, cancellationToken)
                .ConfigureAwait(false);
            var issue = payload?.Items?.FirstOrDefault(item =>
                item.HtmlUrl is not null
                && (item.Body?.Contains(fingerprint, StringComparison.OrdinalIgnoreCase) == true
                    || item.Title?.Contains(fingerprint, StringComparison.OrdinalIgnoreCase) == true));
            if (issue?.HtmlUrl is null || issue.Number <= 0)
            {
                return null;
            }

            return new GitHubIssueMatch(issue.Number, issue.HtmlUrl, issue.Title ?? $"Issue #{issue.Number}");
        }
        catch
        {
            return null;
        }
    }

    public static string BuildNewIssueUrl(CrashReport report)
    {
        var title = Truncate(
            $"[Crash {report.Fingerprint}] {ShortExceptionTitle(report)}",
            120);
        var body = BuildIssueBody(report);
        return AppInfo.NewIssueUrl(title, body);
    }

    public static string? BuildMailToUrl(CrashReport report)
    {
        if (string.IsNullOrWhiteSpace(AppInfo.CrashReportEmail))
        {
            return null;
        }

        var subject = Uri.EscapeDataString(
            $"SnowRunner Tuning Shop crash ({AppInfo.Version}) [{report.Fingerprint}]");
        var body = Uri.EscapeDataString(Truncate(report.FullText, 1800));
        return $"mailto:{AppInfo.CrashReportEmail}?subject={subject}&body={body}";
    }

    private static string BuildIssueBody(CrashReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("## Crash report");
        builder.AppendLine();
        builder.AppendLine("Submitted automatically from SnowRunner Tuning Shop.");
        builder.AppendLine();
        builder.AppendLine("```text");
        builder.AppendLine(report.FullText.TrimEnd());
        builder.AppendLine("```");
        return Truncate(builder.ToString(), 6500);
    }

    private static string ShortExceptionTitle(CrashReport report)
    {
        var type = report.ExceptionType;
        var dot = type.LastIndexOf('.');
        if (dot >= 0 && dot + 1 < type.Length)
        {
            type = type[(dot + 1)..];
        }

        var message = string.IsNullOrWhiteSpace(report.Message) ? "Unexpected error" : report.Message.Trim();
        message = message.Replace('\r', ' ').Replace('\n', ' ');
        return Truncate($"{type}: {message}", 90);
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength];

    private static HttpClient CreateClient()
    {
        var client = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("SnowRunnerTuningShop", AppInfo.Version));
        client.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-GitHub-Api-Version", "2022-11-28");
        return client;
    }

    private sealed class GitHubSearchResponse
    {
        [JsonPropertyName("items")]
        public GitHubIssue[]? Items { get; set; }
    }

    private sealed class GitHubIssue
    {
        [JsonPropertyName("number")]
        public int Number { get; set; }

        [JsonPropertyName("html_url")]
        public string? HtmlUrl { get; set; }

        [JsonPropertyName("title")]
        public string? Title { get; set; }

        [JsonPropertyName("body")]
        public string? Body { get; set; }
    }
}
