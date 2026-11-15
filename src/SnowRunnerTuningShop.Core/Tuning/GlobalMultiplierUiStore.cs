using System.Text.Json;
using System.Text.Json.Serialization;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop.Core.Tuning;

/// <summary>
/// Persists last-used global multiplier box UI state per active preset under AppData.
/// </summary>
public static class GlobalMultiplierUiStore
{
    public const string NonePresetKey = "_none";

    private static readonly object Gate = new();
    private static string? _testStoreDirectory;
    private static string? _testPresetKey;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>Raised after the active preset key used by this store may have changed.</summary>
    public static event EventHandler? ActivePresetChanged;

    public static string GetStorePath()
    {
        if (!string.IsNullOrWhiteSpace(_testStoreDirectory))
        {
            return Path.Combine(_testStoreDirectory, "global-multiplier-ui.json");
        }

        return Path.Combine(WorkspaceConfigStore.GetAppDataDirectory(), "global-multiplier-ui.json");
    }

    public static string ResolvePresetKey()
    {
        if (_testPresetKey is not null)
        {
            return string.IsNullOrWhiteSpace(_testPresetKey) ? NonePresetKey : _testPresetKey.Trim();
        }

        var (id, _) = WorkspaceConfigStore.GetActivePreset();
        return string.IsNullOrWhiteSpace(id) ? NonePresetKey : id.Trim();
    }

    /// <summary>Notify UI that the active preset (and therefore stored slider state) may have changed.</summary>
    public static void NotifyActivePresetChanged() =>
        ActivePresetChanged?.Invoke(null, EventArgs.Empty);

    public static IDisposable BeginTestScope(string storeDirectory, string? presetKey = NonePresetKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeDirectory);
        Directory.CreateDirectory(storeDirectory);
        lock (Gate)
        {
            _testStoreDirectory = storeDirectory;
            _testPresetKey = presetKey ?? NonePresetKey;
        }

