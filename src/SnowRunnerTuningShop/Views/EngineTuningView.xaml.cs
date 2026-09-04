using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Engine;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class EngineTuningView : UserControl
{
    private readonly ObservableCollection<EngineRowViewModel> _engines = [];
    private readonly ICollectionView _enginesView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public EngineTuningView()
    {
        InitializeComponent();
        _enginesView = CollectionViewSource.GetDefaultView(_engines);
        _enginesView.Filter = MatchesFilter;
        EnginesGrid.ItemsSource = _enginesView;
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
        ReloadEngines();
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
        ApplyMultipliersButton.IsEnabled = false;
        SaveIndividualButton.IsEnabled = false;
        RestoreEnginesButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        var hasPak = !string.IsNullOrWhiteSpace(PakPath);
        ApplyMultipliersButton.IsEnabled = hasPak && _pakWritesAllowed;
        SaveIndividualButton.IsEnabled = hasPak && _pakWritesAllowed;
        RestoreEnginesButton.IsEnabled = hasPak
            && _pakWritesAllowed
            && PakBaselineService.HasBaseline(PakPath!);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadEngines();
    }

    private void RestoreEnginesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        try
        {
            var result = EngineService.RestoreEnginesFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            ReloadEngines();
            ReportStatus(UiText.Engine.MultipliersAppliedStatus(
                result.ChangedEngines,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Engine.RestoreEnginesMessage(
                    result.ChangedEngines,
                    result.UpdatedFiles),
                UiText.Engine.RestoreEnginesSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Engine.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        try
        {
            var result = EngineService.ApplyGlobalMultipliers(
                PakPath,
                GetMultiplier(TorqueMultiplierSlider),
                GetMultiplier(FuelMultiplierSlider),
                GetMultiplier(DamageMultiplierSlider),
                GetMultiplier(ResponsivenessMultiplierSlider));

            ReloadEngines();
            ReportStatus(UiText.Engine.MultipliersAppliedStatus(
                result.ChangedEngines,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Engine.MultipliersSavedMessage(
                    result.ChangedEngines,
                    result.UpdatedFiles),
                UiText.Engine.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Engine.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => ReportStatus(UiText.Engine.LoadPakFirst)))
        {
            return;
        }

        try
        {
            EnginesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            EnginesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var engines = _engines
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = EngineService.SaveEngineChanges(PakPath, engines);
            ReloadEngines();

            ReportStatus(UiText.Engine.IndividualSavedStatus(result.ChangedEngines, result.UpdatedFiles));

            MessageBox.Show(
                UiText.Engine.IndividualSavedMessage(result.ChangedEngines),
                UiText.Engine.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Engine.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            MessageBox.Show(ex.Message, UiText.Engine.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Engine.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyEngines(IReadOnlyList<EngineDefinition> engines)
    {
        _engines.Clear();
        foreach (var engine in engines)
        {
            _engines.Add(EngineRowViewModel.FromDefinition(engine));
        }
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _enginesView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is EngineRowViewModel row
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
            UiText.Slider.Torque,
            GetMultiplierIndex(TorqueMultiplierSlider));
        FuelMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.FuelConsumption,
            GetMultiplierIndex(FuelMultiplierSlider));
        DamageMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.DamageCapacity,
            GetMultiplierIndex(DamageMultiplierSlider));
        ResponsivenessMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Responsiveness,
            GetMultiplierIndex(ResponsivenessMultiplierSlider));
    }

    private static int GetMultiplierIndex(Slider slider) =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(slider.Value));

    private static double GetMultiplier(Slider slider) =>
        TuningMultiplierPresets.GetValue(GetMultiplierIndex(slider));

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class EngineRowViewModel : INotifyPropertyChanged
    {
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
        public int Price { get; init; }

        public double Torque
        {
            get => _torque;
            set
            {
                if (Math.Abs(_torque - value) < 0.0001)
                {
                    return;
                }

                _torque = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Torque)));
            }
        }

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

        public double DamageCapacity
        {
            get => _damageCapacity;
            set
            {
                if (Math.Abs(_damageCapacity - value) < 0.0001)
                {
                    return;
                }

                _damageCapacity = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DamageCapacity)));
            }
        }

        public double EngineResponsiveness
        {
            get => _engineResponsiveness;
            set
            {
                if (Math.Abs(_engineResponsiveness - value) < 0.0000001)
                {
                    return;
                }

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
