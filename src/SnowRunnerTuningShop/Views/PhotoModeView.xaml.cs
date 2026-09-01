using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.PhotoMode;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class PhotoModeView : UserControl
{
    private sealed record LabeledValue<T>(string Label, T Value);

    private sealed class SliderBinding
    {
        public required Slider Slider { get; init; }
        public required TextBlock ValueText { get; init; }
        public required TextBlock HintText { get; init; }
        public required string Label { get; init; }
        public required string Format { get; init; }
        public required bool IsInteger { get; init; }
        public IReadOnlyList<double> AllowedValues { get; private set; } = [];

        public double Read()
        {
            if (AllowedValues.Count == 0)
            {
                return Slider.Value;
            }

            var index = (int)Math.Round(Slider.Value);
            index = Math.Clamp(index, 0, AllowedValues.Count - 1);
            return AllowedValues[index];
        }

        public void Write(double value)
        {
            Slider.Value = AllowedValues.Count == 0
                ? value
                : IndexOfNearest(value);
        }

        public void ApplyConstraint(PhotoModeSliderConstraint constraint)
        {
            AllowedValues = constraint.AllowedValues;
            Slider.Minimum = 0;
            Slider.Maximum = Math.Max(0, AllowedValues.Count - 1);
            Slider.TickFrequency = 1;
            Slider.IsSnapToTickEnabled = true;
            Slider.IsEnabled = AllowedValues.Count > 1;

            if (AllowedValues.Count <= 1)
            {
                HintText.Text = UiText.PhotoMode.SliderFixedPakField(Label);
                HintText.Visibility = Visibility.Visible;
            }
            else if (AllowedValues.Count <= 5)
            {
                HintText.Text = UiText.PhotoMode.SliderLimitedPakField(Label, AllowedValues.Count, constraint.FieldWidth);
                HintText.Visibility = Visibility.Visible;
            }
            else
            {
                HintText.Visibility = Visibility.Collapsed;
            }
        }

        private int IndexOfNearest(double value)
        {
            var bestIndex = 0;
            var bestDistance = double.MaxValue;
            for (var index = 0; index < AllowedValues.Count; index++)
            {
                var distance = Math.Abs(AllowedValues[index] - value);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = index;
                }
            }

            return bestIndex;
        }
    }

    private AppSession? _session;
    private bool _pakLoadedSuccessfully;
    private double _fixedExposure;
    private double _fixedContrast;
    private int _fixedTimeIndex;
    private readonly List<SliderBinding> _sliderBindings = [];

    public PhotoModeView()
    {
        InitializeComponent();
        BuildSliderPanels();
        BindWeatherChoices();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadFromPak();
        _session.BaselineChanged += (_, _) => ReloadFromPak();
        ReloadFromPak();
    }

    private void BindWeatherChoices()
    {
        WeatherCombo.ItemsSource = new LabeledValue<string>[]
        {
            new(UiText.PhotoMode.WeatherDefault, PhotoModeSettingKeys.WeatherDefault),
            new(UiText.PhotoMode.WeatherClearSky, PhotoModeSettingKeys.WeatherClearSky),
            new(UiText.PhotoMode.WeatherLightRain, PhotoModeSettingKeys.WeatherLightRain),
            new(UiText.PhotoMode.WeatherHeavyRain, PhotoModeSettingKeys.WeatherHeavyRain),
            new(UiText.PhotoMode.WeatherHeavySnow, PhotoModeSettingKeys.WeatherHeavySnow),
        };
        WeatherCombo.DisplayMemberPath = nameof(LabeledValue<string>.Label);
        WeatherCombo.SelectedValuePath = nameof(LabeledValue<string>.Value);
    }

    private void BuildSliderPanels()
    {
        AddInfoNote(LookPanel, UiText.PhotoMode.Exposure, UiText.PhotoMode.ExposureNote);
        AddInfoNote(LookPanel, UiText.PhotoMode.Contrast, UiText.PhotoMode.ContrastNote);
        AddSlider(LookPanel, UiText.PhotoMode.Hue, -3, 3, 0.1, "0.0", isInteger: false);
        AddSlider(LookPanel, UiText.PhotoMode.Saturation, 0, 2, 0.05, "0.00", isInteger: false);
        AddSlider(LookPanel, UiText.PhotoMode.ColorGrading, 0, 19, 1, "0", isInteger: true);
        AddSlider(LookPanel, UiText.PhotoMode.ColorGradingIntensity, 0, 1, 0.05, "0.00", isInteger: false);
        AddSlider(LookPanel, UiText.PhotoMode.Vignette, 0, 1, 0.05, "0.00", isInteger: false);
        AddSlider(LookPanel, UiText.PhotoMode.FilmGrain, 0, 1, 0.05, "0.00", isInteger: false);

        AddInfoNote(CameraPanel, UiText.PhotoMode.FieldOfView, UiText.PhotoMode.FieldOfViewNote);
        AddSlider(CameraPanel, UiText.PhotoMode.Aperture, 0, 200, 1, "0", isInteger: true);
        AddSlider(CameraPanel, UiText.PhotoMode.FocusPoint, 5, 50, 0.25, "0.00", isInteger: false);
        AddSlider(CameraPanel, UiText.PhotoMode.FocusSpan, 5, 200, 0.5, "0.00", isInteger: false);
    }

    private void AddInfoNote(Grid panel, string label, string note)
    {
        var row = panel.RowDefinitions.Count;
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(grid, row);
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var caption = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Top,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
        };
        Grid.SetColumn(caption, 0);

        var body = new TextBlock
        {
            Text = note,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
        };
        Grid.SetColumn(body, 1);

        grid.Children.Add(caption);
        grid.Children.Add(body);
        panel.Children.Add(grid);
    }

    private void AddSlider(
        Grid panel,
        string label,
        double min,
        double max,
        double step,
        string format,
        bool isInteger)
    {
        var row = panel.RowDefinitions.Count;
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(container, row);

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(180) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(72) });

        var caption = new TextBlock
        {
            Text = label,
            VerticalAlignment = VerticalAlignment.Center,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
        };
        Grid.SetColumn(caption, 0);

        var slider = new Slider
        {
            Minimum = min,
            Maximum = max,
            TickFrequency = step,
            IsSnapToTickEnabled = true,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(slider, 1);

        var valueText = new TextBlock
        {
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorPrimaryBrush"),
        };
        Grid.SetColumn(valueText, 2);

        var hintText = new TextBlock
        {
            Margin = new Thickness(180, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 11,
            Visibility = Visibility.Collapsed,
            Foreground = (System.Windows.Media.Brush)FindResource("TextFillColorSecondaryBrush"),
        };

        var binding = new SliderBinding
        {
            Slider = slider,
            ValueText = valueText,
            HintText = hintText,
            Label = label,
            Format = format,
            IsInteger = isInteger,
        };
        slider.ValueChanged += (_, _) => UpdateSliderLabel(binding);
        _sliderBindings.Add(binding);

        grid.Children.Add(caption);
        grid.Children.Add(slider);
        grid.Children.Add(valueText);
        container.Children.Add(grid);
        container.Children.Add(hintText);
        panel.Children.Add(container);
    }

    private static void UpdateSliderLabel(SliderBinding binding)
    {
        var value = binding.Read();
        binding.ValueText.Text = binding.IsInteger
            ? ((int)Math.Round(value)).ToString(CultureInfo.InvariantCulture)
            : binding.Format.Contains('.')
                ? value.ToString(binding.Format, CultureInfo.InvariantCulture)
                : value.ToString(CultureInfo.InvariantCulture);
    }

    private void ReloadFromPak()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            HintText.Text = UiText.PhotoMode.LoadPakHint;
            HintText.Visibility = Visibility.Visible;
            StatusText.Text = "";
            ApplyButton.IsEnabled = false;
            RestoreButton.IsEnabled = false;
            _pakLoadedSuccessfully = false;
            return;
        }

        HintText.Visibility = Visibility.Collapsed;
        RestoreButton.IsEnabled = PakBaselineService.HasBaseline(_session.PakPath);
        UpdateReapplySavedButton();

        try
        {
            var settings = PhotoModeService.LoadSettings(_session.PakPath);
            var constraints = PhotoModeService.LoadSliderConstraints(_session.PakPath);
            _fixedExposure = settings.Exposure;
            _fixedContrast = settings.Contrast;
            _fixedTimeIndex = settings.TimeIndex;
            ApplyConstraintsToSliders(constraints);
            ApplySettingsToUi(settings);
            StatusText.Text = constraints.Any(constraint =>
                    constraint.AllowedValues.Count <= 3
                    && !constraint.SettingKey.Equals(PhotoModeSettingKeys.Exposure, StringComparison.Ordinal)
                    && !constraint.SettingKey.Equals(PhotoModeSettingKeys.Contrast, StringComparison.Ordinal))
                ? $"{UiText.PhotoMode.LoadedStatus} {UiText.PhotoMode.SliderRangeLimited}"
                : UiText.PhotoMode.LoadedStatus;
            ApplyButton.IsEnabled = true;
            _pakLoadedSuccessfully = true;
        }
        catch (Exception ex)
        {
            ApplyButton.IsEnabled = false;
            _pakLoadedSuccessfully = false;
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
        }
    }

    private void ApplyConstraintsToSliders(IReadOnlyList<PhotoModeSliderConstraint> constraints)
    {
        var adjustable = constraints
            .Where(constraint =>
                !constraint.SettingKey.Equals(PhotoModeSettingKeys.Exposure, StringComparison.Ordinal)
                && !constraint.SettingKey.Equals(PhotoModeSettingKeys.Contrast, StringComparison.Ordinal))
            .ToArray();

        for (var i = 0; i < _sliderBindings.Count && i < adjustable.Length; i++)
        {
            _sliderBindings[i].ApplyConstraint(adjustable[i]);
        }
    }

    private void ApplySettingsToUi(PhotoModeSettings settings)
    {
        WeatherCombo.SelectedItem = ((IEnumerable<LabeledValue<string>>)WeatherCombo.ItemsSource!)
            .FirstOrDefault(item => item.Value == settings.WeatherPresetKey);

        var values = new[]
        {
            settings.Hue,
            settings.Saturation,
            settings.ColorGrading,
            settings.ColorGradingIntensity,
            settings.Vignette,
            settings.FilmGrain,
            settings.Aperture,
            settings.FocusPoint,
            settings.FocusSpan,
        };

        for (var i = 0; i < _sliderBindings.Count && i < values.Length; i++)
        {
            _sliderBindings[i].Write(values[i]);
            UpdateSliderLabel(_sliderBindings[i]);
        }
    }

    private PhotoModeSettings ReadSettingsFromUi()
    {
        return new PhotoModeSettings
        {
            TimeIndex = _fixedTimeIndex,
            WeatherPresetKey = (WeatherCombo.SelectedItem as LabeledValue<string>)?.Value
                ?? PhotoModeSettingKeys.WeatherDefault,
            Exposure = _fixedExposure,
            Contrast = _fixedContrast,
            Hue = _sliderBindings[0].Read(),
            Saturation = _sliderBindings[1].Read(),
            ColorGrading = (int)Math.Round(_sliderBindings[2].Read()),
            ColorGradingIntensity = _sliderBindings[3].Read(),
            Vignette = _sliderBindings[4].Read(),
            FilmGrain = _sliderBindings[5].Read(),
            Aperture = (int)Math.Round(_sliderBindings[6].Read()),
            FocusPoint = _sliderBindings[7].Read(),
            FocusSpan = _sliderBindings[8].Read(),
        };
    }

    private void UpdateReapplySavedButton()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ReapplySavedButton.IsEnabled = false;
            ReapplySavedButton.Visibility = Visibility.Collapsed;
            return;
        }

        var editionId = WorkspaceConfigStore.TryResolveEditionId(_session.PakPath);
        var hasSavedProfile = !string.IsNullOrWhiteSpace(editionId)
            && PhotoModeProfileService.HasProfile(editionId);
        ReapplySavedButton.Visibility = hasSavedProfile ? Visibility.Visible : Visibility.Collapsed;
        ReapplySavedButton.IsEnabled = hasSavedProfile
            && PakBaselineService.HasBaseline(_session.PakPath);
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => SaveSettings(restoreBaseline: false);

    private void ReapplySavedButton_Click(object sender, RoutedEventArgs e) => SaveSettings(reapplySaved: true);

    private void RestoreButton_Click(object sender, RoutedEventArgs e) => SaveSettings(restoreBaseline: true);

    private void SaveSettings(bool restoreBaseline = false, bool reapplySaved = false)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.PhotoMode.LoadPakHint;
            return;
        }

        if (!restoreBaseline && !reapplySaved && !_pakLoadedSuccessfully)
        {
            MessageBox.Show(
                StatusText.Text,
                UiText.PhotoMode.SaveErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = restoreBaseline
                ? PhotoModeService.RestoreBaseline(_session.PakPath)
                : reapplySaved
                    ? PhotoModeProfileService.ReapplySaved(_session.PakPath)
                    : PhotoModeService.ApplySettings(_session.PakPath, ReadSettingsFromUi());

            ReloadFromPak();
            StatusText.Text = result.UpdatedEntries <= 0
                ? reapplySaved
                    ? UiText.PhotoMode.ReappliedSavedNoChanges
                    : UiText.PhotoMode.NoChangesToSave
                : reapplySaved
                    ? UiText.PhotoMode.ReappliedSaved(result.UpdatedEntries)
                    : UiText.PhotoMode.Saved(result.UpdatedEntries);

            if (result.UpdatedEntries > 0)
            {
                MessageBox.Show(
                    reapplySaved
                        ? UiText.PhotoMode.ReappliedSaved(result.UpdatedEntries)
                        : UiText.PhotoMode.Saved(result.UpdatedEntries),
                    UiText.PhotoMode.SaveSuccessTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
            MessageBox.Show(ex.Message, UiText.PhotoMode.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
