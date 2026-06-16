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
                "diff" => DiffHelp(),
                "snapshot-diff" => SnapshotDiffHelp(),
                "impact" => ImpactHelp(),
                "reverse" => ReverseHelp(),
                "release-review" => ReleaseReviewHelp(),
                "portfolio" => PortfolioHelp(),
                "package-impact" => PackageImpactHelp(),
                "contract-diff" => ContractDiffHelp(),
                "baseline" => BaselineHelp(),
                _ => RootHelp()
            });
            return command is "scan" or "report" or "reduce" or "flow" or "relate" or "export" or "endpoints" or "combine" or "paths" or "diff" or "snapshot-diff" or "impact" or "reverse" or "release-review" or "portfolio" or "package-impact" or "contract-diff" or "baseline" ? 0 : 1;
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
                "diff" => await RunDiffAsync(rest, output, error, cancellationToken),
                "snapshot-diff" => await RunSnapshotDiffAsync(rest, output, error, cancellationToken),
                "impact" => await RunImpactAsync(rest, output, error, cancellationToken),
                "reverse" => await RunReverseAsync(rest, output, error, cancellationToken),
                "release-review" => await RunReleaseReviewAsync(rest, output, error, cancellationToken),
                "portfolio" => await RunPortfolioAsync(rest, output, error, cancellationToken),
                "package-impact" => await RunPackageImpactAsync(rest, output, error, cancellationToken),
                "contract-diff" => await RunContractDiffAsync(rest, output, error, cancellationToken),
                "baseline" => await RunBaselineAsync(rest, output, error, cancellationToken),
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

    private static async Task<int> RunBaselineAsync(string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || IsHelp(args[0]))
        {
            await output.WriteLineAsync(BaselineHelp());
            return 0;
        }

        var subcommand = args[0].ToLowerInvariant();
        var values = ParseOptions(args.Skip(1).ToArray());
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
                        values.HasFlag("--dry-run"),
                        ParseOptionalDate(values.GetValueOrDefault("--created-at")),
                        values.HasFlag("--local-only"),
                        values.HasFlag("--public-source")),
                    cancellationToken);

                await output.WriteLineAsync($"TraceMap baseline create completed: {result.Manifest.BaselineId}");
                await output.WriteLineAsync($"Safety classification: {result.Validation.Classification}");
                await output.WriteLineAsync($"Facts summarized: {result.Manifest.Counts.FactsTotal}");
                await output.WriteLineAsync($"Known gaps: {result.Manifest.KnownGaps.Count}");
                if (result.ManifestPath is not null)
                {
                    await output.WriteLineAsync($"Manifest: {result.ManifestPath}");
                    await output.WriteLineAsync($"Summary: {result.SummaryPath}");
                }

                return result.Validation.Classification == "rejected" ? 1 : 0;
            }
            case "validate":
            {
                if (!values.TryGetValue("--manifest", out var manifestPath) || string.IsNullOrWhiteSpace(manifestPath))
                {
                    await error.WriteLineAsync("error: baseline validate requires --manifest <baseline-manifest.json>.");
                    return 1;
                }

                var result = await LegacyBaselineArtifacts.ValidateAsync(new LegacyBaselineValidateOptions(manifestPath), cancellationToken);
                await output.WriteLineAsync($"TraceMap baseline validate completed: {result.Manifest.BaselineId}");
                await output.WriteLineAsync($"Safety classification: {result.Validation.Classification}");
                await output.WriteLineAsync($"Diagnostics: {result.Validation.Diagnostics.Count}");
                foreach (var diagnostic in result.Validation.Diagnostics)
                {
                    await output.WriteLineAsync($"- {diagnostic.Category}: {diagnostic.FilePath}");
                }

                return result.Validation.Classification == "rejected" ? 1 : 0;
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
                        ParseOptionalDate(values.GetValueOrDefault("--generated-at")),
                        values.GetValueOrDefault("--migration-map"),
                        values.HasFlag("--dry-run")),
                    cancellationToken);

                await output.WriteLineAsync($"TraceMap baseline compare completed: {result.Comparison.BaselineId} -> {result.Comparison.CandidateId}");
                await output.WriteLineAsync($"Overall status: {result.Comparison.OverallStatus}");
                await output.WriteLineAsync($"Review-needed entries: {result.Comparison.ReviewNeeded.Count}");
                if (result.JsonPath is not null)
                {
                    await output.WriteLineAsync($"Comparison JSON: {result.JsonPath}");
                    await output.WriteLineAsync($"Comparison Markdown: {result.MarkdownPath}");
                }

                return result.Validation.Classification == "rejected" ? 1 : 0;
            }
            default:
                await error.WriteLineAsync($"error: unknown baseline command '{subcommand}'.");
                return 1;
        }
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
        var values = ParseOptions(args);
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
                indexPath,
                outputPath,
                format,
                values.GetValueOrDefault("--from-endpoint"),
                values.GetValueOrDefault("--from-symbol"),
                values.GetValueOrDefault("--from-source"),
                values.GetValueOrDefault("--to-surface"),
                values.GetValueOrDefault("--surface-name"),
                values.GetValueOrDefault("--source-pair"),
                ParsePositiveInt(values, "--max-depth", 8),
                ParsePositiveInt(values, "--max-paths", 100),
                ParsePositiveInt(values, "--max-frontier", 10000)),
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
                values.HasFlag("--exit-code")),
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
        var values = ParseOptions(args);
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
                ParsePositiveInt(values, "--max-checklist-items", 50)),
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

            if (arg is "--restore" or "--include-paths" or "--include-reverse" or "--include-impact" or "--allow-identity-mismatch" or "--exit-code" or "--allow-mixed-inputs" or "--release-review" or "--dry-run" or "--local-only" or "--public-source")
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
              tracemap baseline create --scan-output <path> --label <slug> --purpose <slug> --out <path>

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
              diff      Compare two combined indexes and report static evidence changes.
              snapshot-diff Compare two TraceMap snapshots by source, coverage, and extractor evidence.
              contract-diff Compare API/DTO static contract evidence between two indexes.
              impact    Explain static change evidence between two combined indexes.
              reverse   Trace reverse static reachability from dependency surfaces.
              release-review Assemble a deterministic before/after release evidence packet.
              portfolio Summarize dependency evidence across many TraceMap indexes.
              package-impact Report static package upgrade evidence from indexed package declarations.
              baseline  Create, validate, and compare redacted legacy baseline artifacts.
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

    private static string BaselineHelp()
    {
        return """
            Usage:
              tracemap baseline create --scan-output <path> --label <slug> --purpose <slug> --out <path> [--dry-run] [--created-at <yyyy-MM>] [--local-only] [--public-source]
              tracemap baseline validate --manifest <baseline-manifest.json>
              tracemap baseline compare --baseline <baseline-manifest.json> --candidate <baseline-manifest.json> --out <path> [--dry-run] [--generated-at <yyyy-MM>] [--migration-map <path>]

            Required:
              --scan-output <path>       Ignored local scan output directory or samples/synthetic-legacy-scan.
              --label <slug>             Neutral sample label.
              --purpose <slug>           Neutral baseline purpose.
              --out <path>               Output directory.
              --manifest <path>          Redacted baseline manifest to validate.
              --baseline <path>          Original redacted baseline manifest.
              --candidate <path>         Candidate redacted baseline manifest.

            Optional:
              --dry-run                  Report classification without writing outputs.
              --created-at <yyyy-MM>     Pinned creation date for deterministic baselines.
              --generated-at <yyyy-MM>   Pinned comparison date.
              --local-only               Mark generated baseline local-only; output must stay under .tmp/legacy-baselines.
              --public-source            Allow public commit identity disclosure when manifest values are safe.
              --migration-map <path>     legacy-baseline-migration-map.v1 for renamed rules or fact types.

            Outputs:
              baseline-manifest.json and baseline-summary.md for create
              comparison.json and comparison.md for compare
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
              tracemap paths --index <combined.sqlite> --out <path> [--format <markdown|json>] [selectors]

            Required:
              --index <path>             Combined TraceMap index from tracemap combine.
              --out <path>               Output directory or file path.

            Selectors:
              --from-endpoint "<M> <P>"  Start from an HTTP endpoint method/path key.
              --from-symbol <symbol>     Start from matching source-local symbol candidates.
              --from-source <label>      Constrain start evidence to a source label.
              --to-surface <kind>        sql-query, http-route, http-client, or package-config.
              --surface-name <text>      Exact name, or leading/trailing * wildcard.
              --source-pair <a>:<b>      Constrain endpoint crossing; escape literal colons as \:.

            Bounds:
              --max-depth <n>            Default: 8.
              --max-paths <n>            Default: 100.
              --max-frontier <n>         Default: 10000.

            Outputs:
              paths-report.md and/or paths-report.json
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
              --surface <kind>           sql-query, http-route, http-client, or package-config.
              --surface-name <text>      Exact case-insensitive surface name.
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
              --scope <value>            all, sources, coverage, surfaces, contracts, api-dto, sql-schema, packages, paths, reverse, gaps, or checklist.
              --contract-delta <path>    Include contract delta impact context.
              --sql-schema-delta <path>  Validate SQL/schema delta input and report workflow status.
              --package-delta <path>     Validate package delta input and report deferred package-upgrade status.
              --include-paths            Include bounded path context where combined indexes support it.
              --include-reverse          Include bounded reverse context where combined indexes support it.
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
