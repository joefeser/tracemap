using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record VaultExportOptions(
    string? CombinedIndexPath,
    string OutputPath,
    IReadOnlyList<string>? PathsReportPaths = null,
    IReadOnlyList<string>? ReverseReportPaths = null,
    string? SourceClaimCatalogPath = null,
    string? MinimumClaimLevel = null,
    string? Date = null,
    string Format = "markdown,json",
    bool DryRun = false,
    bool Force = false);

public sealed record VaultExportResult(
    EvidenceGraphVault Graph,
    IReadOnlyList<string> PlannedFiles,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<VaultExportDiagnostic> Diagnostics);

public sealed record VaultExportDiagnostic(
    string Code,
    string RuleId,
    string Location,
    string Category);

public sealed record VaultSourceProvenance(
    string SourceIndexId,
    string SourceIdentityHash,
    string? ScannerVersion,
    string? CommitSha,
    string? Language,
    string? AnalysisLevel,
    string? BuildStatus);

public sealed record VaultEvidenceLocation(
    string FilePath,
    int? StartLine,
    int? EndLine,
    string? SnippetHash);

public sealed record EvidenceGraphVault(
    string SchemaVersion,
    string ContentHash,
    VaultExportGenerator Generator,
    string Classification,
    IReadOnlyList<VaultExportInputSummary> Inputs,
    IReadOnlyList<VaultGraphNode> Nodes,
    IReadOnlyList<VaultGraphEdge> Edges,
    IReadOnlyList<VaultGraphGap> Gaps,
    IReadOnlyList<VaultGraphLimitation> Limitations,
    VaultExportSettings Settings);

public sealed record VaultExportGenerator(
    string Name,
    string Version,
    string GeneratedAt);

public sealed record VaultExportInputSummary(
    string Kind,
    string Identity,
    string ClaimLevel,
    string Compatibility,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<VaultSourceProvenance>? SourceProvenance = null);

public sealed record VaultGraphNode(
    string Id,
    string Kind,
    string ClaimLevel,
    string DisplayName,
    string? SourceId,
    string? SourceLabel,
    string? SourceScope,
    string? SurfaceKind,
    string? CommitSha,
    string? Language,
    string? AnalysisLevel,
    string? BuildStatus,
    IReadOnlyList<string> Coverage,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Limitations,
    string FilePath,
    string? ScannerVersion = null,
    string? RepositoryIdentityHash = null,
    IReadOnlyList<VaultEvidenceLocation>? EvidenceLocations = null,
    string? NavigationCategory = null);

public sealed record VaultGraphEdge(
    string Id,
    string Kind,
    string From,
    string To,
    string ClaimLevel,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string? SourceScope,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<VaultEvidenceLocation>? EvidenceLocations = null,
    string? NavigationCategory = null);

public sealed record VaultGraphGap(
    string Id,
    string ClaimLevel,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    string? SourceScope,
    IReadOnlyList<string> Limitations);

public sealed record VaultGraphLimitation(
    string Id,
    string ClaimLevel,
    string RuleId,
    string EvidenceTier,
    string Message);

public sealed record VaultExportSettings(
    string MinimumClaimLevel,
    IReadOnlyList<string> Formats,
    bool Partial,
    int OmittedHiddenNodeCount,
    int OmittedHiddenEdgeCount);

public static class VaultExporter
{
    public const string SchemaVersion = "evidence-graph-vault-export.v1";
    private const string GeneratorName = "tracemap-vault-export";
    private const string GeneratorVersion = SchemaVersion;
    private const int IdHashLength = 24;
    private const string SchemaGapRuleId = "vault-export.gap.schema-incompatible.v1";
    private const string ClaimHiddenRuleId = "vault-export.gap.claim-level-hidden.v1";
    private const string ClaimUnmatchedRuleId = "vault-export.gap.claim-level-unmatched.v1";
    private const string HiddenOmittedRuleId = "vault-export.gap.hidden-evidence-omitted.v1";
    private const string UnsafeSymbolRuleId = "vault-export.gap.unsafe-symbol-omitted.v1";
    private const string HiddenSafeContextRuleId = "vault-export.gap.hidden-safe-context-omitted.v1";
    private const string UnsafeIdComponentRuleId = "vault-export.gap.unsafe-id-component-omitted.v1";
    private const string GeneratedFileStaleRuleId = "vault-export.validation.generated-file-stale.v1";
    private const string UserFileCollisionRuleId = "vault-export.validation.user-file-collision.v1";
    private const string UnsafeRejectedRuleId = "vault-export.validation.unsafe-value-rejected.v1";
    private const string SensitiveWordCategory = "sensitive-word";
    private const string Tier4Unknown = "Tier4Unknown";
    // Hidden-safe context hashes use lowercase SHA-256 truncated after context validation.
    private const int DisplayNameHashLength = 24;
    private const int RepoRelativePathHashLength = 24;
    private const int EvidenceLocationHashLength = 32;
    // Display text is local navigation metadata, not source proof.
    private const int MaxDisplayNameLength = 256;
    private const int MaxRepoRelativePathLength = 260;

    private enum VaultValueContext
    {
        RepoRelativePath,
        GeneratedMetadata,
        EvidenceLocation,
        SymbolDisplayName,
        RouteActionModelMemberName,
        StableTraceMapId,
        RuleId,
        ClosedVocabulary,
        DiagnosticCategory,
        RawExternalOrDataValue,
        MarkdownEvidenceLocation
    }

    private enum VaultSafetyOutcome
    {
        AllowRaw,
        AllowHash,
        AllowCategory,
        OmitWithGap,
        Reject
    }

    private sealed record VaultSafetyDecision(VaultSafetyOutcome Outcome, string? Category);

    static VaultExporter()
    {
        PreValidateClosedVocabulary([
            SchemaVersion,
            GeneratorName,
            ClaimHiddenRuleId,
            ClaimUnmatchedRuleId,
            HiddenOmittedRuleId,
            UnsafeSymbolRuleId,
            HiddenSafeContextRuleId,
            UnsafeIdComponentRuleId,
            GeneratedFileStaleRuleId,
            UserFileCollisionRuleId,
            UnsafeRejectedRuleId,
            "hidden",
            "demo-safe",
            "public-safe",
            "local-path",
            "raw-remote-or-url",
            "raw-sql",
            "connection-string",
            "credential",
            SensitiveWordCategory,
            "unsafe-evidence-location",
            "unsafe-id-component",
            "unsafe-display-name",
            "empty-value",
            "hidden-display-name-invalid",
            "UnsafeIdComponentOmitted",
            "HiddenSafeContextAccepted",
            Tier4Unknown
        ]);
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true
    };

    private static readonly string[] FrontmatterKeyOrder =
    [
        "tracemap_generated",
        "tracemap_export_schema",
        "tracemap_generator",
        "tracemap_content_sha256",
        "tracemap_kind",
        "claim_level",
        "source_id",
        "rule_ids",
        "evidence_tiers",
        "coverage",
        "limitations",
        "aliases",
        "tags"
    ];

