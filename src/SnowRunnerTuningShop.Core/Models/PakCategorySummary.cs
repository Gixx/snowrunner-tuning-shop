namespace SnowRunnerTuningShop.Core.Models;

public sealed record PakCategorySummary(
    string Name,
    int ItemCount,
    int FileCount,
    IReadOnlyList<string> SampleFiles);
