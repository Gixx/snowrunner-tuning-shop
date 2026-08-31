using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.PhotoMode;

internal static class PhotoModeCacheBlockEditor
{
    private const string LineEnding = "\r\n";

    private static readonly Regex DefaultValueRegex = new(
        @"(?<prefix>defaultValue\s+=\s+)(?<value>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly Regex WeatherPresetListRegex = new(
        "title\\s+=\\s+\"UI_PHOTO_MODE_WEATHER\"" + LineEnding +
        "\\s+presetUiNames\\s+=\\s+\\[" + LineEnding +
        "(?<body>(?:\\s+\"UI_PHOTO_MODE_WEATHER_[^\"]+\",?" + LineEnding + ")+)" +
        "(?<suffix>\\s+\\])",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal static PhotoModeSettings ReadSettings(byte[] cacheBlock)
    {
        var text = Encoding.UTF8.GetString(cacheBlock);
        EnsurePhotoModeSectionPresent(text);

        return new PhotoModeSettings
        {
            WeatherPresetKey = ReadFirstWeatherPreset(text),
            Exposure = ReadSliderDefault(text, PhotoModeSettingKeys.Exposure, isInteger: false),
            Contrast = ReadSliderDefault(text, PhotoModeSettingKeys.Contrast, isInteger: false),
            Hue = ReadSliderDefault(text, PhotoModeSettingKeys.Hue, isInteger: false),
            Saturation = ReadSliderDefault(text, PhotoModeSettingKeys.Saturation, isInteger: false),
            ColorGrading = (int)ReadSliderDefault(text, PhotoModeSettingKeys.ColorGrading, isInteger: true),
            ColorGradingIntensity = ReadSliderDefault(text, PhotoModeSettingKeys.ColorGradingIntensity, isInteger: false),
            Vignette = ReadSliderDefault(text, PhotoModeSettingKeys.Vignette, isInteger: false),
            FilmGrain = ReadSliderDefault(text, PhotoModeSettingKeys.FilmGrain, isInteger: false),
            FieldOfView = (int)ReadSliderDefault(text, PhotoModeSettingKeys.Fov, isInteger: true),
            Aperture = (int)ReadSliderDefault(text, PhotoModeSettingKeys.Aperture, isInteger: true),
            FocusPoint = ReadSliderDefault(text, PhotoModeSettingKeys.FocusPoint, isInteger: false),
            FocusSpan = ReadSliderDefault(text, PhotoModeSettingKeys.FocusSpan, isInteger: false),
        };
    }

    internal static byte[] ApplySettings(byte[] cacheBlock, PhotoModeSettings settings)
    {
        var text = Encoding.UTF8.GetString(cacheBlock);
        EnsurePhotoModeSectionPresent(text);

        text = WriteSliderDefault(text, PhotoModeSettingKeys.Exposure, settings.Exposure, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Contrast, settings.Contrast, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Hue, settings.Hue, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Saturation, settings.Saturation, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.ColorGrading, settings.ColorGrading, isInteger: true);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.ColorGradingIntensity, settings.ColorGradingIntensity, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Vignette, settings.Vignette, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.FilmGrain, settings.FilmGrain, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Fov, settings.FieldOfView, isInteger: true);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.Aperture, settings.Aperture, isInteger: true);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.FocusPoint, settings.FocusPoint, isInteger: false);
        text = WriteSliderDefault(text, PhotoModeSettingKeys.FocusSpan, settings.FocusSpan, isInteger: false);
        text = WriteWeatherDefaultFirst(text, settings.WeatherPresetKey);

        return Encoding.UTF8.GetBytes(text);
    }

    private static void EnsurePhotoModeSectionPresent(string text)
    {
        if (!text.Contains(PhotoModeSettingKeys.Aperture, StringComparison.Ordinal))
        {
            throw new PhotoModeLoadException("Photo mode settings were not found in initial.cache_block.");
        }
    }

    private static string ReadFirstWeatherPreset(string text)
    {
        var match = WeatherPresetListRegex.Match(text);
        if (!match.Success)
        {
            throw new PhotoModeLoadException("Photo mode weather presets were not found in initial.cache_block.");
        }

        var firstLine = match.Groups["body"].Value.Split(LineEnding, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
        var key = firstLine.Trim().Trim('"', ',');
        return key;
    }

    private static string WriteWeatherDefaultFirst(string text, string presetKey)
    {
        if (!PhotoModeSettingKeys.WeatherPresets.Contains(presetKey, StringComparer.Ordinal))
        {
            throw new PhotoModeLoadException($"Unknown weather preset key: {presetKey}");
        }

        var match = WeatherPresetListRegex.Match(text);
        if (!match.Success)
        {
            throw new PhotoModeLoadException("Photo mode weather presets were not found in initial.cache_block.");
        }

        var ordered = PhotoModeSettingKeys.WeatherPresets
            .OrderBy(key => key.Equals(presetKey, StringComparison.Ordinal) ? 0 : 1)
            .ThenBy(key => Array.IndexOf(PhotoModeSettingKeys.WeatherPresets, key))
            .ToArray();

        return WeatherPresetListRegex.Replace(
            text,
            current =>
            {
                var indent = new string(' ', current.Groups["body"].Value.TakeWhile(char.IsWhiteSpace).Count());
                var rebuiltBody = string.Join(
                    LineEnding,
                    ordered.Select((key, index) =>
                    {
                        var suffix = index < ordered.Length - 1 ? "," : "";
                        return $"{indent}\"{key}\"{suffix}";
                    })) + LineEnding;

                return current.Value.Replace(current.Groups["body"].Value, rebuiltBody);
            },
            1);
    }

    private static double ReadSliderDefault(string text, string titleKey, bool isInteger)
    {
        var block = FindControllerBlock(text, titleKey);
        var match = DefaultValueRegex.Match(block);
        if (!match.Success)
        {
            throw new PhotoModeLoadException($"Missing defaultValue for {titleKey}.");
        }

        var raw = match.Groups["value"].Value.Trim();
        return isInteger
            ? int.Parse(raw, CultureInfo.InvariantCulture)
            : double.Parse(raw, CultureInfo.InvariantCulture);
    }

    private static string WriteSliderDefault(string text, string titleKey, double value, bool isInteger)
    {
        var block = FindControllerBlock(text, titleKey);
        var formatted = isInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : value.ToString("0.##", CultureInfo.InvariantCulture);

        var updatedBlock = DefaultValueRegex.Replace(
            block,
            m => $"{m.Groups["prefix"].Value}{formatted}",
            1);

        var blockIndex = text.IndexOf(block, StringComparison.Ordinal);
        if (blockIndex < 0)
        {
            throw new PhotoModeLoadException($"Could not update {titleKey}.");
        }

        return text.Remove(blockIndex, block.Length).Insert(blockIndex, updatedBlock);
    }

    private static string FindControllerBlock(string text, string titleKey)
    {
        var marker = $"title   =   \"{titleKey}\"";
        var start = text.IndexOf(marker, StringComparison.Ordinal);
        if (start < 0)
        {
            throw new PhotoModeLoadException($"Photo mode setting {titleKey} was not found.");
        }

        var end = text.IndexOf("__type", start, StringComparison.Ordinal);
        if (end < 0)
        {
            end = start + 400;
        }
        else
        {
            end = text.IndexOf('\n', end);
            if (end < 0)
            {
                end = start + 400;
            }
        }

        return text[start..end];
    }
}
