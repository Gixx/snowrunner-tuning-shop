namespace SnowRunnerTuningShop.Core.Constants;

public static class PakPaths
{
    public const string MediaPrefix = "[media]/";
    public const string TemplatesPrefix = "[media]/_templates/";
    public const string ClassesPrefix = "[media]/classes/";
    public const string DlcPrefix = "[media]/_dlc/";

    public static readonly string[] TuningCategories =
    [
        "engines",
        "winches",
        "gearboxes",
        "suspensions",
        "wheels",
        "trucks",
    ];

    public static string FormatTuningCategoryName(string categoryId) =>
        categoryId.Equals("trucks", StringComparison.OrdinalIgnoreCase)
            ? "vehicles"
            : categoryId;
}
