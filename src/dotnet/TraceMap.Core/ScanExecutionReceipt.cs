using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace TraceMap.Core;

public static class ScanReceiptSchema
{
    public const string Version = "scan-execution-receipt.v1";
    public const int MaxStages = 32;
    public const int MaxSupportingIds = 256;
}

public sealed record ScanStageReceipt(
    string StageId,
    string Stage,
    string OperationCode,
    int Attempt,
    long DurationMilliseconds,
    string Outcome,
    string CoverageBefore,
    string CoverageAfter,
    string LastProvenSafeState,
    string MutationState,
    string CleanupResult,
    string Retryability,
    string NextAction,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingGapIds,
    IReadOnlyList<string> Limitations);

public sealed record ScanExecutionReceipt(
    string SchemaVersion,
    string ReceiptId,
    string RuleId,
    string EvidenceClass,
    string RunId,
    string TraceId,
    string ScannerVersion,
    IReadOnlyList<string> ExtractorVersions,
    string RepositoryIdentityHash,
    string CommitSha,
    string AuthorizedScopeFingerprint,
    string? SourceSnapshotDigest,
    string Outcome,
    string Coverage,
    IReadOnlyList<ScanStageReceipt> Stages,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingGapIds,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Collects bounded, sanitized operational observations. Receipts describe the
/// scanner run; they are not static code evidence and do not establish root cause.
/// </summary>
public sealed class ScanReceiptRecorder
{
    private static readonly HashSet<string> Stages = new(StringComparer.Ordinal)
    {
        "discovery", "semantic-analysis", "static-extraction", "source-verification", "artifact-write"
    };
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "inventory-and-identity", "compiler-and-syntax-analysis", "deterministic-fact-extraction",
        "pre-extraction-snapshot-verification", "post-extraction-snapshot-verification", "manifest-write", "facts-write", "index-write",
        "output-directory-prepare", "report-write", "analyzer-log-write", "webforms-static-extraction"
    };
    private static readonly HashSet<string> MutationStates = new(StringComparer.Ordinal)
    {
        "none-observed", "occurred", "unknown"
    };
    private static readonly HashSet<string> CleanupResults = new(StringComparer.Ordinal)
    {
        "not-required", "completed", "not-attempted", "failed", "unknown"
    };
    private static readonly HashSet<string> Retryabilities = new(StringComparer.Ordinal)
    {
        "not-required", "retry-after-dependency-restoration", "retry-after-input-correction",
        "retry-after-owner-review", "retry-after-correction", "unknown"
    };
    private static readonly HashSet<string> SafeStates = new(StringComparer.Ordinal)
    {
        "stage-started", "inventory-and-commit-observed", "semantic-input-snapshot-verified",
        "syntax-fallback-completed", "facts-created", "source-snapshot-verified", "manifest-written",
        "output-directory-prepared", "facts-written", "index-written", "report-written", "analyzer-log-written", "unknown"
    };
    private static readonly string[] ReceiptLimitations =
    [
        "The receipt records observed scanner stages; it does not prove root cause or operator fault.",
        "Successful stages do not prove application correctness, runtime reachability, or complete repository coverage.",
        "Durations are local monotonic observations and are not deterministic evidence identifiers."
    ];

    private readonly object gate = new();
    private readonly List<ScanStageReceipt> stages = [];
    private readonly string scopeFingerprint;
    private string? repositoryIdentityHash;
    private string? commitSha;
    private string? runId;
    private string? sourceSnapshotDigest;
    private string coverage = "unknown";
    private string outcome = "failed";
    private IReadOnlyList<string> extractorVersions = [ScannerVersions.TraceMap];

    public ScanReceiptRecorder(ScanOptions options, IEnumerable<string>? additionalAuthorizedInputs = null)
    {
        scopeFingerprint = Hash(string.Join('\n',
            Normalize(options.SolutionPaths),
            Normalize(options.ProjectPaths),
            Normalize(options.IncludeGlobs),
            Normalize(options.ExcludeGlobs),
            options.TargetFramework?.Trim() ?? string.Empty,
            options.Restore ? "restore" : "no-restore",
            Normalize(options.BinlogPaths),
            options.BinlogCommitSha?.Trim() ?? string.Empty,
            Normalize(additionalAuthorizedInputs)));
    }

    public bool CanWriteAuthoritativeReceipt => repositoryIdentityHash is not null && commitSha is not null && runId is not null;

    public void Bind(GitMetadata git)
    {
        if (!IsCommitSha(git.CommitSha))
            return;
        var identity = string.IsNullOrWhiteSpace(git.RemoteUrl) ? git.RepoName : git.RemoteUrl;
        repositoryIdentityHash = Hash(identity ?? "unknown");
        commitSha = git.CommitSha;
        runId ??= "run-" + Hash($"{repositoryIdentityHash}|{commitSha}|{scopeFingerprint}")[..20];
    }

    public void Bind(ScanResult result)
    {
        Bind(result.Manifest);
        extractorVersions = result.Facts
            .Select(fact => fact.Evidence?.ExtractorVersion)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Append(ScannerVersions.TraceMap)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .Take(64)
            .ToArray();
        var factIds = result.Facts.Select(fact => fact.FactId);
        var gapIds = result.Facts.Where(fact => fact.FactType == FactTypes.AnalysisGap).Select(fact => fact.FactId);
        Complete(result.Manifest.BuildStatus == "FailedOrPartial" ? "partial" : "succeeded", result.Manifest.AnalysisLevel, factIds, gapIds);
    }

    public void Bind(ScanManifest manifest)
    {
        if (!IsCommitSha(manifest.CommitSha))
            return;
        repositoryIdentityHash ??= Hash(string.IsNullOrWhiteSpace(manifest.RemoteUrl) ? manifest.RepoName : manifest.RemoteUrl);
        commitSha = manifest.CommitSha;
        runId = manifest.ScanId;
        sourceSnapshotDigest = manifest.SourceSnapshotDigest;
        coverage = NormalizeCoverage(manifest.AnalysisLevel);
    }

    public ScanReceiptOperation StartStage(
        string stage,
        string operationCode,
        string coverageBefore = "unchanged",
        string mutationState = "none-observed",
        string cleanupResult = "not-required") =>
        new(
            this,
            NormalizeAllowed(stage, Stages, "other"),
            NormalizeAllowed(operationCode, Operations, "other"),
            NormalizeCoverage(coverageBefore),
            NormalizeAllowed(mutationState, MutationStates, "unknown"),
            NormalizeAllowed(cleanupResult, CleanupResults, "unknown"));

    public void Complete(
        string finalOutcome,
        string finalCoverage,
        IEnumerable<string>? supportingFactIds = null,
        IEnumerable<string>? supportingGapIds = null)
    {
        outcome = NormalizeOutcome(finalOutcome);
        coverage = NormalizeCoverage(finalCoverage);
        finalFactIds = Bound(supportingFactIds, IsSafeFactId);
        finalGapIds = Bound(supportingGapIds, IsSafeGapId);
    }

    public void MarkPartial(string finalCoverage, IEnumerable<string>? supportingGapIds = null)
    {
        if (outcome == "succeeded")
            outcome = "partial";
        coverage = NormalizeCoverage(finalCoverage);
        finalGapIds = Bound(finalGapIds.Concat(supportingGapIds ?? []), IsSafeGapId);
    }

    private IReadOnlyList<string> finalFactIds = [];
    private IReadOnlyList<string> finalGapIds = [];

    public ScanExecutionReceipt CreateReceipt()
    {
        if (!CanWriteAuthoritativeReceipt)
            throw new InvalidOperationException("ScanReceiptIdentityUnavailable");

        ScanStageReceipt[] ordered;
        lock (gate)
            ordered = stages.Take(ScanReceiptSchema.MaxStages).ToArray();

        var receiptId = "receipt-" + Hash($"{runId}|{commitSha}|{scopeFingerprint}|{outcome}|{string.Join('|', ordered.Select(stage => stage.StageId))}")[..20];
        return new ScanExecutionReceipt(
            ScanReceiptSchema.Version,
            receiptId,
            RuleIds.ScannerStageReceipt,
            "operational-diagnostic",
            runId!,
            runId!,
            ScannerVersions.TraceMap,
            extractorVersions,
            repositoryIdentityHash!,
            commitSha!,
            scopeFingerprint,
            sourceSnapshotDigest,
            outcome,
            coverage,
            ordered,
            finalFactIds,
            finalGapIds,
            ReceiptLimitations);
    }

    internal void Record(
        string stage,
        string operationCode,
        long durationMilliseconds,
        string stageOutcome,
        string coverageBefore,
        string coverageAfter,
        string lastProvenSafeState,
        string mutationState,
        string cleanupResult,
        string retryability,
        string nextAction,
        IEnumerable<string>? supportingFactIds,
        IEnumerable<string>? supportingGapIds)
    {
        var facts = Bound(supportingFactIds, IsSafeFactId);
        var gaps = Bound(supportingGapIds, IsSafeGapId);
        var normalizedOutcome = NormalizeOutcome(stageOutcome);
        var normalizedCoverageBefore = NormalizeCoverage(coverageBefore);
        var normalizedCoverageAfter = NormalizeCoverage(coverageAfter);
        var normalizedSafeState = NormalizeAllowed(lastProvenSafeState, SafeStates, "unknown");
        var normalizedMutationState = NormalizeAllowed(mutationState, MutationStates, "unknown");
        var normalizedCleanupResult = NormalizeAllowed(cleanupResult, CleanupResults, "unknown");
        var normalizedRetryability = NormalizeAllowed(retryability, Retryabilities, "unknown");
        var normalizedNextAction = NormalizeCode(nextAction);
        var stageId = "stage-" + Hash(string.Join('|',
            stage,
            operationCode,
            "1",
            normalizedOutcome,
            normalizedCoverageBefore,
            normalizedCoverageAfter,
            normalizedSafeState,
            normalizedMutationState,
            normalizedCleanupResult,
            normalizedRetryability,
            normalizedNextAction,
            string.Join('|', facts),
            string.Join('|', gaps)))[..20];
        var receipt = new ScanStageReceipt(
            stageId,
            stage,
            operationCode,
            1,
            Math.Max(0, durationMilliseconds),
            normalizedOutcome,
            normalizedCoverageBefore,
            normalizedCoverageAfter,
            normalizedSafeState,
            normalizedMutationState,
            normalizedCleanupResult,
            normalizedRetryability,
            normalizedNextAction,
            facts,
            gaps,
            []);
        lock (gate)
        {
            if (stages.Count < ScanReceiptSchema.MaxStages)
                stages.Add(receipt);
        }
    }

    public static string ClassifyFailure(Exception exception) => exception switch
    {
        OperationCanceledException => "operation-cancelled",
        TimeoutException => "operation-timed-out",
        SourceInventoryException => SourceInventoryException.ErrorCode,
        DirectoryNotFoundException => "input-unavailable",
        UnauthorizedAccessException => "input-unreadable",
        SourceSnapshotException => SourceSnapshotException.ErrorCode,
        _ => "operation-failed"
    };

    public static string ClassifyOutputFailure(Exception exception) => exception switch
    {
        OperationCanceledException => "operation-cancelled",
        TimeoutException => "operation-timed-out",
        UnauthorizedAccessException or DirectoryNotFoundException or IOException => "output-artifact-write-failed",
        ArgumentException or NotSupportedException => "output-path-invalid",
        _ => "output-artifact-write-failed"
    };

    private static IReadOnlyList<string> Bound(IEnumerable<string>? values, Func<string, bool> isSafe)
    {
        var bounded = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var candidate in values ?? [])
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;
            var value = candidate.Trim();
            if (!isSafe(value) || !bounded.Add(value))
                continue;
            if (bounded.Count > ScanReceiptSchema.MaxSupportingIds)
                bounded.Remove(bounded.Max!);
        }
        return bounded.ToArray();
    }

    private static bool IsSafeFactId(string value)
    {
        if (value.Length != 25 || !value.StartsWith("fact-", StringComparison.Ordinal))
            return false;
        for (var index = 5; index < value.Length; index++)
        {
            if (value[index] is not ((>= '0' and <= '9') or (>= 'a' and <= 'f')))
                return false;
        }
        return true;
    }

    private static bool IsSafeGapId(string value)
    {
        if (IsSafeFactId(value))
            return true;
        return value.Length == 24 && value.All(character => character is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
    }

    private static string Normalize(IEnumerable<string>? values) => string.Join('\n',
        (values ?? []).Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().Replace('\\', '/')).OrderBy(value => value, StringComparer.Ordinal));

    private static string NormalizeCode(string? value)
    {
        var normalized = new string((value ?? "unknown").Trim().ToLowerInvariant()
            .Select(character => char.IsAsciiLetterOrDigit(character) || character == '-' ? character : '-')
            .ToArray()).Trim('-');
        return string.IsNullOrEmpty(normalized) ? "unknown" : normalized[..Math.Min(normalized.Length, 80)];
    }

    private static string NormalizeAllowed(string? value, IReadOnlySet<string> allowed, string fallback)
    {
        var normalized = NormalizeCode(value);
        return allowed.Contains(normalized) ? normalized : fallback;
    }

    private static string NormalizeOutcome(string value) => NormalizeCode(value) switch
    {
        "succeeded" => "succeeded",
        "partial" => "partial",
        "failed" => "failed",
        "cancelled" => "cancelled",
        "timed-out" => "timed-out",
        _ => "failed"
    };

    private static string NormalizeCoverage(string? value) => value switch
    {
        "Level1SemanticAnalysis" => "semantic",
        "Level1SemanticAnalysisReduced" => "semantic-reduced",
        "Level3SyntaxAnalysis" => "syntax",
        "Level3SyntaxAnalysisReduced" => "syntax-reduced",
        "semantic" or "semantic-reduced" or "syntax" or "syntax-reduced" or "unknown" or "unchanged" => value,
        _ => "unknown"
    };

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static bool IsCommitSha(string? value) => value is not null
        && value.Length is 40 or 64
        && value.All(character => char.IsAsciiHexDigit(character));
}

