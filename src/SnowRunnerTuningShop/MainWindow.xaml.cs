using System.Windows;
using System.Windows.Controls;

namespace SnowRunnerTuningShop;

public partial class MainWindow : Window
{
    private readonly AppSession _session = new();

    public MainWindow()
    {
        InitializeComponent();

        HomeView.AttachSession(_session);
        PartsView.AttachSession(_session);

        Loaded += (_, _) =>
        {
            NavHome.IsChecked = true;
            ShowPage(HomeView);
        };
    }

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { IsChecked: true } radio)
        {
            return;
        }

        if (ReferenceEquals(radio, NavHome))
        {
            ShowPage(HomeView);
        }
        else if (ReferenceEquals(radio, NavParts))
        {
            ShowPage(PartsView);
        }
        else if (ReferenceEquals(radio, NavVehicles))
        {
            ShowPage(VehiclesView);
        }
        else if (ReferenceEquals(radio, NavSettings))
        {
            ShowPage(SettingsView);
        }
    }

    private void ShowPage(UIElement page)
    {
        HomeView.Visibility = Visibility.Collapsed;
        PartsView.Visibility = Visibility.Collapsed;
        VehiclesView.Visibility = Visibility.Collapsed;
        SettingsView.Visibility = Visibility.Collapsed;
        page.Visibility = Visibility.Visible;
    }
}
