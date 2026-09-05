using System.IO.Compression;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SnowRunnerTuningShop.Core.Diagnostics;

/// <summary>Sends transactional mail via Mailtrap Email Sending API.</summary>
public static class MailtrapEmailClient
{
    private const string SendUrl = "https://send.api.mailtrap.io/api/send";
    /// <summary>Limit on the zipped attachment size.</summary>
    private const long MaxAttachmentBytes = 12 * 1024 * 1024;
    private static readonly HttpClient Http = CreateClient();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    };

    public static async Task SendAsync(
        string subject,
        string textBody,
        string? attachmentPath,
        CancellationToken cancellationToken = default)
    {
        if (!BugReportSecrets.IsMailtrapConfigured)
        {
            throw new InvalidOperationException(
                "Mailtrap is not configured. Add BugReportSecrets.Local.cs with your API token.");
        }

        var payload = new MailtrapSendRequest
        {
            From = new MailtrapAddress
            {
                Email = BugReportSecrets.FromEmail,
                Name = BugReportSecrets.FromName,
            },
            To =
            [
                new MailtrapAddress { Email = AppInfo.BugReportEmail },
            ],
            Subject = subject,
            Text = textBody,
            Category = "bug-report",
        };

        string? tempZipPath = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(attachmentPath))
            {
                if (!File.Exists(attachmentPath))
                {
                    throw new FileNotFoundException("Tuning profile file was not found.", attachmentPath);
                }

                tempZipPath = Path.Combine(
                    Path.GetTempPath(),
                    $"srts-bug-profile-{Guid.NewGuid():N}.zip");
                CreateProfileZip(attachmentPath, tempZipPath);

                var zipInfo = new FileInfo(tempZipPath);
                if (zipInfo.Length > MaxAttachmentBytes)
                {
                    throw new InvalidOperationException(
                        $"Zipped tuning profile is too large to attach ({zipInfo.Length / (1024 * 1024.0):0.0} MB). Max {MaxAttachmentBytes / (1024 * 1024)} MB.");
                }

                var bytes = await File.ReadAllBytesAsync(tempZipPath, cancellationToken).ConfigureAwait(false);
                var zipFileName = Path.GetFileNameWithoutExtension(attachmentPath) + ".zip";
                payload.Attachments =
                [
                    new MailtrapAttachment
                    {
                        Filename = zipFileName,
                        Type = "application/zip",
                        Disposition = "attachment",
                        Content = Convert.ToBase64String(bytes),
                    },
                ];
            }

            var json = JsonSerializer.Serialize(payload, JsonOptions);
            using var request = new HttpRequestMessage(HttpMethod.Post, SendUrl);
            request.Headers.TryAddWithoutValidation("Api-Token", BugReportSecrets.MailtrapApiToken);
            request.Headers.TryAddWithoutValidation("User-Agent", $"SnowRunnerTuningShop/{AppInfo.Version}");
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException(
                    $"Mailtrap send failed ({(int)response.StatusCode}): {Truncate(responseBody, 400)}");
            }
        }
        finally
        {
            if (tempZipPath is not null)
            {
                try
                {
                    File.Delete(tempZipPath);
                }
                catch
                {
                    // ignore temp cleanup
                }
            }
        }
    }

    private static void CreateProfileZip(string profilePath, string zipPath)
    {
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create);
        archive.CreateEntryFromFile(profilePath, Path.GetFileName(profilePath), CompressionLevel.SmallestSize);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(90) };
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return client;
    }

    private static string Truncate(string text, int max)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= max)
        {
            return text;
        }

        return text[..max] + "...";
    }

    private sealed class MailtrapSendRequest
    {
        public MailtrapAddress? From { get; set; }
        public MailtrapAddress[]? To { get; set; }
        public string? Subject { get; set; }
        public string? Text { get; set; }
        public string? Category { get; set; }
        public MailtrapAttachment[]? Attachments { get; set; }
    }

    private sealed class MailtrapAddress
    {
        public string? Email { get; set; }
        public string? Name { get; set; }
    }

    private sealed class MailtrapAttachment
    {
        public string? Content { get; set; }
        public string? Filename { get; set; }
        public string? Type { get; set; }
        public string? Disposition { get; set; }
    }
}
