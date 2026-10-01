using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Cli;

public sealed record WebFormsMigrationHandoff(string SchemaVersion, string RuleId, string Visibility,
    string ClaimLevel, string GeneratorSha256, string BoundedInputSha256, string RunId,
    string PreflightSha256, string CheckpointSha256, string Handler,
    string ApplicationHandoffSha256, string CompiledHandoffSha256,
    IReadOnlyList<WebFormsReviewArtifact> Artifacts, IReadOnlyList<string> Limitations);

public static partial class WebFormsReviewExecutionCommand
{
    internal const string MigrationName = "migration-handoff.local.json";
    internal const string MigrationRule = "workflow.webforms.migration-handoff.v1";

    // One fresh owned root; no automatic recovery, historical folder discovery,
    // website rebuild, SQL execution or claim that a partial run is complete.
    public static async Task<int> MigrationReviewAsync(string[] args, TextWriter output, TextWriter error,
        LocalReviewScanRunner scanRunner, CancellationToken token = default)
    {
        var stage = "admission";
        try
        {
            if (args.Length is not (7 or 9 or 13) || args[0] != "migration-review" || args[1] != "--config" ||
                args[3] != "--handler" || args[5] != "--out" ||
                (args.Length == 9 && args[7] != "--attest-exact-source-commit") ||
                (args.Length == 13 && (args[7] != "--proof-root" || args[9] != "--published-root" || args[11] != "--source-base")))
                throw Fail("MIGRATION_ARGUMENT_INVALID");
            var handler = args[4];
            if (handler.Length is < 1 or > 128 || handler.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                throw Fail("MIGRATION_HANDLER_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[6]);
            // Admission validates the entire destination against authoritative inputs.
            var import = args.Length == 13;
            if (import)
            {
                if (await WebFormsProofImportCommand.RunAsync(["import-proof", "--config", args[2], "--proof-root", args[8],
                        "--published-root", args[10], "--out", root, "--source-base", args[12], "--diagnose"], output, error, token) != 0) return 1;
            }
            else await WebFormsReviewPreflightCommand.BuildAsync(args[2], root, token);
            if (Path.Exists(root)) throw Fail("MIGRATION_OUTPUT_EXISTS");
            var runtime = Path.GetDirectoryName(typeof(WebFormsReviewExecutionCommand).Assembly.Location)!;
            if (Within(root, runtime) || Within(runtime, root)) throw Fail("MIGRATION_OUTPUT_OVERLAPS_TOOL");
            Directory.CreateDirectory(Path.GetDirectoryName(root)!);
            var reservation = root + ".pending-" + Guid.NewGuid().ToString("N");
            Directory.CreateDirectory(reservation);
            Directory.Move(reservation, root);
            var configuration = args[2];
            if (import)
            {
                stage = "proof-import";
                var configurationRoot = Path.Combine(root, "configuration");
                if (await WebFormsProofImportCommand.RunAsync(["import-proof", "--config", args[2], "--proof-root", args[8],
                        "--published-root", args[10], "--out", configurationRoot, "--source-base", args[12]], output, error, token) != 0) return 1;
                configuration = Path.Combine(configurationRoot, "review-config.local.json");
            }
            var start = new List<string> { "start", "--config", configuration, "--out", Path.Combine(root, "review") };
            if (args.Length == 9) start.AddRange([args[7], args[8]]);
            stage = "native-review";
            if (await WebFormsReviewStartCommand.RunAsync(start.ToArray(), output, error, scanRunner, token) != 0) return 1;
            var run = Path.Combine(root, "review", "run");
            var retained = await ReadCompletedRunAsync(run, token);
            var bundle = OwnedPath(run, retained.History.Checkpoint!.Reports!.ReportAttempt);
            stage = "tool-retention";
            if (await RetainToolAsync(["retain-tool", "--run", run, "--out", Path.Combine(root, "tool")], output, error, token) != 0) return 1;
            var focused = Path.Combine(root, "handler");
            stage = "handler-requery";
            if (await RequeryHandlerAsync(["requery-handler", "--run", run, "--bundle", bundle,
                    "--handler", handler, "--out", focused, "--surface-name", "DbDataAdapter.Fill"], output, error, token) != 0) return 1;
            var budget = retained.Plan.Configuration.Budgets;
            stage = "evidence-index";
            var appPath = Path.Combine(bundle, WebFormsReviewReportExecution.HandoffName);
            var compiledPath = Path.Combine(focused, "compiled-paths.handoff.local.json");
            var app = await WebFormsReviewPreflightCommand.HashAsync("application", appPath, budget.MaxRetainedArtifactBytes, token);
            var compiled = await WebFormsReviewPreflightCommand.HashAsync("compiled", compiledPath, budget.MaxRetainedArtifactBytes, token);
            // This separate index pairs the original application evidence with the
            // focused compiled query. The native checkpoint-owned index is untouched.
            var index = await WebFormsReviewEvidenceIndex.WriteAsync(root, retained.Plan.RunId,
                app.Sha256, compiled.Sha256, budget.MaxRetainedArtifactBytes, budget.MaxRetainedArtifactBytes,
                token, WebFormsReviewEvidenceIndex.NewPlanMaxNodes, appPath, compiledPath);
            await File.WriteAllTextAsync(Path.Combine(root, "START-HERE.md"), MigrationInstructions, token);
            stage = "handoff-publication";
            var artifacts = new List<WebFormsReviewArtifact> { index };
            foreach (var relative in new[] { "START-HERE.md", "handler/handler-requery.local.json",
                         "handler/compiled-paths.handoff.local.json", "handler/compiled-paths.local.html", "tool/tracemap.dll" })
            {
                var item = await WebFormsReviewPreflightCommand.HashAsync("handoff", OwnedPath(root, relative), budget.MaxRetainedArtifactBytes, token);
                artifacts.Add(new(relative, item.Bytes, item.Sha256));
            }
            var rechecked = await ReadCompletedRunAsync(run, token);
            if (retained.PreflightSha256 != rechecked.PreflightSha256 || retained.History.Sha256 != rechecked.History.Sha256 ||
                !retained.Files.SequenceEqual(rechecked.Files)) throw Fail("MIGRATION_RUN_CHANGED");
            var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            if (artifacts.Single(item => item.RelativePath == "handler/compiled-paths.handoff.local.json").Sha256 != compiled.Sha256 ||
                artifacts.Single(item => item.RelativePath == "tool/tracemap.dll").Sha256 != generator.Sha256)
                throw Fail("MIGRATION_ARTIFACT_CHANGED");
            var receipt = new WebFormsMigrationHandoff("webforms-migration-handoff.v1", MigrationRule, "local-only",
                "partial-static-migration-review-not-parity", generator.Sha256, "", retained.Plan.RunId,
                retained.PreflightSha256, retained.History.Sha256!, handler, app.Sha256, compiled.Sha256, artifacts,
                ["Application evidence retains the original page/query scope. Compiled evidence is one exact handler to DbDataAdapter.Fill in mixed mode.",
                 "Depth, cycle, work and path limits remain in the evidence. Matching historical method sequences is not runtime parity.",
                 "Command-text hashes and command-type candidates are not readable procedure names, complete SQL parameters or execution proof.",
                 "The folder includes review evidence and the pinned tool, not the external .NET runtime, application source or published website.",
                 "Local hashes are integrity commitments, not authentication. Keep this folder private; no cleanup or relocation authority is granted."]);
            receipt = receipt with { BoundedInputSha256 = MigrationHash(receipt) };
            await File.WriteAllTextAsync(Path.Combine(root, MigrationName), JsonSerializer.Serialize(receipt, JsonOptions), token);
            await output.WriteLineAsync($"migrationReview=ready-for-partial-static-review;entry={Path.Combine(root, "START-HERE.md")};no-sql-executed");
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { await error.WriteLineAsync($"error: WEBFORMS_MIGRATION_FAILED;stage={stage};partial-output-preserved;no-completed-handoff-claim"); return 1; }
    }

    internal static string MigrationHash(WebFormsMigrationHandoff receipt) =>
        WebFormsReviewReportExecution.CanonicalHash(receipt with { BoundedInputSha256 = "" }, 4_194_304, CancellationToken.None);

    public static async Task<int> QueryMigrationAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length < 3 || args[0] != "query-migration" || args[1] != "--root") throw Fail("MIGRATION_QUERY_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            var query = ParseQuery(args[3..]);
            var receiptPath = OwnedPath(root, MigrationName);
            using var receiptLock = new FileStream(receiptPath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
            var receipt = JsonSerializer.Deserialize<WebFormsMigrationHandoff>(bytes, JsonOptions) ?? throw Fail("MIGRATION_RECEIPT_INVALID");
            if (receipt.SchemaVersion != "webforms-migration-handoff.v1" || receipt.RuleId != MigrationRule ||
                receipt.Visibility != "local-only" || receipt.ClaimLevel != "partial-static-migration-review-not-parity" ||
                receipt.Artifacts.Count != 6 || receipt.BoundedInputSha256 != MigrationHash(receipt)) throw Fail("MIGRATION_RECEIPT_INVALID");
            var admitted = receipt.Artifacts.Single(item => item.RelativePath == WebFormsReviewEvidenceIndex.Name);
            var path = OwnedPath(root, admitted.RelativePath);
            RejectQuerySidecars(path);
            var actual = await WebFormsReviewPreflightCommand.HashAsync("migration-index", path, admitted.Bytes, token);
            if (actual.Bytes != admitted.Bytes || actual.Sha256 != admitted.Sha256) throw Fail("MIGRATION_INDEX_CHANGED");
            WebFormsReviewEvidenceIndex.Context context;
            WebFormsEvidenceItem result; bool truncated;
            using (var connection = new SqliteConnection($"Data Source={new Uri(path).AbsoluteUri}?immutable=1;Mode=ReadOnly;Pooling=False"))
            {
                await connection.OpenAsync(token);
                context = WebFormsReviewEvidenceIndex.ReadContext(connection, receipt.RunId, receipt.ApplicationHandoffSha256, receipt.CompiledHandoffSha256);
                if (admitted.Bytes > context.MaxIndexBytes) throw Fail("MIGRATION_INDEX_LIMIT");
                (result, truncated) = ReadQuery(connection, query, token);
            }
            var generator = await WebFormsReviewPreflightCommand.HashAsync("generator", typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var response = new WebFormsEvidenceResponse("webforms-review-evidence-slice.v1", WebFormsReviewEvidenceIndex.Rule,
                "Tier2Structural", "local-only", receipt.ClaimLevel, generator.Sha256, "", receipt.RunId,
                receipt.PreflightSha256, receipt.CheckpointSha256, admitted.Sha256, context.GeneratorSha256,
                context.BoundedInputSha256, query.Document, query.Pointer, query.Offset, query.Limit, query.Depth,
                QueryMaxNodes, QueryMaxBytes, truncated, result, receipt.Limitations);
            response = response with { BoundedInputSha256 = WebFormsReviewReportExecution.CanonicalHash(new
            { generator.Sha256, receiptSha256 = Digest(bytes), indexSha256 = admitted.Sha256, query,
                resultSha256 = WebFormsReviewReportExecution.CanonicalHash(result, QueryMaxBytes, token) }, QueryMaxBytes, token) };
            using var resultBytes = new MemoryStream();
            await using (var bounded = new QueryOutputStream(resultBytes, token)) await JsonSerializer.SerializeAsync(bounded, response, QueryJson, token);
            if (Digest(bytes) != Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(receiptPath, 4_194_304, token)) ||
                actual != await WebFormsReviewPreflightCommand.HashAsync("migration-index", path, admitted.Bytes, token)) throw Fail("MIGRATION_INPUT_CHANGED");
            RejectQuerySidecars(path);
            await output.WriteAsync(Encoding.UTF8.GetString(resultBytes.GetBuffer(), 0, checked((int)resultBytes.Length)));
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        { await error.WriteLineAsync("error: WEBFORMS_MIGRATION_QUERY_INPUT_OR_INDEX_INVALID"); return 1; }
    }

    private const string MigrationInstructions = """
        # Start here: private Web Forms migration evidence

        Give this entire folder to the authorized local reviewer. It contains the native
        run in `review/run`, focused handler evidence in `handler`, a pinned tool in
        `tool`, and `migration-handoff.local.json`. Do not assemble other run folders.
        A missing or invalid handoff receipt means packaging did not complete. Never choose a
        substitute by timestamp. This is partial static evidence, not migration approval.

        ## Claude review instructions

        Work read-only from this folder. Do not run the application, SQL, a scanner,
        traversal, repair, resume, cleanup or external service. Do not edit source.
        Read the small handoff receipt and handler receipt first. Stop on invalid or
        missing artifacts. Use the owner-approved pinned tool and installed .NET runtime:

        ```powershell
        dotnet ./tool/tracemap.dll webforms-review query-migration --root . --document application --pointer /packet/summary --limit 5 --depth 2
        dotnet ./tool/tracemap.dll webforms-review query-migration --root . --document compiled --pointer /header/query --limit 10 --depth 2
        dotnet ./tool/tracemap.dll webforms-review query-migration --root . --document compiled --pointer /chains --limit 5 --depth 2
        ```

        Query-migration verifies the package receipt and indexed evidence, not external
        application inputs. Do not load full handoff JSON, facts or SQLite into context.
        Page through bounded slices using childCount/nextOffset; omitted is not absent.
        Follow chains' variantIndexes into /variants and nodeReferences/edgeReferences
        into /nodes and /edges. Escape JSON Pointer keys (~ as ~0 and / as ~1).
        Inspect commandBinding and commandTextFromPath on terminal nodes, and all
        transition evidence. Source bridges remain candidates, not proven IL calls.

        Application pages/events retain broad original scope; compiled paths are a
        focused mixed source/IL handler-to-Fill query. Do not silently merge scopes.
        Report migration-relevant behavior, evidence pointers, rule IDs/tiers,
        unresolved questions and specific additional source evidence needed.
        Distinguish command fingerprints/type candidates from readable procedure names,
        parameter names/values and actual execution. Do not call parameters missing
        merely because coverage is unverified. Depth/cycle limits do not prove omitted
        database calls. Never infer runtime or historical parity from matching counts.
        Keep identities, hashes and paths private. Produce an assessment, not approval.
        """;
}
