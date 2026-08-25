using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraceMap.Core;

public static class ScanPerformanceSchema
{
    public const string Version = "tracemap-scan-performance/v1";
    public const int MaxExtractorTimings = 64;
}

public static class ScanPerformanceExtractors
{
    public const string CSharpIntegrationSyntax = "csharp-integration-syntax";
    public const string RazorBinding = "razor-binding";
    public const string LegacyWcf = "legacy-wcf";
    public const string LegacyAsmx = "legacy-asmx";
    public const string LegacyRemoting = "legacy-remoting";
    public const string SqlFile = "sql-file";
    public const string SqlExecutionContext = "sql-execution-context";
    public const string PostgresSchemaMigration = "postgres-schema-migration";
    public const string SqlProjectRefactor = "sql-project-refactor";
    public const string Config = "config";
    public const string LegacyData = "legacy-data";
    public const string LegacyDataSymbolComposition = "legacy-data-symbol-composition";
    public const string LegacyWebForms = "legacy-webforms";
    public const string LegacyWinForms = "legacy-winforms";
    public const string LegacyAspNet = "legacy-aspnet";
    public const string LegacyBatchDataMovement = "legacy-batch-data-movement";
    public const string AnalyzerCapability = "analyzer-capability";

    private static readonly IReadOnlyDictionary<string, string> Versions =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [CSharpIntegrationSyntax] = ScannerVersions.CSharpIntegrationSyntaxExtractor,
            [RazorBinding] = ScannerVersions.RazorBindingExtractor,
            [LegacyWcf] = ScannerVersions.LegacyWcfExtractor,
            [LegacyAsmx] = ScannerVersions.LegacyAsmxExtractor,
            [LegacyRemoting] = ScannerVersions.LegacyRemotingExtractor,
            [SqlFile] = $"{ScannerVersions.SqlTextExtractor}+{ScannerVersions.SqlShapeExtractor}",
            [SqlExecutionContext] = ScannerVersions.SqlExecutionContextExtractor,
            [PostgresSchemaMigration] = ScannerVersions.PostgresSchemaMigrationExtractor,
            [SqlProjectRefactor] = ScannerVersions.SqlProjectRefactorExtractor,
            [Config] = ScannerVersions.ConfigExtractor,
            [LegacyData] = ScannerVersions.LegacyDataExtractor,
            [LegacyDataSymbolComposition] = ScannerVersions.LegacyDataSymbolComposition,
            [LegacyWebForms] = ScannerVersions.LegacyWebFormsExtractor,
            [LegacyWinForms] = ScannerVersions.LegacyWinFormsExtractor,
            [LegacyAspNet] = ScannerVersions.LegacyAspNetExtractor,
            [LegacyBatchDataMovement] = ScannerVersions.LegacyBatchDataMovementExtractor,
            [AnalyzerCapability] = ScannerVersions.AnalyzerCapabilityExtractor
        };

    public static (string Extractor, string Version) Normalize(string extractor) =>
        Versions.TryGetValue(extractor, out var version)
            ? (extractor, version)
            : ("other", "unavailable");
}

public sealed record ScanExtractorTiming(
    string Extractor,
    string ExtractorVersion,
    int Ordinal,
    string State,
    long ElapsedMilliseconds,
    long InputCount,
    long EmittedFactCount,
    long EmittedGapCount);

public sealed record ScanActiveExtractor(
    string Extractor,
    string ExtractorVersion,
    int Ordinal,
    long ElapsedMilliseconds,
    long InputCount);

public sealed record ScanPerformanceReceipt(
    string SchemaVersion,
    string RunState,
    long HeartbeatCount,
    bool HeartbeatObserved,
    string TimingCoverage,
    bool TimingsTruncated,
    IReadOnlyList<ScanExtractorTiming> ExtractorTimings,
    ScanActiveExtractor? ActiveExtractor,
    ScanExtractorTiming? SlowestExtractor,
    string NextAction,
    IReadOnlyList<string> Limitations);

