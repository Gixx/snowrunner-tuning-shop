using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Gearbox;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class GearboxTuningView : UserControl
{
    private readonly ObservableCollection<GearboxRowViewModel> _gearboxes = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public GearboxTuningView()
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
        await ReloadGearboxesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _gearboxes.Clear();
        RefreshFilter();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreGearboxesButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreGearboxesButton);

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Gearbox.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.Gearbox.Apply;
        SaveIndividualButton.Content = UiText.Gearbox.SaveIndividualChanges;
        RestoreGearboxesButton.Content = UiText.Gearbox.RestoreGearboxesToBaseline;
        ReloadButton.Content = UiText.Gearbox.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Gearbox.FilterPlaceholder;
        AngVelFootnoteText.Text = UiText.Gearbox.AngVelFootnote;
        PartsTuningUiHelpers.SetColumnHeaders(
            GearboxesGrid,
            UiText.Gearbox.CategoryColumn,
            UiText.Gearbox.NameColumn,
            UiText.Gearbox.UsedByColumn,
            UiText.Gearbox.PriceColumn,
            UiText.Gearbox.FuelColumn,
            UiText.Gearbox.IdleColumn,
            UiText.Gearbox.AwdColumn,
            UiText.Gearbox.TopGearAngVelColumn,
            UiText.Gearbox.HighGearAngVelColumn,
            UiText.Gearbox.ReverseGearAngVelColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadGearboxes();
    }

    private async void RestoreGearboxesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Gearbox.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreGearboxesButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => GearboxService.RestoreGearboxesFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadGearboxes();
                ReportStatus(UiText.Gearbox.MultipliersAppliedStatus(
                    result.ChangedGearboxes,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Gearbox.SaveErrorTitle);
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
                () => ReportStatus(UiText.Gearbox.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreGearboxesButton))
        {
            try
            {
                var path = PakPath!;
                var fuel = GetMultiplier(FuelMultiplierSlider);
                var idle = GetMultiplier(IdleMultiplierSlider);
                var awd = GetMultiplier(AwdMultiplierSlider);
                var result = await Task.Run(() => GearboxService.ApplyGlobalMultipliers(path, fuel, idle, awd));

                ReloadGearboxes();
                ReportStatus(UiText.Gearbox.MultipliersAppliedStatus(
                    result.ChangedGearboxes,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Gearbox.SaveErrorTitle);
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
                () => ReportStatus(UiText.Gearbox.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreGearboxesButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(GearboxesGrid);

                var gearboxes = _gearboxes.Select(row => row.ToDefinition()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => GearboxService.SaveGearboxChanges(path, gearboxes));
                ReloadGearboxes();
                ReportStatus(UiText.Gearbox.IndividualSavedStatus(result.ChangedGearboxes, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Gearbox.SaveErrorTitle);
            }
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
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Gearbox.LoadErrorTitle);
            }
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
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Gearbox.LoadErrorTitle);
            }
        }
    }

    private void ApplyGearboxes(IReadOnlyList<GearboxDefinition> gearboxes)
    {
        _gearboxes.Clear();
        foreach (var gearbox in gearboxes)
        {
            _gearboxes.Add(GearboxRowViewModel.FromDefinition(gearbox));
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
            GearboxesGrid,
            _gearboxes,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.Category,
                row.DisplayName,
                row.Name,
                row.SetName,
                row.SetId,
                row.UsedBy,
                row.UsedByTooltip));

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
            UiText.Slider.FuelConsumption, GetMultiplierIndex(FuelMultiplierSlider));
        IdleMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.IdleFuelModifier, GetMultiplierIndex(IdleMultiplierSlider));
        AwdMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.AwdFuelPenalty, GetMultiplierIndex(AwdMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class GearboxRowViewModel : INotifyPropertyChanged
    {
        private int _price;
        private double _fuelConsumption;
        private double _idleFuelModifier;
        private double? _awdConsumptionModifier;
        private double? _maxGearAngVel;
        private double? _highGearAngVel;
        private double? _reverseGearAngVel;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string SetId { get; init; }
        public required string SetName { get; init; }
        public required string UsedBy { get; init; }
        public required string UsedByTooltip { get; init; }
        public required string Category { get; init; }

        public int Price
        {
            get => _price;
            set
            {
                if (_price == value) return;
                _price = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Price)));
            }
        }

        public double FuelConsumption
        {
            get => _fuelConsumption;
            set
            {
                if (Math.Abs(_fuelConsumption - value) < 0.0001) return;
                _fuelConsumption = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(FuelConsumption)));
            }
        }

        public double IdleFuelModifier
        {
            get => _idleFuelModifier;
            set
            {
                if (Math.Abs(_idleFuelModifier - value) < 0.0001) return;
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

        public double? MaxGearAngVel
        {
            get => _maxGearAngVel;
            set
            {
                if (_maxGearAngVel == value
                    || (_maxGearAngVel is double left
                        && value is double right
                        && Math.Abs(left - right) < 0.0001))
                {
                    return;
                }

                _maxGearAngVel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxGearAngVel)));
            }
        }

        public double? HighGearAngVel
        {
            get => _highGearAngVel;
            set
            {
                if (_highGearAngVel == value
                    || (_highGearAngVel is double left
                        && value is double right
                        && Math.Abs(left - right) < 0.0001))
                {
                    return;
                }

                _highGearAngVel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HighGearAngVel)));
            }
        }

        public double? ReverseGearAngVel
        {
            get => _reverseGearAngVel;
            set
            {
                if (_reverseGearAngVel == value
                    || (_reverseGearAngVel is double left
                        && value is double right
                        && Math.Abs(left - right) < 0.0001))
                {
                    return;
                }

                _reverseGearAngVel = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ReverseGearAngVel)));
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
                MaxGearAngVel = definition.MaxGearAngVel,
                HighGearAngVel = definition.HighGearAngVel,
                ReverseGearAngVel = definition.ReverseGearAngVel,
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
                MaxGearAngVel = MaxGearAngVel,
                HighGearAngVel = HighGearAngVel,
                ReverseGearAngVel = ReverseGearAngVel,
            };
    }
}
