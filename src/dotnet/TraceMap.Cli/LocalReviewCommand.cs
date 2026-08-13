using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;
using TraceMap.Reporting;

namespace TraceMap.Cli;

public delegate Task<int> LocalReviewScanRunner(
    string[] args,
    TextWriter output,
    TextWriter error,
    CancellationToken cancellationToken);

public sealed record LocalReviewStageServices(
    Func<WebFormsModernizationOptions, CancellationToken, Task> WriteWebFormsAsync,
    Func<StaticHtmlEvidenceExplorerOptions, CancellationToken, Task> GenerateExplorerAsync);

public sealed record LocalReviewResult(
    string SchemaVersion,
    string WorkflowId,
    string ToolVersion,
    string DistributionKind,
    string? RepositoryIdentityHash,
    string? CommitSha,
    string? ScanId,
    string? SourceSnapshotDigest,
    string ClaimLevel,
    string Outcome,
    string Coverage,
    string LastProvenSafeState,
    string CleanupResult,
    string Retryability,
    string NextAction,
    IReadOnlyList<LocalReviewStage> Stages,
    IReadOnlyList<LocalReviewArtifact> Artifacts,
    LocalReviewSummary Summary,
    IReadOnlyList<string> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record LocalReviewStage(
    string Stage,
    string Outcome,
    IReadOnlyList<LocalReviewInputHash> Inputs,
    IReadOnlyList<string> OutputKinds);

public sealed record LocalReviewInputHash(string RelativePath, string Sha256);

public sealed record LocalReviewArtifact(
    string ArtifactKind,
    string RelativePath,
    string Sha256,
    string ProducerStage,
    string Status);

public sealed record LocalReviewSummary(int FactCount, int GapCount, int ArtifactCount);

public static class LocalReviewCommand
{
    public const string SchemaVersion = "local-review-result.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    private static readonly string[] RequiredScanArtifacts =
    [
        "scan-manifest.json",
        "facts.ndjson",
        "index.sqlite",
        "report.md",
        "logs/analyzer.log",
        "scan-receipt.json"
    ];
    private static readonly HashSet<string> Flags = new(StringComparer.Ordinal)
    {
        "--webforms-modernization",
        "--explorer"
    };
    private static readonly HashSet<string> ValueOptions = new(StringComparer.Ordinal)
    {
        "--repo",
        "--out",
        "--solution",
        "--project",
        "--include",
        "--exclude",
        "--target-framework"
    };

    public static async Task<int> RunAsync(
        string[] args,
        TextWriter output,
        TextWriter error,
        LocalReviewScanRunner scanRunner,
        CancellationToken cancellationToken = default,
        LocalReviewStageServices? stageServices = null)
    {
        stageServices ??= new LocalReviewStageServices(
            async (options, token) => { _ = await WebFormsModernizationPacketReporter.WriteAsync(options, token); },
            async (options, token) => { _ = await StaticHtmlEvidenceExplorer.GenerateAsync(options, token); });
        if (args.Length == 0 || !string.Equals(args[0], "run", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: LOCAL_REVIEW_ARGUMENT_INVALID; expected 'local-review run'.");
            return 1;
        }

        LocalReviewArguments parsed;
        string fullOutput;
        try
        {
            parsed = Parse(args.Skip(1).ToArray());
            fullOutput = ValidateOutput(parsed.RepositoryPath, parsed.OutputPath);
        }
        catch (LocalReviewException exception)
        {
            await error.WriteLineAsync($"error: {exception.Code}");
            return 1;
        }

        var parent = Path.GetDirectoryName(fullOutput)!;
        var staging = Path.Combine(parent, $".{Path.GetFileName(fullOutput)}.local-review-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(parent);
            if (Directory.Exists(fullOutput))
            {
                Directory.Delete(fullOutput);
            }

            Directory.CreateDirectory(staging);
        }
        catch (Exception)
        {
            await error.WriteLineAsync("error: LOCAL_REVIEW_OUTPUT_UNSAFE");
            return 1;
        }

        ScanManifest? manifest = null;
        var stages = new List<LocalReviewStage>();
        var activeStage = "scan";
        var lastSafeState = "output-staged";
        try
        {
            var scanDirectory = Path.Combine(staging, "scan");
            var scanArguments = parsed.ToScanArguments(scanDirectory);
            using var scanOutput = new StringWriter();
            using var scanError = new StringWriter();
            var scanExit = await scanRunner(scanArguments, scanOutput, scanError, cancellationToken);
            if (scanExit != 0)
            {
                var failed = BuildFailureResult(parsed, staging, "LOCAL_REVIEW_SCAN_FAILED", "scan-attempted", "inspect-scan-receipt");
                await WriteResultAsync(staging, failed, cancellationToken);
                Publish(staging, fullOutput);
                await WriteHumanAsync(failed, fullOutput, output);
                await error.WriteLineAsync("error: LOCAL_REVIEW_SCAN_FAILED");
                return 1;
            }

            EnsureRequiredScanArtifacts(scanDirectory);
            manifest = await ReadManifestAsync(scanDirectory, cancellationToken);
            if (string.IsNullOrWhiteSpace(manifest.GitRootHash)
                || string.IsNullOrWhiteSpace(manifest.SourceSnapshotDigest)
                || string.IsNullOrWhiteSpace(manifest.CommitSha))
            {
                throw new LocalReviewException("LOCAL_REVIEW_IDENTITY_UNAVAILABLE");
            }

            stages.Add(new("scan", Coverage(manifest) == "full" ? "succeeded" : "partial", [], ["scan-artifacts"]));
            lastSafeState = "scan-artifacts-verified";
            var gaps = new SortedSet<string>(StringComparer.Ordinal);
            if (Coverage(manifest) != "full")
            {
                gaps.Add("LOCAL_REVIEW_SCAN_PARTIAL");
            }

            if (parsed.WebFormsModernization)
            {
                activeStage = "webforms-modernization";
                var before = HashDirectory(scanDirectory);
                await stageServices.WriteWebFormsAsync(
                    new WebFormsModernizationOptions(
                        Path.Combine(scanDirectory, "index.sqlite"),
                        Path.Combine(staging, "webforms")),
                    cancellationToken);
                VerifyUnchanged(scanDirectory, before);
                stages.Add(new(
                    "webforms-modernization",
                    "succeeded",
                    ToInputHashes(before, "scan"),
                    ["webforms-modernization-packet"]));
                lastSafeState = "webforms-modernization-verified";
            }

            if (parsed.Explorer)
            {
                activeStage = "explorer";
                var explorerInputDirectory = parsed.WebFormsModernization
                    ? Path.Combine(staging, "webforms")
                    : scanDirectory;
                var explorerInputLabel = parsed.WebFormsModernization ? "webforms" : "scan";
                var before = HashDirectory(explorerInputDirectory);
                await stageServices.GenerateExplorerAsync(
                    new StaticHtmlEvidenceExplorerOptions(
                        explorerInputDirectory,
                        Path.Combine(staging, "explorer"),
                        "hidden-local"),
                    cancellationToken);
                VerifyUnchanged(explorerInputDirectory, before);
                stages.Add(new(
                    "explorer",
                    "succeeded",
                    ToInputHashes(before, explorerInputLabel),
                    ["static-html-explorer"]));
                lastSafeState = "explorer-verified";
            }

            var artifacts = BuildArtifactRecords(staging);
            var (factCount, factGapCount) = CountFacts(Path.Combine(scanDirectory, "facts.ndjson"));
            var coverage = Coverage(manifest);
            var version = TraceMapVersionInfo.Create();
            var outcome = coverage == "full" && gaps.Count == 0 ? "succeeded" : "partial";
            var workflowId = CreateWorkflowId(version, manifest, parsed, stages);
            var result = new LocalReviewResult(
                SchemaVersion,
                workflowId,
                version.ToolVersion,
                version.DistributionKind,
                manifest.GitRootHash,
                manifest.CommitSha,
                manifest.ScanId,
                manifest.SourceSnapshotDigest,
                "local-only",
                outcome,
                coverage,
                stages[^1].Stage + "-verified",
                "completed",
                outcome == "succeeded" ? "not-required" : "retry-after-owner-review",
                outcome == "succeeded" ? "review-generated-artifacts" : "review-scan-gaps",
                stages.AsReadOnly(),
                artifacts,
                new LocalReviewSummary(factCount, factGapCount, artifacts.Count),
                gaps.ToArray(),
                Limitations());
            await WriteResultAsync(staging, result, cancellationToken);
            Publish(staging, fullOutput);
            await WriteHumanAsync(result, fullOutput, output);
            return 0;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryPublishStageFailureAsync(
                parsed,
                staging,
                fullOutput,
                manifest,
                stages,
                activeStage,
                "cancelled",
                "LOCAL_REVIEW_CANCELLED",
                lastSafeState,
                "contact-owner",
                output);
            throw;
        }
        catch (LocalReviewException exception)
        {
            await TryPublishStageFailureAsync(
                parsed,
                staging,
                fullOutput,
                manifest,
                stages,
                activeStage,
                "failed",
                exception.Code,
                lastSafeState,
                NextAction(exception.Code),
                output);
            await error.WriteLineAsync($"error: {exception.Code}");
            return 1;
        }
        catch (Exception)
        {
            await TryPublishStageFailureAsync(
                parsed,
                staging,
                fullOutput,
                manifest,
                stages,
                activeStage,
                "failed",
                "LOCAL_REVIEW_STAGE_FAILED",
                lastSafeState,
                "contact-owner",
                output);
            await error.WriteLineAsync("error: LOCAL_REVIEW_STAGE_FAILED");
            return 1;
        }
    }

    private static LocalReviewArguments Parse(string[] args)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var argument = args[index];
            if (Flags.Contains(argument))
            {
                flags.Add(argument);
                continue;
            }

            if (!ValueOptions.Contains(argument)
                || index + 1 >= args.Length
                || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new LocalReviewException("LOCAL_REVIEW_ARGUMENT_INVALID");
            }

            if (!values.TryGetValue(argument, out var list))
            {
                list = [];
                values[argument] = list;
            }

            list.Add(args[++index]);
        }

        var repository = Single(values, "--repo");
        var output = Single(values, "--out");
        return new LocalReviewArguments(
            repository,
            output,
            Many(values, "--solution"),
            Many(values, "--project"),
            Many(values, "--include"),
            Many(values, "--exclude"),
            OptionalSingle(values, "--target-framework"),
            flags.Contains("--webforms-modernization"),
            flags.Contains("--explorer"));
    }

    private static string Single(Dictionary<string, List<string>> values, string key)
    {
        if (!values.TryGetValue(key, out var items) || items.Count != 1 || string.IsNullOrWhiteSpace(items[0]))
        {
            throw new LocalReviewException("LOCAL_REVIEW_ARGUMENT_INVALID");
        }

        return items[0];
    }

    private static string? OptionalSingle(Dictionary<string, List<string>> values, string key) =>
        values.TryGetValue(key, out var items)
            ? items.Count == 1 && !string.IsNullOrWhiteSpace(items[0])
                ? items[0]
                : throw new LocalReviewException("LOCAL_REVIEW_ARGUMENT_INVALID")
            : null;

    private static IReadOnlyList<string> Many(Dictionary<string, List<string>> values, string key) =>
        values.TryGetValue(key, out var items) ? items.AsReadOnly() : [];

    private static string ValidateOutput(string repositoryPath, string outputPath)
    {
        string repository;
        string output;
        try
        {
            repository = ResolveDirectoryPath(Path.GetFullPath(repositoryPath)).TrimEnd(Path.DirectorySeparatorChar);
            var requestedOutput = Path.GetFullPath(outputPath).TrimEnd(Path.DirectorySeparatorChar);
            var requestedParent = Path.GetDirectoryName(requestedOutput)
                ?? throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_UNSAFE");
            output = Path.Combine(
                ResolveDirectoryPath(requestedParent),
                Path.GetFileName(requestedOutput));
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw new LocalReviewException("LOCAL_REVIEW_ARGUMENT_INVALID");
        }

        if (!Directory.Exists(repository))
        {
            throw new LocalReviewException("LOCAL_REVIEW_ARGUMENT_INVALID");
        }

        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var root = Path.GetPathRoot(output)?.TrimEnd(Path.DirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(output)
            || string.Equals(output, root, comparison)
            || IsWithin(output, repository, comparison)
            || output.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .Any(component => string.Equals(component, ".git", comparison)))
        {
            throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_UNSAFE");
        }

        if (File.Exists(output))
        {
            throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_COLLISION");
        }

        if (Directory.Exists(output))
        {
            var info = new DirectoryInfo(output);
            if (info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || Directory.EnumerateFileSystemEntries(output).Any())
            {
                throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_COLLISION");
            }
        }

        return output;
    }

    private static string ResolveDirectoryPath(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = Path.GetPathRoot(fullPath)
            ?? throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_UNSAFE");
        var relative = Path.GetRelativePath(root, fullPath);
        var current = root;
        if (relative == ".") return current;

        foreach (var component in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            current = Path.Combine(current, component);
            var info = new DirectoryInfo(current);
            if (!info.Exists) continue;
            if (!info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;

            var target = info.ResolveLinkTarget(returnFinalTarget: true)
                ?? throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_UNSAFE");
            current = Path.GetFullPath(target.FullName);
        }

        return current;
    }

    private static bool IsWithin(string candidate, string parent, StringComparison comparison) =>
        string.Equals(candidate, parent, comparison)
        || candidate.StartsWith(parent + Path.DirectorySeparatorChar, comparison);

    private static async Task<ScanManifest> ReadManifestAsync(string scanDirectory, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(Path.Combine(scanDirectory, "scan-manifest.json"));
        return await JsonSerializer.DeserializeAsync<ScanManifest>(stream, ReadOptions, cancellationToken)
            ?? throw new LocalReviewException("LOCAL_REVIEW_IDENTITY_UNAVAILABLE");
    }

    private static void EnsureRequiredScanArtifacts(string scanDirectory)
    {
        if (RequiredScanArtifacts.Any(relative => !File.Exists(Path.Combine(scanDirectory, relative))))
        {
            throw new LocalReviewException("LOCAL_REVIEW_SCAN_FAILED");
        }
    }

    private static SortedDictionary<string, string> HashDirectory(string directory)
    {
        return new SortedDictionary<string, string>(
            Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                .ToDictionary(
                    path => Normalize(Path.GetRelativePath(directory, path)),
                    HashFile,
                    StringComparer.Ordinal),
            StringComparer.Ordinal);
    }

    private static void VerifyUnchanged(string directory, IReadOnlyDictionary<string, string> before)
    {
        var after = HashDirectory(directory);
        if (before.Count != after.Count
            || before.Any(pair => !after.TryGetValue(pair.Key, out var hash)
                || !string.Equals(pair.Value, hash, StringComparison.Ordinal)))
        {
            throw new LocalReviewException("LOCAL_REVIEW_INPUT_MUTATED");
        }
    }

    private static IReadOnlyList<LocalReviewInputHash> ToInputHashes(
        IReadOnlyDictionary<string, string> hashes,
        string root) => hashes
            .Select(pair => new LocalReviewInputHash($"{root}/{pair.Key}", pair.Value))
            .ToArray();

    private static IReadOnlyList<LocalReviewArtifact> BuildArtifactRecords(string staging)
    {
        return Directory.EnumerateFiles(staging, "*", SearchOption.AllDirectories)
            .Select(path =>
            {
                var relative = Normalize(Path.GetRelativePath(staging, path));
                var root = relative.Split('/')[0];
                var stage = root == "webforms" ? "webforms-modernization" : root;
                return new LocalReviewArtifact(
                    ArtifactKind(relative),
                    relative,
                    HashFile(path),
                    stage,
                    "available");
            })
            .OrderBy(artifact => artifact.RelativePath, StringComparer.Ordinal)
            .ToArray();
    }

    private static string ArtifactKind(string relativePath)
    {
        if (relativePath.EndsWith("scan-manifest.json", StringComparison.Ordinal)) return "scan-manifest";
        if (relativePath.EndsWith("scan-receipt.json", StringComparison.Ordinal)) return "scan-execution-receipt";
        if (relativePath.EndsWith("facts.ndjson", StringComparison.Ordinal)) return "facts";
        if (relativePath.EndsWith("index.sqlite", StringComparison.Ordinal)) return "scan-index";
        if (relativePath.EndsWith("webforms-modernization.json", StringComparison.Ordinal)) return "webforms-modernization-packet";
        if (relativePath.StartsWith("explorer/", StringComparison.Ordinal)) return "static-html-explorer-file";
        if (relativePath.EndsWith(".md", StringComparison.Ordinal)) return "markdown-report";
        if (relativePath.EndsWith(".log", StringComparison.Ordinal)) return "diagnostic-log";
        return "generated-file";
    }

    private static (int FactCount, int GapCount) CountFacts(string path)
    {
        var facts = 0;
        var gaps = 0;
        foreach (var line in File.ReadLines(path))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            facts++;
            using var document = JsonDocument.Parse(line);
            if (document.RootElement.TryGetProperty("factType", out var factType)
                && string.Equals(factType.GetString(), FactTypes.AnalysisGap, StringComparison.Ordinal))
            {
                gaps++;
            }
        }

        return (facts, gaps);
    }

    private static string Coverage(ScanManifest manifest) =>
        manifest.AnalysisLevel.Contains("Reduced", StringComparison.Ordinal)
        || string.Equals(manifest.BuildStatus, "FailedOrPartial", StringComparison.Ordinal)
            ? "reduced"
            : "full";

    private static string CreateWorkflowId(
        TraceMapVersionResult version,
        ScanManifest manifest,
        LocalReviewArguments arguments,
        IReadOnlyList<LocalReviewStage> stages)
    {
        var identity = string.Join("\n", new[]
        {
            SchemaVersion,
            version.ToolVersion,
            version.DistributionKind,
            manifest.GitRootHash,
            manifest.CommitSha,
            manifest.ScanId,
            manifest.SourceSnapshotDigest,
            string.Join(',', arguments.IdentityPaths(arguments.Solutions)),
            string.Join(',', arguments.IdentityPaths(arguments.Projects)),
            string.Join(',', arguments.Includes.OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(',', arguments.Excludes.OrderBy(value => value, StringComparer.Ordinal)),
            arguments.TargetFramework ?? string.Empty,
            string.Join(',', stages.Select(stage => $"{stage.Stage}:{stage.Outcome}"))
        });
        return "workflow-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..20];
    }

    private static LocalReviewResult BuildFailureResult(
        LocalReviewArguments arguments,
        string staging,
        string gap,
        string lastSafeState,
        string nextAction)
    {
        var version = TraceMapVersionInfo.Create();
        var artifacts = BuildArtifactRecords(staging);
        var attemptScope = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(arguments.RepositoryPath)))).ToLowerInvariant();
        var identity = string.Join("\n", SchemaVersion, version.ToolVersion, gap, attemptScope, arguments.StageSignature());
        var workflowId = "workflow-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..20];
        return new LocalReviewResult(
            SchemaVersion,
            workflowId,
            version.ToolVersion,
            version.DistributionKind,
            null,
            null,
            null,
            null,
            "local-only",
            "failed",
            "unknown",
            lastSafeState,
            "completed",
            "retry-after-correction",
            nextAction,
            [new("scan", "failed", [], [])],
            artifacts,
            new LocalReviewSummary(0, 0, artifacts.Count),
            [gap],
            Limitations());
    }

    private static async Task TryPublishStageFailureAsync(
        LocalReviewArguments arguments,
        string staging,
        string outputPath,
        ScanManifest? manifest,
        IReadOnlyList<LocalReviewStage> completedStages,
        string activeStage,
        string outcome,
        string gap,
        string lastSafeState,
        string nextAction,
        TextWriter output)
    {
        if (!Directory.Exists(staging) || Directory.Exists(outputPath) || File.Exists(outputPath))
        {
            TryCleanup(staging);
            return;
        }

        try
        {
            var version = TraceMapVersionInfo.Create();
            var artifacts = BuildArtifactRecords(staging);
            var stages = completedStages.ToList();
            if (!stages.Any(stage => string.Equals(stage.Stage, activeStage, StringComparison.Ordinal)))
            {
                stages.Add(new(activeStage, outcome, [], []));
            }
            var attemptScope = manifest?.GitRootHash
                ?? Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(arguments.RepositoryPath)))).ToLowerInvariant();
            var identity = string.Join("\n", SchemaVersion, version.ToolVersion, attemptScope, gap, arguments.StageSignature());
            var workflowId = "workflow-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity))).ToLowerInvariant()[..20];
            var factsPath = Path.Combine(staging, "scan", "facts.ndjson");
            var counts = File.Exists(factsPath)
                ? CountFacts(factsPath)
                : (FactCount: 0, GapCount: 0);
            var result = new LocalReviewResult(
                SchemaVersion,
                workflowId,
                version.ToolVersion,
                version.DistributionKind,
                manifest?.GitRootHash,
                manifest?.CommitSha,
                manifest?.ScanId,
                manifest?.SourceSnapshotDigest,
                "local-only",
                outcome,
                manifest is null ? "unknown" : Coverage(manifest),
                lastSafeState,
                "completed",
                outcome == "cancelled" ? "not-retryable" : "retry-after-correction",
                nextAction,
                stages.AsReadOnly(),
                artifacts,
                new LocalReviewSummary(counts.FactCount, counts.GapCount, artifacts.Count),
                [gap],
                Limitations());
            await WriteResultAsync(staging, result, CancellationToken.None);
            Publish(staging, outputPath);
            await WriteHumanAsync(result, outputPath, output);
        }
        catch (Exception)
        {
            TryCleanup(staging);
        }
    }

    private static string NextAction(string code) => code switch
    {
        "LOCAL_REVIEW_OUTPUT_COLLISION" => "choose-new-output",
        "LOCAL_REVIEW_ARGUMENT_INVALID" => "correct-input",
        "LOCAL_REVIEW_IDENTITY_UNAVAILABLE" => "inspect-scan-receipt",
        "LOCAL_REVIEW_INPUT_MUTATED" => "contact-owner",
        _ => "contact-owner"
    };

    private static IReadOnlyList<string> Limitations() =>
    [
        "This workflow composes local static evidence; it does not prove runtime execution, application correctness, complete coverage, migration safety, release approval, publisher identity, or production state.",
        "Absolute local paths and raw diagnostic text are intentionally excluded from portable workflow artifacts."
    ];

    private static async Task WriteResultAsync(string staging, LocalReviewResult result, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(result, JsonOptions) + "\n";
        await File.WriteAllTextAsync(Path.Combine(staging, "local-review-result.json"), json, new UTF8Encoding(false), cancellationToken);
        var markdown = string.Join('\n', new[]
        {
            "# TraceMap Local Review",
            string.Empty,
            $"- Workflow: `{result.WorkflowId}`",
            $"- Commit: `{result.CommitSha ?? "unavailable"}`",
            $"- Source snapshot: `{result.SourceSnapshotDigest ?? "unavailable"}`",
            $"- Outcome: `{result.Outcome}`",
            $"- Coverage: `{result.Coverage}`",
            $"- Facts: {result.Summary.FactCount}",
            $"- Gaps: {result.Summary.GapCount}",
            $"- Next action: `{result.NextAction}`",
            string.Empty,
            "This packet contains local static evidence only. Review its explicit gaps and limitations before drawing conclusions.",
            string.Empty
        });
        await File.WriteAllTextAsync(Path.Combine(staging, "README.md"), markdown, new UTF8Encoding(false), cancellationToken);
    }

    private static async Task WriteHumanAsync(LocalReviewResult result, string outputPath, TextWriter output)
    {
        await output.WriteLineAsync("TraceMap local review completed.");
        await output.WriteLineAsync($"Commit SHA: {result.CommitSha ?? "unavailable"}");
        await output.WriteLineAsync($"Source snapshot: {result.SourceSnapshotDigest ?? "unavailable"}");
        await output.WriteLineAsync($"Outcome: {result.Outcome}");
        await output.WriteLineAsync($"Coverage: {result.Coverage}");
        await output.WriteLineAsync($"Facts: {result.Summary.FactCount}");
        await output.WriteLineAsync($"Gaps: {result.Summary.GapCount}");
        await output.WriteLineAsync($"Output: {outputPath}");
        await output.WriteLineAsync($"Next action: {result.NextAction}");
    }

    private static void Publish(string staging, string output)
    {
        if (Directory.Exists(output) || File.Exists(output))
        {
            throw new LocalReviewException("LOCAL_REVIEW_OUTPUT_COLLISION");
        }

        Directory.Move(staging, output);
    }

    private static bool TryCleanup(string staging)
    {
        try
        {
            if (Directory.Exists(staging))
            {
                Directory.Delete(staging, recursive: true);
            }

            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static string Normalize(string path) => path.Replace(Path.DirectorySeparatorChar, '/');

    private static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private sealed record LocalReviewArguments(
        string RepositoryPath,
        string OutputPath,
        IReadOnlyList<string> Solutions,
        IReadOnlyList<string> Projects,
        IReadOnlyList<string> Includes,
        IReadOnlyList<string> Excludes,
        string? TargetFramework,
        bool WebFormsModernization,
        bool Explorer)
    {
        public string[] ToScanArguments(string outputPath)
        {
            var arguments = new List<string> { "--repo", RepositoryPath, "--out", outputPath };
            Add(arguments, "--solution", Solutions);
            Add(arguments, "--project", Projects);
            Add(arguments, "--include", Includes);
            Add(arguments, "--exclude", Excludes);
            if (!string.IsNullOrWhiteSpace(TargetFramework))
            {
                arguments.Add("--target-framework");
                arguments.Add(TargetFramework);
            }

            return arguments.ToArray();
        }

        public string StageSignature() => $"webforms={WebFormsModernization};explorer={Explorer}";

        public IReadOnlyList<string> IdentityPaths(IReadOnlyList<string> paths)
        {
            var repository = Path.GetFullPath(RepositoryPath);
            return paths
                .Select(path => Path.IsPathRooted(path)
                    ? Path.GetRelativePath(repository, Path.GetFullPath(path))
                    : path)
                .Select(Normalize)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray();
        }

        private static void Add(List<string> arguments, string option, IReadOnlyList<string> values)
        {
            foreach (var value in values)
            {
                arguments.Add(option);
                arguments.Add(value);
            }
        }
    }

    private sealed class LocalReviewException(string code) : Exception(code)
    {
        public string Code { get; } = code;
    }
}
