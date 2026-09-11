using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class CraneTuningView : UserControl
{
    private readonly ObservableCollection<CraneRowViewModel> _cranes = [];
    private readonly ICollectionView _cranesView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public CraneTuningView()
    {
        InitializeComponent();
        _cranesView = CollectionViewSource.GetDefaultView(_cranes);
        _cranesView.Filter = MatchesFilter;
        CranesGrid.ItemsSource = _cranesView;
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

    public async Task LoadFromPakAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadCranesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _cranes.Clear();
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreCranesButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreCranesButton);

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadCranes();
    }

    private void RestoreCranesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Crane.LoadPakFirst)))
        {
            return;
        }

        try
        {
            var result = CraneService.RestoreCranesFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            ReloadCranes();
            ReportStatus(UiText.Crane.MultipliersAppliedStatus(
                result.ChangedCranes,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Crane.RestoreCranesMessage(
                    result.ChangedCranes,
                    result.UpdatedFiles),
                UiText.Crane.RestoreCranesSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Crane.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Crane.LoadPakFirst)))
        {
            return;
        }

        try
        {
            var result = CraneService.ApplyGlobalMultipliers(
                PakPath,
                GetArmForceMultiplier(),
                GetMovementSpeedMultiplier());

            ReloadCranes();
            ReportStatus(UiText.Crane.MultipliersAppliedStatus(
                result.ChangedCranes,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Crane.MultipliersSavedMessage(
                    result.ChangedCranes,
                    result.UpdatedFiles),
                UiText.Crane.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Crane.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => MessageBox.Show(UiText.Crane.LoadPakFirst, UiText.Crane.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information)))
        {
            return;
        }

        try
        {
            PartsTuningUiHelpers.CommitGridEdits(CranesGrid);

            var cranes = _cranes
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = CraneService.SaveCraneChanges(PakPath, cranes);
            ReloadCranes();

            MessageBox.Show(
                UiText.Crane.IndividualSavedMessage(result.ChangedCranes),
                UiText.Crane.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Crane.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadCranes()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyCranes(CraneService.LoadCranes(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Crane.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadCranesAsync(CancellationToken cancellationToken = default)
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
            var cranes = await Task.Run(() => CraneService.LoadCranes(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyCranes(cranes);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Crane.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyCranes(IReadOnlyList<CraneDefinition> cranes)
    {
        _cranes.Clear();
        foreach (var crane in cranes)
        {
            _cranes.Add(CraneRowViewModel.FromDefinition(crane));
        }
    }

    private void ArmForceMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void MovementSpeedMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _cranesView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is CraneRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.Category,
            row.DisplayName,
            row.Name,
            row.AddonType);

    private void ResetMultiplierSlidersToBaseline()
    {
        ArmForceMultiplierSlider.Value = CraneMultiplierPresets.BaselineIndex;
        MovementSpeedMultiplierSlider.Value = CraneMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (ArmForceMultiplierLabel is null || MovementSpeedMultiplierLabel is null)
        {
            return;
        }

        ArmForceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.ArmForceMultiplier,
            GetArmForceMultiplierLabel());
        MovementSpeedMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.MovementSpeedMultiplier,
            GetMovementSpeedMultiplierLabel());
    }

    private int GetArmForceMultiplierIndex() =>
        CraneMultiplierPresets.ClampArmForceIndex((int)Math.Round(ArmForceMultiplierSlider.Value));

    private int GetMovementSpeedMultiplierIndex() =>
        CraneMultiplierPresets.ClampMovementSpeedIndex((int)Math.Round(MovementSpeedMultiplierSlider.Value));

    private double GetArmForceMultiplier() =>
        CraneMultiplierPresets.GetArmForceValue(GetArmForceMultiplierIndex());

    private double GetMovementSpeedMultiplier() =>
        CraneMultiplierPresets.GetMovementSpeedValue(GetMovementSpeedMultiplierIndex());

    private string GetArmForceMultiplierLabel()
    {
        var index = GetArmForceMultiplierIndex();
        return index == CraneMultiplierPresets.BaselineIndex
            ? StringResources.Get("Slider.MultiplierBaseline", "1 (baseline)")
            : CraneMultiplierPresets.GetArmForceLabel(index);
    }

    private string GetMovementSpeedMultiplierLabel()
    {
        var index = GetMovementSpeedMultiplierIndex();
        return index == CraneMultiplierPresets.BaselineIndex
            ? StringResources.Get("Slider.MultiplierBaseline", "1 (baseline)")
            : CraneMultiplierPresets.GetMovementSpeedLabel(index);
    }

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class CraneRowViewModel : INotifyPropertyChanged
    {
        private double _averageArmForce;
        private double _speedOY;
        private double _speedOYWithLoad;
        private double _speedXZ;
        private double _speedXZWithLoad;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string Category { get; init; }
        public required string AddonType { get; init; }
        public int Price { get; init; }
        public int ArmMotorCount { get; init; }

        public double AverageArmForce
        {
            get => _averageArmForce;
            set => Set(ref _averageArmForce, value, nameof(AverageArmForce));
        }

        public double SpeedOY
        {
            get => _speedOY;
            set => Set(ref _speedOY, value, nameof(SpeedOY));
        }

        public double SpeedOYWithLoad
        {
            get => _speedOYWithLoad;
            set => Set(ref _speedOYWithLoad, value, nameof(SpeedOYWithLoad));
        }

        public double SpeedXZ
        {
            get => _speedXZ;
            set => Set(ref _speedXZ, value, nameof(SpeedXZ));
        }

        public double SpeedXZWithLoad
        {
            get => _speedXZWithLoad;
            set => Set(ref _speedXZWithLoad, value, nameof(SpeedXZWithLoad));
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static CraneRowViewModel FromDefinition(CraneDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceFile = definition.SourceFile,
                Category = definition.Category,
                AddonType = definition.AddonType,
                Price = definition.Price,
                ArmMotorCount = definition.ArmMotorCount,
                AverageArmForce = definition.AverageArmForce,
                SpeedOY = definition.SpeedOY,
                SpeedOYWithLoad = definition.SpeedOYWithLoad,
                SpeedXZ = definition.SpeedXZ,
                SpeedXZWithLoad = definition.SpeedXZWithLoad,
            };

        public CraneDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                SourceFile = SourceFile,
                Category = Category,
                AddonType = AddonType,
                Price = Price,
                ArmMotorCount = ArmMotorCount,
                AverageArmForce = AverageArmForce,
                SpeedOY = SpeedOY,
                SpeedOYWithLoad = SpeedOYWithLoad,
                SpeedXZ = SpeedXZ,
                SpeedXZWithLoad = SpeedXZWithLoad,
            };

        private void Set(ref double field, double value, string propertyName)
        {
            if (Math.Abs(field - value) < 0.0001)
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
