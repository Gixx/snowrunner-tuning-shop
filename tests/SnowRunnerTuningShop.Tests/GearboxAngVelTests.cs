using SnowRunnerTuningShop.Core.Gearbox;

namespace SnowRunnerTuningShop.Tests;

public sealed class GearboxAngVelTests
{
    private const string SampleXml =
        """
        <GearboxVariants>
          <Gearbox Name="g1" FuelConsumption="1.5" IdleFuelModifier="0.3" AWDConsumptionModifier="1.0">
            <GameData Price="1900" />
            <ReverseGear AngVel="1.0" FuelModifier="0.9" />
            <HighGear AngVel="8.0" FuelModifier="0.9" />
            <Gear AngVel="2.0" FuelModifier="1.0" />
            <Gear AngVel="4.0" FuelModifier="1.0" />
            <Gear AngVel="10.0" FuelModifier="1.0" />
          </Gearbox>
        </GearboxVariants>
        """;

    [Fact]
    public void Scale_top_gear_scales_all_auto_gears_proportionally()
    {
        var updated = GearboxService.ApplyGearboxUpdatesToTextForTests(
            SampleXml,
            gearboxName: "g1",
            price: 1900,
            fuelConsumption: 1.5,
            idleFuelModifier: 0.3,
            awdConsumptionModifier: 1.0,
            maxGearAngVel: 20.0,
            highGearAngVel: 8.0,
            reverseGearAngVel: 1.0);

        Assert.Contains("""<Gear AngVel="4" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<Gear AngVel="8" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<Gear AngVel="20" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<HighGear AngVel="8.0" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<ReverseGear AngVel="1.0" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void High_and_reverse_can_change_independently()
    {
        var updated = GearboxService.ApplyGearboxUpdatesToTextForTests(
            SampleXml,
            gearboxName: "g1",
            price: 1900,
            fuelConsumption: 1.5,
            idleFuelModifier: 0.3,
            awdConsumptionModifier: 1.0,
            maxGearAngVel: 10.0,
            highGearAngVel: 14.0,
            reverseGearAngVel: 2.5);

        Assert.Contains("""<HighGear AngVel="14" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<ReverseGear AngVel="2.5" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<Gear AngVel="10.0" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void AngVel_is_clamped_to_saber_range()
    {
        var updated = GearboxService.ApplyGearboxUpdatesToTextForTests(
            SampleXml,
            gearboxName: "g1",
            price: 1900,
            fuelConsumption: 1.5,
            idleFuelModifier: 0.3,
            awdConsumptionModifier: 1.0,
            maxGearAngVel: 10.0,
            highGearAngVel: 99.0,
            reverseGearAngVel: 0.01);

        Assert.Contains("""<HighGear AngVel="32" """, updated, StringComparison.Ordinal);
        Assert.Contains("""<ReverseGear AngVel="0.1" """, updated, StringComparison.Ordinal);
    }
}
