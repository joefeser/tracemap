using System.Text.Json;
using System.Text.Json.Serialization;

namespace TraceMap.Core;

/// <summary>
/// Versioned schema identity and bounds for the operational progress contract.
/// Progress events are operational observations only. They are not TraceMap
/// evidence facts, carry no evidence tier, and never state scan conclusions.
/// </summary>
public static class ScanProgressSchema
{
    public const string Version = "tracemap-scan-progress/v1";
    public const int MaxHistoryEvents = 32;
}

/// <summary>
/// Closed catalog of categorical stage names. Stages never embed repository,
/// project, solution, file, or symbol identities; repeated work is identified
/// only by a deterministic ordinal.
/// </summary>
public static class ScanProgressStages
{
    public const string ArgumentsValidated = "arguments-validated";
    public const string OutputAuthorized = "output-authorized";
    public const string StagingInitialized = "staging-initialized";
    public const string Scan = "scan";
    public const string RepositoryIdentity = "repository-identity";
    public const string Inventory = "inventory";
    public const string SourceSnapshotCapture = "source-snapshot-capture";
    public const string ProjectSelection = "project-selection";
    public const string MsBuildRegistration = "msbuild-registration";
    public const string SolutionLoad = "solution-load";
    public const string ProjectLoad = "project-load";
    public const string Compilation = "compilation";
    public const string SyntaxFallback = "syntax-fallback";
    public const string SpecializedExtraction = "specialized-extraction";
    public const string SourceVerification = "source-verification";
    public const string ArtifactWrite = "artifact-write";
    public const string ScanPublication = "scan-publication";
    public const string WebFormsModernization = "webforms-modernization";
    public const string Explorer = "explorer";
    public const string LocalReviewPublication = "local-review-publication";
}

/// <summary>
/// A single sanitized progress observation. Every field is bounded categorical
/// data: schema identity, sequence, stage catalog values, aggregate counts,
/// a typed failure code, and the last completed categorical stage.
/// </summary>
public sealed record ScanProgressEvent(
    string SchemaVersion,
    long Sequence,
    string Operation,
    string Stage,
    string State,
    long ElapsedMilliseconds,
    IReadOnlyDictionary<string, long>? Counts,
    string? FailureCode,
    string? LastSuccessfulStage,
    int? Ordinal);

/// <summary>
/// Durable checkpoint shape persisted at the operator-selected path. It holds
/// the latest event plus a bounded history of non-heartbeat events.
/// </summary>
public sealed record ScanProgressCheckpoint(
    string SchemaVersion,
    ScanProgressEvent Latest,
    IReadOnlyList<ScanProgressEvent> History);

