using System.Windows;
using System.Windows.Controls;

namespace SnowRunnerTuningShop;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        GlobalExceptionHandler.Register();
        ThemeService.ApplySavedTheme();
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

