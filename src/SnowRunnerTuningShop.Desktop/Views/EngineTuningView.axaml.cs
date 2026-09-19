using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class EngineTuningView : UserControl
{
    private readonly ObservableCollection<EngineRowViewModel> _engines = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public EngineTuningView()
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
        await ReloadEnginesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _engines.Clear();
        RefreshFilter();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreEnginesButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreEnginesButton);

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Engine.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.Engine.Apply;
        SaveIndividualButton.Content = UiText.Engine.SaveIndividualChanges;
        RestoreEnginesButton.Content = UiText.Engine.RestoreEnginesToBaseline;
        ReloadButton.Content = UiText.Engine.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Engine.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            EnginesGrid,
            UiText.Engine.CategoryColumn,
            UiText.Engine.NameColumn,
            UiText.Engine.UsedByColumn,
            UiText.Engine.PriceColumn,
            UiText.Engine.TorqueColumn,
            UiText.Engine.FuelColumn,
            UiText.Engine.DamageColumn,
            UiText.Engine.ResponsivenessColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadEngines();
    }

    private async void RestoreEnginesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreEnginesButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => EngineService.RestoreEnginesFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadEngines();
                ReportStatus(UiText.Engine.MultipliersAppliedStatus(
                    result.ChangedEngines,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Engine.SaveErrorTitle);
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
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreEnginesButton))
        {
            try
            {
                var path = PakPath!;
                var torque = GetMultiplier(TorqueMultiplierSlider);
                var fuel = GetMultiplier(FuelMultiplierSlider);
                var damage = GetMultiplier(DamageMultiplierSlider);
                var responsiveness = GetMultiplier(ResponsivenessMultiplierSlider);
                var result = await Task.Run(() => EngineService.ApplyGlobalMultipliers(
                    path, torque, fuel, damage, responsiveness));

                ReloadEngines();
                ReportStatus(UiText.Engine.MultipliersAppliedStatus(
                    result.ChangedEngines,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Engine.SaveErrorTitle);
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
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreEnginesButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(EnginesGrid);

                var engines = _engines.Select(row => row.ToDefinition()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => EngineService.SaveEngineChanges(path, engines));
                ReloadEngines();
                ReportStatus(UiText.Engine.IndividualSavedStatus(result.ChangedEngines, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Engine.SaveErrorTitle);
            }
        }
    }

    private void ReloadEngines()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyEngines(EngineService.LoadEngines(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Engine.LoadErrorTitle);
            }
        }
    }

    private async Task ReloadEnginesAsync(CancellationToken cancellationToken = default)
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
            var engines = await Task.Run(() => EngineService.LoadEngines(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyEngines(engines);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Engine.LoadErrorTitle);
            }
        }
    }

    private void ApplyEngines(IReadOnlyList<EngineDefinition> engines)
    {
        _engines.Clear();
        foreach (var engine in engines)
        {
            _engines.Add(EngineRowViewModel.FromDefinition(engine));
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
            EnginesGrid,
            _engines,
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
        TorqueMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        FuelMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        DamageMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        ResponsivenessMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (TorqueMultiplierLabel is null
            || FuelMultiplierLabel is null
            || DamageMultiplierLabel is null
            || ResponsivenessMultiplierLabel is null)
        {
            return;
        }

        TorqueMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Torque, GetMultiplierIndex(TorqueMultiplierSlider));
        FuelMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.FuelConsumption, GetMultiplierIndex(FuelMultiplierSlider));
        DamageMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.DamageCapacity, GetMultiplierIndex(DamageMultiplierSlider));
        ResponsivenessMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Responsiveness, GetMultiplierIndex(ResponsivenessMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class EngineRowViewModel : INotifyPropertyChanged
    {
        private int _price;
        private double _torque;
        private double _fuelConsumption;
        private double _damageCapacity;
        private double _engineResponsiveness;
        private bool _hasEngineResponsiveness;

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

        public double Torque
        {
            get => _torque;
            set
            {
                if (Math.Abs(_torque - value) < 0.0001) return;
                _torque = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Torque)));
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

        public double DamageCapacity
        {
            get => _damageCapacity;
            set
            {
                if (Math.Abs(_damageCapacity - value) < 0.0001) return;
                _damageCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DamageCapacity)));
            }
        }

        public double EngineResponsiveness
        {
            get => _engineResponsiveness;
            set
            {
                if (Math.Abs(_engineResponsiveness - value) < 0.0000001) return;
                _engineResponsiveness = value;
                _hasEngineResponsiveness = true;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(EngineResponsiveness)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static EngineRowViewModel FromDefinition(EngineDefinition definition) =>
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
                Torque = definition.Torque,
                FuelConsumption = definition.FuelConsumption,
                DamageCapacity = definition.DamageCapacity,
                _engineResponsiveness = definition.EngineResponsiveness,
                _hasEngineResponsiveness = definition.HasEngineResponsiveness,
            };

        public EngineDefinition ToDefinition() =>
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
                Torque = Torque,
                FuelConsumption = FuelConsumption,
                DamageCapacity = DamageCapacity,
                EngineResponsiveness = EngineResponsiveness,
                HasEngineResponsiveness = _hasEngineResponsiveness
                    || Math.Abs(EngineResponsiveness - EngineService.DefaultEngineResponsiveness) > 1e-6,
            };
    }
}
