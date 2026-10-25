using SnowRunnerTuningShop.Core.Cargo;

namespace SnowRunnerTuningShop.Tests;

public class CargoTypeDisplayNamesTests
{
    [Theory]
    [InlineData("LogsMedium", "LOGS_MEDIUM")]
    [InlineData("Bags2", "BAGS_2")]
    [InlineData("BA20", "BA20")]
    [InlineData("ConcreteBlocks", "CONCRETE_BLOCKS")]
    [InlineData("LogsMedium_Burnt", "LOGS_MEDIUM_BURNT")]
    public void ToScreamingSnake_matches_game_key_bodies(string input, string expected) =>
        Assert.Equal(expected, CargoTypeDisplayNames.ToScreamingSnake(input));

    [Fact]
    public void ResolveDisplayName_uses_UI_CARGO_NAME_keys()
    {
        var strings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["UI_CARGO_LOGS_MEDIUM_NAME"] = "Medium Logs",
            ["UI_CARGO_BURNT_LOGS_NAME"] = "Burnt logs",
            ["UI_CARGO_BOILER"] = "Industrial boiler",
            ["UI_CARGO_RADIOCTIVE_NAME"] = "Secure Container",
        };

        Assert.Equal(
            "Medium Logs",
            CargoTypeDisplayNames.ResolveDisplayName(strings, "CargoLogsMedium", "fallback"));
        Assert.Equal(
            "Burnt logs",
            CargoTypeDisplayNames.ResolveDisplayName(strings, "CargoLogsMedium_Burnt", "fallback"));
        Assert.Equal(
            "Industrial boiler",
            CargoTypeDisplayNames.ResolveDisplayName(strings, "CargoBoiler", "fallback"));
        Assert.Equal(
            "Secure Container",
            CargoTypeDisplayNames.ResolveDisplayName(strings, "CargoRadioactive", "fallback"));
    }

    [Fact]
    public void ResolveDisplayName_falls_back_when_unknown() =>
        Assert.Equal(
            "CargoWeirdThing",
            CargoTypeDisplayNames.ResolveDisplayName(
                new Dictionary<string, string>(),
                "CargoWeirdThing",
                "CargoWeirdThing"));
}
