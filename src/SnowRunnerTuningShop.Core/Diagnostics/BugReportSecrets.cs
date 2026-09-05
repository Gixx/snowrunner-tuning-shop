namespace SnowRunnerTuningShop.Core.Diagnostics;

/// <summary>
/// Mailtrap sending identity. The API token lives in <c>BugReportSecrets.Local.cs</c>
/// (gitignored) — copy from <c>BugReportSecrets.Local.cs.example</c>.
/// </summary>
public static partial class BugReportSecrets
{
    public const string FromEmail = "tuningshop-bugs@gaborivan.de";
    public const string FromName = "SnowRunner Tuning Shop";

#if !BUGREPORT_SECRETS_LOCAL
    /// <summary>Empty unless BugReportSecrets.Local.cs is present at build time.</summary>
    public const string MailtrapApiToken = "";
#endif

    public static bool IsMailtrapConfigured =>
        !string.IsNullOrWhiteSpace(MailtrapApiToken)
        && !MailtrapApiToken.StartsWith("PASTE_", StringComparison.Ordinal)
        && !string.IsNullOrWhiteSpace(FromEmail)
        && !string.IsNullOrWhiteSpace(AppInfo.BugReportEmail);
}
