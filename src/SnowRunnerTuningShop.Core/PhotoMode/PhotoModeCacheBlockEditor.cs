using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SnowRunnerTuningShop.Core.PhotoMode;

internal static class PhotoModeCacheBlockEditor
{
    private const string LineEnding = "\r\n";
    private const string ApertureControllerMarker = "title   =   \"UI_PHOTO_MODE_APERTURE\"";

    /// <summary>
    /// cache_block is mostly text but embeds binary chunks; Latin-1 preserves every byte.
    /// </summary>
    private static readonly Encoding CacheBlockEncoding = Encoding.Latin1;

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
        var text = CacheBlockEncoding.GetString(cacheBlock);
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
        ValidateSettingsFit(cacheBlock, settings);

        var text = CacheBlockEncoding.GetString(cacheBlock);
        EnsurePhotoModeSectionPresent(text);
        var updated = text;

        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Exposure, settings.Exposure, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Contrast, settings.Contrast, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Hue, settings.Hue, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Saturation, settings.Saturation, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.ColorGrading, settings.ColorGrading, isInteger: true);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.ColorGradingIntensity, settings.ColorGradingIntensity, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Vignette, settings.Vignette, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.FilmGrain, settings.FilmGrain, isInteger: false);
        // FOV in photo mode follows the gameplay camera setting; do not patch initial.cache_block.
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.Aperture, settings.Aperture, isInteger: true);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.FocusPoint, settings.FocusPoint, isInteger: false);
        updated = WriteSliderDefaultIfChanged(updated, PhotoModeSettingKeys.FocusSpan, settings.FocusSpan, isInteger: false);
        updated = WriteWeatherDefaultFirstIfChanged(updated, settings.WeatherPresetKey);

        return ReferenceEquals(updated, text) || updated == text
            ? cacheBlock
            : CacheBlockEncoding.GetBytes(updated);
    }

    internal static void ValidateAppliedSettings(byte[] cacheBlock, PhotoModeSettings settings)
    {
        var readBack = ReadSettings(cacheBlock);
        if (Math.Abs(readBack.Exposure - settings.Exposure) > 0.01
            || Math.Abs(readBack.Contrast - settings.Contrast) > 0.01
            || Math.Abs(readBack.Hue - settings.Hue) > 0.05
            || Math.Abs(readBack.Saturation - settings.Saturation) > 0.01
            || readBack.ColorGrading != settings.ColorGrading
            || Math.Abs(readBack.ColorGradingIntensity - settings.ColorGradingIntensity) > 0.01
            || Math.Abs(readBack.Vignette - settings.Vignette) > 0.01
            || Math.Abs(readBack.FilmGrain - settings.FilmGrain) > 0.01
            || readBack.Aperture != settings.Aperture
            || Math.Abs(readBack.FocusPoint - settings.FocusPoint) > 0.05
            || Math.Abs(readBack.FocusSpan - settings.FocusSpan) > 0.05
            || !readBack.WeatherPresetKey.Equals(settings.WeatherPresetKey, StringComparison.Ordinal))
        {
            throw new PhotoModeLoadException(
                "One or more photo mode values could not be written with the field widths in initial.cache_block. " +
                "Use smaller values or restore photo mode baseline first.");
        }
    }

    private static void ValidateSettingsFit(byte[] cacheBlock, PhotoModeSettings settings)
    {
        var text = CacheBlockEncoding.GetString(cacheBlock);
        EnsurePhotoModeSectionPresent(text);

        var errors = new List<string>();
        TryValidateSlider(text, PhotoModeSettingKeys.Exposure, settings.Exposure, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.Contrast, settings.Contrast, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.Hue, settings.Hue, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.Saturation, settings.Saturation, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.ColorGrading, settings.ColorGrading, isInteger: true, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.ColorGradingIntensity, settings.ColorGradingIntensity, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.Vignette, settings.Vignette, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.FilmGrain, settings.FilmGrain, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.Aperture, settings.Aperture, isInteger: true, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.FocusPoint, settings.FocusPoint, isInteger: false, errors);
        TryValidateSlider(text, PhotoModeSettingKeys.FocusSpan, settings.FocusSpan, isInteger: false, errors);

        if (errors.Count > 0)
        {
            throw new PhotoModeLoadException(string.Join(Environment.NewLine, errors));
        }
    }

    private static void TryValidateSlider(
        string text,
        string titleKey,
        double value,
        bool isInteger,
        List<string> errors)
    {
        try
        {
            var (start, block) = FindControllerBlock(text, titleKey);
            var match = DefaultValueRegex.Match(block);
            if (!match.Success)
            {
                return;
            }

            var valueStart = match.Groups["value"].Index;
            var lineEnd = block.IndexOf('\n', valueStart);
            if (lineEnd < 0)
            {
                lineEnd = block.Length;
            }

            var fieldWidth = lineEnd - valueStart;
            var formatted = PhotoModeValueFormatting.FormatDefaultValue(value, isInteger, fieldWidth);
            if (formatted.Length > fieldWidth)
            {
                errors.Add(
                    $"{titleKey}: {formatted.Trim()} needs {formatted.Length} characters, but the pak field only allows {fieldWidth}.");
            }
        }
        catch (PhotoModeLoadException)
        {
        }
    }

    private static void EnsurePhotoModeSectionPresent(string text)
    {
        if (text.Length < 1024)
        {
            throw new PhotoModeLoadException(
                "initial.cache_block is empty or truncated. Close SnowRunner if it is running, " +
                "restore initial.pak from a backup, or use Restore full baseline on the Home page.");
        }

        if (!text.Contains(ApertureControllerMarker, StringComparison.Ordinal))
        {
            throw new PhotoModeLoadException(
                "Photo mode settings were not found in initial.cache_block. " +
                "The pak may be corrupted or from an incompatible game version. " +
                "Close SnowRunner, restore initial.pak from a backup (or Restore full baseline on Home), " +
                "then reapply your saved tuning profile.");
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

    internal static int GetDefaultValueFieldWidth(string text, string titleKey)
    {
        var (_, block) = FindControllerBlock(text, titleKey);
        var match = DefaultValueRegex.Match(block);
        if (!match.Success)
        {
            throw new PhotoModeLoadException($"Missing defaultValue for {titleKey}.");
        }

        var valueStart = match.Groups["value"].Index;
        var lineEnd = block.IndexOf('\n', valueStart);
        if (lineEnd < 0)
        {
            lineEnd = block.Length;
        }

        return lineEnd - valueStart;
    }

    internal static double ReadSliderDefaultPublic(string text, string titleKey, bool isInteger) =>
        ReadSliderDefault(text, titleKey, isInteger);

    private static double ReadSliderDefault(string text, string titleKey, bool isInteger)
    {
        var (_, block) = FindControllerBlock(text, titleKey);
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

    private static string WriteSliderDefaultIfChanged(string text, string titleKey, double value, bool isInteger)
    {
        var current = ReadSliderDefault(text, titleKey, isInteger);
        if (isInteger)
        {
            if ((int)Math.Round(current) == (int)Math.Round(value))
            {
                return text;
            }
        }
        else if (Math.Abs(current - value) < 0.0001)
        {
            return text;
        }

        return WriteSliderDefault(text, titleKey, value, isInteger);
    }

    private static string WriteWeatherDefaultFirstIfChanged(string text, string presetKey)
    {
        var current = ReadFirstWeatherPreset(text);
        if (current.Equals(presetKey, StringComparison.Ordinal))
        {
            return text;
        }

        return WriteWeatherDefaultFirst(text, presetKey);
    }

    private static string WriteSliderDefault(string text, string titleKey, double value, bool isInteger)
    {
        var (start, block) = FindControllerBlock(text, titleKey);
        var match = DefaultValueRegex.Match(block);
        if (!match.Success)
        {
            throw new PhotoModeLoadException($"Missing defaultValue for {titleKey}.");
        }

        var valueStart = match.Groups["value"].Index;
        var lineEnd = block.IndexOf('\n', valueStart);
        if (lineEnd < 0)
        {
            lineEnd = block.Length;
        }

        var valueFieldWidth = lineEnd - valueStart;
        var previousField = block.Substring(valueStart, valueFieldWidth);
        var formatted = PhotoModeValueFormatting.FormatDefaultValue(value, isInteger, valueFieldWidth);

        if (formatted.Length > valueFieldWidth)
        {
            throw new PhotoModeLoadException(
                $"The value for {titleKey} is too long ({formatted.Trim()} needs {formatted.Length} characters, " +
                $"but the pak field only has {valueFieldWidth}). " +
                "Pick a value with the same number of digits/characters, or restore photo mode baseline first.");
        }

        if (formatted.Length < valueFieldWidth)
        {
            formatted = formatted.PadLeft(valueFieldWidth);
        }

        var updatedBlock = block.Remove(valueStart, valueFieldWidth).Insert(valueStart, formatted);
        return text.Remove(start, block.Length).Insert(start, updatedBlock);
    }

    private static (int Start, string Block) FindControllerBlock(string text, string titleKey)
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

        return (start, text[start..end]);
    }
}
