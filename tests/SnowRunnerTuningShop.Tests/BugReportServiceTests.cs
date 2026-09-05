using System.Text;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Diagnostics;

namespace SnowRunnerTuningShop.Tests;

public sealed class BugReportServiceTests
{
    [Fact]
    public void TruncateDescription_limits_to_max_length()
    {
        var text = new string('a', BugReportService.MaxDescriptionLength + 40);
        var truncated = BugReportService.TruncateDescription(text);
        Assert.Equal(BugReportService.MaxDescriptionLength, truncated.Length);
    }

    [Fact]
    public void BuildSubject_includes_app_version()
    {
        Assert.Contains(AppInfo.Version, BugReportService.BuildSubject(), StringComparison.Ordinal);
    }

    [Fact]
    public void WriteDraftEml_includes_description_destination_and_profile_attachment()
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "srts-bug-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var profilePath = Path.Combine(tempRoot, "tuning-profile.test.json");
            File.WriteAllText(profilePath, """{"schemaVersion":1,"entries":{}}""", Encoding.UTF8);

            var emlPath = BugReportService.WriteDraftEmlCore(
                "Store crashes after applying tires",
                profilePath);

            Assert.True(File.Exists(emlPath));
            var eml = File.ReadAllText(emlPath);
            Assert.StartsWith("X-Unsent: 1", eml, StringComparison.Ordinal);
            Assert.Contains(AppInfo.BugReportEmail, eml, StringComparison.Ordinal);
            Assert.Contains("tuning-profile.test.json", eml, StringComparison.Ordinal);
            Assert.Contains("Content-Disposition: attachment;", eml, StringComparison.Ordinal);

            try
            {
                File.Delete(emlPath);
            }
            catch
            {
                // ignore
            }
        }
        finally
        {
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch
            {
                // ignore cleanup failures
            }
        }
    }
}
