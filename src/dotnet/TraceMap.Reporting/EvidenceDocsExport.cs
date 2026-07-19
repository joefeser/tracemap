using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record EvidenceDocsExportOptions(
    string IndexPath,
    string OutputPath,
    IReadOnlyList<string>? RouteFlowReportPaths = null,
    IReadOnlyList<string>? PathsReportPaths = null,
    IReadOnlyList<string>? ReverseReportPaths = null,
    IReadOnlyList<string>? CombinedReportPaths = null,
    IReadOnlyList<string>? ReleaseReviewReportPaths = null,
    IReadOnlyList<string>? VaultGraphPaths = null,
    IReadOnlyList<string>? EvidencePackPaths = null,
    string? SourceClaimCatalogPath = null,
    string? MinimumClaimLevel = null,
    string? Families = null,
    string? Format = null,
    string? Date = null,
    bool DryRun = false,
    bool Force = false,
    IReadOnlyList<string>? PropertyFlowReportPaths = null);

public sealed record EvidenceDocsExportResult(
    EvidenceDocsManifest Manifest,
    IReadOnlyList<EvidenceDocChunk> Chunks,
    IReadOnlyList<string> PlannedFiles,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<EvidenceDocsDiagnostic> Diagnostics);

public sealed record EvidenceDocsDiagnostic(
    string Code,
    string RuleId,
    string EvidenceTier,
    string Location,
    string Category,
    string FilePath,
    int? StartLine,
    int? EndLine,
    string? CommitSha,
    string ExtractorVersion,
    IReadOnlyList<string> SupportingIds);

public sealed record EvidenceDocsManifest(
    string SchemaVersion,
    bool TracemapGenerated,
    string ContentHash,
    EvidenceDocsGenerator Generator,
    string ClaimLevel,
    IReadOnlyList<string> Formats,
    EvidenceDocsGenerationSettings GenerationSettings,
    IReadOnlyList<EvidenceDocsInputSummary> Inputs,
    IReadOnlyList<EvidenceDocsOutputSummary> Outputs,
    IReadOnlyList<EvidenceDocsCount> ChunkCounts,
    IReadOnlyList<EvidenceDocsCount> OmittedCounts,
    IReadOnlyList<EvidenceDocGap> Gaps,
    IReadOnlyList<EvidenceDocLimitation> Limitations,
    IReadOnlyList<string> RepositoryIdentifiers,
    IReadOnlyList<string> CommitShas);

public sealed record EvidenceDocsGenerator(
    string Name,
    string Version,
    string GeneratedAt);

public sealed record EvidenceDocsGenerationSettings(
    IReadOnlyList<string> Families,
    string MinimumClaimLevel,
    bool IncludeRawSnippets);

public sealed record EvidenceDocsInputSummary(
    string Kind,
    string Identity,
    string ClaimLevel,
    string Compatibility,
    IReadOnlyList<string> SourceLabels,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<EvidenceDocSourceRef> SourceRefs,
    string? SchemaVersion = null);

public sealed record EvidenceDocsOutputSummary(
    string Path,
    string Kind,
    string SchemaVersion,
    string Generator,
    long SizeBytes,
    int LineCount,
    string Sha256);

public sealed record EvidenceDocsCount(
    string Key,
    int Count);

public sealed record EvidenceDocChunk(
    string SchemaVersion,
    string ChunkId,
    string ChunkType,
    string ChunkFamily,
    IReadOnlyList<string> QuestionFamilies,
    string ClaimLevel,
    string Title,
    string SectionTitle,
    string SortKey,
    string Summary,
    EvidenceDocClaim Claim,
    string BodyMarkdown,
    IReadOnlyList<EvidenceDocCitation> Citations,
    IReadOnlyList<EvidenceDocSourceRef> SourceRefs,
    IReadOnlyList<string> SupportingIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<EvidenceDocGap> Gaps,
    IReadOnlyList<EvidenceDocLimitation> Limitations,
    IReadOnlyList<EvidenceDocRedaction> Redactions,
    IReadOnlyList<EvidenceDocLink> Links);

public sealed record EvidenceDocClaim(
    string Kind,
    string Text,
    string? Classification,
    string ClaimLevel,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> SupportingIds,
    IReadOnlyList<string> Limitations);

public sealed record EvidenceDocCitation(
    string CitationId,
    string? SourceLabel,
    string SourceScope,
    string? ScanId,
    string? CommitSha,
    string CoverageLabel,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> RuleIds,
    string EvidenceTier,
    string? ExtractorName,
    string? ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingReportIds);

public sealed record EvidenceDocSourceRef(
    string SourceId,
    string SourceLabel,
    string SourceScope,
    string? ScanId,
    string? CommitSha,
    string CoverageLabel,
    string? ExtractorName,
    string? ExtractorVersion,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers);

public sealed record EvidenceDocGap(
    string GapId,
    string RuleId,
    string EvidenceTier,
    string Reason,
    string ChunkFamily,
    IReadOnlyList<EvidenceDocSourceRef> SourceRefs,
    IReadOnlyList<string> SupportingIds,
    IReadOnlyList<string> Limitations);

public sealed record EvidenceDocLimitation(
    string LimitationId,
    string RuleId,
    string EvidenceTier,
    string Message,
    string ChunkFamily,
    IReadOnlyList<string> SupportingIds);

public sealed record EvidenceDocRedaction(
    string RedactionId,
    string RuleId,
    string Category,
    string Location);

public sealed record EvidenceDocLink(
    string LinkId,
    string Label,
    string Target);

public static class EvidenceDocsExporter
{
    public const string SchemaVersion = "tracemap-evidence-docs.v1";
    public const string GeneratorName = "tracemap-docs-export";
    private const string GeneratorVersion = SchemaVersion;
    private const int IdHashLength = 24;
    private const string Tier4Unknown = EvidenceTiers.Tier4Unknown;

    private const string SourceOverviewRuleId = "docs-export.chunk.source-overview.v1";
    private const string EndpointRuleId = "docs-export.chunk.endpoint.v1";
    private const string RouteFlowRuleId = "docs-export.chunk.route-flow.v1";
    private const string PropertyFlowRuleId = "docs-export.chunk.property-flow.v1";
    private const string DependencySurfaceRuleId = "docs-export.chunk.dependency-surface.v1";
    private const string DataSurfaceRuleId = "docs-export.chunk.data-surface.v1";
    private const string PackageConfigRuleId = "docs-export.chunk.package-config.v1";
    private const string QuerySqlShapeRuleId = "docs-export.chunk.query-sql-shape.v1";
    private const string LegacyRuleId = "docs-export.chunk.legacy.v1";
    private const string ReleaseReviewRuleId = "docs-export.chunk.release-review.v1";
    private const string ImpactSummaryRuleId = "docs-export.chunk.impact-summary.v1";
    private const string GapChunkRuleId = "docs-export.chunk.gap.v1";
    private const string LimitationChunkRuleId = "docs-export.chunk.limitation.v1";
    private const string TerminalContextKindMetadataKey = "terminalContextKind";
    private const long MaxPropertyFlowReportBytes = 4L * 1024 * 1024;
    private const string GeneratedFileStaleRuleId = "docs-export.validation.generated-file-stale.v1";
    private const string UserFileCollisionRuleId = "docs-export.validation.user-file-collision.v1";
    private const string UnsafeRejectedRuleId = "docs-export.validation.unsafe-value-rejected.v1";
    private const string ProhibitedClaimRuleId = "docs-export.validation.prohibited-claim-wording.v1";
    private const string SchemaGapRuleId = "docs-export.gap.schema-incompatible.v1";
    private const string ClaimHiddenRuleId = "docs-export.gap.claim-level-hidden.v1";
    private const string ClaimUnmatchedRuleId = "docs-export.gap.claim-level-unmatched.v1";
    private const string HiddenOmittedRuleId = "docs-export.gap.hidden-evidence-omitted.v1";
    private const string DuplicateIdentityRuleId = "docs-export.gap.duplicate-stable-identity.v1";
    private const string UnsupportedFamilyRuleId = "docs-export.gap.unsupported-family.v1";
    private const string MissingProvenanceRuleId = "docs-export.gap.missing-provenance.v1";
    private const string UnknownAnalysisRuleId = "docs-export.gap.unknown-analysis.v1";

    private static readonly string[] AllFamilies =
    [
        "source-overview",
        "endpoint",
        "route-flow",
        "property-flow",
        "dependency-surface",
        "data-surface",
        "package-config",
        "query-sql-shape",
        "legacy",
        "release-review",
        "impact-summary",
        "gap",
        "limitation"
    ];

