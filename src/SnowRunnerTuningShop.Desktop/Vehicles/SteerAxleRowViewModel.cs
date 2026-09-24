using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Media;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Trucks;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Vehicles;

/// <summary>
/// Editable row for a single <see cref="TruckSteerAxle"/> shown in the dynamic
/// per-axle steering list on the vehicle detail page.
/// </summary>
public sealed class SteerAxleRowViewModel : INotifyPropertyChanged
{
    private static readonly IBrush NormalBrush = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));
    private static readonly IBrush WarningBrush = new SolidColorBrush(Color.FromRgb(0xCA, 0x50, 0x10));
    private static readonly IBrush ExtremeBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly IBrush InvalidBrush = new SolidColorBrush(Color.FromRgb(0xC4, 0x2B, 0x1C));
    private static readonly IBrush IdleBrush = new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A));

    public event PropertyChangedEventHandler? PropertyChanged;

    public TruckSteerAxle Axle { get; }

    public string DisplayLabel => UiText.Vehicles.AxleOrdinalLabel(Axle.DisplayOrder);

    public string AddedHintText => UiText.Vehicles.SteerAxleAddedHint;

    public bool ShowAddedHint => !Axle.HadSteerInBaseline;

    private string _angleText = "";

    public string AngleText
    {
        get => _angleText;
        set
        {
            var normalized = value ?? "";
            if (_angleText == normalized)
            {
                return;
            }

            _angleText = normalized;
            OnPropertyChanged();
            RefreshSafeRange();
        }
    }

    private string _safeRangeText = "";

    public string SafeRangeText
    {
        get => _safeRangeText;
        private set
        {
            _safeRangeText = value;
            OnPropertyChanged();
        }
    }

    private IBrush _safeRangeBrush = IdleBrush;

    public IBrush SafeRangeBrush
    {
        get => _safeRangeBrush;
        private set
        {
            _safeRangeBrush = value;
            OnPropertyChanged();
        }
    }

    public SteerAxleRowViewModel(TruckSteerAxle axle)
    {
        Axle = axle;
        _angleText = axle.Angle is { } angle && Math.Abs(angle) > 1e-12
            ? TruckSteerXml.FormatSteerAngle(angle)
            : "";
        RefreshSafeRange();
    }

    private TuningFieldRange GetRange() =>
        Axle.BaselineAngle switch
        {
            > 0 => TuningFieldRange.VanillaFrontSteerDegrees(Axle.BaselineAngle),
            < 0 => TuningFieldRange.VanillaRearSteerDegrees(Axle.BaselineAngle),
            _ => TuningFieldRange.AddedRearSteerDegrees(null),
        };

    private void RefreshSafeRange()
    {
        var range = GetRange();
        if (string.IsNullOrWhiteSpace(AngleText))
        {
            SafeRangeText = BuildHint(range, null);
            SafeRangeBrush = IdleBrush;
            return;
        }

        if (!SafeRangeClassifier.TryParseValue(AngleText, out var value))
        {
            SafeRangeText = UiText.SafeRange.InvalidNumber;
            SafeRangeBrush = InvalidBrush;
            return;
        }

        var zone = SafeRangeClassifier.Classify(value, range);
        SafeRangeText = BuildHint(range, zone);
        SafeRangeBrush = zone switch
        {
            SafeRangeZone.Normal => NormalBrush,
            SafeRangeZone.Warning => WarningBrush,
            SafeRangeZone.Extreme => ExtremeBrush,
            _ => InvalidBrush,
        };
    }

    private static string BuildHint(TuningFieldRange range, SafeRangeZone? zone)
    {
        var parts = new List<string>(3);
        if (range.Baseline is { } baseline)
        {
            parts.Add(UiText.SafeRange.BaselineLabel(FormatValue(baseline, range)));
        }

        parts.Add(UiText.SafeRange.AllowedLabel(FormatValue(range.Min, range), FormatValue(range.Max, range)));
        if (zone is { } resolvedZone)
        {
            parts.Add(UiText.SafeRange.ZoneMessage(resolvedZone));
        }

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

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
