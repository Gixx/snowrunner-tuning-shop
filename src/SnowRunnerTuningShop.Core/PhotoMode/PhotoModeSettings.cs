namespace SnowRunnerTuningShop.Core.PhotoMode;

public sealed class PhotoModeSettings
{
    public int TimeIndex { get; init; } = PhotoModeTimeIndex.GameDefault;

    /// <summary>UI key of the weather preset that should appear first (index 0 on open).</summary>
    public string WeatherPresetKey { get; init; } = PhotoModeSettingKeys.WeatherDefault;

    public double Exposure { get; init; }
    public double Contrast { get; init; } = 1;
    public double Hue { get; init; }
    public double Saturation { get; init; } = 1;
    public int ColorGrading { get; init; }
    public double ColorGradingIntensity { get; init; } = 0.35;
    public double Vignette { get; init; } = 0.35;
    public double FilmGrain { get; init; } = 0.35;
    public int FieldOfView { get; init; } = 80;
    public int Aperture { get; init; } = 30;
    public double FocusPoint { get; init; } = 12;
    public double FocusSpan { get; init; } = 20;

    public static PhotoModeSettings Vanilla { get; } = new();

    public PhotoModeSettings With(
        int? timeIndex = null,
        string? weatherPresetKey = null,
        double? exposure = null,
        double? contrast = null,
        double? hue = null,
        double? saturation = null,
        int? colorGrading = null,
        double? colorGradingIntensity = null,
        double? vignette = null,
        double? filmGrain = null,
        int? fieldOfView = null,
        int? aperture = null,
        double? focusPoint = null,
        double? focusSpan = null)
    {
        return new PhotoModeSettings
        {
            TimeIndex = timeIndex ?? TimeIndex,
            WeatherPresetKey = weatherPresetKey ?? WeatherPresetKey,
            Exposure = exposure ?? Exposure,
            Contrast = contrast ?? Contrast,
            Hue = hue ?? Hue,
            Saturation = saturation ?? Saturation,
            ColorGrading = colorGrading ?? ColorGrading,
            ColorGradingIntensity = colorGradingIntensity ?? ColorGradingIntensity,
            Vignette = vignette ?? Vignette,
            FilmGrain = filmGrain ?? FilmGrain,
            FieldOfView = fieldOfView ?? FieldOfView,
            Aperture = aperture ?? Aperture,
            FocusPoint = focusPoint ?? FocusPoint,
            FocusSpan = focusSpan ?? FocusSpan,
        };
    }
}

public sealed record PhotoModeSaveResult(int UpdatedEntries);

public sealed class PhotoModeLoadException : Exception
{
    public PhotoModeLoadException(string message) : base(message)
    {
    }
}