    private static readonly string[] QuestionFamilyOrder =
    [
        "endpoint-question",
        "data-surface-question",
        "package-question",
        "snapshot-change-question",
        "weak-evidence-question",
        "gap-question",
        "limitation-question"
    ];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly JsonSerializerOptions JsonLineOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
    private static readonly Regex YearMonthPattern = new(@"^\d{4}-\d{2}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex SafeClosedTextPattern = new(@"^[A-Za-z0-9._:/@,+ \[\]\-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex SafePathPattern = new(@"^[A-Za-z0-9._/\-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex UnixLocalPathPattern = new("(?:^|[\\s:='\\\"])/(Users|home|opt|var|srv|app|mnt|private|tmp)/", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex Hex40Pattern = new(@"^[0-9a-fA-F]{40}$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex WindowsPathPattern = new("(?:^|[\\s:='\\\"])(?:[A-Za-z]:[\\\\/]|\\\\\\\\)", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex RawHostPattern = new(@"\b(www\.|[A-Za-z0-9.-]+\.(com|net|org|io|local))\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex RawSqlPattern = new(@"\b(select|insert|update|delete|merge)\b.+\b(from|into|set|where|values)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex ConfigSecretPattern = new(@"(password|passwd|pwd|secret|token|apikey|api_key|connectionstring|connection string)\s*[=:]", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex CredentialPattern = new(@"(sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|AKIA[0-9A-Z]{16})", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex SafeMetadataKeyPattern = new(@"^[A-Za-z0-9_.-]+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex ContentHashLinePattern = new(@"tracemap_content_sha256: [0-9a-f]{64}", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
    private static readonly Regex RepeatedDashPattern = new("-+", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);

    public static async Task<EvidenceDocsExportResult> ExportAsync(EvidenceDocsExportOptions options, CancellationToken cancellationToken = default)
    {
        ValidateRequiredOptions(options);
        var formats = NormalizeFormats(options.Format);
        var selectedFamilies = NormalizeFamilies(options.Families);
        var minimumClaimLevel = NormalizeClaimLevel(options.MinimumClaimLevel ?? "hidden", "--minimum-claim-level");
        var generatedAt = ResolveGeneratedAt(options.Date, minimumClaimLevel);
        var catalog = await ReadClaimCatalogAsync(options.SourceClaimCatalogPath, cancellationToken);
        var diagnostics = new List<EvidenceDocsDiagnostic>();

        var input = await ReadIndexAsync(options.IndexPath, cancellationToken);
        ApplyCatalogClaims(input.Sources, catalog, diagnostics);

        var chunks = ProjectIndexChunks(input, selectedFamilies, diagnostics);
        chunks.AddRange(await ProjectReportChunksAsync(options, input.Sources, selectedFamilies, diagnostics, cancellationToken));
        AddRequestedUnsupportedFamilyGaps(input.Sources, selectedFamilies, chunks);
        AddCatalogUnmatchedGaps(input.Sources, catalog, selectedFamilies, chunks, diagnostics);

        var beforeFilterCount = chunks.Count(chunk => chunk.ChunkType == "claim");
        chunks = ApplyClaimFilter(chunks, minimumClaimLevel, selectedFamilies, input.Sources);
        var afterFilterCount = chunks.Count(chunk => chunk.ChunkType == "claim");
        if (minimumClaimLevel != "hidden" && beforeFilterCount > afterFilterCount && selectedFamilies.Contains("gap", StringComparer.Ordinal))
        {
            chunks.Add(CreateGapChunk(
                "hidden-evidence-omitted",
                HiddenOmittedRuleId,
                "hidden-evidence-omitted",
                "gap",
                input.Sources,
                ["claim-level-filter"],
                minimumClaimLevel));
        }

        if (minimumClaimLevel != "hidden" && chunks.All(chunk => chunk.ChunkType != "claim"))
        {
            throw new InvalidOperationException("NoVisibleEvidenceAfterFiltering: requested claim-level filter left no visible claim-bearing docs-export chunks.");
        }

        chunks = DetectDuplicateChunkIds(chunks, input.Sources);
        if (chunks.Count == 0)
        {
            throw new InvalidOperationException("InputSchemaUnsupported: docs-export requires a TraceMap index with usable source or fact evidence.");
        }

        chunks = AddNavigationLinks(SortChunks(chunks), formats).ToList();
        var limitations = BuildManifestLimitations(chunks, selectedFamilies);
        var manifest = BuildManifest(
            options,
            input,
            chunks,
            formats,
            selectedFamilies,
            minimumClaimLevel,
            generatedAt,
            [],
            limitations);

        var files = BuildGeneratedFiles(options.OutputPath, manifest, chunks, formats);
        ValidateGeneratedStrings(files);
        await ValidateExistingFilesAsync(options.OutputPath, files, options.Force, cancellationToken);
        var outputs = BuildOutputSummaries(options.OutputPath, files);
        manifest = BuildManifest(
            options,
            input,
            chunks,
            formats,
            selectedFamilies,
            minimumClaimLevel,
            generatedAt,
            outputs,
            limitations);
        manifest = WithManifestHash(manifest);
        files["manifest.json"] = SerializeJson(manifest);
        ValidateGeneratedStrings(files);
        await ValidateExistingFilesAsync(options.OutputPath, files, options.Force, cancellationToken);

        if (!options.DryRun)
        {
            foreach (var file in files.OrderBy(file => file.Key, StringComparer.Ordinal))
            {
                var fullPath = Path.Combine(Path.GetFullPath(options.OutputPath), file.Key);
                var directory = Path.GetDirectoryName(fullPath);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(fullPath, file.Value, new UTF8Encoding(false), cancellationToken);
            }
        }

        var plannedFiles = files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray();
        return new EvidenceDocsExportResult(
            manifest,
            chunks,
            plannedFiles,
            options.DryRun ? [] : plannedFiles,
            diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal).ThenBy(diagnostic => diagnostic.Location, StringComparer.Ordinal).ToArray());
    }

    public static string StableIdInputRecord(IReadOnlyList<KeyValuePair<string, string?>> fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            var name = field.Key;
            var value = field.Value ?? string.Empty;
            builder.Append(name.Length)
                .Append(':')
                .Append(name)
                .Append('=')
                .Append(value.Length)
                .Append(':')
                .Append(value)
                .Append('\n');
        }

        return builder.ToString();
    }

    public static bool IsSelfConsistentManifest(string content)
    {
        try
        {
            var node = JsonNode.Parse(content) as JsonObject;
            if (node is null
                || node["schemaVersion"]?.GetValue<string>() != SchemaVersion
                || node["tracemapGenerated"]?.GetValue<bool>() != true
                || node["generator"]?["name"]?.GetValue<string>() != GeneratorName
                || node["contentHash"]?.GetValue<string>() is not { } expected
                || !IsHash(expected, 64))
            {
                return false;
            }

            node["contentHash"] = string.Empty;
            return string.Equals(expected, Hash(SerializeJsonNode(node), 64), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSelfConsistentMarkdown(string content)
    {
        if (!TryReadFrontmatter(content, out var metadata, out _)
            || metadata.GetValueOrDefault("tracemap_export_schema") != SchemaVersion
            || metadata.GetValueOrDefault("tracemap_generator") != GeneratorName
            || metadata.GetValueOrDefault("tracemap_content_sha256") is not { } expected
            || !IsHash(expected, 64))
        {
            return false;
        }

        return string.Equals(expected, Hash(NormalizeMarkdownHashInput(content), 64), StringComparison.Ordinal);
    }

    private static void ValidateRequiredOptions(EvidenceDocsExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath))
        {
            throw new ArgumentException("docs-export requires --index <index-or-combined.sqlite>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("docs-export requires --out <path>.");
        }
    }

    private static IReadOnlyList<string> NormalizeFormats(string? format)
    {
        var value = string.IsNullOrWhiteSpace(format) ? "markdown,jsonl" : format;
        var rawTokens = value.Split(',', StringSplitOptions.None);
        if (rawTokens.Any(token => string.IsNullOrWhiteSpace(token)))
        {
            throw new ArgumentException("docs-export --format must contain markdown, jsonl, or markdown,jsonl.");
        }

        var tokens = rawTokens.Select(token => token.Trim()).ToArray();
        if (tokens.Length != tokens.Distinct(StringComparer.Ordinal).Count())
        {
            throw new ArgumentException("docs-export --format values must not repeat tokens.");
        }

        foreach (var token in tokens)
        {
            if (token is not "markdown" and not "jsonl")
            {
                throw new ArgumentException("docs-export --format supports only markdown and jsonl.");
            }
        }

        return tokens.OrderBy(token => token == "markdown" ? 0 : 1).ToArray();
    }

    private static IReadOnlyList<string> NormalizeFamilies(string? families)
    {
        if (families is null)
        {
            return AllFamilies;
        }

        var rawTokens = families.Split(',', StringSplitOptions.None);
        if (rawTokens.Length == 0 || rawTokens.Any(token => string.IsNullOrWhiteSpace(token)))
        {
            throw new ArgumentException("docs-export --families must contain one or more closed family tokens.");
        }

        var tokens = rawTokens.Select(token => token.Trim()).ToArray();
        foreach (var token in tokens)
        {
            if (!AllFamilies.Contains(token, StringComparer.Ordinal))
            {
                throw new ArgumentException("docs-export --families contains an unsupported family token.");
            }
        }

        return tokens.Distinct(StringComparer.Ordinal).OrderBy(token => Array.IndexOf(AllFamilies, token)).ToArray();
    }

    private static string NormalizeClaimLevel(string claimLevel, string optionName)
    {
        if (claimLevel is "hidden" or "demo-safe" or "public-safe")
        {
            return claimLevel;
        }

        throw new ArgumentException($"{optionName} must be hidden, demo-safe, or public-safe.");
    }

    private static string ResolveGeneratedAt(string? date, string minimumClaimLevel)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            if (minimumClaimLevel == "hidden")
            {
                return "local-only";
            }

            throw new ArgumentException("docs-export --date YYYY-MM is required for demo-safe and public-safe outputs.");
        }

        if (!YearMonthPattern.IsMatch(date))
        {
            throw new ArgumentException("docs-export --date must use YYYY-MM.");
        }

        return date;
    }

    private static async Task<IndexInput> ReadIndexAsync(string indexPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(indexPath))
        {
            throw new InvalidOperationException("InputUnreadable: docs-export --index must point to a readable TraceMap SQLite index.");
        }

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly
        }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (SqliteException)
        {
            throw new InvalidOperationException("InputSchemaUnsupported: docs-export --index must be a TraceMap SQLite index.");
        }

        if (await TableExistsAsync(connection, "index_sources", cancellationToken)
            && await TableExistsAsync(connection, "combined_facts", cancellationToken))
        {
            return await ReadCombinedIndexAsync(connection, cancellationToken);
        }

        if (await TableExistsAsync(connection, "scan_manifest", cancellationToken)
            && await TableExistsAsync(connection, "facts", cancellationToken))
        {
            return await ReadSingleIndexAsync(connection, cancellationToken);
        }

        throw new InvalidOperationException("InputSchemaUnsupported: docs-export --index must contain TraceMap scan or combined index tables.");
    }

    private static async Task<IndexInput> ReadCombinedIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var sources = new List<DocSource>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select source_index_id, label, scan_id, commit_sha, scanner_version, language, analysis_level, build_status
                from index_sources
                order by label, source_index_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var sourceIndexId = reader.GetString(0);
                var label = SafeSourceLabel(reader.GetString(1), "hidden");
                sources.Add(new DocSource(
                    sourceIndexId,
                    label,
                    "combined-source",
                    StringOrNull(reader, 2),
                    CommitOrNull(StringOrNull(reader, 3)),
                    StringOrNull(reader, 5) ?? "unknown",
                    StringOrNull(reader, 4),
                    StringOrNull(reader, 6) ?? "unknown",
                    StringOrNull(reader, 7) ?? "unknown",
                    "hidden"));
            }
        }

        var sourceById = sources.ToDictionary(source => source.SourceId, StringComparer.Ordinal);
        var facts = new List<DocFact>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select combined_fact_id, source_index_id, original_fact_id, scan_id, commit_sha, fact_type, rule_id,
                       evidence_tier, source_symbol, target_symbol, contract_element, file_path, start_line, end_line, properties_json
                from combined_facts
                order by source_index_id, file_path, start_line, fact_type, combined_fact_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!sourceById.TryGetValue(reader.GetString(1), out var source))
                {
                    continue;
                }

                facts.Add(new DocFact(
                    reader.GetString(0),
                    StringOrNull(reader, 2),
                    source,
                    StringOrNull(reader, 3),
                    CommitOrNull(StringOrNull(reader, 4)),
                    reader.GetString(5),
                    reader.GetString(6),
                    reader.GetString(7),
                    StringOrNull(reader, 8),
                    StringOrNull(reader, 9),
                    StringOrNull(reader, 10),
                    SafeRelativePathOrNull(StringOrNull(reader, 11)),
                    IntOrNull(reader, 12),
                    IntOrNull(reader, 13),
                    SafeProperties(StringOrNull(reader, 14))));
            }
        }

        if (sources.Count == 0 && facts.Count == 0)
        {
            throw new InvalidOperationException("InputSchemaUnsupported: docs-export found no usable source or fact evidence.");
        }

