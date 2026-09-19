using SnowRunnerTuningShop.Core.Trucks;

namespace SnowRunnerTuningShop.Tests;

public sealed class TruckEngineSetsServiceTests
{
    private const string TruckXml =
        """
        <Truck>
          <TruckData>
            <EngineSocket Default="ru_special_engine_0" Type="e_ru_special" />
          </TruckData>
        </Truck>
        """;

    private static readonly Dictionary<string, IReadOnlyList<string>> Catalog =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["e_ru_special"] = new[] { "ru_special_engine_0", "ru_special_engine_1" },
            ["e_us_truck_old"] = new[] { "us_truck_old_engine_0", "us_truck_old_engine_1" },
            ["e_us_truck_old_gmc8000"] = new[] { "us_truck_old_engine_gmc8000" },
        };

    [Fact]
    public void ParseEngineSocket_ReadsTypeListAndDefault()
    {
        var (sets, defaultEngine) = TruckEngineSetsService.ParseEngineSocketForTests(
            """
            <EngineSocket Default="us_truck_old_engine_0" Type="e_us_truck_old, e_us_truck_old_gmc8000" />
            """);

        Assert.Equal(["e_us_truck_old", "e_us_truck_old_gmc8000"], sets);
        Assert.Equal("us_truck_old_engine_0", defaultEngine);
    }

    [Fact]
    public void Apply_AddsSecondSet_KeepsValidDefault()
    {
        var updated = TruckEngineSetsService.ApplyEngineSocketSetsForTests(
            TruckXml,
            Catalog,
            ["e_ru_special", "e_us_truck_old"]);

        Assert.Contains(
            """Type="e_ru_special, e_us_truck_old""",
            updated,
            StringComparison.Ordinal);
        Assert.Contains(
            """Default="ru_special_engine_0""",
            updated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Apply_RemovingDefaultSet_PicksFirstEngineOfFirstSet()
    {
        var updated = TruckEngineSetsService.ApplyEngineSocketSetsForTests(
            TruckXml,
            Catalog,
            ["e_us_truck_old_gmc8000"]);

        Assert.Contains(
            """Type="e_us_truck_old_gmc8000""",
            updated,
            StringComparison.Ordinal);
        Assert.Contains(
            """Default="us_truck_old_engine_gmc8000""",
            updated,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractEngineLabels_UsesUiNameWhenStringsProvided()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="ru_special_engine_0" Torque="1">
                <GameData>
                  <UiDesc UiName="UI_ENGINE_TEST" />
                </GameData>
              </Engine>
            </EngineVariants>
            """;

        var labels = TruckEngineSetsService.ExtractEngineLabelsForTests(
            xml,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["UI_ENGINE_TEST"] = "Special Engine Mk0",
            });

        Assert.Equal(["Special Engine Mk0"], labels);
    }

    [Fact]
    public void Apply_EmptySelection_Throws()
    {
        Assert.Throws<InvalidOperationException>(() =>
            TruckEngineSetsService.ApplyEngineSocketSetsForTests(TruckXml, Catalog, []));
    }
}
