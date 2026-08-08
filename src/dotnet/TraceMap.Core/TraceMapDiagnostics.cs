using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace TraceMap.Core;

public enum TraceMapDiagnosticOutcome
{
    Succeeded,
    Failed,
    Cancelled,
    Partial
}

public static class TraceMapDiagnosticPhases
{
    public const string Command = "command";
    public const string Discovery = "discovery";
    public const string SemanticAnalysis = "semantic-analysis";
    public const string StaticExtraction = "static-extraction";
    public const string ManifestWrite = "manifest-write";
    public const string FactsWrite = "facts-write";
    public const string IndexWrite = "index-write";
    public const string ReportWrite = "report-write";
    public const string AnalyzerLogWrite = "analyzer-log-write";
    public const string Reduction = "reduction";
    public const string CombinedReport = "combined-report";
}

public static class TraceMapDiagnostics
{
    public const string ProviderName = "TraceMap";
    public const string ToolVersion = "tracemap-milestone16";

    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "scan", "reduce", "report", "combine", "reverse-impact"
    };

    private static readonly HashSet<string> Phases = new(StringComparer.Ordinal)
    {
        TraceMapDiagnosticPhases.Command,
        TraceMapDiagnosticPhases.Discovery,
        TraceMapDiagnosticPhases.SemanticAnalysis,
        TraceMapDiagnosticPhases.StaticExtraction,
        TraceMapDiagnosticPhases.ManifestWrite,
        TraceMapDiagnosticPhases.FactsWrite,
        TraceMapDiagnosticPhases.IndexWrite,
        TraceMapDiagnosticPhases.ReportWrite,
        TraceMapDiagnosticPhases.AnalyzerLogWrite,
        TraceMapDiagnosticPhases.Reduction,
        TraceMapDiagnosticPhases.CombinedReport
    };

    private static readonly HashSet<string> AnalysisLevels = new(StringComparer.Ordinal)
    {
        "Level1SemanticAnalysis",
        "Level1SemanticAnalysisReduced",
        "Level3SyntaxAnalysis",
        "Level3SyntaxAnalysisReduced"
    };

    private static readonly HashSet<string> BuildStatuses = new(StringComparer.Ordinal)
    {
        "Succeeded", "FailedOrPartial", "NotRun"
    };

    public static readonly ActivitySource ActivitySource = new(ProviderName, ToolVersion);
    public static readonly Meter Meter = new(ProviderName, ToolVersion);

    internal static readonly Histogram<double> OperationDuration = Meter.CreateHistogram<double>(
        "tracemap.operation.duration",
        "ms",
        "Elapsed monotonic duration for a bounded TraceMap operation.");

    internal static readonly Counter<long> OperationCount = Meter.CreateCounter<long>(
        "tracemap.operation.count",
        description: "Completed bounded TraceMap operations.");

    internal static readonly Histogram<long> OperationItems = Meter.CreateHistogram<long>(
        "tracemap.operation.items",
        "{item}",
        "Aggregate item count for a bounded TraceMap operation.");

    public static TraceMapDiagnosticOperation StartCommand(string command) =>
        Start("tracemap.command", command, TraceMapDiagnosticPhases.Command);

    public static TraceMapDiagnosticOperation StartScan() => StartScan(CancellationToken.None);

    public static TraceMapDiagnosticOperation StartScan(CancellationToken cancellationToken) =>
        Start("tracemap.scan", "scan", TraceMapDiagnosticPhases.Command, cancellationToken);

    public static TraceMapDiagnosticOperation StartPhase(
        string command,
        string phase,
        CancellationToken cancellationToken = default) =>
        Start("tracemap." + NormalizeCommand(command) + "." + NormalizePhase(phase), command, phase, cancellationToken);

    private static TraceMapDiagnosticOperation Start(
        string operationName,
        string command,
        string phase,
        CancellationToken cancellationToken = default)
    {
        var normalizedCommand = NormalizeCommand(command);
        var normalizedPhase = NormalizePhase(phase);
        if (!ActivitySource.HasListeners()
            && !OperationDuration.Enabled
            && !OperationCount.Enabled
            && !OperationItems.Enabled)
        {
            return TraceMapDiagnosticOperation.Disabled;
        }

        var tags = new ActivityTagsCollection
        {
            ["tracemap.command"] = normalizedCommand,
            ["tracemap.phase"] = normalizedPhase,
            ["tracemap.tool_version"] = ToolVersion
        };
        var activity = ActivitySource.StartActivity(operationName, ActivityKind.Internal, default(ActivityContext), tags);
        return new TraceMapDiagnosticOperation(activity, normalizedCommand, normalizedPhase, cancellationToken);
    }

    internal static string NormalizeAnalysisLevel(string? value) =>
        value is not null && AnalysisLevels.Contains(value) ? value : "unknown";

    internal static string NormalizeBuildStatus(string? value) =>
        value is not null && BuildStatuses.Contains(value) ? value : "unknown";

    private static string NormalizeCommand(string value) => Commands.Contains(value) ? value : "other";

    private static string NormalizePhase(string value) => Phases.Contains(value) ? value : "other";
}

