using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class GearboxTuningView : UserControl
{
    private readonly ObservableCollection<GearboxRowViewModel> _gearboxes = [];
    private readonly ICollectionView _gearboxesView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public GearboxTuningView()
    {
        InitializeComponent();
        _gearboxesView = CollectionViewSource.GetDefaultView(_gearboxes);
        _gearboxesView.Filter = MatchesFilter;
        GearboxesGrid.ItemsSource = _gearboxesView;
        ResetMultiplierSlidersToBaseline();
    }

    public string? PakPath { get; private set; }

    public void AttachSession(AppSession session) => _session = session;

    public void SetPakWritesAllowed(bool allowed)
    {
        _pakWritesAllowed = allowed;
        RefreshRestoreButton();
    }

    public void LoadFromPak(string pakPath)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        ReloadGearboxes();
    }

    public async Task LoadFromPakAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadGearboxesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _gearboxes.Clear();
        ApplyMultipliersButton.IsEnabled = false;
        SaveIndividualButton.IsEnabled = false;
        RestoreGearboxesButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        var hasPak = !string.IsNullOrWhiteSpace(PakPath);
        ApplyMultipliersButton.IsEnabled = hasPak && _pakWritesAllowed;
        SaveIndividualButton.IsEnabled = hasPak && _pakWritesAllowed;
        RestoreGearboxesButton.IsEnabled = hasPak
            && _pakWritesAllowed
            && PakBaselineService.HasBaseline(PakPath!);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadGearboxes();
    }

    private void RestoreGearboxesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => MessageBox.Show(UiText.Gearbox.LoadPakFirst, UiText.Gearbox.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            var result = GearboxService.RestoreGearboxesFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            ReloadGearboxes();
            MessageBox.Show(
                UiText.Gearbox.RestoreGearboxesMessage(
                    result.ChangedGearboxes,
                    result.UpdatedFiles),
                UiText.Gearbox.RestoreGearboxesSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Gearbox.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => MessageBox.Show(UiText.Gearbox.LoadPakFirst, UiText.Gearbox.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            var result = GearboxService.ApplyGlobalMultipliers(
                PakPath,
                GetMultiplier(FuelMultiplierSlider),
                GetMultiplier(IdleMultiplierSlider),
                GetMultiplier(AwdMultiplierSlider));

            ReloadGearboxes();
            MessageBox.Show(
                UiText.Gearbox.MultipliersSavedMessage(
                    result.ChangedGearboxes,
                    result.UpdatedFiles),
                UiText.Gearbox.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Gearbox.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => MessageBox.Show(UiText.Gearbox.LoadPakFirst, UiText.Gearbox.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            GearboxesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            GearboxesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var gearboxes = _gearboxes
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = GearboxService.SaveGearboxChanges(PakPath, gearboxes);
            ReloadGearboxes();
            MessageBox.Show(
                UiText.Gearbox.IndividualSavedMessage(result.ChangedGearboxes),
                UiText.Gearbox.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Gearbox.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadGearboxes()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyGearboxes(GearboxService.LoadGearboxes(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Gearbox.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadGearboxesAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            var path = PakPath;
            var language = AppLanguage.Current;
            var gearboxes = await Task.Run(() => GearboxService.LoadGearboxes(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyGearboxes(gearboxes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Gearbox.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyGearboxes(IReadOnlyList<GearboxDefinition> gearboxes)
    {
        _gearboxes.Clear();
        foreach (var gearbox in gearboxes)
        {
            _gearboxes.Add(GearboxRowViewModel.FromDefinition(gearbox));
        }
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _gearboxesView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is GearboxRowViewModel row
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
        FuelMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        IdleMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        AwdMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (FuelMultiplierLabel is null || IdleMultiplierLabel is null || AwdMultiplierLabel is null)
        {
            return;
        }

        FuelMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.FuelConsumption,
            GetMultiplierIndex(FuelMultiplierSlider));
        IdleMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.IdleFuelModifier,
            GetMultiplierIndex(IdleMultiplierSlider));
        AwdMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.AwdFuelPenalty,
            GetMultiplierIndex(AwdMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    public sealed class GearboxRowViewModel : INotifyPropertyChanged
    {
        private double _fuelConsumption;
        private double _idleFuelModifier;
        private double? _awdConsumptionModifier;

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

        public double FuelConsumption
        {
            get => _fuelConsumption;
            set
            {
                if (Math.Abs(_fuelConsumption - value) < 0.0001)
                {
                    return;
                }

                _fuelConsumption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelConsumption)));
            }
        }

        public double IdleFuelModifier
        {
            get => _idleFuelModifier;
            set
            {
                if (Math.Abs(_idleFuelModifier - value) < 0.0001)
                {
                    return;
                }

                _idleFuelModifier = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IdleFuelModifier)));
            }
        }

        public double? AwdConsumptionModifier
        {
            get => _awdConsumptionModifier;
            set
            {
                if (_awdConsumptionModifier == value
                    || (_awdConsumptionModifier is double left
                        && value is double right
                        && Math.Abs(left - right) < 0.0001))
                {
                    return;
                }

                _awdConsumptionModifier = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AwdConsumptionModifier)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static GearboxRowViewModel FromDefinition(GearboxDefinition definition) =>
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
                FuelConsumption = definition.FuelConsumption,
                IdleFuelModifier = definition.IdleFuelModifier,
                AwdConsumptionModifier = definition.AwdConsumptionModifier,
            };

        public GearboxDefinition ToDefinition() =>
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
                FuelConsumption = FuelConsumption,
                IdleFuelModifier = IdleFuelModifier,
                AwdConsumptionModifier = AwdConsumptionModifier,
            };
    }
}
