using TraceMap.Core;
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
                _ => RootHelp()
            });
            return command is "scan" or "report" or "reduce" ? 0 : 1;
        }

        try
        {
            return command switch
            {
                "scan" => await RunScanAsync(rest, output, error, cancellationToken),
                "report" => await NotImplementedYetAsync("report", error),
                "reduce" => await NotImplementedYetAsync("reduce", error),
                _ => await UnknownCommandAsync(command, error)
            };
        }
        catch (Exception ex)
        {
            await error.WriteLineAsync($"error: {ex.Message}");
            return 1;
        }
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

        var result = ScanEngine.Scan(new ScanOptions(repoPath, outputPath));
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

    private static Dictionary<string, string> ParseOptions(string[] args)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index++)
        {
            var arg = args[index];
            if (!arg.StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Unexpected argument: {arg}");
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Missing value for {arg}.");
            }

            values[arg] = args[++index];
        }

        return values;
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "help";
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

            Commands:
              scan      Inventory a repository and emit TraceMap artifacts.
              report    Generate a report from an index. Skeleton in Milestone 0.
              reduce    Reduce a contract delta against an index. Skeleton in Milestone 0.
            """;
    }

    private static string ScanHelp()
    {
        return """
            Usage:
              tracemap scan --repo <path> --out <path>

            Required:
              --repo <path>   Repository or folder to scan.
              --out <path>    Output directory for TraceMap artifacts.

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

            Status:
              Command skeleton only in Milestone 0.
            """;
    }
}
