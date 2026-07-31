using TraceMap.Access;
using TraceMap.Reporting;

namespace TraceMap.Access.Cli;

public static class Program
{
    public static Task<int> Main(string[] args) => AccessCommand.RunAsync(args, Console.Out, Console.Error);
}

public static class AccessCommand
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(RootHelp());
            return 0;
        }
        if (args[0] is "--version" or "-v")
        {
            await output.WriteLineAsync(AccessFactBuilder.ScannerVersion);
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();
        if (rest.Any(IsHelp))
        {
            await output.WriteLineAsync(command switch
            {
                "scan" => ScanHelp(),
                "enrich-design" => EnrichDesignHelp(),
                "identity-project" => IdentityProjectHelp(),
                "flow" => FlowHelp(),
                "copy-clone" => CopyCloneHelp(),
                _ => RootHelp()
            });
            return command is "scan" or "enrich-design" or "identity-project" or "flow" or "copy-clone" ? 0 : 1;
        }

        try
        {
            return command switch
            {
                "scan" => await RunScanAsync(ParseOptions(rest), output, cancellationToken),
                "enrich-design" => await RunEnrichDesignAsync(ParseOptions(rest), output, cancellationToken),
                "identity-project" => await RunIdentityProjectAsync(ParseOptions(rest), output, cancellationToken),
                "flow" => await RunFlowAsync(ParseOptions(rest), output, cancellationToken),
                "copy-clone" => await RunCopyCloneAsync(ParseOptions(rest), output, cancellationToken),
                "worker" => await AccessWorkerHost.RunAsync(ParseOptions(rest), output, cancellationToken),
                _ => await UnknownAsync(error)
            };
        }
        catch (AccessScanException ex)
        {
            await error.WriteLineAsync($"error: {ex.Classification}");
            return 1;
        }
        catch (Exception ex)
        {
            var typeToken = new string(ex.GetType().Name.Where(char.IsLetterOrDigit).Take(64).ToArray());
            await error.WriteLineAsync($"error: AccessUnhandledFailure-{(typeToken.Length == 0 ? "Exception" : typeToken)}");
            return 1;
        }
    }

    private static async Task<int> RunIdentityProjectAsync(
        IReadOnlyDictionary<string, string> values,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var baseScan = Required(values, "--base-scan", "AccessBaseScanPathMissing");
        var designEvidence = Required(values, "--design-evidence", "AccessDesignInputPathMissing");
        var outPath = Required(values, "--out", "AccessHiddenIdentityOutputMissing");
        if (values.Keys.Except(["--base-scan", "--design-evidence", "--out"], StringComparer.Ordinal).Any())
            throw new AccessScanException("AccessInvalidArguments");
        ValidateEnrichmentOutputPath(baseScan, designEvidence, outPath);
        var result = await AccessHiddenIdentityProjection.WriteAsync(
            baseScan,
            designEvidence,
            outPath,
            AccessLimits.Default,
            cancellationToken);
        await output.WriteLineAsync("TraceMap Access hidden-local identity projection completed.");
        await output.WriteLineAsync($"Identities written: {result.IdentityCount}");
        await output.WriteLineAsync("Claim level: hidden");
        return 0;
    }

    private static async Task<int> RunEnrichDesignAsync(
        IReadOnlyDictionary<string, string> values,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var baseScan = Required(values, "--base-scan", "AccessBaseScanPathMissing");
        var designEvidence = Required(values, "--design-evidence", "AccessDesignInputPathMissing");
        var outPath = Required(values, "--out", "AccessOutputMissing");
        var unexpected = values.Keys.Except(["--base-scan", "--design-evidence", "--out"], StringComparer.Ordinal).Any();
        if (unexpected) throw new AccessScanException("AccessInvalidArguments");
        ValidateEnrichmentOutputPath(baseScan, designEvidence, outPath);
        var result = await AccessDesignEvidenceComposer.ComposeAsync(baseScan, designEvidence, AccessLimits.Default, cancellationToken);
        try { await AccessArtifactWriter.WriteAsync(outPath, result, AccessLimits.Default, cancellationToken); }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessArtifactWriteFailed"); }
        await output.WriteLineAsync("TraceMap Access design evidence enrichment completed.");
        await output.WriteLineAsync($"Facts written: {result.Facts.Count}");
        await output.WriteLineAsync($"Analysis level: {result.Manifest.AnalysisLevel}");
        return 0;
    }

    private static async Task<int> RunFlowAsync(
        IReadOnlyDictionary<string, string> values,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var index = Required(values, "--index", "AccessFlowIndexMissing");
        var outPath = Required(values, "--out", "AccessFlowOutputMissing");
        var maxDepth = PositiveInt(values, "--max-depth", 12, 64, "AccessFlowBoundsInvalid");
        var maxPaths = PositiveInt(values, "--max-paths", 100, 10_000, "AccessFlowBoundsInvalid");
        var maxGaps = PositiveInt(values, "--max-gaps", 1_000, 10_000, "AccessFlowBoundsInvalid");
        var unexpected = values.Keys.Except(
            ["--index", "--out", "--max-depth", "--max-paths", "--max-gaps"],
            StringComparer.Ordinal).Any();
        if (unexpected) throw new AccessScanException("AccessInvalidArguments");
        AccessScreenDataFlowResult result;
        try
        {
            result = await AccessScreenDataFlowReporter.WriteAsync(
                new(index, outPath, maxDepth, maxPaths, maxGaps),
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            throw new AccessScanException(SafeFlowClassification(ex.Message));
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessFlowCompositionFailed"); }
        await output.WriteLineAsync("TraceMap Access static flow composition completed.");
        await output.WriteLineAsync($"Candidate paths: {result.Report.Summary.PathCount}");
        await output.WriteLineAsync($"Coverage: {result.Report.Coverage}");
        return 0;
    }

    private static async Task<int> RunCopyCloneAsync(
        IReadOnlyDictionary<string, string> values,
        TextWriter output,
        CancellationToken cancellationToken)
    {
        var index = Required(values, "--index", "AccessCopyCloneIndexMissing");
        var outPath = Required(values, "--out", "AccessCopyCloneOutputMissing");
        var maxCandidates = PositiveInt(values, "--max-candidates", 1_000, 10_000, "AccessCopyCloneBoundsInvalid");
        var maxFlowPaths = PositiveInt(values, "--max-flow-paths", 1_000, 10_000, "AccessCopyCloneBoundsInvalid");
        var maxGaps = PositiveInt(values, "--max-gaps", 1_000, 10_000, "AccessCopyCloneBoundsInvalid");
        var unexpected = values.Keys.Except(
            ["--index", "--out", "--max-candidates", "--max-flow-paths", "--max-gaps"],
            StringComparer.Ordinal).Any();
        if (unexpected) throw new AccessScanException("AccessInvalidArguments");
        AccessCopyCloneResult result;
        try
        {
            result = await AccessCopyCloneCandidateReporter.WriteAsync(
                new(index, outPath, maxCandidates, maxFlowPaths, maxGaps),
                cancellationToken);
        }
        catch (InvalidDataException ex)
        {
            throw new AccessScanException(SafeCopyCloneClassification(ex.Message));
        }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessCopyCloneCompositionFailed"); }
        await output.WriteLineAsync("TraceMap Access copy/clone candidate composition completed.");
        await output.WriteLineAsync($"Candidates: {result.Report.Summary.CandidateCount}");
        await output.WriteLineAsync($"Coverage: {result.Report.Coverage}");
        return 0;
    }

    private static void ValidateEnrichmentOutputPath(string baseScan, string designEvidence, string output)
    {
        var rawInputs = new[] { Path.GetFullPath(baseScan), Path.GetFullPath(designEvidence) };
        var rawTarget = Path.GetFullPath(output);
        RejectExclusiveReparsePointAncestors(rawTarget, rawInputs);
        var target = CanonicalizeExistingAncestors(rawTarget);
        foreach (var input in rawInputs.Select(CanonicalizeExistingAncestors))
        {
            if (PathsEqual(target, input) || IsAncestor(target, input) || IsAncestor(input, target))
                throw new AccessScanException("AccessUnsafeOutputPath");
        }
    }

    private static bool IsAncestor(string ancestor, string path)
    {
        var prefix = Path.TrimEndingDirectorySeparator(ancestor) + Path.DirectorySeparatorChar;
        return Path.TrimEndingDirectorySeparator(path).StartsWith(prefix, PathComparison);
    }

    private static bool PathsEqual(string left, string right) =>
        string.Equals(
            Path.TrimEndingDirectorySeparator(left),
            Path.TrimEndingDirectorySeparator(right),
            PathComparison);

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static void RejectExclusiveReparsePointAncestors(string path, IReadOnlyList<string> inputs)
    {
        for (var current = path; !string.IsNullOrEmpty(current); current = Path.GetDirectoryName(current))
        {
            if ((File.Exists(current) || Directory.Exists(current))
                && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
            {
                var sharedByAllInputs = inputs.All(input => PathsEqual(current, input) || IsAncestor(current, input));
                if (!sharedByAllInputs) throw new AccessScanException("AccessUnsafeOutputPath");
            }
        }
    }

    private static string CanonicalizeExistingAncestors(string path)
    {
        var missing = new Stack<string>();
        var current = path;
        while (!File.Exists(current) && !Directory.Exists(current))
        {
            var name = Path.GetFileName(current);
            if (name.Length > 0) missing.Push(name);
            current = Path.GetDirectoryName(current)
                ?? throw new AccessScanException("AccessUnsafeOutputPath");
        }
        var resolved = new DirectoryInfo(current).ResolveLinkTarget(returnFinalTarget: true)?.FullName
            ?? Path.GetFullPath(current);
        while (missing.Count > 0) resolved = Path.Combine(resolved, missing.Pop());
        return Path.GetFullPath(resolved);
    }

    private static async Task<int> RunScanAsync(IReadOnlyDictionary<string, string> values, TextWriter output, CancellationToken cancellationToken)
    {
        var repo = Required(values, "--repo", "AccessRepoMissing");
        var database = Required(values, "--database", "AccessDatabasePathMissing");
        var outPath = Required(values, "--out", "AccessOutputMissing");
        var timeout = 600;
        if (values.TryGetValue("--timeout-seconds", out var timeoutValue) && !int.TryParse(timeoutValue, out timeout))
            throw new AccessScanException("AccessInvalidTimeout");

        var options = new AccessScanOptions(repo, database, outPath, timeout);
        var result = await new AccessScanRunner().RunAsync(options, cancellationToken);
        try { await AccessArtifactWriter.WriteAsync(outPath, result, AccessLimits.Default, cancellationToken); }
        catch (AccessScanException) { throw; }
        catch { throw new AccessScanException("AccessArtifactWriteFailed"); }
        await output.WriteLineAsync("TraceMap Access scan completed.");
        await output.WriteLineAsync($"Facts written: {result.Facts.Count}");
        await output.WriteLineAsync($"Analysis level: {result.Manifest.AnalysisLevel}");
        return 0;
    }

    internal static IReadOnlyDictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var key = args[index];
            if (!key.StartsWith("--", StringComparison.Ordinal) || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new AccessScanException("AccessInvalidArguments");
            if (!values.TryAdd(key, args[++index])) throw new AccessScanException("AccessDuplicateArgument");
        }
        return values;
    }

    private static string Required(IReadOnlyDictionary<string, string> values, string key, string classification) =>
        values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : throw new AccessScanException(classification);

    private static int PositiveInt(
        IReadOnlyDictionary<string, string> values,
        string key,
        int fallback,
        int maximum,
        string classification)
    {
        if (!values.TryGetValue(key, out var value)) return fallback;
        return int.TryParse(value, out var parsed) && parsed > 0 && parsed <= maximum
            ? parsed
            : throw new AccessScanException(classification);
    }

    private static string SafeFlowClassification(string value) =>
        value.StartsWith("AccessFlow", StringComparison.Ordinal)
            && value.Length <= 128
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? value
                : "AccessFlowCompositionFailed";

    private static string SafeCopyCloneClassification(string value) =>
        value.StartsWith("AccessCopyClone", StringComparison.Ordinal)
            && value.Length <= 128
            && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_')
                ? value
                : "AccessCopyCloneCompositionFailed";

    private static bool IsHelp(string value) => value is "--help" or "-h" or "help";
    private static async Task<int> UnknownAsync(TextWriter error) { await error.WriteLineAsync("error: AccessUnknownCommand"); return 1; }

    private static string RootHelp() => """
        TraceMap Microsoft Access adapter

        Usage:
          tracemap-access scan --repo <git-worktree> --database <repo-relative.accdb-or-mdb> --out <new-directory> [--timeout-seconds <30-3600>]
          tracemap-access enrich-design --base-scan <completed-output> --design-evidence <protected-directory> --out <new-directory>
          tracemap-access identity-project --base-scan <completed-output> --design-evidence <protected-directory> --out <new-directory>
          tracemap-access flow --index <completed-index.sqlite> --out <new-directory> [--max-depth <1-64>] [--max-paths <1-10000>] [--max-gaps <1-10000>]
          tracemap-access copy-clone --index <completed-index.sqlite> --out <new-directory> [--max-candidates <1-10000>] [--max-flow-paths <1-10000>] [--max-gaps <1-10000>]
          tracemap-access --version

        The adapter reads static design metadata only. Design enrichment is Mac-compatible and never opens the database or launches Access.
        It does not read rows or execute queries, macros, forms, reports, or VBA.
        """;

    private static string IdentityProjectHelp() => """
        Usage: tracemap-access identity-project --base-scan <completed-output> --design-evidence <protected-directory> --out <new-directory>

        Writes an independently deletable hidden-local identity projection for owner review.
        The output is not a standard scan artifact and is not accepted by combine, vault, public-site, or release-publication workflows.
        Raw design text, inline SQL, expressions, VBA, macro bodies, credentials, and local paths are omitted.
        """;

    private static string ScanHelp() => """
        Usage: tracemap-access scan --repo <git-worktree> --database <repo-relative.accdb-or-mdb> --out <new-directory> [--timeout-seconds <30-3600>]

        Requirements: Windows, installed Microsoft Access/DAO, clean tracked database at checked-out HEAD, and a non-existing output directory.
        Outputs: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
        """;

    private static string EnrichDesignHelp() => """
        Usage: tracemap-access enrich-design --base-scan <completed-output> --design-evidence <protected-directory> --out <new-directory>

        Composes an explicitly supplied source-neutral design bundle with an immutable completed Access scan.
        The command does not require Windows, Microsoft Access, COM, or the original database file.
        Protected design input is read transiently and is never copied into the output.
        Outputs: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
        """;

    private static string FlowHelp() => """
        Usage: tracemap-access flow --index <completed-index.sqlite> --out <new-directory> [--max-depth <1-64>] [--max-paths <1-10000>] [--max-gaps <1-10000>]

        Composes bounded static candidate trails from already-persisted Microsoft Access evidence.
        The command is Mac-compatible and does not open a database, launch Access, read rows, or execute queries, VBA, macros, forms, or reports.
        Outputs: access-flow.md and access-flow.json.
        """;

    private static string CopyCloneHelp() => """
        Usage: tracemap-access copy-clone --index <completed-index.sqlite> --out <new-directory> [--max-candidates <1-10000>] [--max-flow-paths <1-10000>] [--max-gaps <1-10000>]

        Composes conservative static mutation candidates from already-persisted Microsoft Access query and flow evidence.
        Candidate never means proven copy, clone, business intent, execution, runtime reachability, or completeness.
        The command is Mac-compatible and does not open a database, launch Access, read rows, or execute queries, VBA, macros, forms, or reports.
        Outputs: access-copy-clone.md and access-copy-clone.json.
        """;
}
