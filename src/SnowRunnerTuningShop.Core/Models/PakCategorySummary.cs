namespace SnowRunnerTuningShop.Core.Models;

public sealed record PakCategorySummary(
    string Name,
    int FileCount,
    IReadOnlyList<string> SampleFiles);
