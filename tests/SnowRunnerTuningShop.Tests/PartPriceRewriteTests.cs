using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class PartPriceRewriteTests
{
    [Fact]
    public void TrySetPrice_ReplacesExistingValue()
    {
        var text = """<Engine Name="e1" /><GameData Price="1000" UnlockByRank="1" />""";

        Assert.True(PartXmlHelpers.TrySetPrice(ref text, 2500));
        Assert.Contains("Price=\"2500\"", text, StringComparison.Ordinal);
        Assert.Contains("UnlockByRank=\"1\"", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TrySetPrice_NoOpWhenUnchanged()
    {
        var text = """<GameData Price="1000" />""";

        Assert.False(PartXmlHelpers.TrySetPrice(ref text, 1000));
        Assert.Equal("""<GameData Price="1000" />""", text);
    }

    [Fact]
    public void Engine_save_updates_GameData_price_without_changing_torque()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200" EngineResponsiveness="0.04" />
              <GameData Price="1000" UnlockByRank="1" />
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

        Assert.Contains("Price=\"4200\"", updated, StringComparison.Ordinal);
        Assert.Contains("Torque=\"100000\"", updated, StringComparison.Ordinal);
        Assert.Contains("UnlockByRank=\"1\"", updated, StringComparison.Ordinal);
    }
}
