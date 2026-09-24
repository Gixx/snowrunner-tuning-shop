using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public class VehiclePhysicsMassXmlTests
{
    private const string SampleXml =
        """
        <Truck>
          <TruckData FuelCapacity="100" />
          <PhysicsModel Mesh="trucks/demo">
            <Body
              CenterOfMassOffset="(0; -1; 0)"
              ImpactType="Truck"
              Mass="5000"
            >
              <Body Mass="400" ModelFrame="BoneFrontCart_cdt">
                <Body ForceBodyParams="true" Mass="200" ModelFrame="BoneHandle_cdt" />
              </Body>
              <Body Mass="0.2" ModelFrame="BoneKeyFob_cdt" />
            </Body>
          </PhysicsModel>
        </Truck>
        """;

    [Fact]
    public void TryReadPrimaryMass_prefers_ImpactType_Truck_body()
    {
        Assert.True(VehiclePhysicsMassXml.TryReadPrimaryMass(SampleXml, out var mass));
        Assert.Equal(5000, mass);
    }

    [Fact]
    public void ScaleAllMasses_one_third_caps_repeating_fractions()
    {
        var updated = VehiclePhysicsMassXml.ScaleAllMasses(SampleXml, 1.0 / 3.0);

        Assert.Contains("Mass=\"1666.67\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"133.33\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"66.67\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"0.07\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("0.333333", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void ApplyPrimaryMass_scales_nested_bodies_proportionally()
    {
        var updated = VehiclePhysicsMassXml.ApplyPrimaryMass(SampleXml, 2500);

        Assert.Contains("Mass=\"2500\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"200\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"100\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"0.1\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Truck_global_mass_alone_scales_physics_masses()
    {
        var updated = TruckTuningService.ApplyGlobalMultipliersToTextForTests(
            SampleXml,
            fuelMultiplier: 1,
            TruckFrontSteerGlobalMode.Baseline,
            responsivenessMultiplier: 1,
            priceMultiplier: 1,
            massMultiplier: 0.5);

        Assert.Contains("Mass=\"2500\"", updated, StringComparison.Ordinal);
        Assert.Contains("Mass=\"200\"", updated, StringComparison.Ordinal);
        Assert.Contains("FuelCapacity=\"100\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Mass_slider_presets_stop_at_two_times()
    {
        Assert.Equal(2.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.TwoTimesIndex));
        Assert.Equal(TuningMultiplierPresets.TwoTimesIndex, TuningMultiplierPresets.ClampMassIndex(99));
        Assert.Equal("2x", TuningMultiplierPresets.GetLabel(TuningMultiplierPresets.TwoTimesIndex));
    }
}
