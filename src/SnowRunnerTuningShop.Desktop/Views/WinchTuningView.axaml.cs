using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Tuning;
using SnowRunnerTuningShop.Core.Winch;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class WinchTuningView : UserControl
{
    private readonly ObservableCollection<WinchRowViewModel> _winches = [];
    private bool _pakWritesAllowed = true;
    private AppSession? _session;

    public WinchTuningView()
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
        await ReloadWinchesAsync(cancellationToken);
    }

    public void Clear()
    {
        PakPath = null;
        _winches.Clear();
        RefreshFilter();
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

    private void ApplyStaticText()
    {
        MultipliersExpander.Header = UiText.Winch.GlobalMultipliersTitle;
        AutonomousAllCheckBox.Content = UiText.Winch.AutonomousAll;
        ApplyMultipliersButton.Content = UiText.Winch.Apply;
        SaveIndividualButton.Content = UiText.Winch.SaveIndividualChanges;
        RestoreWinchesButton.Content = UiText.Winch.RestoreWinchesToBaseline;
        ReloadButton.Content = UiText.Winch.RefreshList;
        FilterTextBox.PlaceholderText = UiText.Winch.FilterPlaceholder;
        PartsTuningUiHelpers.SetColumnHeaders(
            WinchesGrid,
            UiText.Winch.CategoryColumn,
            UiText.Winch.NameColumn,
            UiText.Winch.PriceColumn,
            UiText.Winch.LengthColumn,
            UiText.Winch.StrengthColumn,
            UiText.Winch.AutonomousColumn);
    }

    private void ReloadButton_Click(object? sender, RoutedEventArgs e)
    {
        RefreshRestoreButton();
        ReloadWinches();
    }

    private async void RestoreWinchesButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryBeginWrite(owner, _session, PakPath, _pakWritesAllowed, requireBaseline: true,
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                var path = PakPath!;
                var result = await Task.Run(() => WinchService.RestoreWinchesFromBaseline(path));
                ResetMultiplierSlidersToBaseline();
                AutonomousAllCheckBox.IsChecked = false;
                ReloadWinches();
                ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                    result.ChangedWinches,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Winch.SaveErrorTitle);
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
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                var path = PakPath!;
                var length = GetLengthMultiplier();
                var strength = GetStrengthMultiplier();
                var autonomous = AutonomousAllCheckBox.IsChecked == true;
                var result = await Task.Run(() => WinchService.ApplyGlobalMultipliers(
                    path,
                    length,
                    strength,
                    autonomous));

                ReloadWinches();
                ReportStatus(UiText.Winch.MultipliersAppliedStatus(
                    result.ChangedWinches,
                    result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Winch.SaveErrorTitle);
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
                () => ReportStatus(UiText.Winch.LoadPakFirst)))
        {
            return;
        }

        using (PakWriteUi.BeginBusyWrite(owner, ApplyMultipliersButton, SaveIndividualButton, RestoreWinchesButton))
        {
            try
            {
                PartsTuningUiHelpers.CommitGridEdits(WinchesGrid);

                var winches = _winches
                    .Select(row => row.ToDefinition())
                    .ToArray();
                var path = PakPath!;
                var result = await Task.Run(() => WinchService.SaveWinchChanges(path, winches));
                ReloadWinches();
                ReportStatus(UiText.Winch.IndividualSavedStatus(result.ChangedWinches, result.UpdatedFiles));
            }
            catch (Exception ex)
            {
                ReportStatus(UiText.Main.ErrorStatus(ex.Message));
                await AppDialogs.ShowError(owner, ex.Message, UiText.Winch.SaveErrorTitle);
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
            if (OwnerWindow is { } owner)
            {
                _ = AppDialogs.ShowError(owner, ex.Message, UiText.Winch.LoadErrorTitle);
            }
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
            if (OwnerWindow is { } owner)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Winch.LoadErrorTitle);
            }
        }
    }

    private void ApplyWinches(IReadOnlyList<WinchDefinition> winches)
    {
        _winches.Clear();
        foreach (var winch in winches)
        {
            _winches.Add(WinchRowViewModel.FromDefinition(winch));
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
            WinchesGrid,
            _winches,
            row => TuningListFilter.Matches(
                FilterTextBox.Text,
                row.Category,
                row.DisplayName,
                row.Name));

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
