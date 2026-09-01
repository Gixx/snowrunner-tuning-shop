namespace SnowRunnerTuningShop.Core.Profile;

internal static class ThrottledProgress
{
    internal static void Report(
        IProgress<TuningProfileReapplyProgress>? progress,
        TuningProfileReapplyProgress value,
        ref int lastReportedStep,
        int minimumStepDelta = 50)
    {
        if (progress is null)
        {
            return;
        }

        var isComplete = value.Total > 0 && value.Current >= value.Total;
        var isStart = value.Current <= 1;
        if (!isComplete && !isStart && value.Current - lastReportedStep < minimumStepDelta)
        {
            return;
        }

        lastReportedStep = value.Current;
        progress.Report(value);
    }
}
