using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

public static partial class WebFormsReviewExecutionCommand
{
    private const string ReportsStarted = "reports-started";
    private const string ReportsFailed = "reports-failed";
    private const string ReportsCancelled = "reports-cancelled";

    private static string ReportPolicyDigest(WebFormsReviewPreflightManifest plan, string scan, string report) =>
        Digest(JsonSerializer.SerializeToUtf8Bytes(WebFormsReviewReportExecution.Policy(plan, scan, report), JsonOptions));
    private static string ScanArtifactsDigest(WebFormsReviewCheckpoint scan) =>
        Digest(JsonSerializer.SerializeToUtf8Bytes(scan.Artifacts, JsonOptions));
    private static string ReportBoundedDigest(string preflight, string runtime, WebFormsReviewCheckpoint checkpoint) =>
        Digest(JsonSerializer.SerializeToUtf8Bytes(new
        {
            preflightSha256 = preflight, runtimeInputsSha256 = runtime,
            policySha256 = checkpoint.Reports!.PolicySha256, sourceSnapshotDigest = checkpoint.SourceSnapshotDigest,
            artifacts = checkpoint.Artifacts, scanCheckpointSha256 = checkpoint.Reports.ScanCheckpointSha256,
            scanArtifactsSha256 = checkpoint.Reports.ScanArtifactsSha256, reportAttempt = checkpoint.Reports.ReportAttempt
        }, JsonOptions));

    private static void ValidateReportCheckpoint(string root, WebFormsReviewPreflightManifest plan, string preflight,
        string runtime, History history, WebFormsReviewCheckpoint checkpoint, HashSet<string> attempts)
    {
        var scan = history.ScanCheckpoint ?? throw Fail("REPORT_SCAN_ANCHOR_UNAVAILABLE");
        var context = checkpoint.Reports ?? throw Fail("REPORT_CONTEXT_INVALID");
        if (checkpoint.State is not (ReportsStarted or ReportsFailed or ReportsCancelled or WebFormsReviewReportExecution.Completed) ||
            !ValidReportAttempt(context.ReportAttempt) || checkpoint.Attempt != scan.Attempt || checkpoint.ScanId != scan.ScanId ||
            checkpoint.SourceSnapshotDigest != scan.SourceSnapshotDigest || checkpoint.FactCount != scan.FactCount ||
            context.ScanCheckpointSha256 != history.ScanCheckpointSha256 || context.ScanArtifactsSha256 != ScanArtifactsDigest(scan) ||
            context.PolicySha256 != ReportPolicyDigest(plan, LexicalOwnedPath(root, scan.Attempt + "/scan"), LexicalOwnedPath(root, context.ReportAttempt)) ||
            checkpoint.BoundedInputSha256 != ReportBoundedDigest(preflight, runtime, checkpoint)) throw Fail("REPORT_CONTEXT_INVALID");
        if (checkpoint.State == ReportsStarted)
        {
            if (!attempts.Add(context.ReportAttempt) || history.Checkpoint?.State is not
                (Completed or ReportsStarted or ReportsFailed or ReportsCancelled)) throw Fail("REPORT_ATTEMPT_REUSE_OR_PHASE_INVALID");
        }
        else if (history.Checkpoint?.State != ReportsStarted || history.Checkpoint.Reports?.ReportAttempt != context.ReportAttempt)
            throw Fail("REPORT_PHASE_INVALID");
        if (checkpoint.State != WebFormsReviewReportExecution.Completed)
        {
            if (!checkpoint.Artifacts.SequenceEqual(scan.Artifacts) || context.Coverage is not null || context.Surfaces != 0 ||
                context.CompiledPaths != 0 || context.ResultBoundedInputSha256 is not null) throw Fail("REPORT_CONTEXT_INVALID");
            return;
        }
        if (context.Coverage is not ("partial-static-review" or "retained-static-review") || context.Surfaces < 0 ||
            context.CompiledPaths < 0 || !ValidSha(context.ResultBoundedInputSha256)) throw Fail("REPORT_RESULT_INVALID");
        var retained = checkpoint.Artifacts.Where(item => item.RelativePath.StartsWith(scan.Attempt + "/scan/", StringComparison.Ordinal)).ToArray();
        var generated = checkpoint.Artifacts.Where(item => item.RelativePath.StartsWith(context.ReportAttempt + "/", StringComparison.Ordinal)).ToArray();
        if (!retained.SequenceEqual(scan.Artifacts) || retained.Length + generated.Length != checkpoint.Artifacts.Count ||
            !checkpoint.Artifacts.SequenceEqual(checkpoint.Artifacts.OrderBy(item => item.RelativePath, StringComparer.Ordinal)) ||
            checkpoint.Artifacts.Select(item => item.RelativePath).Distinct(StringComparer.Ordinal).Count() != checkpoint.Artifacts.Count ||
            checkpoint.Artifacts.Any(item => item.Bytes < 0 || !ValidSha(item.Sha256))) throw Fail("REPORT_ARTIFACTS_INVALID");
        RequireReports(generated, context.ReportAttempt);
    }

