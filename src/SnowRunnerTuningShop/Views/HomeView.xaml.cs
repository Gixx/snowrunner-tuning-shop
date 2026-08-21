using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnowRunnerTuningShop.Core.Backup;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class HomeView : UserControl
{
    private AppSession? _session;
    private bool _autoLoadAttempted;

    public HomeView()
    {
        InitializeComponent();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => RefreshFromSession();
        _session.BaselineChanged += (_, _) => RefreshWorkspaceUi();

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
        if (_session is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        if (!PakBaselineService.HasBaseline(_session.PakPath))
        {
            MessageBox.Show(
                UiText.Main.BaselineMissingShort,
                UiText.Main.BaselineTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var confirm = MessageBox.Show(
            UiText.Main.RestoreFullBaselineConfirmMessage,
            UiText.Main.RestoreFullBaselineConfirmTitle,
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            PakBaselineService.RestorePakFromBaseline(_session.PakPath);
            LoadWorkingPak(_session.PakPath);
            MessageBox.Show(
                UiText.Main.RestoreFullBaselineMessage,
                UiText.Main.RestoreFullBaselineSuccessTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, UiText.Main.BaselineErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void LoadWorkingPak(string pakPath)
    {
        if (_session is null)
        {
            return;
        }

        var summary = InitialPakReader.ReadSummary(pakPath);
        _session.SetPak(pakPath, summary);
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
    }

    private void RefreshFromSession()
    {
        if (_session?.Summary is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            OverviewTextBlock.Text = UiText.Main.OverviewPlaceholder;
            CategoriesListView.ItemsSource = null;
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

        CategoriesListView.ItemsSource = summary.TuningCategories
            .Select(category => new CategoryRow(
                category.Name,
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

    private sealed record CategoryRow(string Name, int FileCount, string SampleFile);
}
