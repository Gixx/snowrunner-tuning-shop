using Avalonia.Controls;
using SnowRunnerTuningShop;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Desktop.Views;

public partial class PartsView : UserControl
{
    private AppSession? _session;
    private string? _loadedPakPath;
    private readonly HashSet<string> _loadedTabs = new(StringComparer.Ordinal);
    private int _loadVersion;
    private CancellationTokenSource? _loadCts;
    private bool _selectionHandlerReady;

    public PartsView()
    {
        InitializeComponent();
        ApplyStaticText();
        Loaded += (_, _) => _selectionHandlerReady = true;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        WinchTuningView.AttachSession(session);
        EngineTuningView.AttachSession(session);
        GearboxTuningView.AttachSession(session);
        SuspensionTuningView.AttachSession(session);
        TireTuningView.AttachSession(session);
        AddonCapacityTuningView.AttachSession(session);
        CraneTuningView.AttachSession(session);
        WinchTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        EngineTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        GearboxTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        SuspensionTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        TireTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        AddonCapacityTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        CraneTuningView.StatusChanged += (_, message) => StatusText.Text = message;
        _session.PakChanged += (_, _) => _ = ReloadPartsAsync();
        _session.BaselineChanged += (_, _) => RefreshWriteGates();
        _session.GameRunningChanged += (_, _) => RefreshWriteGates();
        _ = ReloadPartsAsync();
        RefreshWriteGates();
    }

    private void ApplyStaticText()
    {
        WinchTab.Header = UiText.Parts.Winch;
        EngineTab.Header = UiText.Parts.Engine;
        GearboxTab.Header = UiText.Parts.Gearbox;
        SuspensionTab.Header = UiText.Parts.Suspension;
        TiresTab.Header = UiText.Parts.Tires;
        AddonsTab.Header = UiText.Parts.Addons;
        CranesTab.Header = UiText.Parts.Cranes;
        LoadingText.Text = UiText.Parts.Loading;
    }

    private void RefreshWriteGates()
    {
        var allowed = PakWriteUi.CanWrite(_session);
        WinchTuningView.SetPakWritesAllowed(allowed);
        EngineTuningView.SetPakWritesAllowed(allowed);
        GearboxTuningView.SetPakWritesAllowed(allowed);
        SuspensionTuningView.SetPakWritesAllowed(allowed);
        TireTuningView.SetPakWritesAllowed(allowed);
        AddonCapacityTuningView.SetPakWritesAllowed(allowed);
        CraneTuningView.SetPakWritesAllowed(allowed);
        WinchTuningView.RefreshRestoreButton();
        EngineTuningView.RefreshRestoreButton();
        GearboxTuningView.RefreshRestoreButton();
        SuspensionTuningView.RefreshRestoreButton();
        TireTuningView.RefreshRestoreButton();
        AddonCapacityTuningView.RefreshRestoreButton();
        CraneTuningView.RefreshRestoreButton();
    }

    private async void PartsTabControl_SelectionChanged(object? sender, SelectionChangedEventArgs e)
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
            AddonCapacityTuningView.Clear();
            CraneTuningView.Clear();
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
        _loadCts?.Cancel();
        _loadCts?.Dispose();
        _loadCts = new CancellationTokenSource();
        var cancellationToken = _loadCts.Token;

        SetLoading(true);
        try
        {
            switch (tabKey)
            {
                case "winch":
                    await WinchTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "engine":
                    await EngineTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "gearbox":
                    await GearboxTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "suspension":
                    await SuspensionTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "tires":
                    await TireTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "addons":
                    await AddonCapacityTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
                case "cranes":
                    await CraneTuningView.LoadFromPakAsync(pakPath, cancellationToken);
                    break;
            }

            if (cancellationToken.IsCancellationRequested || version != _loadVersion)
            {
                return;
            }

            _loadedPakPath = pakPath;
            _loadedTabs.Add(tabKey);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return;
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

        if (ReferenceEquals(PartsTabControl.SelectedItem, AddonsTab))
        {
            return "addons";
        }

        if (ReferenceEquals(PartsTabControl.SelectedItem, CranesTab))
        {
            return "cranes";
        }

        return null;
    }

    private void SetLoading(bool isLoading) =>
        LoadingOverlay.IsVisible = isLoading;
}
