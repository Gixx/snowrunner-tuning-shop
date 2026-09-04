using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Winch;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class WinchTuningView : UserControl
{
    private readonly ObservableCollection<WinchRowViewModel> _winches = [];
    private readonly ICollectionView _winchesView;
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public WinchTuningView()
    {
        InitializeComponent();
        _winchesView = CollectionViewSource.GetDefaultView(_winches);
        _winchesView.Filter = MatchesFilter;
        WinchesGrid.ItemsSource = _winchesView;
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
        ReloadWinches();
    }

    public async Task LoadFromPakAsync(string pakPath, CancellationToken cancellationToken = default)
    {
        PakPath = pakPath;
        RefreshRestoreButton();
        await ReloadWinchesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _winches.Clear();
        ApplyMultipliersButton.IsEnabled = false;
        SaveIndividualButton.IsEnabled = false;
        RestoreWinchesButton.IsEnabled = false;
    }

    public void RefreshRestoreButton()
    {
        var hasPak = !string.IsNullOrWhiteSpace(PakPath);
        ApplyMultipliersButton.IsEnabled = hasPak && _pakWritesAllowed;
        SaveIndividualButton.IsEnabled = hasPak && _pakWritesAllowed;
        RestoreWinchesButton.IsEnabled = hasPak
            && _pakWritesAllowed
            && PakBaselineService.HasBaseline(PakPath!);
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadWinches();
    }

    private void RestoreWinchesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = WinchService.RestoreWinchesFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            AutonomousAllCheckBox.IsChecked = false;
            ReloadWinches();
            ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                result.ChangedWinches,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Winch.RestoreWinchesMessage(
                    result.ChangedWinches,
                    result.UpdatedFiles),
                UiText.Winch.RestoreWinchesSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        try
        {
            var result = WinchService.ApplyGlobalMultipliers(
                PakPath,
                GetLengthMultiplier(),
                GetStrengthMultiplier(),
                AutonomousAllCheckBox.IsChecked == true);

            ReloadWinches();
            ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                result.ChangedWinches,
                result.UpdatedFiles));

            MessageBox.Show(
                UiText.Winch.MultipliersSavedMessage(
                    result.ChangedWinches,
                    result.UpdatedFiles),
                UiText.Winch.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryProceed(_session) || !_pakWritesAllowed)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(PakPath))
        {
            MessageBox.Show(UiText.Winch.LoadPakFirst, UiText.Winch.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            // Flush checkbox / cell edits before reading the view-models.
            WinchesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            WinchesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var winches = _winches
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = WinchService.SaveWinchChanges(PakPath, winches);
            ReloadWinches();

            MessageBox.Show(
                UiText.Winch.IndividualSavedMessage(result.ChangedWinches),
                UiText.Winch.SaveSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ReloadWinches()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            ApplyWinches(WinchService.LoadWinches(PakPath, AppLanguage.Current));
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Winch.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task ReloadWinchesAsync(CancellationToken cancellationToken = default)
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
            var winches = await Task.Run(() => WinchService.LoadWinches(path, language), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            ApplyWinches(winches);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Stale load discarded after tab/pak switch.
        }
        catch (Exception ex)
        {
            Clear();
            MessageBox.Show(ex.Message, UiText.Winch.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ApplyWinches(IReadOnlyList<WinchDefinition> winches)
    {
        _winches.Clear();
        foreach (var winch in winches)
        {
            _winches.Add(WinchRowViewModel.FromDefinition(winch));
        }
    }

    private void LengthMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMultiplierLabels();
    }

    private void StrengthMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMultiplierLabels();
    }

    private void FilterTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        TuningListFilter.UpdatePlaceholderVisibility(FilterTextBox, FilterPlaceholder);
        _winchesView.Refresh();
    }

    private bool MatchesFilter(object item) =>
        item is WinchRowViewModel row
        && TuningListFilter.Matches(
            FilterTextBox.Text,
            row.Category,
            row.DisplayName,
            row.Name);

    private void ResetMultiplierSlidersToBaseline()
    {
        LengthMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        StrengthMultiplierSlider.Value = TuningMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (LengthMultiplierLabel is null || StrengthMultiplierLabel is null)
        {
            return;
        }

        LengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.LengthMultiplier,
            GetLengthMultiplierIndex());
        StrengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StrengthMultiplier,
            GetStrengthMultiplierIndex());
    }

    private int GetLengthMultiplierIndex() =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(LengthMultiplierSlider.Value));

    private int GetStrengthMultiplierIndex() =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(StrengthMultiplierSlider.Value));

    private double GetLengthMultiplier() =>
        TuningMultiplierPresets.GetValue(GetLengthMultiplierIndex());

    private double GetStrengthMultiplier() =>
        TuningMultiplierPresets.GetValue(GetStrengthMultiplierIndex());

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class WinchRowViewModel : INotifyPropertyChanged
    {
        private double _length;
        private double _strengthMult;
        private bool _isEngineIgnitionRequired;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string Category { get; init; }
        public int Price { get; init; }

        public double Length
        {
            get => _length;
            set
            {
                if (Math.Abs(_length - value) < 0.0001)
                {
                    return;
                }

                _length = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Length)));
            }
        }

        public double StrengthMult
        {
            get => _strengthMult;
            set
            {
                if (Math.Abs(_strengthMult - value) < 0.0001)
                {
                    return;
                }

                _strengthMult = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StrengthMult)));
            }
        }

        public bool IsAutonomous
        {
            get => !_isEngineIgnitionRequired;
            set
            {
                var engineRequired = !value;
                if (_isEngineIgnitionRequired == engineRequired)
                {
                    return;
                }

                _isEngineIgnitionRequired = engineRequired;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAutonomous)));
            }
        }

        private bool IsEngineIgnitionRequired
        {
            get => _isEngineIgnitionRequired;
            set
            {
                if (_isEngineIgnitionRequired == value)
                {
                    return;
                }

                _isEngineIgnitionRequired = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEngineIgnitionRequired)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public static WinchRowViewModel FromDefinition(WinchDefinition definition) =>
            new()
            {
                EntryPath = definition.EntryPath,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                SourceFile = definition.SourceFile,
                Category = definition.Category,
                Price = definition.Price,
                Length = definition.Length,
                StrengthMult = definition.StrengthMult,
                IsEngineIgnitionRequired = definition.IsEngineIgnitionRequired,
            };

        public WinchDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                DisplayName = DisplayName,
                SourceFile = SourceFile,
                Category = Category,
                Price = Price,
                Length = Length,
                StrengthMult = StrengthMult,
                IsEngineIgnitionRequired = !IsAutonomous,
            };
    }
}
