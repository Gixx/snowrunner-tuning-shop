using System.Globalization;
using System.Text;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Diagnostics;

/// <summary>
/// Builds and sends in-app bug reports (Mailtrap API when configured).
/// </summary>
public static class BugReportService
{
    public const int MaxDescriptionLength = 600;
    private const int MaxComposeBodyLength = 2400;

    public static string? TryGetActiveProfilePath()
    {
        var workspace = WorkspaceConfigStore.TryGetActiveWorkspace();
        if (workspace is null)
        {
            return null;
        }

        var path = TuningProfileService.GetProfilePath(workspace.EditionId);
        return File.Exists(path) ? path : null;
    }

    public static string BuildSubject() =>
        $"SnowRunner Tuning Shop bug report ({AppInfo.Version})";

    public static async Task SendAsync(
        string description,
        bool includeProfileAttachment,
        CancellationToken cancellationToken = default)
    {
        string? attachmentPath = null;
        if (includeProfileAttachment)
        {
            attachmentPath = TryGetActiveProfilePath();
            if (attachmentPath is null)
            {
                throw new InvalidOperationException("No tuning profile file is available to attach.");
            }
        }

        var body = BuildMessageBody(description, includeProfileAttachment, attachedInTransit: attachmentPath is not null);
        await MailtrapEmailClient.SendAsync(
            BuildSubject(),
            body,
            attachmentPath,
            cancellationToken).ConfigureAwait(false);
    }

