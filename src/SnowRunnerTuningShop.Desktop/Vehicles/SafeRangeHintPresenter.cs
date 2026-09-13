using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Vehicles;

internal static class SafeRangeHintPresenter
{
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10));
    private static readonly IBrush ExtremeBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));

    public static void Refresh(TextBlock hintBlock, TextBox inputBox, TuningFieldRange range)
    {
        if (string.IsNullOrWhiteSpace(inputBox.Text))
        {
            hintBlock.Text = BuildIdleHint(range);
            hintBlock.Foreground = GetSecondaryBrush(inputBox);
            inputBox.ClearValue(TextBox.BorderBrushProperty);
            return;
        }

        if (!SafeRangeClassifier.TryParseValue(inputBox.Text, out var value))
        {
            hintBlock.Text = UiText.SafeRange.InvalidNumber;
            hintBlock.Foreground = InvalidBrush;
            inputBox.BorderBrush = InvalidBrush;
            return;
        }

        var zone = SafeRangeClassifier.Classify(value, range);
        hintBlock.Text = BuildActiveHint(range, zone);
        hintBlock.Foreground = GetZoneBrush(zone);
        inputBox.BorderBrush = zone == SafeRangeZone.Normal
            ? GetDefaultBorderBrush(inputBox)
            : GetZoneBrush(zone);
    }

    private static string BuildIdleHint(TuningFieldRange range)
    {
        var parts = new List<string>(3);
        if (range.Baseline is { } baseline)
        {
            parts.Add(UiText.SafeRange.BaselineLabel(FormatValue(baseline, range)));
        }

        parts.Add(UiText.SafeRange.AllowedLabel(
            FormatValue(range.Min, range),
            FormatValue(range.Max, range)));
        return string.Join(" · ", parts);
    }

    private static string BuildActiveHint(TuningFieldRange range, SafeRangeZone zone)
    {
        var parts = new List<string>(3);
        if (range.Baseline is { } baseline)
        {
            parts.Add(UiText.SafeRange.BaselineLabel(FormatValue(baseline, range)));
        }

        parts.Add(UiText.SafeRange.AllowedLabel(
            FormatValue(range.Min, range),
            FormatValue(range.Max, range)));
        parts.Add(UiText.SafeRange.ZoneMessage(zone));
        return string.Join(" · ", parts);
    }

    private static string FormatValue(double value, TuningFieldRange range)
    {
        var suffix = range.UnitSuffix ?? "";
        if (Math.Abs(value - Math.Round(value)) < 1e-9)
        {
            return Math.Round(value).ToString("N0", CultureInfo.InvariantCulture) + suffix;
        }

        return value.ToString("0.######", CultureInfo.InvariantCulture) + suffix;
    }

    private static IBrush GetZoneBrush(SafeRangeZone zone) =>
        zone switch
        {
            SafeRangeZone.Normal => NormalBrush,
            SafeRangeZone.Warning => WarningBrush,
            SafeRangeZone.Extreme => ExtremeBrush,
            _ => InvalidBrush,
        };

    private static IBrush GetSecondaryBrush(StyledElement element) =>
        element.TryFindResource("AppMutedBrush", out var brush) && brush is IBrush muted
            ? muted
            : Brushes.Gray;

    private static IBrush GetDefaultBorderBrush(StyledElement element) =>
        element.TryFindResource("AppCardStrokeBrush", out var brush) && brush is IBrush stroke
            ? stroke
            : Brushes.Gray;
}
