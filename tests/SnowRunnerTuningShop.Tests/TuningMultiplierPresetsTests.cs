using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class TuningMultiplierPresetsTests
{
    [Fact]
    public void Includes_two_thirds_and_three_quarters_between_half_and_baseline()
    {
        Assert.Equal(11, TuningMultiplierPresets.Values.Length);
        Assert.Equal(TuningMultiplierPresets.Values.Length, TuningMultiplierPresets.Labels.Length);
        Assert.Equal(6, TuningMultiplierPresets.BaselineIndex);
        Assert.Equal(10, TuningMultiplierPresets.MaximumIndex);

        Assert.Equal("2/3", TuningMultiplierPresets.GetLabel(4));
        Assert.Equal("3/4", TuningMultiplierPresets.GetLabel(5));
        Assert.Equal(2.0 / 3.0, TuningMultiplierPresets.GetValue(4), 9);
        Assert.Equal(0.75, TuningMultiplierPresets.GetValue(5));
        Assert.Equal(1.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.BaselineIndex));
    }

    [Fact]
    public void Two_thirds_formats_to_two_decimal_places()
    {
        Assert.Equal("0.67", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(4)));
        Assert.Equal("0.75", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(5)));
    }
}
