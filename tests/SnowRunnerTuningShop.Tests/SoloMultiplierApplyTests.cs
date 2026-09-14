using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Winch;

namespace SnowRunnerTuningShop.Tests;

/// <summary>
/// Regression: applying a single non-baseline multiplier must rewrite XML
/// (Engine responsiveness previously discarded successful scales when <c>changed</c> stayed false).
/// </summary>
public sealed class SoloMultiplierApplyTests
{
    [Theory]
    [InlineData(2, 1, 1, 1, "Torque=\"200000\"")]
    [InlineData(1, 2, 1, 1, "FuelConsumption=\"10\"")]
    [InlineData(1, 1, 2, 1, "DamageCapacity=\"400\"")]
    [InlineData(1, 1, 1, 2, "EngineResponsiveness=\"0.08\"")]
    public void Engine_solo_slider_rewrites_target_attribute(
        double torque,
        double fuel,
        double damage,
        double responsiveness,
        string expectedFragment)
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyMultipliersToTextForTests(xml, torque, fuel, damage, responsiveness);

        Assert.Contains(expectedFragment, updated, StringComparison.Ordinal);
        if (torque == 1)
        {
            Assert.Contains("Torque=\"100000\"", updated, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Engine_damage_alone_scales_without_touching_torque()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyMultipliersToTextForTests(xml, 1, 1, 2, 1);

        Assert.Contains("DamageCapacity=\"400\"", updated, StringComparison.Ordinal);
        Assert.Contains("Torque=\"100000\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("EngineResponsiveness=", updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1, 1, "FuelConsumption=\"3\"")]
    [InlineData(1, 2, 1, "IdleFuelModifier=\"0.6\"")]
    [InlineData(1, 1, 2, "AWDConsumptionModifier=\"2\"")]
    public void Gearbox_solo_slider_rewrites_target_attribute(
        double fuel,
        double idle,
        double awd,
        string expectedFragment)
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" FuelConsumption="1.5" IdleFuelModifier="0.3" AWDConsumptionModifier="1" />
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyMultipliersToTextForTests(xml, fuel, idle, awd);

        Assert.Contains(expectedFragment, updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1, 1, 1, "Height=\"0.2\"")]
    [InlineData(1, 2, 1, 1, "Strength=\"1\"")]
    [InlineData(1, 1, 2, 1, "Damping=\"0.8\"")]
    [InlineData(1, 1, 1, 2, "DamageCapacity=\"160\"")]
    public void Suspension_solo_slider_rewrites_target_attribute(
        double height,
        double strength,
        double damping,
        double damage,
        string expectedFragment)
    {
        const string xml =
            """
            <SuspensionSetVariants>
              <SuspensionSet Name="s1" DamageCapacity="80">
                <Suspension WheelType="front" Height="0.1" Strength="0.5" Damping="0.4" />
                <Suspension WheelType="rear" Height="0.1" Strength="0.5" Damping="0.4" />
              </SuspensionSet>
            </SuspensionSetVariants>
            """;

        var updated = SuspensionService.ApplyMultipliersToTextForTests(xml, height, strength, damping, damage);

        Assert.Contains(expectedFragment, updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1, 1, "BodyFrictionAsphalt=\"4\"")]
    [InlineData(1, 2, 1, "BodyFriction=\"2\"")]
    [InlineData(1, 1, 2, "SubstanceFriction=\"4\"")]
    public void Tire_solo_friction_slider_rewrites_target_attribute(
        double onRoad,
        double offRoad,
        double mud,
        string expectedFragment)
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTires>
                <TruckTire Name="tire">
                  <WheelFriction BodyFrictionAsphalt="2" BodyFriction="1" SubstanceFriction="2" />
                  <GameData Price="100" />
                </TruckTire>
              </TruckTires>
            </TruckWheels>
            """;

        var updated = TireService.ApplyMultipliersToTextForTests(xml, onRoad, offRoad, mud);

        Assert.Contains(expectedFragment, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Tire_ignore_ice_alone_injects_IsIgnoreIce()
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTires>
                <TruckTire Name="tire">
                  <WheelFriction BodyFrictionAsphalt="2" BodyFriction="1" SubstanceFriction="2" />
                  <GameData Price="100" />
                </TruckTire>
              </TruckTires>
            </TruckWheels>
            """;

        var updated = TireService.ApplyMultipliersToTextForTests(xml, 1, 1, 1, ignoreIceForAll: true);

