using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

public sealed record WebFormsHandlerRequeryReceipt(string SchemaVersion, string RuleId, string Visibility,
    string ClaimLevel, string GeneratorSha256, string BoundedInputSha256, string ReportingGeneratorSha256,
    string RunId, string SourcePreflightSha256, string SourceCheckpointSha256, string? RecoveryReceiptSha256,
    string CombinedIndexSha256, CombinedPathSymbolRoot Root, CombinedPathQuery Query,
    int ExactChains, int EvidenceVariants, bool Truncated, int? TraversalWorkUnits,
    IReadOnlyList<WebFormsReviewArtifact> Artifacts, IReadOnlyList<string> Limitations)
{
    public CombinedPathGraphObservation? GraphObservation { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? CompletedReportSha256 { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? PrimaryReport { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public bool? PathEnumerationPerformed { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? RootSelectionKind { get; init; }
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
            if (args.Length < 9 || args.Length > 17 || args.Length % 2 != 1 || args[0] != "requery-handler" || args[1] != "--run" ||
                args[3] != "--bundle" || args[5] is not ("--handler" or "--compiled-root") || args[7] != "--out") throw Fail("HANDLER_REQUERY_ARGUMENT_INVALID");
            string? surfaceName = null;
            var compiledOnly = false;
            var methodGraph = false;
            int? maxDepth = null;
            for (var i = 9; i < args.Length; i += 2)
            {
                if (args[i] == "--surface-name" && args[i + 1] == "DbDataAdapter.Fill" && surfaceName is null) surfaceName = args[i + 1];
                else if (args[i] == "--traversal-scope" && args[i + 1] == "compiled-il" && !compiledOnly) compiledOnly = true;
                else if (args[i] == "--view" && args[i + 1] == "method-graph" && !methodGraph) methodGraph = true;
                else if (args[i] == "--max-depth" && maxDepth is null &&
                    int.TryParse(args[i + 1], System.Globalization.NumberStyles.None,
                        System.Globalization.CultureInfo.InvariantCulture, out var depth) && depth is >= 1 and <= 64) maxDepth = depth;
                else throw Fail("HANDLER_REQUERY_SURFACE_OR_SCOPE_INVALID");
            }
            var run = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            if (methodGraph && (compiledOnly || surfaceName is not null)) throw Fail("HANDLER_REQUERY_SURFACE_OR_SCOPE_INVALID");
            var bundle = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            var handler = args[6];
            CombinedPathSymbolRoot? explicitRoot = null;
            if (args[5] == "--compiled-root")
            {
                var rootBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(
                    WebFormsReviewPreflightCommand.PhysicalPath(handler), 16_384, token);
                WebFormsReviewPreflightCommand.RejectDuplicateProperties(rootBytes);
                explicitRoot = JsonSerializer.Deserialize<CombinedPathSymbolRoot>(rootBytes, JsonOptions);
                if (explicitRoot is null || new[] { explicitRoot.SourceIndexId, explicitRoot.ScanId, explicitRoot.CommitSha, explicitRoot.SymbolId }
                    .Any(value => string.IsNullOrWhiteSpace(value)) || explicitRoot.SymbolId.Length > 8192)
                    throw Fail("COMPILED_ROOT_INVALID");
                if (!methodGraph) compiledOnly = true;
            }
            else if (handler.Length is < 1 or > 128 || handler.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) throw Fail("HANDLER_REQUERY_ARGUMENT_INVALID");
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
            if (history.Checkpoint is not { Reports: { } context } checkpoint ||
                checkpoint.State is not (ReportsFailed or WebFormsReviewReportExecution.Completed))
                throw Fail("HANDLER_REQUERY_FAILED_OR_COMPLETED_REPORT_REQUIRED");
            var completed = checkpoint.State == WebFormsReviewReportExecution.Completed;
            foreach (var input in plan.Inputs.Select(item => item.Path).Concat(new[] { plan.Configuration.SourceRoot,
                         plan.Configuration.PublishedRoot, plan.Configuration.ReceiptRoot, plan.Configuration.ParentScanRoot }.OfType<string>()))
                if (Within(destination, input) || Within(input, destination)) throw Fail("HANDLER_REQUERY_OUTPUT_OVERLAPS_INPUT");

            var receiptPath = completed
                ? OwnedPath(run, $"checkpoints/{history.Sequence:D4}.json")
                : OwnedPath(bundle, RecoveryName);
            failureStage = completed ? "completed-context" : "recovery-context";
            using var receiptLock = new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var receiptBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(receiptBytes);
            string sourceIndexSha;
            string? completedReportSha = null;
            if (completed)
            {
                if (!string.Equals(bundle, OwnedPath(run, context.ReportAttempt),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
                    Digest(receiptBytes) != history.Sha256) throw Fail("HANDLER_REQUERY_COMPLETED_CONTEXT_INVALID");
                var compiledArtifact = checkpoint.Artifacts.Single(item => item.RelativePath == context.ReportAttempt + "/compiled/compiled-paths.handoff.local.json");
                var compiledArtifactPath = OwnedPath(run, compiledArtifact.RelativePath);
                var actualCompiled = await WebFormsReviewPreflightCommand.HashAsync("compiled-report", compiledArtifactPath,
                    plan.Configuration.Budgets.MaxRetainedArtifactBytes, token);
                if (actualCompiled.Sha256 != compiledArtifact.Sha256 || actualCompiled.Bytes != compiledArtifact.Bytes)
                    throw Fail("HANDLER_REQUERY_COMPLETED_REPORT_CHANGED");
                completedReportSha = compiledArtifact.Sha256;
                sourceIndexSha = checkpoint.Artifacts.Single(item => item.RelativePath == context.ReportAttempt + "/combined.sqlite").Sha256;
            }
            else
            {
                var recovery = JsonSerializer.Deserialize<WebFormsReportRecoveryReceipt>(receiptBytes, JsonOptions) ?? throw Fail("RECOVERY_RECEIPT_INVALID");
                if (recovery.RunId != plan.RunId || recovery.SourcePreflightSha256 != Digest(planBytes) ||
                    recovery.SourceCheckpointSha256 != history.Sha256 || recovery.BoundedInputSha256 != RecoveryHash(recovery))
                    throw Fail("HANDLER_REQUERY_RECOVERY_CONTEXT_INVALID");
                sourceIndexSha = recovery.SourceIndexSha256;
            }
            // Both readers verify their checkpoint/receipt-bound evidence index.
            CombinedPathSymbolRoot[] roots;
            if (explicitRoot is not null) roots = [explicitRoot];
            else
            {
                using var rootsOutput = new StringWriter(); using var rootsError = new StringWriter();
                failureStage = "root-query";
                var rootQueryResult = completed
                    ? await QueryAsync(["query", "--run", run, "--document", "compiled", "--pointer",
                        "/header/query/symbolRoots", "--limit", "50", "--depth", "2"], rootsOutput, rootsError, token)
                    : await QueryRecoveryAsync(["query-recovery", "--bundle", bundle, "--document", "compiled", "--pointer",
                        "/header/query/symbolRoots", "--limit", "50", "--depth", "2"], rootsOutput, rootsError, token);
                if (rootQueryResult != 0)
                    throw Fail("HANDLER_REQUERY_ROOT_QUERY_FAILED");
                var response = JsonSerializer.Deserialize<WebFormsEvidenceResponse>(rootsOutput.ToString(), QueryJson) ?? throw Fail("HANDLER_REQUERY_ROOT_QUERY_INVALID");
                if (response.Truncated || response.Result.OmittedChildren != 0) throw Fail("HANDLER_REQUERY_ROOT_QUERY_LIMIT");
                roots = response.Result.Children.Select(item =>
                {
                    var fields = item.Children.ToDictionary(child => child.Pointer.Split('/')[^1], child => child.Value?.GetString(), StringComparer.Ordinal);
                    return new CombinedPathSymbolRoot(fields["sourceIndexId"]!, fields["scanId"]!, fields["commitSha"]!, fields["symbolId"]!);
                }).Where(root => root.SymbolId.Split('(', 2)[0].EndsWith("." + handler, StringComparison.Ordinal)).Distinct().ToArray();
                if (roots.Length != 1) throw Fail("HANDLER_REQUERY_ROOT_NOT_UNIQUE");
            }
            var indexPath = OwnedPath(run, context.ReportAttempt + "/combined.sqlite");
            failureStage = "combined-index";
            RejectQuerySidecars(indexPath);
            using var indexLock = new FileStream(indexPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var index = await WebFormsReviewPreflightCommand.HashAsync("combined-index", indexPath, plan.Configuration.Budgets.MaxRetainedArtifactBytes, token);
            if (index.Sha256 != sourceIndexSha) throw Fail("HANDLER_REQUERY_INDEX_CHANGED");
            if (explicitRoot is not null)
            {
                using var connection = new SqliteConnection($"Data Source={new Uri(indexPath).AbsoluteUri}?immutable=1;Mode=ReadOnly;Pooling=False");
                await connection.OpenAsync(token);
                using var command = connection.CreateCommand();
                command.CommandText = """
                    SELECT f.combined_fact_id FROM combined_facts f
                    JOIN index_sources s ON s.source_index_id = f.source_index_id
                    WHERE s.source_index_id = $source AND s.scan_id = $scan AND s.commit_sha = $commit
                      AND f.fact_type = $kind AND f.target_symbol = $symbol
                    LIMIT 2
                    """;
                command.Parameters.AddWithValue("$source", explicitRoot.SourceIndexId);
                command.Parameters.AddWithValue("$scan", explicitRoot.ScanId);
                command.Parameters.AddWithValue("$commit", explicitRoot.CommitSha);
                command.Parameters.AddWithValue("$kind", FactTypes.ManagedMethodDeclared);
                command.Parameters.AddWithValue("$symbol", explicitRoot.SymbolId);
                using var reader = await command.ExecuteReaderAsync(token);
                if (!await reader.ReadAsync(token) || await reader.ReadAsync(token)) throw Fail("COMPILED_ROOT_NOT_UNIQUE");
            }
            await output.WriteLineAsync($"handlerStage=root-verified;rootIdentities=1;selection={(explicitRoot is null ? "retained-handler" : "exact-compiled-method")};no-scan;no-combine");
            var config = plan.Configuration;
            var budget = config.Budgets.Reports ?? new();
            WebFormsReviewPreflightCommand.ValidateReportBudgets(budget);
            RetainedMethodGraph? graphView = null;
            var options = new CombinedDependencyPathOptions(indexPath, destination, ToSurface: methodGraph ? null : "database-api", SurfaceName: surfaceName, IncludeLegacyRoots: true,
                MaxDepth: maxDepth ?? config.Budgets.GraphMaxDepth, MaxPaths: config.Budgets.GraphMaxPaths, MaxFrontier: budget.MaxFrontier)
            { ExactFromSymbol = true, CompiledOnly = compiledOnly, MaxTraversalWork = checked((int)config.Budgets.GraphMaxWork),
                MethodGraphObserver = methodGraph ? graph => graphView = graph : null };
            await output.WriteLineAsync($"handlerStage=graph-started;maxPaths={options.MaxPaths};maxWork={options.MaxTraversalWork};single-root=true");
            await output.WriteLineAsync($"handlerQuery.maxDepth={options.MaxDepth};originalMaxDepth={config.Budgets.GraphMaxDepth};depthOverride={(maxDepth is not null).ToString().ToLowerInvariant()};other-bounds-unchanged=true");
            await output.WriteLineAsync($"handlerQuery.terminalScope={(methodGraph ? "unfiltered-all-outgoing" : surfaceName is null ? "all-database-api" : "DbDataAdapter.Fill")};mixed-source-and-compiled={(compiledOnly ? "false" : "true")}");
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
            // A graph-only query deliberately skips path enumeration. Never publish
            // its empty Paths collection as a normal-looking zero-chain report.
            if (!methodGraph) await GroupedCompiledPathReportWriter.WriteAsync(grouped, destination, limits, token);
            if (methodGraph)
            {
                if (graphView is null) throw Fail("HANDLER_REQUERY_GRAPH_UNAVAILABLE");
                await RetainedMethodGraphWriter.WriteAsync(graphView, destination, index.Sha256, budget.MaxOutputBytes, token);
                await output.WriteLineAsync($"methodGraph=unfiltered-retained;nodes={graphView.Nodes.Count};edges={graphView.Edges.Count};calls={graphView.Calls.Count};cutoffs={graphView.Cutoffs.Count};no-terminal-pruning");
            }
            if (Digest(receiptBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token)) ||
                Digest(planBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(run, "run-manifest.json"), 4_194_304, token)) ||
                history.Sha256 != (await ReadHistoryAsync(run, plan, Digest(planBytes), first.RuntimeInputsSha256, token)).Sha256)
                throw Fail("HANDLER_REQUERY_CONTEXT_CHANGED");
            if (completed && completedReportSha != (await WebFormsReviewPreflightCommand.HashAsync("compiled-report",
                OwnedPath(run, context.ReportAttempt + "/compiled/compiled-paths.handoff.local.json"),
                plan.Configuration.Budgets.MaxRetainedArtifactBytes, token)).Sha256)
                throw Fail("HANDLER_REQUERY_COMPLETED_REPORT_CHANGED");
            var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var artifacts = new List<WebFormsReviewArtifact>();
            foreach (var name in methodGraph
                         ? new[] { RetainedMethodGraphWriter.HtmlName, RetainedMethodGraphWriter.JsonName }
                         : new[] { GroupedCompiledPathReportWriter.HtmlName, GroupedCompiledPathReportWriter.HandoffName })
            {
                var artifact = await WebFormsReviewPreflightCommand.HashAsync("report", Path.Combine(destination, name), budget.MaxOutputBytes, token);
                artifacts.Add(new(name, artifact.Bytes, artifact.Sha256));
            }
            var receipt = new WebFormsHandlerRequeryReceipt("webforms-handler-requery.v1", HandlerRequeryRule, "local-only",
                methodGraph ? "unfiltered-retained-method-graph-not-runtime-completeness" : explicitRoot is null ? "single-handler-retained-static-review-not-parity" : "single-compiled-method-retained-static-review-not-parity", generator.Sha256, "", grouped.GeneratorSha256,
                plan.RunId, Digest(planBytes), history.Sha256!, completed ? null : Digest(receiptBytes), index.Sha256, roots[0], paths.Query,
                grouped.Chains.Count, grouped.Variants.Count, paths.Summary.Truncated, paths.Summary.TraversalWorkUnits, artifacts,
                [completed ? "One exact source/scan/commit/symbol root was selected from the checkpointed completed report index."
                    : "One exact source/scan/commit/symbol root was selected from the verified recovered report index.",
                 "All admitted global competitors remain present; this requery repeats graph construction and traversal, not source scanning or combining.",
                 "Independent single-root path/work bounds are not complete coverage, historical parity, runtime SQL or authenticated-build proof.",
                 "Original checkpoints and report bundle remain unchanged. All identities and paths are private.",
                 "Graph observations count logical scratch fact payload reads and stage wall time, not physical disk I/O or runtime calls."])
                { GraphObservation = observation, CompletedReportSha256 = completedReportSha,
                    PrimaryReport = methodGraph ? RetainedMethodGraphWriter.HtmlName : GroupedCompiledPathReportWriter.HtmlName,
                    PathEnumerationPerformed = !methodGraph,
                    RootSelectionKind = explicitRoot is null ? null : "exact-compiled-method" };
            receipt = receipt with { BoundedInputSha256 = HandlerRequeryHash(receipt) };
            failureStage = "receipt";
            await File.WriteAllTextAsync(Path.Combine(destination, HandlerRequeryName), JsonSerializer.Serialize(receipt, JsonOptions), token);
            await output.WriteLineAsync($"handlerRequery.selectorCandidates={paths.Summary.SelectorCandidateCount}");
            await output.WriteLineAsync($"handlerRequery.primaryReport={receipt.PrimaryReport};pathEnumerationPerformed={(!methodGraph).ToString().ToLowerInvariant()}");
            var safeGaps = paths.Gaps.Select(gap => (gap.GapKind, gap.Reason)).Distinct().Take(20).ToArray();
            static string SafeCode(string? value) => value is { Length: > 0 and <= 96 } &&
                value.All(c => char.IsAsciiLetterOrDigit(c) || c is '_' or '-' or '.') ? value : "none-or-redacted";
            foreach (var gap in safeGaps)
                await output.WriteLineAsync($"handlerGap.kind={SafeCode(gap.GapKind)};reason={SafeCode(gap.Reason)}");
            await output.WriteLineAsync(methodGraph
                ? $"handlerRequery=completed-separate-method-graph;truncated={paths.Summary.Truncated};workUnits={paths.Summary.TraversalWorkUnits};no-scan;no-combine;originals-preserved"
                : $"handlerRequery=completed-separate-report;exactChains={grouped.Chains.Count};evidenceVariants={grouped.Variants.Count};truncated={paths.Summary.Truncated};workUnits={paths.Summary.TraversalWorkUnits};no-scan;no-combine;originals-preserved");
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
