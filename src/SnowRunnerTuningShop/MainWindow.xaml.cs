using System.Windows;
using Microsoft.Win32;
using SnowRunnerTuningShop.Core.Pak;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        WinchTuningView.StatusChanged += (_, message) => SetStatus(message);

        var examplePak = AppPaths.TryFindExamplePak();
        if (examplePak is not null)
        {
            PakPathTextBox.Text = examplePak;
            SetStatus(UiText.Main.ExamplePakDetected);
        }
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
            SetStatus(UiText.Main.FileSelectedStatus);
        }
    }

    private void LoadButton_Click(object sender, RoutedEventArgs e)
    {
        var pakPath = PakPathTextBox.Text.Trim();

        try
        {
            SetStatus(UiText.Main.LoadingPakStatus);
            var summary = InitialPakReader.ReadSummary(pakPath);

            OverviewTextBlock.Text = UiText.Main.OverviewDetails(
                summary.FilePath,
                FormatBytes(summary.FileSizeBytes),
                summary.TotalEntries,
                summary.XmlEntries,
                summary.DlcPackages,
                FormatBytes(summary.UncompressedBytes),
                summary.TopLevelFolders);

            CategoriesListView.ItemsSource = summary.TuningCategories
                .Select(category => new CategoryRowViewModel(
                    category.Name,
                    category.FileCount,
                    category.SampleFiles.FirstOrDefault() ?? "-"))
                .ToList();

            WinchTuningView.LoadFromPak(pakPath);
            SetStatus(UiText.Main.LoadSuccessStatus(summary.TotalEntries));
        }
        catch (Exception ex)
        {
            OverviewTextBlock.Text = UiText.Main.LoadFailedOverview;
            CategoriesListView.ItemsSource = null;
            WinchTuningView.Clear();
            SetStatus(UiText.Main.ErrorStatus(ex.Message));
            MessageBox.Show(
                ex.Message,
                UiText.Main.LoadErrorTitle,
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SetStatus(string message)
    {
        StatusTextBlock.Text = message;
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

    private sealed record CategoryRowViewModel(string Name, int FileCount, string SampleFile);
}
