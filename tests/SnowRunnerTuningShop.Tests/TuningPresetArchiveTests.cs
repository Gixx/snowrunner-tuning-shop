using System.IO.Compression;
using System.Text;
using SnowRunnerTuningShop.Core.Presets;

namespace SnowRunnerTuningShop.Tests;

public sealed class TuningPresetArchiveTests
{
    [Fact]
    public void RoundTrip_writes_and_reads_manifest_and_settings()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tsa-test-{Guid.NewGuid():N}.tsa");
        try
        {
            var package = new TuningPresetPackage(
                new TuningPresetManifest
                {
                    SchemaVersion = 1,
                    Id = "demo-preset",
                    Name = "Demo",
                    Version = "1.2.3",
                    Author = "Tester",
                    Locked = true,
                    CreatedUtc = DateTime.UtcNow,
                },
                new TuningPresetSettings
                {
                    SchemaVersion = 1,
                    Entries = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                    {
                        ["[media]/classes/engines/e.xml"] = Convert.ToBase64String(Encoding.UTF8.GetBytes("<Engine/>")),
                    },
                });

            TuningPresetArchive.Write(path, package);
            var loaded = TuningPresetArchive.Read(path);

            Assert.Equal("demo-preset", loaded.Manifest.Id);
            Assert.Equal("Demo", loaded.Manifest.Name);
            Assert.Equal("1.2.3", loaded.Manifest.Version);
            Assert.True(loaded.Manifest.Locked);
            Assert.Equal("Tester", loaded.Manifest.Author);
            Assert.Single(loaded.Settings.Entries);
            Assert.True(loaded.Settings.Entries.ContainsKey("[media]/classes/engines/e.xml"));
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
    public void Read_rejects_zip_without_manifest()
    {
        var path = Path.Combine(Path.GetTempPath(), $"tsa-bad-{Guid.NewGuid():N}.tsa");
        try
        {
            using (var stream = File.Create(path))
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                var entry = zip.CreateEntry("settings.json");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("""{"schemaVersion":1,"entries":{}}""");
            }

            Assert.Throws<InvalidDataException>(() => TuningPresetArchive.Read(path));
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
    public void ValidateManifest_requires_id_name_version()
    {
        Assert.Throws<InvalidDataException>(() =>
            TuningPresetArchive.ValidateManifest(new TuningPresetManifest
            {
                Id = "",
                Name = "x",
                Version = "1.0.0",
            }));
    }

    [Fact]
    public void SaveAsNew_can_create_locked_user_preset()
    {
        var dir = TuningPresetLibrary.GetUserPresetsDirectory();
        var before = Directory.EnumerateFiles(dir, "*.tsa").ToHashSet(StringComparer.OrdinalIgnoreCase);
        try
        {
            var info = TuningPresetLibrary.SaveAsNew(
                "Locked Pack",
                "1.0.0",
                new Dictionary<string, string> { ["a"] = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes("<x/>")) },
                locked: true,
                author: "Tester");
            Assert.True(info.Locked);
            Assert.Equal("Tester", info.Author);
            Assert.True(File.Exists(info.ArchivePath));
            var package = TuningPresetArchive.Read(info.ArchivePath!);
            Assert.True(package.Manifest.Locked);
        }
        finally
        {
            foreach (var path in Directory.EnumerateFiles(dir, "*.tsa"))
            {
                if (!before.Contains(path))
                {
                    File.Delete(path);
                }
            }
        }
    }

    [Fact]
    public void CanSaveAsNew_false_when_selection_locked()
    {
        var dirty = new TuningPresetDirtyState
        {
            ActivePresetId = "x",
            ActiveSource = TuningPresetSource.User,
            IsDirty = true,
        };
        var locked = new TuningPresetInfo(
            "x",
            "Locked",
            "1.0.0",
            Locked: true,
            TuningPresetSource.Bundled,
            ArchivePath: null);

        Assert.False(TuningPresetLibrary.CanSaveAsNew(dirty, locked));
    }

    [Fact]
    public void ExportPackage_rejects_locked_preset()
    {
        var info = new TuningPresetInfo(
            "locked-id",
            "Locked",
            "1.0.0",
            Locked: true,
            TuningPresetSource.Bundled,
            ArchivePath: Path.GetTempFileName());

        Assert.Throws<InvalidOperationException>(() =>
            TuningPresetLibrary.ExportPackage(info, Path.GetTempFileName()));
    }
}
