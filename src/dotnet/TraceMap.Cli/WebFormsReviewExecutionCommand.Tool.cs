using System.Text.Json;

namespace TraceMap.Cli;

public sealed record WebFormsReviewToolDistribution(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility,
    string DistributionRoot, string EntryRelativePath, string RuntimeVersion,
    string RuntimeInputsSha256, string DotnetRuntimeRoot, IReadOnlyList<string> Limitations);

public sealed record WebFormsReviewToolCopy(
    string SchemaVersion, string RuleId, string EvidenceTier, string Visibility, string ClaimLevel,
    string GeneratorSha256, string BoundedInputSha256, string RunId, string PreflightSha256,
    string CheckpointSha256, WebFormsReviewToolDistribution OriginalTool,
    string CurrentRoot, IReadOnlyList<WebFormsReviewArtifact> Files,
    IReadOnlyList<string> Limitations, string PayloadSha256);

public static partial class WebFormsReviewExecutionCommand
{
    internal const string ToolLocationRule = "workflow.webforms.original-tool-location.v1";
    internal const string ToolCopyRule = "workflow.webforms.retained-tool-copy.v1";
    internal const string ToolCopyName = "tool-copy.local.json";

    private static WebFormsReviewToolDistribution DescribeTool(string root, string runtimeSha) => new(
        "webforms-review-tool-distribution.v1", ToolLocationRule, "Tier2Structural", "local-only",
        root, Path.GetFileName(typeof(WebFormsReviewExecutionCommand).Assembly.Location), Environment.Version.ToString(), runtimeSha,
        Path.GetDirectoryName(typeof(object).Assembly.Location)!,
        ["The distribution locator is retained from the original collector, not authenticated publisher or compilation provenance.",
         "RuntimeInputsSha256 commits only the bounded DLL/executable/native/deps/runtimeconfig selection and .NET runtime version.",
         "The external .NET runtime locator is a protected declaration; its bytes and external SDK/adaptor installations are not pinned or copied."]);

    private static void ValidateToolDistribution(WebFormsReviewCheckpoint checkpoint)
    {
        if (checkpoint.ToolDistribution is not { } tool) return; // Historical absence stays unknown.
        if (tool.SchemaVersion != "webforms-review-tool-distribution.v1" || tool.RuleId != ToolLocationRule
            || tool.EvidenceTier != "Tier2Structural" || tool.Visibility != "local-only"
            || tool.RuntimeInputsSha256 != checkpoint.RuntimeInputsSha256 || !ValidSha(tool.RuntimeInputsSha256)
            || !DeclaredAbsolutePath(tool.DistributionRoot) || !DeclaredAbsolutePath(tool.DotnetRuntimeRoot)
            || string.IsNullOrEmpty(tool.EntryRelativePath) || tool.EntryRelativePath.Length > 256
            || tool.EntryRelativePath.Contains('/') || tool.EntryRelativePath.Contains('\\') || tool.EntryRelativePath.Contains(':')
            || tool.EntryRelativePath is "." or ".." || !tool.EntryRelativePath.EndsWith(".dll", StringComparison.Ordinal)
            || tool.RuntimeVersion is not { Length: > 0 and <= 64 } || !Version.TryParse(tool.RuntimeVersion, out _)
            || tool.Limitations is null || tool.Limitations.Count is < 1 or > 16
            || tool.Limitations.Any(item => string.IsNullOrEmpty(item) || item.Length > 1024))
            throw Fail("TOOL_LOCATION_INVALID");
    }

    private static bool DeclaredAbsolutePath(string? value) => value is { Length: > 0 and <= 32768 }
        && !value.Contains('\0') && Path.IsPathFullyQualified(value);

