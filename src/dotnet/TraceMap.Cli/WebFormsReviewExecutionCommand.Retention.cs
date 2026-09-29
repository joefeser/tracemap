using System.Text.Json;

namespace TraceMap.Cli;

public sealed record WebFormsReviewLocation(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256, string RunId, string PreflightSha256,
    string CheckpointSha256, string ArtifactsSha256, string OriginalPolicyRoot, string CurrentRoot,
    string CopiedFromRoot, string? PreviousLocationSha256, IReadOnlyList<WebFormsReviewArtifact> CopiedFiles,
    IReadOnlyList<string> Limitations, string PayloadSha256);

public sealed record WebFormsReviewProtectedDependency(string Role, string Path, string Status);
public sealed record WebFormsReviewRetentionPlan(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256, string RunId, string RunRoot,
    string PreflightSha256, string CheckpointSha256, string? LocationSha256,
    string Mode, IReadOnlyList<WebFormsReviewArtifact> RetainedFiles,
    IReadOnlyList<WebFormsReviewProtectedDependency> ProtectedDependencies,
    IReadOnlyList<string> DeletionCandidates, IReadOnlyList<string> Limitations);

public static partial class WebFormsReviewExecutionCommand
{
    internal const string LocationName = "run-location.local.json";
    internal const string LocationRule = "workflow.webforms.compiled-review-location.v1";
    internal const string RetentionRule = "workflow.webforms.compiled-review-retention.v1";
    private const string LocationSchema = "webforms-review-location.v1";
    private const string RetentionSchema = "webforms-review-retention-plan.v1";

