using System.IO.Compression;
using System.Text;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Xml;

namespace SnowRunnerTuningShop.Core.Pak;

/// <summary>
/// Shared pak enumeration / baseline-relative replace orchestration for parts tuning services.
/// </summary>
public static class PartPakPipeline
{
    public static string NormalizeEntryPath(string fullName) =>
        fullName.Replace('\\', '/');

    public static void ValidateMultiplier(double value, string parameterName)
    {
        if (!double.IsFinite(value) || value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName, "Multiplier must be a positive number.");
        }
    }

    /// <summary>
    /// Build UTF-8 replacements by transforming baseline (vanilla) entry text and comparing to the working pak.
    /// </summary>
    /// <param name="transform">
    /// <c>(entryPath, baselineText, currentText) → updatedText</c>.
    /// Use <paramref name="baselineText"/> for normal parts; Crane may fall back to <paramref name="currentText"/>.
    /// </param>
    /// <param name="countChangedItems">
    /// Optional per-file changed-item counter. When null, each replaced file counts as 1.
    /// </param>
    /// <param name="includeCurrentEntry">
    /// Optional filter on the working entry text (e.g. Crane addon detection).
    /// </param>
    public static PartPakApplyResult BuildBaselineReplacements(
        string pakPath,
        Func<string, bool> isTargetEntry,
        Func<string, string, string, string> transform,
        Func<string, string, string, int>? countChangedItems = null,
        Func<string, string, bool>? includeCurrentEntry = null)
    {
        ArgumentNullException.ThrowIfNull(isTargetEntry);
        ArgumentNullException.ThrowIfNull(transform);

        var baselinePath = PakBaselineService.RequireBaseline(pakPath);
        var replacements = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        var changedItems = 0;

        using var baselineArchive = ZipFile.OpenRead(baselinePath);
        using var currentArchive = ZipFile.OpenRead(pakPath);

        foreach (var entry in currentArchive.Entries)
        {
            var entryPath = NormalizeEntryPath(entry.FullName);
            if (!isTargetEntry(entryPath))
            {
                continue;
            }

            var currentText = PartXmlHelpers.ReadEntryUtf8(entry);
            if (includeCurrentEntry is not null && !includeCurrentEntry(entryPath, currentText))
            {
                continue;
            }

            var baselineText = PakVanillaText.Read(baselineArchive, entry, PartXmlHelpers.ReadEntryUtf8);
            var updatedText = transform(entryPath, baselineText, currentText);

            if (!string.Equals(currentText, updatedText, StringComparison.Ordinal))
            {
                replacements[entryPath] = Encoding.UTF8.GetBytes(updatedText);
                changedItems += countChangedItems?.Invoke(entryPath, currentText, updatedText) ?? 1;
            }
        }

        return new PartPakApplyResult(replacements, changedItems);
    }

    public static int CommitReplacements(string pakPath, IReadOnlyDictionary<string, byte[]> replacements) =>
        InitialPakWriter.ReplaceEntries(pakPath, replacements);
}

public readonly record struct PartPakApplyResult(
    IReadOnlyDictionary<string, byte[]> Replacements,
    int ChangedItems);
