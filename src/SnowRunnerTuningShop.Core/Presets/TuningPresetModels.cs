using System.Text.Json.Serialization;

namespace SnowRunnerTuningShop.Core.Presets;

public sealed class TuningPresetManifest
{
    public int SchemaVersion { get; set; } = 1;

    public string Id { get; set; } = "";

    public string Name { get; set; } = "";

    public string Version { get; set; } = "1.0.0";

    public string? Author { get; set; }

    public string? Description { get; set; }

    public bool Locked { get; set; }

    public string? Icon { get; set; }

    public DateTime? CreatedUtc { get; set; }

    public string? MinAppVersion { get; set; }
}

public sealed class TuningPresetSettings
{
    public int SchemaVersion { get; set; } = 1;

    /// <summary>Entry path → Base64-encoded XML bytes (same as <c>TuningProfileDocument.Entries</c>).</summary>
    public Dictionary<string, string> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public enum TuningPresetSource
{
    Synthetic,
    Bundled,
    User,
}

public sealed record TuningPresetInfo(
    string Id,
    string Name,
    string Version,
    bool Locked,
    TuningPresetSource Source,
    string? ArchivePath,
    string? Author = null,
    string? Description = null);

public sealed record TuningPresetPackage(
    TuningPresetManifest Manifest,
    TuningPresetSettings Settings,
    byte[]? IconBytes = null);

public sealed class TuningPresetImportResult
{
    public required TuningPresetInfo Info { get; init; }

    public bool Overwrote { get; init; }
}

public sealed class TuningPresetDirtyState
{
    public string? ActivePresetId { get; init; }

    public TuningPresetSource? ActiveSource { get; init; }

    public bool IsDirty { get; init; }

    public bool HasActivePreset =>
        !string.IsNullOrWhiteSpace(ActivePresetId)
        && ActiveSource is TuningPresetSource.Bundled or TuningPresetSource.User;
}