    public static string BuildMessageBody(
        string description,
        bool includeProfileAttachment = false,
        bool attachedInTransit = false)
    {
        var trimmed = TruncateDescription(description);
        var session = CrashReportContext.GetSession();
        var builder = new StringBuilder();

        builder.AppendLine(trimmed);
        builder.AppendLine();
        builder.AppendLine("---");
        builder.AppendLine($"App version: {AppInfo.Version}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
        builder.AppendLine($".NET: {Environment.Version}");
        builder.AppendLine($"Culture: {CultureInfo.CurrentCulture.Name}");
        builder.AppendLine($"UI culture: {CultureInfo.CurrentUICulture.Name}");

        if (!string.IsNullOrWhiteSpace(CrashReportContext.CurrentPage))
        {
            builder.AppendLine($"Page: {CrashReportContext.CurrentPage}");
        }

        if (!string.IsNullOrWhiteSpace(CrashReportContext.VehicleId))
        {
            builder.AppendLine(
                $"Selection: {CrashReportContext.VehicleDisplayName} ({CrashReportContext.VehicleId})");
        }

        if (session is not null)
        {
            builder.AppendLine($"Pak loaded: {session.HasPak}");
            if (!string.IsNullOrWhiteSpace(session.EditionDisplayName))
            {
                builder.AppendLine($"Edition: {session.EditionDisplayName}");
            }

            if (!string.IsNullOrWhiteSpace(session.PakPath))
            {
                builder.AppendLine($"Pak path: {CrashReportBuilder.SanitizePathForReport(session.PakPath)}");
            }
        }

        var profilePath = TryGetActiveProfilePath();
        if (includeProfileAttachment && profilePath is not null)
        {
            builder.AppendLine(
                attachedInTransit
                    ? $"Tuning profile: attached as `{Path.GetFileNameWithoutExtension(profilePath)}.zip`"
                    : $"Tuning profile: please attach `{Path.GetFileName(profilePath)}`");
            builder.AppendLine($"Tuning profile path: {profilePath}");
        }
        else
        {
            builder.AppendLine(
                profilePath is null
                    ? "Tuning profile: (none on disk for active edition)"
                    : "Tuning profile: not attached (user declined)");
        }

        AppendWorkingPakDiagnostics(builder, session?.PakPath);

        return builder.ToString().TrimEnd();
    }

    /// <summary>
    /// English diagnostic lines: workspace health, Tuning Shop marker, Real Life mod,
    /// and whether the working pak likely differs due to non-Tuning-Shop edits.
    /// </summary>
    internal static void AppendWorkingPakDiagnostics(StringBuilder builder, string? sessionPakPath)
    {
        var workspace = WorkspaceConfigStore.TryGetActiveWorkspace();
        var pakPath = !string.IsNullOrWhiteSpace(sessionPakPath)
            ? sessionPakPath
            : workspace?.WorkingPakPath;

        if (string.IsNullOrWhiteSpace(pakPath) || !File.Exists(pakPath))
        {
            // Keep the same labels as the ready path so CI / empty-workspace reports stay parseable.
            builder.AppendLine("Working pak: (not available)");
            builder.AppendLine("Workspace health: not ready");
            builder.AppendLine("Working matches baseline: (n/a)");
            builder.AppendLine("Tuning Shop marker present: (n/a)");
            builder.AppendLine("Saved profile entries: (n/a)");
            builder.AppendLine("Likely external/other-tool edits: unknown (workspace not ready)");
            builder.AppendLine("Real Life mod: (n/a)");
            return;
        }

        var health = WorkspaceHealthService.Evaluate(pakPath);
        builder.AppendLine($"Workspace health: {FormatHealthKind(health.Kind)}");
        builder.AppendLine($"Working matches baseline: {health.WorkingMatchesBaseline}");
        builder.AppendLine($"Tuning Shop marker present: {health.HasMarker}");
        builder.AppendLine($"Saved profile entries: {health.ProfileEntryCount}");
        builder.AppendLine($"Likely external/other-tool edits: {FormatExternalEditLikelihood(health)}");

        if (RealLifeModDetector.TryDetect(pakPath, out var rlVersion))
        {
            builder.AppendLine(
                string.IsNullOrWhiteSpace(rlVersion)
                    ? "Real Life mod: detected"
                    : $"Real Life mod: detected (v{rlVersion})");
        }
        else
        {
            builder.AppendLine("Real Life mod: not detected");
        }
    }

    private static string FormatHealthKind(WorkspaceHealthKind kind) => kind switch
    {
        WorkspaceHealthKind.NotReady => "not ready",
        WorkspaceHealthKind.HealthyVanilla => "healthy vanilla (matches baseline, no marker)",
        WorkspaceHealthKind.HealthyTuned => "healthy tuned (Tuning Shop marker present)",
        WorkspaceHealthKind.ReadyToReapply => "ready to reapply (matches baseline, profile waiting)",
        WorkspaceHealthKind.GameUpdateDetected => "possible game update (differs from baseline, no marker, profile present)",
        WorkspaceHealthKind.UnknownExternalChange => "unknown change (differs from baseline, no marker, no waiting profile)",
        WorkspaceHealthKind.InconsistentMarker => "inconsistent marker (matches baseline but marker still present)",
        _ => kind.ToString(),
    };

    private static string FormatExternalEditLikelihood(WorkspaceHealth health) => health.Kind switch
    {
        WorkspaceHealthKind.UnknownExternalChange =>
            "likely (pak differs from baseline without a Tuning Shop marker; another tool/mod or a game update may have rewritten initial.pak)",
        WorkspaceHealthKind.GameUpdateDetected =>
            "possible (pak differs from baseline without a Tuning Shop marker; often a game update — less often another tool)",
        WorkspaceHealthKind.HealthyTuned =>
            "unlikely (Tuning Shop marker present — this app has written the pak)",
        WorkspaceHealthKind.HealthyVanilla =>
            "no (working pak matches the baseline)",
        WorkspaceHealthKind.ReadyToReapply =>
            "no (working pak matches the baseline; saved profile not yet reapplied)",
        WorkspaceHealthKind.InconsistentMarker =>
            "unclear (marker left on a baseline-matching pak)",
        _ => "unknown (workspace not ready)",
    };

    /// <summary>Opens Gmail's web compose UI (no GitHub account required).</summary>
    public static string BuildGmailComposeUrl(string description, bool includeProfileAttachment)
    {
        if (string.IsNullOrWhiteSpace(AppInfo.BugReportEmail))
        {
            throw new InvalidOperationException("Bug report email is not configured.");
        }

        var subject = BuildSubject();
        var body = Truncate(BuildMessageBody(description, includeProfileAttachment), MaxComposeBodyLength);
        return "https://mail.google.com/mail/?view=cm&fs=1"
            + "&to=" + Uri.EscapeDataString(AppInfo.BugReportEmail)
            + "&su=" + Uri.EscapeDataString(subject)
            + "&body=" + Uri.EscapeDataString(body);
    }

    /// <summary>Fallback for users with a desktop mail client association.</summary>
    public static string BuildMailToUrl(string description, bool includeProfileAttachment)
    {
        if (string.IsNullOrWhiteSpace(AppInfo.BugReportEmail))
        {
            throw new InvalidOperationException("Bug report email is not configured.");
        }

        var subject = Uri.EscapeDataString(BuildSubject());
        var body = Uri.EscapeDataString(
            Truncate(BuildMessageBody(description, includeProfileAttachment), MaxComposeBodyLength));
        return $"mailto:{AppInfo.BugReportEmail}?subject={subject}&body={body}";
    }

    /// <summary>
    /// Writes an unsent .eml draft (Outlook honors X-Unsent) and returns its path.
    /// Prefer this when a profile attachment should already be on the message.
    /// </summary>
    public static string WriteDraftEml(string description, bool includeProfileAttachment)
    {
        string? attachmentPath = null;
        if (includeProfileAttachment)
        {
            attachmentPath = TryGetActiveProfilePath();
            if (attachmentPath is null)
            {
                throw new InvalidOperationException("No tuning profile file is available to attach.");
            }
        }

        return WriteDraftEmlCore(description, attachmentPath);
    }

    internal static string WriteDraftEmlCore(string description, string? attachmentPath)
    {
        if (string.IsNullOrWhiteSpace(AppInfo.BugReportEmail))
        {
            throw new InvalidOperationException("Bug report email is not configured.");
        }

        if (attachmentPath is not null && !File.Exists(attachmentPath))
        {
            throw new FileNotFoundException("Tuning profile file was not found.", attachmentPath);
        }

        var draftsDir = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "bug-reports");
        Directory.CreateDirectory(draftsDir);
        var emlPath = Path.Combine(
            draftsDir,
            $"bug-report-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.eml");

        var subject = BuildSubject();
        var body = BuildMessageBody(description, attachmentPath is not null);
        var boundary = "----=_SnowRunnerTuningShop_" + Guid.NewGuid().ToString("N");

        var eml = new StringBuilder();
        eml.Append("X-Unsent: 1\r\n");
        eml.Append("MIME-Version: 1.0\r\n");
        eml.Append("To: ").Append(AppInfo.BugReportEmail).Append("\r\n");
        eml.Append("Subject: ").Append(EncodeHeader(subject)).Append("\r\n");
        eml.Append("Date: ").Append(DateTimeOffset.Now.ToString("r")).Append("\r\n");
        eml.Append("Content-Type: multipart/mixed; boundary=\"").Append(boundary).Append("\"\r\n");
        eml.Append("\r\n");
        eml.Append("--").Append(boundary).Append("\r\n");
        eml.Append("Content-Type: text/plain; charset=\"utf-8\"\r\n");
        eml.Append("Content-Transfer-Encoding: base64\r\n");
        eml.Append("\r\n");
        eml.Append(ToBase64Lines(Encoding.UTF8.GetBytes(NormalizeNewlines(body))));
        eml.Append("\r\n");

        if (attachmentPath is not null)
        {
            var fileName = Path.GetFileName(attachmentPath);
            var bytes = File.ReadAllBytes(attachmentPath);
            eml.Append("--").Append(boundary).Append("\r\n");
            eml.Append("Content-Type: application/json; name=\"").Append(fileName).Append("\"\r\n");
            eml.Append("Content-Transfer-Encoding: base64\r\n");
            eml.Append("Content-Disposition: attachment; filename=\"")
                .Append(fileName)
                .Append("\"\r\n");
            eml.Append("\r\n");
            eml.Append(ToBase64Lines(bytes));
            eml.Append("\r\n");
        }

        eml.Append("--").Append(boundary).Append("--\r\n");

        File.WriteAllText(emlPath, eml.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        return emlPath;
    }

    public static string TruncateDescription(string description)
    {
        var text = (description ?? string.Empty).Trim();
        if (text.Length <= MaxDescriptionLength)
        {
            return text;
        }

        return text[..MaxDescriptionLength];
    }

    private static string Truncate(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
        {
            return text;
        }

        return text[..Math.Max(0, maxChars - 3)] + "...";
    }

    private static string NormalizeNewlines(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\n", "\r\n", StringComparison.Ordinal);

    private static string EncodeHeader(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        return "=?utf-8?B?" + Convert.ToBase64String(bytes) + "?=";
    }

    private static string ToBase64Lines(byte[] bytes)
    {
        var b64 = Convert.ToBase64String(bytes);
        var builder = new StringBuilder(b64.Length + (b64.Length / 76) * 2);
        for (var i = 0; i < b64.Length; i += 76)
        {
            var len = Math.Min(76, b64.Length - i);
            builder.Append(b64, i, len);
            builder.Append("\r\n");
        }

        return builder.ToString().TrimEnd('\r', '\n');
    }
}