        return new IndexInput("combined-index", sources, facts);
    }

    private static async Task<IndexInput> ReadSingleIndexAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        DocSource? source = null;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select scan_id, commit_sha, scanner_version, analysis_level, build_status
                from scan_manifest
                order by scanned_at desc
                limit 1;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                var scanId = reader.GetString(0);
                source = new DocSource(
                    $"single:{Hash(scanId, 24)}",
                    "single",
                    "single-source",
                    scanId,
                    CommitOrNull(StringOrNull(reader, 1)),
                    InferLanguage(StringOrNull(reader, 2)),
                    StringOrNull(reader, 2),
                    StringOrNull(reader, 3) ?? "unknown",
                    StringOrNull(reader, 4) ?? "unknown",
                    "hidden");
            }
        }

        if (source is null)
        {
            throw new InvalidOperationException("InputSchemaUnsupported: docs-export found no usable scan manifest.");
        }

        var facts = new List<DocFact>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                select fact_id, scan_id, commit_sha, fact_type, rule_id, evidence_tier, source_symbol, target_symbol,
                       contract_element, file_path, start_line, end_line, properties_json
                from facts
                order by file_path, start_line, fact_type, fact_id;
                """;
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                facts.Add(new DocFact(
                    reader.GetString(0),
                    reader.GetString(0),
                    source,
                    StringOrNull(reader, 1),
                    CommitOrNull(StringOrNull(reader, 2)),
                    reader.GetString(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    StringOrNull(reader, 6),
                    StringOrNull(reader, 7),
                    StringOrNull(reader, 8),
                    SafeRelativePathOrNull(StringOrNull(reader, 9)),
                    IntOrNull(reader, 10),
                    IntOrNull(reader, 11),
                    SafeProperties(StringOrNull(reader, 12))));
            }
        }

        return new IndexInput("single-index", [source], facts);
    }

    private static List<EvidenceDocChunk> ProjectIndexChunks(IndexInput input, IReadOnlyList<string> selectedFamilies, List<EvidenceDocsDiagnostic> diagnostics)
    {
        var chunks = new List<EvidenceDocChunk>();
        if (selectedFamilies.Contains("source-overview", StringComparer.Ordinal))
        {
            foreach (var source in input.Sources)
            {
                chunks.Add(CreateSourceOverviewChunk(source, input));
            }
        }

        foreach (var group in input.Facts.GroupBy(FamilyForFact).OrderBy(group => Array.IndexOf(AllFamilies, group.Key)))
        {
            if (!selectedFamilies.Contains(group.Key, StringComparer.Ordinal))
            {
                continue;
            }

            foreach (var fact in group.OrderBy(fact => fact.Source.Label, StringComparer.Ordinal)
                         .ThenBy(fact => fact.RuleId, StringComparer.Ordinal)
                         .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
            {
                if (group.Key is "gap")
                {
                    chunks.Add(CreateFactGapChunk(fact));
                }
                else
                {
                    chunks.Add(CreateFactChunk(fact, group.Key));
                }
            }
        }

        if (input.Kind == "single-index")
        {
            foreach (var family in selectedFamilies.Where(family => family is "route-flow" or "release-review" or "impact-summary"))
            {
                chunks.Add(CreateGapChunk(
                    $"single-index-{family}",
                    SchemaGapRuleId,
                    "schema-incompatible",
                    family,
                    input.Sources,
                    [$"single-index:{family}"],
                    "hidden"));
            }
        }

        if (input.Facts.Count == 0 && selectedFamilies.Contains("gap", StringComparer.Ordinal))
        {
            chunks.Add(CreateGapChunk("no-facts", UnknownAnalysisRuleId, "missing-provenance", "gap", input.Sources, ["index:facts"], "hidden"));
        }

        if (selectedFamilies.Contains("limitation", StringComparer.Ordinal))
        {
            chunks.Add(CreateLimitationChunk(input.Sources));
        }

        return chunks;
    }

    private static async Task<IReadOnlyList<EvidenceDocChunk>> ProjectReportChunksAsync(
        EvidenceDocsExportOptions options,
        IReadOnlyList<DocSource> sources,
        IReadOnlyList<string> selectedFamilies,
        List<EvidenceDocsDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var chunks = new List<EvidenceDocChunk>();
        await AddReportChunksAsync(options.RouteFlowReportPaths ?? [], "route-flow-report", "route-flow", RouteFlowRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddReportChunksAsync(options.PathsReportPaths ?? [], "paths-report", "route-flow", RouteFlowRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddReportChunksAsync(options.ReverseReportPaths ?? [], "reverse-report", "dependency-surface", DependencySurfaceRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddReportChunksAsync(options.CombinedReportPaths ?? [], "combined-report", "dependency-surface", DependencySurfaceRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddReportChunksAsync(options.ReleaseReviewReportPaths ?? [], "release-review-report", "release-review", ReleaseReviewRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddReportChunksAsync(options.EvidencePackPaths ?? [], "evidence-pack", "legacy", LegacyRuleId, selectedFamilies, sources, chunks, diagnostics, cancellationToken);
        await AddPropertyFlowReportChunksAsync(options.PropertyFlowReportPaths ?? [], selectedFamilies, sources, chunks, diagnostics, cancellationToken);

        await AddVaultGraphChunksAsync(options.VaultGraphPaths ?? [], selectedFamilies, sources, chunks, diagnostics, cancellationToken);

        return chunks;
    }

    private static async Task AddVaultGraphChunksAsync(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> selectedFamilies,
        IReadOnlyList<DocSource> sources,
        List<EvidenceDocChunk> chunks,
        List<EvidenceDocsDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0)
        {
            return;
        }

        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            try
            {
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                using var document = JsonDocument.Parse(json);
                if (StringProperty(document.RootElement, "schemaVersion") != "evidence-graph-vault-export.v1")
                {
                    throw new JsonException("unsupported vault graph schema");
                }

                if (!selectedFamilies.Contains("dependency-surface", StringComparer.Ordinal))
                {
                    continue;
                }

                var graphId = $"vault-graph:{Hash(json, 24)}";
                var classification = NormalizeClaimLevel(StringProperty(document.RootElement, "classification") ?? "hidden", "vault graph classification");
                var nodeCount = document.RootElement.TryGetProperty("nodes", out var nodes) && nodes.ValueKind == JsonValueKind.Array ? nodes.GetArrayLength() : 0;
                var edgeCount = document.RootElement.TryGetProperty("edges", out var edges) && edges.ValueKind == JsonValueKind.Array ? edges.GetArrayLength() : 0;
                var gapCount = document.RootElement.TryGetProperty("gaps", out var gaps) && gaps.ValueKind == JsonValueKind.Array ? gaps.GetArrayLength() : 0;
                var body = $"""
                    ## Vault graph evidence

                    This chunk records a compatible `evidence-graph-vault-export.v1` graph as supplemental static link metadata.

                    | Field | Value |
                    | --- | --- |
                    | Graph ID | `{EscapeInline(graphId)}` |
                    | Claim level | `{EscapeInline(classification)}` |
                    | Nodes | `{nodeCount}` |
                    | Edges | `{edgeCount}` |
                    | Gaps | `{gapCount}` |

                    Vault graph chunks preserve generated graph metadata and do not reinterpret source evidence.
                    """;
                chunks.Add(CreateChunk(
                    "dependency-surface",
                    "claim",
                    classification,
                    "Vault graph evidence",
                    "Compatible vault graph metadata supplied to docs export.",
                    body,
                    [CreateReportCitation(graphId, "evidence-graph-vault-export.v1", sources)],
                    sources.Select(ToSourceRef).ToArray(),
                    [graphId],
                    [DependencySurfaceRuleId],
                    [EvidenceTiers.Tier2Structural],
                    sources.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).DefaultIfEmpty("vault-graph").ToArray(),
                    [],
                    [LimitationForFamily("dependency-surface", [graphId])]));
            }
            catch
            {
                if (selectedFamilies.Contains("gap", StringComparer.Ordinal))
                {
                    chunks.Add(CreateGapChunk($"vault-graph-{Hash(path, 16)}", SchemaGapRuleId, "schema-incompatible", "gap", sources, ["vault-graph"], "hidden"));
                }

                diagnostics.Add(CreateDiagnostic("InputSchemaIncompatible", SchemaGapRuleId, "/inputs/vault-graph", "schema-incompatible", "vault-graph"));
            }
        }
    }

    private static async Task AddPropertyFlowReportChunksAsync(
        IReadOnlyList<string> paths,
        IReadOnlyList<string> selectedFamilies,
        IReadOnlyList<DocSource> sources,
        List<EvidenceDocChunk> chunks,
        List<EvidenceDocsDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0 || !selectedFamilies.Contains("property-flow", StringComparer.Ordinal))
        {
            return;
        }

        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            try
            {
                if (new FileInfo(path).Length > MaxPropertyFlowReportBytes)
                {
                    chunks.Add(CreateGapChunk($"property-flow-report-too-large-{Hash(path, 16)}", SchemaGapRuleId, "input-too-large", "property-flow", sources, ["property-flow-report"], "hidden"));
                    diagnostics.Add(CreateDiagnostic("InputTooLarge", SchemaGapRuleId, "/inputs/property-flow-report", "input-too-large", "property-flow-report"));
                    continue;
                }

                var json = await File.ReadAllTextAsync(path, cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var reportType = StringProperty(root, "reportType")
                    ?? StringProperty(root, "schemaVersion")
                    ?? StringProperty(root, "version");
                if (string.IsNullOrWhiteSpace(reportType))
                {
                    chunks.Add(CreateGapChunk($"property-flow-report-{Hash(path, 16)}", MissingProvenanceRuleId, "missing-provenance", "property-flow", sources, ["property-flow-report"], "hidden"));
                    diagnostics.Add(CreateDiagnostic("InputMissingProvenance", MissingProvenanceRuleId, "/inputs/property-flow-report", "missing-provenance", "property-flow-report"));
                    continue;
                }

                var terminalContexts = ExtractPropertyFlowTerminalContexts(root);
                var reportId = $"report:{Hash($"property-flow-report|{reportType}|{Hash(json, 64)}", 24)}";
                var supportingIds = DistinctSorted([reportId, .. terminalContexts.Select(context => context.SupportingId)]);
                var body = BuildPropertyFlowReportBody(reportType, reportId, terminalContexts);
                chunks.Add(CreateChunk(
                    "property-flow",
                    "claim",
                    sources.Count == 0 ? "hidden" : MinClaim(sources.Select(source => source.ClaimLevel)),
                    $"{TitleForFamily("property-flow")} report evidence",
                    $"Report `{EscapeInline(reportType)}` was supplied as deterministic `property-flow-report` evidence. It is preserved as static report evidence only.",
                    body,
                    [CreateReportCitation(reportId, reportType, sources)],
                    sources.Select(ToSourceRef).ToArray(),
                    supportingIds,
                    [PropertyFlowRuleId],
                    [EvidenceTiers.Tier2Structural],
                    sources.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    [],
                    [LimitationForFamily("property-flow", supportingIds)]));
            }
            catch (JsonException)
            {
                chunks.Add(CreateGapChunk($"property-flow-report-json-{Hash("property-flow-report", 16)}", SchemaGapRuleId, "schema-incompatible", "property-flow", sources, ["property-flow-report"], "hidden"));
                diagnostics.Add(CreateDiagnostic("InputSchemaIncompatible", SchemaGapRuleId, "/inputs/property-flow-report", "schema-incompatible", "property-flow-report"));
            }
        }
    }

    private static async Task AddReportChunksAsync(
        IReadOnlyList<string> paths,
        string inputKind,
        string family,
        string packagingRuleId,
        IReadOnlyList<string> selectedFamilies,
        IReadOnlyList<DocSource> sources,
        List<EvidenceDocChunk> chunks,
        List<EvidenceDocsDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        if (paths.Count == 0 || !selectedFamilies.Contains(family, StringComparer.Ordinal))
        {
            return;
        }

        foreach (var _ in paths)
        {
            try
            {
                var path = _;
                var json = await File.ReadAllTextAsync(path, cancellationToken);
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;
                var reportType = StringProperty(root, "reportType")
                    ?? StringProperty(root, "schemaVersion")
                    ?? StringProperty(root, "version");
                if (string.IsNullOrWhiteSpace(reportType))
                {
                    chunks.Add(CreateGapChunk($"{inputKind}-{Hash(path, 16)}", MissingProvenanceRuleId, "missing-provenance", family, sources, [inputKind], "hidden"));
                    diagnostics.Add(CreateDiagnostic("InputMissingProvenance", MissingProvenanceRuleId, $"/inputs/{inputKind}", "missing-provenance", inputKind));
                    continue;
                }

                var reportId = $"report:{Hash($"{inputKind}|{reportType}|{Hash(json, 64)}", 24)}";
                var sourceRefs = sources.Select(ToSourceRef).ToArray();
                var citation = CreateReportCitation(reportId, reportType, sources);
                var body = BuildReportBody(inputKind, family, reportType, reportId);
                chunks.Add(CreateChunk(
                    family,
                    "claim",
                    sources.Count == 0 ? "hidden" : MinClaim(sources.Select(source => source.ClaimLevel)),
                    $"{TitleForFamily(family)} report evidence",
                    $"Report `{EscapeInline(reportType)}` was supplied as deterministic `{EscapeInline(inputKind)}` evidence. It is preserved as static report evidence only.",
                    body,
                    [citation],
                    sourceRefs,
                    [reportId],
                    [packagingRuleId],
                    [EvidenceTiers.Tier2Structural],
                    sources.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    [],
                    [LimitationForFamily(family, [reportId])]));
            }
            catch (JsonException)
            {
                chunks.Add(CreateGapChunk($"{inputKind}-json-{Hash(inputKind, 16)}", SchemaGapRuleId, "schema-incompatible", family, sources, [inputKind], "hidden"));
                diagnostics.Add(CreateDiagnostic("InputSchemaIncompatible", SchemaGapRuleId, $"/inputs/{inputKind}", "schema-incompatible", inputKind));
            }
        }
    }

    private static EvidenceDocChunk CreateSourceOverviewChunk(DocSource source, IndexInput input)
    {
        var sourceRef = ToSourceRef(source);
        var limitations = new[]
        {
            new EvidenceDocLimitation(
                StableId("limitation", "docs-export/limitation/v1", [new("ruleId", SourceOverviewRuleId), new("source", source.SourceId)]),
                SourceOverviewRuleId,
                EvidenceTiers.Tier2Structural,
                "Source overview chunks summarize indexed static evidence metadata and do not prove runtime behavior, deployment, release approval, or production usage.",
                "source-overview",
                [source.SourceId])
        };
        var citation = new EvidenceDocCitation(
            StableId("citation", "docs-export/citation/v1", [new("source", source.SourceId), new("ruleId", SourceOverviewRuleId)]),
            source.Label,
            source.Scope,
            source.ScanId,
            source.CommitSha,
            source.CoverageLabel,
            null,
            null,
            null,
            [SourceOverviewRuleId],
            EvidenceTiers.Tier2Structural,
            "index-metadata",
            source.ExtractorVersion,
            [],
            [],
            []);
        var factCount = input.Facts.Count(fact => fact.Source.SourceId == source.SourceId);
        var body = $"""
            ## Source overview

            This chunk packages deterministic TraceMap source metadata for `{EscapeInline(source.Label)}`.

            | Field | Value |
            | --- | --- |
            | Source scope | `{EscapeInline(source.Scope)}` |
            | Scan ID | `{EscapeInline(source.ScanId ?? "unknown")}` |
            | Commit SHA | `{EscapeInline(DisplayCommitSha(source.CommitSha))}` |
            | Language | `{EscapeInline(source.Language)}` |
            | Coverage label | `{EscapeInline(source.CoverageLabel)}` |
            | Build status | `{EscapeInline(source.BuildStatus)}` |
            | Indexed facts | `{factCount}` |

            Citations: `{SourceOverviewRuleId}` / `{EvidenceTiers.Tier2Structural}`.
            """;
        return CreateChunk(
            "source-overview",
            "claim",
            source.ClaimLevel,
            "Source overview",
            "Static source metadata from a TraceMap index.",
            body,
            [citation],
            [sourceRef],
            [source.SourceId],
            [SourceOverviewRuleId],
            [EvidenceTiers.Tier2Structural],
            [source.CoverageLabel],
            [],
            limitations);
    }

    private static EvidenceDocChunk CreateFactChunk(DocFact fact, string family)
    {
        var legacyDataDescriptor = TryProjectLegacyDataDescriptor(fact);
        var packagingRuleId = PackagingRuleForFamily(family);
        var sourceRef = ToSourceRef(fact.Source);
        var citation = CreateFactCitation(fact);
        if (legacyDataDescriptor is not null)
        {
            return CreateLegacyDataDescriptorChunk(fact, family, legacyDataDescriptor, packagingRuleId, sourceRef, citation);
        }

        var safeMetadata = SafeFactMetadata(fact);
        var body = $"""
            ## {TitleForFamily(family)}

            This chunk packages deterministic `{EscapeInline(fact.FactType)}` evidence from `{EscapeInline(fact.Source.Label)}`.

            | Field | Value |
            | --- | --- |
            | Source | `{EscapeInline(fact.Source.Label)}` |
            | Commit SHA | `{EscapeInline(DisplayCommitSha(fact.CommitSha))}` |
            | Coverage label | `{EscapeInline(fact.Source.CoverageLabel)}` |
            | Fact type | `{EscapeInline(fact.FactType)}` |
            | Rule IDs | `{EscapeInline(string.Join(", ", DistinctSorted([fact.RuleId, packagingRuleId])))}` |
            | Evidence tier | `{EscapeInline(fact.EvidenceTier)}` |
            | File span | `{EscapeInline(FormatSpan(fact))}` |
            | Safe metadata | `{EscapeInline(safeMetadata)}` |

            Citations preserve supporting fact IDs and static evidence tiers. This chunk does not add runtime, deployment, approval, ownership, vulnerability, reachability, traffic, or business-impact conclusions.
            """;
        return CreateChunk(
            family,
            "claim",
            fact.Source.ClaimLevel,
            $"{TitleForFamily(family)} evidence",
            $"Static `{fact.FactType}` evidence with TraceMap citations.",
            body,
            [citation],
            [sourceRef],
            [fact.FactId],
            DistinctSorted([fact.RuleId, packagingRuleId]),
            DistinctSorted([fact.EvidenceTier]),
            [fact.Source.CoverageLabel],
            GapsForFact(fact, family),
            [LimitationForFamily(family, [fact.FactId])]);
    }

    private static EvidenceDocChunk CreateLegacyDataDescriptorChunk(
        DocFact fact,
        string family,
        LegacyDataModelDescriptorProjectionRow descriptor,
        string packagingRuleId,
        EvidenceDocSourceRef sourceRef,
        EvidenceDocCitation citation)
    {
        var ruleIds = DistinctSorted([fact.RuleId, RuleIds.LegacyDataModelSurface, packagingRuleId]);
        var supportingIds = DistinctSorted([fact.FactId, descriptor.DescriptorId, .. descriptor.SupportingFactIds]);
        var evidenceTiers = DistinctSorted([fact.EvidenceTier, descriptor.EvidenceTier]);
        var coverageLabels = DistinctSorted([fact.Source.CoverageLabel, descriptor.CoverageLabel]);
        var limitations = new[]
        {
            LimitationForFamily(family, supportingIds),
            new EvidenceDocLimitation(
                StableId("limitation", "docs-export/limitation/v1", [new("ruleId", RuleIds.LegacyDataModelSurface), new("descriptor", descriptor.DescriptorId)]),
                RuleIds.LegacyDataModelSurface,
                EvidenceTiers.Tier4Unknown,
                "Legacy data descriptor chunks preserve static model metadata only; they do not prove runtime database access, SQL execution, provider selection, live schema existence, production usage, or migration behavior.",
                family,
                supportingIds)
        };
        var body = $"""
            ## Legacy data model descriptor

            This chunk packages static legacy data model descriptor evidence from `{EscapeInline(fact.Source.Label)}` for retrieval.

            | Field | Value |
            | --- | --- |
            | Source | `{EscapeInline(fact.Source.Label)}` |
            | Commit SHA | `{EscapeInline(DisplayCommitSha(fact.CommitSha))}` |
            | Coverage label | `{EscapeInline(fact.Source.CoverageLabel)}` |
            | Fact type | `{EscapeInline(fact.FactType)}` |
            | Descriptor ID | `{EscapeInline(descriptor.DescriptorId)}` |
            | Display label | `{EscapeInline(descriptor.DisplayName)}` |
            | Display clearance | `{(descriptor.DisplayClearance ? "true" : "false")}` |
            | Metadata format | `{EscapeInline(descriptor.MetadataFormat)}` |
            | Source artifact type | `{EscapeInline(descriptor.SourceArtifactType)}` |
            | Model kind | `{EscapeInline(descriptor.ModelKind)}` |
            | Descriptor role | `{EscapeInline(descriptor.DescriptorRole)}` |
            | Projection rule | `{EscapeInline(RuleIds.LegacyDataModelSurface)}` |
            | Source rule | `{EscapeInline(fact.RuleId)}` |
            | Evidence tiers | `{EscapeInline(string.Join(", ", evidenceTiers))}` |
            | Coverage labels | `{EscapeInline(string.Join(", ", coverageLabels))}` |
            | File span | `{EscapeInline(FormatSpan(fact))}` |
            | Limitations | `{EscapeInline(string.Join(", ", descriptor.Limitations))}` |

            Citations preserve source facts, descriptor projection metadata, and static evidence tiers. This chunk does not claim that a database exists, SQL executed, a provider was selected at runtime, a migration ran, or production data was accessed.
            """;

        return CreateChunk(
            family,
            "claim",
            fact.Source.ClaimLevel,
            "Legacy data model descriptor evidence",
            "Static legacy data model descriptor evidence with TraceMap citations.",
            body,
            [citation],
            [sourceRef],
            supportingIds,
            ruleIds,
            evidenceTiers,
            coverageLabels,
            GapsForFact(fact, family),
            limitations);
    }

    private static EvidenceDocChunk CreateFactGapChunk(DocFact fact)
    {
        var gap = new EvidenceDocGap(
            StableId("gap", "docs-export/gap/v1", [new("reason", "unknown-analysis"), new("fact", fact.FactId)]),
            string.IsNullOrWhiteSpace(fact.RuleId) ? UnknownAnalysisRuleId : fact.RuleId,
            string.IsNullOrWhiteSpace(fact.EvidenceTier) ? Tier4Unknown : fact.EvidenceTier,
            "reduced-coverage",
            "gap",
            [ToSourceRef(fact.Source)],
            [fact.FactId],
            ["Analysis gap facts preserve uncertainty and do not prove absence."]);
        return CreateGapChunkFromGap(gap, fact.Source.ClaimLevel);
    }

    private static EvidenceDocChunk CreateGapChunk(string key, string ruleId, string reason, string family, IReadOnlyList<DocSource> sources, IReadOnlyList<string> supportingIds, string claimLevel)
    {
        var gap = new EvidenceDocGap(
            StableId("gap", "docs-export/gap/v1", [new("reason", reason), new("family", family), new("key", key)]),
            ruleId,
            Tier4Unknown,
            reason,
            family,
            sources.Select(ToSourceRef).ToArray(),
            supportingIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["Docs-export gaps describe evidence availability or safety boundaries; they do not prove source behavior or absence."]);
        return CreateGapChunkFromGap(gap, claimLevel);
    }

    private static EvidenceDocChunk CreateGapChunkFromGap(EvidenceDocGap gap, string claimLevel)
    {
        var body = $"""
            ## Evidence gap

            Docs export recorded a `{EscapeInline(gap.Reason)}` gap for `{EscapeInline(gap.ChunkFamily)}`.

            | Field | Value |
            | --- | --- |
            | Gap ID | `{EscapeInline(gap.GapId)}` |
            | Rule ID | `{EscapeInline(gap.RuleId)}` |
            | Evidence tier | `{EscapeInline(gap.EvidenceTier)}` |
            | Supporting IDs | `{EscapeInline(string.Join(", ", gap.SupportingIds))}` |

            Gap chunks preserve uncertainty for external ingestion and do not upgrade conclusions.
            """;
        return CreateChunk(
            "gap",
            "gap",
            claimLevel,
            "Evidence gap",
            $"Docs-export gap: {gap.Reason}.",
            body,
            [],
            gap.SourceRefs,
            gap.SupportingIds,
            [GapChunkRuleId, gap.RuleId],
            [gap.EvidenceTier],
            gap.SourceRefs.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            [gap],
            [new EvidenceDocLimitation(
                StableId("limitation", "docs-export/limitation/v1", [new("gap", gap.GapId)]),
                gap.RuleId,
                gap.EvidenceTier,
                "Gap chunks are evidence availability records and must not be read as clean absence findings.",
                "gap",
                gap.SupportingIds)]);
    }

    private static EvidenceDocChunk CreateLimitationChunk(IReadOnlyList<DocSource> sources)
    {
        var limitation = new EvidenceDocLimitation(
            StableId("limitation", "docs-export/limitation/v1", [new("ruleId", LimitationChunkRuleId), new("scope", "docs-export")]),
            LimitationChunkRuleId,
            Tier4Unknown,
            "Docs export packages existing deterministic evidence for external ingestion and does not call models, generate embeddings, write vector databases, rank retrieval results, or answer natural-language questions.",
            "limitation",
            ["docs-export"]);
        var body = $"""
            ## Docs export limitations

            This chunk records the docs-export boundary as first-class retrieval evidence.

            | Field | Value |
            | --- | --- |
            | Rule ID | `{LimitationChunkRuleId}` |
            | Evidence tier | `{Tier4Unknown}` |

            TraceMap emits deterministic evidence documents. External systems remain responsible for retrieval, embeddings, ranking, answer generation, access controls, and data retention.
            """;
        return CreateChunk(
            "limitation",
            "limitation",
            MinClaim(sources.Select(source => source.ClaimLevel).DefaultIfEmpty("hidden")),
            "Docs export limitations",
            "Docs-export boundary and limitation evidence.",
            body,
            [],
            sources.Select(ToSourceRef).ToArray(),
            ["docs-export"],
            [LimitationChunkRuleId],
            [Tier4Unknown],
            sources.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).DefaultIfEmpty("not-applicable").ToArray(),
            [],
            [limitation]);
    }

    private static EvidenceDocChunk CreateChunk(
        string family,
        string type,
        string claimLevel,
        string title,
        string summary,
        string bodyMarkdown,
        IReadOnlyList<EvidenceDocCitation> citations,
        IReadOnlyList<EvidenceDocSourceRef> sourceRefs,
        IReadOnlyList<string> supportingIds,
        IReadOnlyList<string> ruleIds,
        IReadOnlyList<string> evidenceTiers,
        IReadOnlyList<string> coverageLabels,
        IReadOnlyList<EvidenceDocGap> gaps,
        IReadOnlyList<EvidenceDocLimitation> limitations)
    {
        var orderedSupportingIds = supportingIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var orderedRules = ruleIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var orderedTiers = evidenceTiers.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var orderedCoverage = coverageLabels.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var sourceKey = string.Join('|', sourceRefs.Select(source => source.SourceId).OrderBy(value => value, StringComparer.Ordinal));
        var id = StableId("chunk", "docs-export/chunk/v1",
        [
            new("schemaVersion", SchemaVersion),
            new("chunkFamily", family),
            new("chunkType", type),
            new("sourceIdentity", sourceKey),
            new("claimLevel", claimLevel),
            new("ruleIds", string.Join('|', orderedRules)),
            new("evidenceTiers", string.Join('|', orderedTiers)),
            new("supportingIds", string.Join('|', orderedSupportingIds)),
            new("title", title)
        ]);
        var sortKey = $"{Array.IndexOf(AllFamilies, family):D2}|{family}|{string.Join('|', orderedCoverage)}|{string.Join('|', orderedRules)}|{id}|{title}";
        var sectionTitle = SectionTitleFor(family, type);
        var questionFamilies = QuestionFamiliesFor(family, type, orderedTiers, orderedCoverage, gaps, limitations);
        var claim = ClaimFor(type, family, claimLevel, orderedRules.Length == 0 ? [GapChunkRuleId] : orderedRules, orderedTiers.Length == 0 ? [Tier4Unknown] : orderedTiers, orderedCoverage, orderedSupportingIds, gaps, limitations);
        return new EvidenceDocChunk(
            SchemaVersion,
            id,
            type,
            family,
            questionFamilies,
            claimLevel,
            title,
            sectionTitle,
            sortKey,
            summary,
            claim,
            NormalizeMarkdownBody(bodyMarkdown),
            citations.OrderBy(citation => citation.CitationId, StringComparer.Ordinal).ToArray(),
            sourceRefs.OrderBy(source => source.SourceLabel, StringComparer.Ordinal).ThenBy(source => source.SourceId, StringComparer.Ordinal).ToArray(),
            orderedSupportingIds,
            orderedRules.Length == 0 ? [GapChunkRuleId] : orderedRules,
            orderedTiers.Length == 0 ? [Tier4Unknown] : orderedTiers,
            orderedCoverage,
            gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            limitations.OrderBy(limitation => limitation.LimitationId, StringComparer.Ordinal).ToArray(),
            [],
            []);
    }

    private static string SectionTitleFor(string family, string type)
    {
        if (type == "gap")
        {
            return "What could TraceMap not prove or export?";
        }

        if (type == "limitation")
        {
            return "What limitations constrain this evidence?";
        }

        return family switch
        {
            "endpoint" or "route-flow" => "What static evidence describes this endpoint or route?",
            "dependency-surface" or "data-surface" or "query-sql-shape" or "property-flow" => "What code has static evidence of touching this surface?",
            "package-config" => "What package or configuration metadata is present?",
            "release-review" => "What changed in the supplied release-review evidence?",
            "gap" => "What could TraceMap not prove or export?",
            "limitation" => "What limitations constrain this evidence?",
            _ => "What static evidence does this chunk cite?"
        };
    }

    private static IReadOnlyList<string> QuestionFamiliesFor(
        string family,
        string type,
        IReadOnlyList<string> evidenceTiers,
        IReadOnlyList<string> coverageLabels,
        IReadOnlyList<EvidenceDocGap> gaps,
        IReadOnlyList<EvidenceDocLimitation> limitations)
    {
        var values = new List<string>();
        switch (family)
        {
            case "endpoint":
            case "route-flow":
                values.Add("endpoint-question");
                break;
            case "dependency-surface":
            case "data-surface":
            case "query-sql-shape":
            case "property-flow":
                values.Add("data-surface-question");
                break;
            case "package-config":
                values.Add("package-question");
                break;
            case "release-review":
                values.Add("snapshot-change-question");
                break;
        }

        if (type == "gap" || gaps.Count > 0)
        {
            values.Add("gap-question");
        }

        if (type == "limitation" || limitations.Count > 0)
        {
            values.Add("limitation-question");
        }

        if (evidenceTiers.Any(tier => tier is EvidenceTiers.Tier3SyntaxOrTextual or Tier4Unknown)
            || coverageLabels.Any(IsWeakCoverageLabel)
            || gaps.Count > 0
            || type == "gap")
        {
            values.Add("weak-evidence-question");
        }

        return values
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value =>
            {
                var index = Array.IndexOf(QuestionFamilyOrder, value);
                return index < 0 ? int.MaxValue : index;
            })
            .ThenBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static bool IsWeakCoverageLabel(string value)
    {
        return value.Contains("reduced", StringComparison.OrdinalIgnoreCase)
            || value.Contains("partial", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || value.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static EvidenceDocClaim ClaimFor(
        string type,
        string family,
        string claimLevel,
        IReadOnlyList<string> ruleIds,
        IReadOnlyList<string> evidenceTiers,
        IReadOnlyList<string> coverageLabels,
        IReadOnlyList<string> supportingIds,
        IReadOnlyList<EvidenceDocGap> gaps,
        IReadOnlyList<EvidenceDocLimitation> limitations)
    {
        var kind = type switch
        {
            "gap" => "gap-statement",
            "limitation" => "limitation-statement",
            _ when evidenceTiers.Any(tier => tier is EvidenceTiers.Tier3SyntaxOrTextual or Tier4Unknown)
                || coverageLabels.Any(IsWeakCoverageLabel)
                || gaps.Count > 0 => "weak-static-evidence",
            _ => "static-evidence"
        };
        var text = kind switch
        {
            "gap-statement" => $"TraceMap recorded a rule-backed evidence gap for {family}.",
            "limitation-statement" => $"TraceMap recorded deterministic limitations for {family}.",
            "weak-static-evidence" => $"TraceMap has lower-tier or reduced-coverage static evidence for {family}.",
            _ => $"TraceMap has deterministic static evidence for {family}."
        };
        var classification = kind == "weak-static-evidence" || kind == "gap-statement" ? "NeedsReview" : null;
        return new EvidenceDocClaim(
            kind,
            text,
            classification,
            claimLevel,
            ruleIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            evidenceTiers.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            coverageLabels.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            supportingIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            limitations.Select(limitation => limitation.LimitationId).Concat(gaps.Select(gap => gap.GapId)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static List<EvidenceDocChunk> ApplyClaimFilter(List<EvidenceDocChunk> chunks, string minimumClaimLevel, IReadOnlyList<string> selectedFamilies, IReadOnlyList<DocSource> sources)
    {
        if (minimumClaimLevel == "hidden")
        {
            return chunks;
        }

        var minimum = ClaimRank(minimumClaimLevel);
        var visible = chunks.Where(chunk => ClaimRank(chunk.ClaimLevel) >= minimum).ToList();
        var omitted = chunks.Count - visible.Count;
        if (omitted > 0 && selectedFamilies.Contains("gap", StringComparer.Ordinal))
        {
            visible.Add(CreateGapChunk("claim-level-hidden", ClaimHiddenRuleId, "claim-level-hidden", "gap", sources, ["claim-level-filter"], minimumClaimLevel));
        }

        return visible;
    }

    private static List<EvidenceDocChunk> DetectDuplicateChunkIds(List<EvidenceDocChunk> chunks, IReadOnlyList<DocSource> sources)
    {
        var duplicates = chunks.GroupBy(chunk => chunk.ChunkId, StringComparer.Ordinal).Where(group => group.Count() > 1).ToArray();
        if (duplicates.Length == 0)
        {
            return chunks;
        }

        var duplicateIds = duplicates.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
        var filtered = chunks.Where(chunk => !duplicateIds.Contains(chunk.ChunkId)).ToList();
        foreach (var duplicate in duplicates)
        {
            filtered.Add(CreateGapChunk($"duplicate-{duplicate.Key}", DuplicateIdentityRuleId, "duplicate-stable-identity", "gap", sources, duplicate.Select(chunk => chunk.ChunkId).ToArray(), MinClaim(duplicate.Select(chunk => chunk.ClaimLevel))));
        }

        return filtered;
    }

    private static EvidenceDocsManifest BuildManifest(
        EvidenceDocsExportOptions options,
        IndexInput input,
        IReadOnlyList<EvidenceDocChunk> chunks,
        IReadOnlyList<string> formats,
        IReadOnlyList<string> selectedFamilies,
        string minimumClaimLevel,
        string generatedAt,
        IReadOnlyList<EvidenceDocsOutputSummary> outputs,
        IReadOnlyList<EvidenceDocLimitation> limitations)
    {
        var inputs = new List<EvidenceDocsInputSummary>
        {
            new(
                input.Kind,
                $"index:{Hash(IndexIdentity(input), 24)}",
                "hidden",
                "compatible",
                input.Sources.Select(source => source.Label).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                input.Sources.Select(source => source.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                input.Kind == "single-index"
                    ? ["Single-source indexes cannot prove combined report families; unavailable combined views are represented as gaps."]
                    : [],
                input.Sources.Select(ToSourceRef).ToArray())
        };
        var chunkCounts = chunks
            .GroupBy(chunk => chunk.ChunkFamily, StringComparer.Ordinal)
            .OrderBy(group => Array.IndexOf(AllFamilies, group.Key))
            .Select(group => new EvidenceDocsCount(group.Key, group.Count()))
            .ToArray();
        var omittedCounts = AllFamilies
            .Where(family => !selectedFamilies.Contains(family, StringComparer.Ordinal))
            .Select(family => new EvidenceDocsCount($"{family}:not_requested", 0))
            .ToArray();
        var gaps = chunks.SelectMany(chunk => chunk.Gaps)
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        return new EvidenceDocsManifest(
            SchemaVersion,
            true,
            string.Empty,
            new EvidenceDocsGenerator(GeneratorName, GeneratorVersion, generatedAt),
            minimumClaimLevel,
            formats,
            new EvidenceDocsGenerationSettings(selectedFamilies, minimumClaimLevel, IncludeRawSnippets: false),
            inputs,
            outputs.OrderBy(output => output.Path, StringComparer.Ordinal).ToArray(),
            chunkCounts,
            omittedCounts,
            gaps,
            limitations,
            input.Sources.Select(source => $"repo:{Hash(source.SourceId, 24)}").Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            input.Sources.Select(source => source.CommitSha)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray());
    }

    private static EvidenceDocsManifest WithManifestHash(EvidenceDocsManifest manifest)
    {
        var blank = manifest with { ContentHash = string.Empty };
        return manifest with { ContentHash = Hash(SerializeJson(blank), 64) };
    }

    private static Dictionary<string, string> BuildGeneratedFiles(string outputPath, EvidenceDocsManifest manifest, IReadOnlyList<EvidenceDocChunk> chunks, IReadOnlyList<string> formats)
    {
        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["manifest.json"] = SerializeJson(manifest)
        };

        if (formats.Contains("jsonl", StringComparer.Ordinal))
        {
            var jsonl = new StringBuilder();
            foreach (var chunk in chunks)
            {
                jsonl.Append(JsonSerializer.Serialize(chunk, JsonLineOptions)).Append('\n');
            }

            files["chunks.jsonl"] = jsonl.ToString();
        }

        if (formats.Contains("markdown", StringComparer.Ordinal))
        {
            files["README.md"] = SummaryMarkdown("readme", manifest, chunks);
            files["index.md"] = SummaryMarkdown("index", manifest, chunks);
            foreach (var familyGroup in chunks.GroupBy(chunk => chunk.ChunkFamily, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                files[$"chunks/{familyGroup.Key}/index.md"] = FamilyIndexMarkdown(manifest.ClaimLevel, familyGroup.Key, familyGroup.ToArray());
            }

            foreach (var chunk in chunks)
            {
                files[ChunkPath(chunk)] = ChunkMarkdown(chunk);
            }
        }

        return files.ToDictionary(pair => pair.Key, pair => WithMarkdownHash(pair.Value), StringComparer.Ordinal);
    }

    private static IReadOnlyList<EvidenceDocsOutputSummary> BuildOutputSummaries(string outputPath, IReadOnlyDictionary<string, string> files)
    {
        return files
            .Where(pair => pair.Key != "manifest.json")
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair =>
            {
                var bytes = Encoding.UTF8.GetBytes(pair.Value);
                return new EvidenceDocsOutputSummary(
                    pair.Key,
                    pair.Key.EndsWith(".jsonl", StringComparison.Ordinal) ? "jsonl" : "markdown",
                    SchemaVersion,
                    GeneratorName,
                    bytes.LongLength,
                    pair.Value.Count(character => character == '\n'),
                    Hash(pair.Value, 64));
            })
            .ToArray();
    }

    private static string SummaryMarkdown(string kind, EvidenceDocsManifest manifest, IReadOnlyList<EvidenceDocChunk> chunks)
    {
        var sourceLabels = manifest.Inputs.SelectMany(input => input.SourceLabels).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var frontmatter = SummaryFrontmatter(kind, manifest.ClaimLevel, sourceLabels, "");
        var body = new StringBuilder();
        body.AppendLine("# TraceMap Evidence Docs");
        body.AppendLine();
        body.AppendLine("TraceMap emits deterministic evidence documentation for external systems. Retrieval, embeddings, ranking, answer generation, access controls, and data retention remain outside TraceMap core.");
        body.AppendLine();
        body.AppendLine("| Field | Value |");
        body.AppendLine("| --- | --- |");
        body.AppendLine($"| Schema | `{SchemaVersion}` |");
        body.AppendLine($"| Claim level | `{EscapeInline(manifest.ClaimLevel)}` |");
        body.AppendLine($"| Generated at | `{EscapeInline(manifest.Generator.GeneratedAt)}` |");
        body.AppendLine($"| Chunks | `{chunks.Count}` |");
        body.AppendLine();
        body.AppendLine("## Chunk counts");
        body.AppendLine();
        body.AppendLine("| Family | Count |");
        body.AppendLine("| --- | --- |");
        foreach (var count in manifest.ChunkCounts.OrderBy(count => count.Key, StringComparer.Ordinal))
        {
            body.AppendLine($"| [`{EscapeInline(count.Key)}`](chunks/{EscapeInline(count.Key)}/index.md) | `{count.Count}` |");
        }

        body.AppendLine();
        body.AppendLine("## Navigation");
        body.AppendLine();
        body.AppendLine("- [Chunk index](index.md)");
        foreach (var family in chunks.Select(chunk => chunk.ChunkFamily).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal))
        {
            body.AppendLine($"- [{EscapeText(TitleForFamily(family))} chunks](chunks/{EscapeInline(family)}/index.md)");
        }

        body.AppendLine();
        body.AppendLine("## Limitations");
        body.AppendLine();
        body.AppendLine("- Documentation chunks preserve static evidence, rule IDs, evidence tiers, coverage labels, gaps, and limitations.");
        body.AppendLine("- These files do not prove runtime execution, production traffic, release approval, vulnerability status, ownership, deployment, service reachability, or business impact.");
        return frontmatter + body;
    }

    private static string FamilyIndexMarkdown(string claimLevel, string family, IReadOnlyList<EvidenceDocChunk> chunks)
    {
        var sourceLabels = chunks.SelectMany(chunk => chunk.SourceRefs).Select(source => source.SourceLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();
        builder.Append(SummaryFrontmatter($"{family}-index", claimLevel, sourceLabels, ""));
        builder.AppendLine($"# {EscapeHeading(TitleForFamily(family))} Chunks");
        builder.AppendLine();
        builder.AppendLine("[All docs](../../index.md)");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Chunk family | `{EscapeInline(family)}` |");
        builder.AppendLine($"| Chunks | `{chunks.Count}` |");
        builder.AppendLine($"| Claim level | `{EscapeInline(claimLevel)}` |");
        builder.AppendLine();
        builder.AppendLine("| Chunk | Claim kind | Question families | Rule IDs | Evidence tiers |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var chunk in chunks.OrderBy(chunk => chunk.SortKey, StringComparer.Ordinal).ThenBy(chunk => chunk.ChunkId, StringComparer.Ordinal))
        {
            builder.AppendLine($"| [{EscapeText(chunk.Title)}]({EscapeInline(Path.GetFileName(ChunkPath(chunk)))}) | `{EscapeInline(chunk.Claim.Kind)}` | `{EscapeInline(string.Join(", ", chunk.QuestionFamilies))}` | `{EscapeInline(string.Join(", ", chunk.RuleIds))}` | `{EscapeInline(string.Join(", ", chunk.EvidenceTiers))}` |");
        }

        builder.AppendLine();
        builder.AppendLine("This index is navigation metadata over deterministic static evidence. It does not add new evidence or prove runtime behavior.");
        return builder.ToString();
    }

    private static string ChunkMarkdown(EvidenceDocChunk chunk)
    {
        var sourceLabels = chunk.SourceRefs.Select(source => source.SourceLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var builder = new StringBuilder();
        builder.Append(ChunkFrontmatter(chunk, sourceLabels, ""));
        builder.AppendLine($"# {EscapeHeading(chunk.Title)}");
        builder.AppendLine();
        builder.AppendLine("## Navigation");
        builder.AppendLine();
        foreach (var link in chunk.Links.OrderBy(link => link.LinkId, StringComparer.Ordinal))
        {
            builder.AppendLine($"- [{EscapeText(link.Label)}]({EscapeInline(RelativeChunkLink(chunk, link.Target))})");
        }

        builder.AppendLine();
        builder.AppendLine($"## {EscapeHeading(chunk.SectionTitle)}");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Claim kind | `{EscapeInline(chunk.Claim.Kind)}` |");
        builder.AppendLine($"| Claim | {EscapeText(chunk.Claim.Text)} |");
        builder.AppendLine($"| Claim level | `{EscapeInline(chunk.Claim.ClaimLevel)}` |");
        builder.AppendLine($"| Evidence tiers | `{EscapeInline(string.Join(", ", chunk.Claim.EvidenceTiers))}` |");
        builder.AppendLine($"| Rule IDs | `{EscapeInline(string.Join(", ", chunk.Claim.RuleIds))}` |");
        builder.AppendLine($"| Question families | `{EscapeInline(string.Join(", ", chunk.QuestionFamilies))}` |");
        if (!string.IsNullOrWhiteSpace(chunk.Claim.Classification))
        {
            builder.AppendLine($"| Classification | `{EscapeInline(chunk.Claim.Classification)}` |");
        }

        builder.AppendLine();
        builder.AppendLine(chunk.BodyMarkdown.TrimEnd());
        builder.AppendLine();
        builder.AppendLine("## Citations");
        builder.AppendLine();
        if (chunk.Citations.Count == 0)
        {
            builder.AppendLine("- No source citation is available; see gap records for the rule-backed uncertainty.");
        }
        else
        {
            foreach (var citation in chunk.Citations)
            {
                builder.AppendLine($"- `{EscapeInline(citation.CitationId)}`: rules `{EscapeInline(string.Join(", ", citation.RuleIds))}`, tier `{EscapeInline(citation.EvidenceTier)}`, source `{EscapeInline(citation.SourceLabel ?? "unknown")}`, span `{EscapeInline(citation.FilePath ?? "unknown")}:{citation.StartLine?.ToString() ?? "unknown"}-{citation.EndLine?.ToString() ?? "unknown"}`.");
            }
        }

        if (chunk.Gaps.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Gaps");
            builder.AppendLine();
            foreach (var gap in chunk.Gaps)
            {
                builder.AppendLine($"- `{EscapeInline(gap.GapId)}`: `{EscapeInline(gap.Reason)}` via `{EscapeInline(gap.RuleId)}`.");
            }
        }

        if (chunk.Limitations.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("## Limitations");
            builder.AppendLine();
            foreach (var limitation in chunk.Limitations)
            {
                builder.AppendLine($"- `{EscapeInline(limitation.RuleId)}`: {EscapeText(limitation.Message)}");
            }
        }

        return builder.ToString();
    }

    private static IReadOnlyList<EvidenceDocChunk> AddNavigationLinks(IReadOnlyList<EvidenceDocChunk> chunks, IReadOnlyList<string> formats)
    {
        var markdownAvailable = formats.Contains("markdown", StringComparer.Ordinal);
        return chunks
            .Select(chunk => chunk with
            {
                Links = NavigationLinksFor(chunk, markdownAvailable)
            })
            .ToArray();
    }

    private static IReadOnlyList<EvidenceDocLink> NavigationLinksFor(EvidenceDocChunk chunk, bool markdownAvailable)
    {
        if (!markdownAvailable)
        {
            return [];
        }

        return
        [
            new EvidenceDocLink("chunk-markdown", "Chunk Markdown", ChunkPath(chunk)),
            new EvidenceDocLink("docs-index", "All Evidence Docs", "index.md"),
            new EvidenceDocLink("family-index", $"{TitleForFamily(chunk.ChunkFamily)} Index", $"chunks/{chunk.ChunkFamily}/index.md")
        ];
    }

    private static string ChunkPath(EvidenceDocChunk chunk)
    {
        return $"chunks/{chunk.ChunkFamily}/{Slug(chunk.ChunkId)}.md";
    }

    private static string RelativeChunkLink(EvidenceDocChunk chunk, string target)
    {
        var currentDirectory = Path.GetDirectoryName(ChunkPath(chunk))?.Replace('\\', '/') ?? string.Empty;
        var normalizedTarget = target.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(currentDirectory))
        {
            return normalizedTarget;
        }

        var targetDirectory = Path.GetDirectoryName(normalizedTarget)?.Replace('\\', '/') ?? string.Empty;
        var targetFile = Path.GetFileName(normalizedTarget);
        if (string.Equals(currentDirectory, targetDirectory, StringComparison.Ordinal))
        {
            return string.IsNullOrWhiteSpace(targetFile) ? "." : targetFile;
        }

        var currentParts = currentDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var targetParts = normalizedTarget.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var common = 0;
        while (common < currentParts.Length
               && common < targetParts.Length
               && string.Equals(currentParts[common], targetParts[common], StringComparison.Ordinal))
        {
            common++;
        }

        var relative = new List<string>();
        for (var index = common; index < currentParts.Length; index++)
        {
            relative.Add("..");
        }

        relative.AddRange(targetParts.Skip(common));
        return relative.Count == 0 ? "." : string.Join('/', relative);
    }

    private static string WithMarkdownHash(string content)
    {
        if (!content.StartsWith("---\n", StringComparison.Ordinal))
        {
            return content;
        }

        var normalized = NormalizeLineEndings(content);
        var hash = Hash(NormalizeMarkdownHashInput(normalized), 64);
        return normalized.Replace("tracemap_content_sha256: \n", $"tracemap_content_sha256: {hash}\n", StringComparison.Ordinal)
            .Replace("tracemap_content_sha256: \"\"\n", $"tracemap_content_sha256: {hash}\n", StringComparison.Ordinal);
    }

    private static string ChunkFrontmatter(EvidenceDocChunk chunk, IReadOnlyList<string> sourceLabels, string hash)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("tracemap_generated: true");
        builder.AppendLine($"tracemap_export_schema: {SchemaVersion}");
        builder.AppendLine($"tracemap_generator: {GeneratorName}");
        builder.AppendLine($"tracemap_content_sha256: {hash}");
        builder.AppendLine($"chunk_id: {chunk.ChunkId}");
        builder.AppendLine($"chunk_family: {chunk.ChunkFamily}");
        AppendYamlArray(builder, "question_families", chunk.QuestionFamilies);
        builder.AppendLine($"claim_level: {chunk.ClaimLevel}");
        builder.AppendLine($"section_title: {YamlScalar(chunk.SectionTitle)}");
        AppendYamlArray(builder, "source_labels", sourceLabels);
        builder.AppendLine("---");
        builder.AppendLine();
        return builder.ToString();
    }

    private static string SummaryFrontmatter(string kind, string claimLevel, IReadOnlyList<string> sourceLabels, string hash)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        builder.AppendLine("tracemap_generated: true");
        builder.AppendLine($"tracemap_export_schema: {SchemaVersion}");
        builder.AppendLine($"tracemap_generator: {GeneratorName}");
        builder.AppendLine($"tracemap_content_sha256: {hash}");
        builder.AppendLine($"summary_kind: {kind}");
        builder.AppendLine($"claim_level: {claimLevel}");
        AppendYamlArray(builder, "source_labels", sourceLabels);
        builder.AppendLine("---");
        builder.AppendLine();
        return builder.ToString();
    }

    private static void AppendYamlArray(StringBuilder builder, string key, IReadOnlyList<string> values)
    {
        builder.AppendLine($"{key}:");
        foreach (var value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            builder.AppendLine($"  - {YamlScalar(value)}");
        }
    }

    private static void ValidateGeneratedStrings(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            if (!SafeRelativeOutputPath(path))
            {
                throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: unsafe-file-name at outputPath.");
            }

            var lines = content.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var category = UnsafeCategory(lines[i]);
                if (category is not null)
                {
                    throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {category} at {path}:{i + 1}.");
                }
            }

            if ((path.EndsWith(".md", StringComparison.Ordinal) || path.EndsWith(".jsonl", StringComparison.Ordinal))
                && ContainsProhibitedClaim(content))
            {
                throw new InvalidOperationException($"ProhibitedClaimWording: {ProhibitedClaimRuleId} [{Tier4Unknown}]: unsupported-static-claim at {path}.");
            }
        }
    }

    private static async Task ValidateExistingFilesAsync(string outputPath, IReadOnlyDictionary<string, string> files, bool force, CancellationToken cancellationToken)
    {
        var root = Path.GetFullPath(outputPath);
        var manifestPath = Path.Combine(root, "manifest.json");
        var existingManifest = File.Exists(manifestPath) ? await File.ReadAllTextAsync(manifestPath, cancellationToken) : null;
        var manifestHasGeneratedMarker = existingManifest is not null && HasGeneratedManifestMarker(existingManifest);
        var manifestGenerated = existingManifest is not null && IsSelfConsistentManifest(existingManifest);
        var manifestStale = manifestHasGeneratedMarker && !manifestGenerated;

        foreach (var relativePath in files.Keys)
        {
            var path = Path.Combine(root, relativePath);
            ValidateOutputPathShape(root, relativePath, path);
            if (!File.Exists(path))
            {
                continue;
            }

            var content = await File.ReadAllTextAsync(path, cancellationToken);
            if (relativePath == "manifest.json")
            {
                if (IsSelfConsistentManifest(content) || force && HasGeneratedManifestMarker(content))
                {
                    continue;
                }

                throw new InvalidOperationException(HasGeneratedManifestMarker(content)
                    ? $"GeneratedFileStale: {GeneratedFileStaleRuleId} [{Tier4Unknown}]: manifest."
                    : $"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: manifest.");
            }

            if (relativePath == "chunks.jsonl")
            {
                if (manifestGenerated && ManifestHasMatchingOutput(existingManifest!, relativePath, content)
                    || force && manifestHasGeneratedMarker)
                {
                    continue;
                }

                throw new InvalidOperationException(manifestHasGeneratedMarker
                    ? $"GeneratedFileStale: {GeneratedFileStaleRuleId} [{Tier4Unknown}]: chunks-jsonl."
                    : $"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: chunks-jsonl.");
            }

            if (relativePath.EndsWith(".md", StringComparison.Ordinal))
            {
                if (IsSelfConsistentMarkdown(content) || force && HasGeneratedMarkdownMarker(content))
                {
                    continue;
                }

                throw new InvalidOperationException(HasGeneratedMarkdownMarker(content)
                    ? $"GeneratedFileStale: {GeneratedFileStaleRuleId} [{Tier4Unknown}]: markdown."
                    : $"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: markdown.");
            }
        }
    }

    private static void ValidateOutputPathShape(string root, string relativePath, string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            throw new InvalidOperationException($"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: output-path-directory.");
        }

        var parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = root;
        for (var index = 0; index < parts.Length - 1; index++)
        {
            current = Path.Combine(current, parts[index]);
            if (File.Exists(current))
            {
                throw new InvalidOperationException($"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: output-parent-file.");
            }
        }
    }

    private static bool ManifestHasMatchingOutput(string manifestContent, string relativePath, string fileContent)
    {
        try
        {
            using var document = JsonDocument.Parse(manifestContent);
            if (!document.RootElement.TryGetProperty("outputs", out var outputs) || outputs.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var hash = Hash(fileContent, 64);
            foreach (var output in outputs.EnumerateArray())
            {
                if (StringProperty(output, "path") == relativePath && StringProperty(output, "sha256") == hash)
                {
                    return true;
                }
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasGeneratedManifestMarker(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("schemaVersion", out var schema)
                && schema.GetString() == SchemaVersion
                && document.RootElement.TryGetProperty("tracemapGenerated", out var generated)
                && generated.ValueKind == JsonValueKind.True
                && document.RootElement.TryGetProperty("generator", out var generator)
                && StringProperty(generator, "name") == GeneratorName;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasGeneratedMarkdownMarker(string content)
    {
        return TryReadFrontmatter(content, out var metadata, out _)
            && metadata.GetValueOrDefault("tracemap_export_schema") == SchemaVersion
            && metadata.GetValueOrDefault("tracemap_generator") == GeneratorName;
    }

    private static async Task<SourceClaimCatalog> ReadClaimCatalogAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SourceClaimCatalog([]);
        }

        try
        {
            var content = await File.ReadAllTextAsync(path, cancellationToken);
            using var document = JsonDocument.Parse(content);
            if (StringProperty(document.RootElement, "schemaVersion") != "source-claim-catalog.v1")
            {
                return new SourceClaimCatalog([]);
            }

            var claims = new List<SourceClaim>();
            if (document.RootElement.TryGetProperty("sources", out var sources) && sources.ValueKind == JsonValueKind.Array)
            {
                foreach (var source in sources.EnumerateArray())
                {
                    var claimLevel = StringProperty(source, "claimLevel");
                    if (claimLevel is not ("demo-safe" or "public-safe")
                        || string.IsNullOrWhiteSpace(StringProperty(source, "proofId")))
                    {
                        continue;
                    }

                    claims.Add(new SourceClaim(
                        StringProperty(source, "sourceIndexId"),
                        StringProperty(source, "scanId"),
                        CommitOrNull(StringProperty(source, "commitSha")),
                        claimLevel));
                }
            }

            if (document.RootElement.TryGetProperty("entries", out var entries) && entries.ValueKind == JsonValueKind.Array)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var claimLevel = StringProperty(entry, "claimLevel");
                    if (claimLevel is not ("demo-safe" or "public-safe") || !HasReviewedProofMetadata(entry))
                    {
                        continue;
                    }

                    if (!entry.TryGetProperty("sourceIdentity", out var identity) || identity.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    claims.Add(new SourceClaim(
                        StringProperty(identity, "sourceIndexId"),
                        StringProperty(identity, "scanId"),
                        CommitOrNull(StringProperty(identity, "commitSha")),
                        claimLevel));
                }
            }

            return new SourceClaimCatalog(claims);
        }
        catch
        {
            return new SourceClaimCatalog([]);
        }
    }

    private static bool HasReviewedProofMetadata(JsonElement entry)
    {
        return !string.IsNullOrWhiteSpace(StringProperty(entry, "proofId"))
            && !string.IsNullOrWhiteSpace(StringProperty(entry, "proofPathCategory"))
            && !string.IsNullOrWhiteSpace(StringProperty(entry, "reviewer"))
            && !string.IsNullOrWhiteSpace(StringProperty(entry, "reviewedAt"));
    }

    private static void ApplyCatalogClaims(IReadOnlyList<DocSource> sources, SourceClaimCatalog catalog, List<EvidenceDocsDiagnostic> diagnostics)
    {
        foreach (var source in sources)
        {
            var claim = catalog.ClaimFor(source);
            if (claim is not null)
            {
                source.ClaimLevel = claim;
                source.Label = SafeSourceLabel(source.Label, claim);
            }
        }
    }

    private static void AddCatalogUnmatchedGaps(IReadOnlyList<DocSource> sources, SourceClaimCatalog catalog, IReadOnlyList<string> selectedFamilies, List<EvidenceDocChunk> chunks, List<EvidenceDocsDiagnostic> diagnostics)
    {
        if (!selectedFamilies.Contains("gap", StringComparer.Ordinal))
        {
            return;
        }

        foreach (var unmatched in catalog.Unmatched(sources))
        {
            chunks.Add(CreateGapChunk($"claim-unmatched-{Hash(unmatched, 16)}", ClaimUnmatchedRuleId, "claim-level-unmatched", "gap", sources, ["source-claim-catalog"], "hidden"));
            diagnostics.Add(CreateDiagnostic("InputClaimCatalogUnmatched", ClaimUnmatchedRuleId, "/sourceClaimCatalog/entries", "claim-level", "source-claim-catalog"));
        }
    }

    private static void AddRequestedUnsupportedFamilyGaps(IReadOnlyList<DocSource> sources, IReadOnlyList<string> selectedFamilies, List<EvidenceDocChunk> chunks)
    {
        if (!selectedFamilies.Contains("gap", StringComparer.Ordinal))
        {
            return;
        }

        foreach (var family in selectedFamilies)
        {
            if (family is "gap" or "limitation")
            {
                continue;
            }

            if (chunks.Any(chunk => chunk.ChunkFamily == family || chunk.Gaps.Any(gap => gap.ChunkFamily == family)))
            {
                continue;
            }

            chunks.Add(CreateGapChunk($"unsupported-{family}", UnsupportedFamilyRuleId, "unsupported-family", family, sources, [$"family:{family}"], "hidden"));
        }
    }

    private static IReadOnlyList<EvidenceDocGap> GapsForFact(DocFact fact, string family)
    {
        var gaps = new List<EvidenceDocGap>();
        if (fact.CommitSha is null or "unknown")
        {
            gaps.Add(new EvidenceDocGap(
                StableId("gap", "docs-export/gap/v1", [new("reason", "unknown-commit-sha"), new("fact", fact.FactId)]),
                UnknownAnalysisRuleId,
                Tier4Unknown,
                "unknown-commit-sha",
                family,
                [ToSourceRef(fact.Source)],
                [fact.FactId],
                ["Unknown commit identity prevents strong public provenance."]));
        }

        if (fact.FilePath is null || fact.StartLine is null || fact.EndLine is null)
        {
            gaps.Add(new EvidenceDocGap(
                StableId("gap", "docs-export/gap/v1", [new("reason", "missing-provenance"), new("fact", fact.FactId)]),
                MissingProvenanceRuleId,
                Tier4Unknown,
                "missing-provenance",
                family,
                [ToSourceRef(fact.Source)],
                [fact.FactId],
                ["Missing file or line-span evidence prevents span-level citation."]));
        }

        if (fact.Source.CoverageLabel.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
            || fact.Source.BuildStatus.Contains("Failed", StringComparison.OrdinalIgnoreCase)
            || fact.EvidenceTier == Tier4Unknown)
        {
            gaps.Add(new EvidenceDocGap(
                StableId("gap", "docs-export/gap/v1", [new("reason", "reduced-coverage"), new("fact", fact.FactId)]),
                UnknownAnalysisRuleId,
                Tier4Unknown,
                "reduced-coverage",
                family,
                [ToSourceRef(fact.Source)],
                [fact.FactId],
                ["Reduced or unknown coverage means absence-like conclusions must be treated as unknown."]));
        }

        return gaps;
    }

    private static IReadOnlyList<EvidenceDocLimitation> BuildManifestLimitations(IReadOnlyList<EvidenceDocChunk> chunks, IReadOnlyList<string> selectedFamilies)
    {
        var limitations = new List<EvidenceDocLimitation>
        {
            new(
                StableId("limitation", "docs-export/limitation/v1", [new("ruleId", "docs-export.boundary")]),
                LimitationChunkRuleId,
                Tier4Unknown,
                "Docs export packages existing deterministic evidence for external ingestion and does not call models, generate embeddings, write vector databases, rank retrieval results, or answer natural-language questions.",
                "limitation",
                ["docs-export"])
        };
        limitations.AddRange(chunks.SelectMany(chunk => chunk.Limitations));
        return limitations
            .GroupBy(limitation => limitation.LimitationId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(limitation => limitation.LimitationId, StringComparer.Ordinal)
            .ToArray();
    }

    private static EvidenceDocLimitation LimitationForFamily(string family, IReadOnlyList<string> supportingIds)
    {
        var ruleId = PackagingRuleForFamily(family);
        return new EvidenceDocLimitation(
            StableId("limitation", "docs-export/limitation/v1", [new("family", family), new("ids", string.Join('|', supportingIds))]),
            ruleId,
            family is "source-overview" ? EvidenceTiers.Tier2Structural : Tier4Unknown,
            family switch
            {
                "property-flow" => "Property-flow chunks preserve static value-origin evidence and do not prove full taint, runtime values, object identity, or collection contents.",
                "route-flow" => "Route-flow chunks preserve static path evidence and do not prove runtime request execution, branch feasibility, dependency-injection target selection, traffic, or database execution.",
                "release-review" => "Release-review chunks preserve static report findings and checklist provenance; they are not release approval.",
                "impact-summary" => "Impact-summary chunks preserve static reducer/report classifications and do not claim business impact.",
                _ => "Docs-export chunks preserve static evidence only and do not prove runtime behavior, deployment, release approval, vulnerabilities, ownership, service reachability, production traffic, or business impact."
            },
            family,
            supportingIds);
    }

    private static EvidenceDocCitation CreateFactCitation(DocFact fact)
    {
        return new EvidenceDocCitation(
            StableId("citation", "docs-export/citation/v1", [new("fact", fact.FactId), new("ruleId", fact.RuleId)]),
            fact.Source.Label,
            fact.Source.Scope,
            fact.ScanId ?? fact.Source.ScanId,
            fact.CommitSha ?? fact.Source.CommitSha,
            fact.Source.CoverageLabel,
            fact.FilePath,
            fact.StartLine,
            fact.EndLine,
            [fact.RuleId],
            fact.EvidenceTier,
            ExtractorName(fact),
            fact.Source.ExtractorVersion,
            [fact.FactId],
            [],
            []);
    }

    private static EvidenceDocCitation CreateReportCitation(string reportId, string reportType, IReadOnlyList<DocSource> sources)
    {
        var source = sources.OrderBy(source => source.Label, StringComparer.Ordinal).FirstOrDefault();
        return new EvidenceDocCitation(
            StableId("citation", "docs-export/citation/v1", [new("report", reportId), new("type", reportType)]),
            source?.Label,
            source?.Scope ?? "report",
            source?.ScanId,
            source?.CommitSha,
            source?.CoverageLabel ?? "report-input",
            null,
            null,
            null,
            [RouteFlowRuleId],
            EvidenceTiers.Tier2Structural,
            "report-json",
            SchemaVersion,
            [],
            [],
            [reportId]);
    }

    private static EvidenceDocSourceRef ToSourceRef(DocSource source)
    {
        return new EvidenceDocSourceRef(
            source.SourceId,
            source.Label,
            source.Scope,
            source.ScanId,
            source.CommitSha,
            source.CoverageLabel,
            "index-metadata",
            source.ExtractorVersion,
            [SourceOverviewRuleId],
            [EvidenceTiers.Tier2Structural]);
    }

    private static EvidenceDocsDiagnostic CreateDiagnostic(string code, string ruleId, string location, string category, string supportingId)
    {
        return new EvidenceDocsDiagnostic(
            code,
            ruleId,
            Tier4Unknown,
            location,
            category,
            "unknown",
            null,
            null,
            null,
            SchemaVersion,
            [supportingId]);
    }

    private static string DisplayCommitSha(string? commitSha)
    {
        return string.IsNullOrWhiteSpace(commitSha) ? "not-recorded" : commitSha;
    }

    private static string FamilyForFact(DocFact fact)
    {
        var type = fact.FactType;
        if (type == FactTypes.AnalysisGap || type.EndsWith("Gap", StringComparison.Ordinal))
        {
            return "gap";
        }

        if (type.StartsWith("Access", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.access.", StringComparison.Ordinal))
        {
            return "legacy";
        }

        if (IsPotentialLegacyDataDescriptor(fact) && TryProjectLegacyDataDescriptor(fact) is not null)
        {
            return "data-surface";
        }

        if (type is FactTypes.HttpRouteBinding or FactTypes.HttpCallDetected or FactTypes.HttpClientCreated or FactTypes.RazorFormTarget)
        {
            return "endpoint";
        }

        if (type is FactTypes.ArgumentPassed or FactTypes.LocalAlias or FactTypes.FieldAlias or FactTypes.ParameterDeclared
            or FactTypes.PropertyAccessed or FactTypes.MemberAccessName or FactTypes.SerializerContractMember
            or FactTypes.CallbackBoundary or FactTypes.AsyncBoundary or FactTypes.CollectionElementFlow
            or FactTypes.MutationSemantics or FactTypes.BranchFeasibility)
        {
            return "property-flow";
        }

        if (type is FactTypes.PackageReferenced or FactTypes.ConfigFileDeclared or FactTypes.ConfigKeyDeclared
            or FactTypes.ConnectionStringDeclared or FactTypes.TargetFrameworkDeclared)
        {
            return "package-config";
        }

        if (type.Contains("Sql", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Query", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Database", StringComparison.OrdinalIgnoreCase)
            || type.Contains("Db", StringComparison.Ordinal))
        {
            return "query-sql-shape";
        }

        if (type.Contains("Wcf", StringComparison.Ordinal)
            || type.Contains("Asmx", StringComparison.Ordinal)
            || type.Contains("Remoting", StringComparison.Ordinal)
            || type.Contains("WebForms", StringComparison.Ordinal)
            || type.Contains("WinForms", StringComparison.Ordinal)
            || type.Contains("Legacy", StringComparison.Ordinal))
        {
            return "legacy";
        }

        if (type is FactTypes.FileInventoried or FactTypes.SolutionDeclared or FactTypes.ProjectDeclared
            or FactTypes.BuildEnvironmentDiagnostic or FactTypes.BuildStatus)
        {
            return "data-surface";
        }

        return "dependency-surface";
    }

    private static LegacyDataModelDescriptorProjectionRow? TryProjectLegacyDataDescriptor(DocFact fact)
    {
        if (!IsPotentialLegacyDataDescriptor(fact)
            || fact.FilePath is null
            || fact.StartLine is null
            || fact.EndLine is null)
        {
            return null;
        }

        return LegacyDataModelDescriptorProjection.TryProject(
            new CombinedSurfaceFactInput(
                fact.FactId,
                fact.Source.SourceId,
                fact.Source.Label,
                fact.OriginalFactId ?? fact.FactId,
                fact.ScanId ?? fact.Source.ScanId ?? "unknown",
                fact.CommitSha ?? fact.Source.CommitSha ?? "unknown",
                fact.FactType,
                fact.RuleId,
                fact.EvidenceTier,
                fact.FilePath,
                fact.StartLine.Value,
                fact.EndLine.Value,
                fact.Properties,
                fact.Source.ExtractorVersion),
            new LegacyDataModelDescriptorProjectionOptions(
                AllowClearDisplayLabels: false,
                ClaimLevelContextId: $"docs-export:{fact.Source.ClaimLevel}"));
    }

    private static bool IsPotentialLegacyDataDescriptor(DocFact fact)
    {
        return fact.FactType.StartsWith("LegacyData", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("legacy.data.", StringComparison.Ordinal);
    }

    private static string PackagingRuleForFamily(string family)
    {
        return family switch
        {
            "source-overview" => SourceOverviewRuleId,
            "endpoint" => EndpointRuleId,
            "route-flow" => RouteFlowRuleId,
            "property-flow" => PropertyFlowRuleId,
            "dependency-surface" => DependencySurfaceRuleId,
            "data-surface" => DataSurfaceRuleId,
            "package-config" => PackageConfigRuleId,
            "query-sql-shape" => QuerySqlShapeRuleId,
            "legacy" => LegacyRuleId,
            "release-review" => ReleaseReviewRuleId,
            "impact-summary" => ImpactSummaryRuleId,
            "gap" => GapChunkRuleId,
            "limitation" => LimitationChunkRuleId,
            _ => UnsupportedFamilyRuleId
        };
    }

    private static string TitleForFamily(string family)
    {
        return string.Join(' ', family.Split('-').Select(part => char.ToUpperInvariant(part[0]) + part[1..]));
    }

    private static string BuildReportBody(string inputKind, string family, string reportType, string reportId)
    {
        return $"""
            ## {TitleForFamily(family)} report

            This chunk records that a deterministic `{EscapeInline(inputKind)}` artifact with schema or type `{EscapeInline(reportType)}` was supplied.

            | Field | Value |
            | --- | --- |
            | Report ID | `{EscapeInline(reportId)}` |
            | Input kind | `{EscapeInline(inputKind)}` |
            | Chunk family | `{EscapeInline(family)}` |

            Docs export preserves the report as static evidence metadata and does not reinterpret report findings.
            """;
    }

    private static string BuildPropertyFlowReportBody(string reportType, string reportId, IReadOnlyList<PropertyFlowTerminalContextProjection> terminalContexts)
    {
        var terminalContextText = terminalContexts.Count == 0
            ? "not supplied"
            : string.Join("; ", terminalContexts.Select(context =>
                $"path:{context.PathLabel} node:{context.NodeLabel} kind:{context.TerminalContextKind}"));
        return $"""
            ## {TitleForFamily("property-flow")} report

            This chunk records that a deterministic `property-flow-report` artifact with schema or type `{EscapeInline(reportType)}` was supplied.

            | Field | Value |
            | --- | --- |
            | Report ID | `{EscapeInline(reportId)}` |
            | Input kind | `property-flow-report` |
            | Chunk family | `property-flow` |
            | Terminal contexts | `{EscapeInline(terminalContextText)}` |

            Docs export preserves the report as static evidence metadata and does not reinterpret report findings, parse note prose, infer missing terminal context, or add stronger execution, impact, or coverage conclusions.
            """;
    }

    private static IReadOnlyList<PropertyFlowTerminalContextProjection> ExtractPropertyFlowTerminalContexts(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("lineagePaths", out var paths)
            || paths.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var contexts = new List<PropertyFlowTerminalContextProjection>();
        var pathOrdinal = 0;
        foreach (var path in paths.EnumerateArray())
        {
            pathOrdinal++;
            if (path.ValueKind != JsonValueKind.Object
                || !path.TryGetProperty("nodes", out var nodes)
                || nodes.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var pathId = SafeTokenOrHash(StringProperty(path, "pathId") ?? $"path-{pathOrdinal}");
            var nodeOrdinal = 0;
            foreach (var node in nodes.EnumerateArray())
            {
                nodeOrdinal++;
                if (node.ValueKind != JsonValueKind.Object
                    || !node.TryGetProperty("safeMetadata", out var safeMetadata)
                    || safeMetadata.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var terminalContextKind = StringProperty(safeMetadata, TerminalContextKindMetadataKey);
                if (string.IsNullOrWhiteSpace(terminalContextKind))
                {
                    continue;
                }

                var nodeId = SafeTokenOrHash(StringProperty(node, "nodeId") ?? $"node-{nodeOrdinal}");
                var nodeKind = SafeTokenOrHash(StringProperty(node, "nodeKind") ?? "unknown");
                var displayValue = IsSafeMetadataValue(terminalContextKind)
                    ? terminalContextKind
                    : $"redacted-{Hash(terminalContextKind, 12)}";
                var supportingId = $"property-flow-terminal-context:{Hash($"{pathId}|{nodeId}|{displayValue}", 16)}";
                contexts.Add(new PropertyFlowTerminalContextProjection(pathId, $"{nodeId}/{nodeKind}", displayValue, supportingId));
            }
        }

        return contexts
            .OrderBy(context => context.PathLabel, StringComparer.Ordinal)
            .ThenBy(context => context.NodeLabel, StringComparer.Ordinal)
            .ThenBy(context => context.TerminalContextKind, StringComparer.Ordinal)
            .ToArray();
    }

    private static string SafeFactMetadata(DocFact fact)
    {
        var parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(fact.ContractElement))
        {
            parts.Add($"contract:{SafeTokenOrHash(fact.ContractElement)}");
        }

        if (!string.IsNullOrWhiteSpace(fact.SourceSymbol))
        {
            parts.Add($"source-symbol:{SafeTokenOrHash(fact.SourceSymbol)}");
        }

        if (!string.IsNullOrWhiteSpace(fact.TargetSymbol))
        {
            parts.Add($"target-symbol:{SafeTokenOrHash(fact.TargetSymbol)}");
        }

        if (fact.Properties.TryGetValue(TerminalContextKindMetadataKey, out var terminalContextKind)
            && !string.IsNullOrWhiteSpace(terminalContextKind))
        {
            parts.Add(IsSafeMetadataValue(terminalContextKind)
                ? $"{TerminalContextKindMetadataKey}:{terminalContextKind}"
                : $"{TerminalContextKindMetadataKey}:redacted-{Hash(terminalContextKind, 12)}");
        }

        foreach (var pair in fact.Properties
            .Where(pair => !string.Equals(pair.Key, TerminalContextKindMetadataKey, StringComparison.Ordinal))
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Take(4))
        {
            if (IsSafeMetadataKey(pair.Key) && IsSafeMetadataValue(pair.Value))
            {
                parts.Add($"{pair.Key}:{pair.Value}");
            }
            else if (IsSafeMetadataKey(pair.Key))
            {
                parts.Add($"{pair.Key}:redacted-{Hash(pair.Value, 12)}");
            }
        }

        return parts.Count == 0 ? "none" : string.Join("; ", parts.OrderBy(value => value, StringComparer.Ordinal));
    }

    private static bool IsSafeMetadataKey(string value)
    {
        return value.Length <= 64 && SafeMetadataKeyPattern.IsMatch(value);
    }

    private static bool IsSafeMetadataValue(string value)
    {
        return value.Length <= 96
            && SafeClosedTextPattern.IsMatch(value)
            && UnsafeCategory(value) is null
            && !value.Contains("select ", StringComparison.OrdinalIgnoreCase)
            && !value.Contains(" from ", StringComparison.OrdinalIgnoreCase);
    }

    private static string SafeTokenOrHash(string value)
    {
        return IsSafeMetadataValue(value) ? value : $"hash:{Hash(value, 16)}";
    }

    private static Dictionary<string, string> SafeProperties(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        try
        {
            var values = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions) ?? [];
            foreach (var value in values.Values)
            {
                var category = UnsafeCategory(value);
                if (category is "raw-sql" or "credential-or-config" or "credential")
                {
                    throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {category} at input.properties.");
                }
            }

            return values
                .Where(pair => IsSafeMetadataKey(pair.Key))
                .ToDictionary(pair => pair.Key, pair => pair.Value ?? string.Empty, StringComparer.Ordinal);
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string? ExtractorName(DocFact fact)
    {
        if (fact.Properties.TryGetValue("extractorId", out var extractor) && IsSafeMetadataValue(extractor))
        {
            return extractor;
        }

        var rule = fact.RuleId.Split('.', StringSplitOptions.RemoveEmptyEntries);
        return rule.Length >= 2 ? $"{rule[0]}.{rule[1]}" : "unknown";
    }

    private static string FormatSpan(DocFact fact)
    {
        return fact.FilePath is null ? "unknown" : $"{fact.FilePath}:{fact.StartLine?.ToString() ?? "unknown"}-{fact.EndLine?.ToString() ?? "unknown"}";
    }

    private static string IndexIdentity(IndexInput input)
    {
        return string.Join('|', input.Sources.Select(source => $"{source.SourceId}:{source.ScanId}:{source.CommitSha}:{source.CoverageLabel}").OrderBy(value => value, StringComparer.Ordinal));
    }

    private static string MinClaim(IEnumerable<string> claims)
    {
        return claims.OrderBy(ClaimRank).FirstOrDefault() ?? "hidden";
    }

    private static int ClaimRank(string claim)
    {
        return claim switch
        {
            "hidden" => 0,
            "demo-safe" => 1,
            "public-safe" => 2,
            _ => 0
        };
    }

    private static string? CommitOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Hex40Pattern.IsMatch(value) ? value.ToLowerInvariant() : null;
    }

    private static string SafeSourceLabel(string value, string claimLevel)
    {
        if (claimLevel == "hidden")
        {
            return $"hidden-source-{Hash(value, 12)}";
        }

        return IsSafeMetadataValue(value) ? value : $"source-{Hash(value, 12)}";
    }

    private static string? SafeRelativePathOrNull(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Replace('\\', '/');
        if (Path.IsPathRooted(normalized) || normalized.Contains("..", StringComparison.Ordinal) || !SafePathPattern.IsMatch(normalized) || UnsafeCategory(normalized) is not null)
        {
            return null;
        }

        return normalized;
    }

    private static bool SafeRelativeOutputPath(string value)
    {
        return SafeRelativePathOrNull(value) == value && !value.StartsWith("/", StringComparison.Ordinal);
    }

    private static string? UnsafeCategory(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return null;
        }

        if (UnixLocalPathPattern.IsMatch(value) || WindowsPathPattern.IsMatch(value))
        {
            return "local-absolute-path";
        }

        if (value.Contains("git@", StringComparison.OrdinalIgnoreCase)
            || value.Contains("://", StringComparison.OrdinalIgnoreCase)
            || RawHostPattern.IsMatch(value))
        {
            return "raw-remote-or-url";
        }

        if (RawSqlPattern.IsMatch(value))
        {
            return "raw-sql";
        }

        if (ConfigSecretPattern.IsMatch(value))
        {
            return "credential-or-config";
        }

        if (CredentialPattern.IsMatch(value))
        {
            return "credential";
        }

        if (value.Contains("System.", StringComparison.Ordinal) && value.Contains("Exception", StringComparison.Ordinal))
        {
            return "stack-trace";
        }

        if (value.Contains("<script", StringComparison.OrdinalIgnoreCase)
            || value.Contains("javascript:", StringComparison.OrdinalIgnoreCase))
        {
            return "unsafe-markdown";
        }

        return null;
    }

    private static bool ContainsProhibitedClaim(string content)
    {
        var prohibited = new[]
        {
            "release approved",
            "safe to release",
            "handles production traffic",
            "observed production traffic",
            "is vulnerable",
            "owned by",
            "deployed to",
            "service is reachable",
            "business impact is"
        };
        return prohibited.Any(term => content.Contains(term, StringComparison.OrdinalIgnoreCase));
    }

    private static string EscapeInline(string value)
    {
        return EscapeText(value).Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static string EscapeHeading(string value)
    {
        return EscapeText(value).Replace("#", "\\#", StringComparison.Ordinal);
    }

    private static string EscapeText(string value)
    {
        return value.Replace("|", "\\|", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);
    }

    private static string YamlScalar(string value)
    {
        return SafeClosedTextPattern.IsMatch(value) ? value : JsonSerializer.Serialize(value);
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder();
        foreach (var character in value.ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(character) ? character : '-');
        }

        return RepeatedDashPattern.Replace(builder.ToString(), "-").Trim('-');
    }

    private static IReadOnlyList<EvidenceDocChunk> SortChunks(IEnumerable<EvidenceDocChunk> chunks)
    {
        return chunks
            .OrderBy(chunk => chunk.SortKey, StringComparer.Ordinal)
            .ThenBy(chunk => chunk.ChunkId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string StableId(string prefix, string context, IReadOnlyList<KeyValuePair<string, string?>> fields)
    {
        var record = context + "\n" + StableIdInputRecord(fields);
        return $"{prefix}:{Hash(record, IdHashLength)}";
    }

    private static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes).ToLowerInvariant()[..length];
    }

    private static bool IsHash(string? value, int length)
    {
        return value is not null && value.Length == length && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static string SerializeJson(object value)
    {
        return NormalizeLineEndings(JsonSerializer.Serialize(value, JsonOptions)) + "\n";
    }

    private static string SerializeJsonNode(JsonNode node)
    {
        return NormalizeLineEndings(node.ToJsonString(JsonOptions)) + "\n";
    }

    private static string NormalizeLineEndings(string value)
    {
        var normalized = value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
        return normalized.EndsWith('\n') ? normalized : normalized + "\n";
    }

    private static string NormalizeMarkdownBody(string markdown)
    {
        return NormalizeLineEndings(markdown).TrimEnd() + "\n";
    }

    private static string NormalizeMarkdownHashInput(string content)
    {
        var normalized = NormalizeLineEndings(content);
        return ContentHashLinePattern.Replace(normalized, "tracemap_content_sha256: ");
    }

    private static bool TryReadFrontmatter(string content, out Dictionary<string, string> metadata, out string body)
    {
        metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        body = content;
        var normalized = NormalizeLineEndings(content);
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var yaml = normalized[4..end];
        body = normalized[(end + 5)..];
        foreach (var line in yaml.Split('\n'))
        {
            var separator = line.IndexOf(':', StringComparison.Ordinal);
            if (separator > 0 && !line.StartsWith("  - ", StringComparison.Ordinal))
            {
                metadata[line[..separator]] = line[(separator + 1)..].Trim();
            }
        }

        return true;
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type in ('table', 'view') and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(value) > 0;
    }

    private static string? StringOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? IntOrNull(SqliteDataReader reader, int ordinal)
    {
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static string? StringProperty(JsonElement element, string propertyName)
    {
        return element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;
    }

    private static string InferLanguage(string? scannerVersion)
    {
        if (string.IsNullOrWhiteSpace(scannerVersion))
        {
            return "unknown";
        }

        if (scannerVersion.Contains("typescript", StringComparison.OrdinalIgnoreCase))
        {
            return "typescript";
        }

        if (scannerVersion.Contains("python", StringComparison.OrdinalIgnoreCase))
        {
            return "python";
        }

        if (scannerVersion.Contains("jvm", StringComparison.OrdinalIgnoreCase))
        {
            return "jvm";
        }

        if (scannerVersion.Contains("swift", StringComparison.OrdinalIgnoreCase))
        {
            return "swift";
        }

        return "csharp";
    }

    private sealed record IndexInput(
        string Kind,
        IReadOnlyList<DocSource> Sources,
        IReadOnlyList<DocFact> Facts);

    private sealed record DocFact(
        string FactId,
        string? OriginalFactId,
        DocSource Source,
        string? ScanId,
        string? CommitSha,
        string FactType,
        string RuleId,
        string EvidenceTier,
        string? SourceSymbol,
        string? TargetSymbol,
        string? ContractElement,
        string? FilePath,
        int? StartLine,
        int? EndLine,
        IReadOnlyDictionary<string, string> Properties);

    private sealed record PropertyFlowTerminalContextProjection(
        string PathLabel,
        string NodeLabel,
        string TerminalContextKind,
        string SupportingId);

    private sealed class DocSource(
        string sourceId,
        string label,
        string scope,
        string? scanId,
        string? commitSha,
        string language,
        string? extractorVersion,
        string coverageLabel,
        string buildStatus,
        string claimLevel)
    {
        public string SourceId { get; } = sourceId;
        public string Label { get; set; } = label;
        public string Scope { get; } = scope;
        public string? ScanId { get; } = scanId;
        public string? CommitSha { get; } = commitSha;
        public string Language { get; } = language;
        public string? ExtractorVersion { get; } = extractorVersion;
        public string CoverageLabel { get; } = coverageLabel;
        public string BuildStatus { get; } = buildStatus;
        public string ClaimLevel { get; set; } = claimLevel;
    }

    private sealed record SourceClaim(
        string? SourceIndexId,
        string? ScanId,
        string? CommitSha,
        string ClaimLevel);

    private sealed class SourceClaimCatalog(IReadOnlyList<SourceClaim> claims)
    {
        public string? ClaimFor(DocSource source)
        {
            return claims.FirstOrDefault(claim =>
                !string.IsNullOrWhiteSpace(claim.SourceIndexId) && claim.SourceIndexId == source.SourceId
                || !string.IsNullOrWhiteSpace(claim.ScanId) && claim.ScanId == source.ScanId
                || !string.IsNullOrWhiteSpace(claim.CommitSha) && claim.CommitSha != "unknown" && claim.CommitSha == source.CommitSha)?.ClaimLevel;
        }

        public IReadOnlyList<string> Unmatched(IReadOnlyList<DocSource> sources)
        {
            return claims
                .Where(claim => sources.All(source =>
                    claim.SourceIndexId != source.SourceId
                    && claim.ScanId != source.ScanId
                    && (claim.CommitSha is null or "unknown" || claim.CommitSha != source.CommitSha)))
                .Select(claim => claim.SourceIndexId ?? claim.ScanId ?? claim.CommitSha ?? "unknown")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
        }
    }
}
