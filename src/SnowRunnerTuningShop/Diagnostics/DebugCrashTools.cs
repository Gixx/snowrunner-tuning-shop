using SnowRunnerTuningShop.Core.Diagnostics;

namespace SnowRunnerTuningShop.Diagnostics;

/// <summary>Helpers for manually exercising crash reporting (Settings debug panel).</summary>
public static class DebugCrashTools
{
    public static void ThrowUiTestCrash()
    {
#if DEBUG
        CrashReportContext.SetPage("Settings (debug test)");
        CrashReportContext.SetVehicle("debug_test_vehicle", "Debug Test Truck");
        throw new InvalidOperationException("DEBUG test crash from Settings (UI thread).");
#else
        throw new NotSupportedException("Debug crash tools are only available in Debug builds.");
#endif
    }

    public static void ThrowVehiclePageTestCrash()
    {
#if DEBUG
        CrashReportContext.SetPage("Vehicles");
        CrashReportContext.SetVehicle("debug_test_vehicle", "Debug Test Truck");
        throw new InvalidOperationException(
            "DEBUG test crash simulating a vehicle detail page failure.");
#else
        throw new NotSupportedException("Debug crash tools are only available in Debug builds.");
#endif
    }
}