    public static async Task<VaultExportResult> ExportAsync(VaultExportOptions options, CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        var formats = NormalizeFormats(options.Format);
        var minimumClaimLevel = NormalizeClaimLevel(options.MinimumClaimLevel ?? "hidden", "--minimum-claim-level");
        var generatedAt = ResolveGeneratedAt(options.Date, minimumClaimLevel);
        var catalog = await ReadClaimCatalogAsync(options.SourceClaimCatalogPath, cancellationToken);
        var diagnostics = new List<VaultExportDiagnostic>();
        var graph = await BuildGraphAsync(options, formats, minimumClaimLevel, generatedAt, catalog, diagnostics, cancellationToken);
        graph = WithNavigationCategories(graph);
        graph = WithGraphHash(graph);

        var files = BuildGeneratedFiles(options.OutputPath, graph, formats);
        ValidateGeneratedStrings(graph, files);
        ValidateExistingFiles(files, options.Force);

        if (!options.DryRun)
        {
            foreach (var file in files.OrderBy(file => file.Key, StringComparer.Ordinal))
            {
                var directory = Path.GetDirectoryName(file.Key);
                if (!string.IsNullOrWhiteSpace(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                await File.WriteAllTextAsync(file.Key, file.Value, new UTF8Encoding(false), cancellationToken);
            }
        }

        return new VaultExportResult(
            graph,
            files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            options.DryRun ? [] : files.Keys.OrderBy(path => path, StringComparer.Ordinal).ToArray(),
            diagnostics.OrderBy(diagnostic => diagnostic.Code, StringComparer.Ordinal)
                .ThenBy(diagnostic => diagnostic.Location, StringComparer.Ordinal)
                .ToArray());
    }

    public static bool IsSelfConsistentGraphJson(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("schemaVersion", out var schema)
                || schema.GetString() != SchemaVersion
                || !document.RootElement.TryGetProperty("generator", out var generator)
                || !generator.TryGetProperty("name", out var name)
                || name.GetString() != GeneratorName
                || !document.RootElement.TryGetProperty("contentHash", out var hash))
            {
                return false;
            }

            var expected = hash.GetString();
            if (!IsHash(expected))
            {
                return false;
            }

            var node = JsonNode.Parse(content) as JsonObject;
            if (node is null)
            {
                return false;
            }

            node["contentHash"] = string.Empty;
            var canonical = SerializeCanonicalJson(node);
            return string.Equals(expected, Hash(canonical, 64), StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    public static bool IsSelfConsistentMarkdown(string content)
    {
        if (!TryReadFrontmatter(content, out var metadata, out _)
            || !metadata.TryGetValue("tracemap_export_schema", out var schema)
            || schema != SchemaVersion
            || !metadata.TryGetValue("tracemap_generator", out var generator)
            || generator != GeneratorName
            || !metadata.TryGetValue("tracemap_content_sha256", out var hash)
            || !IsHash(hash))
        {
            return false;
        }

        return string.Equals(hash, Hash(NormalizeMarkdownHashInput(content), 64), StringComparison.Ordinal);
    }

    private static async Task<EvidenceGraphVault> BuildGraphAsync(
        VaultExportOptions options,
        IReadOnlyList<string> formats,
        string minimumClaimLevel,
        string generatedAt,
        SourceClaimCatalog catalog,
        List<VaultExportDiagnostic> diagnostics,
        CancellationToken cancellationToken)
    {
        var inputs = new List<VaultExportInputSummary>();
        var nodes = new List<VaultGraphNode>();
        var edges = new List<VaultGraphEdge>();
        var gaps = new List<VaultGraphGap>();
        var limitations = new List<VaultGraphLimitation>();
        var originalNodeClaims = new Dictionary<string, string>(StringComparer.Ordinal);
        var nodeIdByPathNodeId = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceNodeIdBySourceIndexId = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceClaimBySourceIndexId = new Dictionary<string, string>(StringComparer.Ordinal);
        var safetyOmittedNodeCount = 0;
        var safetyOmittedEdgeCount = 0;
        var compatibleInputCount = 0;

        if (!string.IsNullOrWhiteSpace(options.CombinedIndexPath))
        {
            var inventory = await CombinedDependencyPathReporter.BuildGraphInventoryAsync(options.CombinedIndexPath, cancellationToken: cancellationToken);
            compatibleInputCount++;
            inputs.Add(new VaultExportInputSummary(
                "combined-index",
                CombinedIndexInputIdentity(inventory),
                "hidden",
                "compatible",
                [],
                SourceProvenance(inventory.Sources)));

            foreach (var source in inventory.Sources)
            {
                var claim = catalog.ClaimForSource(source.SourceIndexId) ?? "hidden";
                sourceClaimBySourceIndexId[source.SourceIndexId] = claim;
                var sourceNodeId = StableNodeId("source", string.Join('\u001f',
                    "node/source/v1",
                    source.SourceIndexId,
                    source.CommitSha,
                    source.ScannerVersion,
                    source.Language ?? source.StoredLanguage ?? "unknown"));
                sourceNodeIdBySourceIndexId[source.SourceIndexId] = sourceNodeId;
                var sourceLabel = claim == "hidden" ? $"hidden-source-{Hash(source.SourceIndexId, 12)}" : SafeDisplayValue(source.Label, "source");
                nodes.Add(new VaultGraphNode(
                    sourceNodeId,
                    "source",
                    claim,
                    sourceLabel,
                    sourceNodeId,
                    sourceLabel,
                    source.SourceIndexId,
                    null,
                    claim == "hidden" ? "present" : source.CommitSha,
                    source.Language ?? source.StoredLanguage ?? "unknown",
                    source.AnalysisLevel,
                    source.BuildStatus,
                    [source.AnalysisLevel],
                    ["combined.report.source.v1"],
                    ["Tier2Structural"],
                    [],
                    [],
                    SourceLimitations(source),
                    $"sources/{Slug(sourceNodeId)}.md",
                    source.ScannerVersion,
                    SourceIdentityHash(source),
                    []));
                originalNodeClaims[sourceNodeId] = claim;
            }

            foreach (var pathNode in inventory.Nodes)
            {
                if (pathNode.NodeKind == "source")
                {
                    continue;
                }

                var sourceClaim = sourceClaimBySourceIndexId.GetValueOrDefault(pathNode.SourceIndexId, "hidden");
                var nodeKind = NormalizeNodeKind(pathNode);
                var sourceNodeId = sourceNodeIdBySourceIndexId.GetValueOrDefault(pathNode.SourceIndexId);
                var ruleIds = DistinctSorted([pathNode.RuleId]);
                var evidenceTiers = DistinctSorted([pathNode.EvidenceTier]);
                var coverage = string.IsNullOrWhiteSpace(pathNode.SourceLabel) ? Array.Empty<string>() : new[] { "static-evidence" };
                var displayName = SafeNodeDisplay(pathNode, sourceClaim);
                if (!TryNodeIdentity(pathNode, sourceNodeId ?? pathNode.SourceIndexId, sourceClaim, out var identity, out var rejectedCategory))
                {
                    if (sourceClaim != "hidden")
                    {
                        throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {rejectedCategory} at stableIdComponent.");
                    }

                    safetyOmittedNodeCount++;
                    gaps.Add(CreateSafetyGap(
                        $"unsafe-id-node-{Hash(pathNode.NodeId, 16)}",
                        "UnsafeIdComponentOmitted",
                        "A graph node was omitted because a source-derived stable-ID component failed vault export safety validation.",
                        pathNode.SourceIndexId,
                        rejectedCategory));
                    continue;
                }

                var nodeId = StableNodeId(nodeKind, identity);
                nodeIdByPathNodeId[pathNode.NodeId] = nodeId;
                nodes.Add(new VaultGraphNode(
                    nodeId,
                    nodeKind,
                    sourceClaim,
                    displayName,
                    sourceNodeId,
                    sourceClaim == "hidden" ? null : SafeDisplayValue(pathNode.SourceLabel, "source"),
                    pathNode.SourceIndexId,
                    NormalizeSurfaceKind(pathNode),
                    null,
                    null,
                    null,
                    null,
                    coverage,
                    ruleIds.Count == 0 ? ["combined.report.evidence-node.v1"] : ruleIds,
                    evidenceTiers.Count == 0 ? ["Tier2Structural"] : evidenceTiers,
                    DistinctSorted([pathNode.CombinedFactId]),
                    [],
                    NodeLimitations(pathNode, sourceClaim),
                    $"{DirectoryForNodeKind(nodeKind)}/{Slug(nodeId)}.md",
                    null,
                    null,
                    EvidenceLocations(pathNode)));
                originalNodeClaims[nodeId] = sourceClaim;

                if (pathNode.NodeKind == "symbol" && sourceClaim == "hidden")
                {
                    gaps.Add(CreateGap(
                        "unsafe-symbol",
                        sourceClaim,
                        "SymbolSafetyGap",
                        UnsafeSymbolRuleId,
                        Tier4Unknown,
                        "A symbol evidence node was category-labeled because the source identity is hidden.",
                        pathNode.SourceIndexId));
                }
            }

            foreach (var pathEdge in inventory.Edges.OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal).ThenBy(edge => edge.EdgeId, StringComparer.Ordinal))
            {
                if (!nodeIdByPathNodeId.TryGetValue(pathEdge.FromNodeId, out var from)
                    || !nodeIdByPathNodeId.TryGetValue(pathEdge.ToNodeId, out var to))
                {
                    safetyOmittedEdgeCount++;
                    gaps.Add(CreateSafetyGap(
                        $"unsafe-id-edge-missing-node-{Hash(pathEdge.EdgeId, 16)}",
                        "UnsafeIdComponentOmitted",
                        "A graph edge was omitted because one of its endpoint nodes was omitted by vault export safety validation.",
                        null,
                        "unsafe-id-component"));
                    continue;
                }

                var claim = MinClaim(originalNodeClaims.GetValueOrDefault(from, "hidden"), originalNodeClaims.GetValueOrDefault(to, "hidden"));
                if (!TryStableEdgeId(pathEdge, from, to, claim, out var edgeId, out var edgeRejectedCategory))
                {
                    if (claim != "hidden")
                    {
                        throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {edgeRejectedCategory} at stableIdComponent.");
                    }

                    safetyOmittedEdgeCount++;
                    gaps.Add(CreateSafetyGap(
                        $"unsafe-id-edge-{Hash(pathEdge.EdgeId, 16)}",
                        "UnsafeIdComponentOmitted",
                        "A graph edge was omitted because a source-derived stable-ID component failed vault export safety validation.",
                        null,
                        edgeRejectedCategory));
                    continue;
                }

                edges.Add(new VaultGraphEdge(
                    edgeId,
                    NormalizeEdgeKind(pathEdge.EdgeKind),
                    from,
                    to,
                    claim,
                    pathEdge.Classification,
                    pathEdge.RuleId,
                    pathEdge.EvidenceTier,
                    null,
                    DistinctSorted(pathEdge.SupportingFactIds),
                    DistinctSorted(pathEdge.SupportingCombinedEdgeIds),
                    EdgeLimitations(pathEdge),
                    EvidenceLocations(pathEdge)));
            }

            foreach (var warning in inventory.CoverageWarnings.OrderBy(value => value, StringComparer.Ordinal))
            {
                limitations.Add(CreateLimitation("coverage-warning", "hidden", "combined.report.coverage.v1", Tier4Unknown, SafeDiagnosticMessage(warning)));
            }

            foreach (var gap in inventory.Gaps)
            {
                gaps.Add(CreateGap(
                    gap.GapId,
                    sourceClaimBySourceIndexId.GetValueOrDefault(gap.SourceIndexId ?? string.Empty, "hidden"),
                    gap.Classification,
                    string.IsNullOrWhiteSpace(gap.RuleId) ? SchemaGapRuleId : gap.RuleId,
                    string.IsNullOrWhiteSpace(gap.EvidenceTier) ? Tier4Unknown : gap.EvidenceTier,
                    SafeDiagnosticMessage(gap.Message),
                    gap.SourceIndexId));
            }
        }

        compatibleInputCount += await AddPathReportsAsync(options.PathsReportPaths ?? [], nodes, edges, gaps, inputs, sourceClaimBySourceIndexId, catalog, cancellationToken);
        compatibleInputCount += await AddReverseReportsAsync(options.ReverseReportPaths ?? [], nodes, edges, gaps, inputs, sourceClaimBySourceIndexId, catalog, cancellationToken);

        if (compatibleInputCount == 0)
        {
            throw new InvalidOperationException("InputSchemaUnsupported: no compatible vault export input was supplied.");
        }

        foreach (var unmatched in catalog.UnmatchedSourceIds(sourceClaimBySourceIndexId.Keys))
        {
            var gap = CreateGap(
                $"claim-unmatched-{Hash(unmatched, 16)}",
                "hidden",
                "ClaimCatalogUnmatched",
                ClaimUnmatchedRuleId,
                Tier4Unknown,
                "A source claim catalog entry did not match a stable source identity.",
                null);
            gaps.Add(gap);
            diagnostics.Add(new VaultExportDiagnostic("InputClaimCatalogUnmatched", ClaimUnmatchedRuleId, "/sourceClaimCatalog/sources", "claim-level"));
        }

        var unfilteredNodeCount = nodes.Count(node => node.Kind is not "rule" and not "gap" and not "limitation");
        var unfilteredEdgeCount = edges.Count;
        ApplyClaimFilter(minimumClaimLevel, nodes, edges, gaps);
        var omittedNodes = unfilteredNodeCount - nodes.Count(node => node.Kind is not "rule" and not "gap" and not "limitation");
        var omittedEdges = unfilteredEdgeCount - edges.Count;
        if (minimumClaimLevel != "hidden" && (omittedNodes > 0 || omittedEdges > 0))
        {
            var omissionGap = CreateGap(
                $"hidden-omitted-{omittedNodes}-{omittedEdges}",
                minimumClaimLevel,
                "HiddenEvidenceOmitted",
                HiddenOmittedRuleId,
                Tier4Unknown,
                "Hidden evidence was omitted by the requested claim-level filter; the export is partial.",
                null);
            gaps.Add(omissionGap);
        }

        if (minimumClaimLevel != "hidden" && !nodes.Any(node => node.Kind is not "rule" and not "gap" and not "limitation"))
        {
            throw new InvalidOperationException("NoVisibleEvidenceAfterFiltering: requested claim-level filter left no visible graph evidence.");
        }

        if (minimumClaimLevel != "hidden" && sourceClaimBySourceIndexId.Count > 0 && sourceClaimBySourceIndexId.Values.All(value => ClaimRank(value) < ClaimRank(minimumClaimLevel)))
        {
            throw new InvalidOperationException("InputClaimLevelHidden: requested claim-level filter cannot be satisfied by the supplied source claim catalog.");
        }

        if (minimumClaimLevel == "hidden")
        {
            ApplyHiddenLocalSafety(nodes, edges, gaps);
        }

        AddRuleNodes(nodes, edges, gaps, limitations);
        AddGapAndLimitationNodes(nodes, gaps, limitations);

        var classification = ClassificationFor(nodes, edges, gaps);
        return new EvidenceGraphVault(
            SchemaVersion,
            string.Empty,
            new VaultExportGenerator(GeneratorName, GeneratorVersion, generatedAt),
            classification,
            inputs.OrderBy(input => input.Kind, StringComparer.Ordinal).ThenBy(input => input.Identity, StringComparer.Ordinal).ToArray(),
            SortNodes(nodes),
            SortEdges(edges),
            SortGaps(gaps),
            SortLimitations(limitations),
            new VaultExportSettings(
                minimumClaimLevel,
                formats,
                omittedNodes > 0 || omittedEdges > 0 || safetyOmittedNodeCount > 0 || safetyOmittedEdgeCount > 0,
                omittedNodes + safetyOmittedNodeCount,
                omittedEdges + safetyOmittedEdgeCount));
    }

    private static async Task<int> AddPathReportsAsync(
        IReadOnlyList<string> paths,
        List<VaultGraphNode> nodes,
        List<VaultGraphEdge> edges,
        List<VaultGraphGap> gaps,
        List<VaultExportInputSummary> inputs,
        Dictionary<string, string> sourceClaims,
        SourceClaimCatalog catalog,
        CancellationToken cancellationToken)
    {
        var compatibleCount = 0;
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[]? bytes = null;
            var inputIdentity = SafeUnavailableInputIdentity("input/paths-report/v1", path);
            try
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                inputIdentity = JsonReportInputIdentity("input/paths-report/v1", bytes);
                await using var stream = new MemoryStream(bytes);
                var report = await JsonSerializer.DeserializeAsync<CombinedDependencyPathReport>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("empty paths report");
                if (report.Paths is null || report.Gaps is null || report.Summary is null)
                {
                    throw new InvalidDataException("missing paths report fields");
                }

                compatibleCount++;
                ApplySourceClaims(report.Sources, catalog, sourceClaims);
                inputs.Add(new VaultExportInputSummary(
                    "paths-report",
                    inputIdentity,
                    InputClaimLevel(report.Sources, sourceClaims),
                    "compatible",
                    report.Limitations ?? [],
                    SourceProvenance(report.Sources)));
                foreach (var pathRow in report.Paths)
                {
                    var reportNodeId = StableNodeId("report", $"node/report/path/v1\u001f{pathRow.PathId}");
                    nodes.Add(new VaultGraphNode(
                        reportNodeId,
                        "report",
                        ClaimForPathNodes(pathRow.Nodes, sourceClaims),
                        $"path {Hash(pathRow.PathId, 12)}",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        [report.ReportCoverage],
                        DistinctSorted(pathRow.Edges.Select(edge => edge.RuleId)),
                        DistinctSorted(pathRow.Edges.Select(edge => edge.EvidenceTier)),
                        DistinctSorted(pathRow.SupportingFactIds),
                        DistinctSorted(pathRow.SupportingEdgeIds),
                        pathRow.Notes.Select(note => SafeDiagnosticMessage($"{note.Code}: {note.Message}")).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        $"reports/{Slug(reportNodeId)}.md",
                        null,
                        null,
                        EvidenceLocations(pathRow.Nodes).Concat(EvidenceLocations(pathRow.Edges)).ToArray()));
                }

                foreach (var gap in report.Gaps)
                {
                    gaps.Add(CreateGap(
                        gap.GapId,
                        sourceClaims.GetValueOrDefault(gap.SourceIndexId ?? string.Empty, "hidden"),
                        gap.Classification,
                        string.IsNullOrWhiteSpace(gap.RuleId) ? SchemaGapRuleId : gap.RuleId,
                        string.IsNullOrWhiteSpace(gap.EvidenceTier) ? Tier4Unknown : gap.EvidenceTier,
                        SafeDiagnosticMessage(gap.Message),
                        gap.SourceIndexId));
                }
            }
            catch
            {
                if (bytes is not null)
                {
                    inputIdentity = JsonReportInputIdentity("input/paths-report/v1", bytes);
                }

                inputs.Add(new VaultExportInputSummary("paths-report", inputIdentity, "hidden", "schema-gap", []));
                gaps.Add(CreateGap($"paths-schema-{Hash(inputIdentity, 16)}", "hidden", "InputSchemaUnsupported", SchemaGapRuleId, Tier4Unknown, "A paths report could not be read with the documented schema.", null));
            }
        }

        return compatibleCount;
    }

