using System.Security.Cryptography;
using System.Text;

namespace SnowRunnerTuningShop.Core.Diagnostics;

public sealed record CrashReport(
    string Fingerprint,
    string ExceptionType,
    string Message,
    string StackTrace,
    string FullText,
    bool IsTerminating,
    DateTimeOffset OccurredAtUtc);

public static class CrashReportBuilder
{
    public static CrashReport Build(Exception exception, bool isTerminating)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var stack = exception.StackTrace ?? "";
        var fingerprint = ComputeFingerprint(exception);
        var fullText = ComposeFullText(exception, fingerprint, stack, isTerminating);

        return new CrashReport(
            fingerprint,
            exception.GetType().FullName ?? exception.GetType().Name,
            exception.Message,
            stack,
            fullText,
            isTerminating,
            DateTimeOffset.UtcNow);
    }

    public static string ComputeFingerprint(Exception exception)
    {
        var builder = new StringBuilder();
        builder.Append(exception.GetType().FullName);
        builder.Append('|');
        builder.Append(NormalizeMessage(exception.Message));

        foreach (var frame in ExtractStackFrames(exception.StackTrace))
        {
            builder.Append('|');
            builder.Append(frame);
            if (builder.Length > 512)
            {
                break;
            }
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }

    private static string NormalizeMessage(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return "";
        }

        var text = message.Trim();
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\d+", "#");
        return text.Length > 160 ? text[..160] : text;
    }

    private static IEnumerable<string> ExtractStackFrames(string? stackTrace)
    {
        if (string.IsNullOrWhiteSpace(stackTrace))
        {
            yield break;
        }

        var count = 0;
        foreach (var line in stackTrace.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!line.StartsWith("at ", StringComparison.Ordinal))
            {
                continue;
            }

            if (line.Contains("System.", StringComparison.Ordinal)
                || line.Contains("Microsoft.", StringComparison.Ordinal)
                || line.Contains("MS.", StringComparison.Ordinal))
            {
                continue;
            }

            yield return line[3..].Trim();
            count++;
            if (count >= 4)
            {
                yield break;
            }
        }
    }

    private static string ComposeFullText(
        Exception exception,
        string fingerprint,
        string stackTrace,
        bool isTerminating)
    {
        var session = CrashReportContext.GetSession();
        var builder = new StringBuilder();

        builder.AppendLine("SnowRunner Tuning Shop crash report");
        builder.AppendLine($"Fingerprint: {fingerprint}");
        builder.AppendLine($"App version: {AppInfo.Version}");
        builder.AppendLine($"Occurred (UTC): {DateTimeOffset.UtcNow:O}");
        builder.AppendLine($"Terminating: {isTerminating}");
        builder.AppendLine($"OS: {Environment.OSVersion}");
        builder.AppendLine($"64-bit OS: {Environment.Is64BitOperatingSystem}");
        builder.AppendLine($"64-bit process: {Environment.Is64BitProcess}");
        builder.AppendLine($".NET: {Environment.Version}");
        builder.AppendLine($"Culture: {System.Globalization.CultureInfo.CurrentCulture.Name}");
        builder.AppendLine($"UI culture: {System.Globalization.CultureInfo.CurrentUICulture.Name}");

        if (!string.IsNullOrWhiteSpace(CrashReportContext.CurrentPage))
        {
            builder.AppendLine($"Page: {CrashReportContext.CurrentPage}");
        }

        if (!string.IsNullOrWhiteSpace(CrashReportContext.VehicleId))
        {
            builder.AppendLine($"Vehicle id: {CrashReportContext.VehicleId}");
        }

        if (!string.IsNullOrWhiteSpace(CrashReportContext.VehicleDisplayName))
        {
            builder.AppendLine($"Vehicle: {CrashReportContext.VehicleDisplayName}");
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
                builder.AppendLine($"Pak path: {session.PakPath}");
            }
        }

        builder.AppendLine();
        builder.AppendLine("Exception:");
        builder.AppendLine($"{exception.GetType().FullName}: {exception.Message}");

        if (!string.IsNullOrWhiteSpace(stackTrace))
        {
            builder.AppendLine();
            builder.AppendLine("Stack trace:");
            builder.AppendLine(stackTrace);
        }

        if (exception.InnerException is not null)
        {
            builder.AppendLine();
            builder.AppendLine("Inner exception:");
            builder.AppendLine($"{exception.InnerException.GetType().FullName}: {exception.InnerException.Message}");
            if (!string.IsNullOrWhiteSpace(exception.InnerException.StackTrace))
            {
                builder.AppendLine(exception.InnerException.StackTrace);
            }
        }

        return builder.ToString();
    }
}
