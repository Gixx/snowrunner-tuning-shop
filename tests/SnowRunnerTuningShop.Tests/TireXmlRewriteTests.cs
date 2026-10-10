using SnowRunnerTuningShop.Core.Tires;

namespace SnowRunnerTuningShop.Tests;

public sealed class TireXmlRewriteTests
{
    [Fact]
    public void Multiplier_rewrite_does_not_swallow_GameData_when_WheelFriction_is_truncated()
    {
        // Repro shape from issue #6 profile (wheels_ankatra_1160.xml): a WheelFriction
        // that lost its closing "/>" must not match through the following <GameData>.
        const string xml =
            """
            <TruckWheels>
              <TruckTires>
                <TruckTire _template="Offroad" Name="tire">
                  <WheelFriction _template="Mudtires"
                    BodyFrictionAsphalt="1.91"



                  <GameData
                    Price="5500"
                    UnlockByExploration="false"
                    UnlockByRank="1">
                    <UiDesc UiName="UI_TIRE_NAME" />
                  </GameData>
                </TruckTire>
              </TruckTires>
            </TruckWheels>
            """;

        var updated = TireService.ApplyMultipliersToTextForTests(xml, 2, 2, 2);

        Assert.Equal(xml.ReplaceLineEndings("\n"), updated.ReplaceLineEndings("\n"));
        Assert.DoesNotMatch("""<GameData[^>]*(BodyFriction|SubstanceFriction|BodyFrictionAsphalt)""", updated);
    }

    [Fact]
    public void Multiplier_rewrite_keeps_self_closing_WheelFriction_and_scales_values()
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTires>
                <TruckTire Name="tire">
                  <WheelFriction _template="Mudtires" BodyFrictionAsphalt="2" BodyFriction="1" SubstanceFriction="2"/>
                  <GameData Price="100" UnlockByRank="1"></GameData>
                </TruckTire>
              </TruckTires>
            </TruckWheels>
            """;

        var updated = TireService.ApplyMultipliersToTextForTests(xml, 2, 2, 2);

        Assert.Contains("BodyFrictionAsphalt=\"4\"", updated, StringComparison.Ordinal);
        Assert.Contains("BodyFriction=\"2\"", updated, StringComparison.Ordinal);
        Assert.Contains("SubstanceFriction=\"4\"", updated, StringComparison.Ordinal);
        Assert.Matches("""<WheelFriction\b[^>]*/>""", updated);
        Assert.Contains("<GameData Price=\"100\" UnlockByRank=\"1\">", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_friction_sets_absolute_mud_only_and_skips_other_templates()
    {
        const string xml =
            """
            <TruckWheels>
              <TruckTires>
                <TruckTire Name="mud">
                  <WheelFriction _template="Mudtires" BodyFrictionAsphalt="2" BodyFriction="1" SubstanceFriction="2" IsIgnoreIce="true"/>
                  <GameData Price="100" UnlockByRank="1"></GameData>
                </TruckTire>
                <TruckTire Name="highway">
                  <WheelFriction _template="Highway" BodyFrictionAsphalt="3" BodyFriction="1" SubstanceFriction="0.2"/>
                  <GameData Price="100" UnlockByRank="1"></GameData>
                </TruckTire>
              </TruckTires>
            </TruckWheels>
            """;

        var updated = TireService.ApplyCategoryFrictionToTextForTests(
            xml,
            TireFrictionKind.Mud,
            onRoad: null,
            offRoad: null,
            mud: 3.5);

        Assert.Contains("""SubstanceFriction="3.5" """, updated, StringComparison.Ordinal);
        Assert.Contains("""BodyFrictionAsphalt="2" """, updated, StringComparison.Ordinal);
        Assert.Contains("""IsIgnoreIce="true" """, updated, StringComparison.Ordinal);
        Assert.Contains("""_template="Highway" """, updated, StringComparison.Ordinal);
        Assert.Contains("SubstanceFriction=\"0.2\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Category_friction_classifies_scout_and_heavy_templates()
    {
        Assert.True(TireFrictionKinds.MatchesTemplate("ScoutHighway", TireFrictionKind.Highway));
        Assert.True(TireFrictionKinds.MatchesTemplate("HeavyMudtires", TireFrictionKind.Mud));
        Assert.True(TireFrictionKinds.MatchesTemplate("ScoutChains", TireFrictionKind.Chained));
        Assert.False(TireFrictionKinds.MatchesTemplate("Highway", TireFrictionKind.Mud));
    }
}
