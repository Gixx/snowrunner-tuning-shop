using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Models;
using SnowRunnerTuningShop.Core.Winch;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class WinchTuningView : UserControl
{
    private readonly ObservableCollection<WinchRowViewModel> _winches = [];

    public WinchTuningView()
    {
        InitializeComponent();
        WinchesGrid.ItemsSource = _winches;
        ResetMultiplierSlidersToBaseline();
    }

    public event EventHandler<string>? StatusChanged;

    public string? PakPath { get; private set; }

    public void LoadFromPak(string pakPath)
    {
        PakPath = pakPath;
        RefreshBaselineStatus();
        ReloadWinches();
    }

    public void Clear()
    {
        PakPath = null;
        _winches.Clear();
        BaselineStatusTextBlock.Text = string.Empty;
        ImportPythonBaselineButton.IsEnabled = false;
        RestoreWinchesButton.IsEnabled = false;
        RestorePakButton.IsEnabled = false;
        WinchInfoTextBlock.Text = UiText.Winch.NoData;
    }

    private void ReloadButton_Click(object sender, RoutedEventArgs e)
    {
        RefreshBaselineStatus();
        ReloadWinches();
    }

    private void SetBaselineFromFileButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = UiText.Winch.SelectBaselineDialogTitle,
            Filter = UiText.Main.BrowseDialogFilter,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var baseline = PakBaselineService.SetBaselineFromFile(PakPath, dialog.FileName);
            RefreshBaselineStatus();
            ReportStatus($"Baseline set from {baseline.SourceDescription}.");
            MessageBox.Show(
                UiText.Winch.BaselineImportedMessage(baseline.SourceDescription, baseline.BaselinePath),
                UiText.Winch.BaselineUpdatedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportPythonBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        try
        {
            var baseline = PakBaselineService.ImportOldestPythonEditorBackup(PakPath);
            RefreshBaselineStatus();
            ReportStatus($"Baseline imported from Python editor backup.");
            MessageBox.Show(
                UiText.Winch.BaselineImportedMessage(baseline.SourceDescription, baseline.BaselinePath),
                UiText.Winch.BaselineUpdatedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Winch.SaveErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ClearBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        PakBaselineService.ClearBaseline(PakPath);
        RefreshBaselineStatus();
        ReportStatus("Baseline cleared.");
    }

    private void RestoreWinchesButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Winch.BaselineMissing(BuildPythonBackupHint()),
                UiText.Winch.BaselineTitle,
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
                result.UpdatedFiles,
                Path.GetFileName(result.BackupPath)));

            MessageBox.Show(
                UiText.Winch.RestoreWinchesMessage(
                    result.ChangedWinches,
                    result.UpdatedFiles,
                    result.BackupPath),
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

    private void RestorePakButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Winch.BaselineMissing(BuildPythonBackupHint()),
                UiText.Winch.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Winch.RestorePakConfirmMessage,
            UiText.Winch.RestorePakConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var backupPath = PakBaselineService.RestorePakFromBaseline(PakPath);
            ResetMultiplierSlidersToBaseline();
            AutonomousAllCheckBox.IsChecked = false;
            ReloadWinches();
            ReportStatus($"Entire pak restored from baseline. Backup: {Path.GetFileName(backupPath)}");

            MessageBox.Show(
                UiText.Winch.RestorePakMessage(backupPath),
                UiText.Winch.RestorePakSuccessTitle,
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
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        if (!PakBaselineService.HasBaseline(PakPath))
        {
            MessageBox.Show(
                UiText.Winch.BaselineMissing(BuildPythonBackupHint()),
                UiText.Winch.BaselineTitle,
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
                result.UpdatedFiles,
                Path.GetFileName(result.BackupPath)));

            MessageBox.Show(
                UiText.Winch.MultipliersSavedMessage(
                    result.ChangedWinches,
                    result.UpdatedFiles,
                    result.BackupPath),
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
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            ReportStatus(UiText.Winch.LoadPakFirst);
            return;
        }

        try
        {
            WinchesGrid.CommitEdit(DataGridEditingUnit.Cell, exitEditingMode: true);
            WinchesGrid.CommitEdit(DataGridEditingUnit.Row, exitEditingMode: true);

            var winches = _winches
                .Select(row => row.ToDefinition())
                .ToArray();

            var result = WinchService.SaveWinchChanges(PakPath, winches);
            ReloadWinches();

            ReportStatus(UiText.Winch.IndividualSavedStatus(result.ChangedWinches, result.UpdatedFiles));

            MessageBox.Show(
                UiText.Winch.IndividualSavedMessage(result.ChangedWinches, result.BackupPath),
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

    private void ReloadWinches()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            Clear();
            return;
        }

        try
        {
            var winches = WinchService.LoadWinches(PakPath, AppLanguage.Current);
            _winches.Clear();
            foreach (var winch in winches)
            {
                _winches.Add(WinchRowViewModel.FromDefinition(winch));
            }

            WinchInfoTextBlock.Text = UiText.Winch.LoadedCount(winches.Count);
            ReportStatus(UiText.Winch.LoadedStatus(winches.Count));
        }
        catch (Exception ex)
        {
            Clear();
            ReportStatus(UiText.Winch.LoadErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Winch.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshBaselineStatus()
    {
        if (string.IsNullOrWhiteSpace(PakPath))
        {
            BaselineStatusTextBlock.Text = string.Empty;
            ImportPythonBaselineButton.IsEnabled = false;
            RestoreWinchesButton.IsEnabled = false;
            RestorePakButton.IsEnabled = false;
            return;
        }

        var pythonBackups = PakBaselineService.FindPythonEditorBackups(PakPath);
        ImportPythonBaselineButton.IsEnabled = pythonBackups.Count > 0;

        var baseline = PakBaselineService.TryGetBaselineInfo(PakPath);
        var hasBaseline = baseline is not null;
        RestoreWinchesButton.IsEnabled = hasBaseline;
        RestorePakButton.IsEnabled = hasBaseline;

        if (!hasBaseline)
        {
            BaselineStatusTextBlock.Text = UiText.Winch.BaselineMissing(BuildPythonBackupHint(pythonBackups.Count));
            return;
        }

        BaselineStatusTextBlock.Text = UiText.Winch.BaselineReady(
            baseline.SourceDescription,
            Path.GetFileName(baseline.BaselinePath),
            baseline.LastWriteTimeUtc);
    }

    private string? BuildPythonBackupHint(int? pythonBackupCount = null)
    {
        var count = pythonBackupCount ?? (string.IsNullOrWhiteSpace(PakPath)
            ? 0
            : PakBaselineService.FindPythonEditorBackups(PakPath).Count);

        return count > 0
            ? UiText.Winch.PythonBackupsFound(count)
            : UiText.Winch.PythonBackupsMissing;
    }

    private void LengthMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMultiplierLabels();
    }

    private void StrengthMultiplierSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        UpdateMultiplierLabels();
    }

    private void ResetMultiplierSlidersToBaseline()
    {
        LengthMultiplierSlider.Value = WinchMultiplierPresets.BaselineIndex;
        StrengthMultiplierSlider.Value = WinchMultiplierPresets.BaselineIndex;
        UpdateMultiplierLabels();
    }

    private void UpdateMultiplierLabels()
    {
        if (LengthMultiplierLabel is null || StrengthMultiplierLabel is null)
        {
            return;
        }

        LengthMultiplierLabel.Text = WinchMultiplierPresets.FormatSliderCaption(
            "Length multiplier",
            GetLengthMultiplierIndex());
        StrengthMultiplierLabel.Text = WinchMultiplierPresets.FormatSliderCaption(
            "Strength multiplier",
            GetStrengthMultiplierIndex());
    }

    private int GetLengthMultiplierIndex() =>
        WinchMultiplierPresets.ClampIndex((int)Math.Round(LengthMultiplierSlider.Value));

    private int GetStrengthMultiplierIndex() =>
        WinchMultiplierPresets.ClampIndex((int)Math.Round(StrengthMultiplierSlider.Value));

    private double GetLengthMultiplier() =>
        WinchMultiplierPresets.GetValue(GetLengthMultiplierIndex());

    private double GetStrengthMultiplier() =>
        WinchMultiplierPresets.GetValue(GetStrengthMultiplierIndex());

    private void ReportStatus(string message)
    {
        StatusChanged?.Invoke(this, message);
    }

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
                Length = definition.Length,
                StrengthMult = definition.StrengthMult,
                IsEngineIgnitionRequired = definition.IsEngineIgnitionRequired,
            };

        public WinchDefinition ToDefinition() =>
            new()
            {
                EntryPath = EntryPath,
                Name = Name,
                SourceFile = SourceFile,
                Category = Category,
                Length = Length,
                StrengthMult = StrengthMult,
                IsEngineIgnitionRequired = IsEngineIgnitionRequired,
            };
    }
}
