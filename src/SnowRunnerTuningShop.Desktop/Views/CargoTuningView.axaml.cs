using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Cargo;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class CargoTuningView : UserControl
{
    private readonly ObservableCollection<CargoRowViewModel> _cargos = [];
    private AppSession? _session;

    public CargoTuningView()
    {
        InitializeComponent();
        ApplyStaticText();
        RefreshFilter();
        ResetMultiplierSlidersToBaseline();
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadCargos();
        _session.BaselineChanged += (_, _) =>
        {
            ReloadCargos();
            RefreshWriteButtons();
        };
        _session.GameRunningChanged += (_, _) => RefreshWriteButtons();
        ReloadCargos();
    }

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Cargo.GlobalMultipliersTitle;
        ApplyMultipliersButton.Content = UiText.Cargo.Apply;
        SaveIndividualButton.Content = UiText.Cargo.SaveIndividualChanges;
        RestoreCargosButton.Content = UiText.Cargo.RestoreCargosToBaseline;
        ReloadButton.Content = UiText.Cargo.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Cargo.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            CargosGrid,
            UiText.Cargo.NameColumn,
            UiText.Cargo.IdColumn,
            UiText.Cargo.MassColumn,
            UiText.Cargo.CenterOfMassYColumn,
            UiText.Cargo.FrictionColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshWriteButtons();
        ReloadCargos();
    }

    private async void RestoreCargosButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await TryBeginWriteAsync(owner, requireBaseline: true))
        {
            return;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Cargo.ConfirmRestoreMessage,
                UiText.Cargo.ConfirmRestoreTitle))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                var path = _session!.PakPath!;
                var result = await Task.Run(() => CargoService.RestoreCargosFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                ReloadCargos();
                var status = UiText.Cargo.MultipliersAppliedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                AppDialogs.ShowSuccess(status, UiText.Cargo.RestoreCargosSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Cargo.SaveErrorTitle);
            }
        }
    }

    private async void ApplyMultipliersButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await TryBeginWriteAsync(owner, requireBaseline: true))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                var path = _session!.PakPath!;
                var mass = GetMassMultiplier();
                var bias = GetComBiasIndex();
                var result = await Task.Run(() => CargoService.ApplyGlobal(path, mass, bias));
                ReloadCargos();
                var status = UiText.Cargo.MultipliersAppliedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                AppDialogs.ShowSuccess(status, UiText.Cargo.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Cargo.SaveErrorTitle);
            }
        }
    }

    private async void SaveIndividualButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await TryBeginWriteAsync(owner, requireBaseline: false))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(CargosGrid);
                var cargos = _cargos.Select(row => row.ToDefinition()).ToArray();
                var path = _session!.PakPath!;
                var result = await Task.Run(() => CargoService.SaveCargoChanges(path, cargos));
                ReloadCargos();
                var status = UiText.Cargo.IndividualSavedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                AppDialogs.ShowSuccess(status, UiText.Cargo.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Cargo.SaveErrorTitle);
            }
        }
    }

    private void ReloadCargos()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            _cargos.Clear();
            RefreshFilter();
            PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton);
            ReportStatus(UiText.Cargo.LoadPakFirst);
            return;
        }

        try
        {
            ApplyCargos(CargoService.LoadCargos(_session.PakPath, AppLanguage.Current));
            ReportStatus(UiText.Cargo.LoadedStatus(_cargos.Count));
        }
        catch (Exception ex)
        {
            _cargos.Clear();
            RefreshFilter();
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Cargo.LoadErrorTitle);
            }

            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
        }

        RefreshWriteButtons();
    }

    private void ApplyCargos(IReadOnlyList<CargoDefinition> cargos)
    {
        _cargos.Clear();
        foreach (var cargo in cargos)
        {
            _cargos.Add(CargoRowViewModel.FromDefinition(cargo));
        }

        RefreshFilter();
    }

    private async Task<bool> TryBeginWriteAsync(Window owner, bool requireBaseline)
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            ReportStatus(UiText.Cargo.LoadPakFirst);
            return false;
        }

        if (_session.IsGameRunning)
        {
            ReportStatus(UiText.Main.GameRunningBanner);
            return false;
        }

        if (requireBaseline && !PakBaselineService.HasBaseline(_session.PakPath))
        {
            ReportStatus(UiText.Cargo.LoadPakFirst);
            return false;
        }

        return await PakWriteUi.TryBeginWrite(
            owner,
            _session,
            _session.PakPath,
            writesAllowed: true,
            requireBaseline,
            () => ReportStatus(UiText.Cargo.LoadPakFirst));
    }

    private void RefreshWriteButtons()
    {
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            _session?.PakPath,
            writesAllowed: true,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreCargosButton);
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
            CargosGrid,
            _cargos,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.DisplayName,
                row.Name,
                row.CargoType));

    private void ResetMultiplierSlidersToBaseline()
    {
        MassMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        ComBiasSlider.Value = CargoService.ComBiasBaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (MassMultiplierLabel is null || ComBiasLabel is null)
        {
            return;
        }

        MassMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.Mass,
            GetMassMultiplierIndex());
        ComBiasLabel.Text = UiText.Cargo.ComBiasCaption(GetComBiasIndex());
    }

    private int GetMassMultiplierIndex() =>
        TuningMultiplierPresets.ClampMassIndex(
            (int)Math.Round(MassMultiplierSlider.Value, MidpointRounding.AwayFromZero));

    private double GetMassMultiplier() =>
        TuningMultiplierPresets.GetValue(GetMassMultiplierIndex());

    private int GetComBiasIndex() =>
        Math.Clamp(
            (int)Math.Round(ComBiasSlider.Value, MidpointRounding.AwayFromZero),
            CargoService.ComBiasLowIndex,
            CargoService.ComBiasHighIndex);

    private void ReportStatus(string message) =>
        StatusText.Text = message;

    public sealed class CargoRowViewModel : INotifyPropertyChanged
    {
        private double _totalMass;
        private double? _centerOfMassY;
        private double? _friction;
        private bool _hasCenterOfMassY;
        private bool _hasFriction;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string CargoType { get; init; }
        public required string SourceFile { get; init; }

        public double TotalMass
        {
            get => _totalMass;
            set
            {
                if (Math.Abs(_totalMass - value) < 1e-12)
                {
                    return;
                }

                _totalMass = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalMass)));
            }
        }

        public double? CenterOfMassY
        {
            get => _centerOfMassY;
            set
            {
                if (_centerOfMassY == value)
                {
                    return;
                }

                _centerOfMassY = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CenterOfMassY)));
            }
        }

        public double? Friction
        {
            get => _friction;
            set
            {
                if (_friction == value)
                {
                    return;
                }

                _friction = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Friction)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static CargoRowViewModel FromDefinition(CargoDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                CargoType = definition.CargoType,
                SourceFile = definition.SourceFile,
                _totalMass = definition.TotalMass,
                _hasCenterOfMassY = definition.HasCenterOfMassY,
                _centerOfMassY = definition.HasCenterOfMassY ? definition.CenterOfMassY : null,
                _hasFriction = definition.HasFriction,
                _friction = definition.HasFriction ? definition.Friction : null,
            };

        public CargoDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                CargoType = CargoType,
                SourceFile = SourceFile,
                TotalMass = TotalMass,
                HasCenterOfMassY = _hasCenterOfMassY,
                CenterOfMassY = _hasCenterOfMassY ? _centerOfMassY ?? 0 : 0,
                HasFriction = _hasFriction,
                Friction = _hasFriction ? _friction ?? 0 : 0,
            };
    }
}
