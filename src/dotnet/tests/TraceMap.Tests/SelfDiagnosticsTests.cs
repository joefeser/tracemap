using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SelfDiagnosticsTests
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "tracemap.command",
        "tracemap.phase",
        "tracemap.outcome",
        "tracemap.analysis_level",
        "tracemap.build_status",
        "tracemap.tool_version"
    };

    [Fact]
    public void Disabled_diagnostics_operation_path_allocates_no_per_operation_state()
    {
        for (var index = 0; index < 100; index++)
            TraceMapDiagnostics.StartCommand("scan").Dispose();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 10_000; index++)
            TraceMapDiagnostics.StartCommand("scan").Dispose();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Scan_diagnostics_are_hierarchical_privacy_safe_and_evidence_neutral()
    {
        using var temp = new TempDirectory();
        var repo = Path.Combine(temp.Path, "private-repo-token-should-not-escape");
        Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "SensitiveContract.cs"), "public sealed class SensitiveContract { }");

        var baseline = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "baseline")));
        var activities = new ConcurrentBag<ActivitySnapshot>();
        var measurements = new ConcurrentBag<MeasurementSnapshot>();
        using var activityListener = ListenForActivities(activities);
        using var meterListener = ListenForMeasurements(measurements);

        ScanResult observed;
        string? commandId;
        using (var command = TraceMapDiagnostics.StartCommand("scan"))
        {
            commandId = Activity.Current?.Id;
            observed = ScanEngine.Scan(new ScanOptions(repo, Path.Combine(temp.Path, "observed")));
            command.Complete(TraceMapDiagnosticOutcome.Succeeded);
        }

        Assert.Equal(baseline.Manifest.ScanId, observed.Manifest.ScanId);
        Assert.Equal(baseline.Manifest.SourceSnapshotDigest, observed.Manifest.SourceSnapshotDigest);
        Assert.Equal(
            JsonSerializer.Serialize(baseline.Facts, JsonOptions.StableLine),
            JsonSerializer.Serialize(observed.Facts, JsonOptions.StableLine));

        var commandActivity = Assert.Single(activities, activity => activity.Id == commandId);
        var scanActivity = Assert.Single(activities, activity =>
            activity.Name == "tracemap.scan"
            && activity.ParentId == commandId);
        Assert.Null(commandActivity.ParentId);
        Assert.Equal(commandActivity.Id, scanActivity.ParentId);
        Assert.Contains(activities, activity =>
            activity.Name == "tracemap.scan.discovery"
            && activity.ParentId == scanActivity.Id);
        Assert.Contains(activities, activity =>
            activity.Name == "tracemap.scan.semantic-analysis"
            && activity.ParentId == scanActivity.Id);
        Assert.All(activities, activity => AssertTagsAreSafe(activity.Tags, temp.Path));

        Assert.Contains(measurements, measurement => measurement.Instrument == "tracemap.operation.duration");
        Assert.Contains(measurements, measurement => measurement.Instrument == "tracemap.operation.count");
        Assert.Contains(measurements, measurement => measurement.Instrument == "tracemap.operation.items");
        Assert.All(measurements, measurement => AssertTagsAreSafe(measurement.Tags, temp.Path));
    }

    [Fact]
    public void Diagnostic_operations_close_with_bounded_failure_cancellation_and_partial_outcomes()
    {
        var activities = new ConcurrentBag<ActivitySnapshot>();
        using var listener = ListenForActivities(activities);

        using (var failed = TraceMapDiagnostics.StartCommand("scan"))
            failed.Complete(TraceMapDiagnosticOutcome.Failed, "secret-analysis", "secret-build");
        using (var cancelled = TraceMapDiagnostics.StartCommand("reduce"))
            cancelled.Complete(TraceMapDiagnosticOutcome.Cancelled);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            using var cancelledPhase = TraceMapDiagnostics.StartPhase(
                "reduce",
                TraceMapDiagnosticPhases.Reduction,
                cancellation.Token);
        }
        using (var partial = TraceMapDiagnostics.StartScan())
            partial.Complete(
                TraceMapDiagnosticOutcome.Partial,
                "Level1SemanticAnalysisReduced",
                "FailedOrPartial");

        Assert.Contains(activities, activity => activity.Tags.GetValueOrDefault("tracemap.outcome") == "failed");
        Assert.Equal(2, activities.Count(activity => activity.Tags.GetValueOrDefault("tracemap.outcome") == "cancelled"));
        Assert.Contains(activities, activity => activity.Tags.GetValueOrDefault("tracemap.outcome") == "partial");
        var failedActivity = Assert.Single(activities, activity =>
            activity.Tags.GetValueOrDefault("tracemap.outcome") == "failed");
        Assert.Equal("unknown", failedActivity.Tags["tracemap.analysis_level"]);
        Assert.Equal("unknown", failedActivity.Tags["tracemap.build_status"]);
    }

    [Fact]
    public void Cancelled_scan_stops_before_discovery_and_records_cancelled_operations()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "Sample.cs"), "public sealed class Sample { }");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var activities = new ConcurrentBag<ActivitySnapshot>();
        using var listener = ListenForActivities(activities);

        Assert.Throws<OperationCanceledException>(() => ScanEngine.Scan(
            new ScanOptions(temp.Path, Path.Combine(temp.Path, "out")),
            cancellation.Token));

        var scan = Assert.Single(activities, activity => activity.Name == "tracemap.scan");
        Assert.Equal("cancelled", scan.Tags["tracemap.outcome"]);
        Assert.DoesNotContain(activities, activity => activity.Name == "tracemap.scan.discovery");
    }

    private static ActivityListener ListenForActivities(ConcurrentBag<ActivitySnapshot> activities)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == TraceMapDiagnostics.ProviderName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = activity => activities.Add(new ActivitySnapshot(
                activity.OperationName,
                activity.Id,
                activity.ParentId,
                activity.TagObjects.ToDictionary(pair => pair.Key, pair => pair.Value?.ToString() ?? string.Empty, StringComparer.Ordinal)))
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    private static MeterListener ListenForMeasurements(ConcurrentBag<MeasurementSnapshot> measurements)
    {
        var listener = new MeterListener
        {
            InstrumentPublished = (instrument, meterListener) =>
            {
                if (instrument.Meter.Name == TraceMapDiagnostics.ProviderName)
                    meterListener.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<double>((instrument, _, tags, _) =>
            measurements.Add(new MeasurementSnapshot(instrument.Name, ToDictionary(tags))));
        listener.SetMeasurementEventCallback<long>((instrument, _, tags, _) =>
            measurements.Add(new MeasurementSnapshot(instrument.Name, ToDictionary(tags))));
        listener.Start();
        return listener;
    }

    private static IReadOnlyDictionary<string, string> ToDictionary(ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var tag in tags)
            result[tag.Key] = tag.Value?.ToString() ?? string.Empty;
        return result;
    }

    private static void AssertTagsAreSafe(IReadOnlyDictionary<string, string> tags, string privatePath)
    {
        Assert.All(tags.Keys, key => Assert.Contains(key, AllowedTags));
        var serialized = string.Join('|', tags.Select(pair => $"{pair.Key}={pair.Value}"));
        Assert.DoesNotContain(privatePath, serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("SensitiveContract", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("token-should-not-escape", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-analysis", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-build", serialized, StringComparison.Ordinal);
    }

    private sealed record ActivitySnapshot(
        string Name,
        string? Id,
        string? ParentId,
        IReadOnlyDictionary<string, string> Tags);

    private sealed record MeasurementSnapshot(
        string Instrument,
        IReadOnlyDictionary<string, string> Tags);
}
