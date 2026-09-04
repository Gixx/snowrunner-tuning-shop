using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Trailers;

namespace SnowRunnerTuningShop.Tests;

public sealed class PakFileIdTests
{
    private sealed record Item(string Id, string Path);

    [Fact]
    public void Prefers_pakId_exact_match_over_wiki_catalog_id()
    {
        var items = new[]
        {
            new Item("ank_mk38", "[media]/classes/trucks/ank_mk38.xml"),
            new Item("ank_mk38_military", "[media]/classes/trucks/ank_mk38_military.xml"),
        };

        var found = PakFileId.Find(
            items,
            item => item.Id,
            item => item.Path,
            "ank_mk38",
            "ank_mk38_civilian");

        Assert.NotNull(found);
        Assert.Equal("ank_mk38", found.Id);
    }

    [Fact]
    public void Matches_unique_file_id_suffix()
    {
        var items = new[]
        {
            new Item("azov_64131", "[media]/classes/trucks/azov_64131.xml"),
            new Item("azov_5319", "[media]/classes/trucks/azov_5319.xml"),
        };

        var found = PakFileId.Find(items, item => item.Id, "64131");

        Assert.NotNull(found);
        Assert.Equal("azov_64131", found.Id);
    }

    [Fact]
    public void Prefers_non_dlc_path_when_ids_collide()
    {
        var items = new[]
        {
            new Item("demo_truck", "[media]/_dlc/demo/classes/trucks/demo_truck.xml"),
            new Item("demo_truck", "[media]/classes/trucks/demo_truck.xml"),
        };

        var found = PakFileId.Find(
            items,
            item => item.Id,
            item => item.Path,
            "demo_truck");

        Assert.NotNull(found);
        Assert.DoesNotContain("/_dlc/", found.Path, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class TrailerStoreAvailabilityTests
{
    [Fact]
    public void Train_hitch_alone_is_not_store_ready()
    {
        const string xml =
            """
            <Truck>
              <TruckData />
              <GameData Price="18800">
                <InstallSocket Offset="(0; 0; 0)" Type="Train" />
              </GameData>
            </Truck>
            """;

        Assert.False(TrailerTuningService_IsStoreHitchReady(xml));
    }

    [Fact]
    public void Train_plus_Trailer_socket_is_store_ready()
    {
        const string xml =
            """
            <Truck>
              <TruckData />
              <GameData Price="18800" IsQuest="false">
                <InstallSocket Offset="(0; 0; 0)" Type="Train" />
                <InstallSocket Offset="(0; 0; 0)" Type="Trailer" />
              </GameData>
            </Truck>
            """;

        Assert.True(TrailerTuningService_IsStoreHitchReady(xml));
    }

    [Fact]
    public void Available_in_store_requires_quest_clear_and_compatible_hitch()
    {
        var trainLike = new TrailerTuningDefinition
        {
            EntryPath = "train.xml",
            TrailerId = "train",
            DisplayName = "Diesel Locomotive",
            HasGameData = true,
            IsQuest = false,
            HasStoreCompatibleHitch = false,
        };

        var unlocked = new TrailerTuningDefinition
        {
            EntryPath = "train.xml",
            TrailerId = "train",
            DisplayName = "Diesel Locomotive",
            HasGameData = true,
            IsQuest = false,
            HasStoreCompatibleHitch = true,
        };

        Assert.False(trainLike.IsAvailableInStore);
        Assert.True(unlocked.IsAvailableInStore);
    }

    [Fact]
    public void Undo_store_availability_removes_only_supplemental_Trailer_socket()
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

        var withStore = TrailerTuningService.EnsureStoreHitch(baseline);
        Assert.Contains("""Type="Trailer" """, withStore, StringComparison.Ordinal);
        Assert.Contains("""Type="Train" """, withStore, StringComparison.Ordinal);

        var undone = TrailerTuningService.RemoveSupplementalStoreHitch(withStore, baseline);
        Assert.DoesNotContain("""Type="Trailer" """, undone, StringComparison.Ordinal);
        Assert.Contains("""Type="Train" """, undone, StringComparison.Ordinal);
        Assert.False(TrailerTuningService.IsStoreHitchReady(undone));
    }

    [Fact]
    public void Undo_does_not_strip_vanilla_Trailer_beside_Train()
    {
        const string baseline =
            """
            <Truck>
              <TruckData />
              <GameData Price="18800">
                <InstallSocket Offset="(0; 0; 0)" Type="Train" />
                <InstallSocket Offset="(1; 0; 0)" Type="Trailer" />
              </GameData>
            </Truck>
            """;

        var undone = TrailerTuningService.RemoveSupplementalStoreHitch(baseline, baseline);
        Assert.Contains("""Type="Trailer" """, undone, StringComparison.Ordinal);
        Assert.Contains("""Type="Train" """, undone, StringComparison.Ordinal);
    }

    // InternalsVisibleTo bridge — keeps call sites readable without InternalsVisibleTo imports noise.
    private static bool TrailerTuningService_IsStoreHitchReady(string xml) =>
        TrailerTuningService.IsStoreHitchReady(xml);
}
