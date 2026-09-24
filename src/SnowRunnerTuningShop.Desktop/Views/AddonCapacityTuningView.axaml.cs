using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.AddonCapacity;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class AddonCapacityTuningView : UserControl
{
    private readonly ObservableCollection<AddonRowViewModel> _addons = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public AddonCapacityTuningView()
    {
        InitializeComponent();
        ApplyStaticText();
        RefreshFilter();
        ResetMultiplierSlidersToBaseline();
    }

    public event EventHandler<string>? StatusChanged;

    public string? PakPath { get; private set; }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    public void AttachSession(AppSession session) => _session = session;

    public void SetPakWritesAllowed(bool allowed)
    {
        _pakWritesAllowed = allowed;
        RefreshRestoreButton();
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
        RefreshFilter();
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

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.AddonCapacity.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.AddonCapacity.Apply;
        SaveIndividualButton.Content = UiText.AddonCapacity.SaveIndividualChanges;
        RestoreAddonsButton.Content = UiText.AddonCapacity.RestoreAddonsToBaseline;
        ReloadButton.Content = UiText.AddonCapacity.RefreshList;
        FilterTextBox.PlaceholderText = UiText.AddonCapacity.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            AddonsGrid,
            UiText.AddonCapacity.NameColumn,
            UiText.AddonCapacity.CategoryColumn,
            UiText.AddonCapacity.PriceColumn,
            UiText.AddonCapacity.FuelColumn,
            UiText.AddonCapacity.WaterColumn,
            UiText.AddonCapacity.RepairsColumn,
            UiText.AddonCapacity.WheelsColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadAddons();
    }

    private async void RestoreAddonsButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.AddonCapacity.ConfirmRestoreMessage,
                UiText.AddonCapacity.ConfirmRestoreTitle))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => AddonCapacityService.RestoreAddonsFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.MultipliersAppliedStatus(
                    result.ChangedAddons,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.AddonCapacity.SaveErrorTitle);
            }
        }
    }

    private async void ApplyMultipliersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                var path = PakPath!;
                var fuel = GetMultiplier(FuelMultiplierSlider);
                var water = GetMultiplier(WaterMultiplierSlider);
                var repairs = GetMultiplier(RepairsMultiplierSlider);
                var wheels = GetMultiplier(WheelsMultiplierSlider);
                var price = GetMultiplier(PriceMultiplierSlider);
                var result = await Task.Run(() => AddonCapacityService.ApplyGlobalMultipliers(
                    path, fuel, water, repairs, wheels, price));

                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.MultipliersAppliedStatus(
                    result.ChangedAddons,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.AddonCapacity.SaveErrorTitle);
            }
        }
    }

    private async void SaveIndividualButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => ReportStatus(UiText.AddonCapacity.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreAddonsButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(AddonsGrid);

                var addons = _addons.Select(row => row.ToDefinition()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => AddonCapacityService.SaveAddonChanges(path, addons));
                ReloadAddons();
                ReportStatus(UiText.AddonCapacity.IndividualSavedStatus(result.ChangedAddons, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.AddonCapacity.SaveErrorTitle);
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
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.AddonCapacity.LoadErrorTitle);
            }
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
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.AddonCapacity.LoadErrorTitle);
            }
        }
    }

    private void ApplyAddons(IReadOnlyList<AddonCapacityDefinition> addons)
    {
        _addons.Clear();
        foreach (var addon in addons)
        {
            _addons.Add(AddonRowViewModel.FromDefinition(addon));
        }

        RefreshFilter();
    }

    private void MultiplierSlider_PropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != RangeBase.ValueProperty)
        {
            return;
        }

        UpdateMultiplierLabels();
    }

    private void FilterTextBox_TextChanged(object? sender, TextChangedEventArgs e) =>
        RefreshFilter();

    private void RefreshFilter() =>
        TuningListFilter.ApplyFilter(
            AddonsGrid,
            _addons,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.Category,
                row.DisplayName,
                row.Name));

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
            UiText.Slider.Fuel, GetMultiplierIndex(FuelMultiplierSlider));
        WaterMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Water, GetMultiplierIndex(WaterMultiplierSlider));
        RepairsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.RepairParts, GetMultiplierIndex(RepairsMultiplierSlider));
        WheelsMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.SpareWheels, GetMultiplierIndex(WheelsMultiplierSlider));
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice, GetMultiplierIndex(PriceMultiplierSlider));
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
                if (_price == value) return;
                _price = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
            }
        }

        public int? FuelCapacity
        {
            get => _fuelCapacity;
            set
            {
                if (_fuelCapacity == value) return;
                _fuelCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelCapacity)));
            }
        }

        public int? WaterCapacity
        {
            get => _waterCapacity;
            set
            {
                if (_waterCapacity == value) return;
                _waterCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(WaterCapacity)));
            }
        }

        public int? RepairsCapacity
        {
            get => _repairsCapacity;
            set
            {
                if (_repairsCapacity == value) return;
                _repairsCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(RepairsCapacity)));
            }
        }

        public int? WheelRepairsCapacity
        {
            get => _wheelRepairsCapacity;
            set
            {
                if (_wheelRepairsCapacity == value) return;
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
