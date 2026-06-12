using TraceMap.Core;
using TraceMap.Reduction;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Cli;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        return await TraceMapCommand.RunAsync(args, Console.Out, Console.Error);
    }
}

public static class TraceMapCommand
{
    public static async Task<int> RunAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(RootHelp());
            return 0;
        }

        var command = args[0].ToLowerInvariant();
        var rest = args.Skip(1).ToArray();

        if (rest.Any(IsHelp))
        {
            await output.WriteLineAsync(command switch
            {
                "scan" => ScanHelp(),
                "report" => ReportHelp(),
                "reduce" => ReduceHelp(),
                "flow" => FlowHelp(),
                "relate" => RelateHelp(),
                _ => RootHelp()
            });
            return command is "scan" or "report" or "reduce" or "flow" or "relate" ? 0 : 1;
        }

        try
        {
            return command switch
            {
                "scan" => await RunScanAsync(rest, output, error, cancellationToken),
                "report" => await NotImplementedYetAsync("report", error),
                "reduce" => await RunReduceAsync(rest, output, error, cancellationToken),
                "flow" => await RunFlowAsync(rest, output, error, cancellationToken),
                "relate" => await RunRelateAsync(rest, output, error, cancellationToken),
                _ => await UnknownCommandAsync(command, error)
            };
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
    }

    private static async Task<int> RunReduceAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: reduce requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--contract-delta", out var contractDeltaPath) || string.IsNullOrWhiteSpace(contractDeltaPath))
        {
            await error.WriteLineAsync("error: reduce requires --contract-delta <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: reduce requires --out <path>.");
            return 1;
        }

        var report = await ContractDeltaReducer.ReduceAsync(
            new ReduceOptions(indexPath, contractDeltaPath, outputPath),
            cancellationToken);
        var reportPath = Path.GetExtension(Path.GetFullPath(outputPath)).Equals(".md", StringComparison.OrdinalIgnoreCase)
            ? Path.GetFullPath(outputPath)
            : Path.Combine(Path.GetFullPath(outputPath), "impact-report.md");

        await output.WriteLineAsync($"TraceMap reduce completed: {reportPath}");
        await output.WriteLineAsync($"Findings written: {report.Findings.Count}");
        return 0;
    }

    private static async Task<int> RunScanAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--repo", out var repoPath) || string.IsNullOrWhiteSpace(repoPath))
        {
            await error.WriteLineAsync("error: scan requires --repo <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: scan requires --out <path>.");
            return 1;
        }

        var result = ScanEngine.Scan(new ScanOptions(
            repoPath,
            outputPath,
            SolutionPaths: values.GetMany("--solution"),
            ProjectPaths: values.GetMany("--project"),
            IncludeGlobs: values.GetMany("--include"),
            ExcludeGlobs: values.GetMany("--exclude"),
            TargetFramework: values.GetValueOrDefault("--target-framework"),
            Restore: values.HasFlag("--restore")));
        var fullOutputPath = Path.GetFullPath(outputPath);
        var logsPath = Path.Combine(fullOutputPath, "logs");
        Directory.CreateDirectory(logsPath);

        await ManifestWriter.WriteAsync(Path.Combine(fullOutputPath, "scan-manifest.json"), result.Manifest, cancellationToken);
        await JsonlFactWriter.WriteAsync(Path.Combine(fullOutputPath, "facts.ndjson"), result.Facts, cancellationToken);
        SqliteIndexWriter.Write(Path.Combine(fullOutputPath, "index.sqlite"), result.Manifest, result.Facts);
        await MarkdownReportWriter.WriteAsync(Path.Combine(fullOutputPath, "report.md"), result, cancellationToken);
        await WriteAnalyzerLogAsync(Path.Combine(logsPath, "analyzer.log"), result, cancellationToken);

        await output.WriteLineAsync($"TraceMap scan completed: {fullOutputPath}");
        await output.WriteLineAsync($"Facts written: {result.Facts.Count}");
        await output.WriteLineAsync($"Analysis level: {result.Manifest.AnalysisLevel}");
        return 0;
    }

    private static async Task<int> RunFlowAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: flow requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--symbol", out var symbol) || string.IsNullOrWhiteSpace(symbol))
        {
            await error.WriteLineAsync("error: flow requires --symbol <symbol-or-fragment>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: flow requires --out <path>.");
            return 1;
        }

        var maxDepth = ParsePositiveInt(values, "--max-depth", 5);
        var maxPaths = ParsePositiveInt(values, "--max-paths", 50);
        var report = await FlowPathReporter.WriteAsync(
            new FlowOptions(indexPath, symbol, outputPath, maxDepth, maxPaths),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap flow completed: {report.ReportPath}");
        await output.WriteLineAsync($"Parameter-forward edges indexed: {report.EdgeCount}");
        await output.WriteLineAsync($"Paths written: {report.PathCount}");
        return 0;
    }

    private static async Task<int> RunRelateAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: relate requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--symbol", out var symbol) || string.IsNullOrWhiteSpace(symbol))
        {
            await error.WriteLineAsync("error: relate requires --symbol <symbol-or-fragment>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: relate requires --out <path>.");
            return 1;
        }

        var maxDepth = ParsePositiveInt(values, "--max-depth", 5);
        var maxPaths = ParsePositiveInt(values, "--max-paths", 100);
        var direction = values.GetValueOrDefault("--direction") ?? "both";
        var report = await SymbolRelationshipReporter.WriteAsync(
            new RelationshipOptions(indexPath, symbol, outputPath, direction, maxDepth, maxPaths),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap relate completed: {report.ReportPath}");
        await output.WriteLineAsync($"Symbol relationships indexed: {report.RelationshipCount}");
        await output.WriteLineAsync($"Paths written: {report.PathCount}");
        return 0;
    }

    private static async Task WriteAnalyzerLogAsync(string path, ScanResult result, CancellationToken cancellationToken)
    {
        var lines = new List<string>
        {
            $"scanId={result.Manifest.ScanId}",
            $"repo={result.Manifest.RepoName}",
            $"commitSha={result.Manifest.CommitSha}",
            $"analysisLevel={result.Manifest.AnalysisLevel}",
            $"buildStatus={result.Manifest.BuildStatus}",
            $"facts={result.Facts.Count}"
        };
        lines.AddRange(result.Manifest.KnownGaps.Select(gap => $"knownGap={gap}"));
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static ParsedOptions ParseOptions(string[] args)
    {
        var values = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var flags = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument: {arg}");
            }

            if (arg is "--restore")
            {
                flags.Add(arg);
                continue;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for {arg}.");
            }

            if (!values.TryGetValue(arg, out var list))
            {
                list = [];
                values[arg] = list;
            }

            list.AddRange(args[++index]
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        }

        return new ParsedOptions(values, flags);
    }

    private static int ParsePositiveInt(ParsedOptions values, string key, int defaultValue)
    {
        if (!values.TryGetValue(key, out var value))
        {
            return defaultValue;
        }

        if (int.TryParse(value, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        throw new ArgumentException($"{key} must be a positive integer.");
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "help";
    }

    private sealed class ParsedOptions(
        Dictionary<string, List<string>> values,
        HashSet<string> flags)
    {
        public bool TryGetValue(string key, out string? value)
        {
            if (values.TryGetValue(key, out var list) && list.Count > 0)
            {
                value = list[^1];
                return true;
            }

            value = null;
            return false;
        }

        public string? GetValueOrDefault(string key)
        {
            return TryGetValue(key, out var value) ? value : null;
        }

        public IReadOnlyList<string> GetMany(string key)
        {
            return values.TryGetValue(key, out var list) ? list : [];
        }

        public bool HasFlag(string key)
        {
            return flags.Contains(key);
        }
    }

    private static async Task<int> NotImplementedYetAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"error: tracemap {command} is a Milestone 0 command skeleton and is not implemented yet.");
        return 2;
    }

    private static async Task<int> UnknownCommandAsync(string command, TextWriter error)
    {
        await error.WriteLineAsync($"error: unknown command '{command}'.");
        return 1;
    }

    private static string RootHelp()
    {
        return """
            TraceMap deterministic C# repository indexer

            Usage:
              tracemap scan --repo <path> --out <path>
              tracemap report --index <path> --out <path>
              tracemap reduce --index <path> --contract-delta <path> --out <path>
              tracemap flow --index <path> --symbol <symbol-or-fragment> --out <path>
              tracemap relate --index <path> --symbol <symbol-or-fragment> --out <path>

            Commands:
              scan      Inventory a repository and emit TraceMap artifacts.
              report    Generate a report from an index. Skeleton in Milestone 0.
              reduce    Reduce a contract delta against an index.
              flow      Trace deterministic parameter-forwarding paths.
              relate    Trace deterministic symbol relationship paths.
            """;
    }

    private static string ScanHelp()
    {
        return """
            Usage:
              tracemap scan --repo <path> --out <path> [--solution <path>] [--project <path>] [--include <glob>] [--exclude <glob>] [--target-framework <tfm>] [--restore]

            Required:
              --repo <path>   Repository or folder to scan.
              --out <path>    Output directory for TraceMap artifacts.

            Optional:
              --solution <path>        Solution to load. Repeat or comma-separate for multiple.
              --project <path>         Project to load. Repeat or comma-separate for multiple.
              --include <glob>         Include only matching inventoried paths. Repeatable.
              --exclude <glob>         Exclude matching inventoried paths. Repeatable.
              --target-framework <tfm> MSBuild TargetFramework property for semantic load.
              --restore                Run dotnet restore for selected solution/project targets before semantic load.

            Outputs:
              scan-manifest.json
              facts.ndjson
              index.sqlite
              report.md
              logs/analyzer.log
            """;
    }

    private static string ReportHelp()
    {
        return """
            Usage:
              tracemap report --index <path> --out <path>

            Status:
              Command skeleton only in Milestone 0.
            """;
    }

    private static string ReduceHelp()
    {
        return """
            Usage:
              tracemap reduce --index <path> --contract-delta <path> --out <path>

            Required:
              --index <path>             Existing TraceMap index.sqlite.
              --contract-delta <path>    Contract delta JSON file.
              --out <path>               Output directory or impact-report.md path.

            Outputs:
              impact-report.md
            """;
    }

    private static string FlowHelp()
    {
        return """
            Usage:
              tracemap flow --index <path> --symbol <symbol-or-fragment> --out <path> [--max-depth <n>] [--max-paths <n>]

            Required:
              --index <path>             Existing TraceMap index.sqlite.
              --symbol <symbol>          Source parameter symbol or fragment to trace.
              --out <path>               Output directory or flow-report.md path.

            Optional:
              --max-depth <n>            Maximum forwarding edges per path. Default: 5.
              --max-paths <n>            Maximum paths to write. Default: 50.

            Outputs:
              flow-report.md
            """;
    }

    private static string RelateHelp()
    {
        return """
            Usage:
              tracemap relate --index <path> --symbol <symbol-or-fragment> --out <path> [--direction <incoming|outgoing|both>] [--max-depth <n>] [--max-paths <n>]

            Required:
              --index <path>             Existing TraceMap index.sqlite.
              --symbol <symbol>          Symbol ID/display name fragment to trace.
              --out <path>               Output directory or relationship-report.md path.

            Optional:
              --direction <value>        incoming, outgoing, or both. Default: both.
              --max-depth <n>            Maximum relationship edges per path. Default: 5.
              --max-paths <n>            Maximum paths to write. Default: 100.

            Outputs:
              relationship-report.md
            """;
    }
}