    private static async Task<int> AddReverseReportsAsync(
        IReadOnlyList<string> paths,
        List<VaultGraphNode> nodes,
        List<VaultGraphEdge> edges,
        List<VaultGraphGap> gaps,
        List<VaultExportInputSummary> inputs,
        Dictionary<string, string> sourceClaims,
        SourceClaimCatalog catalog,
        CancellationToken cancellationToken)
    {
        var compatibleCount = 0;
        foreach (var path in paths.OrderBy(value => value, StringComparer.Ordinal))
        {
            byte[]? bytes = null;
            var inputIdentity = SafeUnavailableInputIdentity("input/reverse-report/v1", path);
            try
            {
                bytes = await File.ReadAllBytesAsync(path, cancellationToken);
                inputIdentity = JsonReportInputIdentity("input/reverse-report/v1", bytes);
                await using var stream = new MemoryStream(bytes);
                var report = await JsonSerializer.DeserializeAsync<CombinedReverseReport>(stream, JsonOptions, cancellationToken)
                    ?? throw new InvalidDataException("empty reverse report");
                if (report.ReverseRoots is null || report.Gaps is null || report.Summary is null)
                {
                    throw new InvalidDataException("missing reverse report fields");
                }

                compatibleCount++;
                ApplySourceClaims(report.Snapshot.Sources, catalog, sourceClaims);
                inputs.Add(new VaultExportInputSummary(
                    "reverse-report",
                    inputIdentity,
                    InputClaimLevel(report.Snapshot.Sources, sourceClaims),
                    "compatible",
                    report.Limitations ?? [],
                    SourceProvenance(report.Snapshot.Sources)));
                foreach (var root in report.ReverseRoots)
                {
                    var reportNodeId = StableNodeId("report", $"node/report/reverse/v1\u001f{root.RootId}");
                    nodes.Add(new VaultGraphNode(
                        reportNodeId,
                        "report",
                        ClaimForReverseRoot(root, report.Paths, sourceClaims),
                        $"reverse root {Hash(root.RootId, 12)}",
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        [report.ReportCoverage],
                        DistinctSorted(root.RuleIds),
                        DistinctSorted(root.EvidenceTiers),
                        DistinctSorted(root.SupportingFactIds),
                        DistinctSorted(root.SupportingEdgeIds),
                        root.CoverageCaveats.Select(SafeDiagnosticMessage).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                        $"reports/{Slug(reportNodeId)}.md"));
                }

                foreach (var gap in report.Gaps)
                {
                    gaps.Add(CreateGap(
                        gap.GapId,
                        sourceClaims.GetValueOrDefault(gap.SourceIndexId ?? string.Empty, "hidden"),
                        gap.Classification,
                        string.IsNullOrWhiteSpace(gap.RuleId) ? SchemaGapRuleId : gap.RuleId,
                        string.IsNullOrWhiteSpace(gap.EvidenceTier) ? Tier4Unknown : gap.EvidenceTier,
                        SafeDiagnosticMessage(gap.Message),
                        gap.SourceIndexId));
                }
            }
            catch
            {
                if (bytes is not null)
                {
                    inputIdentity = JsonReportInputIdentity("input/reverse-report/v1", bytes);
                }

                inputs.Add(new VaultExportInputSummary("reverse-report", inputIdentity, "hidden", "schema-gap", []));
                gaps.Add(CreateGap($"reverse-schema-{Hash(inputIdentity, 16)}", "hidden", "InputSchemaUnsupported", SchemaGapRuleId, Tier4Unknown, "A reverse report could not be read with the documented schema.", null));
            }
        }

        return compatibleCount;
    }

    private static void ApplySourceClaims(IEnumerable<CombinedReportSource> sources, SourceClaimCatalog catalog, Dictionary<string, string> sourceClaims)
    {
        foreach (var source in sources.OrderBy(source => source.SourceIndexId, StringComparer.Ordinal))
        {
            sourceClaims[source.SourceIndexId] = catalog.ClaimForSource(source.SourceIndexId) ?? sourceClaims.GetValueOrDefault(source.SourceIndexId, "hidden");
        }
    }

    private static void ApplySourceClaims(IEnumerable<CombinedReverseSourceInfo> sources, SourceClaimCatalog catalog, Dictionary<string, string> sourceClaims)
    {
        foreach (var source in sources.OrderBy(source => source.SourceIndexId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(source.SourceIndexId))
            {
                continue;
            }

            sourceClaims[source.SourceIndexId] = catalog.ClaimForSource(source.SourceIndexId) ?? sourceClaims.GetValueOrDefault(source.SourceIndexId, "hidden");
        }
    }

    private static string InputClaimLevel(IEnumerable<CombinedReportSource> sources, IReadOnlyDictionary<string, string> sourceClaims)
    {
        return sources.Select(source => sourceClaims.GetValueOrDefault(source.SourceIndexId, "hidden"))
            .DefaultIfEmpty("hidden")
            .OrderBy(ClaimRank)
            .First();
    }

    private static string InputClaimLevel(IEnumerable<CombinedReverseSourceInfo> sources, IReadOnlyDictionary<string, string> sourceClaims)
    {
        return sources.Select(source => string.IsNullOrWhiteSpace(source.SourceIndexId) ? "hidden" : sourceClaims.GetValueOrDefault(source.SourceIndexId, "hidden"))
            .DefaultIfEmpty("hidden")
            .OrderBy(ClaimRank)
            .First();
    }

    private static IReadOnlyList<VaultSourceProvenance> SourceProvenance(IEnumerable<CombinedReportSource> sources)
    {
        return sources
            .OrderBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .Select(source => new VaultSourceProvenance(
                source.SourceIndexId,
                SourceIdentityHash(source),
                source.ScannerVersion,
                source.CommitSha,
                source.Language ?? source.StoredLanguage,
                source.AnalysisLevel,
                source.BuildStatus))
            .ToArray();
    }

    private static IReadOnlyList<VaultSourceProvenance> SourceProvenance(IEnumerable<CombinedReverseSourceInfo> sources)
    {
        return sources
            .Where(source => !string.IsNullOrWhiteSpace(source.SourceIndexId))
            .OrderBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .Select(source => new VaultSourceProvenance(
                source.SourceIndexId!,
                source.RepositoryIdentityHash,
                null,
                source.CommitSha,
                source.Language,
                source.AnalysisLevel,
                source.BuildStatus))
            .ToArray();
    }

    private static string SourceIdentityHash(CombinedReportSource source)
    {
        return source.GitRootHash
            ?? source.ScanRootPathHash
            ?? source.IndexPathHash
            ?? Hash(source.SourceIndexId, 32);
    }

    private static IReadOnlyList<VaultEvidenceLocation> EvidenceLocations(CombinedPathNode node)
    {
        return string.IsNullOrWhiteSpace(node.FilePath)
            ? []
            : [new VaultEvidenceLocation(node.FilePath, node.StartLine, node.EndLine, null)];
    }

    private static IReadOnlyList<VaultEvidenceLocation> EvidenceLocations(CombinedPathEdge edge)
    {
        return string.IsNullOrWhiteSpace(edge.FilePath)
            ? []
            : [new VaultEvidenceLocation(edge.FilePath, edge.StartLine, edge.EndLine, null)];
    }

    private static IReadOnlyList<VaultEvidenceLocation> EvidenceLocations(IEnumerable<CombinedPathNode> nodes)
    {
        return nodes.SelectMany(EvidenceLocations)
            .Distinct()
            .OrderBy(location => location.FilePath, StringComparer.Ordinal)
            .ThenBy(location => location.StartLine ?? 0)
            .ThenBy(location => location.EndLine ?? 0)
            .ToArray();
    }

    private static IReadOnlyList<VaultEvidenceLocation> EvidenceLocations(IEnumerable<CombinedPathEdge> edges)
    {
        return edges.SelectMany(EvidenceLocations)
            .Distinct()
            .OrderBy(location => location.FilePath, StringComparer.Ordinal)
            .ThenBy(location => location.StartLine ?? 0)
            .ThenBy(location => location.EndLine ?? 0)
            .ToArray();
    }

    private static void ApplyClaimFilter(string minimumClaimLevel, List<VaultGraphNode> nodes, List<VaultGraphEdge> edges, List<VaultGraphGap> gaps)
    {
        var minimumRank = ClaimRank(minimumClaimLevel);
        var keptNodeIds = nodes
            .Where(node => ClaimRank(node.ClaimLevel) >= minimumRank || node.Kind == "rule")
            .Select(node => node.Id)
            .ToHashSet(StringComparer.Ordinal);
        nodes.RemoveAll(node => ClaimRank(node.ClaimLevel) < minimumRank && node.Kind != "rule");
        edges.RemoveAll(edge => ClaimRank(edge.ClaimLevel) < minimumRank || !keptNodeIds.Contains(edge.From) || !keptNodeIds.Contains(edge.To));
        gaps.RemoveAll(gap => ClaimRank(gap.ClaimLevel) < minimumRank);
    }

