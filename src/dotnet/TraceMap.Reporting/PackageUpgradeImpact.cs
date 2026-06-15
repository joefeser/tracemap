using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PackageImpactOptions(
    string IndexPath,
    string PackageDeltaPath,
    string OutputPath,
    string Format = "markdown",
    string? Source = null,
    string? Package = null,
    string? Ecosystem = null,
    int MaxFindings = 100,
    int MaxGaps = 1000,
    bool ExitCode = false);

public sealed record PackageImpactResult(
    PackageImpactDocument Report,
    string? MarkdownPath,
    string? JsonPath)
{
    public bool HasFindings => Report.Findings.Count > 0;
}

public sealed record PackageImpactDocument(
    string Version,
    string ReportCoverage,
    PackageImpactDelta Delta,
    PackageImpactSummary Summary,
    IReadOnlyList<CombinedReportSource> Sources,
    IReadOnlyList<PackageImpactFinding> Findings,
    IReadOnlyList<PackageImpactGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record PackageImpactDelta(
    string SchemaVersion,
    IReadOnlyList<PackageImpactChange> Changes);

public sealed record PackageImpactSummary(
    string IndexKind,
    int SourceCount,
    int DeltaChangeCount,
    int SelectedChangeCount,
    int PackageEvidenceCount,
    int FindingCount,
    int GapCount,
    bool FindingCapReached,
    bool GapCapReached);

public sealed record PackageImpactChange(
    string Id,
    string PackageName,
    string? Ecosystem,
    string ChangeType,
    string? OldVersion,
    string? OldVersionHash,
    string? NewVersion,
    string? NewVersionHash);

public sealed record PackageImpactFinding(
    string FindingId,
    string Classification,
    string ChangeId,
    string PackageName,
    string? Ecosystem,
    string ChangeType,
    string RuleId,
    string EvidenceRuleId,
    string EvidenceTier,
    string SourceLabel,
    string SourceIndexId,
    string ScanId,
    string CommitSha,
    string FactId,
    string OriginalFactId,
    string FactType,
    string FilePath,
    int StartLine,
    int EndLine,
    string? RequestedOldVersion,
    string? RequestedOldVersionHash,
    string? RequestedNewVersion,
    string? RequestedNewVersionHash,
    string? ObservedVersion,
    string? ObservedVersionHash,
    string? VersionHash,
    string? RedactionReason,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record PackageImpactGap(
    string GapId,
    string Classification,
    string Message,
    string RuleId,
    string EvidenceTier,
    string? ChangeId = null,
    string? PackageName = null,
    string? Ecosystem = null,
    string? SourceLabel = null,
    string? ScanId = null,
    string? CommitSha = null,
    IReadOnlyList<KeyValuePair<string, string>>? Metadata = null);

public static class PackageUpgradeImpactReporter
{
    private const string Version = "1.0";
    private const string DeltaVersion = "package-delta.v1";
    private const string RuleId = "package.upgrade.impact.v1";
    private const int MarkdownRowLimit = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    private static readonly IReadOnlyList<string> Limitations =
    [
        "Package upgrade findings are static package declaration evidence only. They do not prove package restore, installed versions, transitive dependency resolution, runtime loading, deployment, or API compatibility.",
        "Version values are descriptive and safely rendered or hashed. TraceMap does not interpret semantic versioning or compatibility.",
        "Missing package evidence under reduced coverage is an analysis gap, not proof that a package is absent.",
        "Exact package-name matching can miss aliases, relocated packages, shaded dependencies, generated manifests, imported build files, and dynamically declared dependencies.",
        "No vulnerability database, registry, changelog, license, LLM, embedding, vector, or prompt-based analysis is used."
    ];

    public static async Task<PackageImpactResult> WriteAsync(PackageImpactOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("package-impact requires --index <path>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.PackageDeltaPath))
        {
            throw new ArgumentException("package-impact requires --package-delta <path>.", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("package-impact requires --out <path>.", nameof(options));
        }

        var format = CombinedReportHelpers.NormalizeFormat(options.Format, "package-impact");
        var delta = await ReadDeltaAsync(options.PackageDeltaPath, cancellationToken);
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        var read = await ReadIndexAsync(connection, cancellationToken);
        var selectedSources = ApplySourceFilter(read.Sources, options.Source);
        var selectedSourceIds = selectedSources.Select(source => source.SourceIndexId).ToHashSet(StringComparer.Ordinal);
        var surfaces = CombinedDependencyReporter.BuildSurfaces(read.Facts)
            .Where(surface => surface.SurfaceKind == "package-config")
            .Where(surface => selectedSourceIds.Contains(surface.SourceIndexId))
            .OrderBy(surface => surface.SourceLabel, StringComparer.Ordinal)
            .ThenBy(surface => surface.PackageName ?? surface.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(surface => surface.FilePath, StringComparer.Ordinal)
            .ThenBy(surface => surface.StartLine)
            .ToArray();

        var selectedChanges = delta.Changes
            .Where(change => MatchesSelector(change.PackageName, options.Package))
            .Where(change => MatchesSelector(change.Ecosystem, options.Ecosystem))
            .OrderBy(change => change.PackageName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.Ecosystem ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(change => change.Id, StringComparer.Ordinal)
            .ToArray();

        var coverageWarnings = read.CoverageWarnings.ToList();
        if (surfaces.Length == 0)
        {
            coverageWarnings.Add("No package-config surfaces were present in the selected index/source scope.");
        }

        var reducedCoverage = coverageWarnings.Count > 0;
        var findings = new List<PackageImpactFinding>();
        var gaps = new List<PackageImpactGap>();
        var findingCapReached = false;
        var gapCapReached = false;

        foreach (var change in selectedChanges)
        {
            var matches = surfaces
                .Where(surface => PackageMatches(surface, change))
                .OrderBy(surface => surface.SourceLabel, StringComparer.Ordinal)
                .ThenBy(surface => surface.PackageName ?? surface.DisplayName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(surface => surface.FilePath, StringComparer.Ordinal)
                .ThenBy(surface => surface.StartLine)
                .ToArray();

            if (matches.Length == 0)
            {
                AddGap(gaps, options.MaxGaps, ref gapCapReached, NoMatchGap(change, read, selectedSources, reducedCoverage));
                continue;
            }

            foreach (var surface in matches)
            {
                if (findings.Count >= options.MaxFindings)
                {
                    findingCapReached = true;
                    break;
                }

                findings.Add(ToFinding(change, surface));
            }
        }

        foreach (var warning in coverageWarnings.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            AddGap(gaps, options.MaxGaps, ref gapCapReached, CoverageGap(warning, selectedSources));
        }

        var reportCoverage = reducedCoverage || gapCapReached || findingCapReached ? "ReducedCoverage" : "FullEvidenceAvailable";
        var report = new PackageImpactDocument(
            Version,
            reportCoverage,
            delta,
            new PackageImpactSummary(
                read.IndexKind,
                selectedSources.Count,
                delta.Changes.Count,
                selectedChanges.Length,
                surfaces.Length,
                findings.Count,
                gaps.Count,
                findingCapReached,
                gapCapReached),
            selectedSources
                .OrderBy(source => source.Label, StringComparer.Ordinal)
                .ThenBy(source => source.SourceIndexId, StringComparer.Ordinal)
                .ToArray(),
            findings
                .OrderBy(finding => finding.PackageName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(finding => finding.SourceLabel, StringComparer.Ordinal)
                .ThenBy(finding => finding.FilePath, StringComparer.Ordinal)
                .ThenBy(finding => finding.StartLine)
                .ToArray(),
            gaps
                .OrderBy(gap => gap.Classification, StringComparer.Ordinal)
                .ThenBy(gap => gap.ChangeId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(gap => gap.Message, StringComparer.Ordinal)
                .ToArray(),
            Limitations);

        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(
            options.OutputPath,
            format,
            "package-impact-report.md",
            "package-impact-report.json",
            report,
            RenderMarkdown,
            JsonOptions,
            cancellationToken);
        return new PackageImpactResult(report, markdownPath, jsonPath);
    }

    private static async Task<PackageImpactDelta> ReadDeltaAsync(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Package delta file was not found.", path);
        }

        var json = await File.ReadAllTextAsync(path, cancellationToken);
        var input = JsonSerializer.Deserialize<PackageDeltaInput>(json, JsonOptions)
            ?? throw new InvalidDataException("Package delta JSON could not be parsed.");
        if (!string.Equals(input.Version, DeltaVersion, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Package delta version must be {DeltaVersion}.");
        }

        if (input.Changes is null || input.Changes.Count == 0)
        {
            throw new InvalidDataException("Package delta requires a non-empty changes array.");
        }

        var ids = new HashSet<string>(StringComparer.Ordinal);
        var changes = new List<PackageImpactChange>();
        foreach (var change in input.Changes)
        {
            var packageName = change.PackageName ?? change.Name;
            if (string.IsNullOrWhiteSpace(change.Id))
            {
                throw new InvalidDataException("Package delta change id is required.");
            }

            if (!ids.Add(change.Id))
            {
                throw new InvalidDataException($"Package delta contains duplicate change id '{change.Id}'.");
            }

            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new InvalidDataException($"Package delta change '{change.Id}' requires packageName.");
            }

            var changeType = string.IsNullOrWhiteSpace(change.ChangeType) ? "updated" : change.ChangeType.Trim();
            if (!new[] { "added", "removed", "updated", "changed" }.Contains(changeType, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Package delta change '{change.Id}' has unsupported changeType '{changeType}'.");
            }

            var oldVersion = SafeVersion(change.OldVersion, out var oldVersionHash);
            var newVersion = SafeVersion(change.NewVersion, out var newVersionHash);
            changes.Add(new PackageImpactChange(
                change.Id.Trim(),
                packageName.Trim(),
                NullIfWhiteSpace(change.Ecosystem),
                changeType.ToLowerInvariant(),
                oldVersion,
                oldVersionHash,
                newVersion,
                newVersionHash));
        }

        return new PackageImpactDelta(DeltaVersion, changes);
    }

    private static async Task<PackageIndexReadResult> ReadIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var hasSources = await TableExistsAsync(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExistsAsync(connection, "combined_facts", cancellationToken);
        if (hasSources && hasCombinedFacts)
        {
            var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
            return new PackageIndexReadResult("combined", read.Sources, read.Facts, read.CoverageWarnings);
        }

        var hasManifest = await TableExistsAsync(connection, "scan_manifest", cancellationToken);
        var hasFacts = await TableExistsAsync(connection, "facts", cancellationToken);
        if (hasManifest && hasFacts)
        {
            return await ReadSingleIndexAsync(connection, cancellationToken);
        }

        throw new InvalidDataException("package-impact requires a TraceMap single index or combined index.");
    }

    private static async Task<PackageIndexReadResult> ReadSingleIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var manifest = await ReadSingleManifestAsync(connection, cancellationToken);
        var source = new CombinedReportSource(
            "default",
            "default",
            CombinedReportHelpers.Hash(manifest.RemoteUrl ?? manifest.RepoName, 16),
            manifest.ScanId,
            manifest.RepoName,
            manifest.RemoteUrl,
            manifest.Branch,
            manifest.CommitSha,
            manifest.ScannerVersion,
            LanguageFromScanner(manifest.ScannerVersion),
            null,
            false,
            manifest.ScanRootRelativePath,
            manifest.ScanRootPathHash,
            manifest.GitRootHash,
            manifest.AnalysisLevel,
            manifest.BuildStatus);
        var facts = await ReadSingleFactsAsync(connection, source, cancellationToken);
        var warnings = CoverageWarningsFor(source, manifest.KnownGaps);
        return new PackageIndexReadResult("single", [source], facts, warnings);
    }

    private static async Task<ScanManifest> ReadSingleManifestAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select manifest_json from scan_manifest order by scanned_at desc limit 1;";
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is string json
            ? JsonSerializer.Deserialize<ScanManifest>(json, JsonOptions) ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.")
            : throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadSingleFactsAsync(SqliteConnection connection, CombinedReportSource source, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            select fact_id,
                   scan_id,
                   repo,
                   commit_sha,
                   fact_type,
                   rule_id,
                   evidence_tier,
                   source_symbol,
                   target_symbol,
                   contract_element,
                   file_path,
                   start_line,
                   end_line,
                   properties_json
            from facts
            order by file_path, start_line, fact_type, fact_id;
            """;
        var facts = new List<CombinedFactRow>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var factId = reader.GetString(0);
            facts.Add(new CombinedFactRow(
                factId,
                source.SourceIndexId,
                source.Label,
                factId,
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetString(7),
                reader.IsDBNull(8) ? null : reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                reader.GetString(10),
                reader.GetInt32(11),
                reader.GetInt32(12),
                ParseProperties(reader.GetString(13))));
        }

        return facts;
    }

    private static PackageImpactFinding ToFinding(PackageImpactChange change, CombinedDependencySurfaceRow surface)
    {
        return new PackageImpactFinding(
            $"package-impact:{CombinedReportHelpers.Hash($"{change.Id}:{surface.CombinedFactId}", 24)}",
            "StaticPackageEvidence",
            change.Id,
            surface.PackageName ?? surface.DisplayName,
            change.Ecosystem ?? surface.Ecosystem,
            change.ChangeType,
            RuleId,
            surface.RuleId,
            surface.EvidenceTier,
            surface.SourceLabel,
            surface.SourceIndexId,
            surface.ScanId,
            surface.CommitSha,
            surface.CombinedFactId,
            surface.OriginalFactId,
            surface.FactType,
            CombinedReportHelpers.SafePath(surface.FilePath),
            surface.StartLine,
            surface.EndLine,
            change.OldVersion,
            change.OldVersionHash,
            change.NewVersion,
            change.NewVersionHash,
            surface.Version,
            surface.Version is null && surface.VersionHash is null ? null : surface.VersionHash,
            surface.VersionHash,
            surface.RedactionReason,
            CombinedReportHelpers.SortedMetadata([
                Pair("packageManager", surface.PackageManager),
                Pair("manifestKind", surface.ManifestKind),
                Pair("dependencyScope", surface.DependencyScope),
                Pair("dependencyGroup", surface.DependencyGroup),
                Pair("configKey", surface.ConfigKey),
                Pair("displayName", surface.DisplayName)
            ]));
    }

    private static PackageImpactGap NoMatchGap(
        PackageImpactChange change,
        PackageIndexReadResult read,
        IReadOnlyList<CombinedReportSource> selectedSources,
        bool reducedCoverage)
    {
        var source = selectedSources.OrderBy(item => item.Label, StringComparer.Ordinal).FirstOrDefault()
            ?? read.Sources.OrderBy(item => item.Label, StringComparer.Ordinal).FirstOrDefault();
        var classification = reducedCoverage ? "UnknownAnalysisGap" : "NoStaticPackageEvidence";
        var message = reducedCoverage
            ? $"No matching package evidence for {change.PackageName}; selected coverage is reduced."
            : $"No matching static package declaration evidence for {change.PackageName}.";
        return new PackageImpactGap(
            $"package-gap:{CombinedReportHelpers.Hash($"{classification}:{change.Id}:{source?.ScanId}", 24)}",
            classification,
            message,
            RuleId,
            reducedCoverage ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural,
            change.Id,
            change.PackageName,
            change.Ecosystem,
            source?.Label,
            source?.ScanId,
            source?.CommitSha,
            CombinedReportHelpers.SortedMetadata([
                Pair("indexKind", read.IndexKind),
                Pair("sourceCount", selectedSources.Count.ToString(CultureInfo.InvariantCulture)),
                Pair("changeType", change.ChangeType)
            ]));
    }

    private static PackageImpactGap CoverageGap(string warning, IReadOnlyList<CombinedReportSource> sources)
    {
        var source = sources.OrderBy(item => item.Label, StringComparer.Ordinal).FirstOrDefault();
        return new PackageImpactGap(
            $"package-coverage:{CombinedReportHelpers.Hash($"{warning}:{source?.ScanId}", 24)}",
            "ReducedCoverage",
            warning,
            RuleId,
            EvidenceTiers.Tier4Unknown,
            SourceLabel: source?.Label,
            ScanId: source?.ScanId,
            CommitSha: source?.CommitSha);
    }

    private static void AddGap(List<PackageImpactGap> gaps, int maxGaps, ref bool capReached, PackageImpactGap gap)
    {
        if (gaps.Count >= maxGaps)
        {
            capReached = true;
            return;
        }

        gaps.Add(gap);
    }

    private static IReadOnlyList<CombinedReportSource> ApplySourceFilter(IReadOnlyList<CombinedReportSource> sources, string? sourceFilter)
    {
        if (string.IsNullOrWhiteSpace(sourceFilter))
        {
            return sources;
        }

        var filtered = sources
            .Where(source => string.Equals(source.Label, sourceFilter, StringComparison.Ordinal))
            .ToArray();
        if (filtered.Length == 0)
        {
            throw new InvalidDataException("package-impact index does not contain the requested --source label.");
        }

        return filtered;
    }

    private static bool PackageMatches(CombinedDependencySurfaceRow surface, PackageImpactChange change)
    {
        var packageName = surface.PackageName ?? surface.DisplayName;
        if (!string.Equals(packageName, change.PackageName, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(change.Ecosystem)
            || (!string.IsNullOrWhiteSpace(surface.Ecosystem)
                && string.Equals(surface.Ecosystem, change.Ecosystem, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesSelector(string? value, string? selector)
    {
        return string.IsNullOrWhiteSpace(selector)
            || string.Equals(value, selector, StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> CoverageWarningsFor(CombinedReportSource source, IReadOnlyList<string> knownGaps)
    {
        var warnings = new SortedSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(source.CommitSha) || source.CommitSha == "unknown")
        {
            warnings.Add($"{source.Label} has unknown commit SHA; scan provenance is reduced.");
        }

        if (!string.Equals(source.BuildStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{source.Label} build status is {source.BuildStatus}; package absence cannot be treated as clean.");
        }

        if (!string.Equals(source.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"{source.Label} analysis level is {source.AnalysisLevel}; package evidence may be reduced.");
        }

        foreach (var gap in knownGaps)
        {
            if (!string.IsNullOrWhiteSpace(gap))
            {
                warnings.Add($"{source.Label} known gap: {gap}");
            }
        }

        return warnings.ToArray();
    }

    private static string RenderMarkdown(PackageImpactDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Package Impact Report");
        builder.AppendLine();
        builder.AppendLine($"- Version: `{report.Version}`");
        builder.AppendLine($"- Coverage: `{report.ReportCoverage}`");
        builder.AppendLine($"- Index kind: `{report.Summary.IndexKind}`");
        builder.AppendLine($"- Sources: `{report.Summary.SourceCount}`");
        builder.AppendLine($"- Delta changes: `{report.Summary.SelectedChangeCount}` selected of `{report.Summary.DeltaChangeCount}`");
        builder.AppendLine($"- Static package evidence rows: `{report.Summary.PackageEvidenceCount}`");
        builder.AppendLine($"- Findings: `{report.Summary.FindingCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine();

        builder.AppendLine("## Package Findings");
        builder.AppendLine();
        if (report.Findings.Count == 0)
        {
            builder.AppendLine("No static package evidence findings matched the selected package delta.");
        }
        else
        {
            builder.AppendLine("| Package | Change | Source | Evidence | Version | Location | Rule |");
            builder.AppendLine("| --- | --- | --- | --- | --- | --- | --- |");
            foreach (var finding in report.Findings.Take(MarkdownRowLimit))
            {
                var requested = VersionRange(finding);
                var observed = finding.ObservedVersion ?? finding.VersionHash ?? finding.ObservedVersionHash ?? "n/a";
                builder.AppendLine($"| {Cell(finding.PackageName)} | {Cell($"{finding.ChangeType} {requested}")} | {Cell(finding.SourceLabel)} | {Cell($"{finding.Classification} {finding.EvidenceTier}")} | {Cell(observed)} | {Cell($"{finding.FilePath}:{finding.StartLine}-{finding.EndLine}")} | {Cell($"{finding.RuleId}; evidence {finding.EvidenceRuleId}")} |");
            }

            if (report.Findings.Count > MarkdownRowLimit)
            {
                builder.AppendLine();
                builder.AppendLine($"Additional findings omitted from Markdown: `{report.Findings.Count - MarkdownRowLimit}`. See JSON output.");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        if (report.Gaps.Count == 0)
        {
            builder.AppendLine("No package-impact gaps were recorded.");
        }
        else
        {
            builder.AppendLine("| Classification | Change | Message | Evidence |");
            builder.AppendLine("| --- | --- | --- | --- |");
            foreach (var gap in report.Gaps.Take(MarkdownRowLimit))
            {
                builder.AppendLine($"| {Cell(gap.Classification)} | {Cell(gap.ChangeId ?? "n/a")} | {Cell(gap.Message)} | {Cell($"{gap.RuleId} {gap.EvidenceTier}")} |");
            }
        }

        builder.AppendLine();
        builder.AppendLine("## Sources");
        builder.AppendLine();
        builder.AppendLine("| Label | Scan ID | Commit | Analysis | Build |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var source in report.Sources)
        {
            builder.AppendLine($"| {Cell(source.Label)} | {Cell(source.ScanId)} | {Cell(source.CommitSha)} | {Cell(source.AnalysisLevel)} | {Cell(source.BuildStatus)} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations)
        {
            builder.AppendLine($"- {limitation}");
        }

        return builder.ToString();
    }

    private static string VersionRange(PackageImpactFinding finding)
    {
        var oldValue = finding.RequestedOldVersion ?? finding.RequestedOldVersionHash;
        var newValue = finding.RequestedNewVersion ?? finding.RequestedNewVersionHash;
        if (oldValue is null && newValue is null)
        {
            return string.Empty;
        }

        return $"{oldValue ?? "n/a"} -> {newValue ?? "n/a"}";
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string name, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", name);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static IReadOnlyDictionary<string, string> ParseProperties(string json)
    {
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private static string? SafeVersion(string? value, out string? hash)
    {
        hash = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length <= 80
            && trimmed.All(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_' or '+' or '~')
            && !trimmed.Contains("://", StringComparison.Ordinal)
            && !trimmed.Contains('@', StringComparison.Ordinal))
        {
            return trimmed;
        }

        hash = $"version-hash:{CombinedReportHelpers.Hash(trimmed, 16)}";
        return null;
    }

    private static string? LanguageFromScanner(string scannerVersion)
    {
        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        return "csharp";
    }

    private static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value)
    {
        return new KeyValuePair<string, string?>(key, value);
    }

    private static string Cell(string? value)
    {
        return CombinedReportHelpers.Cell(value);
    }

    private sealed record PackageIndexReadResult(
        string IndexKind,
        IReadOnlyList<CombinedReportSource> Sources,
        IReadOnlyList<CombinedFactRow> Facts,
        IReadOnlyList<string> CoverageWarnings);

    private sealed record PackageDeltaInput(
        string? Version,
        IReadOnlyList<PackageChangeInput>? Changes);

    private sealed record PackageChangeInput(
        string Id,
        string? PackageName,
        string? Name,
        string? Ecosystem,
        string? ChangeType,
        string? OldVersion,
        string? NewVersion);
}