public sealed class ScanReceiptOperation : IDisposable
{
    private readonly ScanReceiptRecorder recorder;
    private readonly string stage;
    private readonly string operationCode;
    private readonly string coverageBefore;
    private readonly string mutationState;
    private readonly string cleanupResult;
    private readonly long startedAt = Stopwatch.GetTimestamp();
    private int completed;

    internal ScanReceiptOperation(ScanReceiptRecorder recorder, string stage, string operationCode, string coverageBefore, string mutationState, string cleanupResult)
    {
        this.recorder = recorder;
        this.stage = stage;
        this.operationCode = operationCode;
        this.coverageBefore = coverageBefore;
        this.mutationState = mutationState;
        this.cleanupResult = cleanupResult;
    }

    public void Complete(
        string outcome,
        string coverageAfter,
        string lastProvenSafeState,
        string retryability = "not-required",
        string nextAction = "continue",
        IEnumerable<string>? supportingFactIds = null,
        IEnumerable<string>? supportingGapIds = null) =>
        Record(outcome, coverageAfter, lastProvenSafeState, mutationState, cleanupResult, retryability, nextAction, supportingFactIds, supportingGapIds);

    public void Fail(Exception exception, string lastProvenSafeState, string cleanup = "not-attempted") =>
        Fail(exception, lastProvenSafeState, cleanup, ScanReceiptRecorder.ClassifyFailure(exception));

