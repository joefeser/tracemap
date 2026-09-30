using System.Text.Json;
using TraceMap.Reporting;

namespace TraceMap.Cli;

public sealed record WebFormsHandlerRequeryReceipt(string SchemaVersion, string RuleId, string Visibility,
    string ClaimLevel, string GeneratorSha256, string BoundedInputSha256, string ReportingGeneratorSha256,
    string RunId, string SourcePreflightSha256, string SourceCheckpointSha256, string RecoveryReceiptSha256,
    string CombinedIndexSha256, CombinedPathSymbolRoot Root, CombinedPathQuery Query,
    int ExactChains, int EvidenceVariants, bool Truncated, int? TraversalWorkUnits,
    IReadOnlyList<WebFormsReviewArtifact> Artifacts, IReadOnlyList<string> Limitations)
{
    public CombinedPathGraphObservation? GraphObservation { get; init; }
}

public static partial class WebFormsReviewExecutionCommand
{
    internal const string HandlerRequeryRule = "workflow.webforms.retained-handler-requery.v1";
    internal const string HandlerRequeryName = "handler-requery.local.json";

    /// <summary>New single-root report over a verified retained combined index; no scan or combine.</summary>
    public static async Task<int> RequeryHandlerAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        var failureStage = "arguments";
        try
        {
            if (args.Length is not (9 or 11 or 13) || args[0] != "requery-handler" || args[1] != "--run" ||
                args[3] != "--bundle" || args[5] != "--handler" || args[7] != "--out") throw Fail("HANDLER_REQUERY_ARGUMENT_INVALID");
            string? surfaceName = null;
            var compiledOnly = false;
            for (var i = 9; i < args.Length; i += 2)
            {
                if (args[i] == "--surface-name" && args[i + 1] == "DbDataAdapter.Fill" && surfaceName is null) surfaceName = args[i + 1];
                else if (args[i] == "--traversal-scope" && args[i + 1] == "compiled-il" && !compiledOnly) compiledOnly = true;
                else throw Fail("HANDLER_REQUERY_SURFACE_OR_SCOPE_INVALID");
            }
            var run = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var bundle = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            var handler = args[6];
            if (handler.Length is < 1 or > 128 || handler.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) throw Fail("HANDLER_REQUERY_ARGUMENT_INVALID");
            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[8]);
            if (Path.Exists(destination) || Within(destination, run) || Within(run, destination) ||
                Within(destination, bundle) || Within(bundle, destination)) throw Fail("HANDLER_REQUERY_OUTPUT_EXISTS_OR_OVERLAPS");
            using var runLock = new FileStream(OwnedPath(run, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.Read);
            failureStage = "original-context";
            var planBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(run, "run-manifest.json"), 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(planBytes);
            var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(planBytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
            var firstBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(run, "checkpoints/0001.json"), 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(firstBytes);
            var first = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(firstBytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID");
            var history = await ReadHistoryAsync(run, plan, Digest(planBytes), first.RuntimeInputsSha256, token);
            if (history.Checkpoint is not { State: ReportsFailed, Reports: { } context }) throw Fail("HANDLER_REQUERY_FAILED_REPORT_REQUIRED");
            foreach (var input in plan.Inputs.Select(item => item.Path).Concat(new[] { plan.Configuration.SourceRoot,
                         plan.Configuration.PublishedRoot, plan.Configuration.ReceiptRoot, plan.Configuration.ParentScanRoot }.OfType<string>()))
                if (Within(destination, input) || Within(input, destination)) throw Fail("HANDLER_REQUERY_OUTPUT_OVERLAPS_INPUT");

            var receiptPath = OwnedPath(bundle, RecoveryName);
            failureStage = "recovery-context";
            using var receiptLock = new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var receiptBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(receiptBytes);
            var recovery = JsonSerializer.Deserialize<WebFormsReportRecoveryReceipt>(receiptBytes, JsonOptions) ?? throw Fail("RECOVERY_RECEIPT_INVALID");
            if (recovery.RunId != plan.RunId || recovery.SourcePreflightSha256 != Digest(planBytes) ||
                recovery.SourceCheckpointSha256 != history.Sha256 || recovery.BoundedInputSha256 != RecoveryHash(recovery))
                throw Fail("HANDLER_REQUERY_RECOVERY_CONTEXT_INVALID");
            // Reuse the full recovery-index verification and bounded query response.
            using var rootsOutput = new StringWriter(); using var rootsError = new StringWriter();
            failureStage = "root-query";
            if (await QueryRecoveryAsync(["query-recovery", "--bundle", bundle, "--document", "compiled", "--pointer",
                    "/header/query/symbolRoots", "--limit", "50", "--depth", "2"], rootsOutput, rootsError, token) != 0)
                throw Fail("HANDLER_REQUERY_ROOT_QUERY_FAILED");
            var response = JsonSerializer.Deserialize<WebFormsEvidenceResponse>(rootsOutput.ToString(), QueryJson) ?? throw Fail("HANDLER_REQUERY_ROOT_QUERY_INVALID");
            if (response.Truncated || response.Result.OmittedChildren != 0) throw Fail("HANDLER_REQUERY_ROOT_QUERY_LIMIT");
            var roots = response.Result.Children.Select(item =>
            {
                var fields = item.Children.ToDictionary(child => child.Pointer.Split('/')[^1], child => child.Value?.GetString(), StringComparer.Ordinal);
                return new CombinedPathSymbolRoot(fields["sourceIndexId"]!, fields["scanId"]!, fields["commitSha"]!, fields["symbolId"]!);
            }).Where(root => root.SymbolId.Split('(', 2)[0].EndsWith("." + handler, StringComparison.Ordinal)).Distinct().ToArray();
            if (roots.Length != 1) throw Fail("HANDLER_REQUERY_ROOT_NOT_UNIQUE");
            await output.WriteLineAsync("handlerStage=root-verified;rootIdentities=1;no-scan;no-combine");
            var indexPath = OwnedPath(run, context.ReportAttempt + "/combined.sqlite");
            failureStage = "combined-index";
            RejectQuerySidecars(indexPath);
            using var indexLock = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var index = await WebFormsReviewPreflightCommand.HashAsync("combined-index", indexPath, plan.Configuration.Budgets.MaxRetainedArtifactBytes, token);
            if (index.Sha256 != recovery.SourceIndexSha256) throw Fail("HANDLER_REQUERY_INDEX_CHANGED");
            var config = plan.Configuration;
            var budget = config.Budgets.Reports ?? new();
            WebFormsReviewPreflightCommand.ValidateReportBudgets(budget);
            var options = new CombinedDependencyPathOptions(indexPath, destination, ToSurface: "database-api", SurfaceName: surfaceName, IncludeLegacyRoots: true,
                MaxDepth: config.Budgets.GraphMaxDepth, MaxPaths: config.Budgets.GraphMaxPaths, MaxFrontier: budget.MaxFrontier)
            { ExactFromSymbol = true, CompiledOnly = compiledOnly, MaxTraversalWork = checked((int)config.Budgets.GraphMaxWork) };
            await output.WriteLineAsync($"handlerStage=graph-started;maxPaths={options.MaxPaths};maxWork={options.MaxTraversalWork};single-root=true");
            await output.WriteLineAsync($"handlerQuery.terminalScope={(surfaceName is null ? "all-database-api" : "DbDataAdapter.Fill")};mixed-source-and-compiled={(compiledOnly ? "false" : "true")}");
            await output.WriteLineAsync($"handlerQuery.traversalScope={(compiledOnly ? "compiled-il-with-root-attachment" : "mixed")};root-attachment-not-il-proof=true");
            CombinedPathGraphObservation? observation = null;
            failureStage = "graph";
            var paths = await CombinedDependencyPathReporter.BuildSelectedSymbolsAsync(options, roots, true,
                new(budget.MaxInputFacts, budget.MaxInputEdges, budget.MaxInputTextBytes)
                { MaxGraphStorageBytes = budget.MaxGraphStorageBytes, GraphStageObserver = stage =>
                    { failureStage = stage; output.WriteLine("handlerGraphStage=" + stage); }, GraphObservationObserver = usage => observation = usage }, token);
            if (observation is not null)
            {
                foreach (var stage in observation.StageElapsedMilliseconds ?? new Dictionary<string, long>())
                    await output.WriteLineAsync($"handlerGraphUsage.stage={stage.Key};elapsedMs={stage.Value};factPayloadRows={observation.FactPayloadRowsByStage?.GetValueOrDefault(stage.Key) ?? 0}");
                await output.WriteLineAsync($"handlerGraphUsage.factPayloadRows={observation.FactPayloadRowsRead};logicalFactPayloadBytes={observation.FactPayloadBytesRead};not-physical-io=true");
            }
            if (index != await WebFormsReviewPreflightCommand.HashAsync("combined-index", indexPath, index.Bytes, token)) throw Fail("HANDLER_REQUERY_INDEX_CHANGED");
            RejectQuerySidecars(indexPath);
            var limits = new GroupedCompiledPathLimits(budget.MaxProjectionInputBytes, budget.MaxOutputBytes,
                options.MaxPaths, budget.MaxProjectionRecords, budget.MaxProjectionReferences);
            failureStage = "report-projection";
            var grouped = GroupedCompiledPathHandoffBuilder.Create(paths, index.Sha256, limits, token);
            await output.WriteLineAsync("handlerStage=reports-started;no-scan;no-combine");
            await GroupedCompiledPathReportWriter.WriteAsync(grouped, destination, limits, token);
            if (Digest(receiptBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token)) ||
                Digest(planBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(run, "run-manifest.json"), 4_194_304, token)) ||
                history.Sha256 != (await ReadHistoryAsync(run, plan, Digest(planBytes), first.RuntimeInputsSha256, token)).Sha256)
                throw Fail("HANDLER_REQUERY_CONTEXT_CHANGED");
            var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var artifacts = new List<WebFormsReviewArtifact>();
            foreach (var name in new[] { GroupedCompiledPathReportWriter.HtmlName, GroupedCompiledPathReportWriter.HandoffName })
            {
                var artifact = await WebFormsReviewPreflightCommand.HashAsync("report", Path.Combine(destination, name), budget.MaxOutputBytes, token);
                artifacts.Add(new(name, artifact.Bytes, artifact.Sha256));
            }
            var receipt = new WebFormsHandlerRequeryReceipt("webforms-handler-requery.v1", HandlerRequeryRule, "local-only",
                "single-handler-retained-static-review-not-parity", generator.Sha256, "", grouped.GeneratorSha256,
                plan.RunId, Digest(planBytes), history.Sha256!, Digest(receiptBytes), index.Sha256, roots[0], paths.Query,
                grouped.Chains.Count, grouped.Variants.Count, paths.Summary.Truncated, paths.Summary.TraversalWorkUnits, artifacts,
                ["One exact source/scan/commit/symbol root was selected from the verified recovered report index.",
                 "All admitted global competitors remain present; this requery repeats graph construction and traversal, not source scanning or combining.",
                 "Independent single-handler path/work bounds are not complete coverage, historical parity, runtime SQL or authenticated-build proof.",
                 "Original failed checkpoints and recovery bundle remain unchanged. All identities and paths are private.",
                 "Graph observations count logical scratch fact payload reads and stage wall time, not physical disk I/O or runtime calls."])
                { GraphObservation = observation };
            receipt = receipt with { BoundedInputSha256 = HandlerRequeryHash(receipt) };
            failureStage = "receipt";
            await File.WriteAllTextAsync(Path.Combine(destination, HandlerRequeryName), JsonSerializer.Serialize(receipt, JsonOptions), token);
            await output.WriteLineAsync($"handlerRequery.selectorCandidates={paths.Summary.SelectorCandidateCount}");
            var safeGaps = paths.Gaps.Select(gap => (gap.GapKind, gap.Reason)).Distinct().Take(20).ToArray();
            static string SafeCode(string? value) => value is { Length: > 0 and <= 96 } &&
                value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.') ? value : "none-or-redacted";
            foreach (var gap in safeGaps)
                await output.WriteLineAsync($"handlerGap.kind={SafeCode(gap.GapKind)};reason={SafeCode(gap.Reason)}");
            await output.WriteLineAsync($"handlerRequery=completed-separate-report;exactChains={grouped.Chains.Count};evidenceVariants={grouped.Variants.Count};truncated={paths.Summary.Truncated};workUnits={paths.Summary.TraversalWorkUnits};no-scan;no-combine;originals-preserved");
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var code = exception.Message.StartsWith("WEBFORMS_", StringComparison.Ordinal) && exception.Message.Length <= 160 &&
                exception.Message.All(c => c is >= 'A' and <= 'Z' or '_') ? exception.Message : "WEBFORMS_HANDLER_REQUERY_INPUT_OR_OUTPUT_INVALID";
            await error.WriteLineAsync($"error: {code};stage={failureStage};originals-preserved;partial-output-preserved"); return 1;
        }
    }

    internal static string HandlerRequeryHash(WebFormsHandlerRequeryReceipt receipt) =>
        WebFormsReviewReportExecution.CanonicalHash(receipt with { BoundedInputSha256 = "" }, 4_194_304, CancellationToken.None);
}
