using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.Localization;

public sealed class LocaleCatalogDocument
{
    public int SchemaVersion { get; set; } = 1;

    public List<LocaleCatalogEntry> Languages { get; set; } = [];
}

public sealed class LocaleCatalogEntry
{
    public string UiCulture { get; set; } = "";

    public string DisplayName { get; set; } = "";

    public string GameLanguage { get; set; } = "english";

    public string? InnoLanguage { get; set; }

    public string? File { get; set; }

    public int Revision { get; set; } = 1;

    public string? MinAppVersion { get; set; }

    [JsonIgnore]
    public string FileName => LocalePackNames.FileName(UiCulture, File);
}

public sealed record LocalePackSnapshot(
    LanguageOption Option,
    bool IsBundled,
    bool HasOverlay,
    bool HasLocalFile,
    int LocalRevision,
    int? RemoteRevision,
    string? MinAppVersion,
    bool CompatibleWithApp)
{
    public bool RemoteNewer =>
        RemoteRevision is int remote && remote > LocalRevision;

    public bool CanAdd =>
        CompatibleWithApp && !HasLocalFile && RemoteRevision is > 0;

    public bool CanUpdate =>
        CompatibleWithApp && HasLocalFile && RemoteNewer;

    public bool CanRemove => HasOverlay;
}

internal static partial class LocalePackNames
{
    public const string CatalogFileName = "catalog.json";
    public const string KeysFileName = "keys.json";
    public const string OverlayManifestFileName = "manifest.json";

    [GeneratedRegex(@"^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$", RegexOptions.CultureInvariant)]
    private static partial Regex UiCultureRegex();

    public static bool IsValidUiCulture(string? value) =>
        !string.IsNullOrWhiteSpace(value) && UiCultureRegex().IsMatch(value.Trim());

    public static string FileName(string uiCulture, string? file)
    {
        var name = string.IsNullOrWhiteSpace(file)
            ? $"{uiCulture}.json"
            : Path.GetFileName(file.Trim());
        if (string.IsNullOrWhiteSpace(name)
            || !name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
            || name.Contains("..", StringComparison.Ordinal)
            || string.Equals(name, CatalogFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, KeysFileName, StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, OverlayManifestFileName, StringComparison.OrdinalIgnoreCase))
        {
            return $"{uiCulture}.json";
        }

        return name;
    }
}
