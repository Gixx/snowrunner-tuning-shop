using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.AddonCapacity;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class AddonCapacityTuningView : UserControl
{
    private readonly ObservableCollection<AddonRowViewModel> _addons = [];
    private readonly ICollectionView _addonsView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public AddonCapacityTuningView()
    {
        InitializeComponent();
        _addonsView = CollectionViewSource.GetDefaultView(_addons);
        _addonsView.Filter = MatchesFilter;
        AddonsGrid.ItemsSource = _addonsView;
        ResetMultiplierSlidersToBaseline();
    }

    public event EventHandler<string>? StatusChanged;

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
        ReloadAddons();
    }

    public async Task LoadFromPakAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadAddonsAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _addons.Clear();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreAddonsButton);

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadAddons();
    }

    private void RestoreAddonsButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        var confirm = MessageBox.Show(
            UiText.AddonCapacity.ConfirmRestoreMessage,
            UiText.AddonCapacity.ConfirmRestoreTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                var result = AddonCapacityService.RestoreAddonsFromBaseline(PakPath);
                ResetMultiplierSlidersToBaseline();
                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.MultipliersAppliedStatus(
                    result.ChangedAddons,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.AddonCapacity.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                var result = AddonCapacityService.ApplyGlobalMultipliers(
                    PakPath,
                    GetMultiplier(FuelMultiplierSlider),
                    GetMultiplier(WaterMultiplierSlider),
                    GetMultiplier(RepairsMultiplierSlider),
                    GetMultiplier(WheelsMultiplierSlider),
                    GetMultiplier(PriceMultiplierSlider));

                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.MultipliersAppliedStatus(
                    result.ChangedAddons,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.AddonCapacity.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(AddonsGrid);

                var addons = _addons
                    .Select(row => row.ToDefinition())
                    .ToArray();

                var result = AddonCapacityService.SaveAddonChanges(PakPath, addons);
                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.IndividualSavedStatus(result.ChangedAddons, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.AddonCapacity.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ReloadAddons()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyAddons(AddonCapacityService.LoadAddons(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.AddonCapacity.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadAddonsAsync(CancellationToken cancellationToken = default)
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
            var addons = await Task.Run(() => AddonCapacityService.LoadAddons(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyAddons(addons);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.AddonCapacity.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyAddons(IReadOnlyList<AddonCapacityDefinition> addons)
    {
        _addons.Clear();
        foreach (var addon in addons)
        {
            _addons.Add(AddonRowViewModel.FromDefinition(addon));
        }
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _addonsView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is AddonRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.Category,
            row.DisplayName,
            row.Name);

    private void ResetMultiplierSlidersToBaseline()
    {
        FuelMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        WaterMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        RepairsMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        WheelsMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        PriceMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (FuelMultiplierLabel is null
            || WaterMultiplierLabel is null
            || RepairsMultiplierLabel is null
            || WheelsMultiplierLabel is null
            || PriceMultiplierLabel is null)
        {
            return;
        }

        FuelMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Fuel,
            GetMultiplierIndex(FuelMultiplierSlider));
        WaterMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Water,
            GetMultiplierIndex(WaterMultiplierSlider));
        RepairsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.RepairParts,
            GetMultiplierIndex(RepairsMultiplierSlider));
        WheelsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.SpareWheels,
            GetMultiplierIndex(WheelsMultiplierSlider));
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice,
            GetMultiplierIndex(PriceMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class AddonRowViewModel : INotifyPropertyChanged
    {
        private bool _hasPrice;
        private bool _hasFuel;
        private bool _hasWater;
        private bool _hasRepairs;
        private bool _hasWheels;
        private int? _price;
        private int? _fuelCapacity;
        private int? _waterCapacity;
        private int? _repairsCapacity;
        private int? _wheelRepairsCapacity;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string Category { get; init; }

        public int? Price
        {
            get => _price;
            set
            {
                if (_price == value)
                {
                    return;
                }

                _price = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
            }
        }

        public int? FuelCapacity
        {
            get => _fuelCapacity;
            set
            {
                if (_fuelCapacity == value)
                {
                    return;
                }

                _fuelCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelCapacity)));
            }
        }

        public int? WaterCapacity
        {
            get => _waterCapacity;
            set
            {
                if (_waterCapacity == value)
                {
                    return;
                }

                _waterCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WaterCapacity)));
            }
        }

        public int? RepairsCapacity
        {
            get => _repairsCapacity;
            set
            {
                if (_repairsCapacity == value)
                {
                    return;
                }

                _repairsCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RepairsCapacity)));
            }
        }

        public int? WheelRepairsCapacity
        {
            get => _wheelRepairsCapacity;
            set
            {
                if (_wheelRepairsCapacity == value)
                {
                    return;
                }

                _wheelRepairsCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WheelRepairsCapacity)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static AddonRowViewModel FromDefinition(AddonCapacityDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceFile = definition.SourceFile,
                Category = definition.Category,
                _hasPrice = definition.HasPrice,
                _price = definition.HasPrice ? definition.Price : null,
                _hasFuel = definition.HasFuel,
                _fuelCapacity = definition.HasFuel ? definition.FuelCapacity : null,
                _hasWater = definition.HasWater,
                _waterCapacity = definition.HasWater ? definition.WaterCapacity : null,
                _hasRepairs = definition.HasRepairs,
                _repairsCapacity = definition.HasRepairs ? definition.RepairsCapacity : null,
                _hasWheels = definition.HasWheels,
                _wheelRepairsCapacity = definition.HasWheels ? definition.WheelRepairsCapacity : null,
            };

        // Only Has*-true fields are ever written back — never invent missing capacity attrs.
        public AddonCapacityDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                SourceFile = SourceFile,
                Category = Category,
                HasPrice = _hasPrice,
                Price = _hasPrice ? _price ?? 0 : 0,
                HasFuel = _hasFuel,
                FuelCapacity = _hasFuel ? _fuelCapacity ?? 0 : 0,
                HasWater = _hasWater,
                WaterCapacity = _hasWater ? _waterCapacity ?? 0 : 0,
                HasRepairs = _hasRepairs,
                RepairsCapacity = _hasRepairs ? _repairsCapacity ?? 0 : 0,
                HasWheels = _hasWheels,
                WheelRepairsCapacity = _hasWheels ? _wheelRepairsCapacity ?? 0 : 0,
            };
    }
}
