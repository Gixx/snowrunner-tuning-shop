using System.Globalization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Xml;

public static class PartXmlHelpers
{
    private static readonly Regex PriceRegex = new(
        @"\bPrice\s*=\s*""(?<value>[^""]+)""",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static int ExtractPrice(string block)
    {
        var match = PriceRegex.Match(block);
        if (!match.Success)
        {
            return 0;
        }

        return int.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var price)
            ? price
            : 0;
    }

    public static string FormatUsedBy(IReadOnlyList<string> vehicleNames)
    {
        if (vehicleNames.Count == 0)
        {
            return "—";
        }

        if (vehicleNames.Count <= 3)
        {
            return string.Join(", ", vehicleNames);
        }

        return $"{string.Join(", ", vehicleNames.Take(3))} (+{vehicleNames.Count - 3})";
    }

    public static string FormatUsedByTooltip(IReadOnlyList<string> vehicleNames, string emptyMessage) =>
        vehicleNames.Count == 0
            ? emptyMessage
            : string.Join(", ", vehicleNames);
}
