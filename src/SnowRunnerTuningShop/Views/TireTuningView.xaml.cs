using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tires;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class TireTuningView : UserControl
{
    private readonly ObservableCollection<TireRowViewModel> _tires = [];
    private readonly ICollectionView _tiresView;

    public TireTuningView()
    {
        InitializeComponent();
        _tiresView = CollectionViewSource.GetDefaultView(_tires);
        _tiresView.Filter = MatchesFilter;
        TiresGrid.ItemsSource = _tiresView;
        ResetMultiplierSlidersToBaseline();
    }

    public string? PakPath { get; private set; }

    public void LoadFromPak(string pakPath)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        ReloadTires();
    }

    public void Clear()
    {
        PakPath = null;
        _tires.Clear();
        RestoreTiresButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        RestoreTiresButton.IsEnabled = !string.IsNullOrWhiteSpace(PakPath)
            && PakBaselineService.HasBaseline(PakPath);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadTires();
    }

    private void RestoreTiresButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
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
            var result = TireService.RestoreTiresFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            GlobalIgnoreIceCheckBox.IsChecked = false;
            ReloadTires();
            MessageBox.Show(
                UiText.Tires.RestoreTiresMessage(
                    result.ChangedTires,
                    result.UpdatedFiles),
                UiText.Tires.RestoreTiresSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
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
            var result = TireService.ApplyGlobalMultipliers(
                PakPath,
                GetMultiplier(OnRoadFrictionMultiplierSlider),
                GetMultiplier(OffRoadFrictionMultiplierSlider),
                GetMultiplier(MudFrictionMultiplierSlider),
                GlobalIgnoreIceCheckBox.IsChecked == true ? true : null);

            ReloadTires();
            GlobalIgnoreIceCheckBox.IsChecked = false;
            MessageBox.Show(
                UiText.Tires.MultipliersSavedMessage(
                    result.ChangedTires,
                    result.UpdatedFiles),
                UiText.Tires.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Tires.LoadPakFirst, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            TiresGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            TiresGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var tires = _tires
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = TireService.SaveTireChanges(PakPath, tires);
            ReloadTires();
            MessageBox.Show(
                UiText.Tires.IndividualSavedMessage(result.ChangedTires),
                UiText.Tires.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Tires.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadTires()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            var tires = TireService.LoadTires(PakPath, AppLanguage.Current);
            _tires.Clear();
            foreach (var tire in tires)
            {
                _tires.Add(TireRowViewModel.FromDefinition(tire));
            }

            _tiresView.Refresh();
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Tires.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _tiresView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is TireRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.Category,
            row.DisplayName,
            row.Name,
            row.SetName,
            row.SetId,
            row.UsedBy,
            row.UsedByTooltip);

    private void ResetMultiplierSlidersToBaseline()
    {
        OnRoadFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        OffRoadFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        MudFrictionMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (OnRoadFrictionMultiplierLabel is null
            || OffRoadFrictionMultiplierLabel is null
            || MudFrictionMultiplierLabel is null)
        {
            return;
        }

        OnRoadFrictionMultiplierLabel.Text = TuningMultiplierPresets.FormatSliderCaption(
            "On-road",
            GetMultiplierIndex(OnRoadFrictionMultiplierSlider));
        OffRoadFrictionMultiplierLabel.Text = TuningMultiplierPresets.FormatSliderCaption(
            "Off-road",
            GetMultiplierIndex(OffRoadFrictionMultiplierSlider));
        MudFrictionMultiplierLabel.Text = TuningMultiplierPresets.FormatSliderCaption(
            "Mud",
            GetMultiplierIndex(MudFrictionMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    public sealed class TireRowViewModel : INotifyPropertyChanged
    {
        private double _onRoadFriction;
        private double _offRoadFriction;
        private double _mudFriction;
        private bool _ignoreIce;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string SetId { get; init; }
        public required string SetName { get; init; }
        public required string UsedBy { get; init; }
        public required string UsedByTooltip { get; init; }
        public required string Category { get; init; }
        public int Price { get; init; }
        public required string FrictionTemplate { get; init; }

        public double OnRoadFriction
        {
            get => _onRoadFriction;
            set
            {
                if (Math.Abs(_onRoadFriction - value) < 0.0001)
                {
                    return;
                }

                _onRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OnRoadFriction)));
            }
        }

        public double OffRoadFriction
        {
            get => _offRoadFriction;
            set
            {
                if (Math.Abs(_offRoadFriction - value) < 0.0001)
                {
                    return;
                }

                _offRoadFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(OffRoadFriction)));
            }
        }

        public double MudFriction
        {
            get => _mudFriction;
            set
            {
                if (Math.Abs(_mudFriction - value) < 0.0001)
                {
                    return;
                }

                _mudFriction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MudFriction)));
            }
        }

        public bool IgnoreIce
        {
            get => _ignoreIce;
            set
            {
                if (_ignoreIce == value)
                {
                    return;
                }

                _ignoreIce = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IgnoreIce)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static TireRowViewModel FromDefinition(TireDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceFile = definition.SourceFile,
                SetId = definition.SetId,
                SetName = definition.SetName,
                UsedBy = definition.UsedBy,
                UsedByTooltip = definition.UsedByTooltip,
                Category = definition.Category,
                Price = definition.Price,
                FrictionTemplate = definition.FrictionTemplate,
                OnRoadFriction = definition.OnRoadFriction,
                OffRoadFriction = definition.OffRoadFriction,
                MudFriction = definition.MudFriction,
                IgnoreIce = definition.IgnoreIce,
            };

        public TireDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                SourceFile = SourceFile,
                SetId = SetId,
                SetName = SetName,
                UsedBy = UsedBy,
                UsedByTooltip = UsedByTooltip,
                Category = Category,
                Price = Price,
                FrictionTemplate = FrictionTemplate,
                OnRoadFriction = OnRoadFriction,
                OffRoadFriction = OffRoadFriction,
                MudFriction = MudFriction,
                IgnoreIce = IgnoreIce,
            };
    }
}
