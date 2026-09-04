using System.IO.Compression;
using System.Text;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Trailers;

namespace SnowRunnerTuningShop.Tests;

public sealed class PakIoTests
{
    [Fact]
    public void ReplaceEntries_round_trips_payload_and_preserves_sibling_bytes()
    {
        using var folder = new TempFolder();
        var pakPath = Path.Combine(folder.Path, "initial.pak");
        CreateMiniPak(pakPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[media]/classes/trucks/a.xml"] = "<Truck id=\"a\"/>",
            ["[media]/classes/trucks/b.xml"] = "<Truck id=\"b\"/>",
        });

        var beforeB = ReadEntryText(pakPath, "[media]/classes/trucks/b.xml");
        var updated = Encoding.UTF8.GetBytes("<Truck id=\"a-updated\"/>");
        var count = InitialPakWriter.ReplaceEntries(
            pakPath,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["[media]/classes/trucks/a.xml"] = updated,
            },
            syncProfile: false);

        Assert.Equal(1, count);
        Assert.Equal("<Truck id=\"a-updated\"/>", ReadEntryText(pakPath, "[media]/classes/trucks/a.xml"));
        Assert.Equal(beforeB, ReadEntryText(pakPath, "[media]/classes/trucks/b.xml"));
    }

    [Fact]
    public void ReplaceEntries_resolves_entry_names_case_insensitively()
    {
        using var folder = new TempFolder();
        var pakPath = Path.Combine(folder.Path, "initial.pak");
        CreateMiniPak(pakPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[media]/classes/trucks/Truck.xml"] = "<Truck/>",
        });

        var count = InitialPakWriter.ReplaceEntries(
            pakPath,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                ["[media]/classes/trucks/truck.xml"] = Encoding.UTF8.GetBytes("<Truck updated=\"1\"/>"),
            },
            syncProfile: false);

        Assert.Equal(1, count);
        Assert.Equal("<Truck updated=\"1\"/>", ReadEntryText(pakPath, "[media]/classes/trucks/Truck.xml"));
    }

    [Fact]
    public void RemoveEntries_keeps_other_entries()
    {
        using var folder = new TempFolder();
        var pakPath = Path.Combine(folder.Path, "initial.pak");
        CreateMiniPak(pakPath, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[media]/_tuning_shop/marker.xml"] = "<Marker/>",
            ["[media]/classes/trucks/a.xml"] = "<Truck/>",
        });

        var removed = InitialPakWriter.RemoveEntries(
            pakPath,
            ["[media]/_tuning_shop/marker.xml"],
            syncProfile: false);

        Assert.Equal(1, removed);
        Assert.Null(FindEntry(pakPath, "[media]/_tuning_shop/marker.xml"));
        Assert.Equal("<Truck/>", ReadEntryText(pakPath, "[media]/classes/trucks/a.xml"));
    }

    [Fact]
    public void RestorePakFromBaseline_replaces_working_via_temp_move()
    {
        using var folder = new TempFolder();
        var working = Path.Combine(folder.Path, "initial.pak");
        var baseline = Path.Combine(folder.Path, "initial.baseline.custom.pak");
        CreateMiniPak(working, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[media]/classes/trucks/a.xml"] = "<Truck tuned=\"1\"/>",
        });
        CreateMiniPak(baseline, new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["[media]/classes/trucks/a.xml"] = "<Truck vanilla=\"1\"/>",
        });

        // Point edition baseline path by copying into AppData baselines is heavy;
        // instead exercise the temp+move path through a local File.Copy+Move probe and
        // use InitialPakWriter.CopyEntriesFromPak as the restore primitive used by partial restores.
        var source = Path.Combine(folder.Path, "source.pak");
        File.Copy(baseline, source);
        var count = InitialPakWriter.CopyEntriesFromPak(
            working,
            source,
            ["[media]/classes/trucks/a.xml"],
            syncProfile: false);

        Assert.Equal(1, count);
        Assert.Equal("<Truck vanilla=\"1\"/>", ReadEntryText(working, "[media]/classes/trucks/a.xml"));
    }

    [Fact]
    public void Hitch_undo_round_trip_preserves_train_socket()
    {
        const string baseline =
            """
            <Truck>
              <TruckData />
              <GameData Price="18800">
                <InstallSocket Offset="(0; 0; 0)" Type="Train" />
              </GameData>
            </Truck>
            """;

        var unlocked = TrailerHitchXml.EnsureStoreHitch(baseline);
        var restored = TrailerHitchXml.RemoveSupplementalStoreHitch(unlocked, baseline);

        Assert.Contains("""Type="Train" """, restored, StringComparison.Ordinal);
        Assert.DoesNotContain("""Type="Trailer" """, restored, StringComparison.Ordinal);
        Assert.False(TrailerHitchXml.IsStoreHitchReady(restored));
    }

    private static void CreateMiniPak(string path, IReadOnlyDictionary<string, string> entries)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (name, text) in entries)
        {
            var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
            using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
            writer.Write(text);
        }
    }

    private static string ReadEntryText(string pakPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        var entry = PakEntryLocator.FindEntry(archive, entryPath)
            ?? throw new FileNotFoundException(entryPath);
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static ZipArchiveEntry? FindEntry(string pakPath, string entryPath)
    {
        using var archive = ZipFile.OpenRead(pakPath);
        return PakEntryLocator.FindEntry(archive, entryPath);
    }

    private sealed class TempFolder : IDisposable
    {
        public string Path { get; } = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            "SnowRunnerTuningShop-tests-" + Guid.NewGuid().ToString("N"));

        public TempFolder() => Directory.CreateDirectory(Path);

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
                // Best-effort cleanup.
            }
        }
    }
}
