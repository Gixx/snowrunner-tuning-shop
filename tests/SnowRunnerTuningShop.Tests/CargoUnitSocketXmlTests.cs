using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class CargoUnitSocketXmlTests
{
    private const string BricksWithCrane = """
        <ModelBrand ClipCamera="false" DynamicModel="true">
        	<PhysicsModel>
        		<Body Mass="1000.0" />
        	</PhysicsModel>
        	<GameData PackSlotsNumber="1" LoadType="CargoBricks">
        		<CraneSocket Pos="(0.000; 0.988; 0.000)" />
            </GameData>
        </ModelBrand>
        """;

    private const string MediumLogVanilla = """
        <_templates Include="models" />
        <ModelBrand ClipCamera="false" DynamicModel="true">
        	<PhysicsModel>
        		<Body _template="MediumWeight" ModelFrame="BoneRoot" />
        	</PhysicsModel>
        	<GameData PackSlotsNumber="1" LoadType="CargoLogsMedium" ManualLoadSpawnOffsetY="2">
        		<LoadPoint Pos="(-2.0;0;0)" />
        		<LoadPoint Pos="(2.0;0;0)" />
        	</GameData>
        </ModelBrand>
        """;

    [Fact]
    public void IsDynamicLogCargoUnit_detects_logs_not_static_or_other_cargo()
    {
        Assert.True(CargoUnitSocketXml.IsDynamicLogCargoUnit(
            "[media]/_dlc/dlc_3/classes/models/cargo_unit_log_medium.xml"));
        Assert.True(CargoUnitSocketXml.IsDynamicLogCargoUnit(
            "[media]/_dlc/us_09/classes/models/cargo_unit_burnt_log_medium.xml"));
        Assert.False(CargoUnitSocketXml.IsDynamicLogCargoUnit(
            "[media]/_dlc/dlc_3/classes/models/cargo_unit_log_medium_static.xml"));
        Assert.False(CargoUnitSocketXml.IsDynamicLogCargoUnit(
            "[media]/classes/models/cargo_unit_bricks.xml"));
    }

    [Fact]
    public void EnsureWinchSocketsFromCrane_duplicates_crane_pos_as_winch()
    {
        var updated = CargoUnitSocketXml.EnsureWinchSocketsFromCrane(BricksWithCrane);

        Assert.True(CargoUnitSocketXml.HasWinchSocket(updated));
        Assert.True(CargoUnitSocketXml.HasMatchingWinchSocketsForCranes(updated));
        Assert.Contains("WinchSocket", updated, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0.988", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureWinchSocketsFromCrane_is_idempotent()
    {
        var once = CargoUnitSocketXml.EnsureWinchSocketsFromCrane(BricksWithCrane);
        var twice = CargoUnitSocketXml.EnsureWinchSocketsFromCrane(once);

        Assert.Equal(once, twice);
        Assert.Equal(1, CargoUnitSocketXml.CountWinchSockets(twice));
    }

    [Fact]
    public void EnsureLogCraneSockets_adds_three_points_for_medium_logs()
    {
        var updated = CargoUnitSocketXml.EnsureLogCraneSockets(MediumLogVanilla);

        Assert.Equal(3, CargoUnitSocketXml.CountCraneSockets(updated));
        Assert.Contains("2.4", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDesired_cranable_then_winchable_gives_both_on_logs()
    {
        var path = "[media]/_dlc/dlc_3/classes/models/cargo_unit_log_medium.xml";
        var updated = CargoUnitSocketXml.BuildDesired(
            MediumLogVanilla,
            path,
            cranableLogs: true,
            winchableCargo: true);

        Assert.Equal(3, CargoUnitSocketXml.CountCraneSockets(updated));
        Assert.Equal(3, CargoUnitSocketXml.CountWinchSockets(updated));
        Assert.True(CargoUnitSocketXml.HasMatchingWinchSocketsForCranes(updated));
    }

    [Fact]
    public void BuildDesired_both_off_returns_baseline()
    {
        var path = "[media]/classes/models/cargo_unit_bricks.xml";
        var updated = CargoUnitSocketXml.BuildDesired(
            BricksWithCrane,
            path,
            cranableLogs: false,
            winchableCargo: false);

        Assert.Equal(BricksWithCrane, updated);
    }
}
