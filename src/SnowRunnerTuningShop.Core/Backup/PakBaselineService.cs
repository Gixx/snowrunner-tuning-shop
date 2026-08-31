using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Backup;

public sealed record PakBaselineInfo(
    string BaselinePath,
    DateTime LastWriteTimeUtc,
    long FileSizeBytes,
    string SourceDescription,
    string EditionId,
    string EditionDisplayName);

public sealed record WorkspaceActivationResult(
    string WorkingPakPath,
    string EditionId,
    string EditionDisplayName,
    string BaselinePath,
    bool BaselineCreated);

public static class PakBaselineService
{
    public const string LegacyBaselineSuffix = ".baseline";

    public static string GetBaselinePathForEdition(string editionId)
    {
        var id = GameEditionDetector.SanitizeEditionId(editionId);
        return Path.Combine(
            WorkspaceConfigStore.GetBaselinesDirectory(),
            $"initial.baseline.{id}.pak");
    }

    public static bool HasBaselineForEdition(string editionId) =>
        File.Exists(GetBaselinePathForEdition(editionId));

    public static bool HasBaseline(string workingPakPath)
    {
        var editionId = ResolveEditionId(workingPakPath);
        return !string.IsNullOrWhiteSpace(editionId) && HasBaselineForEdition(editionId);
    }

    public static PakBaselineInfo? TryGetBaselineInfo(string workingPakPath)
    {
        var editionId = ResolveEditionId(workingPakPath);
        if (string.IsNullOrWhiteSpace(editionId))
        {
            return null;
        }

        return TryGetBaselineInfoForEdition(editionId);
    }

    public static PakBaselineInfo? TryGetBaselineInfoForEdition(string editionId)
    {
        var baselinePath = GetBaselinePathForEdition(editionId);
        if (!File.Exists(baselinePath))
        {
            return null;
        }

        SetReadOnlyAttribute(baselinePath);
        var edition = ResolveEditionDisplay(editionId);
        var info = new FileInfo(baselinePath);
        return new PakBaselineInfo(
            info.FullName,
            info.LastWriteTimeUtc,
            info.Length,
            edition.DisplayName,
            edition.Id,
            edition.DisplayName);
    }

    public static string RequireBaseline(string workingPakPath)
    {
        var editionId = ResolveEditionId(workingPakPath)
            ?? throw new InvalidOperationException(
                "No baseline is configured. On the Home page, use Set baseline from original.");

        TryMigrateLegacySidecar(workingPakPath, editionId);

        var baselinePath = GetBaselinePathForEdition(editionId);
        if (!File.Exists(baselinePath))
        {
            throw new InvalidOperationException(
                "No baseline is configured for this game edition. " +
                "On the Home page, use Set baseline from original.");
        }

        SetReadOnlyAttribute(baselinePath);
        return baselinePath;
    }

    /// <summary>
    /// Clears read-only on the working pak and verifies the file is writable before patching.
    /// </summary>
    public static void EnsureWritableWorkingPak(string workingPakPath)
    {
        if (string.IsNullOrWhiteSpace(workingPakPath) || !File.Exists(workingPakPath))
        {
            throw new FileNotFoundException("Working initial.pak was not found.", workingPakPath);
        }

        ClearReadOnlyAttribute(workingPakPath);

        var directory = Path.GetDirectoryName(workingPakPath);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
        {
            throw new InvalidOperationException("The working pak directory could not be resolved.");
        }

        var probePath = Path.Combine(directory, $".tuning-shop-write-test-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(probePath, "ok");
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new InvalidOperationException(
                "Cannot write to the working initial.pak folder. Close SnowRunner if it is running, " +
                "then verify the game install folder is writable or point the app at a writable copy of initial.pak.",
                ex);
        }
        finally
        {
            if (File.Exists(probePath))
            {
                File.Delete(probePath);
            }
        }
    }

    /// <summary>
    /// First-time / explicit baseline setup: copy the selected original pak into a read-only
    /// edition baseline and remember it as the working pak path.
    /// </summary>
    public static WorkspaceActivationResult SetBaselineFromOriginal(string originalPakPath)
    {
        if (!File.Exists(originalPakPath))
        {
            throw new FileNotFoundException("Original initial.pak was not found.", originalPakPath);
        }

        var fullPath = Path.GetFullPath(originalPakPath);
        var edition = GameEditionDetector.Detect(fullPath);
        var baselinePath = CreateOrReplaceEditionBaseline(edition.Id, fullPath);
        WorkspaceConfigStore.SetActiveEdition(edition.Id, edition.DisplayName, fullPath);

        return new WorkspaceActivationResult(
            fullPath,
            edition.Id,
            edition.DisplayName,
            baselinePath,
            BaselineCreated: true);
    }

    /// <summary>
    /// Switch to another store/location. Creates a baseline for that edition only when missing.
    /// </summary>
    public static WorkspaceActivationResult ChangeLocation(string pakPath)
    {
        if (!File.Exists(pakPath))
        {
            throw new FileNotFoundException("initial.pak was not found.", pakPath);
        }

        var fullPath = Path.GetFullPath(pakPath);
        var edition = GameEditionDetector.Detect(fullPath);
        var created = false;
        string baselinePath;

        if (HasBaselineForEdition(edition.Id))
        {
            baselinePath = GetBaselinePathForEdition(edition.Id);
            SetReadOnlyAttribute(baselinePath);
        }
        else
        {
            baselinePath = CreateOrReplaceEditionBaseline(edition.Id, fullPath);
            created = true;
        }

        WorkspaceConfigStore.SetActiveEdition(edition.Id, edition.DisplayName, fullPath);
        return new WorkspaceActivationResult(
            fullPath,
            edition.Id,
            edition.DisplayName,
            baselinePath,
            created);
    }

