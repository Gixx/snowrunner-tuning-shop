using SnowRunnerTuningShop.Core.AddonCapacity;
using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Suspension;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Winch;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

/// <summary>
/// Level A: CI-safe rewrite matrix — integer / small-fraction / large multipliers,
/// plus structural XML checks (catches misplaced <c>/</c> before attributes).
/// </summary>
public sealed class XmlRewriteMatrixTests
{
    /// <summary>1 (baseline skip), ⅓ (fraction), 2× (scale-up).</summary>
    public static TheoryData<double> MultiplierShapes { get; } =
    [
        TuningMultiplierPresets.Values[TuningMultiplierPresets.BaselineIndex],
        TuningMultiplierPresets.Values[2], // 1/3
        TuningMultiplierPresets.Values[TuningMultiplierPresets.TwoTimesIndex],
    ];

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Engine_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" />
              <GameData Price="1000" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyMultipliersToTextForTests(xml, m, m, m, m);
        AssertSafe(updated, $"engine multipliers m={m}");
        if (m == 2)
        {
            Assert.Contains("EngineResponsiveness=\"0.08\"", updated, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.01)]
    [InlineData(1)]
    [InlineData(10)]
    public void Engine_max_delta_ang_vel_shapes_stay_safe(double maxDelta)
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" MaxDeltaAngVel="0.01" />
              <GameData Price="1500" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyEngineUpdatesToTextForTests(
            xml,
            engineName: "e1",
            price: 1500,
            torque: 100000,
            fuelConsumption: 5.0,
            damageCapacity: 200,
            engineResponsiveness: 0.04,
            maxDeltaAngVel: maxDelta);

