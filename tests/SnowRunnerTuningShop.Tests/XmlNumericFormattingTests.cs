using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Tests;

public sealed class XmlNumericFormattingTests
{
    [Theory]
    [InlineData(0.3666666666666667, "0.37")]
    [InlineData(1.0 / 3.0, "0.33")]
    [InlineData(1.5, "1.5")]
    [InlineData(2.0, "2")]
    [InlineData(2.005, "2.01")]
    public void Format_CapsFloatsAtTwoDecimals(double value, string expected) =>
        Assert.Equal(expected, XmlNumericFormatting.Format(value));

    [Fact]
    public void Format_KeepTrailingDotZero_ForIntegerLikeFloats() =>
        Assert.Equal("1.0", XmlNumericFormatting.Format(1.0, keepTrailingDotZero: true));

    [Fact]
    public void Gearbox_OneThirdMultiplier_WritesTwoDecimalFuelValues()
    {
        const string xml =
            """
            <GearboxVariants>
              <Gearbox Name="g1" FuelConsumption="1.1" IdleFuelModifier="0.2" AWDConsumptionModifier="1.0" />
            </GearboxVariants>
            """;

        var updated = GearboxService.ApplyMultipliersToTextForTests(xml, 1.0 / 3.0, 1.0 / 3.0, 1.0 / 3.0);

        Assert.Contains("FuelConsumption=\"0.37\"", updated, StringComparison.Ordinal);
        Assert.Contains("IdleFuelModifier=\"0.07\"", updated, StringComparison.Ordinal);
        Assert.Contains("AWDConsumptionModifier=\"0.33\"", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("0.3666", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("0.0666", updated, StringComparison.Ordinal);
        Assert.DoesNotContain("0.3333", updated, StringComparison.Ordinal);
    }
}
