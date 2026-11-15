using SnowRunnerTuningShop.Core.Tuning;

namespace SnowRunnerTuningShop.Tests;

public sealed class GlobalMultiplierUiStoreTests
{
    [Fact]
    public void Persists_engines_per_preset_and_isolates_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), "srts-gmu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (GlobalMultiplierUiStore.BeginTestScope(root, "preset-a"))
            {
                GlobalMultiplierUiStore.SetEngines(new EngineGlobalUiState
                {
                    TorqueIndex = 12,
                    FuelIndex = 4,
                    DamageIndex = 10,
                    ResponsivenessIndex = 11,
                    PriceIndex = 3,
                });

                var loaded = GlobalMultiplierUiStore.GetEngines();
                Assert.Equal(12, loaded.TorqueIndex);
                Assert.Equal(4, loaded.FuelIndex);

                GlobalMultiplierUiStore.SetTestPresetKey("preset-b");
                var other = GlobalMultiplierUiStore.GetEngines();
                Assert.Equal(TuningMultiplierPresets.BaselineIndex, other.TorqueIndex);
                Assert.Equal(TuningMultiplierPresets.BaselineIndex, other.FuelIndex);

                GlobalMultiplierUiStore.SetTestPresetKey("preset-a");
                var again = GlobalMultiplierUiStore.GetEngines();
                Assert.Equal(12, again.TorqueIndex);
                Assert.Equal(4, again.FuelIndex);
            }

            Assert.True(File.Exists(Path.Combine(root, "global-multiplier-ui.json")));
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void Reset_clears_category_to_baseline_defaults()
    {
        var root = Path.Combine(Path.GetTempPath(), "srts-gmu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (GlobalMultiplierUiStore.BeginTestScope(root))
            {
                GlobalMultiplierUiStore.SetTires(new TireGlobalUiState
                {
                    DamageIndex = 14,
                    IgnoreIceAll = true,
                });
                GlobalMultiplierUiStore.ResetTires();
                var state = GlobalMultiplierUiStore.GetTires();
                Assert.Equal(TuningMultiplierPresets.BaselineIndex, state.DamageIndex);
                Assert.False(state.IgnoreIceAll);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void Capture_and_ReplaceForPreset_round_trip_across_keys()
    {
        var root = Path.Combine(Path.GetTempPath(), "srts-gmu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (GlobalMultiplierUiStore.BeginTestScope(root, "source"))
            {
                GlobalMultiplierUiStore.SetEngines(new EngineGlobalUiState { TorqueIndex = 16 });
                var snapshot = GlobalMultiplierUiStore.CaptureCurrentSnapshot();
                Assert.Equal(16, snapshot.Engines!.TorqueIndex);

                GlobalMultiplierUiStore.ReplaceForPreset("imported", snapshot);
                GlobalMultiplierUiStore.SetTestPresetKey("imported");
                Assert.Equal(16, GlobalMultiplierUiStore.GetEngines().TorqueIndex);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }

    [Fact]
    public void Vehicles_and_winch_flags_round_trip()
    {
        var root = Path.Combine(Path.GetTempPath(), "srts-gmu-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            using (GlobalMultiplierUiStore.BeginTestScope(root, "easy"))
            {
                GlobalMultiplierUiStore.SetVehicles(new VehicleGlobalUiState
                {
                    FuelIndex = 15,
                    FastTuningLowerCom = true,
                    AlwaysOnAwd = true,
                    UnlockAllVehicles = true,
                });
                GlobalMultiplierUiStore.SetWinches(new WinchGlobalUiState
                {
                    LengthIndex = 13,
                    AutonomousAll = true,
                });

                var vehicles = GlobalMultiplierUiStore.GetVehicles();
                Assert.Equal(15, vehicles.FuelIndex);
                Assert.True(vehicles.FastTuningLowerCom);
                Assert.True(vehicles.AlwaysOnAwd);
                Assert.True(vehicles.UnlockAllVehicles);

                var winches = GlobalMultiplierUiStore.GetWinches();
                Assert.Equal(13, winches.LengthIndex);
                Assert.True(winches.AutonomousAll);
            }
        }
        finally
        {
            try
            {
                Directory.Delete(root, recursive: true);
            }
            catch
            {
                // best-effort cleanup
            }
        }
    }
}
