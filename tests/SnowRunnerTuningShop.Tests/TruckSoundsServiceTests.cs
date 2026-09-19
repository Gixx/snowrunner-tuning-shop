using SnowRunnerTuningShop.Core.Trucks;

namespace SnowRunnerTuningShop.Tests;

public sealed class TruckSoundsServiceTests
{
    private const string SampleTruckXml =
        """
        <Truck>
          <TruckData>
            <Sounds MinDist="8.0">
              <Honk Sound="trucks/old_set/old_set_honk" />
              <EngineStart Sound="trucks/old_set/old_set_start" />
              <EngineStop Sound="trucks/old_set/old_set_stop" />
              <EngineIdle Sound="trucks/old_set/old_set_idle" />
              <EngineIdle_2d Sound="trucks/old_set/old_set_idle_2d" IsSound2D="true" />
              <EngineLow Sound="trucks/old_set/old_set_low" />
              <EngineHigh Sound="trucks/old_set/old_set_high" />
              <EngineRev Sound="trucks/old_set/old_set_rev" />
              <EngineAccel Sound="trucks/old_set/old_set_acc" />
              <Gear Sound="trucks/common/truck_gear_shift" />
            </Sounds>
          </TruckData>
        </Truck>
        """;

    [Fact]
    public void ReadAssignment_detects_horn_and_engine_set_ids()
    {
        var assignment = TruckSoundsService.ReadAssignment(SampleTruckXml);

        Assert.Equal("old_set", assignment.HornSoundSetId);
        Assert.Equal("old_set", assignment.EngineSoundSetId);
        Assert.Equal("trucks/old_set/old_set_idle", assignment.EngineIdlePath);
        Assert.Equal("trucks/old_set/old_set_high", assignment.EngineHighPath);
    }

    [Fact]
    public void ApplyAssignment_updates_honk_and_engine_tags_from_donor_set()
    {
        var set = new TruckSoundSetDefinition("ford_f750")
        {
            HonkPath = "trucks/ford_f750/ford_f750_honk",
        };
        set.EnginePaths["EngineIdle"] = "trucks/ford_f750/ford_f750_idle";
        set.EnginePaths["EngineHigh"] = "trucks/ford_f750/ford_f750_high";
        set.EnginePaths["EngineStart"] = "trucks/ford_f750/ford_f750_start";
        set.EnginePaths["EngineTurbo"] = "trucks/ford_f750/ford_f750_turbo";

        var catalog = new TruckSoundCatalog([set]);
        var updated = TruckSoundsService.ApplyAssignment(
            SampleTruckXml,
            catalog,
            hornSoundSetId: "ford_f750",
            engineSoundSetId: "ford_f750");

        Assert.Contains("""Honk Sound="trucks/ford_f750/ford_f750_honk" """, updated, StringComparison.Ordinal);
        Assert.Contains("""EngineIdle Sound="trucks/ford_f750/ford_f750_idle" """, updated, StringComparison.Ordinal);
        Assert.Contains("""EngineHigh Sound="trucks/ford_f750/ford_f750_high" """, updated, StringComparison.Ordinal);
        Assert.Contains("""EngineStart Sound="trucks/ford_f750/ford_f750_start" """, updated, StringComparison.Ordinal);
        Assert.Contains("""EngineTurbo		Sound="trucks/ford_f750/ford_f750_turbo" """, updated, StringComparison.Ordinal);
        Assert.Contains("""Gear Sound="trucks/common/truck_gear_shift" """, updated, StringComparison.Ordinal);
        Assert.Contains("""EngineStop Sound="trucks/old_set/old_set_stop" """, updated, StringComparison.Ordinal);
    }
}
