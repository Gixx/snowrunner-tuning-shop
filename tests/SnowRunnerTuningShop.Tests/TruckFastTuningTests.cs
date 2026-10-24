using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class TruckFastTuningTests
{
    private const string CabinBaseline =
        """
        <Truck>
          <PhysicsModel Mesh="trucks/demo">
            <Body
              CenterOfMassOffset="(-0.8; 0; 0)"
              ImpactType="Truck"
              Mass="3000"
              ModelFrame="BoneChassis_cdt"
            >
              <Body
                CenterOfMassOffset="(0.4; 0.8; 0)"
                ImpactType="Truck"
                Mass="2250"
                ModelFrame="BoneCabin_cdt"
              >
                <Constraint Type="Fixed" />
              </Body>
            </Body>
          </PhysicsModel>
          <TruckData SteerSpeed="0.025" BackSteerSpeed="0.015">
            <FirstAxle Location="front" SteeringAngle="30" Torque="default" />
            <SecondAxle Location="front" SteeringAngle="20" Torque="default" />
            <CompatibleWheels Scale="0.47" Type="wheels_demo" />
            <CompatibleWheels Scale="0.52" Type="wheels_demo" />
            <CompatibleWheels Scale="0.57" Type="wheels_demo" />
          </TruckData>
          <GameData Price="1000" />
        </Truck>
        """;

    private const string ArticulatedNoFrontSteer =
        """
        <Truck>
          <TruckData SteerSpeed="0.025" BackSteerSpeed="0.015">
            <FrontWheel Location="front" Torque="default" />
            <RearWheel Location="rear" Torque="default" />
          </TruckData>
          <GameData Price="1000" />
        </Truck>
        """;

    [Fact]
    public void Lower_com_halves_cabin_y_from_baseline()
    {
        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            CabinBaseline,
            CabinBaseline,
            new TruckFastTuningOptions { LowerCenterOfMass = true });

        Assert.True(VehicleCabinCenterOfMassXml.TryReadCabinY(updated, out var y));
        Assert.Equal(0.4, y);
        Assert.True(XmlRewriteSafety.TryValidate(updated, out var failure), failure);
    }

    [Fact]
    public void Lower_com_skips_when_working_already_customized()
    {
        var customized = VehicleCabinCenterOfMassXml.ApplyCabinY(CabinBaseline, -0.2);
        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            customized,
            CabinBaseline,
            new TruckFastTuningOptions { LowerCenterOfMass = true });

        Assert.Equal(customized, updated);
    }

    [Fact]
    public void Better_turning_sets_45_and_38_on_front_steered_axles()
    {
        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            CabinBaseline,
            CabinBaseline,
            new TruckFastTuningOptions { BetterTurningCurve = true });

        var axles = TruckSteerXml.ParseSteerAxles(updated, CabinBaseline);
        var fronts = axles
            .Where(a => a.HadSteerInBaseline && a.BaselineAngle is > 0)
            .OrderBy(a => a.DisplayOrder)
            .ToList();
        Assert.Equal(2, fronts.Count);
        Assert.Equal(45, fronts[0].Angle);
        Assert.Equal(38, fronts[1].Angle);
    }

    [Fact]
    public void Better_turning_skips_articulated_without_front_steer()
    {
        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            ArticulatedNoFrontSteer,
            ArticulatedNoFrontSteer,
            new TruckFastTuningOptions { BetterTurningCurve = true });

        Assert.Equal(ArticulatedNoFrontSteer, updated);
    }

    [Fact]
    public void Better_turning_preserves_custom_first_axle_angle()
    {
        var axles = TruckSteerXml.ParseSteerAxles(CabinBaseline, CabinBaseline);
        axles.Single(a => a.Tag == "FirstAxle").Angle = 50;
        var customized = TruckSteerXml.ApplySteerAxles(CabinBaseline, axles);

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            customized,
            CabinBaseline,
            new TruckFastTuningOptions { BetterTurningCurve = true });

        var result = TruckSteerXml.ParseSteerAxles(updated, CabinBaseline);
        Assert.Equal(50, result.Single(a => a.Tag == "FirstAxle").Angle);
        Assert.Equal(38, result.Single(a => a.Tag == "SecondAxle").Angle);
    }

    [Fact]
    public void Faster_steering_sets_0_05_when_at_baseline()
    {
        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            CabinBaseline,
            CabinBaseline,
            new TruckFastTuningOptions { FasterSteering = true });

        Assert.Contains("SteerSpeed=\"0.05\"", updated, StringComparison.Ordinal);
        Assert.Contains("BackSteerSpeed=\"0.05\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Faster_steering_skips_custom_steer_speed()
    {
        var customized = CabinBaseline.Replace(
            "SteerSpeed=\"0.025\"",
            "SteerSpeed=\"0.08\"",
            StringComparison.Ordinal);

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            customized,
            CabinBaseline,
            new TruckFastTuningOptions { FasterSteering = true });

        Assert.Contains("SteerSpeed=\"0.08\"", updated, StringComparison.Ordinal);
        Assert.Contains("BackSteerSpeed=\"0.05\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Larger_tires_enables_plus_one_and_disables_restriction()
    {
        const string withRestriction =
            """
            <Truck>
              <TruckData>
                <CompatibleWheels Scale="0.47" Type="wheels_demo" />
                <CompatibleWheels Scale="0.52" Type="wheels_demo" />
                <CompatibleWheels Scale="0.57" Type="wheels_demo" />
                <SuspensionSocket MaxWheelRadiusWithoutSuspension="0.6" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            withRestriction,
            withRestriction,
            new TruckFastTuningOptions { LargerTires = true });

        var snapshot = TruckCompatibleWheelsService.BuildSizesSnapshotForTests(updated, withRestriction);
        Assert.Contains(snapshot.Sizes, s => !s.IsVanilla && s.IsEnabled && Math.Abs(s.Scale - 0.62) < 1e-6);
        Assert.True(snapshot.SuspensionRestrictionDisabled);
    }

    [Fact]
    public void Always_on_awd_forces_default_torque_when_drive_still_at_baseline()
    {
        const string rwd =
            """
            <Truck>
              <TruckData>
                <FrontWheel Location="front" Torque="none" />
                <RearWheel Location="rear" Torque="full" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            rwd,
            rwd,
            new TruckFastTuningOptions { AlwaysOnAwd = true });

        Assert.Contains("Torque=\"default\"", updated, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Torque=\"none\"", updated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Always_on_awd_skips_when_drive_already_customized()
    {
        const string baseline =
            """
            <Truck>
              <TruckData>
                <FrontWheel Location="front" Torque="none" />
                <RearWheel Location="rear" Torque="full" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;
        const string customized =
            """
            <Truck>
              <TruckData>
                <FrontWheel Location="front" Torque="full" />
                <RearWheel Location="rear" Torque="full" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            customized,
            baseline,
            new TruckFastTuningOptions { AlwaysOnAwd = true });

        Assert.Equal(customized, updated);
    }

    [Fact]
    public void Always_on_diff_lock_sets_type_when_still_at_baseline()
    {
        const string baseline =
            """
            <Truck>
              <TruckData DiffLockType="Installed">
                <FrontWheel Location="front" Torque="default" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            baseline,
            baseline,
            new TruckFastTuningOptions { AlwaysOnDiffLock = true });

        Assert.Contains("DiffLockType=\"Always\"", updated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Larger_tires_skips_when_extras_already_enabled()
    {
        const string baseline =
            """
            <Truck>
              <TruckData>
                <CompatibleWheels Scale="0.47" Type="wheels_demo" />
                <CompatibleWheels Scale="0.52" Type="wheels_demo" />
                <CompatibleWheels Scale="0.57" Type="wheels_demo" />
              </TruckData>
              <GameData Price="1000" />
            </Truck>
            """;
        var withPlusTwo = TruckCompatibleWheelsService.ApplySizesToTextForTests(
            baseline,
            baseline,
            [0.62, 0.67],
            offsets: [],
            disableSuspensionRestriction: false);

        var updated = TruckTuningService.ApplyFastTuningToTextForTests(
            withPlusTwo,
            baseline,
            new TruckFastTuningOptions { LargerTires = true });

        Assert.Equal(withPlusTwo, updated);
    }
}
