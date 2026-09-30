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
        var result = TuningProfileService.ApplyEntries(
            workingPakPath,
            package.Settings.Entries,
            progress);

        TuningPresetLibrary.SetActivePreset(info.Id, info.Source);
        return result;
    }

    private static TuningPresetSource ResolveSourceAfterApply(string presetId)
    {
        var match = TuningPresetLibrary.ListPresets(includeSynthetic: false)
            .FirstOrDefault(p => string.Equals(p.Id, presetId, StringComparison.OrdinalIgnoreCase));
        return match?.Source ?? TuningPresetSource.User;
    }
}
