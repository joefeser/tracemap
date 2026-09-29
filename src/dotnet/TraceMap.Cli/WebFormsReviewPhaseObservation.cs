using System.ComponentModel;
using System.Diagnostics;

namespace TraceMap.Cli;

public sealed record WebFormsReviewPhaseUsage(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string Phase, string MeasurementScope,
    long ElapsedMilliseconds, long? MaximumObservedWorkingSetBytes,
    long SuccessfulMemorySamples, int RequestedSamplingIntervalMilliseconds,
    IReadOnlyList<string> Limitations);

/// <summary>Constant-space operational observations, never a memory quota or exact phase peak.</summary>
internal sealed class WebFormsReviewPhaseObservation : IDisposable
{
    internal const string Rule = "workflow.webforms.phase-observation.v1";
    internal const string Schema = "webforms-review-phase-usage.v1";
    internal const string Scope = "scan-or-report-attempt-wall-time-and-sampled-parent-process-working-set";
    private const int IntervalMilliseconds = 1000;
    private readonly object gate = new();
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Func<long?> observeMemory;
    private readonly Timer timer;
    private long? maximum;
    private long samples;
    private WebFormsReviewPhaseUsage? completed;
    private readonly string phase;

    internal WebFormsReviewPhaseObservation(string phase, Func<long?>? observeMemory = null)
    {
        if (phase is not ("scan" or "reports")) throw new ArgumentOutOfRangeException(nameof(phase));
        this.phase = phase;
        this.observeMemory = observeMemory ?? ObserveWorkingSet;
        Sample();
        timer = new Timer(_ => Sample(), null, IntervalMilliseconds, IntervalMilliseconds);
    }

    internal void Sample()
    {
        lock (gate)
        {
            if (completed is not null) return;
            var value = observeMemory();
            if (value is not > 0) return;
            maximum = maximum is null ? value : Math.Max(maximum.Value, value.Value);
            samples++;
        }
    }

    internal WebFormsReviewPhaseUsage Finish()
    {
        lock (gate)
        {
            if (completed is not null) return completed;
            Sample();
            clock.Stop();
            completed = new(Schema, Rule, "Tier4Unknown", "local-only", phase, Scope, clock.ElapsedMilliseconds, maximum, samples,
                IntervalMilliseconds,
                ["Elapsed wall time includes extraction/report generation and final artifact/input/runtime validation, but excludes earlier preflight/admission and checkpoint publication.",
                 "Maximum observed working set is a lower bound from start/end and requested one-second samples, not an exact OS or phase peak or an enforced quota.",
                 "The whole parent process is observed; child/adaptor processes are excluded. Thread-pool scheduling and unavailable OS readings can miss spikes.",
                 "CPU work, metadata/IL admission work and transient disk peak are not measured here. Retained artifact bytes remain a separate counter."]);
            timer.Dispose();
            return completed;
        }
    }

    public void Dispose() => Finish();

    private static long? ObserveWorkingSet()
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.Refresh();
            return process.WorkingSet64;
        }
        catch (Exception exception) when (exception is Win32Exception or InvalidOperationException or NotSupportedException
            or IOException or UnauthorizedAccessException)
        { return null; }
    }
}

public static partial class WebFormsReviewExecutionCommand
{
    private static void ValidatePhaseUsage(WebFormsReviewCheckpoint checkpoint)
    {
        if (checkpoint.PhaseUsage is not { } usage) return; // Historical measurements remain unknown.
        if (checkpoint.State is "scan-started" or "reports-started"
            || usage.SchemaVersion != WebFormsReviewPhaseObservation.Schema
            || usage.RuleId != WebFormsReviewPhaseObservation.Rule
            || usage.EvidenceTier != "Tier4Unknown" || usage.Visibility != "local-only"
            || usage.MeasurementScope != WebFormsReviewPhaseObservation.Scope
            || usage.Phase != (checkpoint.State.StartsWith("reports-", StringComparison.Ordinal) ? "reports" : "scan")
            || usage.ElapsedMilliseconds < 0 || usage.SuccessfulMemorySamples < 0
            || usage.RequestedSamplingIntervalMilliseconds != 1000
            || (usage.SuccessfulMemorySamples == 0) != (usage.MaximumObservedWorkingSetBytes is null)
            || usage.MaximumObservedWorkingSetBytes is <= 0
            || usage.Limitations is null || usage.Limitations.Count is < 1 or > 16
            || usage.Limitations.Any(item => item is null || item.Length > 1024))
            throw Fail("PHASE_USAGE_INVALID");
    }
}
