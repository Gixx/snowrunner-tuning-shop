namespace SnowRunnerTuningShop.Core.Constants;

public static class PakPaths
{
    public const string MediaPrefix = "[media]/";
    public const string TemplatesPrefix = "[media]/_templates/";
    public const string ClassesPrefix = "[media]/classes/";
    public const string DlcPrefix = "[media]/_dlc/";

    /// <summary>
    /// Home "Tuning categories" rows — every domain the app can edit as a list of items.
    /// Order roughly matches Parts tabs, then Vehicles, then Trailers.
    /// </summary>
    public static readonly string[] TuningCategories =
    [
        "engines",
        "winches",
        "gearboxes",
        "suspensions",
        "wheels",
        "addons",
        "cranes",
        "trucks",
        "trailers",
    ];

    /// <summary>
    /// Top-level <c>classes/{id}/</c> folders used by profile path tracking.
    /// Nested domains (trailers, addons/cranes) are handled separately.
    /// </summary>
    public static readonly string[] ClassesFolderCategories =
    [
        "engines",
        "winches",
        "gearboxes",
        "suspensions",
        "wheels",
        "trucks",
    ];

    public static string FormatTuningCategoryName(string categoryId) =>
        categoryId.ToLowerInvariant() switch
        {
            "trucks" => "vehicles",
            "wheels" => "tires",
            _ => categoryId,
        };

    public static bool IsCategoryXmlEntry(string categoryId, string entryPath)
    {
        if (string.IsNullOrWhiteSpace(categoryId)
            || string.IsNullOrWhiteSpace(entryPath)
            || !entryPath.EndsWith(".xml", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalized = entryPath.Replace('\\', '/');
        return categoryId.ToLowerInvariant() switch
        {
            "engines" or "winches" or "gearboxes" or "suspensions" or "wheels"
                => normalized.Contains($"/classes/{categoryId}/", StringComparison.OrdinalIgnoreCase),
            "addons" or "cranes"
                => normalized.Contains("/classes/trucks/addons/", StringComparison.OrdinalIgnoreCase),
            "trailers"
                => normalized.Contains("/classes/trucks/trailers/", StringComparison.OrdinalIgnoreCase),
            "trucks" => IsTopLevelTruckXml(normalized),
            _ => false,
        };
    }

    private static bool IsTopLevelTruckXml(string entryPath)
    {
        const string marker = "/classes/trucks/";
        var index = entryPath.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return false;
        }

        var relative = entryPath[(index + marker.Length)..];
        return relative.Length > 0
            && !relative.Contains('/')
            && !relative.Contains('\\');
    }
}
