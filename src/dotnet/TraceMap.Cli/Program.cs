using TraceMap.Core;
using TraceMap.Combine;
using TraceMap.EndpointAlignment;
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
                "export" => ExportHelp(),
                "endpoints" => EndpointsHelp(),
                "combine" => CombineHelp(),
                "paths" => PathsHelp(),
                "route-flow" => RouteFlowHelp(),
                "property-flow" => PropertyFlowHelp(),
                "diff" => DiffHelp(),
                "snapshot-diff" => SnapshotDiffHelp(),
                "impact" => ImpactHelp(),
                "reverse" => ReverseHelp(),
                "release-review" => ReleaseReviewHelp(),
                "portfolio" => PortfolioHelp(),
                "package-impact" => PackageImpactHelp(),
                "vault" => VaultHelp(),
                "docs-export" => DocsExportHelp(),
                "contract-diff" => ContractDiffHelp(),
                "baseline" => BaselineHelp(),
                "evidence-pack" => EvidencePackHelp(),
                "explorer" => ExplorerHelp(),
                _ => RootHelp()
            });
            return command is "scan" or "report" or "reduce" or "flow" or "relate" or "export" or "endpoints" or "combine" or "paths" or "route-flow" or "property-flow" or "diff" or "snapshot-diff" or "impact" or "reverse" or "release-review" or "portfolio" or "package-impact" or "vault" or "docs-export" or "contract-diff" or "baseline" or "evidence-pack" or "explorer" ? 0 : 1;
        }

        try
        {
            return command switch
            {
                "scan" => await RunScanAsync(rest, output, error, cancellationToken),
                "report" => await RunReportAsync(rest, output, error, cancellationToken),
                "reduce" => await RunReduceAsync(rest, output, error, cancellationToken),
                "flow" => await RunFlowAsync(rest, output, error, cancellationToken),
                "relate" => await RunRelateAsync(rest, output, error, cancellationToken),
                "export" => await RunExportAsync(rest, output, error, cancellationToken),
                "endpoints" => await RunEndpointsAsync(rest, output, error, cancellationToken),
                "combine" => await RunCombineAsync(rest, output, error, cancellationToken),
                "paths" => await RunPathsAsync(rest, output, error, cancellationToken),
                "route-flow" => await RunRouteFlowAsync(rest, output, error, cancellationToken),
                "property-flow" => await RunPropertyFlowAsync(rest, output, error, cancellationToken),
                "diff" => await RunDiffAsync(rest, output, error, cancellationToken),
                "snapshot-diff" => await RunSnapshotDiffAsync(rest, output, error, cancellationToken),
                "impact" => await RunImpactAsync(rest, output, error, cancellationToken),
                "reverse" => await RunReverseAsync(rest, output, error, cancellationToken),
                "release-review" => await RunReleaseReviewAsync(rest, output, error, cancellationToken),
                "portfolio" => await RunPortfolioAsync(rest, output, error, cancellationToken),
                "package-impact" => await RunPackageImpactAsync(rest, output, error, cancellationToken),
                "vault" => await RunVaultAsync(rest, output, error, cancellationToken),
                "docs-export" => await RunDocsExportAsync(rest, output, error, cancellationToken),
                "contract-diff" => await RunContractDiffAsync(rest, output, error, cancellationToken),
                "baseline" => await RunBaselineAsync(rest, output, error, cancellationToken),
                "evidence-pack" => await RunEvidencePackAsync(rest, output, error, cancellationToken),
                "explorer" => await RunExplorerAsync(rest, output, error, cancellationToken),
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

        var hasContractDelta = values.TryGetValue("--contract-delta", out var contractDeltaPath) && !string.IsNullOrWhiteSpace(contractDeltaPath);
        var hasSqlSchemaDelta = values.TryGetValue("--sql-schema-delta", out var sqlSchemaDeltaPath) && !string.IsNullOrWhiteSpace(sqlSchemaDeltaPath);
        if (hasContractDelta && hasSqlSchemaDelta)
        {
            await error.WriteLineAsync("error: reduce accepts either --contract-delta <path> or --sql-schema-delta <path>, not both.");
            return 1;
        }

        if (!hasContractDelta && !hasSqlSchemaDelta)
        {
            await error.WriteLineAsync("error: reduce requires --contract-delta <path> or --sql-schema-delta <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: reduce requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: reduce --format must be markdown or json.");
            return 1;
        }

        var result = await ContractDeltaReducer.ReduceAsync(
            new ReduceOptions(
                indexPath,
                contractDeltaPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--change-id"),
                values.GetValueOrDefault("--kind"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--endpoint"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--include-reverse"),
                values.HasFlag("--exit-code"),
                ParsePositiveInt(values, "--max-findings", 100),
                ParsePositiveInt(values, "--max-evidence-rows", 500),
                ParsePositiveInt(values, "--max-paths-per-change", 5),
                ParsePositiveInt(values, "--max-context-queries", 50),
                ParsePositiveInt(values, "--max-gaps", 1000),
                sqlSchemaDeltaPath,
                values.GetValueOrDefault("--table"),
                values.GetValueOrDefault("--column"),
                values.GetValueOrDefault("--query-shape")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap reduce completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Changes analyzed: {result.Report.Summary.ChangeCount}");
        await output.WriteLineAsync($"Findings written: {result.Report.Findings.Count}");
        await output.WriteLineAsync($"Evidence rows: {result.Report.Summary.EvidenceRowCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Sources: {result.Report.Index.SourceCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasActionableFindings ? 1 : 0;
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

        var sqlValidationSummaryPaths = values.GetMany("--sql-validation-summary");
        var sqlValidationAsOf = ParseSqlValidationAsOf(values, sqlValidationSummaryPaths);

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
        var staticPacket = SqlRunbookPacketBuilder.Build(result);
        var validationComposition = await SqlValidationSummaryReader.ReadAsync(
            sqlValidationSummaryPaths,
            [SqlRunbookPacketBuilder.ValidationExpectedSource(result, staticPacket, evaluatedAt: sqlValidationAsOf)],
            cancellationToken);
        var packetCandidate = SqlRunbookPacketBuilder.Build(result, validationComposition);
        await MarkdownReportWriter.WriteAsync(Path.Combine(fullOutputPath, "report.md"), result, packetCandidate, cancellationToken);
        if (SqlRunbookPacketBuilder.HasMeaningfulContent(packetCandidate))
            await SqlRunbookPacketWriter.WriteAsync(fullOutputPath, packetCandidate, cancellationToken);
        await WriteAnalyzerLogAsync(Path.Combine(logsPath, "analyzer.log"), result, cancellationToken);

        await output.WriteLineAsync($"TraceMap scan completed: {fullOutputPath}");
        await output.WriteLineAsync($"Facts written: {result.Facts.Count}");
        await output.WriteLineAsync($"Analysis level: {result.Manifest.AnalysisLevel}");
        return 0;
    }

    private static async Task<int> RunReportAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: report requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: report requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: report --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedDependencyReporter.WriteAsync(
            new CombinedDependencyReportOptions(indexPath, outputPath, format),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap report completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Facts: {result.Report.Summary.FactCount}");
        await output.WriteLineAsync($"Dependency edges: {result.Report.Summary.DependencyEdgeCount}");
        await output.WriteLineAsync($"Endpoint findings: {result.Report.Summary.EndpointFindingCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return 0;
    }

    private static async Task<int> RunPathsAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args, "--include-legacy-roots");
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: paths requires --index <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: paths requires --out <path>.");
            return 1;
        }

        foreach (var unsupported in new[] { "--to-endpoint", "--to-source", "--to-symbol" })
        {
            if (values.TryGetValue(unsupported, out _))
            {
                await error.WriteLineAsync($"error: paths {unsupported} is not supported in v1.");
                return 1;
            }
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: paths --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedDependencyPathReporter.WriteAsync(
            new CombinedDependencyPathOptions(
                IndexPath: indexPath,
                OutputPath: outputPath,
                Format: format,
                FromEndpoint: values.GetValueOrDefault("--from-endpoint"),
                FromSymbol: values.GetValueOrDefault("--from-symbol"),
                FromSource: values.GetValueOrDefault("--from-source"),
                FromWebFormsEvent: values.GetValueOrDefault("--from-webforms-event"),
                ToSurface: values.GetValueOrDefault("--to-surface"),
                SurfaceName: values.GetValueOrDefault("--surface-name"),
                SourcePair: values.GetValueOrDefault("--source-pair"),
                Classification: values.GetValueOrDefault("--classification"),
                View: values.GetValueOrDefault("--view"),
                IncludeLegacyRoots: values.HasFlag("--include-legacy-roots"),
                MaxDepth: ParsePositiveInt(values, "--max-depth", 8),
                MaxPaths: ParsePositiveInt(values, "--max-paths", 100),
                MaxFrontier: ParsePositiveInt(values, "--max-frontier", 10000),
                MessageDirection: values.GetValueOrDefault("--message-direction")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap paths completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Graph nodes: {result.Report.Summary.GraphNodeCount}");
        await output.WriteLineAsync($"Graph edges: {result.Report.Summary.GraphEdgeCount}");
        await output.WriteLineAsync($"Paths: {result.Report.Summary.PathCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return 0;
    }

    private static async Task<int> RunRouteFlowAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: route-flow requires --index <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: route-flow requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: route-flow --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedRouteFlowReporter.WriteAsync(
            new CombinedRouteFlowOptions(
                indexPath,
                outputPath,
                format,
                values.GetValueOrDefault("--route"),
                values.GetValueOrDefault("--client-call"),
                values.GetValueOrDefault("--from-endpoint"),
                values.GetValueOrDefault("--from-webforms-event"),
                values.GetValueOrDefault("--from-symbol"),
                values.GetValueOrDefault("--from-source"),
                values.GetValueOrDefault("--to-surface"),
                values.GetValueOrDefault("--surface-name"),
                values.GetValueOrDefault("--classification"),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-logic-rows", 200),
                ParsePositiveInt(values, "--max-gaps", 1000),
                values.HasFlag("--exit-code")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap route-flow completed: {CombinedReportHelpers.SafePath(result.MarkdownPath ?? result.JsonPath ?? outputPath)}");
        await output.WriteLineAsync($"Classification: {result.Report.Summary.Classification}");
        await output.WriteLineAsync($"Entry evidence: {result.Report.Summary.EntryEvidenceCount}");
        await output.WriteLineAsync($"Static flow rows: {result.Report.Summary.FlowRowCount}");
        await output.WriteLineAsync($"Business/data logic rows: {result.Report.Summary.LogicRowCount}");
        await output.WriteLineAsync($"Dependency surfaces: {result.Report.Summary.DependencySurfaceCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.ExitCodeWouldBeNonZero ? 1 : 0;
    }

    private static async Task<int> RunPropertyFlowAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: property-flow requires --index <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--property", out var propertySelector) || string.IsNullOrWhiteSpace(propertySelector))
        {
            await error.WriteLineAsync("error: property-flow requires --property <selector>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: property-flow requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: property-flow --format must be markdown or json.");
            return 1;
        }

        var result = await PropertyFlowReporter.WriteAsync(
            new PropertyFlowOptions(
                indexPath,
                outputPath,
                propertySelector,
                format,
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--framework") ?? "any",
                ParsePositiveInt(values, "--max-roots", 25),
                ParsePositiveInt(values, "--max-depth", 10),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-inventory", 1000),
                ParsePositiveInt(values, "--max-gaps", 1000),
                values.GetValueOrDefault("--observed-evidence")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap property-flow completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Selected roots: {result.Report.Summary.SelectedRootCount} of {result.Report.Summary.TotalCandidateCount}");
        await output.WriteLineAsync($"Paths: {result.Report.Summary.PathCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Truncated: {result.Report.Summary.Truncated}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return 0;
    }

    private static async Task<int> RunDiffAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--before", out var beforePath) || string.IsNullOrWhiteSpace(beforePath))
        {
            await error.WriteLineAsync("error: diff requires --before <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--after", out var afterPath) || string.IsNullOrWhiteSpace(afterPath))
        {
            await error.WriteLineAsync("error: diff requires --after <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: diff requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: diff --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedDependencyDiffer.WriteAsync(
            new CombinedDependencyDiffOptions(
                beforePath,
                afterPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--allow-identity-mismatch"),
                values.HasFlag("--exit-code"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--endpoint"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-diff-rows", 1000),
                ParsePositiveInt(values, "--max-gaps", 1000)),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap diff completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Source diffs: {result.Report.Summary.SourceDiffCount}");
        await output.WriteLineAsync($"Coverage diffs: {result.Report.Summary.CoverageDiffCount}");
        await output.WriteLineAsync($"Endpoint diffs: {result.Report.Summary.EndpointDiffCount}");
        await output.WriteLineAsync($"Surface diffs: {result.Report.Summary.SurfaceDiffCount}");
        await output.WriteLineAsync($"Edge diffs: {result.Report.Summary.EdgeDiffCount}");
        await output.WriteLineAsync($"Path diffs: {result.Report.Summary.PathDiffCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasDiffs ? 1 : 0;
    }

    private static async Task<int> RunSnapshotDiffAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--before", out var beforePath) || string.IsNullOrWhiteSpace(beforePath))
        {
            await error.WriteLineAsync("error: snapshot-diff requires --before <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--after", out var afterPath) || string.IsNullOrWhiteSpace(afterPath))
        {
            await error.WriteLineAsync("error: snapshot-diff requires --after <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: snapshot-diff requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: snapshot-diff --format must be markdown or json.");
            return 1;
        }

        var result = await SnapshotDiffReporter.WriteAsync(
            new SnapshotDiffOptions(
                beforePath,
                afterPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--allow-identity-mismatch"),
                values.HasFlag("--exit-code"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--endpoint"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-diff-rows", 1000),
                ParsePositiveInt(values, "--max-gaps", 1000)),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap snapshot-diff completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Source diffs: {result.Report.Summary.SourceDiffCount}");
        await output.WriteLineAsync($"Coverage diffs: {result.Report.Summary.CoverageDiffCount}");
        await output.WriteLineAsync($"Extractor diffs: {result.Report.Summary.ExtractorVersionDiffCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasDiffs ? 1 : 0;
    }

    private static async Task<int> RunContractDiffAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--before", out var beforePath) || string.IsNullOrWhiteSpace(beforePath))
        {
            await error.WriteLineAsync("error: contract-diff requires --before <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--after", out var afterPath) || string.IsNullOrWhiteSpace(afterPath))
        {
            await error.WriteLineAsync("error: contract-diff requires --after <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: contract-diff requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: contract-diff --format must be markdown or json.");
            return 1;
        }

        var result = await ApiDtoContractDiffReporter.WriteAsync(
            new ApiDtoContractDiffOptions(
                beforePath,
                afterPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--endpoint"),
                values.GetValueOrDefault("--type"),
                values.GetValueOrDefault("--property"),
                values.GetValueOrDefault("--change-kind"),
                ParsePositiveInt(values, "--max-diff-rows", 1000),
                ParsePositiveInt(values, "--max-evidence-rows", 500),
                ParsePositiveInt(values, "--max-gaps", 1000),
                values.HasFlag("--exit-code")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap contract-diff completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Endpoint diffs: {result.Report.Summary.EndpointDiffCount}");
        await output.WriteLineAsync($"DTO type diffs: {result.Report.Summary.DtoTypeDiffCount}");
        await output.WriteLineAsync($"DTO property diffs: {result.Report.Summary.DtoPropertyDiffCount}");
        await output.WriteLineAsync($"Method diffs: {result.Report.Summary.MethodDiffCount}");
        await output.WriteLineAsync($"Request/response diffs: {result.Report.Summary.RequestResponseDiffCount}");
        await output.WriteLineAsync($"Route shape diffs: {result.Report.Summary.RouteShapeDiffCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasActionableDiffs ? 1 : 0;
    }

    private static async Task<int> RunImpactAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--before", out var beforePath) || string.IsNullOrWhiteSpace(beforePath))
        {
            await error.WriteLineAsync("error: impact requires --before <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--after", out var afterPath) || string.IsNullOrWhiteSpace(afterPath))
        {
            await error.WriteLineAsync("error: impact requires --after <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: impact requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: impact --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedChangeImpactReporter.WriteAsync(
            new CombinedChangeImpactOptions(
                beforePath,
                afterPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--allow-identity-mismatch"),
                values.HasFlag("--exit-code"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--endpoint"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                ParsePositiveInt(values, "--max-impact-items", 100),
                ParsePositiveInt(values, "--max-paths-per-item", 5),
                ParsePositiveInt(values, "--max-path-queries", 50),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-gaps", 1000)),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap impact completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Diff rows considered: {result.Report.Summary.DiffCount}");
        await output.WriteLineAsync($"Impact items: {result.Report.Summary.ImpactItemCount}");
        await output.WriteLineAsync($"Endpoint impacts: {result.Report.Summary.EndpointImpactCount}");
        await output.WriteLineAsync($"Surface impacts: {result.Report.Summary.SurfaceImpactCount}");
        await output.WriteLineAsync($"Edge impacts: {result.Report.Summary.EdgeImpactCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasImpactItems ? 1 : 0;
    }

    private static async Task<int> RunReverseAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: reverse requires --index <combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: reverse requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: reverse --format must be markdown or json.");
            return 1;
        }

        var result = await CombinedReverseReporter.WriteAsync(
            new CombinedReverseOptions(
                indexPath,
                outputPath,
                format,
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                values.GetValueOrDefault("--to") ?? "endpoints",
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-surfaces", 200),
                ParsePositiveInt(values, "--max-roots", 100),
                ParsePositiveInt(values, "--max-paths-per-root", 5),
                ParsePositiveInt(values, "--max-gaps", 1000),
                values.HasFlag("--exit-code"),
                values.GetValueOrDefault("--message-direction")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap reverse completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Selected surfaces: {result.Report.Summary.SelectedSurfaceCount}");
        await output.WriteLineAsync($"Reverse roots: {result.Report.Summary.ReverseRootCount}");
        await output.WriteLineAsync($"Paths: {result.Report.Summary.PathCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasReverseEvidence ? 1 : 0;
    }

    private static async Task<int> RunReleaseReviewAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args, "--include-priority");
        if (!values.TryGetValue("--before", out var beforePath) || string.IsNullOrWhiteSpace(beforePath))
        {
            await error.WriteLineAsync("error: release-review requires --before <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--after", out var afterPath) || string.IsNullOrWhiteSpace(afterPath))
        {
            await error.WriteLineAsync("error: release-review requires --after <index.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: release-review requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: release-review --format must be markdown or json.");
            return 1;
        }

        var sqlValidationSummaryPaths = values.GetMany("--sql-validation-summary");
        var sqlValidationAsOf = ParseSqlValidationAsOf(values, sqlValidationSummaryPaths);

        var result = await ReleaseReviewReporter.WriteAsync(
            new ReleaseReviewOptions(
                beforePath,
                afterPath,
                outputPath,
                format,
                values.GetValueOrDefault("--scope"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--include-reverse"),
                values.HasFlag("--allow-identity-mismatch"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--endpoint"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                values.GetValueOrDefault("--contract-delta"),
                values.GetValueOrDefault("--sql-schema-delta"),
                values.GetValueOrDefault("--package-delta"),
                ParsePositiveInt(values, "--max-findings", 100),
                ParsePositiveInt(values, "--max-surface-rows", 50),
                ParsePositiveInt(values, "--max-paths", 25),
                ParsePositiveInt(values, "--max-gaps", 1000),
                ParsePositiveInt(values, "--max-checklist-items", 50),
                values.HasFlag("--include-priority"),
                sqlValidationSummaryPaths,
                sqlValidationAsOf),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap release-review completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Mode: {result.Report.Mode}");
        await output.WriteLineAsync($"Rollup: {result.Report.Summary.RollupClassification}");
        await output.WriteLineAsync($"Top changed surfaces: {result.Report.Summary.TopChangedSurfaceCount}");
        await output.WriteLineAsync($"Contract findings: {result.Report.Summary.ContractFindingCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        return 0;
    }

    private static async Task<int> RunPortfolioAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: portfolio requires --out <path>.");
            return 1;
        }

        if (values.HasFlag("--exit-code") || values.HasFlag("--allow-mixed-inputs") || values.HasFlag("--release-review"))
        {
            await error.WriteLineAsync("error: portfolio deferred v1 flag is not supported.");
            return 1;
        }

        var indexes = values.GetMany("--index");
        var labels = values.GetMany("--label");
        if (indexes.Count != labels.Count)
        {
            await error.WriteLineAsync("error: portfolio requires each --index to have exactly one --label.");
            return 1;
        }

        var inputs = indexes
            .Select((index, i) => new PortfolioInputSpec(labels[i], index))
            .ToArray();
        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: portfolio --format must be markdown or json.");
            return 1;
        }

        var result = await PortfolioReporter.WriteAsync(
            new PortfolioReportOptions(
                inputs,
                outputPath,
                format,
                values.GetValueOrDefault("--manifest"),
                values.GetValueOrDefault("--before-manifest"),
                values.GetValueOrDefault("--after-manifest"),
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--group"),
                values.GetValueOrDefault("--surface"),
                values.GetValueOrDefault("--surface-name"),
                values.HasFlag("--include-impact"),
                values.HasFlag("--include-paths"),
                values.HasFlag("--include-reverse"),
                ParsePositiveInt(values, "--max-sources", 200),
                ParsePositiveInt(values, "--max-surface-rows", 500),
                ParsePositiveInt(values, "--max-endpoint-findings", 500),
                ParsePositiveInt(values, "--max-shared-surfaces", 200),
                ParsePositiveInt(values, "--max-edge-rows", 500),
                ParsePositiveInt(values, "--max-diff-rows", 200),
                ParsePositiveInt(values, "--max-impact-items", 100),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-roots", 100),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-frontier", 10000),
                ParsePositiveInt(values, "--max-gaps", 1000)),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap portfolio completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Inputs: {result.Report.Summary.InputCount}");
        await output.WriteLineAsync($"Surfaces: {result.Report.Summary.SurfaceCount}");
        await output.WriteLineAsync($"Edges: {result.Report.Summary.EdgeCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.Summary.ReportCoverage}");
        return 0;
    }

    private static async Task<int> RunPackageImpactAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: package-impact requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--package-delta", out var packageDeltaPath) || string.IsNullOrWhiteSpace(packageDeltaPath))
        {
            await error.WriteLineAsync("error: package-impact requires --package-delta <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: package-impact requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("md", StringComparison.OrdinalIgnoreCase)
            && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: package-impact --format must be markdown or json.");
            return 1;
        }

        var result = await PackageUpgradeImpactReporter.WriteAsync(
            new PackageImpactOptions(
                indexPath,
                packageDeltaPath,
                outputPath,
                format,
                values.GetValueOrDefault("--source"),
                values.GetValueOrDefault("--package"),
                values.GetValueOrDefault("--ecosystem"),
                ParsePositiveInt(values, "--max-findings", 100),
                ParsePositiveInt(values, "--max-gaps", 1000),
                values.HasFlag("--exit-code")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap package-impact completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Sources: {result.Report.Summary.SourceCount}");
        await output.WriteLineAsync($"Delta changes: {result.Report.Summary.SelectedChangeCount}");
        await output.WriteLineAsync($"Package evidence: {result.Report.Summary.PackageEvidenceCount}");
        await output.WriteLineAsync($"Findings: {result.Report.Summary.FindingCount}");
        await output.WriteLineAsync($"Gaps: {result.Report.Summary.GapCount}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return values.HasFlag("--exit-code") && result.HasFindings ? 1 : 0;
    }

    private static async Task<int> RunVaultAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(VaultHelp());
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        if (subcommand != "export")
        {
            await error.WriteLineAsync("error: vault supports only the export subcommand.");
            return 1;
        }

        var values = ParseOptions(args.Skip(1).ToArray(), "--dry-run", "--force");
        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: vault export requires --out <vault-output>.");
            return 1;
        }

        var format = values.GetMany("--format").Count == 0
            ? "markdown,json"
            : string.Join(',', values.GetMany("--format"));
        var result = await VaultExporter.ExportAsync(
            new VaultExportOptions(
                values.GetValueOrDefault("--combined-index"),
                outputPath,
                values.GetMany("--paths-report"),
                values.GetMany("--reverse-report"),
                values.GetValueOrDefault("--source-claim-catalog"),
                values.GetValueOrDefault("--minimum-claim-level"),
                values.GetValueOrDefault("--date"),
                format,
                values.HasFlag("--dry-run"),
                values.HasFlag("--force"),
                values.GetMany("--property-flow-report")),
            cancellationToken);

        await output.WriteLineAsync(values.HasFlag("--dry-run")
            ? $"TraceMap vault export dry run: {Path.GetFullPath(outputPath)}"
            : $"TraceMap vault export completed: {Path.GetFullPath(outputPath)}");
        await output.WriteLineAsync($"Classification: {result.Graph.Classification}");
        await output.WriteLineAsync($"Nodes: {result.Graph.Nodes.Count}");
        await output.WriteLineAsync($"Edges: {result.Graph.Edges.Count}");
        await output.WriteLineAsync($"Gaps: {result.Graph.Gaps.Count}");
        await output.WriteLineAsync($"Files: {result.PlannedFiles.Count}");
        return 0;
    }

    private static async Task<int> RunDocsExportAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(DocsExportHelp());
            return 0;
        }

        if (args.Count(arg => arg == "--format") > 1)
        {
            await error.WriteLineAsync("error: docs-export accepts one --format value.");
            return 1;
        }

        if (OptionHasEmptyRawValue(args, "--format"))
        {
            await error.WriteLineAsync("error: docs-export --format must contain markdown, jsonl, or markdown,jsonl.");
            return 1;
        }

        if (OptionHasEmptyRawValue(args, "--families"))
        {
            await error.WriteLineAsync("error: docs-export --families must contain one or more closed family tokens.");
            return 1;
        }

        var values = ParseOptions(args, "--dry-run", "--force");
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: docs-export requires --index <index-or-combined.sqlite>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: docs-export requires --out <path>.");
            return 1;
        }

        var result = await EvidenceDocsExporter.ExportAsync(
            new EvidenceDocsExportOptions(
                indexPath,
                outputPath,
                values.GetMany("--route-flow-report"),
                values.GetMany("--paths-report"),
                values.GetMany("--reverse-report"),
                values.GetMany("--combined-report"),
                values.GetMany("--release-review-report"),
                values.GetMany("--vault-graph"),
                values.GetMany("--evidence-pack"),
                values.GetValueOrDefault("--source-claim-catalog"),
                values.GetValueOrDefault("--minimum-claim-level"),
                values.GetMany("--families").Count == 0 ? null : string.Join(',', values.GetMany("--families")),
                values.GetMany("--format").Count == 0 ? null : string.Join(',', values.GetMany("--format")),
                values.GetValueOrDefault("--date"),
                values.HasFlag("--dry-run"),
                values.HasFlag("--force"),
                values.GetMany("--property-flow-report")),
            cancellationToken);

        await output.WriteLineAsync(values.HasFlag("--dry-run")
            ? $"TraceMap docs-export dry run: {Path.GetFullPath(outputPath)}"
            : $"TraceMap docs-export completed: {Path.GetFullPath(outputPath)}");
        await output.WriteLineAsync($"Claim level: {result.Manifest.ClaimLevel}");
        await output.WriteLineAsync($"Chunks: {result.Chunks.Count}");
        await output.WriteLineAsync($"Gaps: {result.Manifest.Gaps.Count}");
        await output.WriteLineAsync($"Files: {result.PlannedFiles.Count}");
        return 0;
    }

    private static async Task<int> RunExplorerAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(ExplorerHelp());
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        if (subcommand != "generate")
        {
            await error.WriteLineAsync("error: explorer supports only the generate subcommand.");
            return 1;
        }

        var values = ParseOptions(args.Skip(1).ToArray(), "--force");
        if (!values.TryGetValue("--input", out var inputPath) || string.IsNullOrWhiteSpace(inputPath))
        {
            await error.WriteLineAsync("error: explorer generate requires --input <artifact-dir>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: explorer generate requires --out <explorer-output>.");
            return 1;
        }

        var result = await StaticHtmlEvidenceExplorer.GenerateAsync(
            new StaticHtmlEvidenceExplorerOptions(
                inputPath,
                outputPath,
                values.GetValueOrDefault("--safety-profile"),
                values.HasFlag("--force")),
            cancellationToken);

        await output.WriteLineAsync("TraceMap explorer generate completed.");
        await output.WriteLineAsync($"Safety profile: {result.Manifest.SafetyProfile}");
        await output.WriteLineAsync($"Artifacts: {result.Manifest.Counts.ArtifactCount}");
        await output.WriteLineAsync($"Evidence rows: {result.Manifest.Counts.EvidenceRowCount}");
        await output.WriteLineAsync($"Gaps: {result.Manifest.Counts.GapCount}");
        await output.WriteLineAsync($"Files: {result.WrittenFiles.Count}");
        return 0;
    }

    private static async Task<int> RunBaselineAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(BaselineHelp());
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        var values = ParseOptions(args.Skip(1).ToArray(), "--dry-run", "--public-source");
        switch (subcommand)
        {
            case "create":
            {
                if (!values.TryGetValue("--scan-output", out var scanOutput) || string.IsNullOrWhiteSpace(scanOutput))
                {
                    await error.WriteLineAsync("error: baseline create requires --scan-output <path>.");
                    return 1;
                }

                if (!values.TryGetValue("--label", out var label) || string.IsNullOrWhiteSpace(label))
                {
                    await error.WriteLineAsync("error: baseline create requires --label <neutral-slug>.");
                    return 1;
                }

                if (!values.TryGetValue("--purpose", out var purpose) || string.IsNullOrWhiteSpace(purpose))
                {
                    await error.WriteLineAsync("error: baseline create requires --purpose <neutral-slug>.");
                    return 1;
                }

                if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
                {
                    await error.WriteLineAsync("error: baseline create requires --out <path>.");
                    return 1;
                }

                var result = await LegacyBaselineArtifacts.CreateAsync(
                    new LegacyBaselineCreateOptions(
                        scanOutput,
                        label,
                        purpose,
                        outputPath,
                        values.GetValueOrDefault("--classification") ?? LegacyBaselineClassifications.PublicSafe,
                        values.GetValueOrDefault("--created-at"),
                        values.HasFlag("--dry-run"),
                        values.HasFlag("--public-source")),
                    cancellationToken);

                await output.WriteLineAsync($"TraceMap baseline create {(values.HasFlag("--dry-run") ? "dry-run " : string.Empty)}completed: {result.Manifest.BaselineId}");
                await output.WriteLineAsync($"Safety classification: {result.Manifest.Safety.Classification}");
                await output.WriteLineAsync($"Facts: {result.Manifest.Counts.FactsTotal}");
                await output.WriteLineAsync($"Gaps: {result.Manifest.Counts.GapsTotal}");
                foreach (var diagnostic in result.Diagnostics)
                {
                    await WriteBaselineDiagnosticAsync(error, diagnostic);
                }

                if (result.ManifestPath is not null)
                {
                    await output.WriteLineAsync($"Manifest: {result.ManifestPath}");
                    await output.WriteLineAsync($"Summary: {result.SummaryPath}");
                }

                return result.Manifest.Safety.Classification == LegacyBaselineClassifications.Rejected ? 1 : 0;
            }

            case "validate":
            {
                if (!values.TryGetValue("--manifest", out var manifestPath) || string.IsNullOrWhiteSpace(manifestPath))
                {
                    await error.WriteLineAsync("error: baseline validate requires --manifest <path>.");
                    return 1;
                }

                var result = await LegacyBaselineArtifacts.ValidateAsync(new LegacyBaselineValidateOptions(manifestPath), cancellationToken);
                await output.WriteLineAsync($"TraceMap baseline validate completed: {manifestPath}");
                await output.WriteLineAsync($"Safety classification: {result.Classification}");
                await output.WriteLineAsync($"Valid: {result.IsValid.ToString().ToLowerInvariant()}");
                foreach (var diagnostic in result.Diagnostics)
                {
                    await WriteBaselineDiagnosticAsync(error, diagnostic);
                }

                return result.IsValid ? 0 : 1;
            }

            case "compare":
            {
                if (!values.TryGetValue("--baseline", out var baselinePath) || string.IsNullOrWhiteSpace(baselinePath))
                {
                    await error.WriteLineAsync("error: baseline compare requires --baseline <baseline-manifest.json>.");
                    return 1;
                }

                if (!values.TryGetValue("--candidate", out var candidatePath) || string.IsNullOrWhiteSpace(candidatePath))
                {
                    await error.WriteLineAsync("error: baseline compare requires --candidate <baseline-manifest.json>.");
                    return 1;
                }

                if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
                {
                    await error.WriteLineAsync("error: baseline compare requires --out <path>.");
                    return 1;
                }

                var result = await LegacyBaselineArtifacts.CompareAsync(
                    new LegacyBaselineCompareOptions(
                        baselinePath,
                        candidatePath,
                        outputPath,
                        values.GetValueOrDefault("--migration-map"),
                        values.GetValueOrDefault("--generated-at")),
                    cancellationToken);

                await output.WriteLineAsync($"TraceMap baseline compare completed: {result.JsonPath}");
                await output.WriteLineAsync($"Overall status: {result.Comparison.OverallStatus}");
                await output.WriteLineAsync($"Review entries: {result.Comparison.ReviewNeeded.Count}");
                foreach (var diagnostic in result.Diagnostics)
                {
                    await WriteBaselineDiagnosticAsync(error, diagnostic);
                }

                return 0;
            }

            default:
                await error.WriteLineAsync($"error: unknown baseline subcommand '{subcommand}'.");
                return 1;
        }
    }

    private static async Task WriteBaselineDiagnosticAsync(TextWriter error, LegacyBaselineValidationDiagnostic diagnostic)
    {
        await error.WriteLineAsync($"warning: {diagnostic.Category}: ruleId={diagnostic.RuleId}; path={diagnostic.Path}; {diagnostic.Message}");
    }

    private static async Task<int> RunEvidencePackAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(EvidencePackHelp());
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        var values = ParseOptions(args.Skip(1).ToArray(), "--dry-run", "--force");
        switch (subcommand)
        {
            case "create":
            {
                if (!values.TryGetValue("--input", out var input) || string.IsNullOrWhiteSpace(input))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --input <path>.");
                    return 1;
                }

                if (!values.TryGetValue("--input-kind", out var inputKind) || string.IsNullOrWhiteSpace(inputKind))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --input-kind <kind>.");
                    return 1;
                }

                if (!values.TryGetValue("--label", out var label) || string.IsNullOrWhiteSpace(label))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --label <neutral-slug>.");
                    return 1;
                }

                if (!values.TryGetValue("--purpose", out var purpose) || string.IsNullOrWhiteSpace(purpose))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --purpose <neutral-slug>.");
                    return 1;
                }

                if (!values.TryGetValue("--claim-level", out var claimLevel) || string.IsNullOrWhiteSpace(claimLevel))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --claim-level <local-only|demo-safe|public-safe>.");
                    return 1;
                }

                if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
                {
                    await error.WriteLineAsync("error: evidence-pack create requires --out <path>.");
                    return 1;
                }

                var result = await LegacyEvidencePacks.CreateAsync(
                    new LegacyEvidencePackCreateOptions(
                        input,
                        inputKind,
                        label,
                        purpose,
                        claimLevel,
                        outputPath,
                        values.GetValueOrDefault("--date"),
                        values.HasFlag("--dry-run")),
                    cancellationToken);

                await output.WriteLineAsync($"TraceMap evidence-pack create {(values.HasFlag("--dry-run") ? "dry-run " : string.Empty)}completed: {result.Pack.PackId}");
                await output.WriteLineAsync($"Claim level: {result.Pack.ClaimLevel}");
                await output.WriteLineAsync($"Safety classification: {result.Validation.Classification}");
                await output.WriteLineAsync($"Facts: {result.Pack.Summary.FactCount}");
                await output.WriteLineAsync($"Gaps: {result.Pack.Summary.GapCount + result.Pack.Gaps.Count}");
                foreach (var diagnostic in result.Validation.Diagnostics)
                {
                    await WriteEvidencePackDiagnosticAsync(error, diagnostic);
                }

                if (result.JsonPath is not null)
                {
                    await output.WriteLineAsync($"Pack: {result.JsonPath}");
                    await output.WriteLineAsync($"Markdown: {result.MarkdownPath}");
                    await output.WriteLineAsync($"Validation: {result.ValidationPath}");
                }

                return result.Validation.IsValid ? 0 : 1;
            }

            case "validate":
            {
                if (!values.TryGetValue("--pack", out var packPath) || string.IsNullOrWhiteSpace(packPath))
                {
                    await error.WriteLineAsync("error: evidence-pack validate requires --pack <evidence-pack.json>.");
                    return 1;
                }

                var result = await LegacyEvidencePacks.ValidateAsync(
                    new LegacyEvidencePackValidateOptions(packPath, values.GetValueOrDefault("--expected-claim-level")),
                    cancellationToken);
                await output.WriteLineAsync($"TraceMap evidence-pack validate completed: {packPath}");
                await output.WriteLineAsync($"Safety classification: {result.Classification}");
                await output.WriteLineAsync($"Valid: {result.IsValid.ToString().ToLowerInvariant()}");
                foreach (var diagnostic in result.Diagnostics)
                {
                    await WriteEvidencePackDiagnosticAsync(error, diagnostic);
                }

                return result.IsValid ? 0 : 1;
            }

            case "promote":
            {
                if (!values.TryGetValue("--pack", out var packPath) || string.IsNullOrWhiteSpace(packPath))
                {
                    await error.WriteLineAsync("error: evidence-pack promote requires --pack <evidence-pack.json>.");
                    return 1;
                }

                if (!values.TryGetValue("--markdown", out var markdownPath) || string.IsNullOrWhiteSpace(markdownPath))
                {
                    await error.WriteLineAsync("error: evidence-pack promote requires --markdown <evidence-pack.md>.");
                    return 1;
                }

                if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
                {
                    await error.WriteLineAsync("error: evidence-pack promote requires --out <path>.");
                    return 1;
                }

                var result = await LegacyEvidencePacks.PromoteAsync(
                    new LegacyEvidencePackPromoteOptions(
                        packPath,
                        markdownPath,
                        outputPath,
                        values.HasFlag("--force"),
                        values.HasFlag("--dry-run")),
                    cancellationToken);
                await output.WriteLineAsync($"TraceMap evidence-pack promote {(values.HasFlag("--dry-run") ? "dry-run " : string.Empty)}completed: {outputPath}");
                await output.WriteLineAsync($"Safety classification: {result.Validation.Classification}");
                await output.WriteLineAsync($"Valid: {result.Validation.IsValid.ToString().ToLowerInvariant()}");
                foreach (var diagnostic in result.Validation.Diagnostics)
                {
                    await WriteEvidencePackDiagnosticAsync(error, diagnostic);
                }

                return result.Validation.IsValid ? 0 : 1;
            }

            default:
                await error.WriteLineAsync($"error: unknown evidence-pack subcommand '{subcommand}'.");
                return 1;
        }
    }

    private static async Task WriteEvidencePackDiagnosticAsync(TextWriter error, LegacyEvidencePackValidationDiagnostic diagnostic)
    {
        await error.WriteLineAsync($"warning: {diagnostic.Category}: ruleId={diagnostic.RuleId}; path={diagnostic.Path}; {diagnostic.Message}");
    }

    private static async Task<int> RunEndpointsAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--client-index", out var clientIndexPath) || string.IsNullOrWhiteSpace(clientIndexPath))
        {
            await error.WriteLineAsync("error: endpoints requires --client-index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--server-index", out var serverIndexPath) || string.IsNullOrWhiteSpace(serverIndexPath))
        {
            await error.WriteLineAsync("error: endpoints requires --server-index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: endpoints requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "markdown";
        if (!format.Equals("markdown", StringComparison.OrdinalIgnoreCase) && !format.Equals("md", StringComparison.OrdinalIgnoreCase) && !format.Equals("json", StringComparison.OrdinalIgnoreCase))
        {
            await error.WriteLineAsync("error: endpoints --format must be markdown or json.");
            return 1;
        }

        var result = await EndpointAlignmentEngine.AlignAsync(
            new EndpointAlignmentOptions(
                clientIndexPath,
                serverIndexPath,
                outputPath,
                format.Equals("json", StringComparison.OrdinalIgnoreCase) ? "json" : "markdown",
                values.GetValueOrDefault("--client-label"),
                values.GetValueOrDefault("--server-label")),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap endpoints completed: {result.MarkdownPath ?? result.JsonPath}");
        await output.WriteLineAsync($"Findings written: {result.Report.Findings.Count}");
        await output.WriteLineAsync($"Report coverage: {result.Report.ReportCoverage}");
        return 0;
    }

    private static async Task<int> RunCombineAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        var indexPaths = values.GetMany("--index");
        if (indexPaths.Count == 0)
        {
            await error.WriteLineAsync("error: combine requires at least one --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: combine requires --out <path>.");
            return 1;
        }

        var labels = values.GetMany("--label");
        if (labels.Count > 0 && labels.Count != indexPaths.Count)
        {
            await error.WriteLineAsync("error: combine requires either no --label values or one --label value per --index.");
            return 1;
        }

        var result = await CombinedIndexBuilder.CombineAsync(
            new CombineOptions(indexPaths, outputPath, labels),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap combine completed: {result.OutputPath}");
        await output.WriteLineAsync($"Sources imported: {result.Sources.Count}");
        await output.WriteLineAsync($"Facts imported: {result.FactCount}");
        await output.WriteLineAsync($"Symbols imported: {result.SymbolCount}");
        await output.WriteLineAsync($"Relationships imported: {result.RelationshipCount}");
        await output.WriteLineAsync($"Call edges imported: {result.CallEdgeCount}");
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

    private static async Task<int> RunExportAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        var values = ParseOptions(args);
        if (!values.TryGetValue("--index", out var indexPath) || string.IsNullOrWhiteSpace(indexPath))
        {
            await error.WriteLineAsync("error: export requires --index <path>.");
            return 1;
        }

        if (!values.TryGetValue("--out", out var outputPath) || string.IsNullOrWhiteSpace(outputPath))
        {
            await error.WriteLineAsync("error: export requires --out <path>.");
            return 1;
        }

        var format = values.GetValueOrDefault("--format") ?? "json";
        var report = await IndexExporter.WriteAsync(
            new IndexExportOptions(indexPath, outputPath, format),
            cancellationToken);

        await output.WriteLineAsync($"TraceMap export completed: {report.OutputPath}");
        await output.WriteLineAsync($"Format: {report.Format}");
        await output.WriteLineAsync($"Facts exported: {report.FactCount}");
        await output.WriteLineAsync($"Relationships exported: {report.RelationshipCount}");
        await output.WriteLineAsync($"Call edges exported: {report.CallEdgeCount}");
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
        lines.AddRange(result.Facts
            .Where(fact => fact.FactType == FactTypes.AnalyzerCapabilityDiagnostic)
            .GroupBy(fact => new
            {
                Code = fact.Properties.GetValueOrDefault("capabilityCode") ?? "unknown",
                State = fact.Properties.GetValueOrDefault("capabilityState") ?? "unknown",
                Effect = fact.Properties.GetValueOrDefault("coverageEffect") ?? "unknown"
            })
            .OrderBy(group => group.Key.Code, StringComparer.Ordinal)
            .ThenBy(group => group.Key.State, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Effect, StringComparer.Ordinal)
            .Select(group => $"capability={group.Key.Code};state={group.Key.State};coverage={group.Key.Effect};count={group.Count()}"));
        await File.WriteAllLinesAsync(path, lines, cancellationToken);
    }

    private static ParsedOptions ParseOptions(string[] args, params string[] additionalFlags)
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

            if (arg is "--restore" or "--include-paths" or "--include-reverse" or "--include-impact" or "--allow-identity-mismatch" or "--exit-code" or "--allow-mixed-inputs" or "--release-review"
                || additionalFlags.Contains(arg, StringComparer.Ordinal))
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

    private static DateTimeOffset? ParseOptionalDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (DateTimeOffset.TryParse(value, out var parsed))
        {
            return parsed;
        }

        if (DateTime.TryParse($"{value}-01", out var date))
        {
            return new DateTimeOffset(DateTime.SpecifyKind(date, DateTimeKind.Utc));
        }

        throw new ArgumentException("Date values must be full dates or year-month values.");
    }

    private static DateTimeOffset? ParseSqlValidationAsOf(ParsedOptions values, IReadOnlyList<string> summaryPaths)
    {
        var text = values.GetValueOrDefault("--sql-validation-as-of");
        if (summaryPaths.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(text))
                throw new ArgumentException("--sql-validation-as-of requires at least one --sql-validation-summary.");
            return null;
        }
        if (string.IsNullOrWhiteSpace(text))
            throw new ArgumentException("--sql-validation-summary requires --sql-validation-as-of <RFC3339 timestamp> for deterministic freshness evaluation.");
        if (!SqlValidationSummaryReader.TryParseTimestamp(text, out var value))
            throw new ArgumentException("--sql-validation-as-of must use RFC3339 with an explicit offset.");
        return value;
    }

    private static bool IsHelp(string arg)
    {
        return arg is "-h" or "--help" or "help";
    }

    private static bool OptionHasEmptyRawValue(string[] args, string option)
    {
        for (var index = 0; index < args.Length; index++)
        {
            if (args[index] == option && (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]) || args[index + 1].StartsWith("--", StringComparison.Ordinal)))
            {
                return true;
            }
        }

        return false;
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
              tracemap export --index <path> --out <path> [--format <json|mermaid>]
              tracemap endpoints --client-index <path> --server-index <path> --out <path> [--format <markdown|json>]
              tracemap combine --index <path> [--index <path>] --out <combined.sqlite> [--label <label>]
              tracemap paths --index <combined.sqlite> --out <path> [selectors]
              tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path>
              tracemap snapshot-diff --before <index.sqlite> --after <index.sqlite> --out <path>
              tracemap contract-diff --before <index.sqlite> --after <index.sqlite> --out <path>
              tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path>
              tracemap reverse --index <combined.sqlite> --out <path> [selectors]
              tracemap release-review --before <index.sqlite> --after <index.sqlite> --out <path>
              tracemap portfolio --out <path> (--index <index.sqlite> --label <label> ... | --manifest <portfolio.json>)
              tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <path>
              tracemap vault export --combined-index <combined.sqlite> --out <vault-output>
              tracemap docs-export --index <index-or-combined.sqlite> --out <docs-output>
              tracemap baseline create --scan-output <path> --label <neutral-slug> --purpose <neutral-slug> --out <path>
              tracemap evidence-pack create --input <path> --input-kind <kind> --label <neutral-slug> --purpose <neutral-slug> --claim-level <level> --date <yyyy-MM> --out <path>
              tracemap explorer generate --input <artifact-dir> --out <explorer-output>

            Commands:
              scan      Inventory a repository and emit TraceMap artifacts.
              report    Generate a combined dependency report from a combined index.
              reduce    Reduce a contract delta against an index.
              flow      Trace deterministic parameter-forwarding paths.
              relate    Trace deterministic symbol relationship paths.
              export    Export a deterministic JSON summary or Mermaid graph from an index.
              endpoints Align client HTTP calls with server HTTP route bindings.
              combine   Combine multiple TraceMap indexes into one queryable SQLite database.
              paths     Trace deterministic dependency paths through a combined index.
              route-flow Report static route-centered call flow evidence from a combined index.
              diff      Compare two combined indexes and report static evidence changes.
              snapshot-diff Compare two TraceMap snapshots by source, coverage, and extractor evidence.
              contract-diff Compare API/DTO static contract evidence between two indexes.
              impact    Explain static change evidence between two combined indexes.
              reverse   Trace reverse static reachability from dependency surfaces.
              release-review Assemble a deterministic before/after release evidence packet.
              portfolio Summarize dependency evidence across many TraceMap indexes.
              package-impact Report static package upgrade evidence from indexed package declarations.
              vault    Export deterministic Markdown evidence notes and graph.json from existing TraceMap evidence.
              docs-export Generate deterministic Markdown and JSONL evidence docs for external ingestion.
              baseline Create, validate, and compare redacted legacy baseline summaries.
              evidence-pack Create, validate, and promote redacted legacy evidence packs.
              explorer Generate a local static HTML evidence explorer from existing TraceMap artifacts.
            """;
    }

    private static string ScanHelp()
    {
        return """
            Usage:
              tracemap scan --repo <path> --out <path> [--solution <path>] [--project <path>] [--include <glob>] [--exclude <glob>] [--target-framework <tfm>] [--restore] [--sql-validation-summary <path>]

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
              --sql-validation-summary <path>
                                       Explicit sql-validation-summary/v1 input. Repeatable; never executed.
              --sql-validation-as-of <timestamp>
                                       Required with summaries; RFC3339 instant used for deterministic freshness.

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
              tracemap report --index <combined.sqlite> --out <path> [--format <markdown|json>]

            Required:
              --index <path>             Combined TraceMap index from tracemap combine.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.

            Outputs:
              dependency-report.md and/or dependency-report.json
            """;
    }

    private static string PathsHelp()
    {
        return """
            Usage:
              tracemap paths --index <index.sqlite|combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --index <path>             TraceMap index or combined index.
              --out <path>               Output directory or file path.

            Selectors:
              --from-endpoint "<M> <P>"  Start from an HTTP endpoint method/path key.
              --from-symbol <symbol>     Start from matching source-local symbol candidates.
              --from-webforms-event <id>  Start from a WebForms event/root fact or selector.
              --from-source <label>      Constrain start evidence to a source label.
              --to-surface <kind>        sql-query, sql-persistence, http-route, http-client,
                                          package-config, wcf-operation, asmx-service,
                                          asmx-operation, asmx-client, asmx-config,
                                          asmx-metadata, legacy-data, dependency-surface,
                                          remoting-endpoint, remoting-registration,
                                          remoting-channel, remoting-object, remoting-api,
                                          message-queue, message-topic, message-subscription,
                                          message-exchange, message-stream, message-event,
                                          message-channel, message-unknown.
              --surface-name <text>      Exact name, or leading/trailing * wildcard.
              --message-direction <dir>  For message surfaces: publish, consume, bind, declare, or all.
              --source-pair <a>:<b>      Constrain endpoint crossing; escape literal colons as \:.
              --classification <value>   StrongStaticPath, ProbableStaticPath,
                                          NeedsReviewStaticPath, NoBackendEvidence,
                                          ReducedCoverage, or AnalysisGap.

            Legacy flow view:
              --include-legacy-roots      Include WebForms/API/service roots in path composition.
              --view legacy-flows         Use legacy static-flow wording and schema metadata.

            Bounds:
              --max-depth <n>            Default: 8.
              --max-paths <n>            Default: 100.
              --max-frontier <n>         Default: 10000.

            Outputs:
              paths-report.md and/or paths-report.json
            """;
    }

    private static string RouteFlowHelp()
    {
        return """
            Usage:
              tracemap route-flow --index <combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --index <path>             Combined TraceMap index from tracemap combine.
              --out <path>               Output directory or file path.

            Selectors:
              --route "<M> <P>"          Select server HTTP route evidence.
              --client-call "<M> <P>"    Select client HTTP call evidence.
              --from-endpoint "<M> <P>"  Reuse paths endpoint selector grammar.
              --from-webforms-event <id> Reuse paths WebForms root selector grammar.
              --from-symbol <symbol>     Reuse paths symbol selector grammar.
              --from-source <label>      Constrain entry evidence to a source label.
              --to-surface <kind>        sql-query, sql-persistence, http-route, http-client,
                                          package-config, wcf-operation, asmx-service,
                                          asmx-operation, asmx-client, asmx-config,
                                          asmx-metadata, remoting-endpoint,
                                          remoting-registration, remoting-channel,
                                          remoting-object, remoting-api, legacy-data,
                                          dependency-surface, message-queue, message-topic,
                                          message-subscription, message-exchange,
                                          message-stream, message-event, message-channel,
                                          or message-unknown.
              --surface-name <text>      Exact name, or leading/trailing * wildcard.
              --classification <value>   StrongStaticRouteFlow, ProbableStaticRouteFlow,
                                          NeedsReviewStaticRouteFlow, NoRouteFlowEvidence,
                                          or UnknownAnalysisGap.

            Bounds:
              --max-depth <n>            Default: 8.
              --max-paths <n>            Default: 100.
              --max-frontier <n>         Default: 10000.
              --max-logic-rows <n>       Default: 200.
              --max-gaps <n>             Default: 1000.
              --exit-code                Return 1 for review, no-evidence, unknown, or blocking-gap results.

            Outputs:
              route-flow-report.md and/or route-flow-report.json

            Notes:
              Route-flow reports are static evidence only. They do not prove runtime execution, traffic, auth, dependency-injection target selection, SQL execution, or production use.
            """;
    }

    private static string PropertyFlowHelp()
    {
        return """
            Usage:
              tracemap property-flow --index <combined.sqlite> --property <selector> --out <path> [--format <markdown|json>]

            Required:
              --index <path>             Combined TraceMap index from tracemap combine.
              --property <selector>      field:, control:, binding:, model:, dto:, symbol:, or fact: selector.
              --out <path>               Output directory or file path.

            Selectors:
              field:<name>               UI field or safe visible field/control name.
              control:<name>             Form control name such as formControlName or HTML name.
              binding:<name>             Template binding expression or property path.
              model:<type>.<property>    Model or view-model property evidence.
              dto:<type>.<property>      DTO/serializer contract property evidence.
              symbol:<id-or-display>     Source-local symbol identity or safe display name.
              fact:<combinedFactId>      Exact combined_facts.combined_fact_id.

            Filters:
              --source <label>           Case-insensitive exact source label filter.
              --framework <value>        angular, razor, or any. Default: any.
              --observed-evidence <path> Optional demo metadata JSON file. Does not upgrade static lineage.

            Bounds:
              --max-roots <n>            Default: 25.
              --max-depth <n>            Default: 10.
              --max-paths <n>            Default: 100.
              --max-frontier <n>         Default: 10000.
              --max-inventory <n>        Default: 1000.
              --max-gaps <n>             Default: 1000.

            Outputs:
              property-flow-report.md and/or property-flow-report.json

            Notes:
              Property-flow reports are static evidence only. They do not prove runtime UI visibility, submitted values, branch feasibility, auth, dependency-injection target selection, SQL execution, or production use.
            """;
    }

    private static string DiffHelp()
    {
        return """
            Usage:
              tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --before <path>            Earlier combined TraceMap index.
              --after <path>             Later combined TraceMap index.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --scope <value>            all, sources, endpoints, surfaces, edges, or paths. Comma-separated. Default: sources,endpoints,surfaces,edges.
              --include-paths            Enable bounded dependency path comparison.
              --allow-identity-mismatch  Continue when same source labels point at different source identities.
              --exit-code                Return exit code 1 when diff rows are present.
              --source <label>           Filter to one source label.
              --endpoint "<M> <P>"       Filter endpoint/path diffs to method/path key.
              --surface <kind>           sql-query, http-route, http-client, or package-config.
              --surface-name <text>      Exact case-insensitive surface name.
              --max-depth <n>            Path diff depth. Default: 8.
              --max-paths <n>            Path diff paths per snapshot. Default: 100.
              --max-frontier <n>         Path diff frontier cap. Default: 10000.
              --max-diff-rows <n>        Diff rows per kind. Default: 1000.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              diff-report.md and/or diff-report.json
            """;
    }

    private static string SnapshotDiffHelp()
    {
        return """
            Usage:
              tracemap snapshot-diff --before <index.sqlite> --after <index.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --before <path>            Earlier TraceMap index, single-language or combined.
              --after <path>             Later TraceMap index of the same mode.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --scope <value>            all, sources, coverage, endpoints, contract-shapes, surfaces, graph, gaps, extractors, or paths.
              --include-paths            Reserve bounded path comparison for combined indexes.
              --allow-identity-mismatch  Continue when same source labels point at different source identities.
              --exit-code                Return exit code 1 when snapshot diff rows are present.
              --source <label>           Filter to one source label.
              --endpoint "<M> <P>"       Filter future endpoint/path evidence to method/path key.
              --surface <kind>           Filter future surface evidence.
              --surface-name <text>      Filter future surface evidence by name.
              --max-depth <n>            Path diff depth. Default: 8.
              --max-paths <n>            Path diff paths per snapshot. Default: 100.
              --max-frontier <n>         Path diff frontier cap. Default: 10000.
              --max-diff-rows <n>        Diff rows per kind. Default: 1000.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              snapshot-diff-report.md and/or snapshot-diff-report.json
            """;
    }

    private static string ContractDiffHelp()
    {
        return """
            Usage:
              tracemap contract-diff --before <index.sqlite> --after <index.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --before <path>            Earlier TraceMap index, single-language or combined.
              --after <path>             Later TraceMap index of the same mode.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --scope <value>            all, endpoints, dto-types, dto-properties, methods, request-response, or route-shapes.
              --exit-code                Return exit code 1 only for Added, Removed, or ChangedEvidence rows.
              --source <label>           Filter combined indexes to one source label.
              --endpoint "<M> <P>"       Filter endpoint/route evidence to method/path key.
              --type <name>              Filter DTO/type evidence by exact safe identity or display name.
              --property <name>          Filter DTO property/member evidence by exact name.
              --change-kind <kind>       endpoint, dto-type, dto-property, method, request-response, or route-shape.
              --max-diff-rows <n>        Diff rows per kind. Default: 1000.
              --max-evidence-rows <n>    Reserved evidence row budget. Default: 500.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              contract-diff-report.md and/or contract-diff-report.json
            """;
    }

    private static string ImpactHelp()
    {
        return """
            Usage:
              tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --before <path>            Earlier combined TraceMap index.
              --after <path>             Later combined TraceMap index.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --scope <value>            all, sources, coverage, endpoints, surfaces, edges, or paths. Comma-separated. Default: sources,coverage,endpoints,surfaces,edges.
              --include-paths            Include bounded before/after path context for changed endpoint, surface, and edge items.
              --allow-identity-mismatch  Continue when same source labels point at different source identities.
              --exit-code                Return exit code 1 when impact items are present.
              --source <label>           Filter to one source label.
              --endpoint "<M> <P>"       Filter endpoint/path evidence to method/path key.
              --surface <kind>           sql-query, http-route, http-client, or package-config.
              --surface-name <text>      Exact case-insensitive surface name.
              --max-impact-items <n>     Impact rows. Default: 100.
              --max-paths-per-item <n>   Path rows per item when path evidence is included. Default: 5.
              --max-path-queries <n>     Reserved path-context query cap. Default: 50.
              --max-depth <n>            Path diff depth. Default: 8.
              --max-frontier <n>         Path diff frontier cap. Default: 10000.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              impact-report.md and/or impact-report.json
            """;
    }

    private static string ReverseHelp()
    {
        return """
            Usage:
              tracemap reverse --index <combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --index <path>             Combined TraceMap index from tracemap combine.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --exit-code                Return exit code 1 when reverse roots or paths are present.
              --source <label>           Filter selected surfaces and requested roots to one source label.
              --surface <kind>           sql-query, http-route, http-client, package-config,
                                          legacy-data, message-queue, message-topic,
                                          message-subscription, message-exchange,
                                          message-stream, message-event, message-channel,
                                          or message-unknown.
              --surface-name <text>      Exact case-insensitive surface name.
              --message-direction <dir>  For message surfaces: publish, consume, bind, declare, or all.
              --to <target>              endpoints, symbols, sources, or all. Default: endpoints.
              --max-surfaces <n>         Selected surfaces. Default: 200.
              --max-roots <n>            Reverse roots. Default: 100.
              --max-paths-per-root <n>   Paths per root. Default: 5.
              --max-depth <n>            Reverse traversal depth. Default: 8.
              --max-frontier <n>         Reverse traversal frontier cap. Default: 10000.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              reverse-report.md and/or reverse-report.json
            """;
    }

    private static string ReleaseReviewHelp()
    {
        return """
            Usage:
              tracemap release-review --before <index.sqlite> --after <index.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --before <path>            Earlier TraceMap index, single-language or combined.
              --after <path>             Later TraceMap index of the same mode.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --scope <value>            all, sources, coverage, surfaces, contracts, api-dto, sql-schema, sql-evidence, access-evidence, packages, paths, reverse, gaps, or checklist.
              --contract-delta <path>    Include contract delta impact context.
              --sql-schema-delta <path>  Validate SQL/schema delta input and report workflow status.
              --package-delta <path>     Validate package delta input and report deferred package-upgrade status.
              --sql-validation-summary <path>
                                         Compose an explicit sql-validation-summary/v1 input. Repeatable; never executed.
              --sql-validation-as-of <timestamp>
                                         Required with summaries; RFC3339 instant used for deterministic freshness.
              --include-paths            Include bounded path context where combined indexes support it.
              --include-reverse          Include bounded reverse context where combined indexes support it.
              --include-priority         Include deterministic review priority scoring in release-review output.
              --allow-identity-mismatch  Continue when combined source labels point at different source identities.
              --source <label>           Filter to one source label.
              --endpoint "<M> <P>"       Filter endpoint/path evidence to method/path key where compatible.
              --surface <kind>           sql-query, http-route, http-client, or package-config.
              --surface-name <text>      Exact case-insensitive surface name where compatible.
              --max-findings <n>         Release findings. Default: 100.
              --max-surface-rows <n>     Top changed surface rows. Default: 50.
              --max-paths <n>            Path/reverse rows exposed by release review. Default: 25.
              --max-gaps <n>             Gap rows. Default: 1000.
              --max-checklist-items <n>  Checklist rows. Default: 50.

            Outputs:
              release-review.md and/or release-review.json
            """;
    }

    private static string PortfolioHelp()
    {
        return """
            Usage:
              tracemap portfolio --out <path> --index <index.sqlite> --label <label> [--index <path> --label <label> ...]
              tracemap portfolio --out <path> --manifest <portfolio.json>
              tracemap portfolio --out <path> --before-manifest <portfolio.json> --after-manifest <portfolio.json>

            Required:
              --out <path>               Output directory or file path.

            Inputs:
              --index <path>             TraceMap single-language or combined index. Repeatable.
              --label <label>            Label paired with each --index. Repeatable.
              --manifest <path>          Portfolio manifest with version 1.0 and inputs.
              --before-manifest <path>   Earlier portfolio manifest for source-level comparison.
              --after-manifest <path>    Later portfolio manifest for source-level comparison.

            Optional:
              --format <value>           markdown or json. Directory outputs write both.
              --source <label>           Filter to one source or combined container label.
              --group <tag>              Filter to manifest group or role tag.
              --surface <kind>           http-client, http-route, sql-query, sql-persistence, or package-config.
              --surface-name <text>      Case-insensitive contained surface name.
              --include-impact           Request deferred portfolio impact context.
              --include-paths            Request deferred path context.
              --include-reverse          Request deferred reverse context.
              --max-sources <n>          Source rows. Default: 200.
              --max-surface-rows <n>     Dependency surface rows. Default: 500.
              --max-endpoint-findings <n> Endpoint findings. Default: 500.
              --max-shared-surfaces <n>  Shared surface groups. Default: 200.
              --max-edge-rows <n>        Dependency edge rows. Default: 500.
              --max-diff-rows <n>        Source comparison rows. Default: 200.
              --max-impact-items <n>     Deferred impact item cap. Default: 100.
              --max-paths <n>            Deferred path cap. Default: 100.
              --max-roots <n>            Deferred reverse root cap. Default: 100.
              --max-depth <n>            Deferred graph depth. Default: 8.
              --max-frontier <n>         Deferred graph frontier. Default: 10000.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              portfolio-report.md and/or portfolio-report.json
            """;
    }

    private static string PackageImpactHelp()
    {
        return """
            Usage:
              tracemap package-impact --index <index.sqlite> --package-delta <delta.json> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --index <path>             TraceMap single-language or combined index.
              --package-delta <path>     package-delta.v1 JSON file.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. Directory outputs write both.
              --source <label>           Filter to one source label.
              --package <name>           Filter delta changes by package name.
              --ecosystem <name>         Filter delta changes by ecosystem.
              --max-findings <n>         Finding rows. Default: 100.
              --max-gaps <n>             Gap rows. Default: 1000.
              --exit-code                Return exit code 1 when static package evidence findings are present.

            Delta schema:
              { "version": "package-delta.v1", "changes": [{ "id": "pkg-1", "packageName": "Newtonsoft.Json", "ecosystem": "nuget", "changeType": "updated", "oldVersion": "13.0.1", "newVersion": "13.0.3" }] }

            Outputs:
              package-impact-report.md and/or package-impact-report.json
            """;
    }

    private static string VaultHelp()
    {
        return """
            Usage:
              tracemap vault export --combined-index <combined.sqlite> --out <vault-output> [--format <markdown|json|markdown,json>]
              tracemap vault export --paths-report <paths-report.json> --out <vault-output>
              tracemap vault export --reverse-report <reverse-report.json> --out <vault-output>
              tracemap vault export --property-flow-report <property-flow-report.json> --out <vault-output>

            Inputs:
              --combined-index <path>          Existing combined TraceMap SQLite index. Read-only.
              --paths-report <path>            Existing paths-report.json. Repeatable.
              --reverse-report <path>          Existing reverse-report.json. Repeatable.
              --property-flow-report <path>    Existing property-flow-report.json. Repeatable; terminal-context navigation remains hidden/local.
              --source-claim-catalog <path>    source-claim-catalog.v1 JSON for demo/public promotion.

            Options:
              --out <path>                     Output vault directory.
              --minimum-claim-level <value>    hidden, demo-safe, or public-safe. Default: hidden.
              --date <yyyy-MM>                 Required for demo-safe and public-safe deterministic output.
              --format <value>                 markdown, json, or markdown,json. Default: markdown,json.
              --dry-run                        Validate and list planned files without writing.
              --force                          Replace stale generated files after validation.

            Outputs:
              graph.json, README.md, index.md, and deterministic Markdown notes.

            Notes:
              The vault is a local navigation aid over static evidence. It does not prove runtime behavior, deployment, release safety, vulnerabilities, production traffic, or impact.
            """;
    }

    private static string DocsExportHelp()
    {
        return """
            Usage:
              tracemap docs-export --index <index-or-combined.sqlite> --out <docs-output> [--format <markdown|jsonl|markdown,jsonl>]

            Inputs:
              --index <path>                    Existing TraceMap scan or combined SQLite index. Read-only.
              --route-flow-report <path>        Existing route-flow JSON report. Repeatable.
              --property-flow-report <path>     Existing property-flow JSON report. Repeatable.
              --paths-report <path>             Existing paths-report JSON. Repeatable.
              --reverse-report <path>           Existing reverse-report JSON. Repeatable.
              --combined-report <path>          Existing combined dependency report JSON. Repeatable.
              --release-review-report <path>    Existing release-review JSON. Repeatable.
              --vault-graph <path>              Existing vault graph JSON. Schema gaps are emitted unless compatible.
              --evidence-pack <path>            Existing evidence-pack JSON. Repeatable.
              --source-claim-catalog <path>     source-claim-catalog.v1 JSON for demo/public promotion.

            Options:
              --out <path>                      Output docs directory.
              --minimum-claim-level <value>     hidden, demo-safe, or public-safe. Default: hidden.
              --families <value>                Comma-separated closed family list.
              --format <value>                  markdown, jsonl, or markdown,jsonl. Default: markdown,jsonl.
              --date <yyyy-MM>                  Required for demo-safe and public-safe deterministic output.
              --dry-run                         Validate and list planned files without writing.
              --force                           Replace stale generated files after validation.

            Outputs:
              manifest.json, chunks.jsonl, README.md, index.md, and chunk Markdown files depending on --format.

            Notes:
              Docs export emits deterministic evidence documents for external systems. TraceMap does not call LLMs, generate embeddings, write vector databases, prompt-classify claims, rank retrieval, or answer questions.
            """;
    }

    private static string BaselineHelp()
    {
        return """
            Usage:
              tracemap baseline create --scan-output <path> --label <neutral-slug> --purpose <neutral-slug> --out <path> [--dry-run]
              tracemap baseline validate --manifest <baseline-manifest.json>
              tracemap baseline compare --baseline <baseline-manifest.json> --candidate <baseline-manifest.json> --out <path>

            Create required:
              --scan-output <path>       Existing TraceMap scan output under samples/ or ignored .tmp storage.
              --label <neutral-slug>     Neutral sample label. Paths, remotes, hostnames, and identity-looking labels are rejected.
              --purpose <neutral-slug>   Neutral baseline purpose such as original-parser-snapshot.
              --out <path>               .tmp/legacy-baselines/<baseline-id> or .kiro/baselines/legacy/<baseline-id>.

            Create optional:
              --classification <value>   public-safe or local-only. Default: public-safe.
              --created-at <yyyy-MM>     Fixture-pinned creation period for deterministic output.
              --public-source            Allow public repository identity hash and commit SHA.
              --dry-run                  Report safety classification without writing output files.

            Compare optional:
              --migration-map <path>     legacy-baseline-migration-map.v1 JSON file for schema, rule, or fact renames.
              --generated-at <yyyy-MM>   Fixture-pinned comparison period for deterministic output.

            Outputs:
              baseline-manifest.json, baseline-summary.md, comparison.json, comparison.md
            """;
    }

    private static string EvidencePackHelp()
    {
        return """
            Usage:
              tracemap evidence-pack create --input <path> --input-kind <kind> --label <neutral-slug> --purpose <neutral-slug> --claim-level <local-only|demo-safe|public-safe> --date <yyyy-MM> --out <path> [--dry-run]
              tracemap evidence-pack validate --pack <evidence-pack.json> [--expected-claim-level <local-only|demo-safe|public-safe>]
              tracemap evidence-pack promote --pack <evidence-pack.json> --markdown <evidence-pack.md> --out docs/evidence-packs/legacy/<pack-id> [--force] [--dry-run]

            Create required:
              --input <path>             Existing redacted summary, baseline manifest, or scan output.
              --input-kind <kind>        legacy-validation-summary, public-demo-summary, legacy-baseline, or scan-output.
              --label <neutral-slug>     Neutral sample label. Paths, remotes, hostnames, and identity-looking labels are rejected.
              --purpose <neutral-slug>   Neutral evidence-pack purpose such as legacy-validation-proof.
              --claim-level <value>      local-only, demo-safe, or public-safe.
              --out <path>               Usually .tmp/legacy-evidence-packs/<pack-id>.

            Create optional:
              --date <yyyy-MM>           Required for demo-safe and public-safe deterministic output.
              --dry-run                  Build and validate the pack without writing files.

            Validate optional:
              --expected-claim-level <value>  Fail when the pack claim level differs.

            Promote optional:
              --force                    Replace an existing approved promotion destination.
              --dry-run                  Validate promotion inputs without copying files.

            Outputs:
              evidence-pack.json, evidence-pack.md, validation-result.json
            """;
    }

    private static string ExplorerHelp()
    {
        return """
            Usage:
              tracemap explorer generate --input <artifact-dir> --out <explorer-output> [--safety-profile <public-demo|hidden-local>] [--force]

            Required:
              --input <artifact-dir>      Directory containing generated TraceMap artifacts such as scan-manifest.json and facts.ndjson.
              --out <explorer-output>     Output directory for the local static explorer.

            Optional:
              --safety-profile <value>    public-demo (default) or hidden-local.
              --force                     Overwrite prior TraceMap-generated explorer output.

            Outputs:
              index.html
              assets/explorer.css
              assets/explorer.js
              data/explorer-manifest.json
              data/explorer-data.json
              README.md

            Notes:
              The explorer is a local generated artifact, not the public tracemap.tools site.
              It renders selected generated artifacts and does not rescan source code,
              query databases, call services, or derive new impact conclusions.
            """;
    }

    private static string ReduceHelp()
    {
        return """
            Usage:
              tracemap reduce --index <path> --contract-delta <path> --out <path>
              tracemap reduce --index <path> --sql-schema-delta <path> --out <path>

            Required:
              --index <path>             Existing TraceMap index.sqlite.
              --contract-delta <path>    Contract delta JSON file. Mutually exclusive with --sql-schema-delta.
              --sql-schema-delta <path>  SQL/schema delta JSON file. Mutually exclusive with --contract-delta.
              --out <path>               Output directory, impact-report.md, or impact-report.json path.

            Optional:
              --format <value>           markdown or json. Directory outputs write both.
              --scope <value>            all or comma-separated contract kinds. Default: all.
              --source <label>           Combined-index source label filter.
              --change-id <id>           v2 contract-delta change id filter.
              --kind <kind>              Filter to one v2 contract kind.
              --table <name>             Filter SQL/schema changes by table selector.
              --column <name>            Filter SQL/schema changes by column or mapped-name selector.
              --query-shape <hash>       Filter SQL/schema changes by query shape hash.
              --endpoint "<M> <P>"       Filter endpoint contract changes by method/path.
              --surface <kind>           Filter dependency-surface contract changes by kind.
              --include-paths            Request bounded combined-index path context.
              --include-reverse          Request bounded combined-index reverse context.
              --exit-code                Return 1 for actionable findings; SQL/schema review-tier findings return 0.
              --max-findings <n>         Finding rows. Default: 100.
              --max-evidence-rows <n>    Evidence rows across all findings. Default: 500.
              --max-paths-per-change <n> Reserved path context cap. Default: 5.
              --max-context-queries <n>  Reserved context query cap. Default: 50.
              --max-gaps <n>             Gap rows. Default: 1000.

            Outputs:
              impact-report.md and/or impact-report.json
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

    private static string ExportHelp()
    {
        return """
            Usage:
              tracemap export --index <path> --out <path> [--format <json|mermaid>]

            Required:
              --index <path>             Existing TraceMap index.sqlite.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           json or mermaid. Default: json.

            Outputs:
              index-export.json or relationships.mmd
            """;
    }

    private static string EndpointsHelp()
    {
        return """
            Usage:
              tracemap endpoints --client-index <path> --server-index <path> --out <path> [--format <markdown|json>] [--client-label <label>] [--server-label <label>]

            Required:
              --client-index <path>      Client TraceMap index.sqlite.
              --server-index <path>      Server TraceMap index.sqlite.
              --out <path>               Output directory or file path.

            Optional:
              --format <value>           markdown or json. File outputs default to markdown; directory outputs write both.
              --client-label <label>     Human-readable client source label.
              --server-label <label>     Human-readable server source label.

            Outputs:
              endpoint-report.md and/or endpoint-report.json
            """;
    }

    private static string CombineHelp()
    {
        return """
            Usage:
              tracemap combine --index <path> [--index <path>] --out <combined.sqlite> [--label <label>]

            Required:
              --index <path>             Existing TraceMap index.sqlite. Repeat or comma-separate for multiple.
              --out <path>               Output combined SQLite database path.

            Optional:
              --label <label>            Source label. Provide none, or one per --index.

            Outputs:
              combined.sqlite with index_sources, combined_facts, combined_symbols, dependency tables, and derived-row placeholders.
        """;
    }

}
