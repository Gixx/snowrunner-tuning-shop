using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class PartDamageCapacityTests
{
    [Fact]
    public void Gearbox_save_updates_DamageCapacity()
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" DamageCapacity="100" FuelConsumption="1.5" IdleFuelModifier="0.2"
                       AWDConsumptionModifier="1.0">
                <Gear AngVel="4.0" />
                <HighGear AngVel="8.0" />
                <ReverseGear AngVel="2.0" />
                <GameData Price="1000" />
              </Gearbox>
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyGearboxUpdatesToTextForTests(
            xml,
            gearboxName: "g1",
            price: 1000,
            fuelConsumption: 1.5,
            idleFuelModifier: 0.2,
            awdConsumptionModifier: 1.0,
            maxGearAngVel: 4.0,
            highGearAngVel: 8.0,
            reverseGearAngVel: 2.0,
            damageCapacity: 250);

        Assert.Contains("""DamageCapacity="250" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Tires_TruckWheels_DamageCapacity_is_shared_across_tires_in_file()
    {
        const string xml =
            """
            <TruckWheels DamageCapacity="50" Radius="1">
              <TruckTire Name="t1">
                <WheelFriction BodyFriction="1" BodyFrictionAsphalt="2" SubstanceFriction="3" />
                <GameData Price="100" />
              </TruckTire>
              <TruckTire Name="t2">
                <WheelFriction BodyFriction="1" BodyFrictionAsphalt="2" SubstanceFriction="3" />
                <GameData Price="100" />
              </TruckTire>
            </TruckWheels>
            """;

        // No public parse helper — exercise via save path with temporary write is heavy.
        // Instead verify TruckWheels rewrite through TireService internals by saving via
        // Apply pattern: construct definitions and call Save is pak-bound.
        // Use reflection-free approach: Gearbox-style isn't available; use PartXmlHelpers + service
        // ApplyUpdates through public Save requires pak. Keep a focused regex assertion via
        // TireService private path isn't exposed — add internal test hook below.

        var updated = TireService.ApplyTireDamageCapacityForTests(xml, damageCapacity: 80);
        Assert.Contains("""DamageCapacity="80" """, updated, StringComparison.Ordinal);
        Assert.Contains("Name=\"t1\"", updated, StringComparison.Ordinal);
        Assert.Contains("Name=\"t2\"", updated, StringComparison.Ordinal);
    }
}
