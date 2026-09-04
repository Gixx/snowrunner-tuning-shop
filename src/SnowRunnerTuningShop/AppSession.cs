using SnowRunnerTuningShop.Core.Models;

namespace SnowRunnerTuningShop;

/// <summary>Shared loaded-pak state across Home / Parts / Vehicles pages.</summary>
public sealed class AppSession
{
    public string? PakPath { get; private set; }

    public PakSummary? Summary { get; private set; }

    public bool HasPak => !string.IsNullOrWhiteSpace(PakPath) && Summary is not null;

    /// <summary>True when a SnowRunner process is detected; pak writes must be blocked.</summary>
    public bool IsGameRunning { get; private set; }

    public event EventHandler? PakChanged;

    public event EventHandler? BaselineChanged;

    public event EventHandler? GameRunningChanged;

    public void SetPak(string pakPath, PakSummary summary)
    {
        PakPath = pakPath;
        Summary = summary;
        PakChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearPak()
    {
        PakPath = null;
        Summary = null;
        PakChanged?.Invoke(this, EventArgs.Empty);
    }

    public void NotifyBaselineChanged() =>
        BaselineChanged?.Invoke(this, EventArgs.Empty);

    public void SetGameRunning(bool isRunning)
    {
        if (IsGameRunning == isRunning)
        {
            return;
        }

        IsGameRunning = isRunning;
        GameRunningChanged?.Invoke(this, EventArgs.Empty);
    }
}
