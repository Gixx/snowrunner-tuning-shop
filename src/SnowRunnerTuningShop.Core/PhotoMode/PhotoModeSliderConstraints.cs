namespace SnowRunnerTuningShop.Core.PhotoMode;

public sealed record PhotoModeSliderConstraint(
    string SettingKey,
    IReadOnlyList<double> AllowedValues,
    int FieldWidth);

public static class PhotoModeSliderConstraints
{
    public static IReadOnlyList<PhotoModeSliderConstraint> Resolve(byte[] cacheBlock)
    {
        var text = System.Text.Encoding.Latin1.GetString(cacheBlock);
        return
        [
            ResolveSlider(text, PhotoModeSettingKeys.Exposure, -0.5, 0.5, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.Contrast, 0, 2, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.Hue, -3, 3, 0.1, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.Saturation, 0, 2, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.ColorGrading, 0, 19, 1, isInteger: true),
            ResolveSlider(text, PhotoModeSettingKeys.ColorGradingIntensity, 0, 1, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.Vignette, 0, 1, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.FilmGrain, 0, 1, 0.05, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.Aperture, 0, 200, 1, isInteger: true),
            ResolveSlider(text, PhotoModeSettingKeys.FocusPoint, 5, 50, 0.25, isInteger: false),
            ResolveSlider(text, PhotoModeSettingKeys.FocusSpan, 5, 200, 0.5, isInteger: false),
        ];
    }

    private static PhotoModeSliderConstraint ResolveSlider(
        string text,
        string settingKey,
        double min,
        double max,
        double step,
        bool isInteger)
    {
        var fieldWidth = PhotoModeCacheBlockEditor.GetDefaultValueFieldWidth(text, settingKey);
        var allowed = new List<double>();
        var steps = (int)Math.Round((max - min) / step) + 1;
        for (var index = 0; index < steps; index++)
        {
            var value = min + (index * step);
            if (isInteger)
            {
                value = Math.Round(value);
            }
            else
            {
                value = Math.Round(value / step) * step;
            }

            if (value < min - 0.0001 || value > max + 0.0001)
            {
                continue;
            }

            if (!PhotoModeValueFormatting.FitsInField(value, isInteger, fieldWidth))
            {
                continue;
            }

            if (allowed.Count == 0 || Math.Abs(allowed[^1] - value) > 0.0001)
            {
                allowed.Add(value);
            }
        }

        if (allowed.Count == 0)
        {
            var current = PhotoModeCacheBlockEditor.ReadSliderDefaultPublic(text, settingKey, isInteger);
            allowed.Add(current);
        }

        return new PhotoModeSliderConstraint(settingKey, allowed, fieldWidth);
    }
}
