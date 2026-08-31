namespace SnowRunnerTuningShop.Core.PhotoMode;

/// <summary>
/// Flat photo mode time preset index stored in sslbundle (0 = game default slot).
/// Preset slots 0–17 map to Morning 1 … Night 3.
/// </summary>
public static class PhotoModeTimeIndex
{
    public const int GameDefault = 0;
    public const int PresetMinimum = 0;
    public const int PresetMaximum = 17;

    public static string Format(int index)
    {
        if (index == GameDefault)
        {
            return "Default";
        }

        if (index is < PresetMinimum or > PresetMaximum)
        {
            return $"Preset {index}";
        }

        var period = index switch
        {
            <= 4 => "Morning",
            <= 9 => "Afternoon",
            <= 14 => "Evening",
            _ => "Night",
        };

        var step = index switch
        {
            <= 4 => index + 1,
            <= 9 => index - 4,
            <= 14 => index - 9,
            _ => index - 14,
        };

        return $"{period} {step}";
    }

    public static IReadOnlyList<(int Index, string Label)> AllChoices()
    {
        var list = new List<(int, string)> { (GameDefault, Format(GameDefault)) };
        for (var i = 1; i <= PresetMaximum; i++)
        {
            list.Add((i, Format(i)));
        }

        return list;
    }
}
