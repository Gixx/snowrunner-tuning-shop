using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
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
    private AccordionDirtyHeader? _globalDirty;
    private bool _suppressDirty;

    public WinchTuningView()
    {
        InitializeComponent();
        _winchesView = CollectionViewSource.GetDefaultView(_winches);
        _winchesView.Filter = MatchesFilter;
        WinchesGrid.ItemsSource = _winchesView;
        _globalDirty = new AccordionDirtyHeader(GlobalMultipliersExpander, UiText.Winch.GlobalMultipliersTitle);
        AutonomousAllCheckBox.Checked += (_, _) => { if (!_suppressDirty) _globalDirty?.MarkDirty(); };
        AutonomousAllCheckBox.Unchecked += (_, _) => { if (!_suppressDirty) _globalDirty?.MarkDirty(); };
        RestoreMultiplierSlidersFromStore();
        GlobalMultiplierUiStore.ActivePresetChanged += OnActivePresetChanged;
        Unloaded += (_, _) => GlobalMultiplierUiStore.ActivePresetChanged -= OnActivePresetChanged;
    }

    public event EventHandler<string>? StatusChanged;

    public string? PakPath { get; private set; }

    public void AttachSession(AppSession session) => _session = session;

    private void OnActivePresetChanged(object? sender, EventArgs e) =>
        Dispatcher.Invoke(RestoreMultiplierSlidersFromStore);

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
        PartsTuningUiHelpers.ClearWriteButtons(ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton);
    }

    public void RefreshRestoreButton() =>
        PartsTuningUiHelpers.SetPartWriteButtonStates(
            _session,
            PakPath,
            _pakWritesAllowed,
            ApplyMultipliersButton,
            SaveIndividualButton,
            RestoreWinchesButton);

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadWinches();
    }

    private void RestoreWinchesButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                var result = WinchService.RestoreWinchesFromBaseline(PakPath);
                GlobalMultiplierUiStore.ResetWinches();
                RestoreMultiplierSlidersFromStore();
                ReloadWinches();
                ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                    result.ChangedWinches,
                    result.UpdatedFiles));
                UiFeedback.ShowSuccess(
                    UiText.Winch.RestoreWinchesMessage(result.ChangedWinches, result.UpdatedFiles),
                    UiText.Winch.RestoreWinchesSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void ApplyMultipliersButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                var result = WinchService.ApplyGlobalMultipliers(
                    PakPath,
                    GetLengthMultiplier(),
                    GetStrengthMultiplier(),
                    AutonomousAllCheckBox.IsChecked == true,
                    GetPriceMultiplier());

                SaveMultiplierSlidersToStore();
                _globalDirty?.ClearDirty();
                ReloadWinches();
                ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                    result.ChangedWinches,
                    result.UpdatedFiles));
                UiFeedback.ShowSuccess(
                    UiText.Winch.MultipliersSavedMessage(result.ChangedWinches, result.UpdatedFiles),
                    UiText.Winch.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void SaveIndividualButton_Click(object sender, RoutedEventArgs e)
    {
        if (!PakWriteUi.TryBeginWrite(_session, PakPath, _pakWritesAllowed, requireBaseline: false,
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(WinchesGrid);

                var winches = _winches
                    .Select(row => row.ToDefinition())
                    .ToArray();

                var result = WinchService.SaveWinchChanges(PakPath, winches);
                ReloadWinches();
                ReportStatus(UiText.Winch.IndividualSavedStatus(result.ChangedWinches, result.UpdatedFiles));
                UiFeedback.ShowSuccess(
                    UiText.Winch.IndividualSavedMessage(result.ChangedWinches),
                    UiText.Winch.SaveSuccessTitle);
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
        if (!_suppressDirty)
        {
            _globalDirty?.MarkDirty();
        }
    }

    private void StrengthMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMultiplierLabels();
        if (!_suppressDirty)
        {
            _globalDirty?.MarkDirty();
        }
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

    private void RestoreMultiplierSlidersFromStore()
    {
        _suppressDirty = true;
        try
        {
            var state = GlobalMultiplierUiStore.GetWinches();
            LengthMultiplierSlider.Value = TuningMultiplierPresets.ClampIndex(state.LengthIndex);
            StrengthMultiplierSlider.Value = TuningMultiplierPresets.ClampIndex(state.StrengthIndex);
            PriceMultiplierSlider.Value = TuningMultiplierPresets.ClampIndex(state.PriceIndex);
            AutonomousAllCheckBox.IsChecked = state.AutonomousAll;
            UpdateMultiplierLabels();
            _globalDirty?.ClearDirty();
        }
        finally
        {
            _suppressDirty = false;
        }
    }

    private void SaveMultiplierSlidersToStore() =>
        GlobalMultiplierUiStore.SetWinches(new WinchGlobalUiState
        {
            LengthIndex = GetLengthMultiplierIndex(),
            StrengthIndex = GetStrengthMultiplierIndex(),
            PriceIndex = GetPriceMultiplierIndex(),
            AutonomousAll = AutonomousAllCheckBox.IsChecked == true,
        });

    private void UpdateMultiplierLabels()
    {
        if (LengthMultiplierLabel is null
            || StrengthMultiplierLabel is null
            || PriceMultiplierLabel is null)
        {
            return;
        }

        LengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.LengthMultiplier,
            GetLengthMultiplierIndex());
        StrengthMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StrengthMultiplier,
            GetStrengthMultiplierIndex());
        PriceMultiplierLabel.Text = UiText.Slider.Caption(
            UiText.Slider.StorePrice,
            GetPriceMultiplierIndex());
    }

    private int GetLengthMultiplierIndex() =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(LengthMultiplierSlider.Value));

    private int GetStrengthMultiplierIndex() =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(StrengthMultiplierSlider.Value));

    private int GetPriceMultiplierIndex() =>
        TuningMultiplierPresets.ClampIndex((int)Math.Round(PriceMultiplierSlider.Value));

    private double GetLengthMultiplier() =>
        TuningMultiplierPresets.GetValue(GetLengthMultiplierIndex());

    private double GetStrengthMultiplier() =>
        TuningMultiplierPresets.GetValue(GetStrengthMultiplierIndex());

    private double GetPriceMultiplier() =>
        TuningMultiplierPresets.GetValue(GetPriceMultiplierIndex());

    private void ReportStatus(string message) =>
        StatusChanged?.Invoke(this, message);

    public sealed class WinchRowViewModel : INotifyPropertyChanged
    {
        private double _length;
        private double _strengthMult;
        private bool _isEngineIgnitionRequired;
        private int _price;

        public required string EntryPath { get; init; }
        public required string Name { get; init; }
        public required string DisplayName { get; init; }
        public required string SourceFile { get; init; }
        public required string Category { get; init; }

        public int Price
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
