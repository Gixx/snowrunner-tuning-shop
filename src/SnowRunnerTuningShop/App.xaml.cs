using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace SnowRunnerTuningShop;

public partial class App : Application
{
    public static string? PendingTsaPath { get; set; }

    protected override void OnStartup(StartupEventArgs e)
    {
        GlobalExceptionHandler.Register();
        LanguageService.ApplySavedLanguage();
        ThemeService.ApplySavedTheme();

        PendingTsaPath = e.Args
            .Select(a => a.Trim('"'))
            .FirstOrDefault(a =>
                a.EndsWith(".tsa", StringComparison.OrdinalIgnoreCase) && File.Exists(a));

        base.OnStartup(e);
    }

    private void TuningDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            DataGridHeaderMinWidths.Apply(dataGrid);
        }
    }
}
