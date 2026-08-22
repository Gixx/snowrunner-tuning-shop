using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SnowRunnerTuningShop.Core.Config;

namespace SnowRunnerTuningShop;

public partial class MainWindow : Window
{
    private readonly AppSession _session = new();
    private bool _navOpen;
    private bool _sidebarPinned;
    private bool _suppressPinHandler;

    public MainWindow()
    {
        InitializeComponent();

        HomeView.AttachSession(_session);
        PartsView.AttachSession(_session);
        VehiclesView.AttachSession(_session);

        _sidebarPinned = WorkspaceConfigStore.GetSidebarPinned();
        _suppressPinHandler = true;
        PinMenuCheckBox.IsChecked = _sidebarPinned;
        _suppressPinHandler = false;

        if (_sidebarPinned)
        {
            _navOpen = true;
        }

        ApplyNavLayout();

        Loaded += (_, _) =>
        {
            NavHome.IsChecked = true;
            ShowPage(HomeView);
        };
    }

    private void MenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (_sidebarPinned)
        {
            _sidebarPinned = false;
            _navOpen = false;
            _suppressPinHandler = true;
            PinMenuCheckBox.IsChecked = false;
            _suppressPinHandler = false;
            WorkspaceConfigStore.SetSidebarPinned(false);
        }
        else
        {
            _navOpen = !_navOpen;
        }

        ApplyNavLayout();
    }

    private void NavScrim_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_sidebarPinned)
        {
            return;
        }

        _navOpen = false;
        ApplyNavLayout();
    }

    private void PinMenuCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        if (_suppressPinHandler)
        {
            return;
        }

        _sidebarPinned = PinMenuCheckBox.IsChecked == true;
        if (_sidebarPinned)
        {
            _navOpen = true;
        }

        WorkspaceConfigStore.SetSidebarPinned(_sidebarPinned);
        ApplyNavLayout();
    }

    private void ApplyNavLayout()
    {
        var showNav = _sidebarPinned || _navOpen;
        NavPane.Visibility = showNav ? Visibility.Visible : Visibility.Collapsed;
        NavScrim.Visibility = !_sidebarPinned && _navOpen ? Visibility.Visible : Visibility.Collapsed;
        MainContent.Margin = _sidebarPinned ? new Thickness(220, 0, 0, 0) : new Thickness(0);
        Panel.SetZIndex(NavScrim, 10);
        Panel.SetZIndex(NavPane, 20);
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

        if (!_sidebarPinned)
        {
            _navOpen = false;
            ApplyNavLayout();
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