    private static void AddRuleNodes(List<VaultGraphNode> nodes, IReadOnlyList<VaultGraphEdge> edges, IReadOnlyList<VaultGraphGap> gaps, IReadOnlyList<VaultGraphLimitation> limitations)
    {
        var ruleIds = nodes.SelectMany(node => node.RuleIds)
            .Concat(edges.Select(edge => edge.RuleId))
            .Concat(gaps.Select(gap => gap.RuleId))
            .Concat(limitations.Select(limitation => limitation.RuleId))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal);
        foreach (var ruleId in ruleIds)
        {
            var id = StableNodeId("rule", $"node/rule/v1\u001f{ruleId}");
            if (nodes.Any(node => node.Id == id))
            {
                continue;
            }

            nodes.Add(new VaultGraphNode(
                id,
                "rule",
                "public-safe",
                ruleId,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                [],
                [ruleId],
                [],
                [],
                [],
                [],
                $"rules/{Slug(id)}.md"));
        }
    }

    private static void AddGapAndLimitationNodes(List<VaultGraphNode> nodes, IReadOnlyList<VaultGraphGap> gaps, IReadOnlyList<VaultGraphLimitation> limitations)
    {
        foreach (var gap in gaps)
        {
            var id = StableNodeId("gap", $"node/gap/v1\u001f{gap.Id}");
            if (nodes.Any(node => node.Id == id))
            {
                continue;
            }

            nodes.Add(new VaultGraphNode(id, "gap", gap.ClaimLevel, gap.Classification, null, null, gap.SourceScope, null, null, null, null, null, [], [gap.RuleId], [gap.EvidenceTier], [], [], gap.Limitations, $"gaps/{Slug(id)}.md"));
        }

        foreach (var limitation in limitations)
        {
            var id = StableNodeId("limitation", $"node/limitation/v1\u001f{limitation.Id}");
            if (nodes.Any(node => node.Id == id))
            {
                continue;
            }

            nodes.Add(new VaultGraphNode(id, "limitation", limitation.ClaimLevel, "limitation", null, null, null, null, null, null, null, null, [], [limitation.RuleId], [limitation.EvidenceTier], [], [], [limitation.Message], $"limitations/{Slug(id)}.md"));
        }
    }

    private static Dictionary<string, string> BuildGeneratedFiles(string outputPath, EvidenceGraphVault graph, IReadOnlyList<string> formats)
    {
        var outputRoot = Path.GetFullPath(outputPath);
        var files = new Dictionary<string, string>(StringComparer.Ordinal);
        if (formats.Contains("json", StringComparer.Ordinal))
        {
            files[Path.Combine(outputRoot, "graph.json")] = SerializeGraph(graph);
        }

        if (formats.Contains("markdown", StringComparer.Ordinal))
        {
            files[Path.Combine(outputRoot, "Start Here.md")] = RenderGeneratedMarkdown("start-here", graph.Classification, [], [], graph.Settings.Partial ? ["partial"] : [], [], ["Start Here", "TraceMap Evidence Vault"], TagsForSummary(graph, "start-here"), RenderStartHere(graph));
            files[Path.Combine(outputRoot, "README.md")] = RenderGeneratedMarkdown("readme", graph.Classification, [], [], [], [], ["TraceMap Evidence Vault"], TagsForSummary(graph, "readme"), RenderReadme(graph));
            files[Path.Combine(outputRoot, "index.md")] = RenderGeneratedMarkdown("index", graph.Classification, [], [], graph.Settings.Partial ? ["partial"] : [], [], ["Evidence Index"], TagsForSummary(graph, "index"), RenderIndex(graph));
            foreach (var group in graph.Nodes.GroupBy(node => DirectoryForNodeKind(node.Kind), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                files[Path.Combine(outputRoot, group.Key, "index.md")] = RenderGeneratedMarkdown(
                    $"{group.Key}-index",
                    graph.Classification,
                    DistinctSorted(group.SelectMany(node => node.RuleIds)),
                    DistinctSorted(group.SelectMany(node => node.EvidenceTiers)),
                    DistinctSorted(group.SelectMany(node => node.Coverage)),
                    DistinctSorted(group.SelectMany(node => node.Limitations)),
                    [$"{FolderTitle(group.Key)} Index"],
                    TagsForFolderIndex(graph, group.Key, group),
                    RenderFolderIndex(graph, group.Key, group));
            }

            foreach (var node in graph.Nodes)
            {
                files[Path.Combine(outputRoot, node.FilePath)] = RenderNodeNote(node, graph);
            }
        }

        return files;
    }

    private static string RenderStartHere(EvidenceGraphVault graph)
    {
        var coverage = DistinctSorted(graph.Nodes.SelectMany(node => node.Coverage));
        var hiddenOmitted = graph.Settings.OmittedHiddenNodeCount + graph.Settings.OmittedHiddenEdgeCount;
        var builder = new StringBuilder();
        builder.AppendLine("# Start Here");
        builder.AppendLine();
        builder.AppendLine("## Evidence Summary");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Classification | {Cell(graph.Classification)} |");
        builder.AppendLine($"| Input types | {Cell(string.Join(", ", DistinctSorted(graph.Inputs.Select(input => input.Kind))))} |");
        builder.AppendLine($"| Visible sources | {graph.Nodes.Count(node => node.Kind == "source")} |");
        builder.AppendLine($"| Non-gap nodes | {graph.Nodes.Count(node => node.Kind != "gap")} |");
        builder.AppendLine($"| Edges | {graph.Edges.Count} |");
        builder.AppendLine($"| Gaps | {graph.Gaps.Count} |");
        builder.AppendLine($"| Limitations | {graph.Limitations.Count} |");
        builder.AppendLine($"| Omitted hidden evidence | {hiddenOmitted} |");
        builder.AppendLine();
        builder.AppendLine("## Coverage And Claim Level");
        builder.AppendLine();
        builder.AppendLine($"- Claim level: `{Cell(graph.Classification)}`.");
        builder.AppendLine($"- Coverage labels: `{Cell(coverage.Count == 0 ? "none" : string.Join(", ", coverage))}`.");
        builder.AppendLine(graph.Settings.Partial
            ? "- This export is partial. Hidden, filtered, unsupported, or reduced evidence may be omitted and must not be treated as complete."
            : "- This export is a deterministic static evidence navigation aid. It does not prove runtime behavior or absence.");
        builder.AppendLine();
        builder.AppendLine("## Start With");
        builder.AppendLine();
        foreach (var link in StartLinks(graph))
        {
            builder.AppendLine($"- [{Cell(link.Title)}]({Cell(link.Path)})");
        }

        builder.AppendLine();
        builder.AppendLine("## Review Queues");
        builder.AppendLine();
        builder.AppendLine($"- Weak or syntax-only evidence: `{graph.Nodes.Count(node => node.EvidenceTiers.Any(tier => tier is EvidenceTiers.Tier3SyntaxOrTextual or Tier4Unknown))}` nodes.");
        builder.AppendLine($"- Needs-review or reduced coverage edges: `{graph.Edges.Count(edge => IsReviewClassification(edge.Classification))}` edges.");
        builder.AppendLine($"- Gaps: `{graph.Gaps.Count}` records.");
        builder.AppendLine($"- Limitations: `{graph.Limitations.Count}` records.");
        builder.AppendLine();
        builder.AppendLine("## Indexes");
        builder.AppendLine();
        builder.AppendLine("- [All evidence](index.md)");
        foreach (var group in graph.Nodes.GroupBy(node => DirectoryForNodeKind(node.Kind), StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            builder.AppendLine($"- [{Cell(FolderTitle(group.Key))}]({Cell(group.Key + "/index.md")})");
        }

        builder.AppendLine();
        builder.AppendLine("## Gaps And Limitations");
        builder.AppendLine();
        builder.AppendLine("Gaps and limitations are first-class evidence records. They preserve uncertainty and do not prove clean absence or runtime safety.");
        builder.AppendLine();
        builder.AppendLine("## Source Artifacts");
        builder.AppendLine();
        builder.AppendLine("- [Machine graph](graph.json)");
        builder.AppendLine("- [Generated index](index.md)");
        return builder.ToString();
    }

    private static string RenderReadme(EvidenceGraphVault graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Evidence Vault");
        builder.AppendLine();
        builder.AppendLine("[Start Here](Start%20Here.md) is the human entry point for coverage, review queues, and category indexes.");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Schema | {Cell(graph.SchemaVersion)} |");
        builder.AppendLine($"| Classification | {Cell(graph.Classification)} |");
        builder.AppendLine($"| Generated period | {Cell(graph.Generator.GeneratedAt)} |");
        builder.AppendLine($"| Nodes | {graph.Nodes.Count} |");
        builder.AppendLine($"| Edges | {graph.Edges.Count} |");
        builder.AppendLine($"| Gaps | {graph.Gaps.Count} |");
        builder.AppendLine($"| Partial | {graph.Settings.Partial.ToString().ToLowerInvariant()} |");
        builder.AppendLine();
        builder.AppendLine("This vault is a deterministic local navigation aid over static TraceMap evidence. Edges describe cited static evidence relationships and do not prove runtime execution, deployment, service reachability, release safety, vulnerabilities, ownership, traffic, or business impact.");
        return builder.ToString();
    }

    private static string RenderIndex(EvidenceGraphVault graph)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Evidence Index");
        builder.AppendLine();
        builder.AppendLine("## Nodes");
        builder.AppendLine();
        builder.AppendLine("| Kind | Claim level | Note | Rule IDs | Evidence tiers |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        foreach (var node in graph.Nodes)
        {
            builder.AppendLine($"| {Cell(node.Kind)} | {Cell(node.ClaimLevel)} | [{Cell(node.DisplayName)}]({Cell(node.FilePath)}) | {Cell(string.Join(", ", node.RuleIds))} | {Cell(string.Join(", ", node.EvidenceTiers))} |");
        }

        builder.AppendLine();
        builder.AppendLine("## Edges");
        builder.AppendLine();
        builder.AppendLine("| Kind | Claim level | From | To | Rule ID | Evidence tier |");
        builder.AppendLine("| --- | --- | --- | --- | --- | --- |");
        foreach (var edge in graph.Edges)
        {
            var from = graph.Nodes.First(node => node.Id == edge.From);
            var to = graph.Nodes.First(node => node.Id == edge.To);
            builder.AppendLine($"| {Cell(edge.Kind)} | {Cell(edge.ClaimLevel)} | [{Cell(from.DisplayName)}]({Cell(RelativePath("index.md", from.FilePath))}) | [{Cell(to.DisplayName)}]({Cell(RelativePath("index.md", to.FilePath))}) | {Cell(edge.RuleId)} | {Cell(edge.EvidenceTier)} |");
        }

        return builder.ToString();
    }

    private static string RenderFolderIndex(EvidenceGraphVault graph, string folder, IEnumerable<VaultGraphNode> nodes)
    {
        var orderedNodes = nodes.OrderBy(node => node.Kind, StringComparer.Ordinal).ThenBy(node => node.DisplayName, StringComparer.Ordinal).ThenBy(node => node.Id, StringComparer.Ordinal).ToArray();
        var nodeIds = orderedNodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var edges = graph.Edges.Where(edge => nodeIds.Contains(edge.From) || nodeIds.Contains(edge.To)).ToArray();
        var coverage = DistinctSorted(orderedNodes.SelectMany(node => node.Coverage));
        var builder = new StringBuilder();
        builder.AppendLine($"# {FolderTitle(folder)} Index");
        builder.AppendLine();
        builder.AppendLine("[Start Here](../Start%20Here.md) | [All evidence](../index.md)");
        builder.AppendLine();
        builder.AppendLine("| Field | Value |");
        builder.AppendLine("| --- | --- |");
        builder.AppendLine($"| Nodes | {orderedNodes.Length} |");
        builder.AppendLine($"| Related edges | {edges.Length} |");
        builder.AppendLine($"| Claim level | {Cell(graph.Classification)} |");
        builder.AppendLine($"| Coverage labels | {Cell(coverage.Count == 0 ? "none" : string.Join(", ", coverage))} |");
        builder.AppendLine($"| Evidence tiers | {Cell(string.Join(", ", DistinctSorted(orderedNodes.SelectMany(node => node.EvidenceTiers))))} |");
        builder.AppendLine($"| Primary rule IDs | {Cell(string.Join(", ", DistinctSorted(orderedNodes.SelectMany(node => node.RuleIds))))} |");
        builder.AppendLine();
        builder.AppendLine("| Kind | Title | Rule IDs | Evidence tiers | Coverage |");
        builder.AppendLine("| --- | --- | --- | --- | --- |");
        if (folder == "surfaces")
        {
            foreach (var surfaceGroup in orderedNodes.GroupBy(node => node.SurfaceKind ?? "unknown", StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal))
            {
                builder.AppendLine($"| surface-kind | **{Cell(surfaceGroup.Key)}** | {Cell(string.Join(", ", DistinctSorted(surfaceGroup.SelectMany(node => node.RuleIds))))} | {Cell(string.Join(", ", DistinctSorted(surfaceGroup.SelectMany(node => node.EvidenceTiers))))} | {Cell(string.Join(", ", DistinctSorted(surfaceGroup.SelectMany(node => node.Coverage))))} |");
                foreach (var node in surfaceGroup)
                {
                    builder.AppendLine($"| {Cell(node.Kind)} | [{Cell(node.DisplayName)}]({Cell(RelativePath(Path.Combine(folder, "index.md"), node.FilePath))}) | {Cell(string.Join(", ", node.RuleIds))} | {Cell(string.Join(", ", node.EvidenceTiers))} | {Cell(string.Join(", ", node.Coverage))} |");
                }
            }

            return builder.ToString();
        }

        foreach (var node in orderedNodes)
        {
            builder.AppendLine($"| {Cell(node.Kind)} | [{Cell(node.DisplayName)}]({Cell(RelativePath(Path.Combine(folder, "index.md"), node.FilePath))}) | {Cell(string.Join(", ", node.RuleIds))} | {Cell(string.Join(", ", node.EvidenceTiers))} | {Cell(string.Join(", ", node.Coverage))} |");
        }

        return builder.ToString();
    }

    private static string RenderNodeNote(VaultGraphNode node, EvidenceGraphVault graph)
    {
        var tags = TagsForNode(node);
        var aliases = AliasesForNode(node);
        var body = new StringBuilder();
        body.AppendLine($"# {node.DisplayName}");
        body.AppendLine();
        body.AppendLine("## Evidence");
        body.AppendLine();
        body.AppendLine("| Field | Value |");
        body.AppendLine("| --- | --- |");
        body.AppendLine($"| Node ID | {Cell(node.Id)} |");
        body.AppendLine($"| Kind | {Cell(node.Kind)} |");
        body.AppendLine($"| Claim level | {Cell(node.ClaimLevel)} |");
        body.AppendLine($"| Rule IDs | {Cell(string.Join(", ", node.RuleIds))} |");
        body.AppendLine($"| Evidence tiers | {Cell(string.Join(", ", node.EvidenceTiers))} |");
        body.AppendLine($"| Coverage | {Cell(string.Join(", ", node.Coverage))} |");
        body.AppendLine($"| Supporting facts | {Cell(string.Join(", ", node.SupportingFactIds))} |");
        body.AppendLine($"| Supporting edges | {Cell(string.Join(", ", node.SupportingEdgeIds))} |");

        if (node.EvidenceLocations is { Count: > 0 })
        {
            body.AppendLine();
            body.AppendLine("## Evidence Locations");
            body.AppendLine();
            body.AppendLine("| File | Span |");
            body.AppendLine("| --- | --- |");
            foreach (var location in node.EvidenceLocations.OrderBy(location => location.FilePath, StringComparer.Ordinal)
                         .ThenBy(location => location.StartLine ?? 0)
                         .ThenBy(location => location.EndLine ?? 0))
            {
                body.AppendLine($"| {Cell(location.FilePath)} | {Cell(LineSpan(location))} |");
            }
        }

        if (node.Limitations.Count > 0)
        {
            body.AppendLine();
            body.AppendLine("## Limitations");
            body.AppendLine();
            foreach (var limitation in node.Limitations)
            {
                body.AppendLine($"- {Cell(limitation)}");
            }
        }

        var outgoing = graph.Edges.Where(edge => edge.From == node.Id).ToArray();
        var incoming = graph.Edges.Where(edge => edge.To == node.Id).ToArray();
        if (outgoing.Length > 0)
        {
            body.AppendLine();
            body.AppendLine("## Outgoing Evidence");
            body.AppendLine();
            foreach (var edge in outgoing)
            {
                var target = graph.Nodes.First(candidate => candidate.Id == edge.To);
                body.AppendLine($"- {Cell(edge.Kind)} to [{Cell(target.DisplayName)}]({Cell(RelativePath(node.FilePath, target.FilePath))}) using `{Cell(edge.RuleId)}` / `{Cell(edge.EvidenceTier)}`.");
            }
        }

        if (incoming.Length > 0)
        {
            body.AppendLine();
            body.AppendLine("## Backlinks");
            body.AppendLine();
            foreach (var edge in incoming)
            {
                var source = graph.Nodes.First(candidate => candidate.Id == edge.From);
                body.AppendLine($"- {Cell(edge.Kind)} from [{Cell(source.DisplayName)}]({Cell(RelativePath(node.FilePath, source.FilePath))}) using `{Cell(edge.RuleId)}` / `{Cell(edge.EvidenceTier)}`.");
            }
        }

        return RenderGeneratedMarkdown(
            node.Kind,
            node.ClaimLevel,
            node.RuleIds,
            node.EvidenceTiers,
            node.Coverage,
            node.Limitations,
            aliases,
            tags,
            body.ToString(),
            node.SourceId);
    }

    private static string RenderGeneratedMarkdown(
        string kind,
        string claimLevel,
        IReadOnlyList<string> ruleIds,
        IReadOnlyList<string> evidenceTiers,
        IReadOnlyList<string> coverage,
        IReadOnlyList<string> limitations,
        IReadOnlyList<string> aliases,
        IReadOnlyList<string> tags,
        string body,
        string? sourceId = null)
    {
        var metadata = new SortedDictionary<string, IReadOnlyList<string>?>(StringComparer.Ordinal)
        {
            ["tracemap_generated"] = null,
            ["tracemap_export_schema"] = null,
            ["tracemap_generator"] = null,
            ["tracemap_content_sha256"] = null,
            ["tracemap_kind"] = null,
            ["claim_level"] = null
        };
        if (!string.IsNullOrWhiteSpace(sourceId))
        {
            metadata["source_id"] = null;
        }

        if (ruleIds.Count > 0)
        {
            metadata["rule_ids"] = ruleIds;
        }

        if (evidenceTiers.Count > 0)
        {
            metadata["evidence_tiers"] = evidenceTiers;
        }

        if (coverage.Count > 0)
        {
            metadata["coverage"] = coverage;
        }

        if (limitations.Count > 0)
        {
            metadata["limitations"] = limitations;
        }

        if (aliases.Count > 0)
        {
            metadata["aliases"] = aliases;
        }

        if (tags.Count > 0)
        {
            metadata["tags"] = tags;
        }

        var frontmatter = BuildFrontmatter(metadata, new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["tracemap_generated"] = "true",
            ["tracemap_export_schema"] = SchemaVersion,
            ["tracemap_generator"] = GeneratorName,
            ["tracemap_content_sha256"] = string.Empty,
            ["tracemap_kind"] = kind,
            ["claim_level"] = claimLevel,
            ["source_id"] = sourceId
        });
        var withoutHash = $"{frontmatter}{body.TrimEnd()}\n";
        var hash = Hash(withoutHash, 64);
        return withoutHash.Replace("tracemap_content_sha256: \"\"", $"tracemap_content_sha256: \"{hash}\"", StringComparison.Ordinal);
    }

    private static string BuildFrontmatter(SortedDictionary<string, IReadOnlyList<string>?> metadata, IReadOnlyDictionary<string, string?> scalars)
    {
        var builder = new StringBuilder();
        builder.AppendLine("---");
        foreach (var key in FrontmatterKeyOrder)
        {
            if (!metadata.ContainsKey(key))
            {
                continue;
            }

            if (metadata[key] is { } array)
            {
                builder.AppendLine($"{key}:");
                IEnumerable<string> orderedItems = key == "aliases" ? array : array.OrderBy(value => value, StringComparer.Ordinal);
                foreach (var item in orderedItems)
                {
                    builder.AppendLine($"  - \"{YamlEscape(item)}\"");
                }
            }
            else
            {
                var value = scalars.GetValueOrDefault(key);
                if (key == "tracemap_generated")
                {
                    builder.AppendLine("tracemap_generated: true");
                }
                else
                {
                    builder.AppendLine($"{key}: \"{YamlEscape(value ?? string.Empty)}\"");
                }
            }
        }

        builder.AppendLine("---");
        builder.AppendLine();
        return builder.ToString();
    }

    private static EvidenceGraphVault WithGraphHash(EvidenceGraphVault graph)
    {
        var withoutHash = graph with { ContentHash = string.Empty };
        var contentHash = Hash(SerializeGraph(withoutHash), 64);
        return graph with { ContentHash = contentHash };
    }

    private static EvidenceGraphVault WithNavigationCategories(EvidenceGraphVault graph)
    {
        return graph with
        {
            Nodes = graph.Nodes.Select(node => node with { NavigationCategory = NavigationCategoryForNode(node) }).ToArray(),
            Edges = graph.Edges.Select(edge => edge with { NavigationCategory = NavigationCategoryForEdge(edge) }).ToArray()
        };
    }

    private static string SerializeGraph(EvidenceGraphVault graph)
    {
        var node = JsonSerializer.SerializeToNode(graph, JsonOptions)
            ?? throw new InvalidOperationException("Unable to serialize graph.");
        return SerializeCanonicalJson(SortJson(node));
    }

    private static string SerializeCanonicalJson(JsonNode node)
    {
        return node.ToJsonString(JsonOptions).ReplaceLineEndings("\n") + "\n";
    }

    private static JsonNode SortJson(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            var sorted = new JsonObject();
            foreach (var property in obj.OrderBy(property => property.Key, StringComparer.Ordinal))
            {
                sorted[property.Key] = property.Value is null ? null : SortJson(property.Value.DeepClone());
            }

            return sorted;
        }

        if (node is JsonArray array)
        {
            var sorted = new JsonArray();
            foreach (var item in array)
            {
                sorted.Add(item is null ? null : SortJson(item.DeepClone()));
            }

            return sorted;
        }

        return node.DeepClone();
    }

    private static void ValidateExistingFiles(IReadOnlyDictionary<string, string> files, bool force)
    {
        foreach (var path in files.Keys.OrderBy(path => path, StringComparer.Ordinal))
        {
            if (!File.Exists(path))
            {
                continue;
            }

            var existing = File.ReadAllText(path);
            var fileName = Path.GetFileName(path);
            var generated = fileName.Equals("graph.json", StringComparison.Ordinal)
                ? IsSelfConsistentGraphJson(existing)
                : IsSelfConsistentMarkdown(existing);
            if (generated)
            {
                continue;
            }

            if (HasGeneratedProvenance(existing, fileName))
            {
                if (force)
                {
                    continue;
                }

                throw new InvalidOperationException("GeneratedFileStale: existing generated output has an invalid or stale content hash.");
            }

            throw new InvalidOperationException("UserFileCollision: output path contains a non-generated file.");
        }
    }

    private static bool HasGeneratedProvenance(string content, string fileName)
    {
        return fileName.Equals("graph.json", StringComparison.Ordinal)
            ? HasGeneratedGraphProvenance(content)
            : HasGeneratedMarkdownProvenance(content);
    }

    private static bool HasGeneratedGraphProvenance(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("schemaVersion", out var schema)
                && schema.GetString() == SchemaVersion
                && document.RootElement.TryGetProperty("generator", out var generator)
                && generator.TryGetProperty("name", out var name)
                && name.GetString() == GeneratorName;
        }
        catch
        {
            return false;
        }
    }

    private static bool HasGeneratedMarkdownProvenance(string content)
    {
        return TryReadFrontmatter(content, out var metadata, out _)
            && metadata.TryGetValue("tracemap_generated", out var generated)
            && generated.Equals("true", StringComparison.OrdinalIgnoreCase)
            && metadata.TryGetValue("tracemap_export_schema", out var schema)
            && schema == SchemaVersion
            && metadata.TryGetValue("tracemap_generator", out var generator)
            && generator == GeneratorName;
    }

    private static void ValidateGeneratedStrings(EvidenceGraphVault graph, IReadOnlyDictionary<string, string> files)
    {
        var hiddenEvidenceLocations = graph.Classification == "hidden"
            ? graph.Nodes.SelectMany(node => node.EvidenceLocations ?? [])
                .Concat(graph.Edges.SelectMany(edge => edge.EvidenceLocations ?? []))
                .Select(location => location.FilePath)
                .Where(path => UnsafeCategory(path) == SensitiveWordCategory && IsSafeRepoRelativePath(path))
                .Distinct(StringComparer.Ordinal)
                .ToArray()
            : [];

        foreach (var violation in JsonStringLeaves(JsonSerializer.SerializeToNode(graph, JsonOptions)!, "$"))
        {
            var decision = ClassifyGeneratedValue(
                graph.Classification,
                JsonValueContext(violation.Location),
                violation.Value);
            if (decision.Outcome == VaultSafetyOutcome.Reject)
            {
                throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {decision.Category} at {violation.Location}.");
            }
        }

        foreach (var file in files.OrderBy(file => file.Key, StringComparer.Ordinal))
        {
            var line = 1;
            foreach (var textLine in file.Value.Split('\n'))
            {
                var decision = ClassifyGeneratedValue(graph.Classification, VaultValueContext.MarkdownEvidenceLocation, textLine);
                if (decision.Outcome == VaultSafetyOutcome.Reject)
                {
                    if (graph.Classification == "hidden"
                        && decision.Category == SensitiveWordCategory
                        && hiddenEvidenceLocations.Any(path => textLine.Contains(path, StringComparison.Ordinal)))
                    {
                        line++;
                        continue;
                    }

                    throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {decision.Category} at markdown line {line}.");
                }

                line++;
            }
        }
    }

    private static VaultSafetyDecision ClassifySourceValue(string claimLevel, VaultValueContext context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new VaultSafetyDecision(VaultSafetyOutcome.AllowCategory, "empty-value");
        }

        var normalized = NormalizeSafetyValue(context, value);
        var category = UnsafeCategory(normalized);
        if (category is not null && category != SensitiveWordCategory)
        {
            return new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
        }

        if (context is VaultValueContext.RepoRelativePath or VaultValueContext.EvidenceLocation)
        {
            if (IsSafeRepoRelativePath(normalized))
            {
                return category == SensitiveWordCategory && claimLevel == "hidden"
                    ? new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null)
                    : new VaultSafetyDecision(category is null ? VaultSafetyOutcome.AllowRaw : VaultSafetyOutcome.Reject, category);
            }

            return new VaultSafetyDecision(VaultSafetyOutcome.Reject, category ?? "local-path");
        }

        if (context is VaultValueContext.SymbolDisplayName or VaultValueContext.RouteActionModelMemberName)
        {
            if (!IsSafeDisplayText(normalized))
            {
                return claimLevel == "hidden"
                    ? new VaultSafetyDecision(VaultSafetyOutcome.OmitWithGap, "hidden-display-name-invalid")
                    : new VaultSafetyDecision(VaultSafetyOutcome.Reject, category ?? "unsafe-display-name");
            }

            if (category == SensitiveWordCategory)
            {
                return claimLevel == "hidden"
                    ? new VaultSafetyDecision(VaultSafetyOutcome.AllowHash, "sensitive-word-safe-name")
                    : new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
            }

            return new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null);
        }

        if (context == VaultValueContext.StableTraceMapId)
        {
            if (!IsSafeStableTraceMapId(normalized))
            {
                return new VaultSafetyDecision(VaultSafetyOutcome.Reject, category ?? "unsafe-id-component");
            }

            if (category == SensitiveWordCategory)
            {
                return claimLevel == "hidden"
                    ? new VaultSafetyDecision(VaultSafetyOutcome.AllowHash, "sensitive-word-safe-name")
                    : new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
            }

            return new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null);
        }

        if (context is VaultValueContext.RuleId or VaultValueContext.ClosedVocabulary or VaultValueContext.DiagnosticCategory)
        {
            return category is null
                ? new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null)
                : new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
        }

        return category is null
            ? new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null)
            : new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
    }

    private static VaultSafetyDecision ClassifyGeneratedValue(string claimLevel, VaultValueContext context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null);
        }

        var normalized = NormalizeSafetyValue(context, value);
        var category = UnsafeCategory(normalized);
        if (category is null)
        {
            if (context is VaultValueContext.SymbolDisplayName or VaultValueContext.RouteActionModelMemberName
                && !IsSafeDisplayText(normalized))
            {
                return new VaultSafetyDecision(VaultSafetyOutcome.Reject, "unsafe-display-name");
            }

            if (context == VaultValueContext.StableTraceMapId && !IsSafeStableTraceMapId(normalized))
            {
                return new VaultSafetyDecision(VaultSafetyOutcome.Reject, "unsafe-id-component");
            }

            return new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null);
        }

        if (claimLevel == "hidden"
            && category == SensitiveWordCategory
            && context is VaultValueContext.EvidenceLocation or VaultValueContext.RepoRelativePath
            && IsSafeRepoRelativePath(normalized))
        {
            return new VaultSafetyDecision(VaultSafetyOutcome.AllowRaw, null);
        }

        return new VaultSafetyDecision(VaultSafetyOutcome.Reject, category);
    }

    private static VaultValueContext JsonValueContext(string pointer)
    {
        if (IsEvidenceLocationPointer(pointer))
        {
            return VaultValueContext.EvidenceLocation;
        }

        if (Regex.IsMatch(pointer, @"/(id|from|to|identity|sourceId)$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(pointer, @"/(supportingFactIds|supportingEdgeIds)/\d+$", RegexOptions.CultureInvariant))
        {
            return VaultValueContext.StableTraceMapId;
        }

        if (Regex.IsMatch(pointer, @"/(ruleId)$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(pointer, @"/ruleIds/\d+$", RegexOptions.CultureInvariant))
        {
            return VaultValueContext.RuleId;
        }

        if (Regex.IsMatch(pointer, @"/displayName$", RegexOptions.CultureInvariant))
        {
            return VaultValueContext.SymbolDisplayName;
        }

        if (Regex.IsMatch(pointer, @"/filePath$", RegexOptions.CultureInvariant))
        {
            return VaultValueContext.RepoRelativePath;
        }

        if (Regex.IsMatch(pointer, @"/(classification|claimLevel|kind|surfaceKind|language|analysisLevel|buildStatus|compatibility|minimumClaimLevel|generatedAt|schemaVersion|name|version)$", RegexOptions.CultureInvariant)
            || Regex.IsMatch(pointer, @"/(coverage|evidenceTiers|formats)/\d+$", RegexOptions.CultureInvariant))
        {
            return VaultValueContext.ClosedVocabulary;
        }

        return VaultValueContext.GeneratedMetadata;
    }

    private static bool IsEvidenceLocationPointer(string pointer)
    {
        return Regex.IsMatch(pointer, @"/evidenceLocations/\d+/filePath$", RegexOptions.CultureInvariant);
    }

    private static IEnumerable<(string Location, string Value)> JsonStringLeaves(JsonNode node, string path)
    {
        switch (node)
        {
            case JsonValue value:
                if (value.TryGetValue<string>(out var text))
                {
                    yield return (path, text);
                }
                break;
            case JsonObject obj:
                foreach (var property in obj)
                {
                    if (property.Value is not null)
                    {
                        foreach (var leaf in JsonStringLeaves(property.Value, $"{path}/{property.Key}"))
                        {
                            yield return leaf;
                        }
                    }
                }
                break;
            case JsonArray array:
                for (var i = 0; i < array.Count; i++)
                {
                    if (array[i] is not null)
                    {
                        foreach (var leaf in JsonStringLeaves(array[i]!, $"{path}/{i}"))
                        {
                            yield return leaf;
                        }
                    }
                }
                break;
        }
    }

    private static string? UnsafeCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var text = value.Trim();
        if (text.Contains("/Users/", StringComparison.Ordinal)
            || text.Contains("\\Users\\", StringComparison.Ordinal)
            || text.Contains("/home/", StringComparison.Ordinal)
            || text.Contains("file://", StringComparison.OrdinalIgnoreCase)
            || text.Contains("C:\\", StringComparison.OrdinalIgnoreCase)
            || text.Contains("~/", StringComparison.Ordinal)
            || text.StartsWith("//", StringComparison.Ordinal)
            || text.Contains("$HOME", StringComparison.OrdinalIgnoreCase)
            || text.Contains("%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("/tmp/", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, @"(^|[\s""'`])(/tmp/|/var/tmp/)", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || text.Contains("\\Temp\\", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, @"(^|[\s""'`])\\\\[^\\/\s]+[\\/]", RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"(^|[\s""'`])([A-Za-z]:[\\/])", RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"(^|[\\/])\.\.([\\/]|$)", RegexOptions.CultureInvariant))
        {
            return "local-path";
        }

        if (text.Contains("git@", StringComparison.OrdinalIgnoreCase)
            || text.Contains("github.com:", StringComparison.OrdinalIgnoreCase)
            || text.Contains("private.example", StringComparison.OrdinalIgnoreCase)
            || text.Contains("://", StringComparison.OrdinalIgnoreCase))
        {
            return "raw-remote-or-url";
        }

        if (Regex.IsMatch(text, @"\b(server|data source|database|initial catalog|user id|uid|password|pwd)\s*=[^;\r\n]+;", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"\b(connectionstring|connection string)\b\s*[:=]", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant))
        {
            return "connection-string";
        }

        if (Regex.IsMatch(text, @"-----BEGIN [A-Z ]*PRIVATE KEY-----", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"\bAuthorization\s*:\s*(Bearer|Basic)\s+\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"\b(api[-_ ]?key|access[-_ ]?token|session[-_ ]?id|password|secret)\s*[:=]\s*\S+", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"\b(sk|pk)_(live|test)_[A-Za-z0-9]{16,}\b", RegexOptions.CultureInvariant)
            || Regex.IsMatch(text, @"\b[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\.[A-Za-z0-9_-]{20,}\b", RegexOptions.CultureInvariant))
        {
            return "credential";
        }

        if (Regex.IsMatch(text, @"\bselect\b.+\bfrom\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)
            || Regex.IsMatch(text, @"\binsert\b.+\binto\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)
            || Regex.IsMatch(text, @"\bupdate\b.+\bset\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline)
            || Regex.IsMatch(text, @"\bdelete\b.+\bfrom\b", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline))
        {
            return "raw-sql";
        }

        if (text.Contains("password", StringComparison.OrdinalIgnoreCase)
            || text.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || text.Contains("token", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connectionstring", StringComparison.OrdinalIgnoreCase)
            || text.Contains("connection string", StringComparison.OrdinalIgnoreCase))
        {
            return SensitiveWordCategory;
        }

        return null;
    }

    private static void PreValidateClosedVocabulary(IEnumerable<string> values)
    {
        foreach (var value in values)
        {
            if (UnsafeCategory(value) is { } category)
            {
                throw new InvalidOperationException($"VaultExportClosedVocabularyUnsafe: {category}.");
            }
        }
    }

    private static void ApplyHiddenLocalSafety(List<VaultGraphNode> nodes, List<VaultGraphEdge> edges, List<VaultGraphGap> gaps)
    {
        var safeContextGaps = new SortedDictionary<string, string>(StringComparer.Ordinal);

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].EvidenceLocations is not { Count: > 0 } locations)
            {
                continue;
            }

            nodes[i] = nodes[i] with
            {
                EvidenceLocations = NormalizeHiddenEvidenceLocations(
                    locations,
                    $"$/nodes/{i}/evidenceLocations",
                    safeContextGaps)
            };
        }

        for (var i = 0; i < edges.Count; i++)
        {
            if (edges[i].EvidenceLocations is not { Count: > 0 } locations)
            {
                continue;
            }

            edges[i] = edges[i] with
            {
                EvidenceLocations = NormalizeHiddenEvidenceLocations(
                    locations,
                    $"$/edges/{i}/evidenceLocations",
                    safeContextGaps)
            };
        }

        foreach (var gapEntry in safeContextGaps)
        {
            gaps.Add(new VaultGraphGap(
                $"gap:{Hash(string.Join('\u001f', ["gap/v1", gapEntry.Key, "hidden", "HiddenSafeContextAccepted", HiddenSafeContextRuleId, Tier4Unknown]), IdHashLength)}",
                "hidden",
                "HiddenSafeContextAccepted",
                HiddenSafeContextRuleId,
                Tier4Unknown,
                "A hidden safe-context evidence location contains a sensitive word and remains local-only; public and demo exports stay strict.",
                gapEntry.Value,
                [
                    "Local hidden context preserves navigation evidence but does not prove public safety.",
                    $"Evidence location hash: {gapEntry.Value}."
                ]));
        }
    }

    private static IReadOnlyList<VaultEvidenceLocation> NormalizeHiddenEvidenceLocations(
        IReadOnlyList<VaultEvidenceLocation> locations,
        string pointer,
        IDictionary<string, string> safeContextGaps)
    {
        var normalized = new List<VaultEvidenceLocation>(locations.Count);
        for (var i = 0; i < locations.Count; i++)
        {
            var location = locations[i];
            var filePath = NormalizeRepoRelativePath(location.FilePath);
            if (!IsSafeRepoRelativePath(filePath))
            {
                var category = UnsafeCategory(location.FilePath) ?? "unsafe-evidence-location";
                throw new InvalidOperationException($"UnsafeValueRejected: {category} at {pointer}/{i}/filePath.");
            }

            if (UnsafeCategory(filePath) == SensitiveWordCategory)
            {
                var locationHash = Hash(filePath, EvidenceLocationHashLength);
                var safeScope = $"evidence-location:{locationHash}";
                safeContextGaps[$"hidden-safe-evidence-location-{locationHash}"] = safeScope;
            }

            normalized.Add(location with { FilePath = filePath });
        }

        return normalized
            .Distinct()
            .OrderBy(location => location.FilePath, StringComparer.Ordinal)
            .ThenBy(location => location.StartLine ?? 0)
            .ThenBy(location => location.EndLine ?? 0)
            .ToArray();
    }

    private static string NormalizeRepoRelativePath(string value)
    {
        return value.Trim().Replace('\\', '/');
    }

    private static bool IsSafeRepoRelativePath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var text = NormalizeRepoRelativePath(value);
        if (text.Length > MaxRepoRelativePathLength
            || text.StartsWith("/", StringComparison.Ordinal)
            || text.StartsWith("~/", StringComparison.Ordinal)
            || text.StartsWith("$HOME", StringComparison.OrdinalIgnoreCase)
            || text.StartsWith("%USERPROFILE%", StringComparison.OrdinalIgnoreCase)
            || text.Contains("://", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(text, @"^[A-Za-z]:/", RegexOptions.CultureInvariant)
            || text.StartsWith("//", StringComparison.Ordinal)
            || text.Any(char.IsControl))
        {
            return false;
        }

        var segments = text.Split('/');
        if (segments.Length == 0)
        {
            return false;
        }

        for (var i = 0; i < segments.Length; i++)
        {
            var segment = segments[i];
            if (string.IsNullOrWhiteSpace(segment)
                || segment == "."
                || segment == "..")
            {
                return false;
            }

            if (i == 0
                && (segment.Equals("users", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("home", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("tmp", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("temp", StringComparison.OrdinalIgnoreCase)
                    || segment.Equals("var", StringComparison.OrdinalIgnoreCase)))
            {
                return false;
            }
        }

        var category = UnsafeCategory(text);
        return category is null or SensitiveWordCategory;
    }

    private static async Task<SourceClaimCatalog> ReadClaimCatalogAsync(string? path, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SourceClaimCatalog(new Dictionary<string, string>(StringComparer.Ordinal));
        }

        await using var stream = File.OpenRead(path);
        var document = await JsonSerializer.DeserializeAsync<SourceClaimCatalogDocument>(stream, JsonOptions, cancellationToken)
            ?? throw new InvalidDataException("Source claim catalog could not be parsed.");
        if (document.SchemaVersion != "source-claim-catalog.v1")
        {
            throw new InvalidDataException("InputSchemaUnsupported: source claim catalog schema is unsupported.");
        }

        var entries = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var entry in document.Sources ?? [])
        {
            if (string.IsNullOrWhiteSpace(entry.SourceIndexId) || string.IsNullOrWhiteSpace(entry.ProofId))
            {
                continue;
            }

            entries[entry.SourceIndexId.Trim()] = NormalizeClaimLevel(entry.ClaimLevel, "source claim catalog");
        }

        return new SourceClaimCatalog(entries);
    }

    private static void ValidateOptions(VaultExportOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("vault export requires --out <vault-output>.");
        }

        if (string.IsNullOrWhiteSpace(options.CombinedIndexPath)
            && (options.PathsReportPaths is null || options.PathsReportPaths.Count == 0)
            && (options.ReverseReportPaths is null || options.ReverseReportPaths.Count == 0))
        {
            throw new ArgumentException("InputMissing: vault export requires --combined-index, --paths-report, or --reverse-report.");
        }

        if (!string.IsNullOrWhiteSpace(options.CombinedIndexPath) && !File.Exists(options.CombinedIndexPath))
        {
            throw new FileNotFoundException("InputMissing: combined index does not exist.", options.CombinedIndexPath);
        }

        foreach (var reportPath in (options.PathsReportPaths ?? []).Concat(options.ReverseReportPaths ?? []))
        {
            if (!File.Exists(reportPath))
            {
                throw new FileNotFoundException("InputMissing: report input does not exist.", reportPath);
            }
        }
    }

    private static IReadOnlyList<string> NormalizeFormats(string? format)
    {
        var values = (format ?? "markdown,json")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(value => value.ToLowerInvariant() switch
            {
                "md" or "markdown" => "markdown",
                "json" => "json",
                _ => throw new ArgumentException("vault export --format must be markdown, json, or markdown,json.")
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return values.Length == 0 ? ["json", "markdown"] : values;
    }

    private static string ResolveGeneratedAt(string? date, string minimumClaimLevel)
    {
        if (string.IsNullOrWhiteSpace(date))
        {
            if (minimumClaimLevel != "hidden")
            {
                throw new ArgumentException("vault export --date <yyyy-MM> is required for demo-safe or public-safe output.");
            }

            return "hidden";
        }

        var trimmed = date.Trim();
        if (trimmed.Length == 7
            && trimmed[4] == '-'
            && int.TryParse(trimmed[..4], out var year)
            && int.TryParse(trimmed[5..], out var month)
            && year is >= 2000 and <= 2100
            && month is >= 1 and <= 12)
        {
            return trimmed;
        }

        throw new ArgumentException("vault export --date must be YYYY-MM.");
    }

    private static string NormalizeClaimLevel(string? value, string source)
    {
        return (value ?? "hidden").Trim().ToLowerInvariant() switch
        {
            "hidden" or "local-only" => "hidden",
            "demo-safe" => "demo-safe",
            "public-safe" => "public-safe",
            _ => throw new ArgumentException($"{source} claim level must be hidden, demo-safe, or public-safe.")
        };
    }

    private static int ClaimRank(string claimLevel)
    {
        return claimLevel switch
        {
            "hidden" => 0,
            "demo-safe" => 1,
            "public-safe" => 2,
            _ => 0
        };
    }

    private static string MinClaim(string left, string right) => ClaimRank(left) <= ClaimRank(right) ? left : right;

    private static string ClassificationFor(IReadOnlyList<VaultGraphNode> nodes, IReadOnlyList<VaultGraphEdge> edges, IReadOnlyList<VaultGraphGap> gaps)
    {
        return nodes.Select(node => node.ClaimLevel)
            .Concat(edges.Select(edge => edge.ClaimLevel))
            .Concat(gaps.Select(gap => gap.ClaimLevel))
            .DefaultIfEmpty("hidden")
            .OrderBy(ClaimRank)
            .First();
    }

    private static string ClaimForPathNodes(IReadOnlyList<CombinedPathNode> nodes, IReadOnlyDictionary<string, string> sourceClaims)
    {
        return nodes.Select(node => sourceClaims.GetValueOrDefault(node.SourceIndexId, "hidden"))
            .DefaultIfEmpty("hidden")
            .OrderBy(ClaimRank)
            .First();
    }

    private static string ClaimForReverseRoot(
        CombinedReverseRoot root,
        IReadOnlyList<CombinedReversePath> paths,
        IReadOnlyDictionary<string, string> sourceClaims)
    {
        var rootPathIds = root.PathIds.ToHashSet(StringComparer.Ordinal);
        return paths
            .Where(path => rootPathIds.Contains(path.PathId))
            .SelectMany(path => path.Nodes)
            .Select(node => string.IsNullOrWhiteSpace(node.SourceIndexId) ? "hidden" : sourceClaims.GetValueOrDefault(node.SourceIndexId, "hidden"))
            .DefaultIfEmpty("hidden")
            .OrderBy(ClaimRank)
            .First();
    }

    private static string CombinedIndexInputIdentity(CombinedPathGraphInventory inventory)
    {
        var identity = new
        {
            sources = inventory.Sources
                .OrderBy(source => source.SourceIndexId, StringComparer.Ordinal)
                .Select(source => new
                {
                    source.SourceIndexId,
                    source.Label,
                    source.ScanId,
                    source.RepoName,
                    source.RemoteUrl,
                    source.Branch,
                    source.CommitSha,
                    source.ScannerVersion,
                    source.Language,
                    source.StoredLanguage,
                    source.LanguageCorrected,
                    source.ScanRootRelativePath,
                    source.ScanRootPathHash,
                    source.GitRootHash,
                    source.AnalysisLevel,
                    source.BuildStatus
                }),
            coverageWarnings = inventory.CoverageWarnings.OrderBy(value => value, StringComparer.Ordinal),
            nodes = inventory.Nodes.OrderBy(node => node.NodeId, StringComparer.Ordinal),
            edges = inventory.Edges.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal),
            gaps = inventory.Gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal)
        };
        return StableObjectInputIdentity("input/combined-index/v1", identity);
    }

    private static string JsonReportInputIdentity(string context, byte[] bytes)
    {
        try
        {
            var node = JsonNode.Parse(bytes);
            if (node is null)
            {
                return $"{context}:json:{Hash(Convert.ToHexString(bytes), 24)}";
            }

            RemovePathDerivedIdentityFields(node);
            return $"{context}:json:{Hash(SerializeCanonicalJson(SortJson(node)), 24)}";
        }
        catch
        {
            return $"{context}:bytes:{Hash(Convert.ToHexString(bytes), 24)}";
        }
    }

    private static string StableObjectInputIdentity(string context, object value)
    {
        var node = JsonSerializer.SerializeToNode(value, JsonOptions)
            ?? throw new InvalidOperationException("Unable to serialize input identity.");
        return $"{context}:{Hash(SerializeCanonicalJson(SortJson(node)), 24)}";
    }

    private static string SafeUnavailableInputIdentity(string context, string? path)
    {
        var fileName = string.IsNullOrWhiteSpace(path) ? "unknown" : Path.GetFileName(path);
        return $"{context}:unavailable:{Hash(fileName, 24)}";
    }

    private static void RemovePathDerivedIdentityFields(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var property in obj.Select(property => property.Key).ToArray())
            {
                if (property.Equals("indexPath", StringComparison.OrdinalIgnoreCase)
                    || property.Equals("outputPath", StringComparison.OrdinalIgnoreCase)
                    || property.Equals("indexPathHash", StringComparison.OrdinalIgnoreCase))
                {
                    obj.Remove(property);
                    continue;
                }

                if (obj[property] is { } child)
                {
                    RemovePathDerivedIdentityFields(child);
                }
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                if (item is not null)
                {
                    RemovePathDerivedIdentityFields(item);
                }
            }
        }
    }

    private static string StableNodeId(string kind, string identity)
    {
        return $"node:{kind}:{Hash(identity, IdHashLength)}";
    }

    private static string StableEdgeId(string kind, string from, string to, string ruleId, string evidenceTier, string classification, IReadOnlyList<string> factIds, IReadOnlyList<string> edgeIds)
    {
        return $"edge:{NormalizeEdgeKind(kind)}:{Hash(string.Join('\u001f', ["edge/v1", kind, from, to, ruleId, evidenceTier, classification, string.Join('|', DistinctSorted(factIds)), string.Join('|', DistinctSorted(edgeIds))]), IdHashLength)}";
    }

    private static bool TryNodeIdentity(
        CombinedPathNode node,
        string sourceNodeId,
        string claimLevel,
        out string identity,
        out string rejectedCategory)
    {
        identity = string.Empty;
        rejectedCategory = "unsafe-id-component";
        if (!TryIdentityComponent(claimLevel, VaultValueContext.StableTraceMapId, sourceNodeId, "source-id", out var safeSourceNodeId, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.ClosedVocabulary, NormalizeNodeKind(node), "node-kind", out var safeNodeKind, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.ClosedVocabulary, NormalizeSurfaceKind(node) ?? "none", "surface-kind", out var safeSurfaceKind, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.RuleId, node.RuleId ?? "none", "rule-id", out var safeRuleId, out rejectedCategory))
        {
            return false;
        }

        var endpointComponent = node.NodeKind is "endpoint" && claimLevel != "hidden"
            ? $"{node.HttpMethod} {node.NormalizedPathKey}"
            : $"internal-node:{Hash(node.NodeId, 16)}";
        if (!TryIdentityComponent(claimLevel, VaultValueContext.RouteActionModelMemberName, endpointComponent, "endpoint", out var safeEndpointComponent, out rejectedCategory))
        {
            return false;
        }

        var evidenceComponent = node.NodeKind is "surface" or "endpoint"
            ? node.ShapeHash ?? node.TextHash ?? node.SurfaceName ?? node.DisplayName ?? $"internal-node:{Hash(node.NodeId, 16)}"
            : $"internal-node:{Hash(node.NodeId, 16)}";
        if (!TryIdentityComponent(claimLevel, VaultValueContext.RouteActionModelMemberName, evidenceComponent, "evidence", out var safeEvidenceComponent, out rejectedCategory))
        {
            return false;
        }

        identity = string.Join('\u001f',
            "node/evidence/v1",
            safeSourceNodeId,
            safeNodeKind,
            safeSurfaceKind,
            safeRuleId,
            safeEndpointComponent,
            safeEvidenceComponent);
        return true;
    }

    private static bool TryStableEdgeId(CombinedPathEdge edge, string from, string to, string claimLevel, out string edgeId, out string rejectedCategory)
    {
        edgeId = string.Empty;
        rejectedCategory = "unsafe-id-component";
        if (!TryIdentityComponent(claimLevel, VaultValueContext.ClosedVocabulary, edge.EdgeKind, "edge-kind", out var safeKind, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.StableTraceMapId, from, "from", out var safeFrom, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.StableTraceMapId, to, "to", out var safeTo, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.RuleId, edge.RuleId, "rule-id", out var safeRuleId, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.ClosedVocabulary, edge.EvidenceTier, "evidence-tier", out var safeEvidenceTier, out rejectedCategory)
            || !TryIdentityComponent(claimLevel, VaultValueContext.ClosedVocabulary, edge.Classification, "classification", out var safeClassification, out rejectedCategory))
        {
            return false;
        }

        var safeFactIds = new List<string>();
        foreach (var factId in DistinctSorted(edge.SupportingFactIds))
        {
            if (!TryIdentityComponent(claimLevel, VaultValueContext.StableTraceMapId, factId, "supporting-fact", out var safeFactId, out rejectedCategory))
            {
                return false;
            }

            safeFactIds.Add(safeFactId);
        }

        var safeEdgeIds = new List<string>();
        foreach (var supportingEdgeId in DistinctSorted(edge.SupportingCombinedEdgeIds))
        {
            if (!TryIdentityComponent(claimLevel, VaultValueContext.StableTraceMapId, supportingEdgeId, "supporting-edge", out var safeSupportingEdgeId, out rejectedCategory))
            {
                return false;
            }

            safeEdgeIds.Add(safeSupportingEdgeId);
        }

        edgeId = StableEdgeId(safeKind, safeFrom, safeTo, safeRuleId, safeEvidenceTier, safeClassification, safeFactIds, safeEdgeIds);
        return true;
    }

    private static bool TryIdentityComponent(
        string claimLevel,
        VaultValueContext context,
        string? value,
        string fallback,
        out string safeValue,
        out string rejectedCategory)
    {
        safeValue = fallback;
        rejectedCategory = "unsafe-id-component";
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        var normalized = NormalizeSafetyValue(context, value);
        var decision = ClassifySourceValue(claimLevel, context, normalized);
        rejectedCategory = decision.Category ?? "unsafe-id-component";
        switch (decision.Outcome)
        {
            case VaultSafetyOutcome.AllowRaw:
                safeValue = normalized;
                return true;
            case VaultSafetyOutcome.AllowHash:
                safeValue = $"{ContextLabel(context)}-sha256:{Hash($"vault-export/identity/v1/{context}/{normalized}", DisplayNameHashLength)}";
                return true;
            case VaultSafetyOutcome.AllowCategory:
            case VaultSafetyOutcome.OmitWithGap:
                safeValue = decision.Category ?? ContextLabel(context);
                return true;
            case VaultSafetyOutcome.Reject:
            default:
                return false;
        }
    }

    private static string NormalizeSafetyValue(VaultValueContext context, string value)
    {
        var trimmed = value.Trim();
        return context is VaultValueContext.RepoRelativePath or VaultValueContext.EvidenceLocation
            ? NormalizeRepoRelativePath(trimmed)
            : trimmed.ReplaceLineEndings(" ");
    }

    private static string ContextLabel(VaultValueContext context)
    {
        return context switch
        {
            VaultValueContext.RepoRelativePath => "repo-relative-path",
            VaultValueContext.EvidenceLocation => "evidence-location",
            VaultValueContext.SymbolDisplayName => "symbol-display",
            VaultValueContext.RouteActionModelMemberName => "route-action-model-member",
            VaultValueContext.StableTraceMapId => "stable-tracemap-id",
            VaultValueContext.RuleId => "rule-id",
            VaultValueContext.ClosedVocabulary => "closed-vocabulary",
            VaultValueContext.DiagnosticCategory => "diagnostic-category",
            VaultValueContext.MarkdownEvidenceLocation => "markdown-evidence-location",
            _ => "generated-metadata"
        };
    }

    private static string NormalizeNodeKind(CombinedPathNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.PackageName) || node.SurfaceKind == "package-config")
        {
            return "package";
        }

        return node.NodeKind switch
        {
            "endpoint" => "endpoint",
            "HttpClientSurface" or "HttpRouteSurface" => "endpoint",
            "surface" => "surface",
            "symbol" => "symbol",
            _ => "surface"
        };
    }

    private static string? NormalizeSurfaceKind(CombinedPathNode node)
    {
        if (NormalizeNodeKind(node) == "package")
        {
            return "package-config";
        }

        return string.IsNullOrWhiteSpace(node.SurfaceKind) ? null : node.SurfaceKind;
    }

    private static string NormalizeEdgeKind(string value)
    {
        return value switch
        {
            "inherits" or "implements" or "overrides" => "calls",
            _ => value
        };
    }

    private static string SafeNodeDisplay(CombinedPathNode node, string sourceClaim)
    {
        var kind = NormalizeNodeKind(node);
        if (sourceClaim == "hidden")
        {
            return $"{kind}-{Hash(node.NodeId, 12)}";
        }

        return kind switch
        {
            "endpoint" => SafeDisplayValue($"{node.HttpMethod ?? "HTTP"} {node.NormalizedPathKey ?? "route"}", "endpoint"),
            "surface" => SafeDisplayValue($"{node.SurfaceKind ?? "surface"} {node.SurfaceName ?? node.ShapeHash ?? node.TextHash ?? Hash(node.NodeId, 12)}", "surface"),
            "package" => SafeDisplayValue($"{node.PackageName ?? "package"} {node.TextLength ?? string.Empty}".Trim(), "package"),
            "symbol" => SafeDisplayValue(node.DisplayName, "symbol"),
            _ => SafeDisplayValue(node.DisplayName, kind)
        };
    }

    private static string SafeDisplayValue(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        var text = value.Trim().ReplaceLineEndings(" ");
        if (UnsafeCategory(text) is { } category)
        {
            throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {category} at displayName.");
        }

        return IsSafeDisplayText(text) ? text : $"{fallback}-{Hash(text, DisplayNameHashLength)}";
    }

    private static bool IsSafeDisplayText(string value)
    {
        return value.Length <= MaxDisplayNameLength
            && !value.Any(char.IsControl)
            && !value.Contains('\t', StringComparison.Ordinal)
            && !Regex.IsMatch(value, @" {2,}", RegexOptions.CultureInvariant);
    }

    private static bool IsSafeStableTraceMapId(string value)
    {
        return value.Length <= MaxDisplayNameLength
            && value.Length > 0
            && !value.Any(char.IsControl)
            && !Regex.IsMatch(value, @"\s", RegexOptions.CultureInvariant)
            && UnsafeCategory(value) is null or SensitiveWordCategory;
    }

    private static IReadOnlyList<string> SourceLimitations(CombinedReportSource source)
    {
        var limitations = new List<string>();
        if (!string.Equals(source.BuildStatus, "Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add("Build status is not clean; analysis coverage is reduced.");
        }

        if (!string.Equals(source.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.OrdinalIgnoreCase))
        {
            limitations.Add("Analysis level indicates reduced or partial static coverage.");
        }

        return limitations.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> NodeLimitations(CombinedPathNode node, string sourceClaim)
    {
        var limitations = new List<string>();
        if (sourceClaim == "hidden")
        {
            limitations.Add("Display values are category-labeled because the source identity is hidden.");
        }

        if (node.EvidenceTier == Tier4Unknown)
        {
            limitations.Add("Evidence tier is unknown; do not upgrade the conclusion from graph presence.");
        }

        return limitations.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static IReadOnlyList<string> EdgeLimitations(CombinedPathEdge edge)
    {
        var limitations = new List<string> { "Static evidence relationship only; this edge does not prove runtime execution." };
        if (edge.EvidenceTier == Tier4Unknown)
        {
            limitations.Add("Unknown evidence tier preserved from source evidence.");
        }

        return limitations.OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static VaultGraphGap CreateGap(string key, string claimLevel, string classification, string ruleId, string evidenceTier, string message, string? sourceScope)
    {
        var id = $"gap:{Hash(string.Join('\u001f', ["gap/v1", key, claimLevel, classification, ruleId, evidenceTier, sourceScope ?? ""]), IdHashLength)}";
        return new VaultGraphGap(id, claimLevel, classification, ruleId, evidenceTier, message, sourceScope, []);
    }

    private static VaultGraphGap CreateSafetyGap(string key, string classification, string message, string? sourceScope, string? category)
    {
        var safeCategory = string.IsNullOrWhiteSpace(category) ? "unsafe-id-component" : category;
        var id = $"gap:{Hash(string.Join('\u001f', ["gap/v1", key, "hidden", classification, UnsafeIdComponentRuleId, Tier4Unknown, sourceScope ?? string.Empty, safeCategory]), IdHashLength)}";
        return new VaultGraphGap(
            id,
            "hidden",
            classification,
            UnsafeIdComponentRuleId,
            Tier4Unknown,
            message,
            sourceScope,
            [$"Rejected component category: {safeCategory}."]);
    }

    private static VaultGraphLimitation CreateLimitation(string key, string claimLevel, string ruleId, string evidenceTier, string message)
    {
        return new VaultGraphLimitation($"limitation:{Hash(string.Join('\u001f', ["limitation/v1", key, claimLevel, ruleId, evidenceTier, message]), IdHashLength)}", claimLevel, ruleId, evidenceTier, message);
    }

    private static string SafeDiagnosticMessage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Sanitized diagnostic category.";
        }

        return UnsafeCategory(value) is null ? value.ReplaceLineEndings(" ") : "Sanitized diagnostic category.";
    }

    private static IReadOnlyList<string> DistinctSorted(IEnumerable<string?> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<VaultGraphNode> SortNodes(IEnumerable<VaultGraphNode> nodes)
    {
        return nodes.DistinctBy(node => node.Id)
            .OrderBy(node => node.Kind, StringComparer.Ordinal)
            .ThenBy(node => node.Id, StringComparer.Ordinal)
            .ThenBy(node => node.SourceScope, StringComparer.Ordinal)
            .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<VaultGraphEdge> SortEdges(IEnumerable<VaultGraphEdge> edges)
    {
        return edges.DistinctBy(edge => edge.Id)
            .OrderBy(edge => edge.Kind, StringComparer.Ordinal)
            .ThenBy(edge => edge.From, StringComparer.Ordinal)
            .ThenBy(edge => edge.To, StringComparer.Ordinal)
            .ThenBy(edge => edge.RuleId, StringComparer.Ordinal)
            .ThenBy(edge => edge.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<VaultGraphGap> SortGaps(IEnumerable<VaultGraphGap> gaps)
    {
        return gaps.DistinctBy(gap => gap.Id)
            .OrderBy(gap => gap.RuleId, StringComparer.Ordinal)
            .ThenBy(gap => gap.Classification, StringComparer.Ordinal)
            .ThenBy(gap => gap.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<VaultGraphLimitation> SortLimitations(IEnumerable<VaultGraphLimitation> limitations)
    {
        return limitations.DistinctBy(limitation => limitation.Id)
            .OrderBy(limitation => limitation.RuleId, StringComparer.Ordinal)
            .ThenBy(limitation => limitation.Id, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> TagsForNode(VaultGraphNode node)
    {
        return DistinctSorted([
            $"tracemap/kind/{Slug(node.Kind)}",
            $"tracemap/claim/{Slug(node.ClaimLevel)}",
            .. node.EvidenceTiers.Select(tier => $"tracemap/tier/{Slug(tier)}"),
            .. node.Coverage.Select(coverage => $"tracemap/coverage/{Slug(coverage)}"),
            .. string.IsNullOrWhiteSpace(node.SurfaceKind) ? [] : new[] { $"tracemap/surface/{Slug(node.SurfaceKind)}" },
            .. node.Kind == "gap" ? new[] { "tracemap/review/gap" } : [],
            .. node.EvidenceTiers.Any(tier => tier is EvidenceTiers.Tier3SyntaxOrTextual or Tier4Unknown) || node.Coverage.Any(IsWeakCoverageLabel)
                ? new[] { "tracemap/review/needs-review" }
                : []
        ]);
    }

    private static IReadOnlyList<string> TagsForSummary(EvidenceGraphVault graph, string kind)
    {
        return DistinctSorted([
            $"tracemap/kind/{Slug(kind)}",
            $"tracemap/claim/{Slug(graph.Classification)}",
            .. graph.Settings.Partial ? new[] { "tracemap/coverage/partial", "tracemap/review/needs-review" } : []
        ]);
    }

    private static IReadOnlyList<string> TagsForFolderIndex(EvidenceGraphVault graph, string folder, IEnumerable<VaultGraphNode> nodes)
    {
        var nodeArray = nodes.ToArray();
        return DistinctSorted([
            $"tracemap/kind/{Slug(folder)}",
            $"tracemap/claim/{Slug(graph.Classification)}",
            .. nodeArray.SelectMany(TagsForNode),
            .. graph.Settings.Partial ? new[] { "tracemap/coverage/partial" } : []
        ]);
    }

    private static IReadOnlyList<string> AliasesForNode(VaultGraphNode node)
    {
        var values = new List<(int Category, string Value)>();
        AddAlias(values, 0, node.DisplayName);
        AddAlias(values, 1, ShortStableId(node.Id));
        foreach (var value in node.RuleIds)
        {
            AddAlias(values, 2, value);
        }

        foreach (var value in node.EvidenceTiers)
        {
            AddAlias(values, 3, value);
        }

        foreach (var value in node.Coverage)
        {
            AddAlias(values, 4, value);
        }

        AddAlias(values, 5, node.SurfaceKind);
        AddAlias(values, 6, node.Kind);
        return values
            .GroupBy(value => value.Value, StringComparer.Ordinal)
            .Select(group => group.OrderBy(value => value.Category).First())
            .OrderBy(value => value.Category)
            .ThenBy(value => value.Value, StringComparer.Ordinal)
            .Select(value => value.Value)
            .ToArray();
    }

    private static void AddAlias(List<(int Category, string Value)> values, int category, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values.Add((category, value.Trim()));
        }
    }

    private static string ShortStableId(string id)
    {
        var index = id.LastIndexOf(':');
        return index >= 0 && index < id.Length - 1 ? id[(index + 1)..] : id;
    }

    private static bool IsWeakCoverageLabel(string value)
    {
        return value.Contains("reduced", StringComparison.OrdinalIgnoreCase)
            || value.Contains("partial", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unsupported", StringComparison.OrdinalIgnoreCase)
            || value.Contains("unknown", StringComparison.OrdinalIgnoreCase)
            || value.Contains("failed", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReviewClassification(string value)
    {
        return value.Contains("review", StringComparison.OrdinalIgnoreCase)
            || IsWeakCoverageLabel(value);
    }

    private static IReadOnlyList<(string Title, string Path)> StartLinks(EvidenceGraphVault graph)
    {
        var links = new List<(string Title, string Path)>();
        foreach (var (kind, folder, title) in new[]
                 {
                     ("endpoint", "endpoints", "Endpoints"),
                     ("surface", "surfaces", "Dependency surfaces"),
                     ("package", "packages", "Packages"),
                     ("gap", "gaps", "Gaps"),
                     ("limitation", "limitations", "Limitations"),
                     ("rule", "rules", "Rules")
                 })
        {
            if (graph.Nodes.Any(node => node.Kind == kind))
            {
                links.Add((title, $"{folder}/index.md"));
            }
        }

        if (links.Count == 0)
        {
            links.Add(("All evidence", "index.md"));
        }

        return links;
    }

    private static string FolderTitle(string folder)
    {
        return folder switch
        {
            "endpoints" => "Endpoints",
            "surfaces" => "Dependency Surfaces",
            "packages" => "Packages",
            "symbols" => "Symbols",
            "rules" => "Rules",
            "gaps" => "Gaps",
            "limitations" => "Limitations",
            "reports" => "Reports",
            "sources" => "Sources",
            _ => "Evidence Nodes"
        };
    }

    private static string NavigationCategoryForNode(VaultGraphNode node)
    {
        return node.Kind switch
        {
            "source" => "source",
            "endpoint" => "endpoint",
            "surface" => "surface",
            "package" => "package",
            "symbol" => "symbol",
            "rule" => "rule",
            "gap" => "gap",
            "limitation" => "limitation",
            "report" => "report",
            _ => "source"
        };
    }

    private static string NavigationCategoryForEdge(VaultGraphEdge edge)
    {
        return edge.Kind switch
        {
            "route-flow-evidence" => "route-flow-evidence",
            "static-path-evidence" => "static-path-evidence",
            "surface-evidence" => "surface-evidence",
            "symbol-evidence" => "symbol-evidence",
            "report-evidence" => "report-evidence",
            "links-to-rule" => "links-to-rule",
            "has-limitation" => "has-limitation",
            "has-gap" => "has-gap",
            "supports" => "supports",
            _ => "describes"
        };
    }

    private static string DirectoryForNodeKind(string kind)
    {
        return kind switch
        {
            "source" => "sources",
            "endpoint" => "endpoints",
            "route" => "routes",
            "surface" => "surfaces",
            "package" => "packages",
            "symbol" => "symbols",
            "rule" => "rules",
            "gap" => "gaps",
            "limitation" => "limitations",
            "report" => "reports",
            _ => "nodes"
        };
    }

    private static string Slug(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var ch in value.ToLowerInvariant())
        {
            builder.Append(char.IsAsciiLetterOrDigit(ch) ? ch : '-');
        }

        return builder.ToString().Trim('-');
    }

    private static string RelativePath(string fromFile, string toFile)
    {
        var fromDirectory = Path.GetDirectoryName(fromFile.Replace('\\', '/')) ?? string.Empty;
        var prefix = string.IsNullOrWhiteSpace(fromDirectory) ? string.Empty : "../";
        return prefix + toFile.Replace('\\', '/');
    }

    private static string LineSpan(VaultEvidenceLocation location)
    {
        return (location.StartLine, location.EndLine) switch
        {
            ({ } start, { } end) when start == end => start.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ({ } start, { } end) => $"{start.ToString(System.Globalization.CultureInfo.InvariantCulture)}-{end.ToString(System.Globalization.CultureInfo.InvariantCulture)}",
            ({ } start, null) => start.ToString(System.Globalization.CultureInfo.InvariantCulture),
            _ => "line-span-unavailable"
        };
    }

    private static string Cell(string? value)
    {
        return (value ?? string.Empty)
            .Replace("|", "\\|", StringComparison.Ordinal)
            .ReplaceLineEndings(" ")
            .Replace("[", "\\[", StringComparison.Ordinal)
            .Replace("]", "\\]", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal)
            .Replace("`", "\\`", StringComparison.Ordinal);
    }

    private static string YamlEscape(string value)
    {
        return value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
    }

    private static string Hash(string value, int length = 64)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }

    private static bool IsHash(string? value)
    {
        return value is { Length: 64 } && value.All(ch => ch is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static bool TryReadFrontmatter(string content, out Dictionary<string, string> metadata, out string body)
    {
        metadata = new Dictionary<string, string>(StringComparer.Ordinal);
        body = string.Empty;
        var normalized = content.ReplaceLineEndings("\n");
        if (!normalized.StartsWith("---\n", StringComparison.Ordinal))
        {
            return false;
        }

        var end = normalized.IndexOf("\n---\n", 4, StringComparison.Ordinal);
        if (end < 0)
        {
            return false;
        }

        var lines = normalized[4..end].Split('\n');
        foreach (var line in lines)
        {
            var colon = line.IndexOf(':', StringComparison.Ordinal);
            if (colon <= 0 || line.StartsWith("  - ", StringComparison.Ordinal))
            {
                continue;
            }

            var key = line[..colon];
            var value = line[(colon + 1)..].Trim();
            if (value.StartsWith('"') && value.EndsWith('"') && value.Length >= 2)
            {
                value = value[1..^1].Replace("\\\"", "\"", StringComparison.Ordinal).Replace("\\\\", "\\", StringComparison.Ordinal);
            }

            metadata[key] = value;
        }

        body = normalized[(end + "\n---\n".Length)..];
        return true;
    }

    private static string NormalizeMarkdownHashInput(string content)
    {
        return content.ReplaceLineEndings("\n").Replace(
            System.Text.RegularExpressions.Regex.Match(content.ReplaceLineEndings("\n"), "tracemap_content_sha256: \"[a-f0-9]{64}\"").Value,
            "tracemap_content_sha256: \"\"",
            StringComparison.Ordinal);
    }

    private sealed record SourceClaimCatalogDocument(string SchemaVersion, IReadOnlyList<SourceClaimCatalogEntry>? Sources);

    private sealed record SourceClaimCatalogEntry(string SourceIndexId, string ClaimLevel, string ProofId);

    private sealed class SourceClaimCatalog(IReadOnlyDictionary<string, string> entries)
    {
        public string? ClaimForSource(string sourceIndexId) => entries.GetValueOrDefault(sourceIndexId);

        public IReadOnlyList<string> UnmatchedSourceIds(IEnumerable<string> sourceIndexIds)
        {
            var matched = sourceIndexIds.ToHashSet(StringComparer.Ordinal);
            return entries.Keys.Where(key => !matched.Contains(key)).OrderBy(key => key, StringComparer.Ordinal).ToArray();
        }
    }
}
