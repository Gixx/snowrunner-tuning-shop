using Avalonia.Controls;
using SnowRunnerTuningShop.Core;

namespace SnowRunnerTuningShop.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        VersionText.Text = $"Version {AppInfo.Version} · Core shared with the Windows WPF app";
    }
}
