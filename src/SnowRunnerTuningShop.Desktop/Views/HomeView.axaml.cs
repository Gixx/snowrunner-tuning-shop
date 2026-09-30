using System.IO;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Core.Presets;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Core.Updates;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class HomeView : UserControl
{
    private AppSession? _session;
    private bool _autoLoadAttempted;
    private AppUpdateCheckResult? _availableUpdate;
    private (string Message, string Title)? _pendingLoadError;
    private string? _selectedPresetId;

    public HomeView()
    {
        InitializeComponent();
        ApplyStaticText();
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

    public void RefreshPresetsFromOutside() => RefreshPresetsList();

    private void ApplyStaticText()
    {
        BaselineTitleText.Text = UiText.Main.BaselineTitle;
        BaselineWarningText.Text = UiText.Main.BaselineWarning;
        SetBaselineButton.Content = UiText.Main.SetBaselineFromOriginal;
        UpdateBannerTitle.Text = UiText.Settings.UpdateAvailableTitle;
        DownloadUpdateButton.Content = UiText.Settings.DownloadUpdate;
        SkipUpdateButton.Content = UiText.Settings.SkipThisVersion;
        BaselineReadyTitleText.Text = UiText.Main.BaselineReadyTitle;
        ChangeLocationButton.Content = UiText.Main.ChangeLocation;
        RefreshBaselineButton.Content = UiText.Workspace.RefreshBaseline;
        ReapplyButton.Content = UiText.Workspace.ReapplySavedChanges;
        RestoreFullBaselineButton.Content = UiText.Main.RestoreFullBaseline;
        PresetsTitleText.Text = UiText.Main.PresetsTitle;
        PresetNewButton.Content = UiText.Main.PresetNew;
        PresetExportButton.Content = UiText.Main.PresetExport;
        PresetImportButton.Content = UiText.Main.PresetImport;
        PresetApplyButton.Content = UiText.Main.PresetApply;
        PresetSaveAsNewButton.Content = UiText.Main.PresetSaveAsNew;
        PresetDeleteButton.Content = UiText.Main.PresetDelete;
        PresetNameColumnHeader.Text = UiText.Main.PresetNameColumn;
        PresetVersionColumnHeader.Text = UiText.Main.PresetVersionColumn;
        PresetAuthorColumnHeader.Text = UiText.Main.PresetAuthorColumn;
        PresetLockColumnHeader.Text = UiText.Main.PresetLockColumn;
        PresetStatusColumnHeader.Text = UiText.Main.PresetStatusColumn;
        PresetDetailTitleText.Text = UiText.Main.PresetDetailTitle;
        CategoriesTitleText.Text = UiText.Main.CategoriesTitle;
        CategoryColumnHeader.Text = UiText.Main.CategoryColumn;
        ItemsColumnHeader.Text = UiText.Main.ItemsColumn;
        FilesColumnHeader.Text = UiText.Main.FilesColumn;
        SampleColumnHeader.Text = UiText.Main.SampleFileColumn;
    }

    private Window? OwnerWindow => TopLevel.GetTopLevel(this) as Window;

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
            _pendingLoadError = (ex.Message, UiText.Main.LoadErrorTitle);
        }
    }

    private async void SetBaselineButton_Click(object? sender, RoutedEventArgs e) =>
        await ActivateFromBrowse(changeLocation: false);

    private async void ChangeLocationButton_Click(object? sender, RoutedEventArgs e) =>
        await ActivateFromBrowse(changeLocation: true);

    private async Task ActivateFromBrowse(bool changeLocation)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        var path = await PickPakPath(
            changeLocation
                ? UiText.Main.ChangeLocationDialogTitle
                : UiText.Main.SelectOriginalPakDialogTitle);
        if (path is null)
        {
            return;
        }

        try
        {
            if (changeLocation)
            {
                var fullPath = Path.GetFullPath(path);
                var edition = GameEditionDetector.Detect(fullPath);
                if (!PakBaselineService.HasBaselineForEdition(edition.Id)
                    && TuningProfileMarker.HasMarker(fullPath)
                    && !await AppDialogs.Confirm(
                        owner,
                        UiText.Main.ChangeLocationMarkedPakConfirm,
                        UiText.Main.ChangeLocationMarkedPakTitle))
                {
                    return;
                }
            }

            var result = changeLocation
                ? PakBaselineService.ChangeLocation(path)
                : PakBaselineService.SetBaselineFromOriginal(path);

            LoadWorkingPak(result.WorkingPakPath);
            _session.NotifyBaselineChanged();
            RefreshWorkspaceUi();

            await AppDialogs.ShowInfo(
                owner,
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
                changeLocation ? UiText.Main.LocationChangedTitle : UiText.Main.BaselineUpdatedTitle);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.BaselineErrorTitle);
        }
    }

    private async Task<string?> PickPakPath(string title)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var options = new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("SnowRunner pak") { Patterns = ["*.pak"] },
                FilePickerFileTypes.All,
            ],
        };

        if (AppPaths.TryFindSuggestedPakDirectory() is { } start
            && await top.StorageProvider.TryGetFolderFromPathAsync(start) is { } folder)
        {
            options.SuggestedStartLocation = folder;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(options);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async Task<string?> PickTsaOpenPath()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = UiText.Main.PresetImportTitle,
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Tuning Shop archive") { Patterns = ["*.tsa"] },
                FilePickerFileTypes.All,
            ],
        });
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private async Task<string?> PickTsaSavePath(string suggestedName)
    {
        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return null;
        }

        var file = await top.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = UiText.Main.PresetExportTitle,
            SuggestedFileName = suggestedName,
            DefaultExtension = "tsa",
            FileTypeChoices =
            [
                new FilePickerFileType("Tuning Shop archive") { Patterns = ["*.tsa"] },
            ],
        });
        return file?.TryGetLocalPath();
    }

    private async void RestoreFullBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        if (await WorkspaceCommands.TryRestoreFullBaseline(owner, _session))
        {
            _selectedPresetId = null;
            RefreshPresetsList();
        }
    }

    private async void RefreshBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryRefreshBaselineFromGame(owner, _session);
    }

    private async void ReapplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryReapplySavedChanges(owner, _session);
        RefreshPresetsList();
    }

    private async void PresetNewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        if (await WorkspaceCommands.TryStartNewPreset(owner, _session))
        {
            _selectedPresetId = null;
            RefreshPresetsList();
        }
    }

    private void PresetsList_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(PresetsList).Properties.IsLeftButtonPressed)
        {
            return;
        }

        var item = (e.Source as Control)?.FindAncestorOfType<ListBoxItem>();
        if (item?.IsSelected == true)
        {
            PresetsList.SelectedItem = null;
            e.Handled = true;
        }
    }

    private void PresetsList_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        _selectedPresetId = PresetsList.SelectedItem is PresetRow row ? row.Id : null;
        RefreshPresetActions();
        RefreshPresetDetail();
    }

    private async void PresetImportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        var path = await PickTsaOpenPath();
        if (path is null)
        {
            return;
        }

        try
        {
            await ImportArchiveAsync(owner, path, quietSuccess: false);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetImportTitle);
        }
    }

    private async void PresetExportButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (PresetsList.SelectedItem is not PresetRow selectedRow
            || selectedRow.Source == TuningPresetSource.Synthetic)
        {
            await AppDialogs.ShowInfo(owner, UiText.Main.PresetCannotExportUnnamed, UiText.Main.PresetExportTitle);
            return;
        }

        if (selectedRow.Locked)
        {
            await AppDialogs.ShowInfo(owner, UiText.Main.PresetCannotExportLocked, UiText.Main.PresetExportTitle);
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
            await AppDialogs.ShowInfo(owner, UiText.Main.PresetNothingToExport, UiText.Main.PresetExportTitle);
            return;
        }

        var path = await PickTsaSavePath(SanitizeFileName(selectedRow.DisplayName) + TuningPresetArchive.Extension);
        if (path is null)
        {
            return;
        }

        try
        {
            if (canExportPackage)
            {
                TuningPresetLibrary.ExportPackage(selectedRow.ToInfo(), path);
            }
            else
            {
                var name = Path.GetFileNameWithoutExtension(path);
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = selectedRow.DisplayName;
                }

                TuningPresetLibrary.ExportCurrentProfile(
                    path,
                    name,
                    selectedRow.Version is "-" or null or "" ? "1.0.0" : selectedRow.Version,
                    profile!.Entries,
                    locked: false);
            }

            await AppDialogs.ShowInfo(
                owner,
                UiText.Main.PresetExportSuccess(path),
                UiText.Main.PresetExportSuccessTitle);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetExportTitle);
        }
    }

    private async void PresetApplyButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        if (!await PakWriteUi.TryProceed(owner, _session))
        {
            return;
        }

        if (PresetsList.SelectedItem is not PresetRow row || row.Source == TuningPresetSource.Synthetic)
        {
            await AppDialogs.ShowInfo(owner, UiText.Main.PresetCannotApplyUnnamed, UiText.Main.PresetApplyConfirmTitle);
            return;
        }

        if (string.IsNullOrWhiteSpace(_session.PakPath))
        {
            await AppDialogs.ShowError(owner, UiText.Main.BaselineMissingShort, UiText.Main.PresetApplyConfirmTitle);
            return;
        }

        if (!await AppDialogs.Confirm(
                owner,
                UiText.Main.PresetApplyConfirm(row.DisplayName),
                UiText.Main.PresetApplyConfirmTitle))
        {
            return;
        }

        try
        {
            var info = row.ToInfo();
            var dialog = new ReapplyProgressWindow(
                _session.PakPath,
                progress => TuningPresetApplyService.Apply(_session.PakPath!, info, progress));
            await dialog.ShowDialog(owner);
            if (!dialog.Succeeded || dialog.Result is not { } result)
            {
                if (!string.IsNullOrWhiteSpace(dialog.ErrorMessage))
                {
                    await AppDialogs.ShowError(owner, dialog.ErrorMessage, UiText.Main.PresetApplyConfirmTitle);
                }

                return;
            }

            LoadWorkingPak(_session.PakPath);
            await AppDialogs.ShowInfo(
                owner,
                UiText.Main.PresetApplySuccess(row.DisplayName, result.AppliedCount),
                UiText.Main.PresetApplySuccessTitle);
            RefreshPresetsList(selectId: row.Id);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetApplyConfirmTitle);
        }
    }

    private async void PresetSaveAsNewButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        var editionId = WorkspaceConfigStore.Load().ActiveEditionId;
        var profile = string.IsNullOrWhiteSpace(editionId)
            ? null
            : TuningProfileService.TryLoadProfile(editionId);
        if (profile?.Entries.Count is not > 0)
        {
            await AppDialogs.ShowInfo(owner, UiText.Main.PresetNothingToExport, UiText.Main.PresetSaveAsTitle);
            return;
        }

        var selected = PresetsList.SelectedItem as PresetRow;
        if (selected?.Locked == true)
        {
            return;
        }

        var initialName = selected is null || selected.Source == TuningPresetSource.Synthetic
            ? ""
            : selected.DisplayName + " copy";
        var dialog = new SavePresetAsWindow(initialName);
        await dialog.ShowDialog(owner);
        if (!dialog.Confirmed)
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
            await AppDialogs.ShowInfo(
                owner,
                UiText.Main.PresetSaveAsSuccess(saved.Name),
                UiText.Main.PresetSaveAsTitle);
            RefreshPresetsList(selectId: saved.Id);
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetSaveAsTitle);
        }
    }

    private async void PresetDeleteButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        if (PresetsList.SelectedItem is not PresetRow row
            || row.Source == TuningPresetSource.Synthetic)
        {
            return;
        }

        var confirmMessage = row.Locked
            ? UiText.Main.PresetDeleteConfirmLocked(row.DisplayName)
            : UiText.Main.PresetDeleteConfirm(row.DisplayName);
        if (!await AppDialogs.Confirm(owner, confirmMessage, UiText.Main.PresetDeleteTitle))
        {
            return;
        }

        var restoreBaseline = row.Locked;
        if (!row.Locked)
        {
            var keepChoice = await AppDialogs.ConfirmKeepOrRestoreBaseline(
                owner,
                UiText.Main.PresetDeleteKeepSettings,
                UiText.Main.PresetDeleteKeepSettingsTitle);
            if (keepChoice == PresetDeleteSettingsChoice.Cancel)
            {
                return;
            }

            restoreBaseline = keepChoice == PresetDeleteSettingsChoice.RestoreBaseline;
        }

        if (restoreBaseline)
        {
            if (_session is null || !await PakWriteUi.TryProceed(owner, _session))
            {
                return;
            }

            if (string.IsNullOrWhiteSpace(_session.PakPath)
                || !PakBaselineService.HasBaseline(_session.PakPath))
            {
                await AppDialogs.ShowError(owner, UiText.Main.BaselineMissingShort, UiText.Main.PresetDeleteTitle);
                return;
            }

            try
            {
                PakBaselineService.RestorePakFromBaseline(_session.PakPath);
                LoadWorkingPak(_session.PakPath);
            }
            catch (Exception ex)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetDeleteTitle);
                return;
            }
        }

        try
        {
            TuningPresetLibrary.DeleteFromLibrary(row.ToInfo());
            await AppDialogs.ShowInfo(
                owner,
                restoreBaseline
                    ? UiText.Main.PresetDeleteSuccessBaseline(row.DisplayName)
                    : UiText.Main.PresetDeleteSuccessKept(row.DisplayName),
                UiText.Main.PresetDeleteSuccessTitle);
            RefreshPresetsList();
        }
        catch (Exception ex)
        {
            await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetDeleteTitle);
        }
    }

    private async Task ImportArchiveAsync(Window owner, string archivePath, bool quietSuccess)
    {
        TuningPresetImportResult imported;
        try
        {
            imported = TuningPresetLibrary.Import(archivePath, overwriteUserOwned: false);
        }
        catch (InvalidOperationException ex) when (ex.Message.Contains("already exists", StringComparison.OrdinalIgnoreCase))
        {
            var package = TuningPresetArchive.Read(archivePath);
            if (!await AppDialogs.Confirm(
                    owner,
                    UiText.Main.PresetImportOverwriteConfirm(package.Manifest.Id),
                    UiText.Main.PresetImportOverwriteTitle))
            {
                return;
            }

            imported = TuningPresetLibrary.Import(archivePath, overwriteUserOwned: true);
        }

        if (!quietSuccess)
        {
            await AppDialogs.ShowInfo(
                owner,
                UiText.Main.PresetImportSuccess(imported.Info.Name),
                UiText.Main.PresetImportTitle);
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

        SetupPanel.IsVisible = !isReady;
        ReadyPanel.IsVisible = isReady;

        if (!isReady || workspace is null)
        {
            HealthBanner.IsVisible = false;
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

        ApplyHealthUi(WorkspaceHealthService.Evaluate(workspace.WorkingPakPath));
        RefreshPresetActions();
    }

    private void ApplyHealthUi(WorkspaceHealth health)
    {
        ProfileStatusTextBlock.Text = health.HasProfile
            ? UiText.Workspace.ProfileStatus(health.ProfileEntryCount)
            : UiText.Workspace.NoSavedProfile;

        RefreshBaselineButton.IsEnabled = health.CanRefreshBaseline;
        ToolTip.SetTip(RefreshBaselineButton, UiText.Workspace.RefreshBaselineTooltip(health));
        ReapplyButton.IsEnabled = health.CanReapply && PakWriteUi.CanWrite(_session);
        RestoreFullBaselineButton.IsEnabled = PakWriteUi.CanWrite(_session);

        switch (health.Kind)
        {
            case WorkspaceHealthKind.GameUpdateDetected:
                ShowHealthBanner(UiText.Workspace.GameUpdateTitle, UiText.Workspace.GameUpdateMessage, caution: true);
                break;
            case WorkspaceHealthKind.UnknownExternalChange:
                ShowHealthBanner(UiText.Workspace.UnknownChangeTitle, UiText.Workspace.UnknownChangeMessage, caution: true);
                break;
            case WorkspaceHealthKind.ReadyToReapply:
                ShowHealthBanner(UiText.Workspace.ReadyToReapplyTitle, UiText.Workspace.ReadyToReapplyMessage, caution: false);
                break;
            case WorkspaceHealthKind.InconsistentMarker:
                ShowHealthBanner(
                    UiText.Workspace.InconsistentMarkerTitle,
                    UiText.Workspace.InconsistentMarkerMessage,
                    caution: true);
                break;
            default:
                HealthBanner.IsVisible = false;
                break;
        }
    }

    private void ShowHealthBanner(string title, string message, bool caution)
    {
        HealthBannerTitle.Text = title;
        HealthBannerMessage.Text = message;
        var backgroundKey = caution ? "AppCautionBrush" : "AppCardBrush";
        var strokeKey = caution ? "AppCautionStrokeBrush" : "AppAccentStrokeBrush";
        if (this.FindResource(backgroundKey) is IBrush background)
        {
            HealthBanner.Background = background;
        }

        if (this.FindResource(strokeKey) is IBrush stroke)
        {
            HealthBanner.BorderBrush = stroke;
        }

        HealthBanner.IsVisible = true;
    }

    private void RefreshFromSession()
    {
        if (_session?.Summary is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            CategoriesList.ItemsSource = null;
            RefreshPresetsList();
            return;
        }

        var summary = _session.Summary;
        CategoriesList.ItemsSource = summary.TuningCategories
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

        var rows = presets.Select(p => PresetRow.From(p, dirty)).ToList();
        PresetsList.ItemsSource = rows;

        PresetRow? match = null;
        if (preferredId is not null)
        {
            match = rows.FirstOrDefault(r =>
                string.Equals(r.Id, preferredId, StringComparison.OrdinalIgnoreCase));
        }

        PresetsList.SelectedItem = match;
        _selectedPresetId = match?.Id;
        RefreshPresetActions();
        RefreshPresetDetail();
    }

    private void RefreshPresetActions()
    {
        var canWrite = PakWriteUi.CanWrite(_session);
        var hasWorkspace = !string.IsNullOrWhiteSpace(_session?.PakPath);
        var selected = PresetsList.SelectedItem as PresetRow;
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
        if (PresetsList.SelectedItem is not PresetRow selected)
        {
            CategoriesPanel.IsVisible = true;
            PresetDetailPanel.IsVisible = false;
            return;
        }

        CategoriesPanel.IsVisible = false;
        PresetDetailPanel.IsVisible = true;
        PresetDetailNameText.Text = selected.DisplayName;
        PresetDetailAuthorText.Text = UiText.Main.PresetDetailAuthorLabel(
            string.IsNullOrWhiteSpace(selected.Author) ? "—" : selected.Author);
        PresetDetailVersionText.Text = UiText.Main.PresetDetailVersionLabel(
            string.IsNullOrWhiteSpace(selected.Version) ? "—" : selected.Version);
        PresetDetailDescriptionText.Text = string.IsNullOrWhiteSpace(selected.Description)
            ? UiText.Main.PresetDetailNoDescription
            : selected.Description;
    }

    private async void HomeView_Loaded(object? sender, RoutedEventArgs e)
    {
        Loaded -= HomeView_Loaded;
        if (_pendingLoadError is { } pending)
        {
            _pendingLoadError = null;
            await ShowError(pending.Message, pending.Title);
        }

        await RefreshUpdateBannerAsync();
        RefreshPresetsList();

        if (!string.IsNullOrWhiteSpace(App.PendingTsaPath) && OwnerWindow is { } owner)
        {
            var path = App.PendingTsaPath;
            App.PendingTsaPath = null;
            try
            {
                await ImportArchiveAsync(owner, path!, quietSuccess: false);
            }
            catch (Exception ex)
            {
                await AppDialogs.ShowError(owner, ex.Message, UiText.Main.PresetImportTitle);
            }
        }
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
            UpdateBanner.IsVisible = show;
            if (show)
            {
                UpdateBannerMessage.Text = UiText.Settings.UpdateAvailableMessage(result.LatestVersion!);
            }
        }
        catch
        {
            UpdateBanner.IsVisible = false;
        }
    }

    private async void DownloadUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (OwnerWindow is not { } owner)
        {
            return;
        }

        var url = _availableUpdate?.ReleasePageUrl ?? AppInfo.LatestReleasePageUrl;
        await AppDialogs.OpenUrl(owner, url);
    }

    private void SkipUpdateButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_availableUpdate?.LatestVersion))
        {
            WorkspaceConfigStore.SetSkippedAppVersion(_availableUpdate.LatestVersion);
        }

        UpdateBanner.IsVisible = false;
        _availableUpdate = null;
    }

    private async Task ShowError(string message, string title)
    {
        if (OwnerWindow is { } owner)
        {
            await AppDialogs.ShowError(owner, message, title);
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized) ? "preset" : sanitized;
    }

    public sealed record CategoryRow(string Name, int ItemCount, int FileCount, string SampleFile);

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
