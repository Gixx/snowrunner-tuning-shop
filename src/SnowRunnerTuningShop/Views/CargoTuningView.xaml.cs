using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Cargo;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class CargoTuningView : UserControl
{
    private readonly ObservableCollection<CargoRowViewModel> _cargos = [];
    private readonly ICollectionView _cargosView;
    private AppSession? _session;

    public CargoTuningView()
    {
        InitializeComponent();
        _cargosView = CollectionViewSource.GetDefaultView(_cargos);
        _cargosView.Filter = MatchesFilter;
        CargosGrid.ItemsSource = _cargosView;
        ResetMultiplierSlidersToBaseline();
    }

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

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshWriteButtons();
        ReloadCargos();
    }

    private void RestoreCargosButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginWrite(requireBaseline: true))
        {
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Cargo.ConfirmRestoreMessage,
            UiText.Cargo.ConfirmRestoreTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question,
            MessageBoxResult.No);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                var result = CargoService.RestoreCargosFromBaseline(_session!.PakPath!);
                ResetMultiplierSlidersToBaseline();
                ReloadCargos();
                var status = UiText.Cargo.MultipliersAppliedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                UiFeedback.ShowSuccess(status, UiText.Cargo.RestoreCargosSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Cargo.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginWrite(requireBaseline: true))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                var result = CargoService.ApplyGlobal(
                    _session!.PakPath!,
                    GetMassMultiplier(),
                    GetComBiasIndex());

                ReloadCargos();
                var status = UiText.Cargo.MultipliersAppliedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                UiFeedback.ShowSuccess(status, UiText.Cargo.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Cargo.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!TryBeginWrite(requireBaseline: false))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreCargosButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(CargosGrid);

                var cargos = _cargos
                    .Select(row => row.ToDefinition())
                    .ToArray();

                var result = CargoService.SaveCargoChanges(_session!.PakPath!, cargos);
                ReloadCargos();
                var status = UiText.Cargo.IndividualSavedStatus(result.ChangedCargos, result.UpdatedFiles);
                ReportStatus(status);
                UiFeedback.ShowSuccess(status, UiText.Cargo.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Cargo.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ReloadCargos()
    {
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            _cargos.Clear();
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
            MessageBox.Show(ex.Message, UiText.Cargo.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
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
    }

    private bool TryBeginWrite(bool requireBaseline)
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

        return PakWriteUi.TryBeginWrite(
            _session,
            _session.PakPath,
            writesAllowed: true,
            requireBaseline,
            () => ReportStatus(UiText.Cargo.LoadPakFirst));
    }

    private void RefreshWriteButtons()
    {
        var pakPath = _session?.PakPath;
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            pakPath,
            writesAllowed: true,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreCargosButton);
    }

    private void MultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) =>
        UpdateMultiplierLabels();

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _cargosView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is CargoRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.DisplayName,
            row.Name,
            row.CargoType);

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
