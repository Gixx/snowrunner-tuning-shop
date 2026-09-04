using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace SnowRunnerTuningShop.Views;

public partial class PartsView : UserControl
{
    private AppSession? _session;
    private string? _loadedPakPath;
    private readonly HashSet<string> _loadedTabs = new(StringComparer.Ordinal);
    private int _loadVersion;
    private Storyboard? _spinnerStoryboard;
    private bool _selectionHandlerReady;

    public PartsView()
    {
        InitializeComponent();
        Loaded += (_, _) => _selectionHandlerReady = true;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => _ = ReloadPartsAsync();
        _session.BaselineChanged += (_, _) => RefreshWriteGates();
        _session.GameRunningChanged += (_, _) => RefreshWriteGates();
        _ = ReloadPartsAsync();
        RefreshWriteGates();
    }

    private void RefreshWriteGates()
    {
        var allowed = PakWriteUi.CanWrite(_session);
        WinchTuningView.SetPakWritesAllowed(allowed);
        EngineTuningView.SetPakWritesAllowed(allowed);
        GearboxTuningView.SetPakWritesAllowed(allowed);
        SuspensionTuningView.SetPakWritesAllowed(allowed);
        TireTuningView.SetPakWritesAllowed(allowed);
        WinchTuningView.RefreshRestoreButton();
        EngineTuningView.RefreshRestoreButton();
        GearboxTuningView.RefreshRestoreButton();
        SuspensionTuningView.RefreshRestoreButton();
        TireTuningView.RefreshRestoreButton();
    }

    private async void PartsTabControl_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_selectionHandlerReady || !IsLoaded)
        {
            return;
        }

        await EnsureSelectedTabLoadedAsync();
    }

    private async Task ReloadPartsAsync()
    {
        _loadedTabs.Clear();
        _loadedPakPath = null;

        if (_session?.HasPak == true && !string.IsNullOrWhiteSpace(_session.PakPath))
        {
            await EnsureSelectedTabLoadedAsync(force: true);
        }
        else
        {
            WinchTuningView.Clear();
            EngineTuningView.Clear();
            GearboxTuningView.Clear();
            SuspensionTuningView.Clear();
            TireTuningView.Clear();
            SetLoading(false);
        }
    }

    private async Task EnsureSelectedTabLoadedAsync(bool force = false)
    {
        if (_session?.HasPak != true || string.IsNullOrWhiteSpace(_session.PakPath))
        {
            return;
        }

        var tabKey = GetSelectedTabKey();
        if (tabKey is null)
        {
            return;
        }

        var pakPath = _session.PakPath;
        if (!force
            && string.Equals(_loadedPakPath, pakPath, StringComparison.OrdinalIgnoreCase)
            && _loadedTabs.Contains(tabKey))
        {
            return;
        }

        var version = ++_loadVersion;
        SetLoading(true);
        try
        {
            switch (tabKey)
            {
                case "winch":
                    await WinchTuningView.LoadFromPakAsync(pakPath);
                    break;
                case "engine":
                    await EngineTuningView.LoadFromPakAsync(pakPath);
                    break;
                case "gearbox":
                    await GearboxTuningView.LoadFromPakAsync(pakPath);
                    break;
                case "suspension":
                    await SuspensionTuningView.LoadFromPakAsync(pakPath);
                    break;
                case "tires":
                    await TireTuningView.LoadFromPakAsync(pakPath);
                    break;
            }

            if (version != _loadVersion)
            {
                return;
            }

            _loadedPakPath = pakPath;
            _loadedTabs.Add(tabKey);
        }
        finally
        {
            if (version == _loadVersion)
            {
                SetLoading(false);
            }
        }
    }

    private string? GetSelectedTabKey()
    {
        if (ReferenceEquals(PartsTabControl.SelectedItem, WinchTab))
        {
            return "winch";
        }

        if (ReferenceEquals(PartsTabControl.SelectedItem, EngineTab))
        {
            return "engine";
        }

        if (ReferenceEquals(PartsTabControl.SelectedItem, GearboxTab))
        {
            return "gearbox";
        }

        if (ReferenceEquals(PartsTabControl.SelectedItem, SuspensionTab))
        {
            return "suspension";
        }

        if (ReferenceEquals(PartsTabControl.SelectedItem, TiresTab))
        {
            return "tires";
        }

        return null;
    }

    private void SetLoading(bool isLoading)
    {
        LoadingOverlay.Visibility = isLoading ? Visibility.Visible : Visibility.Collapsed;
        if (isLoading)
        {
            StartSpinner();
        }
        else
        {
            StopSpinner();
        }
    }

    private void StartSpinner()
    {
        _spinnerStoryboard ??= CreateSpinnerStoryboard();
        _spinnerStoryboard.Begin();
    }

    private void StopSpinner()
    {
        _spinnerStoryboard?.Stop();
        LoadingSpinnerRotate.Angle = 0;
    }

    private Storyboard CreateSpinnerStoryboard()
    {
        var animation = new DoubleAnimation(0, 360, TimeSpan.FromSeconds(0.85))
        {
            RepeatBehavior = RepeatBehavior.Forever,
        };
        Storyboard.SetTarget(animation, LoadingSpinnerRotate);
        Storyboard.SetTargetProperty(animation, new PropertyPath(RotateTransform.AngleProperty));

        var storyboard = new Storyboard();
        storyboard.Children.Add(animation);
        return storyboard;
    }
}
