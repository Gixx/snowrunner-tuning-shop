using System.Text.Json;
using System.Text.Json.Serialization;
using SnowRunnerTuningShop.Core.Localization;
using SnowRunnerTuningShop.Core.Profile;

namespace SnowRunnerTuningShop.Core.Config;

public sealed class WorkspaceConfig
{
    public string? ActiveEditionId { get; set; }

    public bool SidebarPinned { get; set; }

    /// <summary>App theme: System, Dark, or Light.</summary>
    public string ThemeMode { get; set; } = ThemeModes.System;

    /// <summary>UI culture code (en, de, fr, es, pt, pt-BR, pl, ru, uk).</summary>
    public string UiCulture { get; set; } = "en";

    /// <summary>Latest GitHub release the user chose not to be notified about.</summary>
    public string? SkippedAppVersion { get; set; }

    public Dictionary<string, EditionConfig> Editions { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public static class ThemeModes
{
    public const string System = "System";
    public const string Dark = "Dark";
    public const string Light = "Light";

    public static string Normalize(string? value) =>
        value switch
        {
            Dark => Dark,
            Light => Light,
            _ => System,
        };
}

public sealed class EditionConfig
{
    public string WorkingPakPath { get; set; } = "";

    public string DisplayName { get; set; } = "Custom";

    public PakFingerprintSnapshot? BaselineFingerprint { get; set; }

    public PakFingerprintSnapshot? LastKnownWorkingFingerprint { get; set; }
}

public sealed record ActiveWorkspace(
    string EditionId,
    string DisplayName,
    string WorkingPakPath,
    string BaselinePath,
    bool BaselineExists);

public static class WorkspaceConfigStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string GetAppDataDirectory()
    {
        var root = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SnowRunnerTuningShop");
        Directory.CreateDirectory(root);
        return root;
    }

    public static string GetConfigPath() =>
        Path.Combine(GetAppDataDirectory(), "config.json");

    public static string GetBaselinesDirectory()
    {
        var directory = Path.Combine(GetAppDataDirectory(), "baselines");
        Directory.CreateDirectory(directory);
        return directory;
    }

    public static WorkspaceConfig Load()
    {
        var path = GetConfigPath();
        if (!File.Exists(path))
        {
            return new WorkspaceConfig();
        }

        try
        {
            var json = File.ReadAllText(path);
            var config = JsonSerializer.Deserialize<WorkspaceConfig>(json, JsonOptions) ?? new WorkspaceConfig();
            config.Editions = new Dictionary<string, EditionConfig>(
                config.Editions ?? [],
                StringComparer.OrdinalIgnoreCase);
            return config;
        }
        catch
        {
            return new WorkspaceConfig();
        }
    }

    public static void Save(WorkspaceConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        var path = GetConfigPath();
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    public static ActiveWorkspace? TryGetActiveWorkspace()
    {
        var config = Load();
        if (string.IsNullOrWhiteSpace(config.ActiveEditionId)
            || !config.Editions.TryGetValue(config.ActiveEditionId, out var edition)
            || string.IsNullOrWhiteSpace(edition.WorkingPakPath))
        {
            return null;
        }

        var editionId = GameEditionDetector.SanitizeEditionId(config.ActiveEditionId);
        var baselinePath = Path.Combine(
            GetBaselinesDirectory(),
            $"initial.baseline.{editionId}.pak");

        return new ActiveWorkspace(
            editionId,
            string.IsNullOrWhiteSpace(edition.DisplayName) ? editionId : edition.DisplayName,
            Path.GetFullPath(edition.WorkingPakPath),
            baselinePath,
            File.Exists(baselinePath));
    }

    public static void SetActiveEdition(string editionId, string displayName, string workingPakPath)
    {
        var config = Load();
        var id = GameEditionDetector.SanitizeEditionId(editionId);
        config.ActiveEditionId = id;
        if (!config.Editions.TryGetValue(id, out var edition))
        {
            edition = new EditionConfig();
        }

        edition.DisplayName = displayName;
        edition.WorkingPakPath = Path.GetFullPath(workingPakPath);
        config.Editions[id] = edition;
        Save(config);
    }

    public static void UpdateEditionFingerprints(
        string editionId,
        PakFingerprintSnapshot? baselineFingerprint = null,
        string? workingPakPath = null,
        PakFingerprintSnapshot? workingFingerprint = null)
    {
        var config = Load();
        var id = GameEditionDetector.SanitizeEditionId(editionId);
        if (!config.Editions.TryGetValue(id, out var edition))
        {
            edition = new EditionConfig();
            config.Editions[id] = edition;
        }

        if (baselineFingerprint is not null)
        {
            edition.BaselineFingerprint = baselineFingerprint;
        }

        if (workingFingerprint is not null)
        {
            edition.LastKnownWorkingFingerprint = workingFingerprint;
        }
        else if (!string.IsNullOrWhiteSpace(workingPakPath) && File.Exists(workingPakPath))
        {
            edition.LastKnownWorkingFingerprint = PakFingerprintService.ComputeFileFingerprint(workingPakPath);
        }

        if (!string.IsNullOrWhiteSpace(workingPakPath))
        {
            edition.WorkingPakPath = Path.GetFullPath(workingPakPath);
        }

        Save(config);
    }

    public static bool GetSidebarPinned() => Load().SidebarPinned;

    public static void SetSidebarPinned(bool pinned)
    {
        var config = Load();
        config.SidebarPinned = pinned;
        Save(config);
    }

    public static string GetThemeMode() => ThemeModes.Normalize(Load().ThemeMode);

    public static void SetThemeMode(string themeMode)
    {
        var config = Load();
        config.ThemeMode = ThemeModes.Normalize(themeMode);
        Save(config);
    }

    public static string GetUiCulture() => LanguageCatalog.NormalizeUiCulture(Load().UiCulture);

    public static void SetUiCulture(string uiCulture)
    {
        var config = Load();
        config.UiCulture = LanguageCatalog.NormalizeUiCulture(uiCulture);
        Save(config);
    }

    public static string? GetSkippedAppVersion() => Load().SkippedAppVersion;

    public static void SetSkippedAppVersion(string? version)
    {
        var config = Load();
        config.SkippedAppVersion = string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        Save(config);
    }

    public static string? TryResolveEditionId(string workingPakPath)
    {
        var full = Path.GetFullPath(workingPakPath);
        var config = Load();
        foreach (var pair in config.Editions)
        {
            if (string.Equals(
                    Path.GetFullPath(pair.Value.WorkingPakPath),
                    full,
                    StringComparison.OrdinalIgnoreCase))
            {
                return GameEditionDetector.SanitizeEditionId(pair.Key);
            }
        }

        if (!string.IsNullOrWhiteSpace(config.ActiveEditionId)
            && config.Editions.TryGetValue(config.ActiveEditionId, out var active)
            && string.Equals(
                Path.GetFullPath(active.WorkingPakPath),
                full,
                StringComparison.OrdinalIgnoreCase))
        {
            return GameEditionDetector.SanitizeEditionId(config.ActiveEditionId);
        }

        return null;
    }
}
