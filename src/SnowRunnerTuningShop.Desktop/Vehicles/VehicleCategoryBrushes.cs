using Avalonia.Media;

namespace SnowRunnerTuningShop.Desktop.Vehicles;

public static class VehicleCategoryBrushes
{
    public static IBrush ForCategory(string? category)
    {
        var color = category?.Trim() switch
        {
            "Highway" => Color.FromRgb(0x3D, 0x5A, 0x80),
            "Heavy Duty" => Color.FromRgb(0xC4, 0x5C, 0x26),
            "Heavy" => Color.FromRgb(0x8B, 0x1E, 0x1E),
            "Offroad" => Color.FromRgb(0x4A, 0x7C, 0x3F),
            "Scout" => Color.FromRgb(0x2A, 0x6F, 0x7A),
            _ => Color.FromRgb(0x4A, 0x4A, 0x4A),
        };

        return new SolidColorBrush(color);
    }
}
