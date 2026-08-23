namespace SnowRunnerTuningShop.Core.Profile;

public sealed class TuningProfileDocument
{
    public int SchemaVersion { get; set; } = 1;

    public string EditionId { get; set; } = "";

    public string BaselineSha256 { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    public Dictionary<string, string> Entries { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}