    private static async Task<int> ExecuteReportsAsync(string root, WebFormsReviewPreflightManifest plan, string preflightSha,
        string runtimeRoot, string runtimeSha, History history, WebFormsReviewReportRunner runner, TextWriter output, CancellationToken token)
    {
        var scan = history.ScanCheckpoint ?? throw Fail("REPORT_SCAN_ANCHOR_UNAVAILABLE");
        if (history.Checkpoint?.State == WebFormsReviewReportExecution.Completed)
        {
            await VerifyArtifactsAsync(root, history.Checkpoint, plan, token);
            await PrintAsync(history.Checkpoint);
            return 0;
        }
        if (history.Sequence >= 254) throw Fail("CHECKPOINT_COUNT_LIMIT");
        await VerifyArtifactsAsync(root, scan, plan, token);
        var before = GitMetadataProvider.Detect(plan.Configuration.SourceRoot);
        var attempt = "reports/" + Guid.NewGuid().ToString("N");
        var scanPath = OwnedPath(root, scan.Attempt + "/scan");
        var reportPath = OwnedPath(root, attempt);
        var context = new WebFormsReviewReportCheckpointContext(attempt, history.ScanCheckpointSha256!, ScanArtifactsDigest(scan),
            ReportPolicyDigest(plan, scanPath, reportPath), null, 0, 0, null);
        history = await PublishAsync(root, Create(ReportsStarted, scan.Artifacts, scan.Gaps, context), token, history);
        using var observation = new WebFormsReviewPhaseObservation("reports");
        try
        {
            var result = await runner(plan, scanPath, reportPath, token);
            var artifacts = await CollectUnderAsync(root, attempt, plan.Configuration.Budgets, token);
            var expected = result.GeneratedArtifacts.Select(item => item with { RelativePath = attempt + "/" + item.RelativePath })
                .OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
            if (!artifacts.SequenceEqual(expected)) throw Fail("REPORT_OUTPUT_CHANGED");
            RequireReports(artifacts, attempt);
            if (result.Coverage is not ("partial-static-review" or "retained-static-review") || result.Surfaces < 0 ||
                result.CompiledPaths < 0 || !ValidSha(result.BoundedInputSha256)) throw Fail("REPORT_RESULT_INVALID");
            await VerifyArtifactsAsync(root, scan, plan, token);
            await WebFormsReviewInputValidation.ValidateAsync(plan, token);
            await WebFormsReviewInputValidation.RecheckAsync(plan, token);
            var after = GitMetadataProvider.Detect(plan.Configuration.SourceRoot);
            if (before.CommitSha != after.CommitSha || before.GitRootPath != after.GitRootPath || before.RemoteUrl != after.RemoteUrl ||
                before.ScanRootRelativePath != after.ScanRootRelativePath || after.CommitSha != plan.Configuration.SourceCommitSha)
                throw Fail("SOURCE_IDENTITY_CHANGED");
            if (runtimeSha != await RuntimeDigestAsync(runtimeRoot, token)) throw Fail("RUNTIME_CHANGED");
            var all = scan.Artifacts.Concat(artifacts).OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
            var completed = Create(WebFormsReviewReportExecution.Completed, all,
                scan.Gaps.Where(gap => gap is not ("UnifiedReportsPending" or "CrossIndexParentJoinsPending"))
                    .Concat(result.Gaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(),
                context with { Coverage = result.Coverage, Surfaces = result.Surfaces, CompiledPaths = result.CompiledPaths,
                    ResultBoundedInputSha256 = result.BoundedInputSha256 });
            await VerifyArtifactsAsync(root, completed, plan, token);
            completed = completed with { PhaseUsage = observation.Finish() };
            completed = completed with { CheckpointPayloadSha256 = PayloadDigest(completed) };
            history = await PublishAsync(root, completed, token, history);
            await PrintAsync(completed);
            return 0;
        }
        catch (Exception exception)
        {
            if (history.Checkpoint?.State == WebFormsReviewReportExecution.Completed) throw;
            var cancelled = exception is OperationCanceledException || token.IsCancellationRequested;
            history = await PublishAsync(root, Create(cancelled ? ReportsCancelled : ReportsFailed, scan.Artifacts,
                cancelled ? ["Cancelled"] : SafeReportFailure(exception) is { } category
                    ? ["ReportOrArtifactValidationFailed", category] : ["ReportOrArtifactValidationFailed"], context, observation.Finish()), CancellationToken.None, history);
            if (cancelled && exception is not OperationCanceledException)
                throw new OperationCanceledException("Native report execution cancelled.", exception, token);
            throw;
        }

        WebFormsReviewCheckpoint Create(string state, IReadOnlyList<WebFormsReviewArtifact> artifacts,
            IReadOnlyList<string> gaps, WebFormsReviewReportCheckpointContext reportContext, WebFormsReviewPhaseUsage? usage = null)
        {
            var value = new WebFormsReviewCheckpoint(Schema, RuleId, "local-only", "review-only-static-not-runtime", plan.RunId,
                history.Sequence + 1, history.Sha256, plan.GeneratorSha256, preflightSha, "", state, scan.Attempt,
                scan.ScanId, scan.SourceSnapshotDigest, scan.FactCount, gaps, artifacts, "", runtimeSha)
                { Reports = reportContext, PhaseUsage = usage };
            value = value with { BoundedInputSha256 = ReportBoundedDigest(preflightSha, runtimeSha, value) };
            return value with { CheckpointPayloadSha256 = PayloadDigest(value) };
        }
        async Task PrintAsync(WebFormsReviewCheckpoint checkpoint)
        {
            await output.WriteLineAsync($"webFormsExecution={WebFormsReviewReportExecution.Completed};reviewOnly=true;sourceRescanned=false");
            await output.WriteLineAsync($"webFormsWorkbench={OwnedPath(root, checkpoint.Reports!.ReportAttempt + "/index.html")}");
            await output.WriteLineAsync($"webFormsHandoff={OwnedPath(root, checkpoint.Reports.ReportAttempt + "/" + WebFormsReviewReportExecution.HandoffName)}");
        }
    }

    private static void RequireReports(IReadOnlyList<WebFormsReviewArtifact> artifacts, string attempt)
    {
        foreach (var relative in new[] { "combined.sqlite", "index.html", WebFormsReviewReportExecution.HandoffName,
                     "compiled/compiled-paths.local.html", "compiled/compiled-paths.handoff.local.json" })
            if (!artifacts.Any(item => item.RelativePath == attempt + "/" + relative)) throw Fail("REPORT_ARTIFACT_MISSING");
    }
    private static bool ValidReportAttempt(string value) => value is not null && value.StartsWith("reports/", StringComparison.Ordinal)
        && value.Length == 40 && Guid.TryParseExact(value[8..], "N", out _);
    private static bool ValidSha(string? value) => value?.Length == 64 && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
