namespace SnowRunnerTuningShop.Core.PhotoMode;

public sealed class PhotoModeProfileDocument
{
    public int SchemaVersion { get; set; } = 1;

    public string EditionId { get; set; } = "";

    public string BaselineSha256 { get; set; } = "";

    public DateTime UpdatedUtc { get; set; }

    public PhotoModeSettings Settings { get; set; } = PhotoModeSettings.Vanilla;
}
