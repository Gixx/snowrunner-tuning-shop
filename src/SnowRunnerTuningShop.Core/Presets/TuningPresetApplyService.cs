using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Presets;

public static class TuningPresetApplyService
{
    public static TuningProfileReapplyResult Apply(
        string workingPakPath,
        TuningPresetPackage package,
        IProgress<TuningProfileReapplyProgress>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(package);
        EnsureCleanBaselineWorkingPak(workingPakPath, progress);
        var result = TuningProfileService.ApplyEntries(
            workingPakPath,
            package.Settings.Entries,
            progress);

        TuningPresetLibrary.SetActivePreset(package.Manifest.Id, ResolveSourceAfterApply(package.Manifest.Id));
        return result;
    }

    public static TuningProfileReapplyResult Apply(
        string workingPakPath,
        TuningPresetInfo info,
        IProgress<TuningProfileReapplyProgress>? progress = null)
    {
        if (info.Source == TuningPresetSource.Synthetic)
        {
            throw new InvalidOperationException("Cannot apply the unnamed placeholder preset.");
        }

        var package = TuningPresetLibrary.LoadPackage(info);
        EnsureCleanBaselineWorkingPak(workingPakPath, progress);
        var result = TuningProfileService.ApplyEntries(
            workingPakPath,
            package.Settings.Entries,
            progress);

        TuningPresetLibrary.SetActivePreset(info.Id, info.Source);
        return result;
    }

    /// <summary>
    /// Preset Apply always starts from vanilla: if the working pak diverges from the baseline
    /// (e.g. Active – changed), restore it first so entries are not layered on dirty XML.
    /// </summary>
    private static void EnsureCleanBaselineWorkingPak(
        string workingPakPath,
        IProgress<TuningProfileReapplyProgress>? progress)
    {
        var baselinePath = PakBaselineService.RequireBaseline(workingPakPath);
        var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath);
        EditionConfig? edition = null;
        if (!string.IsNullOrWhiteSpace(editionId))
        {
            WorkspaceConfigStore.Load().Editions.TryGetValue(editionId, out edition);
        }

        var workingFingerprint = PakFingerprintService.GetFreshFingerprint(
            workingPakPath,
            edition?.LastKnownWorkingFingerprint);
        var baselineFingerprint = PakFingerprintService.GetFreshFingerprint(
            baselinePath,
            edition?.BaselineFingerprint);

        if (PakFingerprintService.FingerprintsMatch(workingFingerprint, baselineFingerprint))
        {
            return;
        }

        progress?.Report(
            new TuningProfileReapplyProgress(
                TuningProfileReapplyPhase.Preparing,
                Current: 0,
                Total: 1,
                EntryPath: "baseline-restore"));

        PakBaselineService.RestorePakFromBaseline(workingPakPath);
    }

    private static TuningPresetSource ResolveSourceAfterApply(string presetId)
    {
        var match = TuningPresetLibrary.ListPresets(includeSynthetic: false)
            .FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
        return match?.Source ?? TuningPresetSource.User;
    }
}