public sealed class TraceMapDiagnosticOperation : IDisposable
{
    internal static TraceMapDiagnosticOperation Disabled { get; } = new();

    private readonly Activity? activity;
    private readonly string command = "other";
    private readonly string phase = "other";
    private readonly long startedAt;
    private readonly bool enabled;
    private readonly CancellationToken cancellationToken;
    private int completed;

    private TraceMapDiagnosticOperation()
    {
    }

    internal TraceMapDiagnosticOperation(
        Activity? activity,
        string command,
        string phase,
        CancellationToken cancellationToken)
    {
        this.activity = activity;
        this.command = command;
        this.phase = phase;
        this.cancellationToken = cancellationToken;
        startedAt = Stopwatch.GetTimestamp();
        enabled = true;
    }

    public void RecordItems(long count)
    {
        if (!enabled || count < 0)
            return;

        TraceMapDiagnostics.OperationItems.Record(count, Tags(outcome: null));
    }

    public void Complete(
        TraceMapDiagnosticOutcome outcome,
        string? analysisLevel = null,
        string? buildStatus = null)
    {
        if (!enabled || Interlocked.Exchange(ref completed, 1) != 0)
            return;

        var outcomeValue = outcome.ToString().ToLowerInvariant();
        var normalizedAnalysisLevel = TraceMapDiagnostics.NormalizeAnalysisLevel(analysisLevel);
        var normalizedBuildStatus = TraceMapDiagnostics.NormalizeBuildStatus(buildStatus);
        activity?.SetTag("tracemap.outcome", outcomeValue);
        if (analysisLevel is not null)
            activity?.SetTag("tracemap.analysis_level", normalizedAnalysisLevel);
        if (buildStatus is not null)
            activity?.SetTag("tracemap.build_status", normalizedBuildStatus);

        var tags = Tags(outcomeValue, analysisLevel is null ? null : normalizedAnalysisLevel, buildStatus is null ? null : normalizedBuildStatus);
        var elapsedMilliseconds = Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds;
        TraceMapDiagnostics.OperationDuration.Record(elapsedMilliseconds, tags);
        TraceMapDiagnostics.OperationCount.Add(1, tags);
        activity?.Dispose();
    }

    public void Dispose() => Complete(cancellationToken.IsCancellationRequested
        ? TraceMapDiagnosticOutcome.Cancelled
        : TraceMapDiagnosticOutcome.Failed);

    private TagList Tags(string? outcome, string? analysisLevel = null, string? buildStatus = null)
    {
        var tags = new TagList
        {
            { "tracemap.command", command },
            { "tracemap.phase", phase },
            { "tracemap.tool_version", TraceMapDiagnostics.ToolVersion }
        };
        if (outcome is not null)
            tags.Add("tracemap.outcome", outcome);
        if (analysisLevel is not null)
            tags.Add("tracemap.analysis_level", analysisLevel);
        if (buildStatus is not null)
            tags.Add("tracemap.build_status", buildStatus);
        return tags;
    }
}
