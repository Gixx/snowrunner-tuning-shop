using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class TuningMultiplierPresetsTests
{
    [Fact]
    public void Uses_hybrid_step_grid_from_point_one_to_five()
    {
        Assert.Equal(23, TuningMultiplierPresets.Values.Length);
        Assert.Equal(TuningMultiplierPresets.Values.Length, TuningMultiplierPresets.Labels.Length);
        Assert.Equal(9, TuningMultiplierPresets.BaselineIndex);
        Assert.Equal(14, TuningMultiplierPresets.TwoTimesIndex);
        Assert.Equal(22, TuningMultiplierPresets.MaximumIndex);

        Assert.Equal(0.1, TuningMultiplierPresets.GetValue(0));
        Assert.Equal("0.1x", TuningMultiplierPresets.GetLabel(0));
        Assert.Equal(1.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.BaselineIndex));
        Assert.Equal(1.2, TuningMultiplierPresets.GetValue(10));
        Assert.Equal(2.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.TwoTimesIndex));
        Assert.Equal(2.25, TuningMultiplierPresets.GetValue(15));
        Assert.Equal(3.5, TuningMultiplierPresets.GetValue(19));
        Assert.Equal(5.0, TuningMultiplierPresets.GetValue(TuningMultiplierPresets.MaximumIndex));
    }

    [Fact]
    public void Hybrid_steps_format_cleanly_for_xml()
    {
        Assert.Equal("0.1", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(0)));
        Assert.Equal("1.2", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(10)));
        Assert.Equal("2.25", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(15)));
        Assert.Equal("2.75", XmlNumericFormatting.Format(TuningMultiplierPresets.GetValue(17)));
    }
}
