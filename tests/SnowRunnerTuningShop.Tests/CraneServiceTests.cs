using SnowRunnerTuningShop.Core.Crane;

namespace SnowRunnerTuningShop.Tests;

public sealed class CraneServiceTests
{
    private const string SampleMinicraneXml =
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

    [Fact]
    public void Arm_force_multiplier_scales_Crane_and_Arm_motors_but_not_Anchor()
    {
        var updated = CraneService.ApplyMultipliersToTextForTests(SampleMinicraneXml, armForceMultiplier: 2, movementSpeedMultiplier: 1);

        Assert.Contains("Name=\"Anchor\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"4000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"160000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"800000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"120000\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Movement_speed_multiplier_scales_ControlledIK_coeffs()
    {
        var updated = CraneService.ApplyMultipliersToTextForTests(SampleMinicraneXml, armForceMultiplier: 1, movementSpeedMultiplier: 2);

        Assert.Contains("CoeffEndMovementSpeedOY=\"2.0\"", updated, StringComparison.Ordinal);
        Assert.Contains("CoeffEndMovementSpeedOYWithLoad=\"1.0\"", updated, StringComparison.Ordinal);
        Assert.Contains("CoeffEndMovementSpeedXZ=\"2.0\"", updated, StringComparison.Ordinal);
        Assert.Contains("CoeffEndMovementSpeedXZWithLoad=\"1.0\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"80000\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Baseline_multipliers_leave_xml_unchanged()
    {
        var updated = CraneService.ApplyMultipliersToTextForTests(SampleMinicraneXml, 1, 1);
        Assert.Equal(SampleMinicraneXml.ReplaceLineEndings("\n"), updated.ReplaceLineEndings("\n"));
    }

    [Fact]
    public void Multi_root_templates_plus_TruckAddon_parses_and_scales_arm_motors()
    {
        const string xml =
            """
            <_templates Include="trucks">
              <Body>
                <Anchor>
                  <Constraint Name="Anchor" Type="Prismatic">
                    <Motor Force="4000" Type="Position" />
                  </Constraint>
                </Anchor>
              </Body>
            </_templates>
            <TruckAddon>
              <PhysicsModel>
                <Body>
                  <Constraint Name="Crane" Type="Hinge">
                    <Motor Force="80000" Tau="0.5" Type="Position" />
                  </Constraint>
                  <Constraint Name="Arm1" Type="Hinge">
                    <Motor Force="400000" Tau="0.5" Type="Position" />
                  </Constraint>
                </Body>
              </PhysicsModel>
              <ControlledIK
                CoeffEndMovementSpeedOY="1.0"
                CoeffEndMovementSpeedOYWithLoad="0.5"
                CoeffEndMovementSpeedXZ="1.0"
                CoeffEndMovementSpeedXZWithLoad="0.5" />
              <GameData Price="5700">
                <AddonType Name="Crane" />
              </GameData>
            </TruckAddon>
            """;

        var updated = CraneService.ApplyMultipliersToTextForTests(xml, armForceMultiplier: 2, movementSpeedMultiplier: 2);

        Assert.DoesNotContain("<__crane_root__>", updated, StringComparison.Ordinal);
        Assert.Contains("<_templates", updated, StringComparison.Ordinal);
        Assert.Contains("<TruckAddon>", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"4000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"160000\"", updated, StringComparison.Ordinal);
        Assert.Contains("Force=\"800000\"", updated, StringComparison.Ordinal);
        Assert.Contains("CoeffEndMovementSpeedOYWithLoad=\"1.0\"", updated, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Crane", true)]
    [InlineData("Arm1", true)]
    [InlineData("ArmExt", true)]
    [InlineData("Cabin", true)]
    [InlineData("GrapplerBase", true)]
    [InlineData("Anchor", false)]
    [InlineData("AnchorExt", false)]
    [InlineData("LeftGrappler", false)]
    [InlineData("RightGrappler", false)]
    public void IsArmConstraintName_filters_outriggers_and_jaws(string name, bool expected)
    {
        Assert.Equal(expected, CraneService.IsArmConstraintName(name));
    }
}
