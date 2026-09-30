using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Presets;

public static class TuningPresetLibrary
{
    public const string SyntheticId = "__unnamed__";
    public const string BundledSourceKey = "bundled";
    public const string UserSourceKey = "user";

    public static string GetUserPresetsDirectory()
    {
        var directory = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "presets");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static string GetBundledPresetsDirectory()
    {
        foreach (var root in BundledRoots())
        {
            if (Directory.Exists(root))
            {
                return root;
            }
        }

        var fallback = Path.Combine(AppContext.BaseDirectory, "assets", "presets");
        Directory.CreateDirectory(fallback);
        return fallback;
    }

    public static IReadOnlyList<TuningPresetInfo> ListPresets(bool includeSynthetic = true)
    {
        var byId = new Dictionary<string, TuningPresetInfo>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in EnumerateArchivePaths(GetBundledPresetsDirectory()))
        {
            if (TryReadInfo(path, TuningPresetSource.Bundled, out var info))
            {
                byId[info.Id] = info;
            }
        }

        foreach (var path in EnumerateArchivePaths(GetUserPresetsDirectory()))
        {
            if (TryReadInfo(path, TuningPresetSource.User, out var info))
            {
                byId[info.Id] = info; // user overrides bundled same id
            }
        }

        var list = byId.Values
            .OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(p => p.Version, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (includeSynthetic)
        {
            var (activeId, _) = WorkspaceConfigStore.GetActivePreset();
            // Untitled workspace row when nothing is active, or when the library is empty.
            if (list.Count == 0 || string.IsNullOrWhiteSpace(activeId))
            {
                list.Insert(0, CreateSyntheticInfo());
            }
        }

        return list;
    }

    /// <summary>
    /// Clears the active preset pointer and the saved tuning profile for the edition,
    /// leaving an untitled (unnamed) workspace after a baseline restore.
    /// </summary>
    public static void ClearToUntitledWorkspace(string? editionId)
    {
        SetActivePreset(null, null);
        if (!string.IsNullOrWhiteSpace(editionId))
        {
            TuningProfileService.ClearProfile(editionId);
        }
    }

    public static TuningPresetInfo CreateSyntheticInfo(string? displayName = null) =>
        new(
            SyntheticId,
            string.IsNullOrWhiteSpace(displayName) ? "[unnamed]" : displayName.Trim(),
            "-",
            Locked: false,
            TuningPresetSource.Synthetic,
            ArchivePath: null);

    public static bool IsSynthetic(string? presetId) =>
        string.Equals(presetId, SyntheticId, StringComparison.OrdinalIgnoreCase);

    public static TuningPresetPackage? TryLoadPackage(string id, TuningPresetSource source)
    {
        if (IsSynthetic(id) || source == TuningPresetSource.Synthetic)
        {
            return null;
        }

        var info = ListPresets(includeSynthetic: false)
            .FirstOrDefault(p =>
                string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)
                && p.Source == source);
        if (info?.ArchivePath is null || !File.Exists(info.ArchivePath))
        {
            // Fall back: any source with matching id (user preferred via ListPresets merge)
            info = ListPresets(includeSynthetic: false)
                .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        if (info?.ArchivePath is null || !File.Exists(info.ArchivePath))
        {
            return null;
        }

        return TuningPresetArchive.Read(info.ArchivePath);
    }

    public static TuningPresetPackage LoadPackage(TuningPresetInfo info)
    {
        if (info.Source == TuningPresetSource.Synthetic || IsSynthetic(info.Id))
        {
            throw new InvalidOperationException("The unnamed preset has no archive to load.");
        }

        if (string.IsNullOrWhiteSpace(info.ArchivePath) || !File.Exists(info.ArchivePath))
        {
            throw new FileNotFoundException("Preset archive was not found.", info.ArchivePath);
        }

        return TuningPresetArchive.Read(info.ArchivePath);
    }

    public static TuningPresetImportResult Import(
        string sourceArchivePath,
        bool overwriteUserOwned = false)
    {
        var package = TuningPresetArchive.Read(sourceArchivePath);
        var id = package.Manifest.Id.Trim();
        var existing = ListPresets(includeSynthetic: false)
            .FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            if (existing.Source == TuningPresetSource.Bundled)
            {
                throw new InvalidOperationException(
                    $"A bundled preset with id '{id}' already exists and cannot be overwritten.");
            }

            if (!overwriteUserOwned)
            {
                throw new InvalidOperationException(
                    $"A preset with id '{id}' already exists. Pass overwrite to replace the user copy.");
            }
        }

