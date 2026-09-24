using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trucks;

namespace SnowRunnerTuningShop.Tests;

public class TruckSteerXmlTests
{
    private const string DualFrontXml =
        """
        <Truck>
          <TruckData>
            <SecondAxle Location="front" SteeringAngle="20" Torque="default" />
            <MiddleAxle Location="rear" Torque="default" />
            <FirstAxle Location="front" SteeringAngle="30" Torque="default" />
          </TruckData>
        </Truck>
        """;

    private const string PhoenixXml =
        """
        <Truck>
          <TruckData>
            <FirstAxle Location="front" SteeringAngle="18" Torque="full" />
            <SecondAxle Location="front" SteeringAngle="16" Torque="full" />
            <ThirdAxle Location="rear" SteeringAngle="-8" Torque="default" />
            <FourthAxle Location="rear" SteeringAngle="-14" Torque="default" />
          </TruckData>
        </Truck>
        """;

    [Fact]
    public void Parse_lists_every_axle_including_non_steer()
    {
        var axles = TruckSteerXml.ParseSteerAxles(DualFrontXml, DualFrontXml);
        Assert.Equal(3, axles.Count);
        // Display: FirstAxle ahead of SecondAxle (tag hint), then middle — even when Second is declared first
        Assert.Equal("FirstAxle", axles[0].Tag);
        Assert.Equal(30, axles[0].Angle);
        Assert.Equal(1, axles[0].DisplayOrder);
        Assert.Equal("SecondAxle", axles[1].Tag);
        Assert.Equal(20, axles[1].Angle);
        Assert.False(axles[2].HadSteerInBaseline);
        Assert.Equal("MiddleAxle", axles[2].Tag);
        Assert.Equal(3, axles[2].DisplayOrder);
    }

    [Fact]
    public void Parse_orders_front_axles_by_wheel_pos_not_file_order()
    {
        const string azovLike =
            """
            <Truck>
              <TruckData>
                <SecondAxle Location="front" SteeringAngle="20" Torque="default" />
                <MiddleAxle Location="rear" Torque="default" />
                <FirstAxle Location="front" SteeringAngle="30" Torque="default" />
                <Wheel _template="FirstAxle" Pos="(2.972; 0.547; 1.02)" />
                <Wheel _template="SecondAxle" Pos="(1.014; 0.562; 1.02)" />
                <Wheel _template="MiddleAxle" Pos="(-1.996; 0.576; 0.99)" />
              </TruckData>
            </Truck>
            """;

        var axles = TruckSteerXml.ParseSteerAxles(azovLike, azovLike);
        Assert.Equal("FirstAxle", axles[0].Tag);
        Assert.Equal(30, axles[0].Angle);
        Assert.Equal("SecondAxle", axles[1].Tag);
        Assert.Equal(20, axles[1].Angle);
        Assert.Equal("MiddleAxle", axles[2].Tag);
    }

    [Fact]
    public void Apply_edits_one_front_without_flattening_the_other()
    {
        var axles = TruckSteerXml.ParseSteerAxles(DualFrontXml, DualFrontXml);
        var firstFront = axles.Single(a => a.Tag == "FirstAxle");
        firstFront.Angle = 40;
        var updated = TruckSteerXml.ApplySteerAxles(DualFrontXml, axles);

        Assert.Contains("SteeringAngle=\"20\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"40\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringAngle=\"30\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Clear_vanilla_steer_restores_baseline_and_keeps_attribute()
    {
        var axles = TruckSteerXml.ParseSteerAxles(DualFrontXml, DualFrontXml);
        axles.Single(a => a.Tag == "SecondAxle").Angle = null;
        var updated = TruckSteerXml.ApplySteerAxles(DualFrontXml, axles);

        Assert.Contains("SteeringAngle=\"20\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Inject_and_clear_added_rear_steer_on_middle_axle()
    {
        var axles = TruckSteerXml.ParseSteerAxles(DualFrontXml, DualFrontXml);
        axles.Single(a => a.Tag == "MiddleAxle").Angle = -20;
        var injected = TruckSteerXml.ApplySteerAxles(DualFrontXml, axles);
        Assert.Contains("MiddleAxle", injected, StringComparison.Ordinal);
        Assert.Matches("MiddleAxle[^>]*SteeringAngle=\"-20\"", injected);

        axles = TruckSteerXml.ParseSteerAxles(injected, DualFrontXml);
        var middle = axles.Single(a => a.Tag == "MiddleAxle");
        Assert.False(middle.HadSteerInBaseline);
        middle.Angle = 0;
        var cleared = TruckSteerXml.ApplySteerAxles(injected, axles);
        Assert.DoesNotMatch("MiddleAxle[^>]*SteeringAngle=", cleared);
    }

    [Fact]
    public void Ignores_camera_Rear_tags_without_Torque()
    {
        const string loadstarLike =
            """
            <Truck>
              <TruckData>
                <RearWheel Location="rear" Torque="default" />
                <FrontWheel Location="front" SteeringAngle="40" Torque="default" />
              </TruckData>
              <Camera>
                <Rear HorTransitionEnd="-1.2" ViewPosOffset="(0.2; -0.1; 0.55)" />
                <Rear ViewPosOffset="(0.05; -0.1; 0.7)" />
              </Camera>
            </Truck>
            """;

        var axles = TruckSteerXml.ParseSteerAxles(loadstarLike, loadstarLike);
        Assert.Equal(2, axles.Count);
        Assert.Equal("FrontWheel", axles[0].Tag);
        Assert.Equal(1, axles[0].DisplayOrder);
        Assert.Equal("RearWheel", axles[1].Tag);
        Assert.Equal(2, axles[1].DisplayOrder);
    }

    [Fact]
    public void Apply_phoenix_keeps_independent_rear_angles()
    {
        var axles = TruckSteerXml.ParseSteerAxles(PhoenixXml, PhoenixXml);
        axles.Single(a => a.Tag == "ThirdAxle").Angle = -12;
        var updated = TruckSteerXml.ApplySteerAxles(PhoenixXml, axles);

        Assert.Contains("SteeringAngle=\"18\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"16\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"-12\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"-14\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_front_max_only_touches_baseline_positive_axles()
    {
        var withAdded = TruckSteerXml.ParseSteerAxles(DualFrontXml, DualFrontXml);
        withAdded.Single(a => a.Tag == "MiddleAxle").Angle = -25;
        var working = TruckSteerXml.ApplySteerAxles(DualFrontXml, withAdded);

        var updated = TruckSteerXml.ApplyGlobalSteerPresets(
            working,
            TruckFrontSteerGlobalMode.Maximum,
            TruckRearSteerGlobalMode.Baseline);

        Assert.Contains("SteeringAngle=\"60\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringAngle=\"20\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringAngle=\"30\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"-25\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_rear_min_only_touches_baseline_negative_axles()
    {
        var updated = TruckSteerXml.ApplyGlobalSteerPresets(
            PhoenixXml,
            TruckFrontSteerGlobalMode.Baseline,
            TruckRearSteerGlobalMode.Minimum);

        Assert.Contains("SteeringAngle=\"18\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"16\"", updated, StringComparison.Ordinal);
        Assert.Contains("SteeringAngle=\"-10\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringAngle=\"-8\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("SteeringAngle=\"-14\"", updated, StringComparison.Ordinal);
    }
}