    public void FailOutput(Exception exception, string lastProvenSafeState, string cleanup = "not-attempted") =>
        Fail(exception, lastProvenSafeState, cleanup, ScanReceiptRecorder.ClassifyOutputFailure(exception));

    private void Fail(Exception exception, string lastProvenSafeState, string cleanup, string failureCode) =>
        Record(
            exception is OperationCanceledException ? "cancelled" : exception is TimeoutException ? "timed-out" : "failed",
            "unknown",
            lastProvenSafeState,
            "unknown",
            cleanup,
            exception is OperationCanceledException ? "retry-after-owner-review" : "retry-after-correction",
            failureCode);

    private void Record(
        string outcome,
        string coverageAfter,
        string lastProvenSafeState,
        string mutation,
        string cleanup,
        string retryability,
        string nextAction,
        IEnumerable<string>? supportingFactIds = null,
        IEnumerable<string>? supportingGapIds = null)
    {
        if (Interlocked.Exchange(ref completed, 1) != 0)
            return;
        recorder.Record(stage, operationCode, (long)Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds, outcome, coverageBefore, coverageAfter, lastProvenSafeState, mutation, cleanup, retryability, nextAction, supportingFactIds, supportingGapIds);
    }

    public void Dispose()
    {
        if (Volatile.Read(ref completed) == 0)
            Record("failed", "unknown", "stage-started", "unknown", "unknown", "retry-after-correction", "operation-failed");
    }
}
