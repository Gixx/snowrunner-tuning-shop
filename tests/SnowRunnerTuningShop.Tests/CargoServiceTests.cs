using SnowRunnerTuningShop.Core.Cargo;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public class CargoServiceTests
{
    private const string LogsCargoXml =
        """
        <TruckAddon>
        	<PhysicsModel Mesh="trucks/cargo/cargo_logs">
        		<NetSync Legacy="false" />
        		<Body
        			CenterOfMassOffset="(0; -0.3; 0)"
        			Friction="0.1"
        			ImpactType="Foliage"
        			Mass="4000"
        			ModelFrame="BoneRoot_cdt"
        		>
        			<Constraint Type="Fixed" />
        			<Body AngularDamping="3.0" Mass="50" ModelFrame="BoneShieldFront_cdt">
        				<Constraint Type="Fixed" />
        			</Body>
        			<Body ImpactType="Truck" Mass="4000" ModelFrame="BoneRear_cdt">
        				<Constraint ExplicitParentFrame="0" Type="Fixed" />
        			</Body>
        		</Body>
        	</PhysicsModel>
        	<GameData>
        		<InstallSlot CargoLength="1" CargoType="CargoLogsMedium" ManualLoads="3" />
        	</GameData>
        </TruckAddon>
        """;

    private const string NoComCargoXml =
        """
        <TruckAddon _noinherit="true">
        	<PhysicsModel Mesh="trucks/cargo/cargo_ba20">
        		<NetSync Legacy="false" />
        		<Body Mass="2000" ModelFrame="BoneRoot_cdt">
        			<Constraint ExplicitParentFrame="0" Type="Fixed" />
        			<Body Mass="1000" ModelFrame="BoneCargo_1_cdt">
        				<Constraint ExplicitParentFrame="1" Type="Fixed" />
        			</Body>
        		</Body>
        	</PhysicsModel>
        	<GameData>
        		<InstallSlot CargoLength="2" CargoType="CargoBA20" CargoValue="1" />
        	</GameData>
        </TruckAddon>
        """;

    [Fact]
    public void IsCargoXmlEntry_requires_cargo_folder()
    {
        Assert.True(CargoService.IsCargoXmlEntry(
            "[media]/classes/trucks/cargo/cargo_logs.xml"));
        Assert.False(CargoService.IsCargoXmlEntry(
            "[media]/classes/trucks/addons/frame_addon.xml"));
        Assert.False(CargoService.IsCargoXmlEntry(
            "[media]/classes/trucks/trailers/trailer.xml"));
    }

    [Fact]
    public void Global_mass_scales_all_body_masses()
    {
        var updated = CargoService.ApplyGlobalToTextForTests(
            LogsCargoXml,
            massMultiplier: 2.0,
            centerOfMassBiasIndex: CargoService.ComBiasBaselineIndex);

        Assert.Contains("Mass=\"8000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"100\"", updated, StringComparison.Ordinal);
        Assert.Contains("CenterOfMassOffset=\"(0; -0.3; 0)\"", updated, StringComparison.Ordinal);
        Assert.Contains("""Friction="0.1""", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_com_low_lowers_y_and_skips_when_missing()
    {
        var withCom = CargoService.ApplyGlobalToTextForTests(
            LogsCargoXml,
            massMultiplier: 1.0,
            centerOfMassBiasIndex: CargoService.ComBiasLowIndex);

        Assert.True(CargoRootBodyXml.TryReadCenterOfMassY(withCom, out var y));
        Assert.True(y < -0.3);

        var withoutCom = CargoService.ApplyGlobalToTextForTests(
            NoComCargoXml,
            massMultiplier: 1.0,
            centerOfMassBiasIndex: CargoService.ComBiasLowIndex);

        Assert.False(CargoRootBodyXml.TryReadCenterOfMassY(withoutCom, out _));
        Assert.Contains("""Mass="2000""", withoutCom, StringComparison.Ordinal);
        Assert.Contains("""Mass="1000""", withoutCom, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyTotalMass_rescales_nested_bodies_proportionally()
    {
        Assert.True(VehiclePhysicsMassXml.TryReadTotalMass(LogsCargoXml, out var total));
        Assert.Equal(8050, total);

        var updated = VehiclePhysicsMassXml.ApplyTotalMass(LogsCargoXml, 4025);
        Assert.True(VehiclePhysicsMassXml.TryReadTotalMass(updated, out var next));
        Assert.Equal(4025, next);
        Assert.Contains("""Mass="2000""", updated, StringComparison.Ordinal);
        Assert.Contains("""Mass="25""", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Mass_slider_range_includes_one_tenth_through_two_times()
    {
        Assert.Equal(0.1, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.MinimumIndex));
        Assert.Equal(1.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.BaselineIndex));
        Assert.Equal(2.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.TwoTimesIndex));
        Assert.Equal("1/10", TuningMultiplierPresets.GetLabel(0));
    }
}
