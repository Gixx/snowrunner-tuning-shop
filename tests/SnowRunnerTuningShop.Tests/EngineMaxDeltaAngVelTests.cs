using SnowRunnerTuningShop.Core.Engine;

namespace SnowRunnerTuningShop.Tests;

public sealed class EngineMaxDeltaAngVelTests
{
    [Fact]
    public void Save_updates_MaxDeltaAngVel_and_clamps_to_practical_range()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" MaxDeltaAngVel="0.01" />
              <GameData Price="1000" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyEngineUpdatesToTextForTests(
            xml,
            engineName: "e1",
            price: 1000,
            torque: 100000,
            fuelConsumption: 5.0,
            damageCapacity: 200,
            engineResponsiveness: 0.04,
            maxDeltaAngVel: 2_000_000);

        Assert.Contains("""MaxDeltaAngVel="10" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Torque="100000" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_keeps_small_vanilla_style_values()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" MaxDeltaAngVel="0.01" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyEngineUpdatesToTextForTests(
            xml,
            engineName: "e1",
            price: 0,
            torque: 100000,
            fuelConsumption: 5.0,
            damageCapacity: 200,
            engineResponsiveness: 0.04,
            maxDeltaAngVel: 0.05);

        Assert.Contains("""MaxDeltaAngVel="0.05" """, updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_does_not_inject_MaxDeltaAngVel_when_missing_and_unset()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" />
              <GameData Price="1000" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyEngineUpdatesToTextForTests(
            xml,
            engineName: "e1",
            price: 4200,
            torque: 100000,
            fuelConsumption: 5.0,
            damageCapacity: 200,
            engineResponsiveness: 0.04);

        Assert.DoesNotContain("MaxDeltaAngVel", updated, StringComparison.Ordinal);
        Assert.Contains("Price=\"4200\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Save_writes_explicit_zero_when_set()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200"
                      EngineResponsiveness="0.04" MaxDeltaAngVel="0.01" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyEngineUpdatesToTextForTests(
            xml,
            engineName: "e1",
            price: 0,
            torque: 100000,
            fuelConsumption: 5.0,
            damageCapacity: 200,
            engineResponsiveness: 0.04,
            maxDeltaAngVel: 0);

        Assert.Contains("""MaxDeltaAngVel="0" """, updated, StringComparison.Ordinal);
    }
}
