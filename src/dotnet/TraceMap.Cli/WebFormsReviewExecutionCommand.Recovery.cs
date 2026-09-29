using System.Text;
using System.Text.Json;
using TraceMap.Reporting;

namespace TraceMap.Cli;

public sealed record WebFormsReportRecoveryReceipt(string SchemaVersion, string RuleId, string Visibility,
    string ClaimLevel, string GeneratorSha256, string BoundedInputSha256, string RunId,
    string SourcePreflightSha256, string SourceCheckpointSha256, string SourceIndexSha256,
    int ExactChains, int EvidenceVariants, int MaxEvidenceNodes,
    IReadOnlyList<WebFormsReviewArtifact> Artifacts, IReadOnlyList<string> Limitations);

public static partial class WebFormsReviewExecutionCommand
{
    internal const string RecoveryName = "report-recovery.local.json";
    internal const string RecoveryRule = "workflow.webforms.retained-report-recovery.v1";

    /// <summary>Separate local recovery bundle. Never resumes or completes the original run.</summary>
    public static async Task<int> RecoverReportsAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length != 5 || args[0] != "recover-reports" || args[1] != "--run" || args[3] != "--out") throw Fail("RECOVERY_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            if (Path.Exists(destination) || Within(destination, root) || Within(root, destination)) throw Fail("RECOVERY_OUTPUT_EXISTS_OR_OVERLAPS");
            using var runLock = new FileStream(OwnedPath(root, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.Read);
            var planBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "run-manifest.json"), 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(planBytes);
            var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(planBytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
            var firstBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "checkpoints/0001.json"), 4_194_304, token);
            var first = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(firstBytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID");
            var history = await ReadHistoryAsync(root, plan, Digest(planBytes), first.RuntimeInputsSha256, token);
            if (history.Checkpoint is not { State: ReportsFailed, Reports: { } context } failed ||
                !failed.Gaps.Contains("WEBFORMS_EVIDENCE_NODE_LIMIT") || history.ScanCheckpoint is not { } scan)
                throw Fail("RECOVERY_REQUIRES_RETAINED_NODE_LIMIT_FAILURE");
            foreach (var input in plan.Inputs)
                if (Within(destination, input.Path) || Within(input.Path, destination)) throw Fail("RECOVERY_OUTPUT_OVERLAPS_INPUT");
            foreach (var input in new[] { plan.Configuration.SourceRoot, plan.Configuration.PublishedRoot,
                         plan.Configuration.ReceiptRoot, plan.Configuration.ParentScanRoot }.Where(value => value is not null))
                if (Within(destination, input!) || Within(input!, destination)) throw Fail("RECOVERY_OUTPUT_OVERLAPS_INPUT");
            await VerifyArtifactsAsync(root, scan, plan, token);
            await output.WriteLineAsync("recoveryStage=retained-scan-verified;no-scan-started");
            var report = OwnedPath(root, context.ReportAttempt);
            var budgets = plan.Configuration.Budgets.Reports ?? new();
            var appPath = OwnedPath(root, context.ReportAttempt + "/handoff.local.json");
            var compiledPath = OwnedPath(root, context.ReportAttempt + "/compiled/compiled-paths.handoff.local.json");
            var appInput = await WebFormsReviewPreflightCommand.HashAsync("application", appPath, budgets.MaxOutputBytes, token);
            var compiledInput = await WebFormsReviewPreflightCommand.HashAsync("compiled", compiledPath, budgets.MaxOutputBytes, token);
            if (appInput.Bytes > budgets.MaxOutputBytes - compiledInput.Bytes) throw Fail("RECOVERY_INPUT_LIMIT");
            var json = new JsonSerializerOptions(JsonOptions) { MaxDepth = 64 };
            await using var appStream = File.OpenRead(appPath);
            var app = await JsonSerializer.DeserializeAsync<NativeWebFormsReviewHandoff>(appStream, json, token) ?? throw Fail("RECOVERY_HANDOFF_INVALID");
            await using var compiledStream = File.OpenRead(compiledPath);
            var grouped = await JsonSerializer.DeserializeAsync<GroupedCompiledPathHandoff>(compiledStream, json, token) ?? throw Fail("RECOVERY_HANDOFF_INVALID");
            var selectionBytes = plan.Configuration.PageMode == "selected"
                ? Encoding.UTF8.GetByteCount(string.Join('\n', plan.Configuration.PageRelativePaths) + "\n") : 0;
            var limits = new GroupedCompiledPathLimits(budgets.MaxProjectionInputBytes, budgets.MaxOutputBytes - selectionBytes,
                plan.Configuration.Budgets.GraphMaxPaths, budgets.MaxProjectionRecords, budgets.MaxProjectionReferences);
            var paths = GroupedCompiledPathHandoffBuilder.Restore(grouped, limits, token);
            if (app.SchemaVersion != WebFormsReviewReportExecution.Schema || app.RuleId != WebFormsReviewReportExecution.RuleId ||
                app.Visibility != "local-only" || app.ClaimLevel != "review-only-static-not-runtime" ||
                app.RunId != plan.RunId || app.GeneratorSha256 != plan.GeneratorSha256 ||
                app.ReportingGeneratorSha256 != grouped.GeneratorSha256 || app.CompiledHandoffSha256 != compiledInput.Sha256 ||
                app.CompiledHandoffRelativePath != "compiled/compiled-paths.handoff.local.json" ||
                app.IndexSha256 != grouped.InputIndexSha256 ||
                !JsonElement.DeepEquals(JsonSerializer.SerializeToElement(app.Configuration, json), JsonSerializer.SerializeToElement(plan.Configuration, json)) ||
                !app.InputInventory.SequenceEqual(plan.Inputs)) throw Fail("RECOVERY_HANDOFF_CONTEXT_INVALID");
            var index = await WebFormsReviewPreflightCommand.HashAsync("combined", OwnedPath(root, context.ReportAttempt + "/combined.sqlite"),
                plan.Configuration.Budgets.MaxRetainedArtifactBytes, token);
            var packetHash = WebFormsReviewReportExecution.CanonicalHash(app.Packet, budgets.MaxProjectionInputBytes, token);
            if (index.Sha256 != app.IndexSha256 || packetHash != app.PacketSha256) throw Fail("RECOVERY_INPUT_CHANGED");
            var manifests = new List<TraceMap.Core.ScanManifest>();
            if (plan.Configuration.Operation == "attach") manifests.Add(await ReadScanManifestAsync(plan.Configuration.ParentScanRoot!, token));
            manifests.Add(await ReadScanManifestAsync(OwnedPath(root, scan.Attempt + "/scan"), token));
            var manifestsHash = WebFormsReviewReportExecution.CanonicalHash(manifests, 8_388_608, token);
            if (manifestsHash != WebFormsReviewReportExecution.CanonicalHash(app.ScanManifests, 8_388_608, token)) throw Fail("RECOVERY_MANIFEST_CONTEXT_INVALID");
            var inputHash = WebFormsReviewReportExecution.CanonicalHash(new
            {
                policy = WebFormsReviewReportExecution.Policy(plan, OwnedPath(root, scan.Attempt + "/scan"), report),
                preflightInputSha256 = plan.BoundedInputSha256, indexSha256 = index.Sha256,
                packetSha256 = packetHash, scanManifestsSha256 = manifestsHash,
                compiledHandoffSha256 = compiledInput.Sha256, generatorSha256 = app.GeneratorSha256,
                reportingGeneratorSha256 = grouped.GeneratorSha256
            }, budgets.MaxProjectionInputBytes, token);
            if (inputHash != app.BoundedInputSha256) throw Fail("RECOVERY_REPORT_CONTEXT_INVALID");
            await output.WriteLineAsync("recoveryStage=report-context-verified;no-graph-traversal");
            Directory.CreateDirectory(Path.Combine(destination, "compiled"));
            await CopyAsync(appPath, Path.Combine(destination, "handoff.local.json"), appInput);
            await CopyAsync(compiledPath, Path.Combine(destination, "compiled/compiled-paths.handoff.local.json"), compiledInput);
            using (var writer = new StreamWriter(new FileStream(Path.Combine(destination, "index.html"), FileMode.CreateNew, FileAccess.Write), new UTF8Encoding(false)))
            {
                WebFormsReviewReportExecution.Render(writer, app, grouped.Variants.Count, grouped.Chains.Count, paths.Summary.TraversalWorkUnits, token, recovered: true);
            }
            await output.WriteLineAsync("recoveryStage=evidence-index-started;maxNodes=" + WebFormsReviewEvidenceIndex.NewPlanMaxNodes);
            await WebFormsReviewEvidenceIndex.WriteAsync(destination, plan.RunId, appInput.Sha256, compiledInput.Sha256,
                budgets.MaxOutputBytes, plan.Configuration.Budgets.MaxRetainedArtifactBytes, token, WebFormsReviewEvidenceIndex.NewPlanMaxNodes);
            // Original paths and checkpoints remain immutable; partial recovery stays separate on failure.
            if (appInput != await WebFormsReviewPreflightCommand.HashAsync("application", appPath, appInput.Bytes, token) ||
                compiledInput != await WebFormsReviewPreflightCommand.HashAsync("compiled", compiledPath, compiledInput.Bytes, token) ||
                Digest(planBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "run-manifest.json"), 4_194_304, token)) ||
                history.Sha256 != (await ReadHistoryAsync(root, plan, Digest(planBytes), first.RuntimeInputsSha256, token)).Sha256) throw Fail("RECOVERY_INPUT_CHANGED");
            var generator = await WebFormsReviewPreflightCommand.HashAsync("recovery-generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var artifacts = await CollectUnderAsync(Path.GetDirectoryName(destination)!, Path.GetFileName(destination), plan.Configuration.Budgets, token);
            // Normalize to bundle-relative names for independent verification.
            artifacts = artifacts.Select(item => item with { RelativePath = item.RelativePath[(Path.GetFileName(destination).Length + 1)..] }).ToArray();
            var receipt = new WebFormsReportRecoveryReceipt("webforms-report-recovery.v1", RecoveryRule, "local-only",
                "recovered-static-reports-original-run-not-completed", generator.Sha256, "", plan.RunId,
                Digest(planBytes), history.Sha256!, index.Sha256, grouped.Chains.Count, grouped.Variants.Count,
                WebFormsReviewEvidenceIndex.NewPlanMaxNodes, artifacts,
                ["Source scan/checkpoint hashes and report internal commitments were verified; failed outputs were not originally checkpoint-admitted.",
                 "No source scan, compiled collection, graph traversal or website build was repeated. Original run remains failed and unchanged.",
                 "This is a separate private recovery bundle, not path parity, current-source, runtime or authenticated-build proof."]);
            receipt = receipt with { BoundedInputSha256 = RecoveryHash(receipt) };
            await File.WriteAllTextAsync(Path.Combine(destination, RecoveryName), JsonSerializer.Serialize(receipt, JsonOptions), token);
            await output.WriteLineAsync($"reportRecovery=completed-separate-bundle;exactChains={grouped.Chains.Count};evidenceVariants={grouped.Variants.Count};originalRunUnchanged=true;no-scan");
            return 0;

            async Task CopyAsync(string source, string target, WebFormsReviewInput expected)
            {
                await using var input = File.OpenRead(source);
                await using (var targetStream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None)) await input.CopyToAsync(targetStream, token);
                var actual = await WebFormsReviewPreflightCommand.HashAsync(expected.Role, target, expected.Bytes, token);
                if (actual.Bytes != expected.Bytes || actual.Sha256 != expected.Sha256) throw Fail("RECOVERY_COPY_CHANGED");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            var code = exception.Message.StartsWith("WEBFORMS_", StringComparison.Ordinal) && exception.Message.Length <= 128 &&
                exception.Message.All(character => character is >= 'A' and <= 'Z' or '_') ? exception.Message : "WEBFORMS_RECOVERY_INPUT_OR_OUTPUT_INVALID";
            await error.WriteLineAsync("error: " + code + ";original-run-preserved;partial-recovery-preserved"); return 1;
        }
    }

    internal static string RecoveryHash(WebFormsReportRecoveryReceipt receipt) =>
        WebFormsReviewReportExecution.CanonicalHash(receipt with { BoundedInputSha256 = "" }, 4_194_304, CancellationToken.None);

    public static async Task<int> QueryRecoveryAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length < 3 || args[0] != "query-recovery" || args[1] != "--bundle") throw Fail("RECOVERY_QUERY_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var handlerMode = args.Length == 5 && args[3] == "--handler";
            var query = handlerMode
                ? new WebFormsEvidenceQuery("compiled", "/handler-summary") { Handler = args[4] }
                : ParseQuery(args[3..]);
            var receiptPath = OwnedPath(root, RecoveryName);
            using var receiptLock = new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var receiptBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(receiptBytes);
            var receipt = JsonSerializer.Deserialize<WebFormsReportRecoveryReceipt>(receiptBytes, JsonOptions) ?? throw Fail("RECOVERY_RECEIPT_INVALID");
            if (receipt.SchemaVersion != "webforms-report-recovery.v1" || receipt.RuleId != RecoveryRule ||
                receipt.Visibility != "local-only" || receipt.ClaimLevel != "recovered-static-reports-original-run-not-completed" ||
                receipt.BoundedInputSha256 != RecoveryHash(receipt) || receipt.Artifacts.Count is < 4 or > 256 ||
                receipt.MaxEvidenceNodes is < 2 or > WebFormsReviewEvidenceIndex.MaxSupportedNodes) throw Fail("RECOVERY_RECEIPT_INVALID");
            var admitted = receipt.Artifacts.Single(item => item.RelativePath == WebFormsReviewEvidenceIndex.Name);
            var app = receipt.Artifacts.Single(item => item.RelativePath == "handoff.local.json");
            var compiled = receipt.Artifacts.Single(item => item.RelativePath == "compiled/compiled-paths.handoff.local.json");
            var indexPath = OwnedPath(root, admitted.RelativePath);
            RejectQuerySidecars(indexPath);
            var indexHash = await WebFormsReviewPreflightCommand.HashAsync("recovery-index", indexPath, admitted.Bytes, token);
            if (indexHash.Bytes != admitted.Bytes || indexHash.Sha256 != admitted.Sha256) throw Fail("RECOVERY_INDEX_CHANGED");
            WebFormsReviewEvidenceIndex.Context context;
            WebFormsEvidenceItem result; bool truncated;
            using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder
                   { DataSource = indexPath, Mode = Microsoft.Data.Sqlite.SqliteOpenMode.ReadOnly, Pooling = false }.ToString()))
            {
                await connection.OpenAsync(token);
                context = WebFormsReviewEvidenceIndex.ReadContext(connection, receipt.RunId, app.Sha256, compiled.Sha256);
                if (context.MaxNodes != receipt.MaxEvidenceNodes || admitted.Bytes > context.MaxIndexBytes) throw Fail("RECOVERY_CONTEXT_INVALID");
                (result, truncated) = query.Handler is null ? ReadQuery(connection, query, token)
                    : (ReadHandlerSummary(connection, query.Handler, token), false);
            }
            var generator = await WebFormsReviewPreflightCommand.HashAsync("query-generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var response = new WebFormsEvidenceResponse("webforms-review-evidence-slice.v1", WebFormsReviewEvidenceIndex.Rule,
                "Tier2Structural", "local-only", "recovered-static-reports-original-run-not-completed", generator.Sha256, "",
                receipt.RunId, receipt.SourcePreflightSha256, receipt.SourceCheckpointSha256, admitted.Sha256,
                context.GeneratorSha256, context.BoundedInputSha256, query.Document, query.Pointer, query.Offset, query.Limit, query.Depth,
                QueryMaxNodes, QueryMaxBytes, truncated, result,
                ["Values are from a separate locally verified recovery bundle, not a completed original run.",
                 "Only the recovery receipt and indexed evidence bytes were read; no source, DLL, scan or runtime revalidation occurred.",
                 "No parity, authenticated-build or execution proof. Private identities and hashes must not be published."]);
            response = response with { BoundedInputSha256 = WebFormsReviewReportExecution.CanonicalHash(new
            { generator.Sha256, receiptSha256 = Digest(receiptBytes), indexSha256 = admitted.Sha256, query,
                resultSha256 = WebFormsReviewReportExecution.CanonicalHash(result, QueryMaxBytes, token) }, QueryMaxBytes, token) };
            using var bytes = new MemoryStream();
            await using (var bounded = new QueryOutputStream(bytes, token)) await JsonSerializer.SerializeAsync(bounded, response, QueryJson, token);
            if (Digest(receiptBytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token)) ||
                indexHash != await WebFormsReviewPreflightCommand.HashAsync("recovery-index", indexPath, admitted.Bytes, token)) throw Fail("RECOVERY_INPUT_CHANGED");
            RejectQuerySidecars(indexPath);
            await output.WriteAsync(Encoding.UTF8.GetString(bytes.GetBuffer(), 0, checked((int)bytes.Length))); return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { await error.WriteLineAsync("error: WEBFORMS_RECOVERY_QUERY_INPUT_OR_INDEX_INVALID"); return 1; }
    }
}
