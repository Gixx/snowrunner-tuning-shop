using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using SnowRunnerTuningShop.Core.Backup;
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
        public required Func<double> Read { get; init; }
        public required Action<double> Write { get; init; }
        public required string Format { get; init; }
    }

    private AppSession? _session;
    private readonly List<SliderBinding> _sliderBindings = [];

    public PhotoModeView()
    {
        InitializeComponent();
        BuildSliderPanels();
        BindTimeChoices();
        BindWeatherChoices();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadFromPak();
        _session.BaselineChanged += (_, _) => ReloadFromPak();
        ReloadFromPak();
    }

    private void BindTimeChoices()
    {
        TimeCombo.ItemsSource = PhotoModeTimeIndex.AllChoices()
            .Select(choice => new LabeledValue<int>(choice.Label, choice.Index))
            .ToArray();
        TimeCombo.DisplayMemberPath = nameof(LabeledValue<int>.Label);
        TimeCombo.SelectedValuePath = nameof(LabeledValue<int>.Value);
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
        AddSlider(LookPanel, UiText.PhotoMode.Exposure, -0.5, 0.5, 0.05, "0.00");
        AddSlider(LookPanel, UiText.PhotoMode.Contrast, 0, 2, 0.05, "0.00");
        AddSlider(LookPanel, UiText.PhotoMode.Hue, -3, 3, 0.1, "0.0");
        AddSlider(LookPanel, UiText.PhotoMode.Saturation, 0, 2, 0.05, "0.00");
        AddSlider(LookPanel, UiText.PhotoMode.ColorGrading, 0, 19, 1, "0");
        AddSlider(LookPanel, UiText.PhotoMode.ColorGradingIntensity, 0, 1, 0.05, "0.00");
        AddSlider(LookPanel, UiText.PhotoMode.Vignette, 0, 1, 0.05, "0.00");
        AddSlider(LookPanel, UiText.PhotoMode.FilmGrain, 0, 1, 0.05, "0.00");

        AddSlider(CameraPanel, UiText.PhotoMode.FieldOfView, 80, 130, 1, "0");
        AddSlider(CameraPanel, UiText.PhotoMode.Aperture, 0, 200, 1, "0");
        AddSlider(CameraPanel, UiText.PhotoMode.FocusPoint, 5, 50, 0.25, "0.00");
        AddSlider(CameraPanel, UiText.PhotoMode.FocusSpan, 5, 200, 0.5, "0.00");
    }

    private void AddSlider(Grid panel, string label, double min, double max, double step, string format)
    {
        var row = panel.RowDefinitions.Count;
        panel.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var grid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
        Grid.SetRow(grid, row);
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

        var binding = new SliderBinding
        {
            Slider = slider,
            ValueText = valueText,
            Format = format,
            Read = () => slider.Value,
            Write = value => slider.Value = value,
        };
        slider.ValueChanged += (_, _) => UpdateSliderLabel(binding);
        _sliderBindings.Add(binding);

        grid.Children.Add(caption);
        grid.Children.Add(slider);
        grid.Children.Add(valueText);
        panel.Children.Add(grid);
    }

    private static void UpdateSliderLabel(SliderBinding binding)
    {
        binding.ValueText.Text = binding.Format.Contains('.')
            ? binding.Slider.Value.ToString(binding.Format, CultureInfo.InvariantCulture)
            : ((int)Math.Round(binding.Slider.Value)).ToString(CultureInfo.InvariantCulture);
    }

    private void ReloadFromPak()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            HintText.Text = UiText.PhotoMode.LoadPakHint;
            HintText.Visibility = Visibility.Visible;
            StatusText.Text = "";
            RestoreButton.IsEnabled = false;
            return;
        }

        HintText.Visibility = Visibility.Collapsed;
        RestoreButton.IsEnabled = PakBaselineService.HasBaseline(_session.PakPath);

        try
        {
            var settings = PhotoModeService.LoadSettings(_session.PakPath);
            ApplySettingsToUi(settings);
            StatusText.Text = UiText.PhotoMode.LoadedStatus;
        }
        catch (Exception ex)
        {
            StatusText.Text = UiText.Main.ErrorStatus(ex.Message);
        }
    }

    private void ApplySettingsToUi(PhotoModeSettings settings)
    {
        TimeCombo.SelectedItem = ((IEnumerable<LabeledValue<int>>)TimeCombo.ItemsSource!)
            .FirstOrDefault(item => item.Value == settings.TimeIndex);
        WeatherCombo.SelectedItem = ((IEnumerable<LabeledValue<string>>)WeatherCombo.ItemsSource!)
            .FirstOrDefault(item => item.Value == settings.WeatherPresetKey);

        var values = new[]
        {
            settings.Exposure,
            settings.Contrast,
            settings.Hue,
            settings.Saturation,
            settings.ColorGrading,
            settings.ColorGradingIntensity,
            settings.Vignette,
            settings.FilmGrain,
            settings.FieldOfView,
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
            TimeIndex = (TimeCombo.SelectedItem as LabeledValue<int>)?.Value ?? PhotoModeTimeIndex.GameDefault,
            WeatherPresetKey = (WeatherCombo.SelectedItem as LabeledValue<string>)?.Value
                ?? PhotoModeSettingKeys.WeatherDefault,
            Exposure = _sliderBindings[0].Read(),
            Contrast = _sliderBindings[1].Read(),
            Hue = _sliderBindings[2].Read(),
            Saturation = _sliderBindings[3].Read(),
            ColorGrading = (int)Math.Round(_sliderBindings[4].Read()),
            ColorGradingIntensity = _sliderBindings[5].Read(),
            Vignette = _sliderBindings[6].Read(),
            FilmGrain = _sliderBindings[7].Read(),
            FieldOfView = (int)Math.Round(_sliderBindings[8].Read()),
            Aperture = (int)Math.Round(_sliderBindings[9].Read()),
            FocusPoint = _sliderBindings[10].Read(),
            FocusSpan = _sliderBindings[11].Read(),
        };
    }

    private void ApplyButton_Click(object sender, RoutedEventArgs e) => SaveSettings(restoreBaseline: false);

    private void RestoreButton_Click(object sender, RoutedEventArgs e) => SaveSettings(restoreBaseline: true);

    private void SaveSettings(bool restoreBaseline)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            StatusText.Text = UiText.PhotoMode.LoadPakHint;
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
                : PhotoModeService.ApplySettings(_session.PakPath, ReadSettingsFromUi());

            ReloadFromPak();
            StatusText.Text = result.UpdatedEntries <= 0
                ? UiText.PhotoMode.NoChangesToSave
                : UiText.PhotoMode.Saved(result.UpdatedEntries);

            if (result.UpdatedEntries > 0)
            {
                MessageBox.Show(
                    UiText.PhotoMode.Saved(result.UpdatedEntries),
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
