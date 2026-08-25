namespace SnowRunnerTuningShop.Core.Trucks;

/// <summary>SnowRunner truck-store region codes from GameData Country.</summary>
public static class TruckStoreRegions
{
    /// <summary>Full set used by the Python Mods "truck store everywhere" edit.</summary>
    public const string AllCountriesAttributeValue = "US,RU,NE,CE,CAS,WA";

    public static readonly string[] AllCodes =
    [
        "US",
        "RU",
        "NE",
        "CE",
        "CAS",
        "WA",
    ];

    public static string DisplayName(string code) =>
        code.Trim().ToUpperInvariant() switch
        {
            "US" => "North America",
            "RU" => "Russia",
            "NE" => "New England",
            "CE" => "Central Europe",
            "CAS" => "Central Asia",
            "WA" => "Western Australia",
            _ => code.Trim().ToUpperInvariant(),
        };

    public static IReadOnlyList<string> ParseCodes(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(code => code.ToUpperInvariant())
            .Where(code => code.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static bool HasAllStoreRegions(string? raw)
    {
        var codes = ParseCodes(raw);
        if (codes.Count == 0)
        {
            return false;
        }

        return AllCodes.All(required =>
            codes.Any(code => code.Equals(required, StringComparison.OrdinalIgnoreCase)));
    }

    public static string FormatLockedRegions(string? raw)
    {
        var codes = ParseCodes(raw);
        if (codes.Count == 0)
        {
            return "";
        }

        if (HasAllStoreRegions(raw))
        {
            return "All regions";
        }

        return string.Join(", ", codes.Select(DisplayName));
    }
}
