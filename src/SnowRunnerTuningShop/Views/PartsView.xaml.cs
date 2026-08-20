using System.Windows.Controls;

namespace SnowRunnerTuningShop.Views;

public partial class PartsView : UserControl
{
    private AppSession? _session;

    public PartsView()
    {
        InitializeComponent();
    }

    public event EventHandler<string>? StatusChanged
    {
        add => WinchTuningView.StatusChanged += value;
        remove => WinchTuningView.StatusChanged -= value;
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadWinches();
        ReloadWinches();
    }

    private void ReloadWinches()
    {
        if (_session?.HasPak == true && !string.IsNullOrWhiteSpace(_session.PakPath))
        {
            WinchTuningView.LoadFromPak(_session.PakPath);
        }
        else
        {
            WinchTuningView.Clear();
        }
    }
}
