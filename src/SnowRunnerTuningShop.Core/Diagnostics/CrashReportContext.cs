namespace SnowRunnerTuningShop.Core.Diagnostics;

public sealed record CrashSessionSnapshot(
    bool HasPak,
    string? PakPath,
    string? EditionDisplayName);

/// <summary>Lightweight context attached to crash reports (current page, vehicle, pak).</summary>
public static class CrashReportContext
{
    public static string? CurrentPage { get; private set; }

    public static string? VehicleId { get; private set; }

    public static string? VehicleDisplayName { get; private set; }

    public static Func<CrashSessionSnapshot>? SessionProvider { get; set; }

    public static void SetPage(string page)
    {
        CurrentPage = page;
        VehicleId = null;
        VehicleDisplayName = null;
    }

    public static void SetVehicle(string id, string displayName)
    {
        VehicleId = id;
        VehicleDisplayName = displayName;
    }

    public static void ClearVehicle()
    {
        VehicleId = null;
        VehicleDisplayName = null;
    }

    public static CrashSessionSnapshot? GetSession() => SessionProvider?.Invoke();
}