        Assert.Contains("IsIgnoreIce=\"true\"", updated, StringComparison.Ordinal);
        Assert.Contains("BodyFrictionAsphalt=\"2\"", updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2, 1, false, "Length=\"28\"")]
    [InlineData(1, 2, false, "StrengthMult=\"2.0\"")]
    public void Winch_solo_slider_rewrites_target_attribute(
        double length,
        double strength,
        bool autonomous,
        string expectedFragment)
    {
        const string xml =
            """
            <WinchVariants>
              <Winch Name="w1" Length="14" StrengthMult="1.0" IsEngineIgnitionRequired="true" />
            </WinchVariants>
            """;

        var updated = WinchService.ApplyMultipliersToTextForTests(xml, length, strength, autonomous);

        Assert.Contains(expectedFragment, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Winch_autonomous_alone_clears_engine_requirement()
    {
        const string xml =
            """
            <WinchVariants>
              <Winch Name="w1" Length="14" StrengthMult="1.0" IsEngineIgnitionRequired="true" />
            </WinchVariants>
            """;

        var updated = WinchService.ApplyMultipliersToTextForTests(xml, 1, 1, forceAutonomousAll: true);

        Assert.Contains("IsEngineIgnitionRequired=\"false\"", updated, StringComparison.Ordinal);
        Assert.Contains("Length=\"14\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Crane_arm_force_alone_scales_motors()
    {
        const string xml =
            """
            <TruckAddon>
              <PhysicsModel>
                <Body>
                  <Constraint Name="Crane" Type="Hinge">
                    <Motor Force="80000" Tau="0.5" Type="Position" />
                  </Constraint>
                </Body>
              </PhysicsModel>
              <ControlledIK CoeffEndMovementSpeedOY="1.0" CoeffEndMovementSpeedOYWithLoad="0.5"
                CoeffEndMovementSpeedXZ="1.0" CoeffEndMovementSpeedXZWithLoad="0.5" />
              <GameData Price="5700">
                <AddonType Name="Crane" />
              </GameData>
            </TruckAddon>
            """;

        var updated = CraneService.ApplyMultipliersToTextForTests(xml, armForceMultiplier: 2, movementSpeedMultiplier: 1);

        Assert.Contains("Force=\"160000\"", updated, StringComparison.Ordinal);
        Assert.Contains("CoeffEndMovementSpeedOY=\"1.0\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Crane_movement_speed_alone_scales_ik()
    {
        const string xml =
            """
            <TruckAddon>
              <PhysicsModel>
                <Body>
                  <Constraint Name="Crane" Type="Hinge">
                    <Motor Force="80000" Tau="0.5" Type="Position" />
                  </Constraint>
                </Body>
              </PhysicsModel>
              <ControlledIK CoeffEndMovementSpeedOY="1.0" CoeffEndMovementSpeedOYWithLoad="0.5"
                CoeffEndMovementSpeedXZ="1.0" CoeffEndMovementSpeedXZWithLoad="0.5" />
              <GameData Price="5700">
                <AddonType Name="Crane" />
              </GameData>
            </TruckAddon>
            """;

        var updated = CraneService.ApplyMultipliersToTextForTests(xml, armForceMultiplier: 1, movementSpeedMultiplier: 2);

        Assert.Contains("CoeffEndMovementSpeedOY=\"2.0\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"80000\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_fuel_alone_does_not_touch_responsiveness_or_inject_price()
    {
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="100" />
              <GameData UnlockByRank="1" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: 2,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 1,
            priceMultiplier: 1);

        Assert.Contains("FuelCapacity=\"200\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("Responsiveness=", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("Price=", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_responsiveness_alone_scales_existing_attribute()
    {
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="100" Responsiveness="0.4" />
              <GameData Price="5000" UnlockByRank="1" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: 1,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 2,
            priceMultiplier: 1);

        Assert.Contains("Responsiveness=\"0.8\"", updated, StringComparison.Ordinal);
        Assert.Contains("FuelCapacity=\"100\"", updated, StringComparison.Ordinal);
        Assert.Contains("Price=\"5000\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_responsiveness_one_writes_float_one_point_zero()
    {
        // Issue #7: Responsiveness="1" (integer form) made trucks vanish from store/map.
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="100" Responsiveness="0.4" />
              <GameData Price="5000" UnlockByRank="1" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: 1,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 2.5,
            priceMultiplier: 1);

        Assert.Contains("Responsiveness=\"1.0\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("Responsiveness=\"1\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_responsiveness_alone_injects_scaled_default_when_missing()
    {
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="100" />
              <GameData Price="5000" UnlockByRank="1" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: 1,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 2,
            priceMultiplier: 1);

        Assert.Contains("Responsiveness=\"0.8\"", updated, StringComparison.Ordinal);
        Assert.Contains("FuelCapacity=\"100\"", updated, StringComparison.Ordinal);
        Assert.Contains("Price=\"5000\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_all_baseline_leaves_xml_unchanged()
    {
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="100" />
              <GameData UnlockByRank="1" />
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: 1,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 1,
            priceMultiplier: 1);

        Assert.Equal(xml.ReplaceLineEndings("\n"), updated.ReplaceLineEndings("\n"));
    }
}
