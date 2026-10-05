using System.IO.Compression;
using System.Text;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Tests;

public sealed class RealLifeModDetectorTests
{
    [Fact]
    public void Detects_real_life_marker_and_version_in_strings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rl-detect-{Guid.NewGuid():N}.pak");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("[strings]/strings_russian.str");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.WriteLine("""UI_Something "Hello Real Life Mod v49.0.0 {STRING}" """);
            }

            Assert.True(RealLifeModDetector.TryDetect(path, out var version));
            Assert.Equal("49.0.0", version);
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }

    [Fact]
    public void Returns_false_for_vanilla_strings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"rl-vanilla-{Guid.NewGuid():N}.pak");
        try
        {
            using (var archive = ZipFile.Open(path, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("[strings]/strings_english.str");
                using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
                writer.WriteLine("""UI_Something "Thanks, you were a real lifesaver!" """);
            }

            Assert.False(RealLifeModDetector.IsDetected(path));
        }
        finally
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
    }
}
