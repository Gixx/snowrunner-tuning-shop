using System.IO;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Constants;
using SnowRunnerTuningShop.Core.Pak;
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
        _session.GameRunningChanged += (_, _) => RefreshWorkspaceUi();

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
        OverviewTitleText.Text = UiText.Main.OverviewTitle;
        CategoriesTitleText.Text = UiText.Main.CategoriesTitle;
        CategoryColumnHeader.Text = UiText.Main.CategoryColumn;
        ItemsColumnHeader.Text = UiText.Main.ItemsColumn;
        FilesColumnHeader.Text = UiText.Main.FilesColumn;
        SampleColumnHeader.Text = UiText.Main.SampleFileColumn;
        OverviewTextBlock.Text = UiText.Main.OverviewPlaceholder;
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

    private async void RestoreFullBaselineButton_Click(object? sender, RoutedEventArgs e)
    {
        if (_session is null || OwnerWindow is not { } owner)
        {
            return;
        }

        await WorkspaceCommands.TryRestoreFullBaseline(owner, _session);
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
            OverviewTextBlock.Text = UiText.Main.OverviewPlaceholder;
            CategoriesList.ItemsSource = null;
            return;
        }

        var summary = _session.Summary;
        OverviewTextBlock.Text = UiText.Main.OverviewDetails(
            summary.FilePath,
            FormatBytes(summary.FileSizeBytes),
            summary.TotalEntries,
            summary.XmlEntries,
            summary.DlcPackages,
            FormatBytes(summary.UncompressedBytes),
            summary.TopLevelFolders);

        CategoriesList.ItemsSource = summary.TuningCategories
            .Select(category => new CategoryRow(
                PakPaths.FormatTuningCategoryName(category.Name),
                category.ItemCount,
                category.FileCount,
                category.SampleFiles.FirstOrDefault() ?? "-"))
            .ToList();
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double size = bytes;
        var unitIndex = 0;
        while (size >= 1024 && unitIndex < units.Length - 1)
        {
            size /= 1024;
            unitIndex++;
        }

        return $"{size:0.##} {units[unitIndex]}";
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

    public sealed record CategoryRow(string Name, int ItemCount, int FileCount, string SampleFile);
}
