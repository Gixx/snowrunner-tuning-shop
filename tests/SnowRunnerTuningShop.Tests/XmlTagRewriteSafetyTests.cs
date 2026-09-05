using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Suspension;

namespace SnowRunnerTuningShop.Tests;

/// <summary>
/// Regression for issue #6-style tag swallow: truncated open tags must not match through
/// the next element when multipliers rewrite attrs.
/// </summary>
public sealed class XmlTagRewriteSafetyTests
{
    [Fact]
    public void Engine_multiplier_does_not_swallow_GameData_when_Engine_tag_is_truncated()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0"
                DamageCapacity="200"


              <GameData Price="1000" UnlockByRank="1" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyMultipliersToTextForTests(xml, 2, 2, 2, 2);

        Assert.Equal(xml.ReplaceLineEndings("\n"), updated.ReplaceLineEndings("\n"));
        Assert.DoesNotMatch("""<GameData[^>]*(Torque|FuelConsumption|DamageCapacity)""", updated);
    }

    [Fact]
    public void Engine_multiplier_still_scales_well_formed_self_closing_Engine()
    {
        const string xml =
            """
            <EngineVariants>
              <Engine Name="e1" Torque="100000" FuelConsumption="5.0" DamageCapacity="200" />
              <GameData Price="1000" />
            </EngineVariants>
            """;

        var updated = EngineService.ApplyMultipliersToTextForTests(xml, 2, 1, 1, 1);

        Assert.Contains("Torque=\"200000\"", updated, StringComparison.Ordinal);
        Assert.Contains("<GameData Price=\"1000\" />", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Gearbox_multiplier_does_not_swallow_GameData_when_Gearbox_tag_is_truncated()
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" FuelConsumption="1.5" IdleFuelModifier="0.3"


              <GameData Price="1000" UnlockByRank="1" />
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyMultipliersToTextForTests(xml, 2, 2, 2);

        Assert.Equal(xml.ReplaceLineEndings("\n"), updated.ReplaceLineEndings("\n"));
        Assert.DoesNotMatch("""<GameData[^>]*(FuelConsumption|IdleFuelModifier|AWDConsumptionModifier)""", updated);
    }

    [Fact]
    public void Suspension_multiplier_does_not_swallow_GameData_when_Suspension_tag_is_truncated()
    {
        const string xml =
            """
            <SuspensionSetVariants>
              <SuspensionSet Name="s1" DamageCapacity="80">
                <Suspension Height="0.1" Strength="0.5" Damping="0.4"


                <GameData Price="1000" />
              </SuspensionSet>
            </SuspensionSetVariants>
            """;

        var updated = SuspensionService.ApplyMultipliersToTextForTests(xml, 2, 2, 2, 2);

        Assert.DoesNotMatch("""<GameData[^>]*(Height|Strength|Damping)""", updated);
        Assert.Contains("<GameData Price=\"1000\" />", updated, StringComparison.Ordinal);
    }
}
