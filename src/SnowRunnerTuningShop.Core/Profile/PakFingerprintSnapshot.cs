namespace SnowRunnerTuningShop.Core.Profile;

public sealed class PakFingerprintSnapshot
{
    public string Sha256 { get; set; } = "";

    public long SizeBytes { get; set; }

    public DateTime LastWriteTimeUtc { get; set; }
}