        var destName = SanitizeFileName(id) + TuningPresetArchive.Extension;
        var destPath = Path.Combine(GetUserPresetsDirectory(), destName);
        File.Copy(sourceArchivePath, destPath, overwrite: true);

        // Re-read from destination so path is canonical.
        var imported = TuningPresetArchive.Read(destPath);
        return new TuningPresetImportResult
        {
            Info = ToInfo(imported.Manifest, TuningPresetSource.User, destPath),
            Overwrote = existing is not null,
        };
    }

    public static TuningPresetInfo SaveAsNew(
        string name,
        string version,
        IReadOnlyDictionary<string, string> entries,
        bool locked = false,
        string? author = null,
        string? description = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        if (string.IsNullOrWhiteSpace(version))
        {
            version = "1.0.0";
        }

        var id = Guid.NewGuid().ToString("N");
        var package = new TuningPresetPackage(
            new TuningPresetManifest
            {
                SchemaVersion = 1,
                Id = id,
                Name = name.Trim(),
                Version = version.Trim(),
                Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Locked = locked,
                CreatedUtc = DateTime.UtcNow,
            },
            new TuningPresetSettings
            {
                SchemaVersion = 1,
                Entries = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase),
            });

        var destPath = Path.Combine(GetUserPresetsDirectory(), id + TuningPresetArchive.Extension);
        TuningPresetArchive.Write(destPath, package);
        return ToInfo(package.Manifest, TuningPresetSource.User, destPath);
    }

    public static void ExportCurrentProfile(
        string destinationPath,
        string name,
        string version,
        IReadOnlyDictionary<string, string> entries,
        bool locked = false,
        string? author = null,
        string? description = null,
        string? id = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Preset name is required.", nameof(name));
        }

        var package = new TuningPresetPackage(
            new TuningPresetManifest
            {
                SchemaVersion = 1,
                Id = string.IsNullOrWhiteSpace(id) ? Guid.NewGuid().ToString("N") : id.Trim(),
                Name = name.Trim(),
                Version = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim(),
                Author = string.IsNullOrWhiteSpace(author) ? null : author.Trim(),
                Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                Locked = locked,
                CreatedUtc = DateTime.UtcNow,
            },
            new TuningPresetSettings
            {
                SchemaVersion = 1,
                Entries = new Dictionary<string, string>(entries, StringComparer.OrdinalIgnoreCase),
            });

        TuningPresetArchive.Write(destinationPath, package);
    }

    public static void ExportPackage(TuningPresetInfo info, string destinationPath)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Locked)
        {
            throw new InvalidOperationException("Locked presets cannot be exported.");
        }

        var package = LoadPackage(info);
        if (package.Manifest.Locked)
        {
            throw new InvalidOperationException("Locked presets cannot be exported.");
        }

        TuningPresetArchive.Write(destinationPath, package);
    }

    /// <summary>
    /// Removes a user-library preset archive. Bundled presets cannot be deleted from disk.
    /// Clears active selection when the deleted id was active.
    /// </summary>
    public static void DeleteFromLibrary(TuningPresetInfo info)
    {
        ArgumentNullException.ThrowIfNull(info);
        if (info.Source == TuningPresetSource.Synthetic || IsSynthetic(info.Id))
        {
            throw new InvalidOperationException("The unnamed placeholder cannot be deleted.");
        }

        if (info.Source == TuningPresetSource.Bundled)
        {
            // Bundled packs stay on disk; only clear active if this was the selection.
            ClearActiveIfMatches(info.Id);
            return;
        }

        if (info.Source != TuningPresetSource.User)
        {
            throw new InvalidOperationException("Only user presets can be removed from the library.");
        }

        if (!string.IsNullOrWhiteSpace(info.ArchivePath) && File.Exists(info.ArchivePath))
        {
            File.Delete(info.ArchivePath);
        }
        else
        {
            // Fall back: match by id in the user presets folder.
            foreach (var path in EnumerateArchivePaths(GetUserPresetsDirectory()))
            {
                if (!TryReadInfo(path, TuningPresetSource.User, out var candidate))
                {
                    continue;
                }

                if (string.Equals(candidate.Id, info.Id, StringComparison.OrdinalIgnoreCase))
                {
                    File.Delete(path);
                    break;
                }
            }
        }

        ClearActiveIfMatches(info.Id);
    }

    private static void ClearActiveIfMatches(string presetId)
    {
        var (activeId, _) = WorkspaceConfigStore.GetActivePreset();
        if (string.Equals(activeId, presetId, StringComparison.OrdinalIgnoreCase))
        {
            SetActivePreset(null, null);
        }
    }

    public static void SetActivePreset(string? presetId, TuningPresetSource? source)
    {
        if (string.IsNullOrWhiteSpace(presetId) || IsSynthetic(presetId) || source is null or TuningPresetSource.Synthetic)
        {
            WorkspaceConfigStore.SetActivePreset(null, null);
            return;
        }

        WorkspaceConfigStore.SetActivePreset(
            presetId.Trim(),
            source == TuningPresetSource.Bundled ? BundledSourceKey : UserSourceKey);
    }

    public static TuningPresetDirtyState GetDirtyState(string? editionId)
    {
        var (activeId, sourceKey) = WorkspaceConfigStore.GetActivePreset();
        TuningPresetSource? source = sourceKey switch
        {
            BundledSourceKey => TuningPresetSource.Bundled,
            UserSourceKey => TuningPresetSource.User,
            _ => null,
        };

        if (string.IsNullOrWhiteSpace(activeId) || source is null)
        {
            return new TuningPresetDirtyState
            {
                ActivePresetId = null,
                ActiveSource = null,
                IsDirty = HasAnyProfileEntries(editionId),
            };
        }

        var package = TryLoadPackage(activeId, source.Value);
        if (package is null)
        {
            return new TuningPresetDirtyState
            {
                ActivePresetId = activeId,
                ActiveSource = source,
                IsDirty = true,
            };
        }

        var profile = string.IsNullOrWhiteSpace(editionId)
            ? null
            : TuningProfileService.TryLoadProfile(editionId);
        var current = profile?.Entries ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var expected = package.Settings.Entries;
        var dirty = !EntriesEqual(current, expected);

        return new TuningPresetDirtyState
        {
            ActivePresetId = activeId,
            ActiveSource = source,
            IsDirty = dirty,
        };
    }

    public static bool CanSaveAsNew(TuningPresetDirtyState dirty, TuningPresetInfo? selected)
    {
        if (selected?.Locked == true)
        {
            return false;
        }

        // Locked active preset: never fork from it while that row is selected.
        if (dirty.HasActivePreset
            && selected is not null
            && string.Equals(dirty.ActivePresetId, selected.Id, StringComparison.OrdinalIgnoreCase))
        {
            var active = ListPresets(includeSynthetic: false)
                .FirstOrDefault(p => string.Equals(p.Id, dirty.ActivePresetId, StringComparison.OrdinalIgnoreCase));
            if (active?.Locked == true)
            {
                return false;
            }
        }

        if (dirty.IsDirty)
        {
            return true;
        }

        // No active preset yet: allow saving the current workspace profile as a new pack.
        return !dirty.HasActivePreset
            && HasAnyProfileEntries(WorkspaceConfigStore.Load().ActiveEditionId);
    }

    private static bool HasAnyProfileEntries(string? editionId)
    {
        if (string.IsNullOrWhiteSpace(editionId))
        {
            return false;
        }

        var profile = TuningProfileService.TryLoadProfile(editionId);
        return profile?.Entries.Count > 0;
    }

    private static bool EntriesEqual(
        IReadOnlyDictionary<string, string> left,
        IReadOnlyDictionary<string, string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        foreach (var (key, value) in left)
        {
            if (!right.TryGetValue(key, out var other)
                || !string.Equals(value, other, StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    private static bool TryReadInfo(string path, TuningPresetSource source, out TuningPresetInfo info)
    {
        try
        {
            var package = TuningPresetArchive.Read(path);
            info = ToInfo(package.Manifest, source, path);
            return true;
        }
        catch
        {
            info = null!;
            return false;
        }
    }

    private static TuningPresetInfo ToInfo(
        TuningPresetManifest manifest,
        TuningPresetSource source,
        string archivePath) =>
        new(
            manifest.Id,
            manifest.Name,
            manifest.Version,
            manifest.Locked,
            source,
            archivePath,
            manifest.Author,
            manifest.Description);

    private static IEnumerable<string> EnumerateArchivePaths(string directory)
    {
        if (!Directory.Exists(directory))
        {
            yield break;
        }

        foreach (var path in Directory.EnumerateFiles(directory, "*" + TuningPresetArchive.Extension))
        {
            yield return path;
        }
    }

    private static IEnumerable<string> BundledRoots()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "assets", "presets");
        yield return Path.Combine(baseDir, "presets");
    }

    private static string SanitizeFileName(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var name = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(name) ? Guid.NewGuid().ToString("N") : name;
    }
}
