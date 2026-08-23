using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Pak;

namespace SnowRunnerTuningShop.Core.Profile;

public sealed record TuningProfileSyncResult(
    int ChangedEntryCount,
    bool ProfileSaved,
    bool MarkerPresent);

public static class TuningProfileService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetProfilesDirectory()
    {
        var directory = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "profiles");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetProfilePath(string editionId)
    {
        var id = GameEditionDetector.SanitizeEditionId(editionId);
        return Path.Combine(GetProfilesDirectory(), $"tuning-profile.{id}.json");
    }

    public static TuningProfileDocument? TryLoadProfile(string editionId)
    {
        var path = GetProfilePath(editionId);
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TuningProfileDocument>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    public static bool HasProfile(string editionId)
    {
        var profile = TryLoadProfile(editionId);
        return profile?.Entries.Count > 0;
    }

    public static void ClearProfile(string editionId)
    {
        var path = GetProfilePath(editionId);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    public static TuningProfileSyncResult SyncAfterPakWrite(string workingPakPath)
    {
        if (string.IsNullOrWhiteSpace(workingPakPath) || !File.Exists(workingPakPath))
        {
            return new TuningProfileSyncResult(0, false, false);
        }

        var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath);
        if (string.IsNullOrWhiteSpace(editionId))
        {
            return new TuningProfileSyncResult(0, false, TuningProfileMarker.HasMarker(workingPakPath));
        }

        string baselinePath;
        try
        {
            baselinePath = PakBaselineService.RequireBaseline(workingPakPath);
        }
        catch
        {
            return new TuningProfileSyncResult(0, false, TuningProfileMarker.HasMarker(workingPakPath));
        }

        var baselineFingerprint = PakFingerprintService.ComputeFileFingerprint(baselinePath);
        WorkspaceConfigStore.UpdateEditionFingerprints(
            editionId,
            baselineFingerprint: baselineFingerprint,
            workingPakPath: workingPakPath);

        var diff = BuildProfileDiff(workingPakPath, baselinePath);
        var workingFingerprint = PakFingerprintService.ComputeFileFingerprint(workingPakPath);

        if (diff.Count == 0)
        {
            var existingProfile = TryLoadProfile(editionId);
            if (existingProfile?.Entries.Count > 0
                && string.Equals(existingProfile.BaselineSha256, baselineFingerprint.Sha256, StringComparison.OrdinalIgnoreCase)
                && PakFingerprintService.FingerprintsMatch(workingFingerprint, baselineFingerprint))
            {
                WorkspaceConfigStore.UpdateEditionFingerprints(
                    editionId,
                    baselineFingerprint: baselineFingerprint,
                    workingFingerprint: workingFingerprint);

                return new TuningProfileSyncResult(
                    existingProfile.Entries.Count,
                    ProfileSaved: false,
                    MarkerPresent: TuningProfileMarker.HasMarker(workingPakPath));
            }

            ClearProfile(editionId);
            var markerRemoved = SyncMarker(workingPakPath, editionId, baselineFingerprint.Sha256, profileHasEntries: false);
            return new TuningProfileSyncResult(0, false, markerRemoved);
        }

        var profile = new TuningProfileDocument
        {
            SchemaVersion = 1,
            EditionId = editionId,
            BaselineSha256 = baselineFingerprint.Sha256,
            UpdatedUtc = DateTime.UtcNow,
            Entries = diff,
        };

        SaveProfile(profile);

        WorkspaceConfigStore.UpdateEditionFingerprints(
            editionId,
            baselineFingerprint: baselineFingerprint,
            workingFingerprint: workingFingerprint);

        var markerPresent = SyncMarker(workingPakPath, editionId, baselineFingerprint.Sha256, profileHasEntries: true);
        return new TuningProfileSyncResult(profile.Entries.Count, ProfileSaved: true, markerPresent);
    }

    public static void RecordWorkingPakOpened(string workingPakPath)
    {
        if (string.IsNullOrWhiteSpace(workingPakPath) || !File.Exists(workingPakPath))
        {
            return;
        }

        var editionId = WorkspaceConfigStore.TryResolveEditionId(workingPakPath);
        if (string.IsNullOrWhiteSpace(editionId))
        {
            return;
        }

        try
        {
            var workingFingerprint = PakFingerprintService.ComputeFileFingerprint(workingPakPath);
            WorkspaceConfigStore.UpdateEditionFingerprints(
                editionId,
                workingPakPath: workingPakPath,
                workingFingerprint: workingFingerprint);
        }
        catch
        {
            // Non-fatal when the pak is temporarily unavailable.
        }
    }

    public static void OnBaselineReplaced(string editionId, string baselinePath)
    {
        ClearProfile(editionId);
        if (!File.Exists(baselinePath))
        {
            return;
        }

        var baselineFingerprint = PakFingerprintService.ComputeFileFingerprint(baselinePath);
        WorkspaceConfigStore.UpdateEditionFingerprints(
            editionId,
            baselineFingerprint: baselineFingerprint);
    }

    public static void OnWorkingPakRestoredFromBaseline(string editionId, string workingPakPath)
    {
        if (!File.Exists(workingPakPath))
        {
            return;
        }

        var workingFingerprint = PakFingerprintService.ComputeFileFingerprint(workingPakPath);
        WorkspaceConfigStore.UpdateEditionFingerprints(
            editionId,
            workingPakPath: workingPakPath,
            workingFingerprint: workingFingerprint);
    }

    private static Dictionary<string, string> BuildProfileDiff(string workingPakPath, string baselinePath)
    {
        var diff = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        using var workingArchive = ZipFile.OpenRead(workingPakPath);
        using var baselineArchive = ZipFile.OpenRead(baselinePath);

        foreach (var entry in workingArchive.Entries)
        {
            var entryPath = entry.FullName.Replace('\\', '/');
            if (!TuningProfilePaths.IsTrackedEntry(entryPath))
            {
                continue;
            }

            var baselineEntry = PakEntryLocator.FindEntry(baselineArchive, entryPath);
            if (baselineEntry is null)
            {
                continue;
            }

            var workingBytes = ReadEntryBytes(entry);
            var baselineBytes = ReadEntryBytes(baselineEntry);
            if (workingBytes.AsSpan().SequenceEqual(baselineBytes))
            {
                continue;
            }

            diff[entryPath] = Convert.ToBase64String(workingBytes);
        }

        return diff;
    }

    private static void SaveProfile(TuningProfileDocument profile)
    {
        var path = GetProfilePath(profile.EditionId);
        var json = JsonSerializer.Serialize(profile, JsonOptions);
        File.WriteAllText(path, json);
    }

    private static bool SyncMarker(
        string workingPakPath,
        string editionId,
        string baselineSha256,
        bool profileHasEntries)
    {
        var markerExists = TuningProfileMarker.HasMarker(workingPakPath);
        if (!profileHasEntries)
        {
            if (!markerExists)
            {
                return false;
            }

            InitialPakWriter.RemoveEntries(workingPakPath, [TuningProfileMarker.EntryPath], syncProfile: false);
            return false;
        }

        if (markerExists)
        {
            return true;
        }

        var markerBytes = TuningProfileMarker.BuildMarkerXml(editionId, baselineSha256, DateTime.UtcNow);
        InitialPakWriter.ReplaceEntries(
            workingPakPath,
            new Dictionary<string, byte[]>(StringComparer.Ordinal)
            {
                [TuningProfileMarker.EntryPath] = markerBytes,
            },
            syncProfile: false);

        return true;
    }

    private static byte[] ReadEntryBytes(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
