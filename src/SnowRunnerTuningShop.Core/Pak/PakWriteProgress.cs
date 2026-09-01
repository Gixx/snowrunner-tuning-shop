namespace SnowRunnerTuningShop.Core.Pak;

public enum PakWritePhase
{
    Copying,
    Preparing,
    Writing,
    Saving,
}

public sealed record PakWriteProgress(
    PakWritePhase Phase,
    int Current,
    int Total,
    string? EntryName = null);