    /// <summary>Copy an explicitly selected completed run's original pinned tool bytes. Never relocates inputs or executes copied code.</summary>
    public static async Task<int> RetainToolAsync(string[] args, TextWriter output, TextWriter error, CancellationToken token = default)
    {
        try
        {
            if (args.Length != 5 || args[0] != "retain-tool" || args[1] != "--run" || args[3] != "--out")
                throw Fail("TOOL_COPY_ARGUMENT_INVALID");
            var run = WebFormsReviewPreflightCommand.PhysicalPath(args[2]);
            using var runLock = new FileStream(OwnedPath(run, ".native-run.lock"), FileMode.Open, FileAccess.Read, FileShare.None);
            var retained = await ReadCompletedRunAsync(run, token);
            var tool = retained.History.Checkpoint!.ToolDistribution ?? throw Fail("ORIGINAL_TOOL_LOCATION_UNAVAILABLE");
            var original = WebFormsReviewPreflightCommand.PhysicalPath(tool.DistributionRoot);
            if (original != tool.DistributionRoot || !Directory.Exists(original)) throw Fail("ORIGINAL_TOOL_UNAVAILABLE");
            if (tool.RuntimeVersion != Environment.Version.ToString()) throw Fail("TOOL_RUNTIME_VERSION_MISMATCH");
            var files = await RuntimeInventoryAsync(original, token);
            if (RuntimeInventoryDigest(files) != tool.RuntimeInputsSha256) throw Fail("ORIGINAL_TOOL_CHANGED");
            var entry = files.SingleOrDefault(file => file.RelativePath == tool.EntryRelativePath);
            if (entry is null) throw Fail("ORIGINAL_TOOL_ENTRY_UNAVAILABLE");
            if (entry.Sha256 != retained.Plan.GeneratorSha256) throw Fail("ORIGINAL_TOOL_ENTRY_MISMATCH");
            var destination = WebFormsReviewPreflightCommand.PhysicalPath(args[4]);
            ValidateToolCopyDestination(destination);
            var staging = destination + ".pending-" + Guid.NewGuid().ToString("N");
            ValidateToolCopyDestination(staging);
            Directory.CreateDirectory(staging);
            foreach (var file in files) await CopyVerifiedAsync(original, staging, file, token);
            var copiedFiles = await RuntimeInventoryAsync(staging, token);
            if (!files.SequenceEqual(copiedFiles) || RuntimeInventoryDigest(copiedFiles) != tool.RuntimeInputsSha256)
                throw Fail("TOOL_COPY_CHANGED");
            var generator = await WebFormsReviewPreflightCommand.HashAsync("tool-copy-generator",
                typeof(WebFormsReviewExecutionCommand).Assembly.Location, 67_108_864, token);
            var bounded = Digest(JsonSerializer.SerializeToUtf8Bytes(new
            { generator.Sha256, retained.Plan.RunId, retained.PreflightSha256, checkpointSha256 = retained.History.Sha256,
                originalTool = tool, currentRoot = destination, files }, JsonOptions));
            var copy = new WebFormsReviewToolCopy("webforms-review-tool-copy.v1", ToolCopyRule, "Tier2Structural", "local-only",
                "verified-distribution-copy-not-execution-or-sdk-closure", generator.Sha256, bounded, retained.Plan.RunId,
                retained.PreflightSha256, retained.History.Sha256!, tool, destination, files,
                ["Only the exact pinned distribution selection was copied; original run and distribution remain unchanged.",
                 "No copied code was executed and no source, published site, parent scan, external .NET runtime, SDK or adaptor bytes were copied.",
                 "Resume still requires the original .NET runtime version and authoritative external input gates. This is not a portable or self-contained toolchain.",
                 "This local artifact contains private dependency paths and integrity hashes. It is not shareable evidence or cleanup approval."], "");
            copy = copy with { PayloadSha256 = Digest(JsonSerializer.SerializeToUtf8Bytes(copy, JsonOptions)) };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(copy, JsonOptions);
            if (bytes.Length > 1_048_576) throw Fail("TOOL_COPY_MANIFEST_LIMIT");
            await using (var stream = new FileStream(OwnedPath(staging, ToolCopyName), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            { await stream.WriteAsync(bytes, token); await stream.FlushAsync(token); stream.Flush(true); }
            var rechecked = await ReadCompletedRunAsync(run, token);
            if (retained.PreflightSha256 != rechecked.PreflightSha256 || retained.History.Sha256 != rechecked.History.Sha256
                || !retained.Files.SequenceEqual(rechecked.Files)) throw Fail("TOOL_COPY_RUN_CHANGED");
            if (!files.SequenceEqual(await RuntimeInventoryAsync(original, token))) throw Fail("ORIGINAL_TOOL_CHANGED");
            ValidateToolCopyDestination(destination);
            token.ThrowIfCancellationRequested();
            Directory.Move(staging, destination);
            if (!files.SequenceEqual(await RuntimeInventoryAsync(destination, token))) throw Fail("TOOL_COPY_CHANGED");
            await output.WriteLineAsync("webFormsToolCopy=verified;originalPreserved=true;externalRuntimeAndSdkCopied=false;copiedCodeExecuted=false;cleanup=false");
            await output.WriteLineAsync($"webFormsRetainedTool={OwnedPath(destination, tool.EntryRelativePath)}");
            await output.WriteLineAsync($"webFormsPinnedRun={run}");
            return 0;

            void ValidateToolCopyDestination(string target)
            {
                ValidateRelocationDestination(run, target, retained.Plan, retained.Location);
                foreach (var dependency in new[] { original, tool.DotnetRuntimeRoot })
                    if (Within(dependency, target) || Within(target, dependency)) throw Fail("TOOL_COPY_OVERLAPS_DEPENDENCY");
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (ExecutionException exception) { await error.WriteLineAsync("error: " + exception.Message); return 1; }
        catch (Exception) { await error.WriteLineAsync("error: WEBFORMS_NATIVE_TOOL_COPY_INPUT_OR_OUTPUT_INVALID"); return 1; }
    }
}
