using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Cli;

public sealed record WebFormsReviewArtifact(string RelativePath, long Bytes, string Sha256);
public sealed record WebFormsReviewCheckpoint(
    string SchemaVersion, string RuleId, string Visibility, string ClaimLevel,
    string RunId, int Sequence, string? PreviousCheckpointSha256,
    string GeneratorSha256, string PreflightSha256, string BoundedInputSha256,
    string State, string Attempt, string? ScanId, string? SourceSnapshotDigest,
    long FactCount, IReadOnlyList<string> Gaps, IReadOnlyList<WebFormsReviewArtifact> Artifacts,
    string CheckpointPayloadSha256, string RuntimeInputsSha256);

/// <summary>Owned, append-only fresh/compiled-attachment execution. No retained input is rewritten.</summary>
public static class WebFormsReviewExecutionCommand
{
    public const string RuleId = "workflow.webforms.compiled-review-execution.v1";
    public const string Schema = "webforms-compiled-review-checkpoint.v1";
    private const string Completed = "scan-completed-reports-pending";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, MaxDepth = 32
    };
    private static readonly string[] StandardRequired = ["scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log"];
    private static readonly string[] FreshRequired = [.. StandardRequired,
        SourceSnapshotRetention.ManifestName, SourceSnapshotRetention.RosterName];

    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error,
        LocalReviewScanRunner scanRunner, CancellationToken cancellationToken = default) =>
        await RunWithAttachmentAsync(args, output, error, scanRunner, WebFormsReviewAttachmentExecution.WriteAsync, cancellationToken);

    internal static async Task<int> RunWithAttachmentAsync(string[] args, TextWriter output, TextWriter error,
        LocalReviewScanRunner scanRunner, WebFormsReviewAttachmentRunner attachmentRunner, CancellationToken cancellationToken = default)
    {
        try
        {
            if (args.Length != 3 || args[0] is not ("run" or "resume") || args[1] != "--run") throw Fail("ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            if (!Directory.Exists(root)) throw Fail("RUN_UNAVAILABLE");
            var manifestPath = Path.Combine(root, "run-manifest.json");
            var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(manifestPath, 4_194_304, cancellationToken);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
            var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(bytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
            if (!Guid.TryParseExact(plan.RunId, "N", out _)) throw Fail("PREFLIGHT_INVALID");
            // Rebuild the inventory from the explicit config. A modified preflight
            // cannot silently replace its effective configuration or file inventory.
            var config = plan.Inputs?.SingleOrDefault(input => input.Role == "configuration") ?? throw Fail("PREFLIGHT_INVALID");
            var rebuilt = await WebFormsReviewPreflightCommand.BuildAsync(config.Path,
                Path.Combine(root, ".validation-" + Guid.NewGuid().ToString("N")), cancellationToken);
            var comparable = rebuilt with { RunId = plan.RunId };
            if (!JsonElement.DeepEquals(JsonSerializer.SerializeToElement(plan, JsonOptions),
                    JsonSerializer.SerializeToElement(comparable, JsonOptions))) throw Fail("PREFLIGHT_CHANGED_OR_TOOL_MISMATCH");
            var attach = plan.Configuration.Operation == "attach";
            if (!attach && plan.Configuration.Operation != "fresh") throw Fail("OPERATION_INVALID");
            // BuildAsync checked a prospective child. Check the actual run root's
            // separation too: the root cannot be an ancestor of any input root.
            foreach (var inputRoot in new[] { plan.Configuration.SourceRoot, plan.Configuration.PublishedRoot, plan.Configuration.ReceiptRoot, plan.Configuration.ParentScanRoot }.Where(path => path is not null))
                if (Within(root, inputRoot!) || Within(inputRoot!, root)) throw Fail("RUN_OVERLAPS_INPUT");
            var runtimeRoot = WebFormsReviewPreflightCommand.PhysicalPath(Path.GetDirectoryName(typeof(WebFormsReviewExecutionCommand).Assembly.Location)!);
            if (Within(runtimeRoot, root) || Within(root, runtimeRoot)) throw Fail("RUN_OVERLAPS_RUNTIME");
            var lockPath = OwnedPath(root, ".native-run.lock");
            // A retained empty lock file is harmless after a crash; FileShare.None
            // protects concurrent executors without timestamp/stale-lock guesses.
            using var runLock = new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            var preflightSha = Digest(bytes);
            var runtimeSha = await RuntimeDigestAsync(runtimeRoot, cancellationToken);
            var history = await ReadHistoryAsync(root, plan, preflightSha, runtimeSha, cancellationToken);
            if (args[0] == "run" && history.Checkpoint is not null) throw Fail("RUN_ALREADY_STARTED_USE_RESUME");
            var validated = await WebFormsReviewInputValidation.ValidateAsync(plan, cancellationToken);
            var validationGaps = validated.Gaps.Concat(["ExternalSdkInputsNotPinned", "SourceCommitDoesNotAssertCleanWorkingTree"])
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (history.Checkpoint?.State == Completed)
            {
                await VerifyArtifactsAsync(root, history.Checkpoint, plan, cancellationToken);
                if (attach)
                {
                    var retained = await ReadScanManifestAsync(OwnedPath(root, history.Checkpoint.Attempt + "/scan"), cancellationToken);
                    await WebFormsReviewAttachmentExecution.ValidateDerivedAsync(plan, validated, retained, cancellationToken);
                }
                await output.WriteLineAsync($"webFormsExecution={Completed};retainedSnapshot=true;sourceRescanned=false");
                await output.WriteLineAsync($"webFormsScan={OwnedPath(root, history.Checkpoint.Attempt + "/scan")}");
                return 0;
            }
            if (history.Sequence >= 254) throw Fail("CHECKPOINT_COUNT_LIMIT");
            var attempt = "attempts/" + Guid.NewGuid().ToString("N");
            var scanPath = OwnedPath(root, attempt + "/scan");
            Directory.CreateDirectory(OwnedPath(root, attempt));
            var policyDigest = PolicyDigest(plan, scanPath);
            var started = Checkpoint("scan-started", [], null, null, 0, validationGaps);
            history = await PublishAsync(root, started, cancellationToken);
            using var scanOutput = new StringWriter(CultureInfo.InvariantCulture);
            using var scanError = new StringWriter(CultureInfo.InvariantCulture);
            try
            {
                if (attach)
                    await attachmentRunner(plan, validated.ParentManifest ?? throw Fail("PARENT_UNAVAILABLE"), scanPath, cancellationToken);
                else
                {
                    var result = await scanRunner(ScanArguments(plan, scanPath), scanOutput, scanError, cancellationToken);
                    if (result != 0) throw Fail("SCAN_FAILED");
                }
                await WebFormsReviewInputValidation.RecheckAsync(plan, cancellationToken);
                var git = GitMetadataProvider.Detect(plan.Configuration.SourceRoot);
                if (git.CommitSha != plan.Configuration.SourceCommitSha) throw Fail("SOURCE_IDENTITY_CHANGED");
                var artifacts = await CollectArtifactsAsync(root, attempt, plan.Configuration.Budgets, cancellationToken);
                foreach (var required in Required(plan))
                    if (!artifacts.Any(artifact => artifact.RelativePath == attempt + "/scan/" + required)) throw Fail("SCAN_ARTIFACT_MISSING");
                var scanManifest = await ReadScanManifestAsync(scanPath, cancellationToken);
                var validationPlan = ProducedScanPlan(plan, scanPath, scanManifest.ScanId, artifacts, attempt);
                var checkedScan = await WebFormsReviewInputValidation.ValidateParentAsync(validationPlan, git, cancellationToken);
                if (attach)
                {
                    await WebFormsReviewAttachmentExecution.ValidateDerivedAsync(plan, validated, checkedScan.Manifest, cancellationToken);
                    await WebFormsReviewInputValidation.ValidateCompleteOrLegacySnapshotAsync(plan, validated.ParentManifest!, cancellationToken);
                }
                else await WebFormsReviewInputValidation.ValidateCompleteOrLegacySnapshotAsync(validationPlan, checkedScan.Manifest, cancellationToken);
                var publishGaps = checkedScan.Manifest.WebFormsPublishProvenance is null ? new[] { "PublishMapExecutionPending" }
                    : checkedScan.Manifest.WebFormsPublishProvenance.Status == "bound" ? [] : new[] { "PublishReceiptCoverageReduced" };
                var completed = Checkpoint(Completed, artifacts, checkedScan.Manifest.ScanId,
                    checkedScan.Manifest.SourceSnapshotDigest, checkedScan.Facts, validationGaps
                        .Concat(["UnifiedReportsPending"]).Concat(attach ? new[] { "CrossIndexParentJoinsPending" } : [])
                        .Concat(publishGaps).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray());
                await VerifyArtifactsAsync(root, completed, plan, cancellationToken);
                await WebFormsReviewInputValidation.RecheckAsync(plan, cancellationToken);
                if (attach) await WebFormsReviewInputValidation.ValidateCompleteOrLegacySnapshotAsync(plan, validated.ParentManifest!, cancellationToken);
                var gitAfter = GitMetadataProvider.Detect(plan.Configuration.SourceRoot);
                if (gitAfter.CommitSha != git.CommitSha || gitAfter.GitRootPath != git.GitRootPath ||
                    gitAfter.RemoteUrl != git.RemoteUrl || gitAfter.ScanRootRelativePath != git.ScanRootRelativePath) throw Fail("SOURCE_IDENTITY_CHANGED");
                if (runtimeSha != await RuntimeDigestAsync(runtimeRoot, cancellationToken)) throw Fail("RUNTIME_CHANGED");
                history = await PublishAsync(root, completed, cancellationToken);
                await output.WriteLineAsync($"webFormsExecution={Completed};facts={checkedScan.Facts};reviewOnly=true");
                await output.WriteLineAsync($"webFormsScan={scanPath}");
                return 0;
            }
            catch (Exception exception)
            {
                if (history.Checkpoint?.State == Completed) throw;
                // Keep failed attempt bytes. They are not admitted as completed
                // evidence; resume allocates a new owned attempt rather than overwriting.
                var cancelled = exception is OperationCanceledException || cancellationToken.IsCancellationRequested;
                await PublishAsync(root, Checkpoint(cancelled ? "scan-cancelled" : "scan-failed",
                    [], null, null, 0, [cancelled ? "Cancelled" : "ScanOrArtifactValidationFailed"]), CancellationToken.None);
                if (cancelled && exception is not OperationCanceledException)
                    throw new OperationCanceledException("Native execution cancelled.", exception, cancellationToken);
                throw;
            }

            WebFormsReviewCheckpoint Checkpoint(string state, IReadOnlyList<WebFormsReviewArtifact> artifacts,
                string? scanId, string? snapshot, long factCount, IReadOnlyList<string> gaps)
            {
                var bounded = Digest(JsonSerializer.SerializeToUtf8Bytes(new
                { preflightSha256 = preflightSha, runtimeInputsSha256 = runtimeSha, policySha256 = policyDigest, sourceSnapshotDigest = snapshot, artifacts }, JsonOptions));
                var checkpoint = new WebFormsReviewCheckpoint(Schema, RuleId, "local-only", "review-only-static-not-runtime", plan.RunId,
                    history.Sequence + 1, history.Sha256, plan.GeneratorSha256, preflightSha, bounded,
                    state, attempt, scanId, snapshot, factCount, gaps, artifacts, "", runtimeSha);
                return checkpoint with { CheckpointPayloadSha256 = PayloadDigest(checkpoint) };
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (ExecutionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (SourceSnapshotRetentionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (InvalidOperationException exception) when (exception.Message.StartsWith("COMPILED_ATTACHMENT_", StringComparison.Ordinal)
            || exception.Message.StartsWith("WEBFORMS_ATTACHMENT_", StringComparison.Ordinal)
            || exception.Message.StartsWith("WEBFORMS_REVIEW_", StringComparison.Ordinal))
        { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (Exception)
        { await error.WriteLineAsync("error: WEBFORMS_EXECUTION_INPUT_OUTPUT_OR_CHECKPOINT_INVALID"); return 1; }
    }

    internal static string[] ScanArguments(WebFormsReviewPreflightManifest plan, string output)
    {
        var config = plan.Configuration;
        var budget = config.Budgets;
        if (config.SourceFolders.Any(folder => folder.Contains('*'))) throw Fail("SOURCE_SCOPE_GLOB_UNSUPPORTED");
        var args = new List<string> { "--repo", config.SourceRoot, "--out", output, "--il-body-evidence", "--retain-source-snapshot" };
        Add("--source-snapshot-max-files", budget.MaxParentFacts);
        Add("--source-snapshot-max-source-bytes", budget.MaxTotalHashBytes);
        Add("--source-snapshot-max-roster-bytes", budget.MaxRetainedArtifactBytes);
        foreach (var folder in config.SourceFolders.Order(StringComparer.Ordinal)) Add("--include", folder == "." ? "**/*" : folder.Replace('\\', '/') + "/**/*");
        if (config.ProjectMode == "projectless")
            foreach (var extension in new[] { "sln", "csproj", "vbproj", "fsproj", "sqlproj" }) Add("--exclude", "**/*." + extension);
        if (config.ProjectMode == "projects") Add("--exclude", "**/*.sln");
        if (config.SolutionRelativePath is not null)
        { Add("--include", config.SolutionRelativePath.Replace('\\', '/')); Add("--solution", config.SolutionRelativePath); }
        foreach (var project in config.ProjectRelativePaths.Order(StringComparer.Ordinal))
        { Add("--include", project.Replace('\\', '/')); Add("--project", project); }
        foreach (var (role, option) in new[] { ("primary-assembly", "--compiled-input"), ("dependency-assembly", "--compiled-dependency"),
            ("binding-receipt", "--compiled-binding-receipt"), ("pdb", "--pdb-input") })
            foreach (var input in plan.Inputs.Where(input => input.Role == role).OrderBy(input => input.Path, StringComparer.Ordinal)) Add(option, input.Path);
        var publishReceipt = plan.Inputs.SingleOrDefault(input => input.Role == "publish-receipt");
        if (publishReceipt is not null)
        { Add("--webforms-publish-receipt", publishReceipt.Path); Add("--webforms-published-root", config.PublishedRoot); }
        Add("--compiled-max-artifacts", budget.MaxInputFiles);
        Add("--compiled-max-file-bytes", budget.MaxAssemblyBytes);
        Add("--compiled-max-text", budget.MetadataMaxText);
        Add("--compiled-max-work", budget.MetadataMaxWork);
        Add("--pdb-max-artifacts", budget.MaxInputFiles);
        Add("--pdb-max-file-bytes", budget.MaxAssemblyBytes);
        Add("--il-max-text", budget.IlMaxText);
        Add("--il-max-work", budget.IlMaxWork);
        return args.ToArray();
        void Add(string option, object value) { args.Add(option); args.Add(Convert.ToString(value, CultureInfo.InvariantCulture)!); }
    }

    private static WebFormsReviewPreflightManifest ProducedScanPlan(WebFormsReviewPreflightManifest plan, string scanPath,
        string scanId, IReadOnlyList<WebFormsReviewArtifact> artifacts, string attempt) => plan with
    {
        Configuration = plan.Configuration with { Operation = "attach", ParentScanRoot = scanPath }, ParentScanId = scanId,
        Inputs = plan.Inputs.Where(item => !item.Role.StartsWith("parent-", StringComparison.Ordinal)).Concat(Required(plan).Select(name =>
        {
            var artifact = artifacts.Single(item => item.RelativePath == attempt + "/scan/" + name);
            return new WebFormsReviewInput("parent-" + name, Path.Combine(scanPath, name), artifact.Bytes, artifact.Sha256);
        })).ToArray()
    };

    private static async Task<IReadOnlyList<WebFormsReviewArtifact>> CollectArtifactsAsync(string root, string attempt,
        WebFormsReviewBudgets budgets, CancellationToken token)
    {
        var scan = OwnedPath(root, attempt + "/scan");
        var result = new List<WebFormsReviewArtifact>();
        long total = 0;
        // Enumerate one directory at a time so symlinked directories are rejected
        // before traversal and the admission list is independently bounded.
        var pending = new Stack<string>(); pending.Push(scan);
        var entries = 0;
        while (pending.TryPop(out var directory))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(directory))
            {
                token.ThrowIfCancellationRequested();
                if (++entries > 256) throw Fail("OUTPUT_FILE_COUNT_LIMIT");
                var relative = Path.GetRelativePath(root, entry).Replace('\\', '/');
                OwnedPath(root, relative);
                if ((File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw Fail("OUTPUT_LINK_INVALID");
                if (Directory.Exists(entry)) { pending.Push(entry); continue; }
                if (entry.EndsWith("-wal", StringComparison.Ordinal) || entry.EndsWith("-shm", StringComparison.Ordinal) || entry.EndsWith("-journal", StringComparison.Ordinal)) throw Fail("OUTPUT_SIDECAR_INVALID");
                var input = await WebFormsReviewPreflightCommand.HashAsync("output", entry, budgets.MaxRetainedArtifactBytes, token);
                total = checked(total + input.Bytes);
                if (total > budgets.MaxTotalHashBytes) throw Fail("OUTPUT_HASH_BYTES_LIMIT");
                result.Add(new(relative, input.Bytes, input.Sha256));
            }
        }
        return result.OrderBy(item => item.RelativePath, StringComparer.Ordinal).ToArray();
    }

    private static async Task VerifyArtifactsAsync(string root, WebFormsReviewCheckpoint checkpoint, WebFormsReviewPreflightManifest plan, CancellationToken token)
    {
        var actual = await CollectArtifactsAsync(root, checkpoint.Attempt, plan.Configuration.Budgets, token);
        if (!actual.SequenceEqual(checkpoint.Artifacts)) throw Fail("OUTPUT_CHANGED");
        foreach (var required in Required(plan))
            if (!actual.Any(item => item.RelativePath == checkpoint.Attempt + "/scan/" + required)) throw Fail("SCAN_ARTIFACT_MISSING");
    }

    private sealed record History(int Sequence, string? Sha256, WebFormsReviewCheckpoint? Checkpoint);
    private static async Task<History> ReadHistoryAsync(string root, WebFormsReviewPreflightManifest plan, string preflightSha, string runtimeSha, CancellationToken token)
    {
        var directory = OwnedPath(root, "checkpoints");
        if (!Directory.Exists(directory)) return new(0, null, null);
        var paths = Directory.EnumerateFiles(directory, "*.json").Take(257).Order(StringComparer.Ordinal).ToArray();
        if (paths.Length > 256) throw Fail("CHECKPOINT_COUNT_LIMIT");
        History history = new(0, null, null);
        foreach (var path in paths)
        {
            OwnedPath(root, "checkpoints/" + Path.GetFileName(path));
            if (Path.GetFileName(path) != (history.Sequence + 1).ToString("D4", CultureInfo.InvariantCulture) + ".json") throw Fail("CHECKPOINT_SEQUENCE_INVALID");
            var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(path, 4_194_304, token);
            WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
            var checkpoint = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(bytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID");
            if (checkpoint.CheckpointPayloadSha256 != PayloadDigest(checkpoint)) throw Fail("CHECKPOINT_PAYLOAD_CHANGED");
            if (checkpoint.SchemaVersion != Schema || checkpoint.RuleId != RuleId || checkpoint.Visibility != "local-only" ||
                checkpoint.ClaimLevel != "review-only-static-not-runtime" || checkpoint.RunId != plan.RunId ||
                checkpoint.Sequence != history.Sequence + 1 || checkpoint.PreviousCheckpointSha256 != history.Sha256 ||
                checkpoint.PreflightSha256 != preflightSha || checkpoint.GeneratorSha256 != plan.GeneratorSha256 ||
                checkpoint.RuntimeInputsSha256 != runtimeSha ||
                checkpoint.Artifacts is null || checkpoint.Artifacts.Count > 256 || checkpoint.Gaps is null ||
                !ValidAttempt(checkpoint.Attempt) || checkpoint.State is not ("scan-started" or "scan-failed" or "scan-cancelled" or Completed)) throw Fail("CHECKPOINT_INVALID");
            var scanPath = OwnedPath(root, checkpoint.Attempt + "/scan");
            var policyDigest = PolicyDigest(plan, scanPath);
            var expectedDigest = Digest(JsonSerializer.SerializeToUtf8Bytes(new
            { preflightSha256 = preflightSha, runtimeInputsSha256 = runtimeSha, policySha256 = policyDigest, sourceSnapshotDigest = checkpoint.SourceSnapshotDigest, artifacts = checkpoint.Artifacts }, JsonOptions));
            if (checkpoint.BoundedInputSha256 != expectedDigest || (checkpoint.State == Completed &&
                (string.IsNullOrWhiteSpace(checkpoint.ScanId) || checkpoint.SourceSnapshotDigest?.Length != 64 || checkpoint.FactCount < 1)) ||
                (checkpoint.State != Completed && (checkpoint.Artifacts.Count != 0 || checkpoint.ScanId is not null || checkpoint.SourceSnapshotDigest is not null || checkpoint.FactCount != 0))) throw Fail("CHECKPOINT_INVALID");
            if (history.Checkpoint?.State == Completed) throw Fail("CHECKPOINT_AFTER_COMPLETION_INVALID");
            if (checkpoint.State != "scan-started" &&
                (history.Checkpoint?.State != "scan-started" || history.Checkpoint.Attempt != checkpoint.Attempt)) throw Fail("CHECKPOINT_PHASE_INVALID");
            if (checkpoint.State == "scan-started" && history.Checkpoint?.Attempt == checkpoint.Attempt) throw Fail("CHECKPOINT_ATTEMPT_REUSE_INVALID");
            history = new(checkpoint.Sequence, Digest(bytes), checkpoint);
        }
        return history;
    }

    private static IReadOnlyList<string> Required(WebFormsReviewPreflightManifest plan) =>
        plan.Configuration.Operation == "attach" ? StandardRequired : FreshRequired;

    private static string PolicyDigest(WebFormsReviewPreflightManifest plan, string output) =>
        plan.Configuration.Operation == "attach"
            ? Digest(JsonSerializer.SerializeToUtf8Bytes(WebFormsReviewAttachmentExecution.Options(plan, output), JsonOptions))
            : Digest(JsonSerializer.SerializeToUtf8Bytes(ScanArguments(plan, output), JsonOptions));

    private static async Task<ScanManifest> ReadScanManifestAsync(string root, CancellationToken token)
    {
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(Path.Combine(root, "scan-manifest.json"), 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        return JsonSerializer.Deserialize<ScanManifest>(bytes, JsonOptions) ?? throw Fail("SCAN_MANIFEST_INVALID");
    }

    private static async Task<History> PublishAsync(string root, WebFormsReviewCheckpoint checkpoint, CancellationToken token)
    {
        var directory = OwnedPath(root, "checkpoints"); Directory.CreateDirectory(directory);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(checkpoint, JsonOptions);
        var staging = OwnedPath(root, "checkpoints/.pending-" + Guid.NewGuid().ToString("N"));
        await using (var stream = new FileStream(staging, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        { await stream.WriteAsync(bytes, token); await stream.FlushAsync(token); stream.Flush(flushToDisk: true); }
        File.Move(staging, OwnedPath(root, "checkpoints/" + checkpoint.Sequence.ToString("D4", CultureInfo.InvariantCulture) + ".json"));
        return new(checkpoint.Sequence, Digest(bytes), checkpoint);
    }

    private static bool ValidAttempt(string value) => value is not null && value.StartsWith("attempts/", StringComparison.Ordinal)
        && value.Length == 41 && Guid.TryParseExact(value[9..], "N", out _);
    private static string OwnedPath(string root, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || Path.IsPathRooted(relative) || relative.Contains(':') ||
            relative.Split('/').Any(segment => segment is "" or "." or "..")) throw Fail("OUTPUT_LOCATOR_INVALID");
        var path = Path.GetFullPath(Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!Within(root, path) || WebFormsReviewPreflightCommand.PhysicalPath(path) != path) throw Fail("OUTPUT_LINK_INVALID");
        return path;
    }
    private static bool Within(string root, string path) => path.StartsWith(Path.TrimEndingDirectorySeparator(root) + Path.DirectorySeparatorChar,
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal) ||
        string.Equals(root, path, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static string Digest(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static string PayloadDigest(WebFormsReviewCheckpoint checkpoint) =>
        Digest(JsonSerializer.SerializeToUtf8Bytes(checkpoint with { CheckpointPayloadSha256 = "" }, JsonOptions));

    internal static async Task<string> RuntimeDigestAsync(string directory, CancellationToken token)
    {
        var root = WebFormsReviewPreflightCommand.PhysicalPath(directory);
        var files = new List<WebFormsReviewArtifact>();
        var pending = new Stack<string>(); pending.Push(root);
        long total = 0;
        var entries = 0;
        while (pending.TryPop(out var folder))
        {
            foreach (var entry in Directory.EnumerateFileSystemEntries(folder))
            {
                token.ThrowIfCancellationRequested();
                if (++entries > 4096 || (File.GetAttributes(entry) & FileAttributes.ReparsePoint) != 0) throw Fail("RUNTIME_INVENTORY_INVALID");
                if (Directory.Exists(entry)) { pending.Push(entry); continue; }
                var extension = Path.GetExtension(entry).ToLowerInvariant();
                if (extension is not (".dll" or ".exe" or ".so" or ".dylib") &&
                    !entry.EndsWith(".deps.json", StringComparison.OrdinalIgnoreCase) && !entry.EndsWith(".runtimeconfig.json", StringComparison.OrdinalIgnoreCase)) continue;
                if (files.Count >= 512) throw Fail("RUNTIME_FILE_COUNT_LIMIT");
                var input = await WebFormsReviewPreflightCommand.HashAsync("runtime", entry, 268_435_456, token);
                total = checked(total + input.Bytes);
                if (total > 2_147_483_648) throw Fail("RUNTIME_HASH_BYTES_LIMIT");
                files.Add(new(Path.GetRelativePath(root, entry).Replace('\\', '/'), input.Bytes, input.Sha256));
            }
        }
        if (files.Count == 0) throw Fail("RUNTIME_UNAVAILABLE");
        return Digest(JsonSerializer.SerializeToUtf8Bytes(new
        { runtimeVersion = Environment.Version.ToString(), files = files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray() }, JsonOptions));
    }
    private static ExecutionException Fail(string suffix) => new("WEBFORMS_EXECUTION_" + suffix);
    private sealed class ExecutionException(string code) : Exception(code);
}
