namespace SnowRunnerTuningShop.Core.PhotoMode;

public static class PhotoModeSettingKeys
{
    public const string Exposure = "UI_PHOTO_MODE_EXPOSURE";
    public const string Contrast = "UI_PHOTO_MODE_CONTRAST";
    public const string Hue = "UI_PHOTO_MODE_HUE";
    public const string Saturation = "UI_PHOTO_MODE_SATURATION";
    public const string ColorGrading = "UI_PHOTO_MODE_COLOR_GRADING";
    public const string ColorGradingIntensity = "UI_PHOTO_MODE_COLOR_GRADING_ITENSITY";
    public const string Vignette = "UI_PHOTO_MODE_VIGNETTE";
    public const string FilmGrain = "UI_PHOTO_MODE_FILM_GRAIN";
    public const string Fov = "UI_PHOTO_MODE_FOV";
    public const string Aperture = "UI_PHOTO_MODE_APERTURE";
    public const string FocusPoint = "UI_PHOTO_MODE_FOCUS_POINT";
    public const string FocusSpan = "UI_PHOTO_MODE_FOCUS_SPAN";

    public const string WeatherDefault = "UI_PHOTO_MODE_WEATHER_DEFAULT";
    public const string WeatherClearSky = "UI_PHOTO_MODE_WEATHER_CLEAR_SKY";
    public const string WeatherLightRain = "UI_PHOTO_MODE_WEATHER_LIGHT_RAIN";
    public const string WeatherHeavyRain = "UI_PHOTO_MODE_WEATHER_HEAVY_RAIN";
    public const string WeatherHeavySnow = "UI_PHOTO_MODE_WEATHER_HEAVY_SNOW";

    public static readonly string[] WeatherPresets =
    [
        WeatherDefault,
        WeatherClearSky,
        WeatherLightRain,
        WeatherHeavyRain,
        WeatherHeavySnow,
    ];
}
