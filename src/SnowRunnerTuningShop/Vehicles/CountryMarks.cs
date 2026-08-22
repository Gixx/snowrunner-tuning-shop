using System.Windows.Media;

namespace SnowRunnerTuningShop.Vehicles;

public static class CountryMarks
{
    private static readonly Dictionary<string, string> OvalByIso = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AU"] = "AUS",
        ["BE"] = "B",
        ["CA"] = "CDN",
        ["CN"] = "CHN",
        ["CZ"] = "CZ",
        ["DE"] = "D",
        ["FI"] = "FIN",
        ["FR"] = "F",
        ["GB"] = "GB",
        ["IN"] = "IND",
        ["IT"] = "I",
        ["JP"] = "J",
        ["NL"] = "NL",
        ["PL"] = "PL",
        ["PT"] = "P",
        ["RU"] = "RUS",
        ["SE"] = "S",
        ["SU"] = "SU",
        ["UA"] = "UA",
        ["US"] = "USA",
    };

    public static string OvalCode(string? countryCode, string? explicitOval = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitOval))
        {
            return explicitOval.Trim().ToUpperInvariant();
        }

        if (string.IsNullOrWhiteSpace(countryCode))
        {
            return "";
        }

        return OvalByIso.TryGetValue(countryCode, out var oval)
            ? oval
            : countryCode.Trim().ToUpperInvariant();
    }
}

public static class VehicleCategoryColors
{
    public static Brush ForCategory(string? category)
    {
        var color = category?.Trim() switch
        {
            "Highway" => Color.FromRgb(0x3D, 0x5A, 0x80),
            "Heavy Duty" => Color.FromRgb(0xC4, 0x5C, 0x26),
            "Heavy" => Color.FromRgb(0x8B, 0x1E, 0x1E),
            "Offroad" => Color.FromRgb(0x4A, 0x7C, 0x3F),
            "Scout" => Color.FromRgb(0x2A, 0x6F, 0x7A),
            _ => Color.FromRgb(0x4A, 0x4A, 0x4A),
        };

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}
