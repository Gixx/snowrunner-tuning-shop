using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class VehicleLongitudinalBalanceXmlTests
{
    private const string Baseline =
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
              >
                <Constraint Type="Fixed" />
              </Body>
            </Body>
          </PhysicsModel>
        </Truck>
        """;

    [Fact]
    public void Apply_shifts_chassis_and_cabin_X_by_same_delta()
    {
        var updated = VehicleLongitudinalBalanceXml.ApplyDelta(Baseline, Baseline, -0.5);

        Assert.True(XmlRewriteSafety.TryValidate(updated, out var failure), failure);
        Assert.Contains("CenterOfMassOffset=\"(-1.3; 0; 0)\"", updated, StringComparison.Ordinal);
        Assert.Contains("CenterOfMassOffset=\"(-0.1; 0.7; 0)\"", updated, StringComparison.Ordinal);
        Assert.True(VehicleLongitudinalBalanceXml.TryReadDelta(updated, Baseline, out var delta));
        Assert.Equal(-0.5, delta, 3);
    }

    [Fact]
    public void Apply_preserves_cabin_Y_when_shifting_X()
    {
        var withLowCabin = VehicleCabinCenterOfMassXml.ApplyCabinY(Baseline, -0.4);
        var updated = VehicleLongitudinalBalanceXml.ApplyDelta(withLowCabin, Baseline, 0.25);

        Assert.Contains("CenterOfMassOffset=\"(-0.55; 0; 0)\"", updated, StringComparison.Ordinal);
        Assert.Contains("CenterOfMassOffset=\"(0.65; -0.4; 0)\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Zero_delta_is_noop()
    {
        var updated = VehicleLongitudinalBalanceXml.ApplyDelta(Baseline, Baseline, 0);
        Assert.Equal(Baseline, updated);
    }
}
