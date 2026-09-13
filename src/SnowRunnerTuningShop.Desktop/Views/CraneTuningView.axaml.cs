using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Crane;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class CraneTuningView : UserControl
{
    private readonly ObservableCollection<CraneRowViewModel> _cranes = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public CraneTuningView()
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
        await ReloadCranesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _cranes.Clear();
        RefreshFilter();
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

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Crane.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.Crane.Apply;
        SaveIndividualButton.Content = UiText.Crane.SaveIndividualChanges;
        RestoreCranesButton.Content = UiText.Crane.RestoreCranesToBaseline;
        ReloadButton.Content = UiText.Crane.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Crane.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            CranesGrid,
            UiText.Crane.CategoryColumn,
            UiText.Crane.NameColumn,
            UiText.Crane.FileColumn,
            UiText.Crane.PriceColumn,
            UiText.Crane.ArmForceColumn,
            UiText.Crane.SpeedOYColumn,
            UiText.Crane.SpeedOYWithLoadColumn,
            UiText.Crane.SpeedXZColumn,
            UiText.Crane.SpeedXZWithLoadColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadCranes();
    }

    private async void RestoreCranesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Crane.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCranesButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => CraneService.RestoreCranesFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadCranes();
                ReportStatus(UiText.Crane.MultipliersAppliedStatus(
                    result.ChangedCranes,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Crane.SaveErrorTitle);
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
                () => ReportStatus(UiText.Crane.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCranesButton))
        {
            try
            {
                var path = PakPath!;
                var armForce = GetArmForceMultiplier();
                var movementSpeed = GetMovementSpeedMultiplier();
                var result = await Task.Run(() => CraneService.ApplyGlobalMultipliers(
                    path, armForce, movementSpeed));

                ReloadCranes();
                ReportStatus(UiText.Crane.MultipliersAppliedStatus(
                    result.ChangedCranes,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Crane.SaveErrorTitle);
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
                () => ReportStatus(UiText.Crane.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCranesButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(CranesGrid);

                var cranes = _cranes.Select(row => row.ToDefinition()).ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => CraneService.SaveCraneChanges(path, cranes));
                ReloadCranes();
                ReportStatus(UiText.Crane.IndividualSavedMessage(result.ChangedCranes));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Crane.SaveErrorTitle);
            }
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
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Crane.LoadErrorTitle);
            }
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
        }
        catch (Exception ex)
        {
            Clear();
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Crane.LoadErrorTitle);
            }
        }
    }

    private void ApplyCranes(IReadOnlyList<CraneDefinition> cranes)
    {
        _cranes.Clear();
        foreach (var crane in cranes)
        {
            _cranes.Add(CraneRowViewModel.FromDefinition(crane));
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
            CranesGrid,
            _cranes,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.Category,
                row.DisplayName,
                row.Name,
                row.AddonType));

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
