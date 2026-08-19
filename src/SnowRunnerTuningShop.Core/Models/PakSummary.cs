namespace SnowRunnerTuningShop.Core.Models;

public sealed record PakSummary(
    string FilePath,
    long FileSizeBytes,
    int TotalEntries,
    int XmlEntries,
    int DlcPackages,
    long UncompressedBytes,
    IReadOnlyList<PakCategorySummary> TuningCategories,
    IReadOnlyList<string> TopLevelFolders);
