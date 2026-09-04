using System.Text.Json;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop.Core.Localization;

/// <summary>Bundled locale catalog plus optional AppData overlays downloaded from GitHub.</summary>
public static class LocalePackStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static readonly object Gate = new();
    private static bool _loaded;
    private static List<LocaleCatalogEntry> _bundled = [];
    private static List<LocaleCatalogEntry> _overlay = [];

    public static string GetOverlayDirectory()
    {
        var directory = Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "localization");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static void Reload()
    {
        lock (Gate)
        {
            _bundled = ReadCatalog(FindBundledCatalogPath()) ?? BuiltinCatalog();
            _overlay = ReadCatalog(GetOverlayManifestPath()) ?? [];
            _loaded = true;
        }
    }

    public static IReadOnlyList<LanguageOption> GetInstalledOptions()
    {
        EnsureLoaded();
        List<LocaleCatalogEntry> bundled;
        List<LocaleCatalogEntry> overlay;
        lock (Gate)
        {
            bundled = [.. _bundled];
            overlay = [.. _overlay];
        }

        var map = new Dictionary<string, LocaleCatalogEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in bundled)
        {
            TryAddEntry(map, entry);
        }

        foreach (var entry in overlay)
        {
            TryAddEntry(map, entry, replace: true);
        }

        var bundledOrder = bundled
            .Select(entry => entry.UiCulture)
            .ToList();
        return map.Values
            .Where(entry => ResolveLocalePath(entry.UiCulture) is not null)
            .Select(ToOption)
            .OrderBy(option =>
            {
                var index = bundledOrder.FindIndex(culture =>
                    string.Equals(culture, option.UiCulture, StringComparison.OrdinalIgnoreCase));
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static IReadOnlyList<LocaleCatalogEntry> GetBundledEntries()
    {
        EnsureLoaded();
        lock (Gate)
        {
            return [.. _bundled];
        }
    }

    public static IReadOnlyList<LocaleCatalogEntry> GetOverlayEntries()
    {
        EnsureLoaded();
        lock (Gate)
        {
            return [.. _overlay];
        }
    }

    public static bool IsKnownCulture(string uiCulture)
    {
        EnsureLoaded();
        lock (Gate)
        {
            return _bundled.Concat(_overlay).Any(entry =>
                string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static LocaleCatalogEntry? FindEntry(string uiCulture)
    {
        EnsureLoaded();
        lock (Gate)
        {
            return _overlay.FirstOrDefault(entry =>
                       string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase))
                   ?? _bundled.FirstOrDefault(entry =>
                       string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));
        }
    }

    public static bool IsBundled(string uiCulture)
    {
        EnsureLoaded();
        return GetBundledFilePath(uiCulture) is not null;
    }

    public static bool HasOverlay(string uiCulture)
    {
        var path = GetOverlayFilePath(uiCulture);
        return File.Exists(path);
    }

    public static string? ResolveLocalePath(string uiCulture)
    {
        EnsureLoaded();
        var overlayPath = GetOverlayFilePath(uiCulture);
        var bundledPath = GetBundledFilePath(uiCulture);
        var overlayExists = File.Exists(overlayPath);
        var bundledExists = bundledPath is not null && File.Exists(bundledPath);
        if (overlayExists && bundledExists)
        {
            var overlayRevision = OverlayRevision(uiCulture);
            var bundledRevision = BundledRevision(uiCulture);
            return overlayRevision >= bundledRevision ? overlayPath : bundledPath;
        }

        if (overlayExists)
        {
            return overlayPath;
        }

        return bundledExists ? bundledPath : null;
    }

    public static void InstallOverlay(LocaleCatalogEntry entry, string json)
    {
        ArgumentNullException.ThrowIfNull(entry);
        if (!LocalePackNames.IsValidUiCulture(entry.UiCulture))
        {
            throw new InvalidOperationException($"Unsupported UI culture '{entry.UiCulture}'.");
        }

        ValidateLocaleJson(json);
        var directory = GetOverlayDirectory();
        var path = Path.Combine(directory, entry.FileName);
        File.WriteAllText(path, json);
        UpsertOverlayEntry(entry);
        Reload();
    }

    public static void RemoveOverlay(string uiCulture)
    {
        var path = GetOverlayFilePath(uiCulture);
        if (File.Exists(path))
        {
            File.Delete(path);
        }

        EnsureLoaded();
        lock (Gate)
        {
            _overlay.RemoveAll(entry =>
                string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));
            WriteOverlayManifest(_overlay);
        }

        Reload();
    }

    public static IReadOnlyList<LocalePackSnapshot> BuildSnapshots(
        IReadOnlyList<LocaleCatalogEntry>? remoteEntries)
    {
        EnsureLoaded();
        var remote = remoteEntries ?? [];
        var cultures = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in GetBundledEntries().Concat(GetOverlayEntries()).Concat(remote))
        {
            if (LocalePackNames.IsValidUiCulture(entry.UiCulture))
            {
                cultures.Add(entry.UiCulture);
            }
        }

        var snapshots = new List<LocalePackSnapshot>();
        foreach (var culture in cultures.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
        {
            var local = FindEntry(culture);
            var remoteEntry = remote.FirstOrDefault(entry =>
                string.Equals(entry.UiCulture, culture, StringComparison.OrdinalIgnoreCase));
            var effective = remoteEntry ?? local;
            if (effective is null)
            {
                continue;
            }

            var hasLocalFile = ResolveLocalePath(culture) is not null;
            var localRevision = 0;
            if (hasLocalFile)
            {
                localRevision = Math.Max(BundledRevision(culture), OverlayRevision(culture));
                if (localRevision <= 0)
                {
                    localRevision = 1;
                }
            }

            snapshots.Add(new LocalePackSnapshot(
                ToOption(effective),
                IsBundled(culture),
                HasOverlay(culture),
                hasLocalFile,
                localRevision,
                remoteEntry?.Revision,
                remoteEntry?.MinAppVersion ?? local?.MinAppVersion,
                IsCompatible(remoteEntry?.MinAppVersion ?? local?.MinAppVersion)));
        }

        return snapshots;
    }

    private static void EnsureLoaded()
    {
        if (!_loaded)
        {
            Reload();
        }
    }

    private static string? FindBundledCatalogPath()
    {
        foreach (var root in BundledRoots())
        {
            var path = Path.Combine(root, LocalePackNames.CatalogFileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string? GetBundledFilePath(string uiCulture)
    {
        var entry = FindBundledEntry(uiCulture);
        var fileName = LocalePackNames.FileName(uiCulture, entry?.File);
        foreach (var root in BundledRoots())
        {
            var path = Path.Combine(root, fileName);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static IEnumerable<string> BundledRoots()
    {
        var baseDir = AppContext.BaseDirectory;
        yield return Path.Combine(baseDir, "assets", "localization");
        yield return Path.Combine(baseDir, "localization");
        yield return baseDir;
    }

    private static string GetOverlayManifestPath() =>
        Path.Combine(GetOverlayDirectory(), LocalePackNames.OverlayManifestFileName);

    private static string GetOverlayFilePath(string uiCulture)
    {
        var entry = FindOverlayEntry(uiCulture) ?? FindBundledEntry(uiCulture);
        var fileName = LocalePackNames.FileName(uiCulture, entry?.File);
        return Path.Combine(GetOverlayDirectory(), fileName);
    }

    private static LocaleCatalogEntry? FindBundledEntry(string uiCulture)
    {
        lock (Gate)
        {
            return _bundled.FirstOrDefault(entry =>
                string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static LocaleCatalogEntry? FindOverlayEntry(string uiCulture)
    {
        lock (Gate)
        {
            return _overlay.FirstOrDefault(entry =>
                string.Equals(entry.UiCulture, uiCulture, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static int BundledRevision(string uiCulture) =>
        FindBundledEntry(uiCulture)?.Revision ?? 0;

    private static int OverlayRevision(string uiCulture) =>
        FindOverlayEntry(uiCulture)?.Revision ?? 0;

    private static void UpsertOverlayEntry(LocaleCatalogEntry entry)
    {
        EnsureLoaded();
        lock (Gate)
        {
            _overlay.RemoveAll(existing =>
                string.Equals(existing.UiCulture, entry.UiCulture, StringComparison.OrdinalIgnoreCase));
            _overlay.Add(Clone(entry));
            WriteOverlayManifest(_overlay);
        }
    }

    private static void WriteOverlayManifest(List<LocaleCatalogEntry> entries)
    {
        var document = new LocaleCatalogDocument
        {
            SchemaVersion = 1,
            Languages = entries
                .Select(Clone)
                .OrderBy(entry => entry.UiCulture, StringComparer.OrdinalIgnoreCase)
                .ToList(),
        };
        File.WriteAllText(GetOverlayManifestPath(), JsonSerializer.Serialize(document, JsonOptions));
    }

    private static List<LocaleCatalogEntry>? ReadCatalog(string? path)
    {
        if (path is null || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            var document = JsonSerializer.Deserialize<LocaleCatalogDocument>(json, JsonOptions);
            if (document?.Languages is null || document.Languages.Count == 0)
            {
                return null;
            }

            var list = new List<LocaleCatalogEntry>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in document.Languages)
            {
                if (!LocalePackNames.IsValidUiCulture(entry.UiCulture)
                    || !seen.Add(entry.UiCulture.Trim()))
                {
                    continue;
                }

                list.Add(Clone(entry));
            }

            return list.Count == 0 ? null : list;
        }
        catch
        {
            return null;
        }
    }

    private static void TryAddEntry(
        Dictionary<string, LocaleCatalogEntry> map,
        LocaleCatalogEntry entry,
        bool replace = false)
    {
        if (!LocalePackNames.IsValidUiCulture(entry.UiCulture))
        {
            return;
        }

        var key = entry.UiCulture.Trim();
        if (replace || !map.ContainsKey(key))
        {
            map[key] = Clone(entry);
        }
    }

    private static LocaleCatalogEntry Clone(LocaleCatalogEntry entry) =>
        new()
        {
            UiCulture = entry.UiCulture.Trim(),
            DisplayName = string.IsNullOrWhiteSpace(entry.DisplayName)
                ? entry.UiCulture.Trim()
                : entry.DisplayName.Trim(),
            GameLanguage = string.IsNullOrWhiteSpace(entry.GameLanguage)
                ? "english"
                : entry.GameLanguage.Trim(),
            InnoLanguage = string.IsNullOrWhiteSpace(entry.InnoLanguage) ? null : entry.InnoLanguage.Trim(),
            File = entry.File,
            Revision = entry.Revision <= 0 ? 1 : entry.Revision,
            MinAppVersion = string.IsNullOrWhiteSpace(entry.MinAppVersion) ? null : entry.MinAppVersion.Trim(),
        };

    private static LanguageOption ToOption(LocaleCatalogEntry entry) =>
        new(
            entry.UiCulture,
            entry.DisplayName,
            entry.GameLanguage,
            entry.InnoLanguage ?? "");

    private static bool IsCompatible(string? minAppVersion)
    {
        if (string.IsNullOrWhiteSpace(minAppVersion))
        {
            return true;
        }

        var text = minAppVersion.Trim();
        if (text.StartsWith('v') || text.StartsWith('V'))
        {
            text = text[1..];
        }

        if (!Version.TryParse(AppInfo.Version, out var installed)
            || !Version.TryParse(text, out var required))
        {
            return true;
        }

        return installed >= required;
    }

    internal static void ValidateLocaleJson(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Locale file must be a JSON object of string keys.");
        }

        if (document.RootElement.TryGetProperty("languages", out _)
            && document.RootElement.TryGetProperty("schemaVersion", out _))
        {
            throw new InvalidOperationException("File is a catalog, not a locale string table.");
        }

        var count = 0;
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException($"Locale value '{property.Name}' must be a string.");
            }

            count++;
        }

        if (count < 8)
        {
            throw new InvalidOperationException("Locale file does not contain enough strings.");
        }
    }

    private static List<LocaleCatalogEntry> BuiltinCatalog() =>
    [
        new() { UiCulture = "en", DisplayName = "English", GameLanguage = "english", InnoLanguage = "english", Revision = 4 },
        new() { UiCulture = "de", DisplayName = "Deutsch", GameLanguage = "german", InnoLanguage = "german", Revision = 4 },
        new() { UiCulture = "fr", DisplayName = "Français", GameLanguage = "french", InnoLanguage = "french", Revision = 4 },
        new() { UiCulture = "es", DisplayName = "Español", GameLanguage = "spanish", InnoLanguage = "spanish", Revision = 4 },
        new() { UiCulture = "pt", DisplayName = "Português", GameLanguage = "portuguese", InnoLanguage = "portuguese", Revision = 4 },
        new() { UiCulture = "pt-BR", DisplayName = "Português (Brasil)", GameLanguage = "brazilian", InnoLanguage = "brazilianportuguese", Revision = 4 },
        new() { UiCulture = "pl", DisplayName = "Polski", GameLanguage = "polish", InnoLanguage = "polish", Revision = 4 },
        new() { UiCulture = "ru", DisplayName = "Русский", GameLanguage = "russian", InnoLanguage = "russian", Revision = 4 },
        new() { UiCulture = "uk", DisplayName = "Українська", GameLanguage = "ukrainian", InnoLanguage = "ukrainian", Revision = 4 },
        new() { UiCulture = "zh-CN", DisplayName = "简体中文", GameLanguage = "chinese_simplified", InnoLanguage = "chinesesimplified", Revision = 4 },
        new() { UiCulture = "zh-TW", DisplayName = "繁體中文", GameLanguage = "chinese_traditional", InnoLanguage = "chinesetraditional", Revision = 4 },
    ];
}