internal sealed class ScanPerformanceTracker
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly string[] ReceiptLimitations =
    [
        "Elapsed times are local operational observations, not deterministic evidence facts or application-performance claims.",
        "Extractor timings are aggregate and intentionally omit repository, project, file, symbol, route, source, configuration, diagnostic, and exception identities.",
        "A missing terminal extractor observation means timing coverage is partial; it is not evidence that the extractor completed or failed."
    ];

    private readonly object gate = new();
    private readonly string path;
    private readonly TimeProvider timeProvider;
    private readonly List<ScanExtractorTiming> timings = [];
    private ActiveObservation? active;
    private long heartbeatCount;
    private bool timingsTruncated;
    private bool terminalLatched;
    private string runState = "running";
    private string timingCoverage = "unavailable";

    public ScanPerformanceTracker(string path, TimeProvider timeProvider)
    {
        this.path = path;
        this.timeProvider = timeProvider;
        WriteReceiptLocked();
    }

    public void StartExtractor(string extractor, int ordinal, long inputCount)
    {
        lock (gate)
        {
            var identity = ScanPerformanceExtractors.Normalize(extractor);
            active = new ActiveObservation(
                identity.Extractor,
                identity.Version,
                NormalizeOrdinal(ordinal),
                timeProvider.GetTimestamp(),
                NormalizeCount(inputCount));
            timingCoverage = "partial";
            WriteReceiptLocked();
        }
    }

    public void FinishExtractor(string extractor, int ordinal, string state, IReadOnlyList<CodeFact>? facts)
    {
        lock (gate)
        {
            if (terminalLatched)
            {
                return;
            }

            var identity = ScanPerformanceExtractors.Normalize(extractor);
            var normalizedOrdinal = NormalizeOrdinal(ordinal);
            var observation = active is not null
                && active.Extractor == identity.Extractor
                && active.Ordinal == normalizedOrdinal
                    ? active
                    : null;
            var elapsed = observation is null
                ? 0
                : ElapsedMilliseconds(observation.StartedAt, timeProvider.GetTimestamp());
            var output = facts ?? [];
            AddTiming(new ScanExtractorTiming(
                identity.Extractor,
                identity.Version,
                normalizedOrdinal,
                NormalizeTerminalState(state),
                elapsed,
                observation?.InputCount ?? 0,
                NormalizeCount(output.Count),
                NormalizeCount(output.Count(fact => fact.FactType == FactTypes.AnalysisGap))));
            active = null;
            WriteReceiptLocked();
        }
    }

    public void RecordHeartbeat()
    {
        lock (gate)
        {
            heartbeatCount = Math.Min(long.MaxValue, heartbeatCount + 1);
            WriteReceiptLocked();
        }
    }

    public string? RecordProgress(string stage, string state)
    {
        lock (gate)
        {
            if (state == "heartbeat")
            {
                return null;
            }

            if (state is "failed" or "cancelled" or "timed-out")
            {
                runState = state;
                timingCoverage = timings.Count == 0 ? "unavailable" : "partial";
                terminalLatched = state is "cancelled" or "timed-out";
            }
            else if (stage == ScanProgressStages.SpecializedExtraction && state is "completed" or "partial")
            {
                timingCoverage = active is null && !timingsTruncated ? "complete" : "partial";
            }
            else if (stage == ScanProgressStages.LocalReviewPublication && state == "completed")
            {
                runState = "completed";
            }

            WriteReceiptLocked();
            if (stage != ScanProgressStages.SpecializedExtraction || state is "started" or "heartbeat")
            {
                return null;
            }

            var receipt = BuildReceiptLocked();
            var slowest = receipt.SlowestExtractor;
            return slowest is null
                ? $"tracemap-performance {ScanPerformanceSchema.Version} state={receipt.RunState} timingCoverage={receipt.TimingCoverage} heartbeatCount={receipt.HeartbeatCount} slowestExtractor=unavailable nextAction={receipt.NextAction}"
                : $"tracemap-performance {ScanPerformanceSchema.Version} state={receipt.RunState} timingCoverage={receipt.TimingCoverage} heartbeatCount={receipt.HeartbeatCount} slowestExtractor={slowest.Extractor} slowestElapsedMs={slowest.ElapsedMilliseconds} nextAction={receipt.NextAction}";
        }
    }

    public ScanPerformanceReceipt ReadReceipt()
    {
        lock (gate)
        {
            return BuildReceiptLocked();
        }
    }

    private void AddTiming(ScanExtractorTiming timing)
    {
        if (timings.Count >= ScanPerformanceSchema.MaxExtractorTimings)
        {
            timingsTruncated = true;
            return;
        }

        timings.Add(timing);
    }

    private ScanPerformanceReceipt BuildReceiptLocked()
    {
        var terminalTimings = timings
            .Where(timing => timing.State is "completed" or "partial" or "failed" or "cancelled" or "timed-out")
            .OrderBy(timing => timing.Ordinal)
            .ThenBy(timing => timing.Extractor, StringComparer.Ordinal)
            .ToArray();
        var slowest = terminalTimings
            .OrderByDescending(timing => timing.ElapsedMilliseconds)
            .ThenBy(timing => timing.Ordinal)
            .ThenBy(timing => timing.Extractor, StringComparer.Ordinal)
            .FirstOrDefault();
        var activeReceipt = active is null
            ? null
            : new ScanActiveExtractor(
                active.Extractor,
                active.Version,
                active.Ordinal,
                ElapsedMilliseconds(active.StartedAt, timeProvider.GetTimestamp()),
                active.InputCount);
        return new ScanPerformanceReceipt(
            ScanPerformanceSchema.Version,
            runState,
            heartbeatCount,
            heartbeatCount > 0,
            timingCoverage,
            timingsTruncated,
            terminalTimings,
            activeReceipt,
            slowest,
            NextAction(activeReceipt, slowest),
            ReceiptLimitations);
    }

    private string NextAction(ScanActiveExtractor? activeReceipt, ScanExtractorTiming? slowest)
    {
        if (runState is "timed-out" or "failed" && activeReceipt is not null)
        {
            return "inspect-specialized-extractor";
        }

        if (timingCoverage == "complete" && slowest is not null)
        {
            return "inspect-specialized-extractor";
        }

        return "inspect-scan-progress";
    }

    private void WriteReceiptLocked()
    {
        try
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, JsonSerializer.Serialize(BuildReceiptLocked(), JsonOptions));
                File.Move(temporary, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporary))
                {
                    File.Delete(temporary);
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // Operational diagnostics are best effort and never alter scan evidence or outcome.
        }
    }

    private long ElapsedMilliseconds(long startedAt, long endedAt)
    {
        var frequency = timeProvider.TimestampFrequency;
        if (frequency <= 0)
        {
            return 0;
        }

        var deltaTicks = (endedAt - startedAt) * (double)TimeSpan.TicksPerSecond / frequency;
        return Math.Max(0, (long)TimeSpan.FromTicks((long)deltaTicks).TotalMilliseconds);
    }

    private static long NormalizeCount(long value) => Math.Clamp(value, 0, 1_000_000_000);

    private static int NormalizeOrdinal(int value) => Math.Clamp(value, 1, 1_000_000);

    private static string NormalizeTerminalState(string state) =>
        state is "completed" or "partial" or "failed" or "cancelled" or "timed-out" ? state : "failed";

    private sealed record ActiveObservation(
        string Extractor,
        string Version,
        int Ordinal,
        long StartedAt,
        long InputCount);
}
