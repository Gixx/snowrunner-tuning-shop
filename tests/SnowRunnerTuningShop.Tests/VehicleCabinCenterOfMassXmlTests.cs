using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class VehicleCabinCenterOfMassXmlTests
{
    private const string SampleXml =
        """
        <Truck>
          <PhysicsModel Mesh="trucks/demo">
            <Body
              CenterOfMassOffset="(-0.8; 0; 0)"
              ImpactType="Truck"
              Mass="3000"
              ModelFrame="BoneChassis_cdt"
            >
              <Body
                CenterOfMassOffset="(0.4; 0.7; 0)"
                ImpactType="Truck"
                Mass="2250"
                ModelFrame="BoneCabin_cdt"
                NetSync="pv"
              >
                <Constraint Type="Fixed" />
              </Body>
            </Body>
          </PhysicsModel>
        </Truck>
        """;

    [Fact]
    public void TryRead_prefers_BoneCabin_cdt_Y()
    {
        Assert.True(VehicleCabinCenterOfMassXml.TryReadCabinY(SampleXml, out var y));
        Assert.Equal(0.7, y);
    }

    [Fact]
    public void Apply_changes_only_cabin_Y_and_preserves_XZ()
    {
        var updated = VehicleCabinCenterOfMassXml.ApplyCabinY(SampleXml, -0.4);

        Assert.True(XmlRewriteSafety.TryValidate(updated, out var failure), failure);
        Assert.Contains("CenterOfMassOffset=\"(0.4; -0.4; 0)\"", updated, StringComparison.Ordinal);
        Assert.Contains("CenterOfMassOffset=\"(-0.8; 0; 0)\"", updated, StringComparison.Ordinal);
        Assert.True(VehicleCabinCenterOfMassXml.TryReadCabinY(updated, out var y));
        Assert.Equal(-0.4, y);
    }

    [Fact]
    public void Apply_clamps_to_slider_range()
    {
        var high = VehicleCabinCenterOfMassXml.ApplyCabinY(SampleXml, 99);
        Assert.Contains(
            $"CenterOfMassOffset=\"(0.4; {VehicleCabinCenterOfMassXml.FormatY(VehicleCabinCenterOfMassXml.MaxY)}; 0)\"",
            high,
            StringComparison.Ordinal);

        var low = VehicleCabinCenterOfMassXml.ApplyCabinY(SampleXml, -99);
        Assert.Contains(
            $"CenterOfMassOffset=\"(0.4; {VehicleCabinCenterOfMassXml.FormatY(VehicleCabinCenterOfMassXml.MinY)}; 0)\"",
            low,
            StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_cabin_frame_returns_false()
    {
        const string noCabin =
            """
            <Truck>
              <PhysicsModel>
                <Body CenterOfMassOffset="(0; -0.15; 0)" ImpactType="Truck" Mass="800" />
              </PhysicsModel>
            </Truck>
            """;

        Assert.False(VehicleCabinCenterOfMassXml.TryReadCabinY(noCabin, out _));
        Assert.Equal(noCabin, VehicleCabinCenterOfMassXml.ApplyCabinY(noCabin, -0.5));
    }
}