    public static void RestorePakFromBaseline(string workingPakPath)
    {
        var baselinePath = RequireBaseline(workingPakPath);
        var editionId = ResolveEditionId(workingPakPath);
        ClearReadOnlyAttribute(workingPakPath);
        File.Copy(baselinePath, workingPakPath, overwrite: true);

        if (!string.IsNullOrWhiteSpace(editionId))
        {
            TuningProfileService.OnWorkingPakRestoredFromBaseline(editionId, workingPakPath);
        }
    }

    /// <summary>
    /// Copies the current working pak over the read-only baseline without clearing the saved profile.
    /// Use after a game update, when the working file is the new unmodified vanilla pak.
    /// </summary>
    public static string RefreshBaselineFromWorkingPak(string workingPakPath)
    {
        if (!File.Exists(workingPakPath))
        {
            throw new FileNotFoundException("Working initial.pak was not found.", workingPakPath);
        }

        if (TuningProfileMarker.HasMarker(workingPakPath))
        {
            throw new InvalidOperationException(
                "Cannot refresh the baseline while Tuning Shop edits are present in the working pak. " +
                "Restore the full baseline first, or wait until the game has replaced the file.");
        }

        var editionId = ResolveEditionId(workingPakPath)
            ?? throw new InvalidOperationException("No edition is configured for this working pak.");

        if (!HasBaselineForEdition(editionId))
        {
            throw new InvalidOperationException(
                "No baseline is configured for this game edition. " +
                "On the Home page, use Set baseline from original.");
        }

        var workingFingerprint = PakFingerprintService.ComputeFileFingerprint(workingPakPath);
        var baselinePath = GetBaselinePathForEdition(editionId);
        ClearReadOnlyAttribute(baselinePath);
        File.Copy(workingPakPath, baselinePath, overwrite: true);
        SetReadOnlyAttribute(baselinePath);

        var baselineInfo = new FileInfo(baselinePath);
        var baselineFingerprint = new PakFingerprintSnapshot
        {
            Sha256 = workingFingerprint.Sha256,
            SizeBytes = workingFingerprint.SizeBytes,
            LastWriteTimeUtc = baselineInfo.LastWriteTimeUtc,
        };

        WorkspaceConfigStore.UpdateEditionFingerprints(
            editionId,
            baselineFingerprint: baselineFingerprint,
            workingPakPath: workingPakPath,
            workingFingerprint: workingFingerprint);

        return baselinePath;
    }

    public static string CreateOrReplaceEditionBaseline(string editionId, string sourceFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Baseline source file was not found.", sourceFilePath);
        }

        var baselinePath = GetBaselinePathForEdition(editionId);
        ClearReadOnlyAttribute(baselinePath);
        File.Copy(sourceFilePath, baselinePath, overwrite: true);
        SetReadOnlyAttribute(baselinePath);
        TuningProfileService.OnBaselineReplaced(editionId, baselinePath);
        return baselinePath;
    }

    private static string? ResolveEditionId(string workingPakPath)
    {
        var fromConfig = WorkspaceConfigStore.TryResolveEditionId(workingPakPath);
        if (!string.IsNullOrWhiteSpace(fromConfig))
        {
            return fromConfig;
        }

        var detected = GameEditionDetector.Detect(workingPakPath);
        if (HasBaselineForEdition(detected.Id))
        {
            return detected.Id;
        }

        return detected.Id;
    }

    private static GameEdition ResolveEditionDisplay(string editionId)
    {
        var config = WorkspaceConfigStore.Load();
        if (config.Editions.TryGetValue(editionId, out var edition)
            && !string.IsNullOrWhiteSpace(edition.DisplayName))
        {
            return new GameEdition(editionId, edition.DisplayName);
        }

        return editionId switch
        {
            "steam" => new GameEdition(editionId, "Steam"),
            "gog" => new GameEdition(editionId, "GOG"),
            "epic" => new GameEdition(editionId, "Epic"),
            "xbox" => new GameEdition(editionId, "Xbox"),
            _ => new GameEdition(editionId, "Custom"),
        };
    }

    private static void TryMigrateLegacySidecar(string workingPakPath, string editionId)
    {
        if (HasBaselineForEdition(editionId))
        {
            return;
        }

        var legacyPath = workingPakPath + LegacyBaselineSuffix;
        if (!File.Exists(legacyPath))
        {
            return;
        }

        CreateOrReplaceEditionBaseline(editionId, legacyPath);
    }

    private static void SetReadOnlyAttribute(string path)
    {
        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) == 0)
        {
            File.SetAttributes(path, attributes | FileAttributes.ReadOnly);
        }
    }

    private static void ClearReadOnlyAttribute(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & FileAttributes.ReadOnly) != 0)
        {
            File.SetAttributes(path, attributes & ~FileAttributes.ReadOnly);
        }
    }
}
