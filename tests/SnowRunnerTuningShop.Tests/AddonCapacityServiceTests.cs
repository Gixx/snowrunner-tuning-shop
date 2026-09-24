using SnowRunnerTuningShop.Core.AddonCapacity;

namespace SnowRunnerTuningShop.Tests;

public class AddonCapacityServiceTests
{
    private const string MaintainerXml =
        """
        <TruckAddon>
          <TruckData
            FuelCapacity="2000"
            RepairsCapacity="350"
            WheelRepairsCapacity="6"
          />
          <GameData
            Price="6800"
            Category="frame_addons"
            UiName="UI_ADDON_MAINTAINER"
          />
        </TruckAddon>
        """;

    private const string WaterOnlyXml =
        """
        <TruckAddon>
          <TruckData WaterCapacity="1800" />
          <GameData Price="5300" Category="frame_addons" UiName="UI_ADDON_WATER" />
        </TruckAddon>
        """;

    private const string CraneOnlyXml =
        """
        <TruckAddon>
          <TruckData />
          <AddonType Name="Crane" />
          <GameData Price="1000" Category="frame_addons" />
        </TruckAddon>
        """;

    [Fact]
    public void HasAnyCapacityAttribute_detects_capacity_addons_only()
    {
        Assert.True(AddonCapacityService.HasAnyCapacityAttribute(MaintainerXml));
        Assert.True(AddonCapacityService.HasAnyCapacityAttribute(WaterOnlyXml));
        Assert.False(AddonCapacityService.HasAnyCapacityAttribute(CraneOnlyXml));
    }

    [Fact]
    public void Global_fuel_alone_scales_existing_fuel_and_leaves_other_fields()
    {
        var updated = AddonCapacityService.ApplyGlobalMultipliersToTextForTests(
            MaintainerXml,
            fuelMultiplier: 2,
            waterMultiplier: 1,
            repairsMultiplier: 1,
            wheelsMultiplier: 1,
            priceMultiplier: 1);

        Assert.Contains("FuelCapacity=\"4000\"", updated, StringComparison.Ordinal);
        Assert.Contains("RepairsCapacity=\"350\"", updated, StringComparison.Ordinal);
        Assert.Contains("WheelRepairsCapacity=\"6\"", updated, StringComparison.Ordinal);
        Assert.Contains("Price=\"6800\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("WaterCapacity=", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_water_scales_water_only_addon()
    {
        var updated = AddonCapacityService.ApplyGlobalMultipliersToTextForTests(
            WaterOnlyXml,
            fuelMultiplier: 2,
            waterMultiplier: 0.5,
            repairsMultiplier: 2,
            wheelsMultiplier: 2,
            priceMultiplier: 2);

        Assert.Contains("WaterCapacity=\"900\"", updated, StringComparison.Ordinal);
        Assert.Contains("Price=\"10600\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("FuelCapacity=", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void Global_does_not_inject_missing_capacity_attrs()
    {
        var updated = AddonCapacityService.ApplyGlobalMultipliersToTextForTests(
            WaterOnlyXml,
            fuelMultiplier: 3,
            waterMultiplier: 1,
            repairsMultiplier: 3,
            wheelsMultiplier: 3,
            priceMultiplier: 1);

        Assert.DoesNotContain("FuelCapacity=", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("RepairsCapacity=", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("WheelRepairsCapacity=", updated, StringComparison.Ordinal);
        Assert.Contains("WaterCapacity=\"1800\"", updated, StringComparison.Ordinal);
    }

    [Fact]
    public void IsAddonXmlEntry_requires_addons_folder()
    {
        Assert.True(AddonCapacityService.IsAddonXmlEntry(
            "[media]/classes/trucks/addons/frame_addon_watertank.xml"));
        Assert.False(AddonCapacityService.IsAddonXmlEntry(
            "[media]/classes/trucks/trailers/trailer_watertank.xml"));
        Assert.False(AddonCapacityService.IsAddonXmlEntry(
            "[media]/classes/engines/engines_scout_default.xml"));
    }
}