/// <summary>
/// Emits sanitized scan-progress observations. When enabled, every event is
/// written immediately to the console sink (real stderr in production, never
/// the buffered scan output writers) and atomically to the durable checkpoint
/// file. A background heartbeat reports the active categorical stage while a
/// stage is in progress, including while the calling thread is blocked inside
/// MSBuildWorkspace or Roslyn waits.
/// </summary>
public sealed class ScanProgressReporter : IDisposable
{
    public const string ScanOperation = "scan";
    public const string LocalReviewOperation = "local-review";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions DocumentJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        ScanOperation,
        LocalReviewOperation
    };
    private static readonly HashSet<string> States = new(StringComparer.Ordinal)
    {
        "started", "heartbeat", "completed", "partial", "failed", "cancelled", "timed-out"
    };
    private static readonly HashSet<string> CountKeys = new(StringComparer.Ordinal)
    {
        "files", "solutions", "projects", "facts", "gaps"
    };
    private static readonly HashSet<string> FailureCodes = new(StringComparer.Ordinal)
    {
        // Workflow-level local-review codes.
        "LOCAL_REVIEW_ARGUMENT_INVALID",
        "LOCAL_REVIEW_CANCELLED",
        "LOCAL_REVIEW_CLEANUP_FAILED",
        "LOCAL_REVIEW_EXPLORER_FAILED",
        "LOCAL_REVIEW_EXPLORER_INPUT_INCOMPATIBLE",
        "LOCAL_REVIEW_IDENTITY_UNAVAILABLE",
        "LOCAL_REVIEW_INPUT_MUTATED",
        "LOCAL_REVIEW_OUTPUT_COLLISION",
        "LOCAL_REVIEW_OUTPUT_UNSAFE",
        "LOCAL_REVIEW_PROGRESS_PATH_UNSAFE",
        "LOCAL_REVIEW_SCAN_FAILED",
        "LOCAL_REVIEW_SCAN_PARTIAL",
        "LOCAL_REVIEW_STAGE_FAILED",
        "LOCAL_REVIEW_TIMEOUT",
        "LOCAL_REVIEW_TIMEOUT_INVALID",
        "LOCAL_REVIEW_WEBFORMS_FAILED",
        "LOCAL_REVIEW_WEBFORMS_INPUT_INCOMPATIBLE",
        "LOCAL_REVIEW_WEBFORMS_PARTIAL",
        // Scan-internal categorical codes.
        "ARTIFACT_WRITE_FAILED",
        "COMPILATION_CREATE_FAILED",
        "COMPILATION_MISSING",
        "MSBUILD_REGISTRATION_FAILED",
        "PROJECT_LOAD_FAILED",
        "SCAN_DISCOVERY_FAILED",
        "SEMANTIC_STAGE_FAILED",
        "SOLUTION_LOAD_FAILED",
        "SOURCE_VERIFICATION_FAILED"
    };

    private readonly object gate = new();
    private readonly TextWriter? console;
    private readonly string? checkpointPath;
    private readonly TimeProvider timeProvider;
    private readonly List<ScanProgressEvent> history = [];
    private readonly ITimer? heartbeatTimer;
    private readonly ScanPerformanceTracker? performanceTracker;
    private readonly List<(string Stage, string Operation, int? Ordinal)> activeStages = [];
    private long sequence;
    private long startedAt;
    private string? lastSuccessfulStage;
    private bool terminalLatched;
    private bool disposed;

    public ScanProgressReporter(
        TextWriter? console,
        string? checkpointPath,
        TimeProvider? timeProvider = null,
        TimeSpan? heartbeatInterval = null)
    {
        this.console = console;
        this.checkpointPath = checkpointPath;
        this.timeProvider = timeProvider ?? TimeProvider.System;
        performanceTracker = checkpointPath is null
            ? null
            : new ScanPerformanceTracker(checkpointPath + ".performance.json", this.timeProvider);
        var interval = heartbeatInterval ?? TimeSpan.FromSeconds(15);
        if (interval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(heartbeatInterval));
        }

        startedAt = this.timeProvider.GetTimestamp();
        heartbeatTimer = this.timeProvider.CreateTimer(
            static state => ((ScanProgressReporter)state!).EmitHeartbeat(),
            this,
        interval,
        interval);
        WriteCheckpointLocked(new ScanProgressCheckpoint(
            ScanProgressSchema.Version,
            new ScanProgressEvent(
                ScanProgressSchema.Version,
                0,
                LocalReviewOperation,
                "arguments-validated",
                "started",
                0,
                null,
                null,
                null,
                null),
            []));
    }

    public IReadOnlyList<CodeFact> ObserveExtractor(
        string extractor,
        int ordinal,
        long inputCount,
        Func<IReadOnlyList<CodeFact>> extract)
    {
        performanceTracker?.StartExtractor(extractor, ordinal, inputCount);
        try
        {
            var facts = extract();
            performanceTracker?.FinishExtractor(extractor, ordinal, "completed", facts);
            return facts;
        }
        catch (OperationCanceledException)
        {
            performanceTracker?.FinishExtractor(extractor, ordinal, "cancelled", null);
            throw;
        }
        catch
        {
            performanceTracker?.FinishExtractor(extractor, ordinal, "failed", null);
            throw;
        }
    }

    /// <summary>
    /// True while a categorical stage is active. Heartbeat observations are
    /// only produced while at least one stage is on the active stack.
    /// </summary>
    public bool IsStageActive
    {
        get
        {
            lock (gate)
            {
                return activeStages.Count > 0;
            }
        }
    }

    /// <summary>Emits a stage-started observation and marks the stage active.</summary>
    public void StartStage(string operation, string stage, int? ordinal = null) =>
        Emit(operation, stage, "started", ordinal: ordinal);

    /// <summary>Emits a terminal stage observation. Completed and partial mark the stage successful.</summary>
    public void FinishStage(
        string operation,
        string stage,
        string state,
        int? ordinal = null,
        IReadOnlyDictionary<string, long>? counts = null,
        string? failureCode = null) =>
        Emit(operation, stage, state, ordinal, counts, failureCode);

    /// <summary>Emits an explicit non-stage observation such as a publication boundary.</summary>
    public void Emit(
        string operation,
        string stage,
        string state,
        int? ordinal = null,
        IReadOnlyDictionary<string, long>? counts = null,
        string? failureCode = null) =>
        EmitCore(operation, stage, state, ordinal, counts, failureCode);

    /// <summary>
    /// Emits a terminal observation for the innermost active categorical stage.
    /// If no stage is active, the observation is dropped.
    /// </summary>
    public void FinishActiveStage(string operation, string state, string? failureCode = null)
    {
        lock (gate)
        {
            if (disposed || activeStages.Count == 0)
            {
                return;
            }

            var innermost = activeStages[^1];
            EmitUnderLock(operation, innermost.Stage, state, innermost.Ordinal, null, failureCode);
        }
    }

    /// <summary>
    /// Emits a terminal observation for the innermost active stage and ends
    /// every active stage, then latches the reporter terminal. Used for
    /// workflow-terminal outcomes such as the timeout deadline, where the
    /// budget for the whole run has elapsed: the scanner thread may continue
    /// briefly through an API that ignored cancellation, so later stage
    /// transitions and heartbeats are dropped instead of overwriting the
    /// terminal observation in the checkpoint. Only further timed-out
    /// observations are accepted after the latch. When no stage is active the
    /// observation falls back to the enclosing workflow scan stage.
    /// </summary>
    public void FinishAllStages(string operation, string state, string? failureCode = null)
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            var innermost = activeStages.Count > 0
                ? activeStages[^1]
                : (Stage: ScanProgressStages.Scan, Operation: operation, Ordinal: (int?)null);
            activeStages.Clear();
            EmitUnderLock(operation, innermost.Stage, state, innermost.Ordinal, null, failureCode);
            terminalLatched = true;
        }
    }

    /// <summary>Fails whichever categorical stage is currently active.</summary>
    public void FailActiveStage(string operation, string failureCode) =>
        FinishActiveStage(operation, "failed", failureCode);

    private void EmitCore(
        string operation,
        string stage,
        string state,
        int? ordinal = null,
        IReadOnlyDictionary<string, long>? counts = null,
        string? failureCode = null)
    {
        ArgumentNullException.ThrowIfNull(operation);
        ArgumentNullException.ThrowIfNull(stage);
        ArgumentNullException.ThrowIfNull(state);

        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            EmitUnderLock(operation, stage, state, ordinal, counts, failureCode);
        }
    }

    private void EmitUnderLock(
        string operation,
        string stage,
        string state,
        int? ordinal,
        IReadOnlyDictionary<string, long>? counts,
        string? failureCode)
    {
        var normalizedOperation = NormalizeAllowed(operation, Operations, "scan");
        var normalizedStage = IsKnownStage(stage) ? stage : "other";
        var normalizedState = NormalizeAllowed(state, States, "failed");
        if (terminalLatched && normalizedState != "timed-out")
        {
            // The run's budget ended; post-terminal stage transitions and
            // heartbeats from a scanner thread that ignored cancellation must
            // not overwrite the terminal observation.
            return;
        }

        var normalizedCode = failureCode is null ? null : NormalizeCode(failureCode);
        var normalizedOrdinal = NormalizeOrdinal(ordinal);
        var scanProgressEvent = new ScanProgressEvent(
            ScanProgressSchema.Version,
            ++sequence,
            normalizedOperation,
            normalizedStage,
            normalizedState,
            ElapsedMillisecondsLocked(),
            NormalizeCounts(counts),
            normalizedCode,
            lastSuccessfulStage,
            normalizedOrdinal);
        if (normalizedState == "started")
        {
            // Nested stages stack: completing an inner stage restores the
            // enclosing stage so heartbeats continue through the outer work.
            activeStages.Add((normalizedStage, normalizedOperation, normalizedOrdinal));
        }
        else if (normalizedState is "completed" or "partial")
        {
            PopStage(normalizedStage);
            lastSuccessfulStage = normalizedStage;
        }
        else if (normalizedState is "failed" or "cancelled" or "timed-out")
        {
            // A terminal enclosing-stage outcome ends every nested stage above
            // it as well; the search removes the deepest match plus its nest.
            PopStage(normalizedStage);
        }

        if (normalizedState != "heartbeat")
        {
            history.Add(scanProgressEvent);
            if (history.Count > ScanProgressSchema.MaxHistoryEvents)
            {
                history.RemoveRange(0, history.Count - ScanProgressSchema.MaxHistoryEvents);
            }
        }

        else
        {
            performanceTracker?.RecordHeartbeat();
        }

        WriteConsoleBestEffort(FormatConsoleLine(scanProgressEvent));
        var performanceSummary = performanceTracker?.RecordProgress(normalizedStage, normalizedState);
        if (performanceSummary is not null)
        {
            WriteConsoleBestEffort(performanceSummary);
        }
        WriteCheckpointLocked(new ScanProgressCheckpoint(
            ScanProgressSchema.Version,
            scanProgressEvent,
            history.ToArray()));
    }

    private void PopStage(string stage)
    {
        for (var index = activeStages.Count - 1; index >= 0; index--)
        {
            if (activeStages[index].Stage == stage)
            {
                activeStages.RemoveRange(index, activeStages.Count - index);
                return;
            }
        }
    }

    private void WriteConsoleBestEffort(string line)
    {
        if (console is null)
        {
            return;
        }

        try
        {
            console.WriteLine(line);
        }
        catch (Exception exception) when (exception is IOException or ObjectDisposedException or InvalidOperationException)
        {
            // Diagnostics must never fail or alter the authoritative scan.
        }
    }

    /// <summary>Reports the innermost active stage, elapsed time, and last completed stage. Rate-limited by the heartbeat timer.</summary>
    public void EmitHeartbeat()
    {
        lock (gate)
        {
            if (disposed || terminalLatched || activeStages.Count == 0)
            {
                return;
            }

            var innermost = activeStages[^1];
            EmitUnderLock(innermost.Operation, innermost.Stage, "heartbeat", innermost.Ordinal, null, null);
        }
    }

    public ScanProgressCheckpoint? ReadCheckpoint()
    {
        lock (gate)
        {
            if (checkpointPath is null || !File.Exists(checkpointPath))
            {
                return null;
            }

            try
            {
                using var stream = File.OpenRead(checkpointPath);
                return JsonSerializer.Deserialize<ScanProgressCheckpoint>(stream, DocumentJsonOptions);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                return null;
            }
        }
    }

    public ScanPerformanceReceipt? ReadPerformanceReceipt() => performanceTracker?.ReadReceipt();

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
        }

        heartbeatTimer?.Dispose();
    }

    private long ElapsedMillisecondsLocked()
    {
        var frequency = timeProvider.TimestampFrequency;
        if (frequency <= 0)
        {
            return 0;
        }

        var elapsed = timeProvider.GetTimestamp() - startedAt;
        var deltaTicks = elapsed * (double)TimeSpan.TicksPerSecond / frequency;
        return Math.Max(0, (long)TimeSpan.FromTicks((long)deltaTicks).TotalMilliseconds);
    }

    private void WriteCheckpointLocked(ScanProgressCheckpoint checkpoint)
    {
        if (checkpointPath is null)
        {
            return;
        }

        try
        {
            var directory = Path.GetDirectoryName(checkpointPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var serialized = JsonSerializer.Serialize(checkpoint, DocumentJsonOptions);
            var temporary = checkpointPath + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                File.WriteAllText(temporary, serialized);
                File.Move(temporary, checkpointPath, overwrite: true);
            }
            finally
            {
                try
                {
                    if (File.Exists(temporary))
                    {
                        File.Delete(temporary);
                    }
                }
                catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
                {
                    // Best-effort cleanup of the abandoned temporary file only.
                }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // The scan itself remains authoritative; a failed diagnostic
            // checkpoint write must not fail the analysis.
        }
    }

    private static string FormatConsoleLine(ScanProgressEvent scanProgressEvent)
    {
        var builder = new System.Text.StringBuilder(160);
        builder.Append("tracemap-progress ")
            .Append(scanProgressEvent.SchemaVersion)
            .Append(" seq=").Append(scanProgressEvent.Sequence)
            .Append(" op=").Append(scanProgressEvent.Operation)
            .Append(" stage=").Append(scanProgressEvent.Stage);
        if (scanProgressEvent.Ordinal is not null)
        {
            builder.Append(" ordinal=").Append(scanProgressEvent.Ordinal.Value);
        }

        builder.Append(" state=").Append(scanProgressEvent.State)
            .Append(" elapsedMs=").Append(scanProgressEvent.ElapsedMilliseconds);
        if (scanProgressEvent.Counts is { Count: > 0 } counts)
        {
            foreach (var pair in counts.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                builder.Append(' ').Append(pair.Key).Append('=').Append(pair.Value);
            }
        }

        if (scanProgressEvent.FailureCode is not null)
        {
            builder.Append(" failureCode=").Append(scanProgressEvent.FailureCode);
        }

        if (scanProgressEvent.LastSuccessfulStage is not null)
        {
            builder.Append(" lastSuccessfulStage=").Append(scanProgressEvent.LastSuccessfulStage);
        }

        return builder.ToString();
    }

    private static bool IsKnownStage(string stage) =>
        stage is ScanProgressStages.ArgumentsValidated
            or ScanProgressStages.OutputAuthorized
            or ScanProgressStages.StagingInitialized
            or ScanProgressStages.Scan
            or ScanProgressStages.RepositoryIdentity
            or ScanProgressStages.Inventory
            or ScanProgressStages.SourceSnapshotCapture
            or ScanProgressStages.ProjectSelection
            or ScanProgressStages.MsBuildRegistration
            or ScanProgressStages.SolutionLoad
            or ScanProgressStages.ProjectLoad
            or ScanProgressStages.Compilation
            or ScanProgressStages.SyntaxFallback
            or ScanProgressStages.SpecializedExtraction
            or ScanProgressStages.SourceVerification
            or ScanProgressStages.ArtifactWrite
            or ScanProgressStages.ScanPublication
            or ScanProgressStages.WebFormsModernization
            or ScanProgressStages.Explorer
            or ScanProgressStages.LocalReviewPublication;

    private static IReadOnlyDictionary<string, long>? NormalizeCounts(IReadOnlyDictionary<string, long>? counts)
    {
        if (counts is null || counts.Count == 0)
        {
            return null;
        }

        var normalized = new SortedDictionary<string, long>(StringComparer.Ordinal);
        foreach (var pair in counts)
        {
            if (CountKeys.Contains(pair.Key) && pair.Value >= 0)
            {
                normalized[pair.Key] = pair.Value;
            }
        }

        return normalized.Count == 0 ? null : normalized;
    }

    private static int? NormalizeOrdinal(int? ordinal) =>
        ordinal is null ? null : Math.Clamp(ordinal.Value, 0, 1_000_000);

    private static string NormalizeAllowed(string value, IReadOnlySet<string> allowed, string fallback) =>
        allowed.Contains(value) ? value : fallback;

    /// <summary>
    /// Failure codes are a closed catalog. Anything outside the catalog —
    /// including a path, exception message, or other sensitive value passed by
    /// mistake — collapses to UNKNOWN so arbitrary text can never reach the
    /// console or the checkpoint through this channel.
    /// </summary>
    private static string NormalizeCode(string value)
    {
        var normalized = new string(value.Trim().ToUpperInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' ? character : '-')
            .ToArray()).Trim('-');
        return FailureCodes.Contains(normalized) ? normalized : "UNKNOWN";
    }
}

/// <summary>
/// Flows a reporter from the guided local-review command into the scan runner
/// without changing the scan-runner delegate contract or the persisted scan
/// argument surface.
/// </summary>
public static class ScanProgressAmbient
{
    private static readonly AsyncLocal<ScanProgressReporter?> CurrentReporter = new();

    public static ScanProgressReporter? Current
    {
        get => CurrentReporter.Value;
        set => CurrentReporter.Value = value;
    }
}
