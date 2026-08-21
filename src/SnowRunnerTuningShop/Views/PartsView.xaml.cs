using System.Windows.Controls;

namespace SnowRunnerTuningShop.Views;

public partial class PartsView : UserControl
{
    private AppSession? _session;

    public PartsView()
    {
        InitializeComponent();
    }

    public void AttachSession(AppSession session)
    {
        _session = session;
        _session.PakChanged += (_, _) => ReloadParts();
        _session.BaselineChanged += (_, _) =>
        {
            WinchTuningView.RefreshRestoreButton();
            EngineTuningView.RefreshRestoreButton();
            GearboxTuningView.RefreshRestoreButton();
            SuspensionTuningView.RefreshRestoreButton();
            TireTuningView.RefreshRestoreButton();
        };
        ReloadParts();
    }

    private void ReloadParts()
    {
        if (_session?.HasPak == true && !string.IsNullOrWhiteSpace(_session.PakPath))
        {
            WinchTuningView.LoadFromPak(_session.PakPath);
            EngineTuningView.LoadFromPak(_session.PakPath);
            GearboxTuningView.LoadFromPak(_session.PakPath);
            SuspensionTuningView.LoadFromPak(_session.PakPath);
            TireTuningView.LoadFromPak(_session.PakPath);
        }
        else
        {
            WinchTuningView.Clear();
            EngineTuningView.Clear();
            GearboxTuningView.Clear();
            SuspensionTuningView.Clear();
            TireTuningView.Clear();
        }
    }
}
