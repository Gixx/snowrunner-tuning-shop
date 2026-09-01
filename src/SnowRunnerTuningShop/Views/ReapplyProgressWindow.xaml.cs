using System.Windows;
using System.Windows.Threading;
using SnowRunnerTuningShop.Core.Profile;
using SnowRunnerTuningShop.Localization;

namespace SnowRunnerTuningShop.Views;

public partial class ReapplyProgressWindow : Window
{
    private const double PreparingSharePercent = 3;
    private const double StagingSharePercent = 2;
    private const double WritingSharePercent = 94;

    private readonly string _pakPath;
    private readonly DispatcherTimer _heartbeatTimer;
    private readonly DateTime _startedUtc;

    private TuningProfileReapplyProgress? _lastProgress;

    public TuningProfileReapplyResult? Result { get; private set; }

    public string? ErrorMessage { get; private set; }

    public ReapplyProgressWindow(string pakPath)
    {
        _pakPath = pakPath;
        _startedUtc = DateTime.UtcNow;
        InitializeComponent();
        StatusDetailText.Text = UiText.Workspace.ReapplyProgressStarting;
        ReapplyProgressBar.IsIndeterminate = true;
        CounterText.Text = string.Empty;

        _heartbeatTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(1),
        };
        _heartbeatTimer.Tick += HeartbeatTimer_Tick;
        _heartbeatTimer.Start();

        Loaded += ReapplyProgressWindow_Loaded;
        Closed += ReapplyProgressWindow_Closed;
    }

    private async void ReapplyProgressWindow_Loaded(object sender, RoutedEventArgs e)
    {
        Loaded -= ReapplyProgressWindow_Loaded;
        await RunReapplyAsync();
    }

    private void ReapplyProgressWindow_Closed(object? sender, EventArgs e)
    {
        _heartbeatTimer.Stop();
    }

    private void HeartbeatTimer_Tick(object? sender, EventArgs e)
    {
        if (_lastProgress is null)
        {
            CounterText.Text = UiText.Workspace.ReapplyProgressElapsed(TimeSpan.Zero);
            return;
        }

        RenderProgress(_lastProgress);
    }

    private async Task RunReapplyAsync()
    {
        var progress = new Progress<TuningProfileReapplyProgress>(OnProgress);
        try
        {
            Result = await Task.Run(() =>
                TuningProfileService.ReapplySavedChanges(_pakPath, progress));
            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            DialogResult = false;
            Close();
        }
    }

    private void OnProgress(TuningProfileReapplyProgress progress)
    {
        _lastProgress = progress;
        RenderProgress(progress);
    }

    private void RenderProgress(TuningProfileReapplyProgress progress)
    {
        ReapplyProgressBar.IsIndeterminate = false;
        ReapplyProgressBar.Value = Math.Clamp(CalculateOverallPercent(progress), 0, 100);

        var elapsed = DateTime.UtcNow - _startedUtc;
        if (progress.Phase == TuningProfileReapplyPhase.Finalizing)
        {
            CounterText.Text = UiText.Workspace.ReapplyProgressElapsed(elapsed);
            StatusDetailText.Text = UiText.Workspace.ReapplyProgressFinalizing;
            return;
        }

        var category = TuningProfileEntryCategories.Resolve(progress.EntryPath);
        StatusDetailText.Text = progress.Phase switch
        {
            TuningProfileReapplyPhase.Preparing =>
                UiText.Workspace.ReapplyProgressPreparing(category),
            TuningProfileReapplyPhase.StagingPak when progress.Total <= 1 =>
                UiText.Workspace.ReapplyProgressStagingCopy,
            TuningProfileReapplyPhase.StagingPak =>
                UiText.Workspace.ReapplyProgressStagingPrepare(category),
            TuningProfileReapplyPhase.WritingPak when progress.Current <= 0 =>
                UiText.Workspace.ReapplyProgressWritingPakStart,
            TuningProfileReapplyPhase.WritingPak =>
                UiText.Workspace.ReapplyProgressWriting(category),
            _ => UiText.Workspace.ReapplyProgressStarting,
        };

        if (progress.Total > 0)
        {
            var counter = progress.Phase switch
            {
                TuningProfileReapplyPhase.Preparing =>
                    UiText.Workspace.ReapplyProgressProfileCounter(progress.Current, progress.Total),
                TuningProfileReapplyPhase.StagingPak =>
                    UiText.Workspace.ReapplyProgressStagingCounter(progress.Current, progress.Total),
                TuningProfileReapplyPhase.WritingPak =>
                    UiText.Workspace.ReapplyProgressPakCounter(progress.Current, progress.Total),
                _ => string.Empty,
            };

            CounterText.Text = string.IsNullOrWhiteSpace(counter)
                ? UiText.Workspace.ReapplyProgressElapsed(elapsed)
                : $"{counter} · {UiText.Workspace.ReapplyProgressElapsed(elapsed)}";
        }
        else
        {
            CounterText.Text = UiText.Workspace.ReapplyProgressElapsed(elapsed);
        }
    }

    private static double CalculateOverallPercent(TuningProfileReapplyProgress progress)
    {
        if (progress.Total <= 0)
        {
            return progress.Phase switch
            {
                TuningProfileReapplyPhase.Finalizing => 100,
                TuningProfileReapplyPhase.StagingPak => PreparingSharePercent,
                TuningProfileReapplyPhase.WritingPak => PreparingSharePercent + StagingSharePercent,
                _ => 0,
            };
        }

        var phaseRatio = progress.Current / (double)progress.Total;
        return progress.Phase switch
        {
            TuningProfileReapplyPhase.Preparing => phaseRatio * PreparingSharePercent,
            TuningProfileReapplyPhase.StagingPak =>
                PreparingSharePercent + (phaseRatio * StagingSharePercent),
            TuningProfileReapplyPhase.WritingPak =>
                PreparingSharePercent + StagingSharePercent + (phaseRatio * WritingSharePercent),
            TuningProfileReapplyPhase.Finalizing => 100,
            _ => 0,
        };
    }
}
