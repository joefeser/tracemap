using TraceMap.Access;

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
            await output.WriteLineAsync(command == "scan" ? ScanHelp() : RootHelp());
            return command == "scan" ? 0 : 1;
        }

        try
        {
            return command switch
            {
                "scan" => await RunScanAsync(ParseOptions(rest), output, cancellationToken),
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

    private static bool IsHelp(string value) => value is "--help" or "-h" or "help";
    private static async Task<int> UnknownAsync(TextWriter error) { await error.WriteLineAsync("error: AccessUnknownCommand"); return 1; }

    private static string RootHelp() => """
        TraceMap Microsoft Access adapter

        Usage:
          tracemap-access scan --repo <git-worktree> --database <repo-relative.accdb-or-mdb> --out <directory> [--timeout-seconds <30-3600>]
          tracemap-access --version

        The adapter reads static design metadata only. It does not read rows or execute queries, macros, forms, reports, or VBA.
        """;

    private static string ScanHelp() => """
        Usage: tracemap-access scan --repo <git-worktree> --database <repo-relative.accdb-or-mdb> --out <directory> [--timeout-seconds <30-3600>]

        Requirements: Windows, installed Microsoft Access/DAO, clean tracked database at checked-out HEAD.
        Outputs: scan-manifest.json, facts.ndjson, index.sqlite, report.md, logs/analyzer.log.
        """;
}
