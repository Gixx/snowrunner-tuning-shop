using SnowRunnerTuningShop.Core.Constants;

namespace SnowRunnerTuningShop.Tests;

public sealed class PakPathsTuningCategoriesTests
{
    [Fact]
    public void TuningCategories_include_parts_vehicles_and_trailers()
    {
        Assert.Contains("engines", PakPaths.TuningCategories);
        Assert.Contains("addons", PakPaths.TuningCategories);
        Assert.Contains("cranes", PakPaths.TuningCategories);
        Assert.Contains("trucks", PakPaths.TuningCategories);
        Assert.Contains("trailers", PakPaths.TuningCategories);
        Assert.DoesNotContain("addons", PakPaths.ClassesFolderCategories);
        Assert.DoesNotContain("trailers", PakPaths.ClassesFolderCategories);
    }

    [Theory]
    [InlineData("trucks", "vehicles")]
    [InlineData("wheels", "tires")]
    [InlineData("trailers", "trailers")]
    [InlineData("addons", "addons")]
    public void FormatTuningCategoryName_uses_ui_labels(string id, string expected) =>
        Assert.Equal(expected, PakPaths.FormatTuningCategoryName(id));

    [Theory]
    [InlineData("engines", "[media]/classes/engines/e_us_truck_old_scout.xml", true)]
    [InlineData("wheels", "[media]/classes/wheels/wheels_scout_offroad.xml", true)]
    [InlineData("trailers", "[media]/classes/trucks/trailers/semi_trailer.xml", true)]
    [InlineData("addons", "[media]/classes/trucks/addons/frame_addon.xml", true)]
    [InlineData("cranes", "[media]/classes/trucks/addons/crane_log.xml", true)]
    [InlineData("trucks", "[media]/classes/trucks/chevrolet_kodiak_c70.xml", true)]
    [InlineData("trucks", "[media]/classes/trucks/trailers/semi_trailer.xml", false)]
    [InlineData("trucks", "[media]/classes/trucks/addons/frame_addon.xml", false)]
    [InlineData("trailers", "[media]/classes/trucks/chevrolet_kodiak_c70.xml", false)]
    [InlineData("engines", "[media]/classes/trucks/chevrolet_kodiak_c70.xml", false)]
    public void IsCategoryXmlEntry_classifies_paths(string category, string path, bool expected) =>
        Assert.Equal(expected, PakPaths.IsCategoryXmlEntry(category, path));
}
