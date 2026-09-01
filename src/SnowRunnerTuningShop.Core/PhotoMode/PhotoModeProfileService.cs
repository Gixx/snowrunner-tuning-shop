using System.Text.Json;
using System.Text.Json.Serialization;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.PhotoMode;

/// <summary>
/// Photo mode settings are stored separately from the main tuning profile so
/// "Reapply saved changes" never writes experimental sslbundle/cache_block data
/// after a game update or baseline refresh.
/// </summary>
public static class PhotoModeProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetProfilesDirectory()
    {
        var directory = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "photo-mode");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetProfilePath(string editionId)
    {
        var id = GameEditionDetector.SanitizeEditionId(editionId);
        return Path.Combine(GetProfilesDirectory(), $"photo-mode.{id}.json");
    }

    public static PhotoModeProfileDocument? TryLoadProfile(string editionId)
    {
        var path = GetProfilePath(editionId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PhotoModeProfileDocument>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool HasProfile(string editionId)
    {
        var profile = TryLoadProfile(editionId);
        return profile is not null;
    }

    public static void ClearProfile(string editionId)
    {
        var path = GetProfilePath(editionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static void SaveProfile(string workingPakPath, PhotoModeSettings settings)
    {
        var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath)
            ?? throw new InvalidOperationException("No game edition is configured for this working pak.");

        var baselinePath = PakBaselineService.RequireBaseline(workingPakPath);
        var baselineSha256 = PakFingerprintService.ComputeFileFingerprint(baselinePath).Sha256;

        var profile = new PhotoModeProfileDocument
        {
            SchemaVersion = 1,
            EditionId = editionId,
            BaselineSha256 = baselineSha256,
            UpdatedUtc = DateTime.UtcNow,
            Settings = settings,
        };

        var path = GetProfilePath(editionId);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static PhotoModeSaveResult ReapplySaved(string workingPakPath)
    {
        var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath)
            ?? throw new InvalidOperationException("No game edition is configured for this working pak.");

        var profile = TryLoadProfile(editionId)
            ?? throw new PhotoModeLoadException("No saved photo mode settings exist for this edition.");

        var baselinePath = PakBaselineService.RequireBaseline(workingPakPath);
        var baselineSha256 = PakFingerprintService.ComputeFileFingerprint(baselinePath).Sha256;
        if (!string.Equals(profile.BaselineSha256, baselineSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new PhotoModeLoadException(
                "Saved photo mode settings were created with a different baseline. " +
                "Refresh the baseline after a game update, then apply photo mode again from this page.");
        }

        return PhotoModeService.ApplySettings(workingPakPath, profile.Settings, saveProfile: false);
    }
}
