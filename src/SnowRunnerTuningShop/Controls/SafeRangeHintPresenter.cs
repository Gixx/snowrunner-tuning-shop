using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Controls;

public static class SafeRangeHintPresenter
{
    private static readonly SolidColorBrush NormalBrush = CreateBrush(0x10, 0x7C, 0x10);
    private static readonly SolidColorBrush WarningBrush = CreateBrush(0xCA, 0x50, 0x10);
    private static readonly SolidColorBrush ExtremeBrush = CreateBrush(0xC4, 0x2B, 0x1C);
    private static readonly SolidColorBrush InvalidBrush = CreateBrush(0xC4, 0x2B, 0x1C);

    public static void Refresh(TextBlock hintBlock, TextBox inputBox, TuningFieldRange range)
    {
        if (string.IsNullOrWhiteSpace(inputBox.Text))
        {
            hintBlock.Text = BuildIdleHint(range);
            hintBlock.Foreground = GetSecondaryBrush(inputBox);
            ResetInputBorder(inputBox);
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

    private static Brush GetZoneBrush(SafeRangeZone zone) =>
        zone switch
        {
            SafeRangeZone.Normal => NormalBrush,
            SafeRangeZone.Warning => WarningBrush,
            SafeRangeZone.Extreme => ExtremeBrush,
            _ => InvalidBrush,
        };

    private static Brush GetSecondaryBrush(FrameworkElement element) =>
        element.TryFindResource("TextFillColorSecondaryBrush") as Brush
        ?? Brushes.Gray;

    private static Brush GetDefaultBorderBrush(FrameworkElement element) =>
        element.TryFindResource("ControlStrokeColorDefaultBrush") as Brush
        ?? SystemColors.ControlDarkBrush;

    private static void ResetInputBorder(TextBox inputBox) =>
        inputBox.ClearValue(Border.BorderBrushProperty);

    private static SolidColorBrush CreateBrush(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }
}
