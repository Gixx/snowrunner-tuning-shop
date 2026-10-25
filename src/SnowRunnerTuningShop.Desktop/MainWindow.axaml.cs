using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Core.Config;
using SnowRunnerTuningShop.Core.Diagnostics;
using SnowRunnerTuningShop.Core.Game;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Desktop.Views;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop;

public partial class MainWindow : Window
{
    private static readonly TimeSpan ToastVisibleDuration = TimeSpan.FromSeconds(3.2);

    private readonly AppSession _session = new();
    private readonly GameRunningMonitor _gameRunningMonitor;
    private readonly DispatcherTimer _toastHideTimer;
    private bool _navOpen;
    private bool _sidebarPinned;
    private bool _suppressPinHandler;
    private CancellationTokenSource? _toastFadeCts;

    public MainWindow()
    {
        InitializeComponent();

        _toastHideTimer = new DispatcherTimer { Interval = ToastVisibleDuration };
        _toastHideTimer.Tick += (_, _) =>
        {
            _toastHideTimer.Stop();
            _ = FadeToastAsync(toVisible: false);
        };

        TuningProfileService.ProfileSynced += OnProfileSynced;

        ToolTip.SetTip(MenuButton, UiText.Nav.OpenMenu);
        NavHome.Content = UiText.Nav.Home;
        NavGeneral.Content = UiText.Nav.General;
        NavParts.Content = UiText.Nav.Parts;
        NavVehicles.Content = UiText.Nav.Vehicles;
        NavTrailers.Content = UiText.Nav.Trailers;
        NavCargo.Content = UiText.Nav.Cargo;
        NavSettings.Content = UiText.Nav.Settings;
        ReportBugButton.Content = UiText.Nav.ReportBug;
        PinMenuCheckBox.Content = UiText.Nav.PinMenu;
        VersionText.Text = UiText.Nav.VersionLabel;
        SubtitleText.Text = UiText.Main.Subtitle;
        GameRunningBannerText.Text = UiText.Main.GameRunningBanner;
        HomeView.AttachSession(_session);
        GeneralView.AttachSession(_session);
        PartsView.AttachSession(_session);
        VehiclesView.AttachSession(_session);
        TrailersView.AttachSession(_session);
        CargoView.AttachSession(_session);
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

        Closed += (_, _) =>
        {
            TuningProfileService.ProfileSynced -= OnProfileSynced;
            _gameRunningMonitor.Dispose();
        };
    }

    private void OnProfileSynced(object? sender, EventArgs e)
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => _session.NotifyTuningChanged());
            return;
        }

        _session.NotifyTuningChanged();
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
            ShowPage(VehiclesView);
        }
        else if (ReferenceEquals(radio, NavTrailers))
        {
            ShowPage(TrailersView);
        }
        else if (ReferenceEquals(radio, NavCargo))
        {
            ShowPage(CargoView);
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
            _ when ReferenceEquals(page, VehiclesView) => UiText.Nav.Vehicles,
            _ when ReferenceEquals(page, TrailersView) => UiText.Nav.Trailers,
            _ when ReferenceEquals(page, CargoView) => UiText.Nav.Cargo,
            _ when ReferenceEquals(page, SettingsView) => UiText.Nav.Settings,
            _ => page.GetType().Name,
        });

        HomeView.IsVisible = false;
        GeneralView.IsVisible = false;
        PartsView.IsVisible = false;
        VehiclesView.IsVisible = false;
        TrailersView.IsVisible = false;
        CargoView.IsVisible = false;
        SettingsView.IsVisible = false;
        page.IsVisible = true;

        if (ReferenceEquals(page, HomeView))
        {
            HomeView.RefreshPresetsFromOutside();
        }
    }

    /// <summary>Bottom-center snackbar for successful writes (non-modal).</summary>
    public void ShowToast(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        ToastText.Text = AppDialogs.FormatToastMessage(message);
        ToastHost.IsVisible = true;
        _ = FadeToastAsync(toVisible: true);
        _toastHideTimer.Stop();
        _toastHideTimer.Start();
    }

    private async Task FadeToastAsync(bool toVisible)
    {
        _toastFadeCts?.Cancel();
        _toastFadeCts = new CancellationTokenSource();
        var token = _toastFadeCts.Token;
        var from = ToastHost.Opacity;
        var to = toVisible ? 1.0 : 0.0;
        const int steps = 8;
        var stepMs = 180 / steps;
        try
        {
            for (var i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                ToastHost.Opacity = from + ((to - from) * i / steps);
                await Task.Delay(stepMs, token);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (!toVisible && ToastHost.Opacity <= 0.01)
        {
            ToastHost.IsVisible = false;
        }
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
