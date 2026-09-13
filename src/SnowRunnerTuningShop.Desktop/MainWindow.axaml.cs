using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Game;
using SnowRunnerTuningShop.Desktop.Views;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

public partial class MainWindow : Window
{
    private readonly AppSession _session = new();
    private readonly GameRunningMonitor _gameRunningMonitor;
    private bool _navOpen;
    private bool _sidebarPinned;
    private bool _suppressPinHandler;

    public MainWindow()
    {
        InitializeComponent();

        ToolTip.SetTip(MenuButton, UiText.Nav.OpenMenu);
        NavHome.Content = UiText.Nav.Home;
        NavGeneral.Content = UiText.Nav.General;
        NavParts.Content = UiText.Nav.Parts;
        NavVehicles.Content = UiText.Nav.Vehicles;
        NavTrailers.Content = UiText.Nav.Trailers;
        NavPhotoMode.Content = UiText.Nav.PhotoMode;
        NavSettings.Content = UiText.Nav.Settings;
        ReportBugButton.Content = UiText.Nav.ReportBug;
        PinMenuCheckBox.Content = UiText.Nav.PinMenu;
        VersionText.Text = UiText.Nav.VersionLabel;
        SubtitleText.Text = UiText.Main.Subtitle;
        GameRunningBannerText.Text = UiText.Main.GameRunningBanner;
        PlaceholderVehicles.Set(UiText.Nav.Vehicles);
        PlaceholderTrailers.Set(UiText.Nav.Trailers);
        PlaceholderPhotoMode.Set(UiText.Nav.PhotoMode);

        HomeView.AttachSession(_session);
        GeneralView.AttachSession(_session);
        PartsView.AttachSession(_session);
        SettingsView.AttachSession(_session);
        _gameRunningMonitor = new GameRunningMonitor(_session);
        _session.GameRunningChanged += (_, _) => UpdateGameRunningBanner();
        UpdateGameRunningBanner();

        CrashReportContext.SessionProvider = () =>
        {
            var edition = WorkspaceConfigStore.TryGetActiveWorkspace();
            return new CrashSessionSnapshot(
                _session.HasPak,
                _session.PakPath,
                edition?.DisplayName);
        };

        _sidebarPinned = WorkspaceConfigStore.GetSidebarPinned();
        _suppressPinHandler = true;
        PinMenuCheckBox.IsChecked = _sidebarPinned;
        _suppressPinHandler = false;

        if (_sidebarPinned)
        {
            _navOpen = true;
        }

        ApplyNavLayout();

        Opened += async (_, _) =>
        {
            _gameRunningMonitor.Start();
            NavHome.IsChecked = true;
            ShowPage(HomeView);
            if (WorkspaceConfigStore.ConsumeCorruptConfigWarning())
            {
                await AppDialogs.ShowWarning(
                    this,
                    UiText.Main.ConfigCorruptMessage,
                    UiText.Main.ConfigCorruptTitle);
            }
        };

        Closed += (_, _) => _gameRunningMonitor.Dispose();
    }

    private void UpdateGameRunningBanner()
    {
        GameRunningBanner.IsVisible = _session.IsGameRunning;
    }

    private void MenuButton_Click(object? sender, RoutedEventArgs e)
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

    private void NavScrim_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        if (_sidebarPinned)
        {
            return;
        }

        _navOpen = false;
        ApplyNavLayout();
    }

    private async void ReportBugButton_Click(object? sender, RoutedEventArgs e)
    {
        if (!_sidebarPinned)
        {
            _navOpen = false;
            ApplyNavLayout();
        }

        var dialog = new BugReportWindow();
        await dialog.ShowDialog(this);
    }

    private void PinMenuCheckBox_Changed(object? sender, RoutedEventArgs e)
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
        NavPane.IsVisible = showNav;
        NavScrim.IsVisible = !_sidebarPinned && _navOpen;
        MainContent.Margin = _sidebarPinned ? new Thickness(220, 0, 0, 0) : new Thickness(0);
    }

    private void Nav_IsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (!IsLoaded || sender is not RadioButton { IsChecked: true } radio)
        {
            return;
        }

        if (ReferenceEquals(radio, NavHome))
        {
            ShowPage(HomeView);
        }
        else if (ReferenceEquals(radio, NavGeneral))
        {
            ShowPage(GeneralView);
        }
        else if (ReferenceEquals(radio, NavParts))
        {
            ShowPage(PartsView);
        }
        else if (ReferenceEquals(radio, NavVehicles))
        {
            ShowPage(PlaceholderVehicles);
        }
        else if (ReferenceEquals(radio, NavTrailers))
        {
            ShowPage(PlaceholderTrailers);
        }
        else if (ReferenceEquals(radio, NavPhotoMode))
        {
            ShowPage(PlaceholderPhotoMode);
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

    private void ShowPage(Control page)
    {
        CrashReportContext.SetPage(page switch
        {
            _ when ReferenceEquals(page, HomeView) => UiText.Nav.Home,
            _ when ReferenceEquals(page, GeneralView) => UiText.Nav.General,
            _ when ReferenceEquals(page, PartsView) => UiText.Nav.Parts,
            _ when ReferenceEquals(page, PlaceholderVehicles) => UiText.Nav.Vehicles,
            _ when ReferenceEquals(page, PlaceholderTrailers) => UiText.Nav.Trailers,
            _ when ReferenceEquals(page, PlaceholderPhotoMode) => UiText.Nav.PhotoMode,
            _ when ReferenceEquals(page, SettingsView) => UiText.Nav.Settings,
            _ => page.GetType().Name,
        });

        HomeView.IsVisible = false;
        GeneralView.IsVisible = false;
        PartsView.IsVisible = false;
        PlaceholderVehicles.IsVisible = false;
        PlaceholderTrailers.IsVisible = false;
        PlaceholderPhotoMode.IsVisible = false;
        SettingsView.IsVisible = false;
        page.IsVisible = true;
    }
}

internal sealed class GameRunningMonitor : IDisposable
{
    private readonly AppSession _session;
    private readonly DispatcherTimer _timer;
    private bool _disposed;

    public GameRunningMonitor(AppSession session)
    {
        _session = session;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _timer.Tick += (_, _) => Poll();
    }

    public void Start()
    {
        Poll();
        _timer.Start();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
    }

    private void Poll()
    {
        try
        {
            _session.SetGameRunning(SnowRunnerProcessGuard.IsRunning());
        }
        catch
        {
            // Never break the UI timer on probe failures.
        }
    }
}
