using Avalonia.Media;

namespace SnowRunnerTuningShop.Desktop.Trailers;

public static class TrailerCategoryBrushes
{
    public static IBrush ForHitch(string? hitch)
    {
        var color = hitch?.Trim().ToLowerInvariant() switch
        {
            "scout" => Color.FromRgb(0x2A, 0x6F, 0x7A),
            "standard" => Color.FromRgb(0x4A, 0x7C, 0x3F),
            "saddle-low" => Color.FromRgb(0xC4, 0x5C, 0x26),
            "saddle-high" => Color.FromRgb(0x8B, 0x1E, 0x1E),
            "other" => Color.FromRgb(0x5C, 0x3D, 0x7A),
            _ => Color.FromRgb(0x4A, 0x4A, 0x4A),
        };

        return new SolidColorBrush(color);
    }
}
