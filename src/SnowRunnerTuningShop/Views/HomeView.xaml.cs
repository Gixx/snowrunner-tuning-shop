using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Presets;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class HomeView : UserControl
{
    private AppSession? _session;
    private bool _autoLoadAttempted;
    private AppUpdateCheckResult? _availableUpdate;
    private string? _selectedPresetId;

    public HomeView()
    {
        InitializeComponent();
        Loaded += HomeView_Loaded;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) =>
        {
            RefreshWorkspaceUi();
            RefreshFromSession();
        };
        _session.BaselineChanged += (_, _) => RefreshWorkspaceUi();
        _session.GameRunningChanged += (_, _) =>
        {
            RefreshWorkspaceUi();
            RefreshPresetActions();
        };
        _session.TuningChanged += (_, _) => RefreshPresetsList();

        if (!_autoLoadAttempted)
        {
            _autoLoadAttempted = true;
            TryAutoLoadWorkspace();
        }
        else
        {
            RefreshWorkspaceUi();
            RefreshFromSession();
        }
    }

    /// <summary>Import a .tsa opened via file association / command line.</summary>
    public void TryImportPendingArchive(string? archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return;
        }

        try
        {
            ImportArchive(archivePath, quietSuccess: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetImportTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    /// <summary>Called when Home becomes the active page so preset dirty state is current.</summary>
    public void RefreshPresetsFromOutside() => RefreshPresetsList();

    private void TryAutoLoadWorkspace()
    {
        var workspace = WorkspaceConfigStore.TryGetActiveWorkspace();
        if (workspace is null || !workspace.BaselineExists || !File.Exists(workspace.WorkingPakPath))
        {
            RefreshWorkspaceUi();
            RefreshFromSession();
            return;
        }

        try
        {
            LoadWorkingPak(workspace.WorkingPakPath);
        }
        catch (Exception ex)
        {
            _session?.ClearPak();
            RefreshWorkspaceUi();
            MessageBox.Show(ex.Message, UiText.Main.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SetBaselineButton_Click(object sender, RoutedEventArgs e) =>
        ActivateFromBrowse(changeLocation: false);

    private void ChangeLocationButton_Click(object sender, RoutedEventArgs e) =>
        ActivateFromBrowse(changeLocation: true);

    private void ActivateFromBrowse(bool changeLocation)
    {
        if (_session is null)
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = changeLocation
                ? UiText.Main.ChangeLocationDialogTitle
                : UiText.Main.SelectOriginalPakDialogTitle,
            Filter = UiText.Main.BrowseDialogFilter,
            CheckFileExists = true,
            FileName = "initial.pak",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (changeLocation)
            {
                var fullPath = Path.GetFullPath(dialog.FileName);
                var edition = GameEditionDetector.Detect(fullPath);
                if (!PakBaselineService.HasBaselineForEdition(edition.Id)
                    && TuningProfileMarker.HasMarker(fullPath))
                {
                    var confirm = MessageBox.Show(
                        UiText.Main.ChangeLocationMarkedPakConfirm,
                        UiText.Main.ChangeLocationMarkedPakTitle,
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);
                    if (confirm != MessageBoxResult.Yes)
                    {
                        return;
                    }
                }
            }

            var result = changeLocation
                ? PakBaselineService.ChangeLocation(dialog.FileName)
                : PakBaselineService.SetBaselineFromOriginal(dialog.FileName);

            LoadWorkingPak(result.WorkingPakPath);
            _session.NotifyBaselineChanged();
            RefreshWorkspaceUi();

            MessageBox.Show(
                changeLocation
                    ? UiText.Main.LocationChangedMessage(
                        result.EditionDisplayName,
                        result.WorkingPakPath,
                        result.BaselinePath,
                        result.BaselineCreated)
                    : UiText.Main.BaselineCreatedMessage(
                        result.EditionDisplayName,
                        result.WorkingPakPath,
                        result.BaselinePath),
                changeLocation ? UiText.Main.LocationChangedTitle : UiText.Main.BaselineUpdatedTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.BaselineErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RestoreFullBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (WorkspaceCommands.TryRestoreFullBaseline(_session))
        {
            _selectedPresetId = null;
            RefreshPresetsList();
        }
    }

    private void RefreshBaselineButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        WorkspaceCommands.TryRefreshBaselineFromGame(_session);
    }

    private void ReapplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        WorkspaceCommands.TryReapplySavedChanges(_session);
        RefreshPresetsList();
    }

    private void PresetNewButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        if (WorkspaceCommands.TryStartNewPreset(_session))
        {
            _selectedPresetId = null;
            RefreshPresetsList();
        }
    }

    private void PresetsListView_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        while (source is not null && source is not ListView)
        {
            if (source is ListViewItem item)
            {
                if (item.IsSelected)
                {
                    PresetsListView.SelectedItem = null;
                    e.Handled = true;
                }

                return;
            }

            source = VisualTreeHelper.GetParent(source);
        }
    }

    private void PresetsListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _selectedPresetId = PresetsListView.SelectedItem is PresetRow row ? row.Id : null;
        RefreshPresetActions();
        RefreshPresetDetail();
    }

    private void PresetImportButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = UiText.Main.PresetImportTitle,
            Filter = UiText.Main.PresetFileFilter,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            ImportArchive(dialog.FileName, quietSuccess: false);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetImportTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PresetExportButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetsListView.SelectedItem is not PresetRow selectedRow
            || selectedRow.Source == TuningPresetSource.Synthetic)
        {
            MessageBox.Show(
                UiText.Main.PresetCannotExportUnnamed,
                UiText.Main.PresetExportTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (selectedRow.Locked)
        {
            MessageBox.Show(
                UiText.Main.PresetCannotExportLocked,
                UiText.Main.PresetExportTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var editionId = WorkspaceConfigStore.Load().ActiveEditionId;
        var profile = string.IsNullOrWhiteSpace(editionId)
            ? null
            : TuningProfileService.TryLoadProfile(editionId);

        var canExportPackage = selectedRow is
        {
            Locked: false,
            Source: not TuningPresetSource.Synthetic,
            ArchivePath: not null,
        };
        if (!canExportPackage && profile?.Entries.Count is not > 0)
        {
            MessageBox.Show(
                UiText.Main.PresetNothingToExport,
                UiText.Main.PresetExportTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var suggestedName = SanitizeFileName(selectedRow.DisplayName);

        var dialog = new SaveFileDialog
        {
            Title = UiText.Main.PresetExportTitle,
            Filter = UiText.Main.PresetFileFilter,
            FileName = suggestedName + TuningPresetArchive.Extension,
            AddExtension = true,
            DefaultExt = "tsa",
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            if (canExportPackage)
            {
                TuningPresetLibrary.ExportPackage(selectedRow.ToInfo(), dialog.FileName);
            }
            else
            {
                var name = Path.GetFileNameWithoutExtension(dialog.FileName);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = selectedRow.DisplayName;
                }

                TuningPresetLibrary.ExportCurrentProfile(
                    dialog.FileName,
                    name,
                    selectedRow.Version is "-" or null or "" ? "1.0.0" : selectedRow.Version,
                    profile!.Entries,
                    locked: false);
            }

            MessageBox.Show(
                UiText.Main.PresetExportSuccess(dialog.FileName),
                UiText.Main.PresetExportSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetExportTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PresetApplyButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null || !PakWriteUi.TryProceed(_session))
        {
            return;
        }

        if (PresetsListView.SelectedItem is not PresetRow row
            || row.Source == TuningPresetSource.Synthetic)
        {
            MessageBox.Show(
                UiText.Main.PresetCannotApplyUnnamed,
                UiText.Main.PresetApplyConfirmTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.PresetApplyConfirmTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Main.PresetApplyConfirm(row.DisplayName),
            UiText.Main.PresetApplyConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            var info = row.ToInfo();
            var dialog = new ReapplyProgressWindow(
                _session.PakPath,
                progress => TuningPresetApplyService.Apply(_session.PakPath!, info, progress))
            {
                Owner = Window.GetWindow(this),
            };

            if (dialog.ShowDialog() != true || dialog.Result is not { } result)
            {
                if (!string.IsNullOrWhiteSpace(dialog.ErrorMessage))
                {
                    MessageBox.Show(
                        dialog.ErrorMessage,
                        UiText.Main.PresetApplyConfirmTitle,
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }

                return;
            }

            LoadWorkingPak(_session.PakPath);
            MessageBox.Show(
                UiText.Main.PresetApplySuccess(row.DisplayName, result.AppliedCount),
                UiText.Main.PresetApplySuccessTitle,
                MessageBoxButton.OK,
                result.MissingEntryPaths.Count > 0 || result.FailedEntryPaths.Count > 0
                    ? MessageBoxImage.Warning
                    : MessageBoxImage.Information);
            RefreshPresetsList(selectId: row.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetApplyConfirmTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PresetSaveAsNewButton_Click(object sender, RoutedEventArgs e)
    {
        var editionId = WorkspaceConfigStore.Load().ActiveEditionId;
        var profile = string.IsNullOrWhiteSpace(editionId)
            ? null
            : TuningProfileService.TryLoadProfile(editionId);
        if (profile?.Entries.Count is not > 0)
        {
            MessageBox.Show(
                UiText.Main.PresetNothingToExport,
                UiText.Main.PresetSaveAsTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var selected = PresetsListView.SelectedItem as PresetRow;
        if (selected?.Locked == true)
        {
            return;
        }

        var initialName = selected is null || selected.Source == TuningPresetSource.Synthetic
            ? ""
            : selected.DisplayName + " copy";
        var dialog = new SavePresetAsWindow(initialName)
        {
            Owner = Window.GetWindow(this),
        };
        if (dialog.ShowDialog() != true)
        {
            return;
        }

        try
        {
            var saved = TuningPresetLibrary.SaveAsNew(
                dialog.PresetName,
                dialog.PresetVersion,
                profile.Entries,
                locked: dialog.PresetLocked,
                author: dialog.PresetAuthor,
                description: dialog.PresetDescription);
            TuningPresetLibrary.SetActivePreset(saved.Id, TuningPresetSource.User);
            MessageBox.Show(
                UiText.Main.PresetSaveAsSuccess(saved.Name),
                UiText.Main.PresetSaveAsTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshPresetsList(selectId: saved.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetSaveAsTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void PresetDeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (PresetsListView.SelectedItem is not PresetRow row
            || row.Source == TuningPresetSource.Synthetic)
        {
            return;
        }

        var confirmMessage = row.Locked
            ? UiText.Main.PresetDeleteConfirmLocked(row.DisplayName)
            : UiText.Main.PresetDeleteConfirm(row.DisplayName);
        var confirm = MessageBox.Show(
            confirmMessage,
            UiText.Main.PresetDeleteTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        var restoreBaseline = row.Locked;
        if (!row.Locked)
        {
            var keep = MessageBox.Show(
                UiText.Main.PresetDeleteKeepSettings,
                UiText.Main.PresetDeleteKeepSettingsTitle,
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question);
            if (keep == MessageBoxResult.Cancel)
            {
                return;
            }

            restoreBaseline = keep == MessageBoxResult.No;
        }

        if (restoreBaseline)
        {
            if (_session is null || !PakWriteUi.TryProceed(_session))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_session.PakPath)
                || !PakBaselineService.HasBaseline(_session.PakPath))
            {
                MessageBox.Show(
                    UiText.Main.BaselineMissingShort,
                    UiText.Main.PresetDeleteTitle,
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            try
            {
                PakBaselineService.RestorePakFromBaseline(_session.PakPath);
                LoadWorkingPak(_session.PakPath);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, UiText.Main.PresetDeleteTitle, MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
        }

        try
        {
            TuningPresetLibrary.DeleteFromLibrary(row.ToInfo());
            MessageBox.Show(
                restoreBaseline
                    ? UiText.Main.PresetDeleteSuccessBaseline(row.DisplayName)
                    : UiText.Main.PresetDeleteSuccessKept(row.DisplayName),
                UiText.Main.PresetDeleteSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            RefreshPresetsList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.PresetDeleteTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ImportArchive(string archivePath, bool quietSuccess)
    {
        TuningPresetImportResult imported;
        try
        {
            imported = TuningPresetLibrary.Import(archivePath, overwriteUserOwned: false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var package = TuningPresetArchive.Read(archivePath);
            var confirm = MessageBox.Show(
                UiText.Main.PresetImportOverwriteConfirm(package.Manifest.Id),
                UiText.Main.PresetImportOverwriteTitle,
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes)
            {
                return;
            }

            imported = TuningPresetLibrary.Import(archivePath, overwriteUserOwned: true);
        }

        if (!quietSuccess)
        {
            MessageBox.Show(
                UiText.Main.PresetImportSuccess(imported.Info.Name),
                UiText.Main.PresetImportTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        RefreshPresetsList(selectId: imported.Info.Id);
    }

    private void LoadWorkingPak(string pakPath)
    {
        if (_session is null)
        {
            return;
        }

        var summary = InitialPakReader.ReadSummary(pakPath);
        _session.SetPak(pakPath, summary);
        TuningProfileService.RecordWorkingPakOpened(pakPath);
        RefreshWorkspaceUi();
        RefreshFromSession();
    }

    private void RefreshWorkspaceUi()
    {
        var workspace = WorkspaceConfigStore.TryGetActiveWorkspace();
        var isReady = workspace is not null
            && workspace.BaselineExists
            && File.Exists(workspace.WorkingPakPath);

        SetupPanel.Visibility = isReady ? Visibility.Collapsed : Visibility.Visible;
        ReadyPanel.Visibility = isReady ? Visibility.Visible : Visibility.Collapsed;

        if (!isReady || workspace is null)
        {
            HealthBanner.Visibility = Visibility.Collapsed;
            RealLifeModBanner.Visibility = Visibility.Collapsed;
            RefreshPresetActions();
            return;
        }

        var baseline = PakBaselineService.TryGetBaselineInfoForEdition(workspace.EditionId);
        BaselineReadyTextBlock.Text = baseline is null
            ? UiText.Main.BaselineReadyNote
            : UiText.Main.BaselineReadyStatus(
                workspace.DisplayName,
                Path.GetFileName(baseline.BaselinePath),
                baseline.LastWriteTimeUtc);

        WorkingPakTextBlock.Text = UiText.Main.WorkingPakStatus(
            workspace.DisplayName,
            workspace.WorkingPakPath);

        ApplyRealLifeModBanner(workspace.WorkingPakPath);
        ApplyHealthUi(WorkspaceHealthService.Evaluate(workspace.WorkingPakPath));
        RefreshPresetActions();
    }

    private void ApplyRealLifeModBanner(string workingPakPath)
    {
        if (!RealLifeModDetector.TryDetect(workingPakPath, out var version))
        {
            RealLifeModBanner.Visibility = Visibility.Collapsed;
            return;
        }

        RealLifeModBannerTitle.Text = UiText.Main.RealLifeModDetectedTitle;
        RealLifeModBannerMessage.Text = UiText.Main.RealLifeModDetectedMessage(version);
        RealLifeModBanner.Visibility = Visibility.Visible;
    }

    private void ApplyHealthUi(WorkspaceHealth health)
    {
        ProfileStatusTextBlock.Text = health.HasProfile
            ? UiText.Workspace.ProfileStatus(health.ProfileEntryCount)
            : UiText.Workspace.NoSavedProfile;

        RefreshBaselineButton.IsEnabled = health.CanRefreshBaseline;
        RefreshBaselineButton.ToolTip = UiText.Workspace.RefreshBaselineTooltip(health);
        ReapplyButton.IsEnabled = health.CanReapply && PakWriteUi.CanWrite(_session);
        RestoreFullBaselineButton.IsEnabled = PakWriteUi.CanWrite(_session);

        switch (health.Kind)
        {
            case WorkspaceHealthKind.GameUpdateDetected:
                ShowHealthBanner(
                    UiText.Workspace.GameUpdateTitle,
                    UiText.Workspace.GameUpdateMessage,
                    caution: true);
                break;
            case WorkspaceHealthKind.UnknownExternalChange:
                ShowHealthBanner(
                    UiText.Workspace.UnknownChangeTitle,
                    UiText.Workspace.UnknownChangeMessage,
                    caution: true);
                break;
            case WorkspaceHealthKind.ReadyToReapply:
                ShowHealthBanner(
                    UiText.Workspace.ReadyToReapplyTitle,
                    UiText.Workspace.ReadyToReapplyMessage,
                    caution: false);
                break;
            case WorkspaceHealthKind.InconsistentMarker:
                ShowHealthBanner(
                    UiText.Workspace.InconsistentMarkerTitle,
                    UiText.Workspace.InconsistentMarkerMessage,
                    caution: true);
                break;
            default:
                HealthBanner.Visibility = Visibility.Collapsed;
                break;
        }
    }

    private void ShowHealthBanner(string title, string message, bool caution)
    {
        HealthBannerTitle.Text = title;
        HealthBannerMessage.Text = message;
        HealthBanner.SetResourceReference(
            Border.BackgroundProperty,
            caution ? "SystemFillColorCautionBackgroundBrush" : "CardBackgroundFillColorSecondaryBrush");
        HealthBanner.SetResourceReference(
            Border.BorderBrushProperty,
            caution ? "SystemFillColorCautionBrush" : "AccentFillColorDefaultBrush");
        HealthBanner.Visibility = Visibility.Visible;
    }

    private void RefreshFromSession()
    {
        if (_session?.Summary is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            CategoriesListView.ItemsSource = null;
            RefreshPresetsList();
            return;
        }

        var summary = _session.Summary;
        CategoriesListView.ItemsSource = summary.TuningCategories
            .Select(category => new CategoryRow(
                PakPaths.FormatTuningCategoryName(category.Name),
                category.ItemCount,
                category.FileCount,
                category.SampleFiles.FirstOrDefault() ?? "-"))
            .ToList();

        RefreshPresetsList();
    }

    private void RefreshPresetsList(string? selectId = null)
    {
        var preferredId = selectId ?? _selectedPresetId;
        var editionId = WorkspaceConfigStore.Load().ActiveEditionId;
        var dirty = TuningPresetLibrary.GetDirtyState(editionId);
        var presets = TuningPresetLibrary.ListPresets(includeSynthetic: true)
            .Select(p => p.Source == TuningPresetSource.Synthetic
                ? TuningPresetLibrary.CreateSyntheticInfo(UiText.Main.PresetUnnamed)
                : p)
            .ToList();

        var rows = presets
            .Select(p => PresetRow.From(p, dirty))
            .ToList();
        PresetsListView.ItemsSource = rows;

        PresetRow? match = null;
        if (preferredId is not null)
        {
            match = rows.FirstOrDefault(r =>
                string.Equals(r.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        }

        PresetsListView.SelectedItem = match;
        _selectedPresetId = match?.Id;
        RefreshPresetActions();
        RefreshPresetDetail();
    }

    private void RefreshPresetActions()
    {
        var canWrite = PakWriteUi.CanWrite(_session);
        var hasWorkspace = !string.IsNullOrWhiteSpace(_session?.PakPath);
        var selected = PresetsListView.SelectedItem as PresetRow;
        var editionId = WorkspaceConfigStore.Load().ActiveEditionId;
        var dirty = TuningPresetLibrary.GetDirtyState(editionId);

        PresetImportButton.IsEnabled = true;
        PresetNewButton.IsEnabled = PakWriteUi.CanWrite(_session);
        PresetExportButton.IsEnabled = hasWorkspace
            && selected is not null
            && selected.Source != TuningPresetSource.Synthetic
            && !selected.Locked;
        PresetApplyButton.IsEnabled = canWrite
            && hasWorkspace
            && selected is not null
            && selected.Source != TuningPresetSource.Synthetic;
        PresetSaveAsNewButton.IsEnabled = hasWorkspace
            && TuningPresetLibrary.CanSaveAsNew(dirty, selected?.ToInfo());
        PresetDeleteButton.IsEnabled = selected is not null
            && selected.Source != TuningPresetSource.Synthetic;
    }

    private void RefreshPresetDetail()
    {
        if (PresetsListView.SelectedItem is not PresetRow selected)
        {
            CategoriesPanel.Visibility = Visibility.Visible;
            PresetDetailPanel.Visibility = Visibility.Collapsed;
            return;
        }

        CategoriesPanel.Visibility = Visibility.Collapsed;
        PresetDetailPanel.Visibility = Visibility.Visible;
        PresetDetailNameText.Text = selected.DisplayName;
        PresetDetailAuthorText.Text = UiText.Main.PresetDetailAuthorLabel(
            string.IsNullOrWhiteSpace(selected.Author) ? "—" : selected.Author);
        PresetDetailVersionText.Text = UiText.Main.PresetDetailVersionLabel(
            string.IsNullOrWhiteSpace(selected.Version) ? "—" : selected.Version);
        PresetDetailDescriptionText.Text = string.IsNullOrWhiteSpace(selected.Description)
            ? UiText.Main.PresetDetailNoDescription
            : selected.Description;
    }

    private async void HomeView_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= HomeView_Loaded;
        await RefreshUpdateBannerAsync();
        RefreshPresetsList();
    }

    private async Task RefreshUpdateBannerAsync()
    {
        try
        {
            var result = await AppUpdateService.CheckAsync();
            var skipped = WorkspaceConfigStore.GetSkippedAppVersion();
            var show = result.Status == AppUpdateStatus.UpdateAvailable
                && !string.IsNullOrWhiteSpace(result.LatestVersion)
                && !AppUpdateService.IsSameVersion(result.LatestVersion, skipped);

            _availableUpdate = show ? result : null;
            UpdateBanner.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                UpdateBannerMessage.Text = UiText.Settings.UpdateAvailableMessage(result.LatestVersion!);
            }
        }
        catch
        {
            UpdateBanner.Visibility = Visibility.Collapsed;
        }
    }

    private void DownloadUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        AppUpdateUi.StartDownload(Window.GetWindow(this), _availableUpdate);
    }

    private void SkipUpdateButton_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_availableUpdate?.LatestVersion))
        {
            WorkspaceConfigStore.SetSkippedAppVersion(_availableUpdate.LatestVersion);
        }

        UpdateBanner.Visibility = Visibility.Collapsed;
        _availableUpdate = null;
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "preset" : sanitized;
    }

    private sealed record CategoryRow(string Name, int ItemCount, int FileCount, string SampleFile);

    private sealed record PresetRow(
        string Id,
        string DisplayName,
        string Version,
        string Author,
        string LockGlyph,
        string Status,
        bool Locked,
        TuningPresetSource Source,
        string? ArchivePath,
        string? Description = null)
    {
        public static PresetRow From(TuningPresetInfo info, TuningPresetDirtyState dirty)
        {
            string status;
            if (info.Source == TuningPresetSource.Synthetic)
            {
                // Untitled row stands in for the workspace when no named preset is active.
                status = dirty.HasActivePreset
                    ? ""
                    : (dirty.IsDirty
                        ? UiText.Main.PresetStatusChanged
                        : UiText.Main.PresetStatusActive);
            }
            else
            {
                var isActive = dirty.HasActivePreset
                    && string.Equals(dirty.ActivePresetId, info.Id, StringComparison.OrdinalIgnoreCase);
                status = isActive
                    ? (dirty.IsDirty ? UiText.Main.PresetStatusChanged : UiText.Main.PresetStatusActive)
                    : "";
            }

            return new PresetRow(
                info.Id,
                info.Source == TuningPresetSource.Synthetic ? UiText.Main.PresetUnnamed : info.Name,
                info.Version,
                info.Author ?? "",
                info.Locked ? UiText.Main.PresetLockedGlyph : "",
                status,
                info.Locked,
                info.Source,
                info.ArchivePath,
                info.Description);
        }

        public TuningPresetInfo ToInfo() =>
            new(Id, DisplayName, Version, Locked, Source, ArchivePath, Author, Description);
    }
}
