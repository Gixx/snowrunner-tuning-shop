using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop.Core.Profile;

public enum WorkspaceHealthKind
{
    NotReady,
    HealthyVanilla,
    HealthyTuned,
    ReadyToReapply,
    GameUpdateDetected,
    UnknownExternalChange,
    InconsistentMarker,
}

public sealed record WorkspaceHealth(
    WorkspaceHealthKind Kind,
    string? EditionId,
    bool HasMarker,
    bool HasProfile,
    int ProfileEntryCount,
    bool WorkingMatchesBaseline,
    bool CanRefreshBaseline,
    bool CanReapply)
{
    public static WorkspaceHealth Unavailable { get; } = new(
        WorkspaceHealthKind.NotReady,
        EditionId: null,
        HasMarker: false,
        HasProfile: false,
        ProfileEntryCount: 0,
        WorkingMatchesBaseline: false,
        CanRefreshBaseline: false,
        CanReapply: false);
}

public static class WorkspaceHealthService
{
    public static WorkspaceHealth Evaluate(string? workingPakPath)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(workingPakPath) || !File.Exists(workingPakPath))
            {
                return WorkspaceHealth.Unavailable;
            }

            var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath);
            if (string.IsNullOrWhiteSpace(editionId)
                || !PakBaselineService.HasBaselineForEdition(editionId))
            {
                return WorkspaceHealth.Unavailable;
            }

            var baselinePath = PakBaselineService.GetBaselinePathForEdition(editionId);
            if (!File.Exists(baselinePath))
            {
                return WorkspaceHealth.Unavailable;
            }

            var config = WorkspaceConfigStore.Load();
            config.Editions.TryGetValue(editionId, out var edition);

            var workingFingerprint = PakFingerprintService.GetFreshFingerprint(
                workingPakPath,
                edition?.LastKnownWorkingFingerprint);
            var baselineFingerprint = PakFingerprintService.GetFreshFingerprint(
                baselinePath,
                edition?.BaselineFingerprint);

            if (!PakFingerprintService.FingerprintsMatch(workingFingerprint, edition?.LastKnownWorkingFingerprint)
                || !PakFingerprintService.FingerprintsMatch(baselineFingerprint, edition?.BaselineFingerprint))
            {
                WorkspaceConfigStore.UpdateEditionFingerprints(
                    editionId,
                    baselineFingerprint: baselineFingerprint,
                    workingPakPath: workingPakPath,
                    workingFingerprint: workingFingerprint);
            }

            var hasMarker = TuningProfileMarker.HasMarker(workingPakPath);
            var profile = TuningProfileService.TryLoadProfile(editionId);
            var profileEntryCount = profile?.Entries.Count ?? 0;
            var hasProfile = profileEntryCount > 0;
            var workingMatchesBaseline = PakFingerprintService.FingerprintsMatch(
                workingFingerprint,
                baselineFingerprint);

            var kind =
                hasMarker && workingMatchesBaseline ? WorkspaceHealthKind.InconsistentMarker :
                hasMarker ? WorkspaceHealthKind.HealthyTuned :
                hasProfile && workingMatchesBaseline ? WorkspaceHealthKind.ReadyToReapply :
                hasProfile ? WorkspaceHealthKind.GameUpdateDetected :
                workingMatchesBaseline ? WorkspaceHealthKind.HealthyVanilla :
                WorkspaceHealthKind.UnknownExternalChange;

            return new WorkspaceHealth(
                kind,
                editionId,
                hasMarker,
                hasProfile,
                profileEntryCount,
                workingMatchesBaseline,
                CanRefreshBaseline: !hasMarker && !workingMatchesBaseline,
                CanReapply: hasProfile && workingMatchesBaseline);
        }
        catch
        {
            return WorkspaceHealth.Unavailable;
        }
    }
}