        AssertSafe(updated, $"engine MaxDeltaAngVel={maxDelta}");
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Gearbox_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" FuelConsumption="1.5" IdleFuelModifier="0.3" AWDConsumptionModifier="1.0"
                       DamageCapacity="180">
                <GameData Price="900" />
                <ReverseGear AngVel="1.5" FuelModifier="0.9" />
                <HighGear AngVel="8" FuelModifier="1.0" />
                <Gear AngVel="2.0" FuelModifier="1.0" />
              </Gearbox>
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyMultipliersToTextForTests(xml, m, m, m);
        AssertSafe(updated, $"gearbox multipliers m={m}");
    }

    [Theory]
    [InlineData(0.1)]
    [InlineData(1)]
    [InlineData(32)]
    public void Gearbox_angvel_shapes_stay_safe(double angVel)
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" FuelConsumption="1.5" IdleFuelModifier="0.3" AWDConsumptionModifier="1.0"
                       DamageCapacity="180">
                <GameData Price="900" />
                <ReverseGear AngVel="1.5" FuelModifier="0.9" />
                <HighGear AngVel="8" FuelModifier="1.0" />
                <Gear AngVel="2.0" FuelModifier="1.0" />
              </Gearbox>
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyGearboxUpdatesToTextForTests(
            xml,
            gearboxName: "g1",
            price: 900,
            fuelConsumption: 1.5,
            idleFuelModifier: 0.3,
            awdConsumptionModifier: 1.0,
            maxGearAngVel: angVel,
            highGearAngVel: angVel,
            reverseGearAngVel: angVel,
            damageCapacity: 180);

        AssertSafe(updated, $"gearbox AngVel={angVel}");
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Suspension_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <SuspensionSetVariants>
              <SuspensionSet Name="s1" CriticalDamageThreshold="0.5" DamageCapacity="200">
                <Suspension Height="0.2" Strength="0.05" Damping="0.3" WheelType="front" />
                <Suspension Height="0.1" Strength="0.03" Damping="0.3" WheelType="rear" />
                <GameData Price="800" />
              </SuspensionSet>
            </SuspensionSetVariants>
            """;

        var updated = SuspensionService.ApplyMultipliersToTextForTests(xml, m, m, m, m);
        AssertSafe(updated, $"suspension multipliers m={m}");
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Tire_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTire>
                <WheelFriction BodyFriction="1.0" SubstanceFriction="1.5" />
                <GameData Price="400" />
              </TruckTire>
            </TruckWheels>
            """;

        var updated = TireService.ApplyMultipliersToTextForTests(xml, m, m, m);
        AssertSafe(updated, $"tire multipliers m={m}");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(200)]
    public void Tire_damage_shapes_stay_safe(double damage)
    {
        const string xml =
            """
            <TruckWheels DamageCapacity="100">
              <TruckTire>
                <WheelFriction BodyFriction="1.0" SubstanceFriction="1.5" />
              </TruckTire>
            </TruckWheels>
            """;

        var updated = TireService.ApplyTireDamageCapacityForTests(xml, damage);
        AssertSafe(updated, $"tire damage={damage}");
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Winch_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <WinchVariants>
              <Winch Name="w1" Length="14" StrengthMult="1.0" IsEngineIgnitionRequired="true">
                <GameData Price="500" />
              </Winch>
            </WinchVariants>
            """;

        var updated = WinchService.ApplyMultipliersToTextForTests(xml, m, m);
        AssertSafe(updated, $"winch multipliers m={m}");
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Crane_multipliers_stay_structurally_safe(double m)
    {
        var updated = CraneService.ApplyMultipliersToTextForTests(CraneFixture, m, m);
        AssertSafe(updated, $"crane multipliers m={m}");
    }

    private const string CraneFixture =
        """
        <TruckAddon>
          <PhysicsModel>
            <Body>
              <Constraint Name="Anchor" Type="Prismatic">
                <Motor Force="4000" Type="Position" />
              </Constraint>
              <Constraint Name="Crane" Type="Hinge">
                <Motor Force="80000" Tau="0.5" Type="Position" />
              </Constraint>
              <Constraint Name="Arm1" Type="Hinge">
                <Motor Force="400000" Tau="0.5" Type="Position" />
              </Constraint>
              <Constraint Name="ArmExt" Type="Prismatic">
                <Motor Force="60000" Tau="0.9" Type="Position" />
              </Constraint>
            </Body>
          </PhysicsModel>
          <ControlledIK
            CoeffEndMovementSpeedOY="1.0"
            CoeffEndMovementSpeedOYWithLoad="0.5"
            CoeffEndMovementSpeedXZ="1.0"
            CoeffEndMovementSpeedXZWithLoad="0.5">
            <Chain EndOffset="(0; 0; 0)" ModelFrames="BoneRoot_cdt" />
          </ControlledIK>
          <GameData Price="5700">
            <UiDesc UiName="UI_ADDON_MINICRANE_1_NAME" />
            <AddonType Name="Crane" />
          </GameData>
        </TruckAddon>
        """;

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Truck_global_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <Truck>
              <TruckData FuelCapacity="200" Responsiveness="0.4" SteerSpeed="0.025" BackSteerSpeed="0.015">
                <RearWheel Location="rear" Torque="default" />
                <FrontWheel Location="front" SteeringAngle="40" Torque="default" />
                <Wheels>
                  <Wheel _template="FrontWheel" Pos="(2; 0.5; 1)" />
                  <Wheel _template="RearWheel" Pos="(-2; 0.5; 1)" />
                </Wheels>
              </TruckData>
              <GameData Price="25000" />
              <PhysicsModel>
                <Body Mass="5000" ImpactType="Truck" />
              </PhysicsModel>
            </Truck>
            """;

        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            xml,
            fuelMultiplier: m,
            frontSteerMode: m > 1 ? TruckFrontSteerGlobalMode.Maximum : TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: m,
            priceMultiplier: m,
            massMultiplier: Math.Min(m, 2),
            rearSteerMode: TruckRearSteerGlobalMode.Baseline);

        AssertSafe(updated, $"truck globals m={m}");
    }

    [Theory]
    [InlineData(-10)]
    [InlineData(-40)]
    [InlineData(-60)]
    public void Truck_added_rear_steer_on_self_closing_tag_stays_safe(double angle)
    {
        const string xml =
            """
            <Truck>
              <TruckData>
                <RearWheel ConnectedToHandbrake="true" Location="rear" Torque="default" />
                <FrontWheel Location="front" SteeringAngle="40" Torque="connectable" />
              </TruckData>
            </Truck>
            """;

        var axles = TruckSteerXml.ParseSteerAxles(xml, xml);
        axles.Single(a => a.Tag == "RearWheel").Angle = angle;
        var updated = TruckSteerXml.ApplySteerAxles(xml, axles);

        AssertSafe(updated, $"added rear steer {angle}");
        Assert.DoesNotContain("/ SteeringAngle", updated, StringComparison.Ordinal);
        Assert.Matches($@"RearWheel[^>]*SteeringAngle=""{angle}""", updated);
    }

    [Theory]
    [MemberData(nameof(MultiplierShapes))]
    public void Addon_capacity_multipliers_stay_structurally_safe(double m)
    {
        const string xml =
            """
            <TruckAddon>
              <TruckData FuelCapacity="80" WaterCapacity="40" RepairsCapacity="200" WheelRepairsCapacity="2" />
              <GameData Price="3000" />
            </TruckAddon>
            """;

        var updated = AddonCapacityService.ApplyGlobalMultipliersToTextForTests(xml, m, m, m, m, m);
        AssertSafe(updated, $"addon capacity m={m}");
    }

    [Fact]
    public void Engine_sets_max_selection_stays_structurally_safe()
    {
        const string truckXml =
            """
            <Truck>
              <TruckData>
                <EngineSocket Type="e_us_truck_old_loadstar, e_us_truck_old_scout" Default="us_truck_old_loadstar_engine_0" />
              </TruckData>
            </Truck>
            """;

        var catalog = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["e_us_truck_old_loadstar"] = ["us_truck_old_loadstar_engine_0", "us_truck_old_loadstar_engine_1"],
            ["e_us_truck_old_scout"] = ["us_scout_engine_0"],
            ["e_us_truck_modern"] = ["us_modern_0", "us_modern_1", "us_modern_2"],
        };

        var selected = catalog.Keys.ToArray(); // max list
        var updated = TruckEngineSetsService.ApplyEngineSocketSetsForTests(truckXml, catalog, selected);
        AssertSafe(updated, "engine sets max selection");
        Assert.Contains("e_us_truck_modern", updated, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Safety_helper_rejects_slash_before_attribute()
    {
        const string broken =
            """
            <RearWheel Location="rear" Torque="default" / SteeringAngle="-40">
            """;

        Assert.False(XmlRewriteSafety.TryValidate(broken, out var failure));
        Assert.Contains("Misplaced self-close", failure, StringComparison.Ordinal);
    }

    private static void AssertSafe(string xml, string context)
    {
        Assert.True(
            XmlRewriteSafety.TryValidate(xml, out var failure),
            $"{context}: {failure}");
    }
}
