using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class HomeView : UserControl
{
    private AppSession? _session;

    public HomeView()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? StatusChanged;

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => RefreshFromSession();

        var examplePak = AppPaths.TryFindExamplePak();
        if (examplePak is not null && string.IsNullOrWhiteSpace(PakPathTextBox.Text))
        {
            PakPathTextBox.Text = examplePak;
            ReportStatus(UiText.Main.ExamplePakDetected);
        }

        RefreshFromSession();
    }

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = UiText.Main.BrowseDialogTitle,
            Filter = UiText.Main.BrowseDialogFilter,
            CheckFileExists = true,
        };

        if (dialog.ShowDialog() == true)
        {
            PakPathTextBox.Text = dialog.FileName;
            ReportStatus(UiText.Main.FileSelectedStatus);
        }
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session is null)
        {
            return;
        }

        var pakPath = PakPathTextBox.Text.Trim();
        try
        {
            ReportStatus(UiText.Main.LoadingPakStatus);
            var summary = InitialPakReader.ReadSummary(pakPath);
            _session.SetPak(pakPath, summary);
            ReportStatus(UiText.Main.LoadSuccessStatus(summary.TotalEntries));
        }
        catch (Exception ex)
        {
            _session.ClearPak();
            OverviewTextBlock.Text = UiText.Main.LoadFailedOverview;
            CategoriesListView.ItemsSource = null;
            ReportStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(ex.Message, UiText.Main.LoadErrorTitle, MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void RefreshFromSession()
    {
        if (_session?.Summary is null || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            if (string.IsNullOrWhiteSpace(PakPathTextBox.Text))
            {
                PakPathTextBox.Text = UiText.Main.NoPakSelected;
            }

            OverviewTextBlock.Text = UiText.Main.OverviewPlaceholder;
            CategoriesListView.ItemsSource = null;
            return;
        }

        PakPathTextBox.Text = _session.PakPath;
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

    private void ReportStatus(string message) => StatusChanged?.Invoke(this, message);

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