        return new TestScope();
    }

    public static void SetTestPresetKey(string? presetKey)
    {
        lock (Gate)
        {
            if (_testStoreDirectory is null)
            {
                throw new InvalidOperationException("Call BeginTestScope before SetTestPresetKey.");
            }

            _testPresetKey = string.IsNullOrWhiteSpace(presetKey) ? NonePresetKey : presetKey.Trim();
        }
    }

    public static GlobalMultiplierUiPresetState GetCurrent()
    {
        lock (Gate)
        {
            var doc = LoadUnlocked();
            var key = ResolvePresetKey();
            if (!doc.ByPresetId.TryGetValue(key, out var state) || state is null)
            {
                state = new GlobalMultiplierUiPresetState();
                doc.ByPresetId[key] = state;
            }

            return state;
        }
    }

    /// <summary>Deep-clone of the current preset's UI state (for embedding in .tsa packages).</summary>
    public static GlobalMultiplierUiPresetState CaptureCurrentSnapshot()
    {
        lock (Gate)
        {
            var doc = LoadUnlocked();
            var key = ResolvePresetKey();
            if (!doc.ByPresetId.TryGetValue(key, out var state) || state is null)
            {
                return new GlobalMultiplierUiPresetState();
            }

            return Clone(state);
        }
    }

    /// <summary>Replace stored UI state for a preset id (used when applying/importing a .tsa).</summary>
    public static void ReplaceForPreset(string? presetId, GlobalMultiplierUiPresetState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var key = string.IsNullOrWhiteSpace(presetId) ? NonePresetKey : presetId.Trim();
        lock (Gate)
        {
            var doc = LoadUnlocked();
            doc.ByPresetId[key] = Clone(state);
            SaveUnlocked(doc);
        }
    }

    public static void UpdateCurrent(Action<GlobalMultiplierUiPresetState> mutate)
    {
        ArgumentNullException.ThrowIfNull(mutate);
        lock (Gate)
        {
            var doc = LoadUnlocked();
            var key = ResolvePresetKey();
            if (!doc.ByPresetId.TryGetValue(key, out var state) || state is null)
            {
                state = new GlobalMultiplierUiPresetState();
                doc.ByPresetId[key] = state;
            }

            mutate(state);
            SaveUnlocked(doc);
        }
    }

    public static EngineGlobalUiState GetEngines() =>
        GetCurrent().Engines ??= new EngineGlobalUiState();

    public static void SetEngines(EngineGlobalUiState state) =>
        UpdateCurrent(s => s.Engines = Clone(state));

    public static GearboxGlobalUiState GetGearboxes() =>
        GetCurrent().Gearboxes ??= new GearboxGlobalUiState();

    public static void SetGearboxes(GearboxGlobalUiState state) =>
        UpdateCurrent(s => s.Gearboxes = Clone(state));

    public static SuspensionGlobalUiState GetSuspensions() =>
        GetCurrent().Suspensions ??= new SuspensionGlobalUiState();

    public static void SetSuspensions(SuspensionGlobalUiState state) =>
        UpdateCurrent(s => s.Suspensions = Clone(state));

    public static WinchGlobalUiState GetWinches() =>
        GetCurrent().Winches ??= new WinchGlobalUiState();

    public static void SetWinches(WinchGlobalUiState state) =>
        UpdateCurrent(s => s.Winches = Clone(state));

    public static TireGlobalUiState GetTires() =>
        GetCurrent().Tires ??= new TireGlobalUiState();

    public static void SetTires(TireGlobalUiState state) =>
        UpdateCurrent(s => s.Tires = Clone(state));

    public static CraneGlobalUiState GetCranes() =>
        GetCurrent().Cranes ??= new CraneGlobalUiState();

    public static void SetCranes(CraneGlobalUiState state) =>
        UpdateCurrent(s => s.Cranes = Clone(state));

    public static AddonGlobalUiState GetAddons() =>
        GetCurrent().Addons ??= new AddonGlobalUiState();

    public static void SetAddons(AddonGlobalUiState state) =>
        UpdateCurrent(s => s.Addons = Clone(state));

    public static TrailerGlobalUiState GetTrailers() =>
        GetCurrent().Trailers ??= new TrailerGlobalUiState();

    public static void SetTrailers(TrailerGlobalUiState state) =>
        UpdateCurrent(s => s.Trailers = Clone(state));

    public static CargoGlobalUiState GetCargo() =>
        GetCurrent().Cargo ??= new CargoGlobalUiState();

    public static void SetCargo(CargoGlobalUiState state) =>
        UpdateCurrent(s => s.Cargo = Clone(state));

    public static VehicleGlobalUiState GetVehicles() =>
        GetCurrent().Vehicles ??= new VehicleGlobalUiState();

    public static void SetVehicles(VehicleGlobalUiState state) =>
        UpdateCurrent(s => s.Vehicles = Clone(state));

    public static void ResetEngines() => SetEngines(new EngineGlobalUiState());
    public static void ResetGearboxes() => SetGearboxes(new GearboxGlobalUiState());
    public static void ResetSuspensions() => SetSuspensions(new SuspensionGlobalUiState());
    public static void ResetWinches() => SetWinches(new WinchGlobalUiState());
    public static void ResetTires() => SetTires(new TireGlobalUiState());
    public static void ResetCranes() => SetCranes(new CraneGlobalUiState());
    public static void ResetAddons() => SetAddons(new AddonGlobalUiState());
    public static void ResetTrailers() => SetTrailers(new TrailerGlobalUiState());
    public static void ResetCargo() => SetCargo(new CargoGlobalUiState());
    public static void ResetVehicles() => SetVehicles(new VehicleGlobalUiState());

    private static T Clone<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonOptions), JsonOptions)
        ?? throw new InvalidOperationException("Failed to clone UI state.");

    private static GlobalMultiplierUiDocument LoadUnlocked()
    {
        var path = GetStorePath();
        if (!File.Exists(path))
        {
            return new GlobalMultiplierUiDocument();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<GlobalMultiplierUiDocument>(json, JsonOptions)
                   ?? new GlobalMultiplierUiDocument();
        }
        catch
        {
            return new GlobalMultiplierUiDocument();
        }
    }

    private static void SaveUnlocked(GlobalMultiplierUiDocument document)
    {
        var path = GetStorePath();
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(path, json);
    }

    private sealed class TestScope : IDisposable
    {
        public void Dispose()
        {
            lock (Gate)
            {
                _testStoreDirectory = null;
                _testPresetKey = null;
            }
        }
    }
}
