using System.Text.Json;
using TraceMap.Core;
using Microsoft.Data.Sqlite;

namespace TraceMap.Cli;

public sealed record WebFormsReviewStatusPhase(string Name, string State, string ObservationScope,
    IReadOnlyDictionary<string, long> ObservedCounts, IReadOnlyDictionary<string, long> ConfiguredLimits,
    long? WorkUnitsUsed, IReadOnlyList<string> UsageGaps)
{
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? WorkUnitsScope { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WebFormsReviewPhaseUsage? ResourceUsage { get; init; }
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<CompiledAdmissionWorkUsage>? AdmissionWork { get; init; }
}
public sealed record WebFormsReviewStatusAction(string Kind, string Instruction, string? Command, IReadOnlyList<string> Arguments);
public sealed record WebFormsReviewLocatorStatus(string Role, int Present, int Missing);
public sealed record WebFormsReviewStatus(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256, string OriginalGeneratorSha256,
    string RunId, string RunRoot, string PreflightSha256, string? CheckpointSha256, int CheckpointSequence,
    string State, string Coverage, bool RetainedArtifactsVerified, bool ReaderMatchesOriginalGenerator,
    string? WorkbenchPath, IReadOnlyList<WebFormsReviewStatusPhase> Phases,
    IReadOnlyList<WebFormsReviewLocatorStatus> InputLocators, IReadOnlyDictionary<string, int> CompiledGapsByKind,
    int OmittedCompiledGapKinds, IReadOnlyDictionary<string, int> CompiledTruncationsByReason,
    IReadOnlyList<string> CheckpointGaps, int OmittedCheckpointGaps,
    IReadOnlyList<WebFormsReviewStatusAction> NextActions, IReadOnlyList<string> Limitations)
{
    public string Operation { get; init; } = "";
    public string PageMode { get; init; } = "";
    public string SourceCommitSha { get; init; } = "";
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public WebFormsReviewToolDistribution? OriginalTool { get; init; }
}

public static partial class WebFormsReviewExecutionCommand
{
    internal const string StatusRule = "workflow.webforms.compiled-review-status.v1";

    /// <summary>Bounded operational status from pinned retained evidence; never scans or repairs.</summary>
    public static async Task<int> StatusAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length is not (3 or 4) || args[0] != "status" || args[1] != "--run" ||
                (args.Length == 4 && args[3] != "--json")) throw Fail("STATUS_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var manifestPath = OwnedPath(root, "run-manifest.json");
            var lockPath = OwnedPath(root, ".native-run.lock");
            // Preflight has no lock file. Do not create one in a read-only command;
            // a final history recheck detects execution starting during observation.
            var hasRunLock = File.Exists(lockPath);
            using var runLock = new FileStream(hasRunLock ? lockPath : manifestPath,
                FileMode.Open, FileAccess.Read, hasRunLock ? FileShare.None : FileShare.Read);
            var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(manifestPath, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
            var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(bytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
            if (plan.SchemaVersion != WebFormsReviewPreflightCommand.ManifestSchema || plan.RuleId != WebFormsReviewPreflightCommand.RuleId ||
                plan.Visibility != "local-only" || plan.ClaimLevel != "preflight-only-not-executed" ||
                !Guid.TryParseExact(plan.RunId, "N", out _) || !ValidSha(plan.GeneratorSha256) ||
                plan.Inputs is null || plan.Inputs.Count > 20_480 || plan.Configuration?.Budgets is null ||
                plan.Configuration.Operation is not ("fresh" or "attach") || plan.Configuration.PageMode is not ("selected" or "all") ||
                plan.Configuration.SourceCommitSha is not { Length: 40 } sourceCommit ||
                !sourceCommit.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f')) throw Fail("PREFLIGHT_INVALID");
            var preflight = Digest(bytes);
            var firstPath = OwnedPath(root, "checkpoints/0001.json");
            var runtime = "";
            if (File.Exists(firstPath))
            {
                var firstBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(firstPath, 4_194_304, token);
                WebFormsReviewPreflightCommand.RejectDuplicateProperties(firstBytes);
                runtime = (JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(firstBytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID")).RuntimeInputsSha256;
            }
            var history = await ReadHistoryAsync(root, plan, preflight, runtime, token);
            var checkpoint = history.Checkpoint;
            var state = checkpoint?.State ?? plan.State;
            var phases = new List<WebFormsReviewStatusPhase>();
            var budget = plan.Configuration.Budgets;
            var publishCount = plan.Inputs.Count(input => WebFormsReviewPreflightCommand.IsPublishInventoryRole(input.Role));
            phases.Add(new("preflight", "retained-plan", "pinned inventory; external bytes not revalidated",
                new Dictionary<string, long> { ["inputFiles"] = plan.Inputs.Count, ["publishInventoryFiles"] = publishCount, ["hashedBytes"] = plan.HashedBytes },
                new Dictionary<string, long> { ["nonPublishInputFiles"] = budget.MaxInputFiles,
                    ["publishInventoryFiles"] = budget.MaxPublishInputFiles ?? budget.MaxInputFiles, ["totalHashBytes"] = budget.MaxTotalHashBytes },
                null, ["Input pools share maxInputFiles unless maxPublishInputFiles is explicitly configured; do not compare total files against the non-publish pool.",
                    "Elapsed time, peak memory and work units were not retained by this phase."]));
            var verified = false;
            if (history.ScanCheckpoint is not null)
            {
                await VerifyArtifactsAsync(root, checkpoint?.State == WebFormsReviewReportExecution.Completed ? checkpoint : history.ScanCheckpoint, plan, token);
                verified = true;
            }
            var scanArtifacts = history.ScanCheckpoint?.Artifacts ?? [];
            var admissionWork = new List<CompiledAdmissionWorkUsage>();
            if (history.ScanCheckpoint is { } completedScan)
            {
                var scanManifest = await ReadScanManifestAsync(OwnedPath(root, completedScan.Attempt + "/scan"), token);
                if (scanManifest.CompiledInputProvenance is { } metadata)
                {
                    CompiledAdmissionWorkUsage.Validate(metadata.AdmissionWork, "metadata", metadata.GeneratorSha256,
                        metadata.BoundedInputSha256, metadata.EffectiveLimits.MaxTotalWorkUnits);
                    if (metadata.AdmissionWork is { } usage) admissionWork.Add(usage);
                }
                if (scanManifest.IlBodyProvenance is { } il)
                {
                    CompiledAdmissionWorkUsage.Validate(il.AdmissionWork, "il-body", il.GeneratorSha256,
                        il.BoundedInputSha256, il.EffectiveLimits.MaxTotalWorkUnits);
                    if (il.AdmissionWork is { } usage) admissionWork.Add(usage);
                }
            }
            phases.Add(new("scan", history.ScanCheckpoint is null ? state.StartsWith("scan-", StringComparison.Ordinal) ? state : "pending" : "retained-completed",
                "admitted owned scan artifacts; not current source or historical build equivalence",
                new Dictionary<string, long> { ["retainedFacts"] = history.ScanCheckpoint?.FactCount ?? 0,
                    ["artifactFiles"] = scanArtifacts.Count, ["artifactBytes"] = scanArtifacts.Sum(item => item.Bytes) },
                new Dictionary<string, long> { ["metadataWorkUnits"] = budget.MetadataMaxWork, ["ilWorkUnits"] = budget.IlMaxWork,
                    ["ilBodies"] = budget.IlMaxBodies ?? 50_000,
                    ["artifactBytesPerFile"] = budget.MaxRetainedArtifactBytes, ["totalHashBytes"] = budget.MaxTotalHashBytes },
                null, ["Retained fact and artifact counts are not metadata/IL work consumption. Optional AdmissionWork records each collector's independent logical credit consumption, not total scan work. Exact phase peak, child-process usage and transient disk peak were not recorded.",
                    "ResourceUsage is an optional retained attempt observation; absent historical elapsed time and memory samples remain unknown."])
                { AdmissionWork = admissionWork.Count == 0 ? null : admissionWork,
                    ResourceUsage = history.ScanCheckpoint?.PhaseUsage
                    ?? (checkpoint?.State is "scan-failed" or "scan-cancelled" ? checkpoint.PhaseUsage : null) });
            var gapKinds = new Dictionary<string, int>(StringComparer.Ordinal);
            IReadOnlyDictionary<string, int> truncations = new Dictionary<string, int>(StringComparer.Ordinal);
            var omittedKinds = 0;
            var reportCounts = new Dictionary<string, long>();
            var reportBudget = budget.Reports ?? new();
            var coverage = checkpoint?.Reports?.Coverage ?? "not-reported";
            long? compiledWork = null;
            long? pageWork = null;
            string? workbench = null;
            if (checkpoint?.State == WebFormsReviewReportExecution.Completed)
            {
                var prefix = checkpoint.Reports!.ReportAttempt + "/";
                var reportArtifacts = checkpoint.Artifacts.Where(item => item.RelativePath.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
                reportCounts["artifactFiles"] = reportArtifacts.Length; reportCounts["artifactBytes"] = reportArtifacts.Sum(item => item.Bytes);
                var index = reportArtifacts.Single(item => item.RelativePath == prefix + WebFormsReviewEvidenceIndex.Name);
                var app = reportArtifacts.Single(item => item.RelativePath == prefix + WebFormsReviewReportExecution.HandoffName);
                var compiled = reportArtifacts.Single(item => item.RelativePath == prefix + "compiled/compiled-paths.handoff.local.json");
                var indexPath = OwnedPath(root, index.RelativePath); RejectQuerySidecars(indexPath);
                using var connection = new SqliteConnection($"Data Source={new Uri(indexPath).AbsoluteUri}?immutable=1;Mode=ReadOnly;Pooling=False");
                await connection.OpenAsync(token);
                var context = WebFormsReviewEvidenceIndex.ReadContext(connection, plan.RunId, app.Sha256, compiled.Sha256);
                if (index.Bytes > context.MaxIndexBytes) throw Fail("STATUS_INDEX_LIMIT");
                reportCounts["surfaces"] = Number("application", "/packet/summary/surfaceCount");
                reportCounts["eventChains"] = Number("application", "/packet/summary/eventChainCount");
                reportCounts["packetGaps"] = Number("application", "/packet/summary/gapCount");
                reportCounts["packetTruncated"] = Flag("application", "/packet/summary/truncated") ? 1 : 0;
                reportCounts["compiledVariants"] = Container("compiled", "/variants").ChildCount;
                reportCounts["compiledGroups"] = Container("compiled", "/chains").ChildCount;
                reportCounts["compiledGraphNodes"] = Number("compiled", "/header/summary/graphNodeCount");
                reportCounts["compiledGraphEdges"] = Number("compiled", "/header/summary/graphEdgeCount");
                reportCounts["compiledGaps"] = Number("compiled", "/header/summary/gapCount");
                reportCounts["compiledTruncated"] = Flag("compiled", "/header/summary/truncated") ? 1 : 0;
                // Older summaries omit this counter. Absence is unknown, not
                // invented zero and not a reason to rewrite historical proof.
                compiledWork = SummaryWork("compiled", "/header/summary");
                pageWork = SummaryWork("application", "/packet/summary");
                if (compiledWork is not null) reportCounts["compiledTraversalWorkUnits"] = compiledWork.Value;
                if (pageWork is not null) reportCounts["pageTraversalWorkUnits"] = pageWork.Value;
                reportCounts["requestedCompiledRoots"] = Number("application", "/requestedCompiledRoots");
                reportCounts["omittedCompiledRoots"] = Number("application", "/omittedCompiledRoots");
                var kinds = ReadQuery(connection, new("compiled", "/header/inventory/gapsByKind", Limit: 50, Depth: 1), token).Result;
                omittedKinds = kinds.OmittedChildren;
                foreach (var child in kinds.Children)
                {
                    var name = child.Pointer.Split('/').Last().Replace("~1", "/", StringComparison.Ordinal).Replace("~0", "~", StringComparison.Ordinal);
                    gapKinds.Add(name, child.Value!.Value.GetInt32());
                }
                truncations = ReadStatusTruncationReasons(connection, token);
                workbench = OwnedPath(root, prefix + "index.html");
                WebFormsEvidenceItem Container(string document, string pointer) => ReadQuery(connection, new(document, pointer, Depth: 0), token).Result;
                long Number(string document, string pointer) => Container(document, pointer).Value!.Value.GetInt64();
                bool Flag(string document, string pointer) => Container(document, pointer).Value!.Value.GetBoolean();
                long? SummaryWork(string document, string pointer)
                {
                    var summary = ReadQuery(connection, new(document, pointer, Limit: 16, Depth: 1), token).Result;
                    var item = summary.Children.SingleOrDefault(child => child.Pointer == pointer + "/traversalWorkUnits");
                    if (item is null) return null;
                    var value = item.Value!.Value.GetInt64();
                    if (value < 0 || value > budget.GraphMaxWork) throw Fail("STATUS_TRAVERSAL_WORK_INVALID");
                    return value;
                }
            }
            phases.Add(new("reports", checkpoint?.Reports is null ? "pending" : checkpoint.State,
                "retained aggregate counts; each graph query shares its path/work budget across all selected roots",
                reportCounts, new Dictionary<string, long> { ["inputFacts"] = reportBudget.MaxInputFacts,
                    ["inputEdges"] = reportBudget.MaxInputEdges, ["inputTextBytes"] = reportBudget.MaxInputTextBytes,
                    ["graphStorageBytes"] = reportBudget.MaxGraphStorageBytes ?? TraceMap.Reporting.CombinedPathAdmissionLimits.DefaultMaxGraphStorageBytes,
                    ["surfaces"] = reportBudget.MaxSurfaces, ["eventChains"] = reportBudget.MaxEventChains,
                    ["compiledRoots"] = reportBudget.MaxCompiledRoots, ["pathsPerGraphQuery"] = budget.GraphMaxPaths,
                    ["depthPerPath"] = budget.GraphMaxDepth, ["traversalWorkPerGraphQuery"] = budget.GraphMaxWork,
                    ["projectionInputBytes"] = reportBudget.MaxProjectionInputBytes, ["renderedOutputBytes"] = reportBudget.MaxOutputBytes },
                compiledWork is not null && pageWork is not null ? checked(compiledWork.Value + pageWork.Value) : null,
                ["Work usage sums only the independently bounded page and compiled traversal queries when both are retained. Graph admission, exact phase peak, child-process usage and transient disk peak remain separate unavailable counters.",
                    "Aggregate artifact bytes include combined storage and are not the rendered-output byte counter. Null work usage never means zero or complete coverage."])
                { WorkUnitsScope = compiledWork is null || pageWork is null ? null : "page-and-compiled-graph-query-traversal-all-selected-roots",
                    ResourceUsage = checkpoint?.State.StartsWith("reports-", StringComparison.Ordinal) == true ? checkpoint.PhaseUsage : null });
            var observedLocators = plan.Inputs.Select(input => (input.Role, Present: File.Exists(input.Path))).ToArray();
            var locators = observedLocators.GroupBy(input => input.Role, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => new WebFormsReviewLocatorStatus(group.Key, group.Count(input => input.Present), group.Count(input => !input.Present))).ToArray();
            var actions = new List<WebFormsReviewStatusAction>();
            if (locators.Any(item => item.Missing > 0)) actions.Add(new("restore-input-locators",
                "Restore the original explicitly declared input locations before execution/resume. Presence checks do not establish byte equality; do not select replacements by timestamp.", null, []));
            if (checkpoint?.State == WebFormsReviewReportExecution.Completed)
                actions.Add(new("inspect-retained-evidence", "Open the workbench or retrieve bounded evidence; preserve partial coverage and every gap.",
                    "webforms-review", ["query", "--run", root]));
            else actions.Add(new(checkpoint is null ? "execute-pinned-plan" : "resume-pinned-plan",
                "Use the original pinned tool/runtime. Execution validates inputs and creates new owned attempts; status does not authorize source changes or repair.",
                "webforms-review", [checkpoint is null ? "run" : "resume", "--run", root]));
            if (gapKinds.Count > 0 || coverage == "partial-static-review") actions.Add(new("review-coverage-gaps",
                "Review missing provenance and traversal/admission limits before expanding scope. Cycle truncation is not automatically a work-budget failure; do not raise every cap.", null, []));
            var generator = await WebFormsReviewPreflightCommand.HashAsync("status-generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var gaps = checkpoint?.Gaps ?? plan.Gaps;
            var status = new WebFormsReviewStatus("webforms-review-status.v1", StatusRule, "Tier2Structural", "local-only",
                "retained-status-not-fresh-source-or-runtime", generator.Sha256, "", plan.GeneratorSha256,
                plan.RunId, root, preflight, history.Sha256, history.Sequence, state, coverage, verified,
                generator.Sha256 == plan.GeneratorSha256, workbench, phases, locators, gapKinds, omittedKinds,
                truncations, gaps.Take(64).ToArray(), Math.Max(0, gaps.Count - 64), actions,
                ["Status is a point-in-time retained artifact observation, not fresh source, binding, build, runtime SQL or all-pages validation.",
                 "Only explicitly pinned file locators were checked for presence; no source/snapshot roster walk or byte validation occurred. Presence does not prove source equality or original generator/runtime availability. Resume admission was not performed.",
                 "Unknown usage is null or explicitly listed as a gap, not invented zero, success or unused capacity.",
                 "Only bounded indexed summaries were retrieved; no whole handoff, source snippets, scanning, repair, cleanup or LLM calls occurred.",
                 "Private paths and hashes are local integrity commitments. This response is not a shareable projection or deletion approval."])
            { Operation = plan.Configuration.Operation, PageMode = plan.Configuration.PageMode, SourceCommitSha = plan.Configuration.SourceCommitSha,
                OriginalTool = checkpoint?.ToolDistribution };
            status = status with { BoundedInputSha256 = Digest(JsonSerializer.SerializeToUtf8Bytes(status, JsonOptions)) };
            var rechecked = await ReadHistoryAsync(root, plan, preflight, runtime, token);
            if (rechecked.Sha256 != history.Sha256 || rechecked.Sequence != history.Sequence ||
                preflight != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(manifestPath, 4_194_304, token))) throw Fail("STATUS_RUN_CHANGED");
            var response = JsonSerializer.SerializeToUtf8Bytes(status, JsonOptions);
            if (response.Length > QueryMaxBytes) throw Fail("STATUS_RESPONSE_LIMIT");
            token.ThrowIfCancellationRequested();
            if (args.Length == 4) await output.WriteLineAsync(System.Text.Encoding.UTF8.GetString(response));
            else
            {
                await output.WriteLineAsync($"webFormsStatus={state};operation={status.Operation};pageMode={status.PageMode};retainedArtifactsVerified={verified};coverage={coverage}");
                await output.WriteLineAsync($"checkpoints={history.Sequence};facts={history.ScanCheckpoint?.FactCount ?? 0};missingInputLocators={locators.Sum(item => item.Missing)};readerMatchesOriginalGenerator={status.ReaderMatchesOriginalGenerator};resumeAdmissionPerformed=false");
                await output.WriteLineAsync($"originalToolLocation={(status.OriginalTool is null ? "unknown-historical" : "retained-declaration-not-current-availability")};externalSdkPinned=false");
                if (reportCounts.Count > 0) await output.WriteLineAsync($"surfaces={reportCounts["surfaces"]};compiledVariants={reportCounts["compiledVariants"]};compiledGroups={reportCounts["compiledGroups"]};compiledGaps={reportCounts["compiledGaps"]};compiledTruncated={reportCounts["compiledTruncated"] != 0}");
                if (truncations.Count > 0) await output.WriteLineAsync("compiledTruncations=" + string.Join(',', truncations.Select(item => item.Key + ":" + item.Value)));
                if (reportCounts.Count > 0) await output.WriteLineAsync($"traversalWork=page:{pageWork?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"};compiled:{compiledWork?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"};limitPerGraphQuery={budget.GraphMaxWork};sharedAcrossSelectedRoots=true");
                if (workbench is not null) await output.WriteLineAsync($"webFormsWorkbench={workbench}");
                foreach (var phase in phases.Where(item => item.ResourceUsage is not null))
                    await output.WriteLineAsync($"phaseUsage={phase.Name};elapsedMs={phase.ResourceUsage!.ElapsedMilliseconds};observedWorkingSetBytes={phase.ResourceUsage.MaximumObservedWorkingSetBytes?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "unknown"};memorySamples={phase.ResourceUsage.SuccessfulMemorySamples};exactPhasePeak=false;childrenIncluded=false");
                foreach (var usage in admissionWork)
                    await output.WriteLineAsync($"admissionWork={usage.Phase};credits={usage.ConsumedWorkUnits};max={usage.MaxWorkUnits};refusedAggregateRequests={usage.RefusedAggregateRequests};scope={usage.WorkUnitsScope};totalScanWork=false");
                foreach (var action in actions) await output.WriteLineAsync($"nextAction={action.Kind}: {action.Instruction}");
                await output.WriteLineAsync("workUsage=not-fully-retained;use-status-json-for-configured-limits-and-observed-counts;cleanup=false");
            }
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ExecutionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (IOException) { await error.WriteLineAsync("error: WEBFORMS_NATIVE_STATUS_RUN_BUSY_OR_INPUT_UNAVAILABLE"); return 1; }
        catch (Exception) { await error.WriteLineAsync("error: WEBFORMS_NATIVE_STATUS_INPUT_OR_INDEX_INVALID"); return 1; }
    }

    // Aggregate only categorical truncation reasons over the independently
    // node/byte-bounded token index. No gap messages, paths or whole rows load.
    internal static IReadOnlyDictionary<string, int> ReadStatusTruncationReasons(SqliteConnection connection, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        using var command = connection.CreateCommand();
        command.CommandText = """
            WITH root AS (SELECT id FROM json_nodes WHERE document='compiled' AND parent_id IS NULL),
            header AS (SELECT n.id FROM json_nodes n JOIN root r ON n.parent_id=r.id WHERE n.document='compiled' AND n.name='header'),
            gaps AS (SELECT n.id FROM json_nodes n JOIN header h ON n.parent_id=h.id WHERE n.document='compiled' AND n.name='gaps')
            SELECT CASE WHEN reason.scalar_json IN ($cycle,$depth,$frontier,$work,$path,$selector)
                THEN CAST(reason.scalar_json AS TEXT) ELSE '"other-retained-reason"' END AS category, COUNT(*)
            FROM json_nodes item JOIN gaps ON item.parent_id=gaps.id
            JOIN json_nodes kind ON kind.parent_id=item.id AND kind.document='compiled' AND kind.name='gapKind'
            LEFT JOIN json_nodes reason ON reason.parent_id=item.id AND reason.document='compiled' AND reason.name='reason'
            WHERE item.document='compiled' AND kind.scalar_json=$kind
            GROUP BY category ORDER BY category COLLATE BINARY
            """;
        foreach (var value in new[] { "cycle", "depth", "frontier", "work", "path", "selector" })
            command.Parameters.AddWithValue("$" + value, JsonSerializer.SerializeToUtf8Bytes(value == "selector" ? "selector-candidates" : value));
        command.Parameters.AddWithValue("$kind", JsonSerializer.SerializeToUtf8Bytes("TruncatedByLimit"));
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        using var registration = token.Register(command.Cancel);
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            token.ThrowIfCancellationRequested();
            var category = JsonSerializer.Deserialize<string>(reader.GetString(0)) ?? throw Fail("STATUS_TRUNCATION_INVALID");
            result.Add(category, checked((int)reader.GetInt64(1)));
        }
        return result;
    }
}
