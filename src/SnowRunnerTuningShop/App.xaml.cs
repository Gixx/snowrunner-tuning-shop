using System.Windows;
using System.Windows.Controls;

namespace SnowRunnerTuningShop;

public partial class App : Application
{
    private void TuningDataGrid_Loaded(object sender, RoutedEventArgs e)
    {
        if (sender is DataGrid dataGrid)
        {
            DataGridHeaderMinWidths.Apply(dataGrid);
        }
    }
}