    /// <summary>Explicit completed-run copy or read-only dependency plan. Never deletes or scans.</summary>
    public static async Task<int> RetentionAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            var relocating = args.FirstOrDefault() == "relocate";
            if ((!relocating && args.FirstOrDefault() != "retention-plan") ||
                args.Length != (relocating ? 5 : 3) || args[1] != "--run" || (relocating && args[3] != "--out"))
                throw Fail("RETENTION_ARGUMENT_INVALID");
            var root = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            using var runLock = new FileStream(OwnedPath(root, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
            var retained = await ReadCompletedRunAsync(root, token);
            var generator = await WebFormsReviewPreflightCommand.HashAsync("location-generator",
                typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            if (!relocating)
            {
                var dependencies = ProtectedDependencies(root, retained.Plan, retained.Location, retained.History.Checkpoint?.ToolDistribution);
                var bounded = Digest(JsonSerializer.SerializeToUtf8Bytes(new
                {
                    generator.Sha256, retained.Plan.RunId, root, retained.PreflightSha256,
                    checkpointSha256 = retained.History.Sha256, retained.LocationSha256,
                    files = retained.Files, dependencies, mode = "dry-run-protect-only"
                }, JsonOptions));
                var plan = new WebFormsReviewRetentionPlan(RetentionSchema, RetentionRule, "Tier2Structural",
                    "local-only", "retention-plan-not-deletion-approval", generator.Sha256, bounded,
                    retained.Plan.RunId, root, retained.PreflightSha256, retained.History.Sha256!, retained.LocationSha256,
                    "dry-run-protect-only", retained.Files, dependencies, [],
                    ["Only this explicitly selected completed native run was inspected; no TEMP discovery or cleanup occurred.",
                     "Retained artifacts were hash-verified. External dependency paths are protected declarations, not freshly validated source or DLL bytes.",
                     "Unknown files, abandoned attempts, other runs and previous locations remain protected; none are deletion candidates.",
                     "Original tool/runtime locators, when retained, are protected declarations rather than current availability. Historical absence remains unknown; preserve tool installations and proof wrappers. External SDK bytes are not pinned.",
                     "Private paths and hashes are local integrity commitments, not authenticated build provenance or shareable evidence."]);
                var rechecked = await ReadCompletedRunAsync(root, token);
                if (retained.History.Sha256 != rechecked.History.Sha256 || retained.PreflightSha256 != rechecked.PreflightSha256 ||
                    retained.LocationSha256 != rechecked.LocationSha256 || !retained.Files.SequenceEqual(rechecked.Files))
                    throw Fail("LOCATION_SOURCE_CHANGED");
                var planBytes = JsonSerializer.SerializeToUtf8Bytes(plan, JsonOptions);
                if (planBytes.Length > 16_777_216) throw Fail("RETENTION_PLAN_BYTES_LIMIT");
                await output.WriteLineAsync(System.Text.Encoding.UTF8.GetString(planBytes));
                return 0;
            }

            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            ValidateRelocationDestination(root, destination, retained.Plan, retained.Location, retained.History.Checkpoint?.ToolDistribution);
            // Reserve an owned sibling. On failure it remains inspectable; no old proof is removed.
            var staging = destination + ".pending-" + Guid.NewGuid().ToString("N");
            ValidateRelocationDestination(root, staging, retained.Plan, retained.Location, retained.History.Checkpoint?.ToolDistribution);
            Directory.CreateDirectory(staging);
            using (var destinationLock = new FileStream(OwnedPath(staging, ".native-run.lock"), FileMode.CreateNew,
                FileAccess.ReadWrite, FileShare.None))
            {
                foreach (var file in retained.Files)
                    await CopyVerifiedAsync(root, staging, file, token);
                var original = retained.Location?.OriginalPolicyRoot ?? root;
                var location = new WebFormsReviewLocation(LocationSchema, LocationRule, "Tier2Structural", "local-only",
                    "review-only-static-not-runtime", generator.Sha256, "", retained.Plan.RunId,
                    retained.PreflightSha256, retained.History.Sha256!, ScanArtifactsDigest(retained.History.Checkpoint!),
                    original, destination, root, retained.LocationSha256, retained.Files,
                    ["Explicit byte-for-byte copy of admitted completed artifacts and checkpoint metadata; original run is preserved.",
                     "Original policy paths are retained only to validate existing checkpoint digests; all owned artifact reads use CurrentRoot.",
                     "Source, published DLLs, receipts, parent scans and original tool/SDK dependencies are not relocated or freshly revalidated.",
                     "Unknown files and abandoned attempts were not copied; preserve the source run. No deletion is authorized.",
                     "Stored identities may contain original private paths. This is not a privacy projection, build attestation, runtime proof or all-pages claim."], "");
                location = location with { BoundedInputSha256 = LocationInputDigest(location) };
                location = location with { PayloadSha256 = LocationPayloadDigest(location) };
                var bytes = JsonSerializer.SerializeToUtf8Bytes(location, JsonOptions);
                if (bytes.Length > 4_194_304) throw Fail("LOCATION_BYTES_LIMIT");
                await using var stream = new FileStream(OwnedPath(staging, LocationName), FileMode.CreateNew, FileAccess.Write, FileShare.None);
                await stream.WriteAsync(bytes, token); await stream.FlushAsync(token); stream.Flush(true);
                // Re-read source metadata and every admitted artifact before publishing the copy.
                var rechecked = await ReadCompletedRunAsync(root, token);
                if (retained.History.Sha256 != rechecked.History.Sha256 || retained.PreflightSha256 != rechecked.PreflightSha256 ||
                    retained.LocationSha256 != rechecked.LocationSha256 || !retained.Files.SequenceEqual(rechecked.Files))
                    throw Fail("LOCATION_SOURCE_CHANGED");
                token.ThrowIfCancellationRequested();
            }
            ValidateRelocationDestination(root, destination, retained.Plan, retained.Location, retained.History.Checkpoint?.ToolDistribution);
            Directory.Move(staging, destination);
            using var publishedLock = new FileStream(OwnedPath(destination, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
            var published = await ReadCompletedRunAsync(destination, token);
            if (!published.Files.SequenceEqual(retained.Files)) throw Fail("LOCATION_COPY_CHANGED");
            await output.WriteLineAsync("webFormsRelocation=verified-copy;originalPreserved=true;externalInputsMoved=false;cleanup=false");
            await output.WriteLineAsync($"webFormsRun={destination}");
            await output.WriteLineAsync($"webFormsWorkbench={OwnedPath(destination, published.History.Checkpoint!.Reports!.ReportAttempt + "/index.html")}");
            return 0;
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ExecutionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (Exception) { await error.WriteLineAsync("error: WEBFORMS_NATIVE_RETENTION_INPUT_OR_COPY_INVALID"); return 1; }
    }

    private sealed record CompletedRun(WebFormsReviewPreflightManifest Plan, string PreflightSha256, History History,
        WebFormsReviewLocation? Location, string? LocationSha256, IReadOnlyList<WebFormsReviewArtifact> Files);

    private static async Task<CompletedRun> ReadCompletedRunAsync(string root, CancellationToken token)
    {
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "run-manifest.json"), 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var plan = JsonSerializer.Deserialize<WebFormsReviewPreflightManifest>(bytes, JsonOptions) ?? throw Fail("PREFLIGHT_INVALID");
        if (plan.SchemaVersion != WebFormsReviewPreflightCommand.ManifestSchema || plan.RuleId != WebFormsReviewPreflightCommand.RuleId ||
            plan.Visibility != "local-only" || plan.ClaimLevel != "preflight-only-not-executed" ||
            !Guid.TryParseExact(plan.RunId, "N", out _) || !ValidSha(plan.GeneratorSha256) ||
            plan.Inputs is null || plan.Inputs.Count > 20_480 ||
            plan.Configuration?.Budgets is null || plan.Configuration.Budgets.MaxTotalHashBytes < 1 ||
            plan.Configuration.Budgets.MaxRetainedArtifactBytes < 1) throw Fail("PREFLIGHT_INVALID");
        var preflight = Digest(bytes);
        var firstBytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, "checkpoints/0001.json"), 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(firstBytes);
        var first = JsonSerializer.Deserialize<WebFormsReviewCheckpoint>(firstBytes, JsonOptions) ?? throw Fail("CHECKPOINT_INVALID");
        var history = await ReadHistoryAsync(root, plan, preflight, first.RuntimeInputsSha256, token);
        if (history.Checkpoint?.State != WebFormsReviewReportExecution.Completed || history.Checkpoint.Reports is null)
            throw Fail("LOCATION_COMPLETED_REPORT_REQUIRED");
        await VerifyArtifactsAsync(root, history.Checkpoint, plan, token);
        var files = new List<WebFormsReviewArtifact>(history.Checkpoint.Artifacts) { new("run-manifest.json", bytes.Length, preflight) };
        for (var index = 1; index <= history.Sequence; index++)
        {
            var relative = "checkpoints/" + index.ToString("D4", System.Globalization.CultureInfo.InvariantCulture) + ".json";
            var file = await WebFormsReviewPreflightCommand.HashAsync("checkpoint", OwnedPath(root, relative), 4_194_304, token);
            files.Add(new(relative, file.Bytes, file.Sha256));
        }
        var readme = OwnedPath(root, "README.md");
        if (File.Exists(readme))
        {
            var file = await WebFormsReviewPreflightCommand.HashAsync("readme", readme, 4_194_304, token);
            files.Add(new("README.md", file.Bytes, file.Sha256));
        }
        var ordered = files.OrderBy(file => file.RelativePath, StringComparer.Ordinal).ToArray();
        if (ordered.Sum(file => file.Bytes) > plan.Configuration.Budgets.MaxTotalHashBytes) throw Fail("LOCATION_TOTAL_BYTES_LIMIT");
        var location = await ReadLocationAsync(root, plan, preflight, token);
        string? locationSha = null;
        if (location is not null)
        {
            locationSha = Digest(await WebFormsReviewPreflightCommand.ReadSmallAsync(OwnedPath(root, LocationName), 4_194_304, token));
            if (!ordered.SequenceEqual(location.CopiedFiles)) throw Fail("LOCATION_COPY_ROSTER_CHANGED");
        }
        return new(plan, preflight, history, location, locationSha, ordered);
    }

    private static IReadOnlyList<WebFormsReviewProtectedDependency> ProtectedDependencies(string root, WebFormsReviewPreflightManifest plan,
        WebFormsReviewLocation? location = null, WebFormsReviewToolDistribution? tool = null)
    {
        var result = new List<WebFormsReviewProtectedDependency> { new("owned-run", root, "hash-verified-retain") };
        if (location is not null)
        {
            result.Add(new("original-policy-root", location.OriginalPolicyRoot, "previous-proof-location-retain-not-revalidated"));
            result.Add(new("copied-from-root", location.CopiedFromRoot, "previous-proof-location-retain-not-revalidated"));
        }
        foreach (var (role, path) in new[] { ("source-root", plan.Configuration.SourceRoot), ("published-root", plan.Configuration.PublishedRoot),
            ("receipt-root", plan.Configuration.ReceiptRoot), ("parent-scan-root", plan.Configuration.ParentScanRoot) })
            if (path is not null) result.Add(new(role, path, "declared-external-retain-not-revalidated"));
        foreach (var input in plan.Inputs)
            result.Add(new(input.Role, input.Path, "pinned-external-retain-not-revalidated"));
        result.Add(new("current-tool-distribution", Path.GetDirectoryName(typeof(WebFormsReviewExecutionCommand).Assembly.Location)!,
            "current-reader-retain-not-original-generator-availability"));
        if (tool is not null)
        {
            result.Add(new("original-tool-distribution", tool.DistributionRoot, "checkpointed-declaration-retain-not-revalidated"));
            result.Add(new("original-dotnet-runtime", tool.DotnetRuntimeRoot, "declared-external-retain-bytes-not-pinned"));
        }
        return result.Distinct().OrderBy(item => item.Role, StringComparer.Ordinal).ThenBy(item => item.Path, StringComparer.Ordinal).ToArray();
    }

    private static void ValidateRelocationDestination(string source, string destination, WebFormsReviewPreflightManifest plan,
        WebFormsReviewLocation? location = null, WebFormsReviewToolDistribution? tool = null)
    {
        if (File.Exists(destination) || Directory.Exists(destination)) throw Fail("LOCATION_OUTPUT_EXISTS");
        if (Within(source, destination) || Within(destination, source)) throw Fail("LOCATION_OVERLAPS_RUN");
        var runtime = WebFormsReviewPreflightCommand.PhysicalPath(Path.GetDirectoryName(typeof(WebFormsReviewExecutionCommand).Assembly.Location)!);
        foreach (var dependency in ProtectedDependencies(source, plan, location, tool).Select(item => item.Path).Append(runtime))
            if (Within(dependency, destination) || Within(destination, dependency)) throw Fail("LOCATION_OVERLAPS_DEPENDENCY");
        if (WebFormsReviewPreflightCommand.PhysicalPath(destination) != destination) throw Fail("LOCATION_OUTPUT_LINK_INVALID");
    }

    private static async Task CopyVerifiedAsync(string source, string destination, WebFormsReviewArtifact artifact, CancellationToken token)
    {
        var target = OwnedPath(destination, artifact.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        await using (var input = new FileStream(OwnedPath(source, artifact.RelativePath), FileMode.Open, FileAccess.Read, FileShare.Read,
            65_536, FileOptions.Asynchronous | FileOptions.SequentialScan))
        await using (var output = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 65_536, FileOptions.Asynchronous))
        {
            if (input.Length != artifact.Bytes) throw Fail("LOCATION_SOURCE_CHANGED");
            // A growing input cannot copy beyond its admitted byte count.
            var buffer = new byte[65_536]; long total = 0; int read;
            while ((read = await input.ReadAsync(buffer, token)) != 0)
            {
                total = checked(total + read);
                if (total > artifact.Bytes) throw Fail("LOCATION_SOURCE_CHANGED");
                await output.WriteAsync(buffer.AsMemory(0, read), token);
            }
            if (total != artifact.Bytes) throw Fail("LOCATION_SOURCE_CHANGED");
            await output.FlushAsync(token); output.Flush(true);
        }
        var copied = await WebFormsReviewPreflightCommand.HashAsync("location-copy", target, artifact.Bytes, token);
        if (copied.Bytes != artifact.Bytes || copied.Sha256 != artifact.Sha256) throw Fail("LOCATION_COPY_CHANGED");
    }

    private static async Task<WebFormsReviewLocation?> ReadLocationAsync(string root, WebFormsReviewPreflightManifest plan,
        string preflight, CancellationToken token)
    {
        var path = OwnedPath(root, LocationName);
        if (!File.Exists(path)) return null;
        var bytes = await WebFormsReviewPreflightCommand.ReadSmallAsync(path, 4_194_304, token);
        WebFormsReviewPreflightCommand.RejectDuplicateProperties(bytes);
        var location = JsonSerializer.Deserialize<WebFormsReviewLocation>(bytes, JsonOptions) ?? throw Fail("LOCATION_INVALID");
        if (location.SchemaVersion != LocationSchema || location.RuleId != LocationRule || location.EvidenceTier != "Tier2Structural" ||
            location.Visibility != "local-only" || location.ClaimLevel != "review-only-static-not-runtime" ||
            location.RunId != plan.RunId || location.PreflightSha256 != preflight || location.CurrentRoot != root ||
            !CanonicalAbsolutePath(location.OriginalPolicyRoot) || !CanonicalAbsolutePath(location.CopiedFromRoot) ||
            !ValidSha(location.GeneratorSha256) || !ValidSha(location.CheckpointSha256) || !ValidSha(location.ArtifactsSha256) ||
            (location.PreviousLocationSha256 is not null && !ValidSha(location.PreviousLocationSha256)) ||
            location.CopiedFiles is null || location.CopiedFiles.Count is < 1 or > 514 || location.Limitations is null ||
            location.BoundedInputSha256 != LocationInputDigest(location) || location.PayloadSha256 != LocationPayloadDigest(location))
            throw Fail("LOCATION_INVALID");
        return location;
    }

    private static bool CanonicalAbsolutePath(string? path) => !string.IsNullOrWhiteSpace(path) && Path.IsPathFullyQualified(path) &&
        Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)) == path;
    private static string LocationInputDigest(WebFormsReviewLocation location) => Digest(JsonSerializer.SerializeToUtf8Bytes(new
    {
        location.GeneratorSha256, location.RunId, location.PreflightSha256, location.CheckpointSha256, location.ArtifactsSha256,
        location.OriginalPolicyRoot, location.CurrentRoot, location.CopiedFromRoot, location.PreviousLocationSha256, location.CopiedFiles
    }, JsonOptions));
    private static string LocationPayloadDigest(WebFormsReviewLocation location) =>
        Digest(JsonSerializer.SerializeToUtf8Bytes(location with { PayloadSha256 = "" }, JsonOptions));
}
