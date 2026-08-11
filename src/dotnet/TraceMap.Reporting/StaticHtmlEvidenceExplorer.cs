using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using TraceMap.Core;

[assembly: InternalsVisibleTo("TraceMap.Tests")]

namespace TraceMap.Reporting;

public sealed record StaticHtmlEvidenceExplorerOptions(
    string InputPath,
    string OutputPath,
    string? SafetyProfile = null,
    bool Force = false);

public sealed record StaticHtmlEvidenceExplorerResult(
    ExplorerManifest Manifest,
    ExplorerData Data,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<ExplorerGap> Gaps);

public sealed record ExplorerManifest(
    string SchemaVersion,
    bool TracemapGenerated,
    ExplorerGenerator Generator,
    string SafetyProfile,
    string ClaimLevel,
    string RepoIdentityPolicy,
    string GenerationTimestampPolicy,
    string? GeneratedAt,
    string RepositoryIdentifier,
    string? CommitSha,
    string CoverageStatus,
    ExplorerManifestCounts Counts,
    IReadOnlyList<ExplorerInputArtifact> Inputs,
    IReadOnlyList<ExplorerRedaction> Redactions,
    IReadOnlyList<ExplorerGap> Gaps,
    IReadOnlyList<ExplorerLimitation> Limitations);

public sealed record ExplorerGenerator(
    string Name,
    string Version,
    string TraceMapVersion);

public sealed record ExplorerManifestCounts(
    int SourceCount,
    int ArtifactCount,
    int SurfaceCount,
    int PathCount,
    int ReducerResultCount,
    int EvidenceRowCount,
    int GapCount,
    int LimitationCount,
    int RuleCount,
    int RedactionCount,
    int OmittedCount);

public sealed record ExplorerData(
    string SchemaVersion,
    ExplorerSummary Summary,
    IReadOnlyList<ExplorerSectionStatus> SectionStatuses,
    IReadOnlyList<ExplorerCompatibilityRow> CompatibilityLedger,
    IReadOnlyList<ExplorerSource> Sources,
    IReadOnlyList<ExplorerInputArtifact> Artifacts,
    IReadOnlyList<ExplorerSurface> Surfaces,
    IReadOnlyList<ExplorerPath> Paths,
    IReadOnlyList<ExplorerEvidenceRow> EvidenceRows,
    IReadOnlyList<ExplorerGap> Gaps,
    IReadOnlyList<ExplorerLimitation> Limitations,
    IReadOnlyList<ExplorerRule> Rules,
    IReadOnlyList<ExplorerRedaction> Redactions);

public sealed record ExplorerSurface(
    string SurfaceId,
    string SurfaceKind,
    string? SurfaceSubtype,
    string SafeLabel,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string ArtifactId,
    string SourceId,
    string CommitSha,
    string ExtractorVersion,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerPath(
    string PathId,
    string PathKind,
    string Classification,
    string Confidence,
    string CoverageLabel,
    string ArtifactId,
    IReadOnlyList<ExplorerPathHop> Hops,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerPathHop(
    string HopId,
    int Sequence,
    string EdgeKind,
    string RuleId,
    string EvidenceTier,
    string FromNodeId,
    string ToNodeId,
    string SourceId,
    string CommitSha,
    string ExtractorVersion,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds);

public sealed record ExplorerCompatibilityRow(
    string RowId,
    string SubjectKind,
    string SubjectId,
    string SafeLabel,
    string CompatibilityStatus,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string Scope,
    IReadOnlyList<string> SupportIds,
    IReadOnlyList<string> LimitationIds,
    string Message);

public sealed record ExplorerSummary(
    string SafetyProfile,
    string ClaimLevel,
    string CoverageStatus,
    string? CommitSha,
    int SourceCount,
    int ArtifactCount,
    int SurfaceCount,
    int PathCount,
    int ReducerResultCount,
    int EvidenceRowCount,
    int GapCount,
    int LimitationCount,
    int RuleCount,
    int RedactionCount,
    int OmittedCount,
    IReadOnlyList<string> CoverageLabels,
    bool ReducerOutputPresent);

public sealed record ExplorerSectionStatus(
    string SectionId,
    string Label,
    string Status,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string Message,
    IReadOnlyList<string> SupportIds);

public sealed record ExplorerSource(
    string SourceId,
    string SafeLabel,
    string SourceKind,
    string ClaimLevel,
    string CoverageStatus,
    string? CommitSha,
    IReadOnlyList<string> ExtractorVersions,
    IReadOnlyList<string> ArtifactIds,
    int GapCount,
    int LimitationCount,
    int RedactionCount,
    int OmittedCount);

public sealed record ExplorerInputArtifact(
    string ArtifactId,
    string ArtifactKind,
    string SafeLabel,
    string ContentHash,
    string SchemaVersion,
    string ClaimLevel,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> SourceIds,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Gaps,
    string Compatibility);

public sealed record ExplorerEvidenceRow(
    string EvidenceId,
    string RuleId,
    string EvidenceTier,
    string EvidenceKind,
    string SupportId,
    string ArtifactId,
    string? SourceId,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? SnippetHash,
    string? CoverageLabel,
    string? ExtractorVersion,
    IReadOnlyList<string> Limitations);

public sealed record ExplorerGap(
    string GapId,
    string RuleId,
    string EvidenceTier,
    string GapKind,
    string Scope,
    string AffectedSection,
    string CoverageLabel,
    string Message,
    IReadOnlyList<string> SupportIds);

public sealed record ExplorerLimitation(
    string LimitationId,
    string RuleId,
    string EvidenceTier,
    string LimitationKind,
    string AffectedSection,
    string Scope,
    string ClaimEffect,
    string Message,
    IReadOnlyList<string> SupportIds);

public sealed record ExplorerRule(
    string RuleId,
    string Title,
    string Description,
    string EvidenceTier,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> RelatedSections);

public sealed record ExplorerRedaction(
    string RedactionId,
    string RuleId,
    string Category,
    string Location,
    string Action,
    int Count);

public static class StaticHtmlEvidenceExplorer
{
    public const string SchemaVersion = "tracemap-static-html-evidence-explorer.v3";
    public const string GeneratorName = "tracemap-static-html-evidence-explorer";
    private static readonly string[] PriorSchemaVersions =
    [
        "tracemap-static-html-evidence-explorer.v1",
        "tracemap-static-html-evidence-explorer.v2"
    ];

    public const string UnsupportedSchemaRuleId = "explorer.input.unsupported-schema.v1";
    public const string ProvenanceConflictRuleId = "explorer.input.provenance-conflict.v1";
    public const string MissingCommitRuleId = "explorer.input.missing-commit.v1";
    public const string RedactedDisplayValueRuleId = "explorer.render.redacted-display-value.v1";
    public const string OmittedUnsafeValueRuleId = "explorer.render.omitted-unsafe-value.v1";
    public const string CatalogUnavailableRuleId = "explorer.render.catalog-unavailable.v1";
    public const string NoNetworkAssetsRuleId = "explorer.render.no-network-assets.v1";
    public const string PartialSectionRuleId = "explorer.render.partial-section.v1";
    public const string SectionStatusRuleId = "explorer.render.section-status.v1";
    public const string CompatibilityLedgerRuleId = "explorer.render.compatibility-ledger.v1";
    public const string ReleaseReviewInputRuleId = "explorer.input.release-review.v1";
    public const string PathsReportInputRuleId = "explorer.input.paths-report.v1";
    public const string GeneratedFileStaleRuleId = "explorer.validation.generated-file-stale.v1";
    public const string UserFileCollisionRuleId = "explorer.validation.user-file-collision.v1";
    public const string UnsafeRejectedRuleId = "explorer.validation.unsafe-value-rejected.v1";

    private const string Tier4Unknown = EvidenceTiers.Tier4Unknown;
    private const string Tier2Structural = EvidenceTiers.Tier2Structural;
    private const string PublicDemo = "public-demo";
    private const string HiddenLocal = "hidden-local";
    private const string SourceId = "source:scan-output";
    private const int EvidenceRowNoScriptLimit = 200;
    private const int MaxRuleCatalogTextLength = 360;
    private const long MaxRuleCatalogBytes = 1_048_576;
    private const long MaxReleaseReviewBytes = 16_777_216;
    private const long MaxPathsReportBytes = 33_554_432;
    private const int MaxPathsReportSources = 1_000;
    private const int MaxPathsReportPaths = 1_000;
    private const int MaxPathsReportHops = 10_000;

    private static readonly HashSet<string> SupportedPathClassifications = new(StringComparer.Ordinal)
    {
        CombinedDependencyPathClassifications.StrongStaticPath,
        CombinedDependencyPathClassifications.ProbableStaticPath,
        CombinedDependencyPathClassifications.NeedsReviewPath,
        CombinedDependencyPathClassifications.NeedsReviewStaticPath,
        CombinedDependencyPathClassifications.ReducedCoverage,
        CombinedDependencyPathClassifications.AnalysisGap,
        CombinedDependencyPathClassifications.NoBackendEvidence,
        CombinedDependencyPathClassifications.UnknownAnalysisGap,
        CombinedDependencyPathClassifications.NoPathFound,
        CombinedDependencyPathClassifications.SelectorNoMatch,
        CombinedDependencyPathClassifications.ClassificationFilterNoMatch
    };

    private static readonly HashSet<string> SupportedPathEdgeKinds = new(StringComparer.Ordinal)
    {
        "calls",
        "creates",
        "inherits",
        "implements",
        "overrides",
        "argument-passed",
        "parameter-forward",
        "endpoint-match",
        "fact-attached-to-symbol",
        "surface-evidence",
        "symbol-reconciliation",
        "interface-candidate",
        "override-candidate",
        "message-publish-consume"
    };

    private static readonly HashSet<string> SupportedPathEdgeClassifications = new(StringComparer.Ordinal)
    {
        "EvidenceEdge",
        CombinedEndpointClassifications.MatchedEndpoint,
        CombinedEndpointClassifications.OptionalSegmentMatch,
        CombinedEndpointClassifications.MethodMismatch,
        CombinedEndpointClassifications.ClientCallNoServerEndpoint,
        CombinedEndpointClassifications.ServerEndpointNoClientMatch,
        CombinedEndpointClassifications.AmbiguousMatch,
        CombinedEndpointClassifications.DynamicClientUrlNeedsReview,
        CombinedEndpointClassifications.UnknownAnalysisGap
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex CommitShaPattern = new("^[0-9a-fA-F]{7,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex SqlPattern = new(
        @"\b(select\s+(\*|[\w\[\]"".]+(?:\s*,\s*[\w\[\]"".]+)*)\s+from|insert\s+into|update\s+[\w\[\]"".]+\s+set|delete\s+from|merge\s+into)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    public static async Task<StaticHtmlEvidenceExplorerResult> GenerateAsync(
        StaticHtmlEvidenceExplorerOptions options,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.InputPath))
        {
            throw new ArgumentException("explorer generate requires --input <artifact-dir>.");
        }

        if (string.IsNullOrWhiteSpace(options.OutputPath))
        {
            throw new ArgumentException("explorer generate requires --out <explorer-output>.");
        }

        var safetyProfile = NormalizeSafetyProfile(options.SafetyProfile);
        var inputDirectory = Path.GetFullPath(options.InputPath);
        if (!Directory.Exists(inputDirectory))
        {
            throw new DirectoryNotFoundException("Explorer input artifact directory was not found.");
        }

        var context = await BuildContextAsync(inputDirectory, safetyProfile, cancellationToken);
        var data = BuildData(context);
        var manifest = BuildManifest(context, data);
        var files = BuildGeneratedFiles(manifest, data);

        ValidateGeneratedFilesForSafety(files);
        ValidateExistingFiles(options.OutputPath, files, options.Force);

        var outputDirectory = Path.GetFullPath(options.OutputPath);
        Directory.CreateDirectory(outputDirectory);
        foreach (var (relativePath, content) in files.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            await File.WriteAllTextAsync(fullPath, content, Encoding.UTF8, cancellationToken);
        }

        return new StaticHtmlEvidenceExplorerResult(
            manifest,
            data,
            files.Keys.OrderBy(path => path, StringComparer.Ordinal).Select(path => Path.Combine(outputDirectory, path)).ToArray(),
            data.Gaps);
    }

    private static async Task<ExplorerBuildContext> BuildContextAsync(
        string inputDirectory,
        string safetyProfile,
        CancellationToken cancellationToken)
    {
        var artifacts = new List<ExplorerInputArtifact>();
        var surfaces = new List<ExplorerSurface>();
        var paths = new List<ExplorerPath>();
        var evidenceRows = new List<ExplorerEvidenceRow>();
        var gaps = new List<ExplorerGap>();
        var limitations = new List<ExplorerLimitation>();
        var redactions = new Dictionary<(string RuleId, string Category, string Location, string Action), int>();
        var coverageLabels = new SortedSet<string>(StringComparer.Ordinal);
        var reportSources = new List<ExplorerSource>();
        ScanManifest? manifest = null;
        string? sourceCommitSha = null;
        string? sourceCommitSupportId = null;

        var manifestPath = Path.Combine(inputDirectory, "scan-manifest.json");
        if (File.Exists(manifestPath))
        {
            manifest = await ReadJsonAsync<ScanManifest>(manifestPath, cancellationToken);
            if (IsUsableCommitSha(manifest.CommitSha))
            {
                sourceCommitSha = manifest.CommitSha;
                sourceCommitSupportId = "artifact:scan-manifest";
            }
            var manifestCoverage = CoverageLabelsFromManifest(manifest);
            foreach (var label in manifestCoverage)
            {
                coverageLabels.Add(label);
            }

            artifacts.Add(new ExplorerInputArtifact(
                "artifact:scan-manifest",
                "scan-manifest",
                "Scan manifest",
                await HashFileAsync(manifestPath, cancellationToken),
                "scan-manifest.v1",
                ClaimLevelForSafetyProfile(safetyProfile),
                manifestCoverage,
                [SourceId],
                [],
                [],
                "supported"));

            if (!IsUsableCommitSha(manifest.CommitSha))
            {
                gaps.Add(CreateGap(
                    "missing-commit-scan-manifest",
                    MissingCommitRuleId,
                    "missing-commit",
                    "artifact:scan-manifest",
                    "sources",
                    "PartialAnalysis",
                    "The scan manifest does not contain a usable commit SHA, so source identity is partial.",
                    ["artifact:scan-manifest"]));
            }

            RecordOmittedManifestIdentity(manifest, redactions);
        }
        else
        {
            gaps.Add(CreateGap(
                "missing-scan-manifest",
                PartialSectionRuleId,
                "not-provided",
                "input-directory",
                "sources",
                "PartialAnalysis",
                "scan-manifest.json was not provided; source identity and coverage are partial.",
                []));
        }

        var factsPath = Path.Combine(inputDirectory, "facts.ndjson");
        if (File.Exists(factsPath))
        {
            artifacts.Add(new ExplorerInputArtifact(
                "artifact:facts-ndjson",
                "facts-ndjson",
                "Fact stream",
                await HashFileAsync(factsPath, cancellationToken),
                "facts.ndjson.v1",
                ClaimLevelForSafetyProfile(safetyProfile),
                coverageLabels.Count == 0 ? ["UnknownCoverage"] : coverageLabels.ToArray(),
                [SourceId],
                [],
                [],
                "supported"));

            var facts = await ReadFactsAsync(factsPath, cancellationToken);
            var factCommitShas = facts
                .Select(fact => fact.CommitSha)
                .Where(IsUsableCommitSha)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var factsHaveOneCompleteCommit = facts.Count > 0
                && facts.All(fact => IsUsableCommitSha(fact.CommitSha))
                && factCommitShas.Length == 1;
            if (sourceCommitSha is null && factsHaveOneCompleteCommit)
            {
                sourceCommitSha = factCommitShas[0];
                sourceCommitSupportId = "artifact:facts-ndjson";
            }

            if (manifest is not null && IsUsableCommitSha(manifest.CommitSha))
            {
                foreach (var commitSha in factCommitShas.Where(commitSha => !commitSha.Equals(manifest.CommitSha, StringComparison.OrdinalIgnoreCase)))
                {
                    gaps.Add(CreateGap(
                        $"commit-conflict-{Hash(commitSha, 12)}",
                        ProvenanceConflictRuleId,
                        "commit-conflict",
                        "artifact:facts-ndjson",
                        "evidence-rows",
                        "PartialAnalysis",
                        "facts.ndjson contains evidence for a different commit SHA than scan-manifest.json; affected sections are partial.",
                        ["artifact:scan-manifest", "artifact:facts-ndjson"]));
                }
            }

            if (manifest is null && !factsHaveOneCompleteCommit)
            {
                gaps.Add(CreateGap(
                    "missing-commit-facts",
                    MissingCommitRuleId,
                    "missing-commit",
                    "artifact:facts-ndjson",
                    "evidence-rows",
                    "PartialAnalysis",
                    "facts.ndjson does not establish one usable commit for every fact and no scan manifest was provided.",
                    ["artifact:facts-ndjson"]));
            }

            foreach (var fact in facts.OrderBy(fact => fact.RuleId, StringComparer.Ordinal)
                         .ThenBy(fact => fact.FactType, StringComparer.Ordinal)
                         .ThenBy(fact => fact.Evidence?.FilePath ?? string.Empty, StringComparer.Ordinal)
                         .ThenBy(fact => fact.Evidence?.StartLine ?? 0)
                         .ThenBy(fact => fact.FactId, StringComparer.Ordinal))
            {
                var evidence = fact.Evidence;
                if (evidence is null)
                {
                    gaps.Add(CreateGap(
                        $"missing-evidence-span-{Hash(fact.FactId, 16)}",
                        PartialSectionRuleId,
                        "missing-evidence-span",
                        "artifact:facts-ndjson",
                        "evidence-rows",
                        coverageLabels.Count == 0 ? "UnknownCoverage" : coverageLabels.First(),
                        "An evidence row did not include a file span, so the explorer rendered the row with partial span metadata.",
                        [fact.FactId]));
                }

                var safePath = evidence is null ? null : SafeRepositoryPath(evidence.FilePath, redactions);
                evidenceRows.Add(new ExplorerEvidenceRow(
                    $"evidence:{Hash(fact.FactId, 24)}",
                    SafeClosedText(fact.RuleId, "rule-id", redactions),
                    SafeEvidenceTier(fact.EvidenceTier, gaps),
                    SafeClosedText(fact.FactType, "fact-type", redactions),
                    SafeClosedText(fact.FactId, "support-id", redactions),
                    "artifact:facts-ndjson",
                    SourceId,
                    IsUsableCommitSha(fact.CommitSha) ? fact.CommitSha : null,
                    safePath,
                    evidence?.StartLine,
                    evidence?.EndLine,
                    evidence is null ? "n/a" : SafeSnippetHash(evidence.SnippetHash, redactions),
                    coverageLabels.Count == 0 ? "UnknownCoverage" : coverageLabels.First(),
                    SafeClosedText(evidence?.ExtractorVersion, "extractor-version", redactions),
                    []));
                RecordOmittedFactProperties(fact, redactions);

                if (fact.FactType == FactTypes.AnalysisGap)
                {
                    var ruleId = string.IsNullOrWhiteSpace(fact.RuleId) ? PartialSectionRuleId : fact.RuleId;
                    gaps.Add(CreateGap(
                        $"analysis-gap-{Hash(fact.FactId, 16)}",
                        SafeClosedText(ruleId, "rule-id", redactions),
                        "analysis-gap",
                        SourceId,
                        "coverage",
                        coverageLabels.Count == 0 ? "UnknownCoverage" : coverageLabels.First(),
                        "Input facts contain an AnalysisGap row. The explorer preserves it as a coverage limitation without deriving a new conclusion.",
                        [fact.FactId]));
                }
            }
        }
        else
        {
            gaps.Add(CreateGap(
                "missing-facts-ndjson",
                PartialSectionRuleId,
                "not-provided",
                "input-directory",
                "evidence-rows",
                "PartialAnalysis",
                "facts.ndjson was not provided, so evidence-row tables are unavailable rather than empty.",
                []));
        }

        await AddOptionalArtifactAsync(inputDirectory, "index.sqlite", "sqlite-index", "SQLite index", "index.sqlite.v1", safetyProfile, artifacts, gaps, cancellationToken);
        await AddOptionalArtifactAsync(inputDirectory, "report.md", "markdown-report", "Markdown report", "report.md.v1", safetyProfile, artifacts, gaps, cancellationToken);
        await AddReleaseReviewArtifactAsync(
            inputDirectory,
            safetyProfile,
            sourceCommitSha,
            sourceCommitSupportId,
            artifacts,
            gaps,
            limitations,
            cancellationToken);
        await AddPathsReportArtifactAsync(
            inputDirectory,
            safetyProfile,
            sourceCommitSha,
            artifacts,
            reportSources,
            surfaces,
            paths,
            evidenceRows,
            gaps,
            limitations,
            redactions,
            cancellationToken);
        var catalogLoad = await AddRuleCatalogArtifactAsync(inputDirectory, safetyProfile, artifacts, gaps, redactions, cancellationToken);
        var catalogRules = catalogLoad.Entries;
        await AddUnsupportedJsonArtifactsAsync(inputDirectory, safetyProfile, artifacts, gaps, cancellationToken);

        limitations.Add(CreateLimitation(
            "claim-level-metadata-unavailable",
            CompatibilityLedgerRuleId,
            "claim-level-metadata-unknown",
            "artifacts",
            "claim-level",
            "Compatible first-slice inputs do not expose independent claim-level metadata. The selected output safety profile governs rendering, and no claim-level conflict is inferred from unknown metadata.",
            ["input-directory"]));

        if (artifacts.All(artifact => artifact.ArtifactKind != "sqlite-index"))
        {
            gaps.Add(CreateGap(
                "index-not-provided",
                PartialSectionRuleId,
                "not-provided",
                "input-directory",
                "artifacts",
                "PartialAnalysis",
                "index.sqlite was not provided. The first explorer slice records this as unavailable and does not read raw SQLite content.",
                []));
        }

        var builtInRuleIds = BuiltInExplorerRules().Select(rule => rule.RuleId).ToHashSet(StringComparer.Ordinal);
        var observedRuleIds = evidenceRows
            .Select(row => row.RuleId)
            .Where(ruleId => !builtInRuleIds.Contains(ruleId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(ruleId => ruleId, StringComparer.Ordinal)
            .ToArray();
        var catalogRuleIds = catalogRules
            .Select(rule => rule.RuleId)
            .ToHashSet(StringComparer.Ordinal);
        var observedRulesWithoutCatalog = observedRuleIds
            .Where(ruleId => !catalogRuleIds.Contains(ruleId))
            .ToArray();
        if (observedRulesWithoutCatalog.Length > 0 && (!catalogLoad.FilePresent || catalogRules.Count > 0))
        {
            var catalogProvided = catalogLoad.FilePresent;
            var observedArtifactIds = evidenceRows
                .Where(row => observedRulesWithoutCatalog.Contains(row.RuleId, StringComparer.Ordinal))
                .Select(row => row.ArtifactId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var observedScope = observedArtifactIds.Length == 1 ? observedArtifactIds[0] : "input-directory";
            gaps.Add(CreateGap(
                catalogProvided ? "rule-catalog-observed-entry-unavailable" : "rule-catalog-unavailable",
                CatalogUnavailableRuleId,
                catalogProvided ? "catalog-entry-unavailable" : "catalog-unavailable",
                observedScope,
                "rules",
                coverageLabels.Count == 0 ? "UnknownCoverage" : coverageLabels.First(),
                catalogProvided
                    ? "Compatible evidence inputs reference rule IDs that are not present in the compatible rule catalog artifact; those observed rules remain partial."
                    : "Compatible evidence inputs reference rule IDs that are rendered with observed metadata only because no compatible rule catalog artifact was provided.",
                observedArtifactIds.Concat(observedRulesWithoutCatalog).ToArray()));
        }

        var rules = BuildExplorerRules(evidenceRows, catalogRules);
        var source = BuildSource(manifest, safetyProfile, artifacts, gaps, limitations, redactions, coverageLabels);
        var includePrimarySource = reportSources.Count == 0
            || artifacts.Any(artifact => artifact.SourceIds.Contains(SourceId, StringComparer.Ordinal));
        var sources = (includePrimarySource ? new[] { source } : [])
            .Concat(reportSources)
            .GroupBy(item => item.SourceId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(item => item.SourceId, StringComparer.Ordinal)
            .ToArray();
        var redactionRows = redactions
            .OrderBy(pair => pair.Key.RuleId, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Category, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Location, StringComparer.Ordinal)
            .ThenBy(pair => pair.Key.Action, StringComparer.Ordinal)
            .Select((pair, index) => new ExplorerRedaction(
                $"redaction:{index + 1:D4}",
                pair.Key.RuleId,
                pair.Key.Category,
                pair.Key.Location,
                pair.Key.Action,
                pair.Value))
            .ToArray();

        return new ExplorerBuildContext(
            safetyProfile,
            manifest?.CommitSha is { } sha && IsUsableCommitSha(sha) ? sha : null,
            CoverageStatus(gaps, coverageLabels),
            coverageLabels.Count == 0 ? ["UnknownCoverage"] : coverageLabels.ToArray(),
            sources,
            artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal).ToArray(),
            surfaces.OrderBy(surface => surface.SurfaceKind, StringComparer.Ordinal).ThenBy(surface => surface.SurfaceId, StringComparer.Ordinal).ToArray(),
            paths.OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray(),
            evidenceRows,
            gaps.OrderBy(gap => gap.RuleId, StringComparer.Ordinal).ThenBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            limitations.OrderBy(limitation => limitation.RuleId, StringComparer.Ordinal).ThenBy(limitation => limitation.LimitationId, StringComparer.Ordinal).ToArray(),
            catalogLoad.FilePresent,
            catalogRules.Count > 0,
            rules,
            redactionRows);
    }

    private static ExplorerData BuildData(ExplorerBuildContext context)
    {
        var summary = new ExplorerSummary(
            context.SafetyProfile,
            ClaimLevelForSafetyProfile(context.SafetyProfile),
            context.CoverageStatus,
            context.CommitSha,
            context.Sources.Count,
            context.Artifacts.Count,
            context.Surfaces.Count,
            context.Paths.Count,
            ReducerResultCount: 0,
            context.EvidenceRows.Count,
            context.Gaps.Count,
            context.Limitations.Count,
            context.Rules.Count,
            context.Redactions.Sum(redaction => redaction.Count),
            OmittedCount: context.Gaps.Count(gap => gap.GapKind is "not-provided" or "unsupported"),
            context.CoverageLabels,
            ReducerOutputPresent: false);

        var sectionStatuses = BuildSectionStatuses(context);
        return new ExplorerData(
            SchemaVersion,
            summary,
            sectionStatuses,
            BuildCompatibilityLedger(context, sectionStatuses),
            context.Sources,
            context.Artifacts,
            context.Surfaces,
            context.Paths,
            context.EvidenceRows,
            context.Gaps,
            context.Limitations,
            context.Rules,
            context.Redactions);
    }

    private static ExplorerManifest BuildManifest(ExplorerBuildContext context, ExplorerData data)
    {
        var counts = new ExplorerManifestCounts(
            data.Summary.SourceCount,
            data.Summary.ArtifactCount,
            data.Summary.SurfaceCount,
            data.Summary.PathCount,
            data.Summary.ReducerResultCount,
            data.Summary.EvidenceRowCount,
            data.Summary.GapCount,
            data.Summary.LimitationCount,
            data.Summary.RuleCount,
            data.Summary.RedactionCount,
            data.Summary.OmittedCount);

        return new ExplorerManifest(
            SchemaVersion,
            TracemapGenerated: true,
            new ExplorerGenerator(GeneratorName, SchemaVersion, Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "unknown"),
            context.SafetyProfile,
            ClaimLevelForSafetyProfile(context.SafetyProfile),
            RepoIdentityPolicy: context.CommitSha is null ? "omitted-for-safety" : "commit-sha-only",
            GenerationTimestampPolicy: "omitted-deterministic",
            GeneratedAt: null,
            RepositoryIdentifier: context.CommitSha is null ? SourceId : $"commit:{context.CommitSha}",
            context.CommitSha,
            context.CoverageStatus,
            counts,
            context.Artifacts,
            context.Redactions,
            context.Gaps,
            context.Limitations);
    }

    private static Dictionary<string, string> BuildGeneratedFiles(ExplorerManifest manifest, ExplorerData data)
    {
        var manifestJson = SerializeJson(manifest);
        var dataJson = SerializeJson(data);
        return new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["index.html"] = RenderHtml(data),
            ["assets/explorer.css"] = Css(),
            ["assets/explorer.js"] = JavaScript(),
            ["data/explorer-manifest.json"] = manifestJson,
            ["data/explorer-data.json"] = dataJson,
            ["README.md"] = Readme(manifest)
        };
    }

    private static async Task<T> ReadJsonAsync<T>(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var value = await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken);
        return value ?? throw new InvalidOperationException($"UnsupportedSchema: {UnsupportedSchemaRuleId} [{Tier4Unknown}]: unreadable-json at input artifact.");
    }

    private static async Task<IReadOnlyList<CodeFact>> ReadFactsAsync(string path, CancellationToken cancellationToken)
    {
        var facts = new List<CodeFact>();
        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var fact = JsonSerializer.Deserialize<CodeFact>(line, JsonOptions);
            if (fact is not null)
            {
                facts.Add(fact);
            }
        }

        return facts;
    }

    private static async Task AddOptionalArtifactAsync(
        string inputDirectory,
        string fileName,
        string artifactKind,
        string safeLabel,
        string schemaVersion,
        string safetyProfile,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        CancellationToken cancellationToken)
    {
        var path = Path.Combine(inputDirectory, fileName);
        if (!File.Exists(path))
        {
            return;
        }

        artifacts.Add(new ExplorerInputArtifact(
            $"artifact:{artifactKind}",
            artifactKind,
            safeLabel,
            await HashFileAsync(path, cancellationToken),
            schemaVersion,
            ClaimLevelForSafetyProfile(safetyProfile),
            [],
            [SourceId],
            [],
            [],
            "supported-provenance-only"));

        if (artifactKind == "sqlite-index")
        {
            gaps.Add(CreateGap(
                "sqlite-content-not-rendered",
                PartialSectionRuleId,
                "unsupported",
                "artifact:sqlite-index",
                "artifacts",
                "PartialAnalysis",
                "index.sqlite was discovered and hashed for provenance, but raw SQLite content is not embedded in the first explorer slice.",
                ["artifact:sqlite-index"]));
        }
    }

    private static async Task AddUnsupportedJsonArtifactsAsync(
        string inputDirectory,
        string safetyProfile,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        CancellationToken cancellationToken)
    {
        foreach (var path in Directory.EnumerateFiles(inputDirectory, "*.json", SearchOption.TopDirectoryOnly)
                     .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal))
        {
            var fileName = Path.GetFileName(path);
            if (fileName.Equals("scan-manifest.json", StringComparison.Ordinal)
                || fileName.Equals("release-review.json", StringComparison.Ordinal)
                || fileName.Equals("paths-report.json", StringComparison.Ordinal)
                || fileName.Equals("explorer-manifest.json", StringComparison.Ordinal)
                || fileName.Equals("explorer-data.json", StringComparison.Ordinal))
            {
                continue;
            }

            var artifactId = $"artifact:unsupported-json:{Hash(fileName, 12)}";
            artifacts.Add(new ExplorerInputArtifact(
                artifactId,
                "unsupported-json",
                "Unsupported JSON artifact",
                await HashFileAsync(path, cancellationToken),
                "unsupported-json.v1",
                ClaimLevelForSafetyProfile(safetyProfile),
                [],
                [],
                [],
                [UnsupportedSchemaRuleId],
                "unsupported"));
            gaps.Add(CreateGap(
                $"unsupported-json-{Hash(fileName, 12)}",
                UnsupportedSchemaRuleId,
                "unsupported-schema",
                artifactId,
                "artifacts",
                "PartialAnalysis",
                "A JSON artifact was discovered but is not supported by the first explorer slice. It is labeled unavailable without rendering raw content.",
                [artifactId]));
        }
    }

    private static async Task AddReleaseReviewArtifactAsync(
        string inputDirectory,
        string safetyProfile,
        string? sourceCommitSha,
        string? sourceCommitSupportId,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        List<ExplorerLimitation> limitations,
        CancellationToken cancellationToken)
    {
        const string artifactId = "artifact:release-review";
        var path = Path.Combine(inputDirectory, "release-review.json");
        if (!File.Exists(path))
        {
            return;
        }

        var snapshot = await ReadBoundedArtifactAsync(path, MaxReleaseReviewBytes, cancellationToken);
        if (snapshot.Content is null)
        {
            AddUnsupportedReleaseReviewArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "artifact-too-large",
                "The release-review artifact exceeded the bounded compatibility-reader size and was not parsed.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                snapshot.Content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64
                });
            var root = RequireObject(document.RootElement, "root");
            if (RequireString(root, "reportType") != "release-review"
                || RequireString(root, "version") != "1.2")
            {
                throw new InvalidDataException("unsupported release-review identity");
            }

            var mode = RequireString(root, "mode");
            var expectedIndexKind = mode switch
            {
                "ReleaseReviewSingleV1" => "single",
                "ReleaseReviewCombinedV1" => "combined",
                _ => throw new InvalidDataException("unsupported release-review mode")
            };
            var beforeSnapshot = ReadReleaseReviewSnapshot(root, "beforeSnapshot", "before", expectedIndexKind);
            var afterSnapshot = ReadReleaseReviewSnapshot(root, "afterSnapshot", "after", expectedIndexKind);
            var summary = RequireObject(RequireProperty(root, "summary"), "summary");
            var truncated = RequireBoolean(summary, "truncated");
            var gapCount = RequireNonNegativeInt32(summary, "gapCount");

            var coverageLabels = new SortedSet<string>(StringComparer.Ordinal)
            {
                $"ReleaseReviewBefore{beforeSnapshot.Coverage}",
                $"ReleaseReviewAfter{afterSnapshot.Coverage}",
                truncated ? "ReleaseReviewTruncated" : "ReleaseReviewNotTruncated",
                gapCount > 0 ? "ReleaseReviewGapsPresent" : "ReleaseReviewNoRecordedGaps"
            };
            IReadOnlyList<string> sourceIds;
            if (sourceCommitSha is null || sourceCommitSupportId is null)
            {
                sourceIds = [];
                gaps.Add(CreateGap(
                    "release-review-source-association-unknown",
                    MissingCommitRuleId,
                    "source-association-unknown",
                    artifactId,
                    "artifacts",
                    "PartialAnalysis",
                    "The explorer could not establish an authoritative scan commit for release-review source association, so the artifact remains unbound.",
                    [artifactId]));
            }
            else if (!afterSnapshot.CommitShas.Contains(sourceCommitSha, StringComparer.OrdinalIgnoreCase))
            {
                sourceIds = [];
                gaps.Add(CreateGap(
                    "release-review-commit-conflict",
                    ProvenanceConflictRuleId,
                    "commit-conflict",
                    artifactId,
                    "artifacts",
                    "PartialAnalysis",
                    "The release-review after snapshot does not contain the authoritative scan commit, so the artifact is not bound to the scan source.",
                    [artifactId, sourceCommitSupportId]));
            }
            else
            {
                sourceIds = [SourceId];
            }

            artifacts.Add(new ExplorerInputArtifact(
                artifactId,
                "release-review",
                "Release review",
                snapshot.ContentHash,
                "release-review/1.2",
                ClaimLevelForSafetyProfile(safetyProfile),
                coverageLabels.ToArray(),
                sourceIds,
                ["limitation:release-review-content-not-rendered"],
                [],
                "supported"));
            limitations.Add(CreateLimitation(
                "release-review-content-not-rendered",
                ReleaseReviewInputRuleId,
                "report-content-not-rendered",
                "artifacts",
                "compatibility-only",
                "The explorer validated release-review compatibility metadata and content identity only. Finding bodies, source labels, paths, messages, metadata, and reducer conclusions are not read or rendered in this slice.",
                [artifactId]));
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            AddUnsupportedReleaseReviewArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "unsupported-schema",
                "The release-review artifact did not match the supported v1.2 compatibility metadata contract and was not rendered.");
        }
    }

    private static void AddUnsupportedReleaseReviewArtifact(
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        string safetyProfile,
        string contentHash,
        string gapKind,
        string message)
    {
        const string artifactId = "artifact:release-review";
        artifacts.Add(new ExplorerInputArtifact(
            artifactId,
            "release-review",
            "Release review",
            contentHash,
            "release-review/unsupported",
            ClaimLevelForSafetyProfile(safetyProfile),
            [],
            [],
            [],
            [UnsupportedSchemaRuleId],
            "unsupported"));
        gaps.Add(CreateGap(
            "release-review-unsupported",
            UnsupportedSchemaRuleId,
            gapKind,
            artifactId,
            "artifacts",
            "PartialAnalysis",
            message,
            [artifactId]));
    }

    private static ReleaseReviewSnapshotMetadata ReadReleaseReviewSnapshot(
        JsonElement root,
        string propertyName,
        string expectedSide,
        string expectedIndexKind)
    {
        var snapshot = RequireObject(RequireProperty(root, propertyName), propertyName);
        if (RequireString(snapshot, "side") != expectedSide
            || RequireString(snapshot, "indexKind") != expectedIndexKind)
        {
            throw new InvalidDataException("release-review snapshot identity mismatch");
        }

        var coverage = RequireString(snapshot, "reportCoverage");
        if (coverage is not ("Full" or "Reduced"))
        {
            throw new InvalidDataException("unsupported release-review coverage");
        }

        var sources = RequireProperty(snapshot, "sources");
        if (sources.ValueKind != JsonValueKind.Array || sources.GetArrayLength() == 0)
        {
            throw new InvalidDataException("release-review sources unavailable");
        }

        var commitShas = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sourceElement in sources.EnumerateArray())
        {
            var source = RequireObject(sourceElement, "source");
            var commitProperties = source.EnumerateObject()
                .Where(property => property.NameEquals("commitSha"))
                .ToArray();
            if (commitProperties.Length != 1)
            {
                throw new InvalidDataException("release-review source commit identity unavailable");
            }

            var commit = commitProperties[0].Value;
            if (commit.ValueKind != JsonValueKind.Null
                && (commit.ValueKind != JsonValueKind.String || !IsUsableCommitSha(commit.GetString())))
            {
                throw new InvalidDataException("release-review source commit identity invalid");
            }

            if (commit.ValueKind == JsonValueKind.String)
            {
                commitShas.Add(commit.GetString()!);
            }
        }

        return new ReleaseReviewSnapshotMetadata(coverage, commitShas.ToArray());
    }

    private static async Task AddPathsReportArtifactAsync(
        string inputDirectory,
        string safetyProfile,
        string? authoritativeCommitSha,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerSource> reportSources,
        List<ExplorerSurface> surfaces,
        List<ExplorerPath> paths,
        List<ExplorerEvidenceRow> evidenceRows,
        List<ExplorerGap> gaps,
        List<ExplorerLimitation> limitations,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        CancellationToken cancellationToken)
    {
        const string artifactId = "artifact:paths-report";
        const string limitationId = "limitation:paths-report-display-text-not-rendered";
        var path = Path.Combine(inputDirectory, "paths-report.json");
        if (!File.Exists(path))
        {
            return;
        }

        var snapshot = await ReadBoundedArtifactAsync(path, MaxPathsReportBytes, cancellationToken);
        if (snapshot.Content is null)
        {
            AddUnsupportedPathsReportArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "artifact-too-large",
                "The paths report exceeded the bounded reader size and was not parsed.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                snapshot.Content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 96
                });
            var root = RequireObject(document.RootElement, "root");
            foreach (var required in new[] { "version", "reportCoverage", "sources", "summary", "paths", "gaps", "limitations" })
            {
                _ = RequireProperty(root, required);
            }

            var schemaVersionProperties = root.EnumerateObject()
                .Where(property => property.NameEquals("schemaVersion"))
                .ToArray();
            if (schemaVersionProperties.Length > 1
                || (schemaVersionProperties.Length == 1 && schemaVersionProperties[0].Value.ValueKind != JsonValueKind.Null))
            {
                throw new InvalidDataException("unsupported paths report schema variant");
            }

            if (RequireString(root, "version") != "1.0")
            {
                throw new InvalidDataException("unsupported paths report version");
            }

            var report = JsonSerializer.Deserialize<CombinedDependencyPathReport>(snapshot.Content, JsonOptions)
                ?? throw new InvalidDataException("paths report unavailable");
            ValidatePathsReport(report);

            var orderedSources = report.Sources
                .OrderBy(source => source.SourceIndexId, StringComparer.Ordinal)
                .ToArray();
            var authoritativeMatches = authoritativeCommitSha is null
                ? 0
                : orderedSources.Count(source => source.CommitSha.Equals(authoritativeCommitSha, StringComparison.OrdinalIgnoreCase));
            var sourceIdByIndex = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < orderedSources.Length; index++)
            {
                var reportSource = orderedSources[index];
                var sourceId = authoritativeMatches == 1
                    && reportSource.CommitSha.Equals(authoritativeCommitSha, StringComparison.OrdinalIgnoreCase)
                        ? SourceId
                        : $"source:paths:{Hash(reportSource.SourceIndexId, 24)}";
                sourceIdByIndex.Add(reportSource.SourceIndexId, sourceId);
                if (sourceId == SourceId)
                {
                    continue;
                }

                reportSources.Add(new ExplorerSource(
                    sourceId,
                    $"Paths report source {index + 1:D2}",
                    "combined-path-source",
                    ClaimLevelForSafetyProfile(safetyProfile),
                    report.ReportCoverage == "FullEvidenceAvailable" ? "available" : "reduced",
                    reportSource.CommitSha,
                    [SafeClosedText(reportSource.ScannerVersion, "paths-report.scanner-version", redactions)],
                    [artifactId],
                    report.Gaps.Count,
                    1,
                    0,
                    0));
            }

            var coverageLabels = new SortedSet<string>(StringComparer.Ordinal)
            {
                report.ReportCoverage == "FullEvidenceAvailable" ? "PathsFullEvidenceAvailable" : "PathsReducedCoverage",
                report.Summary.Truncated ? "PathsTruncated" : "PathsNotTruncated",
                report.Gaps.Count > 0 ? "PathsGapsPresent" : "PathsNoRecordedGaps"
            };
            artifacts.Add(new ExplorerInputArtifact(
                artifactId,
                "paths-report",
                "Static dependency paths report",
                snapshot.ContentHash,
                "paths-report/1.0",
                ClaimLevelForSafetyProfile(safetyProfile),
                coverageLabels.ToArray(),
                sourceIdByIndex.Values.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                [limitationId],
                [],
                "supported"));
            limitations.Add(CreateLimitation(
                "paths-report-display-text-not-rendered",
                PathsReportInputRuleId,
                "report-display-text-not-rendered",
                "paths",
                "display",
                "The explorer renders closed path and surface categories plus evidence provenance only. Query selectors, source labels, node display names, surface names, notes, and free-text report limitations are omitted.",
                [artifactId]));

            var sourceByIndex = orderedSources.ToDictionary(source => source.SourceIndexId, StringComparer.Ordinal);
            var surfaceRows = new Dictionary<string, ExplorerSurface>(StringComparer.Ordinal);
            foreach (var reportPath in report.Paths.OrderBy(item => item.PathId, StringComparer.Ordinal))
            {
                var safePathId = $"path:{Hash(reportPath.PathId, 24)}";
                var hops = new List<ExplorerPathHop>();
                for (var index = 0; index < reportPath.Edges.Count; index++)
                {
                    var edge = reportPath.Edges[index];
                    var fromNode = reportPath.Nodes[index];
                    var toNode = reportPath.Nodes[index + 1];
                    // Endpoint-match evidence is emitted at the matched server endpoint.
                    // Other path-edge evidence is emitted at the originating node.
                    var evidenceNode = edge.EdgeKind == "endpoint-match" ? toNode : fromNode;
                    var sourceId = sourceIdByIndex[evidenceNode.SourceIndexId];
                    var source = sourceByIndex[evidenceNode.SourceIndexId];
                    var extractorVersion = SafeClosedText(source.ScannerVersion, "paths-report.scanner-version", redactions);
                    var supportIds = SafeSupportIds(edge.SupportingFactIds.Concat(edge.SupportingCombinedEdgeIds));
                    var hop = new ExplorerPathHop(
                        $"hop:{Hash($"{reportPath.PathId}:{index}:{edge.EdgeId}", 24)}",
                        index + 1,
                        edge.EdgeKind,
                        edge.RuleId,
                        edge.EvidenceTier,
                        $"node:{Hash(edge.FromNodeId, 24)}",
                        $"node:{Hash(edge.ToNodeId, 24)}",
                        sourceId,
                        source.CommitSha,
                        extractorVersion,
                        SafeOptionalRepositoryPath(edge.FilePath, redactions),
                        edge.StartLine,
                        edge.EndLine,
                        supportIds,
                        [limitationId]);
                    hops.Add(hop);
                    evidenceRows.Add(new ExplorerEvidenceRow(
                        $"evidence:{Hash(hop.HopId, 24)}",
                        hop.RuleId,
                        hop.EvidenceTier,
                        "path-hop",
                        hop.SupportIds.FirstOrDefault() ?? hop.HopId,
                        artifactId,
                        sourceId,
                        source.CommitSha,
                        hop.FilePath,
                        hop.StartLine,
                        hop.EndLine,
                        null,
                        report.ReportCoverage,
                        extractorVersion,
                        [limitationId]));
                }

                paths.Add(new ExplorerPath(
                    safePathId,
                    "dependency-path",
                    reportPath.Classification,
                    reportPath.Confidence,
                    report.ReportCoverage,
                    artifactId,
                    hops.OrderBy(hop => hop.Sequence).ThenBy(hop => hop.HopId, StringComparer.Ordinal).ToArray(),
                    SafeSupportIds(reportPath.SupportingFactIds.Concat(reportPath.SupportingEdgeIds)),
                    [limitationId]));

                foreach (var node in reportPath.Nodes.Where(node => !string.IsNullOrWhiteSpace(node.SurfaceKind)))
                {
                    var surfaceId = $"surface:{Hash(node.NodeId, 24)}";
                    if (surfaceRows.ContainsKey(surfaceId))
                    {
                        continue;
                    }

                    var sourceId = sourceIdByIndex[node.SourceIndexId];
                    var source = sourceByIndex[node.SourceIndexId];
                    var extractorVersion = SafeClosedText(source.ScannerVersion, "paths-report.scanner-version", redactions);
                    var supportIds = SafeSupportIds(node.CombinedFactId is null ? [] : [node.CombinedFactId]);
                    var surface = new ExplorerSurface(
                        surfaceId,
                        node.SurfaceKind!,
                        null,
                        $"Static {node.SurfaceKind} surface",
                        reportPath.Classification,
                        node.RuleId!,
                        node.EvidenceTier!,
                        report.ReportCoverage,
                        artifactId,
                        sourceId,
                        node.CommitSha ?? source.CommitSha,
                        extractorVersion,
                        SafeOptionalRepositoryPath(node.FilePath, redactions),
                        node.StartLine,
                        node.EndLine,
                        supportIds,
                        [limitationId]);
                    surfaceRows.Add(surfaceId, surface);
                    evidenceRows.Add(new ExplorerEvidenceRow(
                        $"evidence:{Hash(surfaceId, 24)}",
                        surface.RuleId,
                        surface.EvidenceTier,
                        "dependency-surface",
                        supportIds.FirstOrDefault() ?? surfaceId,
                        artifactId,
                        sourceId,
                        surface.CommitSha,
                        surface.FilePath,
                        surface.StartLine,
                        surface.EndLine,
                        null,
                        surface.CoverageLabel,
                        extractorVersion,
                        [limitationId]));
                }
            }

            surfaces.AddRange(surfaceRows.Values);
            foreach (var reportGap in report.Gaps.OrderBy(item => item.GapId, StringComparer.Ordinal))
            {
                var ruleId = IsSafeRuleId(reportGap.RuleId) ? reportGap.RuleId! : PathsReportInputRuleId;
                var tier = IsSupportedEvidenceTier(reportGap.EvidenceTier) ? reportGap.EvidenceTier! : Tier4Unknown;
                gaps.Add(CreateGap(
                    $"paths-report-{Hash(reportGap.GapId, 16)}",
                    ruleId,
                    "paths-report-gap",
                    artifactId,
                    "paths",
                    report.ReportCoverage,
                    "The paths report preserved a rule-backed static path analysis gap; the explorer does not reinterpret its free-text message.",
                    SafeSupportIds(reportGap.EffectiveSupportingFactIds).Append(artifactId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    tier));
            }

            RecordOmittedPathsReportText(report, redactions);
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            AddUnsupportedPathsReportArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "unsupported-schema",
                "The paths report did not match the supported v1.0 static dependency-path contract and was not rendered.");
        }
    }

    private static void ValidatePathsReport(CombinedDependencyPathReport report)
    {
        if (report.Version != "1.0"
            || report.SchemaVersion is not null
            || report.ReportCoverage is not ("FullEvidenceAvailable" or "ReducedCoverage")
            || report.CoverageWarnings is null
            || report.Query is null
            || report.Sources is null
            || report.Summary is null
            || report.Paths is null
            || report.Gaps is null
            || report.Inventory is null
            || report.Limitations is null
            || report.Sources.Count == 0
            || report.Sources.Count > MaxPathsReportSources
            || report.Paths.Count > MaxPathsReportPaths
            || report.Paths.Sum(path => path?.Edges?.Count ?? 0) > MaxPathsReportHops
            || report.Summary.SourceCount != report.Sources.Count
            || report.Summary.PathCount != report.Paths.Count
            || report.Summary.GapCount != report.Gaps.Count)
        {
            throw new InvalidDataException("paths report contract mismatch");
        }

        var sourceIds = new HashSet<string>(StringComparer.Ordinal);
        var commitBySourceIndex = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in report.Sources)
        {
            if (source is null
                || string.IsNullOrWhiteSpace(source.SourceIndexId)
                || !sourceIds.Add(source.SourceIndexId)
                || !IsUsableCommitSha(source.CommitSha)
                || string.IsNullOrWhiteSpace(source.ScannerVersion))
            {
                throw new InvalidDataException("paths report source identity unavailable");
            }

            commitBySourceIndex.Add(source.SourceIndexId, source.CommitSha);
        }

        var pathIds = new HashSet<string>(StringComparer.Ordinal);
        var gapIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gap in report.Gaps)
        {
            if (gap is null
                || string.IsNullOrWhiteSpace(gap.GapId)
                || !gapIds.Add(gap.GapId)
                || string.IsNullOrWhiteSpace(gap.GapKind))
            {
                throw new InvalidDataException("paths report gap identity unavailable");
            }
        }

        foreach (var path in report.Paths)
        {
            if (path is null
                || string.IsNullOrWhiteSpace(path.PathId)
                || !pathIds.Add(path.PathId)
                || !SupportedPathClassifications.Contains(path.Classification)
                || path.Confidence is not ("High" or "Medium" or "Low")
                || path.Nodes is null
                || path.Edges is null
                || path.SupportingFactIds is null
                || path.SupportingEdgeIds is null
                || path.Notes is null
                || path.Nodes.Count == 0
                || path.Edges.Count + 1 != path.Nodes.Count
                || path.Length != path.Edges.Count
                || path.StartNodeId != path.Nodes[0].NodeId
                || path.EndNodeId != path.Nodes[^1].NodeId)
            {
                throw new InvalidDataException("paths report path shape unavailable");
            }

            var nodeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < path.Nodes.Count; index++)
            {
                var node = path.Nodes[index];
                if (node is null
                    || string.IsNullOrWhiteSpace(node.NodeId)
                    || !nodeIds.Add(node.NodeId)
                    || !sourceIds.Contains(node.SourceIndexId)
                    || (!string.IsNullOrWhiteSpace(node.CommitSha) && !IsUsableCommitSha(node.CommitSha))
                    || (!string.IsNullOrWhiteSpace(node.CommitSha)
                        && !commitBySourceIndex[node.SourceIndexId].Equals(node.CommitSha, StringComparison.OrdinalIgnoreCase))
                    || (!string.IsNullOrWhiteSpace(node.SurfaceKind)
                        && (!CombinedTerminalSurfaceKinds.AllSet.Contains(node.SurfaceKind)
                            || !IsSafeRuleId(node.RuleId)
                            || !IsSupportedEvidenceTier(node.EvidenceTier)))
                    || !IsValidSpan(node.StartLine, node.EndLine))
                {
                    throw new InvalidDataException("paths report node evidence unavailable");
                }
            }

            var edgeIds = new HashSet<string>(StringComparer.Ordinal);
            for (var index = 0; index < path.Edges.Count; index++)
            {
                var edge = path.Edges[index];
                if (edge is null
                    || string.IsNullOrWhiteSpace(edge.EdgeId)
                    || !edgeIds.Add(edge.EdgeId)
                    || !SupportedPathEdgeKinds.Contains(edge.EdgeKind)
                    || !SupportedPathEdgeClassifications.Contains(edge.Classification)
                    || !IsSafeRuleId(edge.RuleId)
                    || !IsSupportedEvidenceTier(edge.EvidenceTier)
                    || edge.SupportingFactIds is null
                    || edge.SupportingCombinedEdgeIds is null
                    || edge.FromNodeId != path.Nodes[index].NodeId
                    || edge.ToNodeId != path.Nodes[index + 1].NodeId
                    || !IsValidSpan(edge.StartLine, edge.EndLine))
                {
                    throw new InvalidDataException("paths report hop evidence unavailable");
                }
            }
        }
    }

    private static bool IsValidSpan(int? startLine, int? endLine)
    {
        return startLine is null && endLine is null
            || startLine is > 0 && endLine is > 0 && endLine >= startLine;
    }

    private static string? SafeOptionalRepositoryPath(
        string? value,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        return string.IsNullOrWhiteSpace(value) ? null : SafeRepositoryPath(value, redactions);
    }

    private static bool IsSafeRuleId(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && Regex.IsMatch(value, "^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant);
    }

    private static bool IsSupportedEvidenceTier(string? value)
    {
        return value is EvidenceTiers.Tier1Semantic
            or EvidenceTiers.Tier2Structural
            or EvidenceTiers.Tier3SyntaxOrTextual
            or EvidenceTiers.Tier4Unknown;
    }

    private static IReadOnlyList<string> SafeSupportIds(IEnumerable<string> values)
    {
        return values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => $"support:{Hash(value, 24)}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void RecordOmittedPathsReportText(
        CombinedDependencyPathReport report,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        var omittedCount = report.Sources.Count
            + report.Paths.Sum(path => path.Nodes.Count(node => !string.IsNullOrWhiteSpace(node.DisplayName)))
            + report.Paths.Sum(path => path.Notes?.Count ?? 0)
            + report.Gaps.Count
            + report.Limitations.Count;
        if (omittedCount > 0)
        {
            var key = (OmittedUnsafeValueRuleId, "paths-report-display-text", "paths-report.text", "omit");
            redactions[key] = redactions.TryGetValue(key, out var count) ? count + omittedCount : omittedCount;
        }
    }

    private static void AddUnsupportedPathsReportArtifact(
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        string safetyProfile,
        string contentHash,
        string gapKind,
        string message)
    {
        const string artifactId = "artifact:paths-report";
        artifacts.Add(new ExplorerInputArtifact(
            artifactId,
            "paths-report",
            "Static dependency paths report",
            contentHash,
            "paths-report/unsupported",
            ClaimLevelForSafetyProfile(safetyProfile),
            [],
            [],
            [],
            [UnsupportedSchemaRuleId],
            "unsupported"));
        gaps.Add(CreateGap(
            "paths-report-unsupported",
            UnsupportedSchemaRuleId,
            gapKind,
            artifactId,
            "paths",
            "PartialAnalysis",
            message,
            [artifactId]));
    }

    private static JsonElement RequireProperty(JsonElement element, string propertyName)
    {
        var properties = element.EnumerateObject()
            .Where(property => property.NameEquals(propertyName))
            .ToArray();
        return properties.Length == 1
            ? properties[0].Value
            : throw new InvalidDataException("release-review property unavailable or duplicated");
    }

    private static JsonElement RequireObject(JsonElement element, string scope)
    {
        return element.ValueKind == JsonValueKind.Object
            ? element
            : throw new InvalidDataException($"release-review {scope} is not an object");
    }

    private static string RequireString(JsonElement element, string propertyName)
    {
        var value = RequireProperty(element, propertyName);
        return value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!
            : throw new InvalidDataException("release-review string property unavailable");
    }

    private static bool RequireBoolean(JsonElement element, string propertyName)
    {
        var value = RequireProperty(element, propertyName);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => throw new InvalidDataException("release-review boolean property unavailable")
        };
    }

    private static int RequireNonNegativeInt32(JsonElement element, string propertyName)
    {
        var value = RequireProperty(element, propertyName);
        return value.ValueKind == JsonValueKind.Number
            && value.TryGetInt32(out var number)
            && number >= 0
                ? number
                : throw new InvalidDataException("release-review count property unavailable");
    }

    private static async Task<RuleCatalogLoadResult> AddRuleCatalogArtifactAsync(
        string inputDirectory,
        string safetyProfile,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        CancellationToken cancellationToken)
    {
        var candidates = new[]
            {
                Path.Combine(inputDirectory, "rule-catalog.yml"),
                Path.Combine(inputDirectory, "rules", "rule-catalog.yml")
            }
            .Where(File.Exists)
            .OrderBy(path => Path.GetRelativePath(inputDirectory, path).Replace('\\', '/'), StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0)
        {
            return new RuleCatalogLoadResult(false, []);
        }

        var path = candidates[0];
        var fileInfo = new FileInfo(path);
        var tooLarge = fileInfo.Length > MaxRuleCatalogBytes;
        artifacts.Add(new ExplorerInputArtifact(
            "artifact:rule-catalog",
            "rule-catalog",
            "Rule catalog",
            await HashFileAsync(path, cancellationToken),
            "rule-catalog.yml.v1",
            ClaimLevelForSafetyProfile(safetyProfile),
            [],
            [SourceId],
            [],
            [],
            tooLarge ? "unsupported" : "supported"));

        if (tooLarge)
        {
            gaps.Add(CreateGap(
                "rule-catalog-too-large",
                UnsupportedSchemaRuleId,
                "artifact-too-large",
                "artifact:rule-catalog",
                "rules",
                "PartialAnalysis",
                $"A rule catalog artifact was provided but exceeded the explorer's {MaxRuleCatalogBytes} byte catalog reader limit, so compatible rule rows were not rendered.",
                ["artifact:rule-catalog"]));
            return new RuleCatalogLoadResult(true, []);
        }

        var entries = await ParseRuleCatalogAsync(path, redactions, cancellationToken);
        if (entries.Count == 0)
        {
            gaps.Add(CreateGap(
                "rule-catalog-empty-or-unsupported",
                UnsupportedSchemaRuleId,
                "unsupported-schema",
                "artifact:rule-catalog",
                "rules",
                "PartialAnalysis",
                "A rule catalog artifact was provided but did not contain compatible rule rows for the explorer's conservative catalog reader.",
                ["artifact:rule-catalog"]));
        }

        return new RuleCatalogLoadResult(true, entries);
    }

    private static ExplorerSource BuildSource(
        ScanManifest? manifest,
        string safetyProfile,
        IReadOnlyList<ExplorerInputArtifact> artifacts,
        IReadOnlyList<ExplorerGap> gaps,
        IReadOnlyList<ExplorerLimitation> limitations,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        IReadOnlyCollection<string> coverageLabels)
    {
        var extractorVersions = new SortedSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(manifest?.ScannerVersion))
        {
            extractorVersions.Add(SafeClosedText(manifest.ScannerVersion, "scanner-version", redactions));
        }

        return new ExplorerSource(
            SourceId,
            "TraceMap scan output",
            "generated-artifact-directory",
            ClaimLevelForSafetyProfile(safetyProfile),
            CoverageStatus(gaps, coverageLabels),
            manifest?.CommitSha is { } sha && IsUsableCommitSha(sha) ? sha : null,
            extractorVersions.ToArray(),
            artifacts.Where(artifact => artifact.SourceIds.Contains(SourceId, StringComparer.Ordinal))
                .Select(artifact => artifact.ArtifactId)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            gaps.Count,
            limitations.Count,
            redactions.Values.Sum(),
            OmittedCount: gaps.Count(gap => gap.GapKind is "not-provided" or "unsupported"));
    }

    private static IReadOnlyList<string> CoverageLabelsFromManifest(ScanManifest manifest)
    {
        var labels = new SortedSet<string>(StringComparer.Ordinal)
        {
            SafeCoverageLabel(manifest.AnalysisLevel ?? "UnknownAnalysisLevel"),
            SafeCoverageLabel(manifest.BuildStatus ?? "UnknownBuildStatus")
        };
        foreach (var gap in manifest.KnownGaps ?? [])
        {
            labels.Add(SafeCoverageLabel(gap));
        }

        if (!(manifest.AnalysisLevel ?? string.Empty).Contains("Full", StringComparison.OrdinalIgnoreCase)
            || !(manifest.BuildStatus ?? string.Empty).Equals("Succeeded", StringComparison.OrdinalIgnoreCase))
        {
            labels.Add("PartialAnalysis");
        }

        return labels.Where(label => !string.IsNullOrWhiteSpace(label)).ToArray();
    }

    private static string CoverageStatus(IReadOnlyCollection<ExplorerGap> gaps, IReadOnlyCollection<string> coverageLabels)
    {
        if (gaps.Count > 0)
        {
            return "partial";
        }

        if (coverageLabels.Any(label =>
                label.Contains("Reduced", StringComparison.OrdinalIgnoreCase)
                || label.Contains("Partial", StringComparison.OrdinalIgnoreCase)
                || label.Contains("Failed", StringComparison.OrdinalIgnoreCase)
                || label.Contains("Unknown", StringComparison.OrdinalIgnoreCase)))
        {
            return "reduced";
        }

        return "available";
    }

    private static ExplorerGap CreateGap(
        string idPart,
        string ruleId,
        string gapKind,
        string scope,
        string affectedSection,
        string coverageLabel,
        string message,
        IReadOnlyList<string> supportIds,
        string evidenceTier = Tier4Unknown)
    {
        return new ExplorerGap(
            $"gap:{idPart}",
            ruleId,
            evidenceTier,
            gapKind,
            scope,
            affectedSection,
            coverageLabel,
            message,
            (supportIds.Count == 0 ? [scope] : supportIds)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    private static ExplorerLimitation CreateLimitation(
        string idPart,
        string ruleId,
        string limitationKind,
        string affectedSection,
        string claimEffect,
        string message,
        IReadOnlyList<string> supportIds)
    {
        return new ExplorerLimitation(
            $"limitation:{idPart}",
            ruleId,
            Tier4Unknown,
            limitationKind,
            affectedSection,
            "input-directory",
            claimEffect,
            message,
            (supportIds.Count == 0 ? ["input-directory"] : supportIds)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    private static IReadOnlyList<ExplorerRule> BuildExplorerRules(IReadOnlyList<ExplorerEvidenceRow> evidenceRows, IReadOnlyList<RuleCatalogEntry> catalogRules)
    {
        var builtInRules = BuiltInExplorerRules();
        var rules = builtInRules.ToDictionary(rule => rule.RuleId, StringComparer.Ordinal);
        foreach (var catalogRule in catalogRules)
        {
            if (catalogRule.RuleId.StartsWith("explorer.", StringComparison.Ordinal)
                && rules.ContainsKey(catalogRule.RuleId))
            {
                continue;
            }

            rules[catalogRule.RuleId] = new ExplorerRule(
                catalogRule.RuleId,
                catalogRule.Title,
                catalogRule.Description,
                catalogRule.EvidenceTier,
                catalogRule.Limitations.Count == 0
                    ? [
                        "The compatible rule catalog did not provide limitations for this rule; treat the rendered metadata as partial."
                    ]
                    : catalogRule.Limitations,
                RelatedSectionsForCatalogRule(catalogRule.RuleId, evidenceRows));
        }

        var observedRules = evidenceRows
            .GroupBy(row => row.RuleId, StringComparer.Ordinal)
            .Where(group => !rules.ContainsKey(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ExplorerRule(
                group.Key,
                "Observed evidence rule",
                group.All(row => row.ArtifactId == "artifact:facts-ndjson")
                    ? "Rule ID observed in safe evidence rows from facts.ndjson. Full rule catalog metadata was not provided in this explorer slice."
                    : "Rule ID observed in safe evidence rows from a compatible generated input. Full rule catalog metadata was not provided in this explorer slice.",
                ObservedEvidenceTier(group.Select(row => row.EvidenceTier)),
                [
                    "Observed rule rows preserve safe evidence-row rule IDs only.",
                    "Without a compatible rule catalog artifact, title, description, and limitations are partial and must not strengthen the underlying evidence."
                ],
                RelatedSectionsForObservedEvidence(group)))
            .ToArray();
        foreach (var observedRule in observedRules)
        {
            rules[observedRule.RuleId] = observedRule;
        }

        return rules.Values
            .OrderBy(rule => rule.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RelatedSectionsForCatalogRule(string ruleId, IReadOnlyList<ExplorerEvidenceRow> evidenceRows)
    {
        var sections = new SortedSet<string>(StringComparer.Ordinal)
        {
            "rules"
        };
        var matchingRows = evidenceRows.Where(row => row.RuleId == ruleId).ToArray();
        if (matchingRows.Length > 0)
        {
            sections.Add("evidence-rows");
            if (matchingRows.Any(row => row.EvidenceKind == "path-hop"))
            {
                sections.Add("paths");
            }
            if (matchingRows.Any(row => row.EvidenceKind == "dependency-surface"))
            {
                sections.Add("surfaces");
            }
        }

        if (ruleId.StartsWith("explorer.render.", StringComparison.Ordinal)
            || ruleId.StartsWith("explorer.input.", StringComparison.Ordinal)
            || ruleId.StartsWith("explorer.validation.", StringComparison.Ordinal))
        {
            sections.Add("gaps");
            sections.Add("limitations");
        }

        return sections.ToArray();
    }

    private static IReadOnlyList<string> RelatedSectionsForObservedEvidence(IGrouping<string, ExplorerEvidenceRow> rows)
    {
        var sections = new SortedSet<string>(StringComparer.Ordinal) { "evidence-rows", "rules" };
        if (rows.Any(row => row.EvidenceKind == "path-hop"))
        {
            sections.Add("paths");
        }
        if (rows.Any(row => row.EvidenceKind == "dependency-surface"))
        {
            sections.Add("surfaces");
        }
        return sections.ToArray();
    }

    private static IReadOnlyList<ExplorerRule> BuiltInExplorerRules()
    {
        return
        [
            Rule(UnsupportedSchemaRuleId, "Unsupported explorer input schema", "Marks unsupported generated artifact schemas as unavailable instead of merging them silently."),
            Rule(ProvenanceConflictRuleId, "Explorer provenance conflict", "Marks sections partial when generated artifacts disagree on commit identity or compatible provenance."),
            Rule(MissingCommitRuleId, "Explorer missing commit metadata", "Records missing commit SHA metadata as a source identity gap."),
            Rule(RedactedDisplayValueRuleId, "Explorer redacted display value", "Records values converted to safe stable hashes or closed placeholders before rendering."),
            Rule(OmittedUnsafeValueRuleId, "Explorer omitted unsafe value", "Records unsafe values omitted from public/demo display and downloadable data."),
            Rule(CatalogUnavailableRuleId, "Explorer rule catalog unavailable", "Records that only observed rule IDs and built-in explorer rule stubs are rendered."),
            Rule(NoNetworkAssetsRuleId, "Explorer local no-network assets", "Documents that generated HTML uses only bundled local CSS and JavaScript assets."),
            Rule(PartialSectionRuleId, "Explorer partial section", "Marks unavailable first-slice sections and missing optional artifacts as partial rather than empty."),
            Rule(SectionStatusRuleId, "Explorer section status", "Records deterministic section availability labels derived from compatible generated artifacts and rule-backed gaps."),
            Rule(CompatibilityLedgerRuleId, "Explorer compatibility ledger", "Records deterministic artifact, section, safety-profile, and claim-metadata compatibility states without reading unsupported content."),
            Rule(ReleaseReviewInputRuleId, "Explorer release-review compatibility reader", "Validates bounded release-review v1.2 identity, snapshot shape, coverage, and content provenance without rendering report findings."),
            Rule(PathsReportInputRuleId, "Explorer static paths report reader", "Validates bounded paths-report v1.0 structure and projects only rule-backed static surfaces, ordered hops, provenance, gaps, and limitations."),
            Rule(GeneratedFileStaleRuleId, "Explorer stale generated file", "Prevents overwriting stale generated explorer output without explicit force."),
            Rule(UserFileCollisionRuleId, "Explorer user file collision", "Prevents overwriting user-authored files in an explorer output directory."),
            Rule(UnsafeRejectedRuleId, "Explorer unsafe generated value rejected", "Fails generation when a generated asset contains an unsafe value after redaction.")
        ];
    }

    private static string ObservedEvidenceTier(IEnumerable<string> tiers)
    {
        var distinct = tiers
            .Where(tier => tier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural or EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(tier => tier, StringComparer.Ordinal)
            .ToArray();
        return distinct.Length == 1 ? distinct[0] : Tier4Unknown;
    }

    private static IReadOnlyList<ExplorerSectionStatus> BuildSectionStatuses(ExplorerBuildContext context)
    {
        var factsProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "facts-ndjson");
        var sqliteProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "sqlite-index");
        var reportProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "markdown-report");
        var pathsReport = context.Artifacts.FirstOrDefault(artifact => artifact.ArtifactKind == "paths-report");
        var compatiblePathsReport = pathsReport?.Compatibility == "supported";
        var ruleCatalogProvided = context.CatalogFilePresent;
        var compatibleRuleCatalogLoaded = context.CatalogRulesLoaded;
        var unsupportedJsonProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "unsupported-json");
        var coverageLabel = context.CoverageLabels.FirstOrDefault() ?? "UnknownCoverage";
        var pathReportReduced = compatiblePathsReport
            && pathsReport!.CoverageLabels.Contains("PathsReducedCoverage", StringComparer.Ordinal);
        var evidenceArtifactProvided = factsProvided || compatiblePathsReport;
        var evidenceRowsStatus = evidenceArtifactProvided
            ? pathReportReduced
                ? "partial"
                : context.EvidenceRows.Count == 0
                ? "no-evidence-under-current-coverage"
                : SectionStatusFromGaps(context.Gaps, "evidence-rows", true)
            : "not-provided";
        var evidenceRowsMessage = evidenceArtifactProvided
            ? context.EvidenceRows.Count == 0
                ? "Compatible evidence artifacts were provided, but no static evidence rows were present under the current coverage."
                : "Evidence rows are rendered from compatible fact and path artifacts after safety filtering and deterministic ordering."
            : "Evidence rows are unavailable because no compatible fact or path artifact was provided.";
        var rows = new List<ExplorerSectionStatus>
        {
            SectionStatus(
                "overview",
                "Evidence Overview",
                context.CoverageStatus,
                coverageLabel,
                "Overview counts are generated from safe explorer view models and preserve partial coverage labels.",
                ["data/explorer-manifest.json", "data/explorer-data.json"]),
            SectionStatus(
                "sources",
                "Sources",
                SectionStatusFromGaps(context.Gaps, "sources", context.Sources.Count > 0),
                coverageLabel,
                "Source identity uses safe generated labels, safe commit SHA when available, and rule-backed gaps for missing identity.",
                context.Sources.Select(source => source.SourceId).ToArray()),
            SectionStatus(
                "artifacts",
                "Artifacts",
                SectionStatusFromGaps(context.Gaps, "artifacts", context.Artifacts.Count > 0),
                coverageLabel,
                unsupportedJsonProvided
                    ? "Artifacts include unsupported JSON entries labeled unavailable without rendering raw content."
                    : "Artifacts are listed by stable ID, schema label, compatibility, and content hash.",
                context.Artifacts.Select(artifact => artifact.ArtifactId).ToArray()),
            SectionStatus(
                "evidence-rows",
                "Evidence Rows",
                evidenceRowsStatus,
                coverageLabel,
                evidenceRowsMessage,
                factsProvided && compatiblePathsReport
                    ? ["artifact:facts-ndjson", "artifact:paths-report"]
                    : factsProvided
                        ? ["artifact:facts-ndjson"]
                        : compatiblePathsReport
                            ? ["artifact:paths-report"]
                            : ["input-directory"]),
            SectionStatus(
                "surfaces",
                "Surfaces",
                compatiblePathsReport
                    ? pathReportReduced
                        ? "partial"
                        : context.Surfaces.Count == 0
                        ? context.Gaps.Any(gap => gap.AffectedSection == "surfaces") ? "partial" : "no-evidence-under-current-coverage"
                        : SectionStatusFromGaps(context.Gaps, "surfaces", true)
                    : pathsReport is not null ? "unsupported-schema" : sqliteProvided ? "not-rendered-in-current-slice" : "not-provided",
                compatiblePathsReport ? pathsReport!.CoverageLabels.FirstOrDefault() ?? "UnknownCoverage" : "PartialAnalysis",
                compatiblePathsReport
                    ? context.Surfaces.Count == 0
                        ? "A compatible paths report was provided, but no closed static dependency-surface rows were present under its recorded coverage."
                        : "Static dependency surfaces are rendered from the compatible paths report with rule, tier, provenance, and limitation metadata."
                    : pathsReport is not null
                        ? "The paths report schema was unsupported, so surface rows are unavailable rather than absent."
                        : sqliteProvided
                            ? "index.sqlite was hashed as provenance, but static surface extraction from SQLite is deferred in this explorer slice."
                            : "Surface rendering requires a compatible surface artifact or future SQLite reader and is unavailable here.",
                compatiblePathsReport || pathsReport is not null ? ["artifact:paths-report"] : sqliteProvided ? ["artifact:sqlite-index"] : ["input-directory"]),
            SectionStatus(
                "paths",
                "Paths",
                compatiblePathsReport
                    ? pathReportReduced
                        ? "partial"
                        : context.Paths.Count == 0
                        ? context.Gaps.Any(gap => gap.AffectedSection == "paths") ? "partial" : "no-evidence-under-current-coverage"
                        : SectionStatusFromGaps(context.Gaps, "paths", true)
                    : pathsReport is not null ? "unsupported-schema" : sqliteProvided ? "not-rendered-in-current-slice" : "not-provided",
                compatiblePathsReport ? pathsReport!.CoverageLabels.FirstOrDefault() ?? "UnknownCoverage" : "PartialAnalysis",
                compatiblePathsReport
                    ? context.Paths.Count == 0
                        ? "A compatible paths report was provided, but no static dependency paths were present under its recorded coverage."
                        : "Static dependency paths preserve deterministic hop order and existing rule-backed classifications without runtime or impact claims."
                    : pathsReport is not null
                        ? "The paths report schema was unsupported, so path rows are unavailable rather than absent."
                        : sqliteProvided
                            ? "index.sqlite was hashed as provenance, but dependency and route path rendering from SQLite is deferred in this explorer slice."
                            : "Path rendering requires a compatible path artifact or future SQLite reader and is unavailable here.",
                compatiblePathsReport || pathsReport is not null ? ["artifact:paths-report"] : sqliteProvided ? ["artifact:sqlite-index"] : ["input-directory"]),
            SectionStatus(
                "reducer-results",
                "Reducer Results",
                reportProvided ? "not-rendered-in-current-slice" : "not-provided",
                "PartialAnalysis",
                reportProvided
                    ? "Markdown report input was hashed as provenance, but reducer-backed result parsing is deferred until a compatible structured reducer artifact is provided."
                    : "Reducer-backed rows are not provided; scanner-only rows are not described as impact.",
                reportProvided ? ["artifact:markdown-report"] : ["input-directory"]),
            SectionStatus(
                "rules",
                "Rules",
                ruleCatalogProvided ? SectionStatusFromGaps(context.Gaps, "rules", true) : "built-in-stubs",
                coverageLabel,
                compatibleRuleCatalogLoaded
                    ? "Rules include compatible rule catalog rows plus built-in explorer rules and observed fallback rows for any uncataloged evidence rule IDs."
                    : ruleCatalogProvided
                    ? "A rule catalog artifact was provided, but no compatible rule rows were loaded; rules use built-in explorer rules and observed fallback rows."
                    : "The explorer renders built-in explorer rules and observed rule IDs; no compatible full rule catalog artifact was provided.",
                ruleCatalogProvided ? ["artifact:rule-catalog"] : context.Rules.Select(rule => rule.RuleId).ToArray()),
            SectionStatus(
                "redactions",
                "Safety & Redactions",
                context.Redactions.Count == 0 ? "none-recorded" : "recorded",
                coverageLabel,
                context.Redactions.Count == 0
                    ? "No redaction rows were recorded for the compatible first-slice inputs."
                    : "Unsafe values were redacted, hashed, categorized, or omitted before visible UI and embedded data were written.",
                ["data/explorer-data.json", "data/explorer-manifest.json"])
        };

        return rows.ToArray();
    }

    private static IReadOnlyList<ExplorerCompatibilityRow> BuildCompatibilityLedger(
        ExplorerBuildContext context,
        IReadOnlyList<ExplorerSectionStatus> sectionStatuses)
    {
        var coverageLabel = context.CoverageLabels.FirstOrDefault() ?? "UnknownCoverage";
        var rows = new List<ExplorerCompatibilityRow>();
        var artifactsByKind = context.Artifacts
            .GroupBy(artifact => artifact.ArtifactKind, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.ToArray(), StringComparer.Ordinal);

        foreach (var expected in ExpectedArtifactSubjects())
        {
            if (!artifactsByKind.TryGetValue(expected.ArtifactKind, out var matchingArtifacts))
            {
                rows.Add(CompatibilityRow(
                    "artifact",
                    expected.ArtifactId,
                    expected.SafeLabel,
                    "not-provided",
                    PartialSectionRuleId,
                    coverageLabel,
                    "generated-input-artifact",
                    ["input-directory"],
                    [],
                    $"{expected.SafeLabel} was not provided; this compatibility state does not prove evidence absence."));
                continue;
            }

            foreach (var artifact in matchingArtifacts)
            {
                rows.Add(BuildArtifactCompatibilityRow(context, artifact, coverageLabel));
            }
        }

        foreach (var artifact in context.Artifacts
                     .Where(artifact => !ExpectedArtifactSubjects().Any(expected => expected.ArtifactKind == artifact.ArtifactKind)))
        {
            rows.Add(BuildArtifactCompatibilityRow(context, artifact, coverageLabel));
        }

        foreach (var section in sectionStatuses)
        {
            var sectionGapIds = context.Gaps
                .Where(gap => section.SectionId == "overview" || gap.AffectedSection == section.SectionId)
                .Select(gap => gap.GapId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var compatibilityStatus = section.Status switch
            {
                _ when sectionGapIds.Length > 0 => "partial",
                "not-provided" => "not-provided",
                "not-rendered-in-current-slice" => "provenance-only",
                "no-evidence-under-current-coverage" or "none-recorded" => "compatible-empty",
                "partial" or "built-in-stubs" => "partial",
                _ => "compatible"
            };
            var ruleId = compatibilityStatus is "not-provided" or "provenance-only" or "partial"
                ? PartialSectionRuleId
                : CompatibilityLedgerRuleId;
            rows.Add(CompatibilityRow(
                "section",
                section.SectionId,
                section.Label,
                compatibilityStatus,
                ruleId,
                section.CoverageLabel,
                "generated-explorer-section",
                section.SupportIds,
                compatibilityStatus == "partial" ? sectionGapIds : [],
                SectionCompatibilityMessage(section.Label, compatibilityStatus)));
        }

        rows.Add(CompatibilityRow(
            "safety-profile",
            $"safety-profile:{context.SafetyProfile}",
            context.SafetyProfile == PublicDemo ? "Public/demo safety profile" : "Hidden/local safety profile",
            "compatible",
            CompatibilityLedgerRuleId,
            coverageLabel,
            "selected-output-profile",
            ["data/explorer-manifest.json", "data/explorer-data.json"],
            [],
            "The selected output safety profile controls rendering and generated-output validation."));

        rows.Add(CompatibilityRow(
            "claim-level",
            "claim-level:unknown",
            "Artifact claim metadata",
            "partial",
            CompatibilityLedgerRuleId,
            coverageLabel,
            "artifact-claim-metadata",
            ["input-directory"],
            ["claim-level-metadata-unavailable"],
            "Compatible first-slice inputs do not expose independent claim-level metadata; unknown metadata is not treated as a conflict."));

        return rows
            .OrderBy(row => row.SubjectKind, StringComparer.Ordinal)
            .ThenBy(row => row.SubjectId, StringComparer.Ordinal)
            .ThenBy(row => row.CompatibilityStatus, StringComparer.Ordinal)
            .ThenBy(row => row.RuleId, StringComparer.Ordinal)
            .ThenBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();
    }

    private static ExplorerCompatibilityRow BuildArtifactCompatibilityRow(
        ExplorerBuildContext context,
        ExplorerInputArtifact artifact,
        string coverageLabel)
    {
        var affectedSection = artifact.ArtifactKind switch
        {
            "scan-manifest" => "sources",
            "facts-ndjson" => "evidence-rows",
            "paths-report" => "paths",
            "rule-catalog" => "rules",
            _ => "artifacts"
        };
        var relatedGaps = context.Gaps
            .Where(gap => gap.AffectedSection == affectedSection
                && (gap.Scope == artifact.ArtifactId || gap.SupportIds.Contains(artifact.ArtifactId, StringComparer.Ordinal)))
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        var commitConflict = relatedGaps.Any(gap => gap.GapKind == "commit-conflict");
        var unsupportedSchema = relatedGaps.Any(gap => gap.GapKind is "unsupported-schema" or "artifact-too-large");
        var partialInput = relatedGaps.Any(gap => gap.GapKind is not ("unsupported-schema" or "artifact-too-large" or "unsupported"));
        var status = artifact.Compatibility switch
        {
            "supported-provenance-only" => "provenance-only",
            _ when unsupportedSchema => "unsupported-schema",
            "unsupported" => "unsupported-artifact",
            _ when commitConflict => "partial",
            _ when partialInput => "partial",
            "supported" when artifact.ArtifactKind == "facts-ndjson" && context.EvidenceRows.Count == 0 => "compatible-empty",
            "supported" => "rendered-compatible",
            _ => "partial"
        };
        var ruleId = status switch
        {
            "unsupported-schema" or "unsupported-artifact" => UnsupportedSchemaRuleId,
            "partial" when commitConflict => ProvenanceConflictRuleId,
            "partial" => PartialSectionRuleId,
            _ => CompatibilityLedgerRuleId
        };
        var limitationIds = artifact.Limitations
            .Concat(relatedGaps.Select(gap => gap.GapId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var supportIds = artifact.SourceIds
            .Append(artifact.ArtifactId)
            .Concat(relatedGaps.SelectMany(gap => gap.SupportIds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        return CompatibilityRow(
            "artifact",
            artifact.ArtifactId,
            artifact.SafeLabel,
            status,
            ruleId,
            artifact.CoverageLabels.FirstOrDefault() ?? coverageLabel,
            "generated-input-artifact",
            supportIds,
            limitationIds,
            ArtifactCompatibilityMessage(artifact.SafeLabel, status));
    }

    private static ExplorerCompatibilityRow CompatibilityRow(
        string subjectKind,
        string subjectId,
        string safeLabel,
        string compatibilityStatus,
        string ruleId,
        string coverageLabel,
        string scope,
        IReadOnlyList<string> supportIds,
        IReadOnlyList<string> limitationIds,
        string message)
    {
        return new ExplorerCompatibilityRow(
            $"compatibility:{subjectKind}:{Hash(subjectId, 20)}",
            subjectKind,
            subjectId,
            safeLabel,
            compatibilityStatus,
            ruleId,
            Tier4Unknown,
            coverageLabel,
            scope,
            supportIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            limitationIds.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            message);
    }

    private static IReadOnlyList<(string ArtifactId, string ArtifactKind, string SafeLabel)> ExpectedArtifactSubjects()
    {
        return
        [
            ("artifact:scan-manifest", "scan-manifest", "Scan manifest"),
            ("artifact:facts-ndjson", "facts-ndjson", "Fact stream"),
            ("artifact:sqlite-index", "sqlite-index", "SQLite index"),
            ("artifact:markdown-report", "markdown-report", "Markdown report"),
            ("artifact:release-review", "release-review", "Release review"),
            ("artifact:paths-report", "paths-report", "Static dependency paths report"),
            ("artifact:rule-catalog", "rule-catalog", "Rule catalog")
        ];
    }

    private static string ArtifactCompatibilityMessage(string safeLabel, string status)
    {
        return status switch
        {
            "rendered-compatible" => $"{safeLabel} was parsed through a compatible safe reader and contributed rendered explorer data.",
            "compatible-empty" => $"{safeLabel} was parsed through a compatible safe reader and contained no renderable rows under the current coverage.",
            "provenance-only" => $"{safeLabel} was hashed for provenance only; its raw content was not inspected or rendered.",
            "unsupported-schema" => $"{safeLabel} was present but its schema is not supported by the current safe reader.",
            "unsupported-artifact" => $"{safeLabel} was present but is not a supported rendered artifact in this explorer slice.",
            _ => $"{safeLabel} contributed only partial compatible data because a rule-backed input gap applies."
        };
    }

    private static string SectionCompatibilityMessage(string safeLabel, string status)
    {
        return status switch
        {
            "compatible" => $"{safeLabel} is available from compatible safe explorer data.",
            "compatible-empty" => $"{safeLabel} has compatible input but no rows under the current coverage.",
            "provenance-only" => $"{safeLabel} has provenance-only input; raw content was not inspected or rendered.",
            "not-provided" => $"{safeLabel} lacks a compatible input and is unavailable rather than evidence-empty.",
            _ => $"{safeLabel} is partial under the current rule-backed explorer coverage."
        };
    }

    private static ExplorerSectionStatus SectionStatus(
        string sectionId,
        string label,
        string status,
        string coverageLabel,
        string message,
        IReadOnlyList<string> supportIds)
    {
        return new ExplorerSectionStatus(
            sectionId,
            label,
            status,
            SectionStatusRuleId,
            Tier4Unknown,
            coverageLabel,
            message,
            supportIds.OrderBy(value => value, StringComparer.Ordinal).ToArray());
    }

    private static string SectionStatusFromGaps(IReadOnlyList<ExplorerGap> gaps, string section, bool provided)
    {
        if (!provided)
        {
            return "not-provided";
        }

        return gaps.Any(gap => gap.AffectedSection.Equals(section, StringComparison.Ordinal))
            ? "partial"
            : "available";
    }

    private static ExplorerRule Rule(string ruleId, string title, string description)
    {
        return new ExplorerRule(
            ruleId,
            title,
            description,
            Tier4Unknown,
            [
                "Explorer rules describe deterministic rendering, provenance, safety, or generation gaps only.",
                "They do not create scanner or reducer conclusions and do not prove runtime behavior."
            ],
            ["overview", "compatibility-ledger", "gaps", "limitations", "artifacts", "evidence-rows"]);
    }

    private static void RecordOmittedManifestIdentity(
        ScanManifest manifest,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        if (!string.IsNullOrWhiteSpace(manifest.RemoteUrl))
        {
            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(manifest.RemoteUrl) ?? "raw-remote",
                "scan-manifest.remoteUrl",
                "omit");
        }

        if (!string.IsNullOrWhiteSpace(manifest.RepoName))
        {
            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(manifest.RepoName) ?? "repo-name",
                "scan-manifest.repoName",
                "omit");
        }

        if (!string.IsNullOrWhiteSpace(manifest.Branch))
        {
            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(manifest.Branch) ?? "branch-name",
                "scan-manifest.branch",
                "omit");
        }

        foreach (var solution in manifest.Solutions ?? [])
        {
            if (string.IsNullOrWhiteSpace(solution))
            {
                continue;
            }

            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(solution) ?? "solution-name",
                "scan-manifest.solutions",
                "omit");
        }

        foreach (var project in manifest.Projects ?? [])
        {
            if (string.IsNullOrWhiteSpace(project))
            {
                continue;
            }

            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(project) ?? "project-path",
                "scan-manifest.projects",
                "omit");
        }
    }

    private static void RecordOmittedFactProperties(
        CodeFact fact,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        if (fact.Properties is null)
        {
            return;
        }

        foreach (var value in fact.Properties.Values)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            AddRedaction(
                redactions,
                OmittedUnsafeValueRuleId,
                UnsafeCategory(value) ?? "raw-fact-property",
                "facts.properties",
                "omit");
        }
    }

    private static string SafeRepositoryPath(
        string? value,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        var safe = CombinedReportHelpers.SafePath(value);
        if (!safe.Equals(value.Replace('\\', '/'), StringComparison.Ordinal))
        {
            AddRedaction(redactions, RedactedDisplayValueRuleId, "absolute-path", "evidence.filePath", "hash");
        }

        if (UnsafeCategory(safe) is { } category)
        {
            AddRedaction(redactions, RedactedDisplayValueRuleId, category, "evidence.filePath", "hash");
            return $"unsafe-path-hash:{Hash(safe, 16)}";
        }

        return safe;
    }

    private static string SafeClosedText(
        string? value,
        string location,
        Dictionary<(string RuleId, string Category, string Location, string Action), int>? redactions)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "unknown";
        }

        var trimmed = value.Trim();
        if (UnsafeCategory(trimmed) is { } category)
        {
            if (redactions is not null)
            {
                AddRedaction(redactions, RedactedDisplayValueRuleId, category, location, "hash");
            }

            return $"{location}-hash:{Hash(trimmed, 16)}";
        }

        return trimmed;
    }

    private static string SafeSnippetHash(
        string? value,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "n/a";
        }

        if (value.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
        {
            return SafeClosedText(value, "snippet-hash", redactions);
        }

        if (Regex.IsMatch(value, "^[0-9a-fA-F]{16,64}$", RegexOptions.CultureInvariant))
        {
            return $"sha256:{value.ToLowerInvariant()}";
        }

        AddRedaction(redactions, RedactedDisplayValueRuleId, "snippet-hash", "evidence.snippetHash", "hash");
        return $"sha256:{Hash(value)}";
    }

    private static string SafeCoverageLabel(string value)
    {
        return Regex.IsMatch(value, "^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant)
            ? value
            : $"coverage-hash:{Hash(value, 16)}";
    }

    private static string SafeEvidenceTier(string value, List<ExplorerGap> gaps)
    {
        if (value is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural or EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown)
        {
            return value;
        }

        gaps.Add(CreateGap(
            $"unknown-tier-{Hash(value, 12)}",
            UnsupportedSchemaRuleId,
            "unknown-vocabulary",
            "artifact:facts-ndjson",
            "evidence-rows",
            "PartialAnalysis",
            "An evidence row used an unknown evidence tier. The row is downgraded to Tier4Unknown in the explorer.",
            []));
        return EvidenceTiers.Tier4Unknown;
    }

    private static async Task<IReadOnlyList<RuleCatalogEntry>> ParseRuleCatalogAsync(
        string path,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        CancellationToken cancellationToken)
    {
        var entries = new List<RuleCatalogEntry>();
        string? id = null;
        string? title = null;
        string? description = null;
        string? evidenceTier = null;
        var limitations = new List<string>();
        string? listContext = null;

        void Flush()
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return;
            }

            entries.Add(new RuleCatalogEntry(
                SafeRuleCatalogRuleId(id, redactions),
                SafeRuleCatalogText(title, "rule-catalog.name", redactions),
                SafeRuleCatalogText(description, "rule-catalog.description", redactions),
                SafeRuleCatalogEvidenceTier(evidenceTier, redactions),
                limitations
                    .Select(value => SafeRuleCatalogText(value, "rule-catalog.limitations", redactions))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()));
        }

        await using var stream = File.OpenRead(path);
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        while (await reader.ReadLineAsync(cancellationToken) is { } rawLine)
        {
            var line = rawLine.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (trimmed.StartsWith("- id:", StringComparison.Ordinal))
            {
                Flush();
                id = UnquoteYamlScalar(trimmed["- id:".Length..]);
                title = null;
                description = null;
                evidenceTier = null;
                limitations.Clear();
                listContext = null;
                continue;
            }

            if (id is null)
            {
                continue;
            }

            var listSeparator = trimmed.IndexOf(':', StringComparison.Ordinal);
            if (listSeparator > 0
                && listSeparator == trimmed.Length - 1
                && !trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                listContext = trimmed[..listSeparator].Trim();
                continue;
            }

            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
            {
                if (listContext == "limitations")
                {
                    limitations.Add(UnquoteYamlScalar(trimmed[2..]));
                }

                continue;
            }

            var separator = trimmed.IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var key = trimmed[..separator].Trim();
            var value = UnquoteYamlScalar(trimmed[(separator + 1)..]);
            listContext = null;
            switch (key)
            {
                case "name":
                    title = value;
                    break;
                case "description":
                    description = value;
                    break;
                case "evidenceTier":
                    evidenceTier = value;
                    break;
            }
        }

        Flush();
        return entries
            .GroupBy(entry => entry.RuleId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(entry => entry.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static string SafeRuleCatalogRuleId(
        string? value,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        if (!string.IsNullOrWhiteSpace(value) && Regex.IsMatch(value, "^[A-Za-z0-9_.:-]+$", RegexOptions.CultureInvariant))
        {
            return value.Trim();
        }

        var safe = SafeClosedText(value, "rule-id", redactions);
        return safe == "unknown" ? "rule-id:unknown" : safe;
    }

    private static string SafeRuleCatalogText(
        string? value,
        string location,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        var safe = SafeClosedText(value, location, redactions);
        if (safe.Length <= MaxRuleCatalogTextLength)
        {
            return safe;
        }

        return $"{safe[..MaxRuleCatalogTextLength]} [truncated-safe-text-hash:{Hash(safe, 12)}]";
    }

    private static string SafeRuleCatalogEvidenceTier(
        string? value,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
    {
        return string.IsNullOrWhiteSpace(value)
            ? Tier4Unknown
            : SafeRuleCatalogText(value, "rule-catalog.evidenceTier", redactions);
    }

    private static string UnquoteYamlScalar(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2
            && ((trimmed[0] == '"' && trimmed[^1] == '"')
                || (trimmed[0] == '\'' && trimmed[^1] == '\'')))
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static void AddRedaction(
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        string ruleId,
        string category,
        string location,
        string action)
    {
        var key = (ruleId, category, location, action);
        redactions[key] = redactions.TryGetValue(key, out var count) ? count + 1 : 1;
    }

    private static string NormalizeSafetyProfile(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return PublicDemo;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "public-demo" or "demo-safe" or "public-safe" => PublicDemo,
            "hidden-local" or "hidden" or "local-only" => HiddenLocal,
            _ => throw new ArgumentException("explorer generate --safety-profile must be public-demo or hidden-local.")
        };
    }

    private static string ClaimLevelForSafetyProfile(string safetyProfile)
    {
        return safetyProfile == HiddenLocal ? "hidden-local" : "public-safe";
    }

    private static bool IsUsableCommitSha(string? value)
    {
        return !string.IsNullOrWhiteSpace(value)
            && !value.Equals("unknown", StringComparison.OrdinalIgnoreCase)
            && value.Trim('0').Length > 0
            && CommitShaPattern.IsMatch(value);
    }

    private static async Task<string> HashFileAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return $"sha256:{Convert.ToHexString(hash).ToLowerInvariant()}";
    }

    private static async Task<BoundedArtifactSnapshot> ReadBoundedArtifactAsync(
        string path,
        long maxBytes,
        CancellationToken cancellationToken)
    {
        if (new FileInfo(path).Length > maxBytes)
        {
            return new BoundedArtifactSnapshot("unavailable:artifact-too-large", null);
        }

        await using var stream = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        using var content = new MemoryStream();
        var buffer = new byte[81_920];
        long totalBytes = 0;
        int read;
        while (true)
        {
            var bytesUntilDecision = maxBytes - totalBytes + 1;
            var requestedBytes = (int)Math.Min(buffer.Length, bytesUntilDecision);
            read = await stream.ReadAsync(buffer.AsMemory(0, requestedBytes), cancellationToken);
            if (read == 0)
            {
                break;
            }

            totalBytes += read;
            if (totalBytes > maxBytes)
            {
                return new BoundedArtifactSnapshot("unavailable:artifact-too-large", null);
            }

            hash.AppendData(buffer, 0, read);
            await content.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        var contentHash = $"sha256:{Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant()}";
        return new BoundedArtifactSnapshot(contentHash, content.ToArray());
    }

    private static string Hash(string value, int length = 64)
    {
        return CombinedReportHelpers.Hash(value, length);
    }

    private static string SerializeJson<T>(T value)
    {
        return JsonSerializer.Serialize(value, JsonOptions).ReplaceLineEndings("\n") + "\n";
    }

    private static string RenderHtml(ExplorerData data)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<!doctype html>");
        builder.AppendLine("<html lang=\"en\">");
        builder.AppendLine("<head>");
        builder.AppendLine("  <meta charset=\"utf-8\">");
        builder.AppendLine("  <meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
        builder.AppendLine("  <meta name=\"tracemap-generated\" content=\"true\">");
        builder.AppendLine("  <title>TraceMap Local Evidence Explorer</title>");
        // Empty data URI favicon keeps the generated explorer self-contained without embedding remote assets.
        builder.AppendLine("  <link rel=\"icon\" href=\"data:,\">");
        builder.AppendLine("  <link rel=\"stylesheet\" href=\"assets/explorer.css\">");
        builder.AppendLine("</head>");
        builder.AppendLine("<body>");
        builder.AppendLine("  <header>");
        builder.AppendLine("    <p class=\"eyebrow\">Local generated artifact</p>");
        builder.AppendLine("    <h1>TraceMap Evidence Explorer</h1>");
        builder.AppendLine("    <p>This static file set renders existing TraceMap artifacts. It does not rescan source code, contact services, or derive new conclusions.</p>");
        if (data.Summary.SafetyProfile == HiddenLocal)
        {
            builder.AppendLine("    <p class=\"notice\">Hidden/local output. Redaction, hash, category-only, and omission counts are recorded in the manifest.</p>");
        }
        builder.AppendLine("  </header>");
        builder.AppendLine("  <nav aria-label=\"Explorer sections\"><ul>");
        foreach (var (id, label) in Sections())
        {
            builder.AppendLine($"    <li><a href=\"#{id}\">{Html(label)}</a></li>");
        }
        builder.AppendLine("  </ul></nav>");

        builder.AppendLine("  <main>");
        RenderOverview(builder, data.Summary);
        RenderCoverage(builder, data.SectionStatuses);
        RenderCompatibilityLedger(builder, data.CompatibilityLedger);
        RenderSources(builder, data.Sources);
        RenderArtifacts(builder, data.Artifacts);
        RenderSurfaces(builder, data.Surfaces);
        RenderPaths(builder, data.Paths);
        RenderGaps(builder, data.Gaps);
        RenderLimitations(builder, data.Limitations);
        RenderRedactions(builder, data.Redactions);
        RenderRules(builder, data.Rules);
        RenderEvidenceRows(builder, data.EvidenceRows, data.Artifacts.Any(artifact => artifact.ArtifactKind == "facts-ndjson"));
        RenderAbout(builder);
        builder.AppendLine("  </main>");
        builder.AppendLine("  <script src=\"assets/explorer.js\"></script>");
        builder.AppendLine("</body>");
        builder.AppendLine("</html>");
        return builder.ToString().ReplaceLineEndings("\n");
    }

    private static void RenderOverview(StringBuilder builder, ExplorerSummary summary)
    {
        builder.AppendLine("    <section id=\"overview\" aria-labelledby=\"overview-heading\">");
        builder.AppendLine("      <h2 id=\"overview-heading\">Evidence Overview</h2>");
        builder.AppendLine("      <dl class=\"summary-grid\">");
        SummaryItem(builder, "Safety profile", summary.SafetyProfile);
        SummaryItem(builder, "Claim level", summary.ClaimLevel);
        SummaryItem(builder, "Coverage status", summary.CoverageStatus);
        SummaryItem(builder, "Commit SHA", summary.CommitSha ?? "partial or unavailable");
        SummaryItem(builder, "Sources", summary.SourceCount.ToString());
        SummaryItem(builder, "Artifacts", summary.ArtifactCount.ToString());
        SummaryItem(builder, "Surfaces", summary.SurfaceCount.ToString());
        SummaryItem(builder, "Paths", summary.PathCount.ToString());
        SummaryItem(builder, "Reducer rows", summary.ReducerResultCount.ToString());
        SummaryItem(builder, "Evidence rows", summary.EvidenceRowCount.ToString());
        SummaryItem(builder, "Gaps", summary.GapCount.ToString());
        SummaryItem(builder, "Limitations", summary.LimitationCount.ToString());
        SummaryItem(builder, "Rules", summary.RuleCount.ToString());
        SummaryItem(builder, "Redacted or hashed", summary.RedactionCount.ToString());
        SummaryItem(builder, "Omitted or unavailable", summary.OmittedCount.ToString());
        SummaryItem(builder, "Reducer output", summary.ReducerOutputPresent ? "present" : "not provided");
        builder.AppendLine("      </dl>");
        var coverageLabels = Html(string.Join(", ", summary.CoverageLabels));
        builder.AppendLine($"      <p><strong>Coverage labels:</strong> {coverageLabels}</p>");
        builder.AppendLine("    </section>");
    }

    private static void SummaryItem(StringBuilder builder, string key, string value)
    {
        builder.AppendLine($"        <div><dt>{Html(key)}</dt><dd>{Html(value)}</dd></div>");
    }

    private static void RenderCoverage(StringBuilder builder, IReadOnlyList<ExplorerSectionStatus> sectionStatuses)
    {
        builder.AppendLine("    <section id=\"coverage\" aria-labelledby=\"coverage-heading\">");
        builder.AppendLine("      <h2 id=\"coverage-heading\">Coverage</h2>");
        builder.AppendLine("      <p>Section status rows describe explorer rendering coverage only. They do not prove runtime behavior or evidence absence outside compatible inputs.</p>");
        builder.AppendLine("      <table><caption>Rule-backed section availability and coverage labels</caption><thead><tr><th>Section</th><th>Status</th><th>Rule ID</th><th>Tier</th><th>Coverage</th><th>Support IDs</th><th>Message</th></tr></thead><tbody>");
        foreach (var row in sectionStatuses)
        {
            var supportIds = Html(string.Join(", ", row.SupportIds));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(row.Label)}</th><td>{Html(row.Status)}</td><td>{Html(row.RuleId)}</td><td>{Html(row.EvidenceTier)}</td><td>{Html(row.CoverageLabel)}</td><td>{supportIds}</td><td>{Html(row.Message)}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderCompatibilityLedger(StringBuilder builder, IReadOnlyList<ExplorerCompatibilityRow> rows)
    {
        builder.AppendLine("    <section id=\"compatibility-ledger\" aria-labelledby=\"compatibility-ledger-heading\">");
        builder.AppendLine("      <h2 id=\"compatibility-ledger-heading\">Compatibility Ledger</h2>");
        builder.AppendLine("      <p>Compatibility rows explain what the explorer could safely read or render. Missing and unsupported inputs are not evidence-absence conclusions.</p>");
        builder.AppendLine("      <table><caption>Rule-backed artifact, section, profile, and claim-metadata compatibility</caption><thead><tr><th>Subject</th><th>Kind</th><th>Label</th><th>Status</th><th>Rule ID</th><th>Tier</th><th>Coverage</th><th>Scope</th><th>Support IDs</th><th>Limitations</th><th>Message</th></tr></thead><tbody>");
        foreach (var row in rows)
        {
            var supportIds = Html(string.Join(", ", row.SupportIds));
            var limitationIds = Html(string.Join(", ", row.LimitationIds));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(row.SubjectId)}</th><td>{Html(row.SubjectKind)}</td><td>{Html(row.SafeLabel)}</td><td>{Html(row.CompatibilityStatus)}</td><td>{Html(row.RuleId)}</td><td>{Html(row.EvidenceTier)}</td><td>{Html(row.CoverageLabel)}</td><td>{Html(row.Scope)}</td><td>{supportIds}</td><td>{limitationIds}</td><td>{Html(row.Message)}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderSources(StringBuilder builder, IReadOnlyList<ExplorerSource> sources)
    {
        builder.AppendLine("    <section id=\"sources\" aria-labelledby=\"sources-heading\">");
        builder.AppendLine("      <h2 id=\"sources-heading\">Sources</h2>");
        builder.AppendLine("      <table><caption>Safe source summaries</caption><thead><tr><th>Label</th><th>Kind</th><th>Coverage</th><th>Commit SHA</th><th>Artifacts</th><th>Extractor versions</th><th>Gaps</th></tr></thead><tbody>");
        foreach (var source in sources.OrderBy(source => source.SourceId, StringComparer.Ordinal))
        {
            var commitSha = Html(source.CommitSha ?? "partial");
            var extractorVersions = Html(string.Join(", ", source.ExtractorVersions));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(source.SafeLabel)}</th><td>{Html(source.SourceKind)}</td><td>{Html(source.CoverageStatus)}</td><td>{commitSha}</td><td>{source.ArtifactIds.Count}</td><td>{extractorVersions}</td><td>{source.GapCount}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderArtifacts(StringBuilder builder, IReadOnlyList<ExplorerInputArtifact> artifacts)
    {
        builder.AppendLine("    <section id=\"artifacts\" aria-labelledby=\"artifacts-heading\">");
        builder.AppendLine("      <h2 id=\"artifacts-heading\">Artifacts</h2>");
        builder.AppendLine("      <table><caption>Input artifacts by stable ID</caption><thead><tr><th>Artifact ID</th><th>Kind</th><th>Label</th><th>Schema</th><th>Compatibility</th><th>Hash</th></tr></thead><tbody>");
        foreach (var artifact in artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal))
        {
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(artifact.ArtifactId)}</th><td>{Html(artifact.ArtifactKind)}</td><td>{Html(artifact.SafeLabel)}</td><td>{Html(artifact.SchemaVersion)}</td><td>{Html(artifact.Compatibility)}</td><td>{Html(artifact.ContentHash)}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderSurfaces(StringBuilder builder, IReadOnlyList<ExplorerSurface> surfaces)
    {
        builder.AppendLine("    <section id=\"surfaces\" aria-labelledby=\"surfaces-heading\">");
        builder.AppendLine("      <h2 id=\"surfaces-heading\">Surfaces</h2>");
        builder.AppendLine("      <p>These rows are static dependency-surface evidence from compatible generated reports. They do not prove runtime reachability, execution, production use, or impact.</p>");
        builder.AppendLine("      <table data-filterable=\"true\"><caption>Rule-backed static dependency surfaces</caption><thead><tr><th>Surface</th><th>Kind</th><th>Classification</th><th>Rule ID</th><th>Tier</th><th>Coverage</th><th>Source</th><th>Commit SHA</th><th>Extractor</th><th>File span</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var surface in surfaces)
        {
            var span = surface.FilePath is null ? "n/a" : $"{surface.FilePath}:{surface.StartLine}-{surface.EndLine}";
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(surface.SurfaceId)}</th><td>{Html(surface.SurfaceKind)}</td><td>{Html(surface.Classification)}</td><td>{Html(surface.RuleId)}</td><td>{Html(surface.EvidenceTier)}</td><td>{Html(surface.CoverageLabel)}</td><td>{Html(surface.SourceId)}</td><td>{Html(surface.CommitSha)}</td><td>{Html(surface.ExtractorVersion)}</td><td>{Html(span)}</td><td>{Html(string.Join(", ", surface.SupportIds))}</td><td>{Html(string.Join(", ", surface.LimitationIds))}</td></tr>");
        }
        if (surfaces.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"12\">No compatible static dependency-surface rows were provided under the current coverage.</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderPaths(StringBuilder builder, IReadOnlyList<ExplorerPath> paths)
    {
        builder.AppendLine("    <section id=\"paths\" aria-labelledby=\"paths-heading\">");
        builder.AppendLine("      <h2 id=\"paths-heading\">Paths</h2>");
        builder.AppendLine("      <p>Paths preserve deterministic static hop evidence and existing classifications. They are not runtime traces or reducer-backed impact conclusions.</p>");
        builder.AppendLine("      <table data-filterable=\"true\"><caption>Ordered static dependency-path hops</caption><thead><tr><th>Path</th><th>Classification</th><th>Confidence</th><th>Hop</th><th>Edge kind</th><th>Rule ID</th><th>Tier</th><th>Coverage</th><th>From</th><th>To</th><th>Source</th><th>Commit SHA</th><th>Extractor</th><th>File span</th><th>Support IDs</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var path in paths)
        {
            foreach (var hop in path.Hops)
            {
                var span = hop.FilePath is null ? "n/a" : $"{hop.FilePath}:{hop.StartLine}-{hop.EndLine}";
                builder.AppendLine($"        <tr><th scope=\"row\">{Html(path.PathId)}</th><td>{Html(path.Classification)}</td><td>{Html(path.Confidence)}</td><td>{hop.Sequence}</td><td>{Html(hop.EdgeKind)}</td><td>{Html(hop.RuleId)}</td><td>{Html(hop.EvidenceTier)}</td><td>{Html(path.CoverageLabel)}</td><td>{Html(hop.FromNodeId)}</td><td>{Html(hop.ToNodeId)}</td><td>{Html(hop.SourceId)}</td><td>{Html(hop.CommitSha)}</td><td>{Html(hop.ExtractorVersion)}</td><td>{Html(span)}</td><td>{Html(string.Join(", ", hop.SupportIds))}</td><td>{Html(string.Join(", ", hop.LimitationIds))}</td></tr>");
            }
        }
        if (paths.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"16\">No compatible static dependency paths were provided under the current coverage.</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderGaps(StringBuilder builder, IReadOnlyList<ExplorerGap> gaps)
    {
        builder.AppendLine("    <section id=\"gaps\" aria-labelledby=\"gaps-heading\">");
        builder.AppendLine("      <h2 id=\"gaps-heading\">Gaps</h2>");
        builder.AppendLine("      <table><caption>Rule-backed analysis and generation gaps</caption><thead><tr><th>Gap</th><th>Rule ID</th><th>Tier</th><th>Kind</th><th>Scope</th><th>Section</th><th>Coverage</th><th>Support IDs</th><th>Message</th></tr></thead><tbody>");
        foreach (var gap in gaps)
        {
            var supportIds = Html(string.Join(", ", gap.SupportIds));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(gap.GapId)}</th><td>{Html(gap.RuleId)}</td><td>{Html(gap.EvidenceTier)}</td><td>{Html(gap.GapKind)}</td><td>{Html(gap.Scope)}</td><td>{Html(gap.AffectedSection)}</td><td>{Html(gap.CoverageLabel)}</td><td>{supportIds}</td><td>{Html(gap.Message)}</td></tr>");
        }
        if (gaps.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"9\">No explorer generation gaps were emitted for the supported first-slice inputs.</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderLimitations(StringBuilder builder, IReadOnlyList<ExplorerLimitation> limitations)
    {
        builder.AppendLine("    <section id=\"limitations\" aria-labelledby=\"limitations-heading\">");
        builder.AppendLine("      <h2 id=\"limitations-heading\">Limitations</h2>");
        builder.AppendLine("      <table><caption>Rule-backed limitations</caption><thead><tr><th>Limitation</th><th>Rule ID</th><th>Tier</th><th>Kind</th><th>Scope</th><th>Section</th><th>Claim effect</th><th>Support IDs</th><th>Message</th></tr></thead><tbody>");
        foreach (var limitation in limitations)
        {
            var supportIds = Html(string.Join(", ", limitation.SupportIds));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(limitation.LimitationId)}</th><td>{Html(limitation.RuleId)}</td><td>{Html(limitation.EvidenceTier)}</td><td>{Html(limitation.LimitationKind)}</td><td>{Html(limitation.Scope)}</td><td>{Html(limitation.AffectedSection)}</td><td>{Html(limitation.ClaimEffect)}</td><td>{supportIds}</td><td>{Html(limitation.Message)}</td></tr>");
        }
        if (limitations.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"9\">No additional explorer limitations beyond visible gaps and rule catalog limitations.</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderRedactions(StringBuilder builder, IReadOnlyList<ExplorerRedaction> redactions)
    {
        builder.AppendLine("    <section id=\"redactions\" aria-labelledby=\"redactions-heading\">");
        builder.AppendLine("      <h2 id=\"redactions-heading\">Safety &amp; Redactions</h2>");
        builder.AppendLine("      <table><caption>Safe redaction, hash, category-only, and omission counts</caption><thead><tr><th>Redaction</th><th>Rule ID</th><th>Category</th><th>Location</th><th>Action</th><th>Count</th></tr></thead><tbody>");
        foreach (var redaction in redactions)
        {
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(redaction.RedactionId)}</th><td>{Html(redaction.RuleId)}</td><td>{Html(redaction.Category)}</td><td>{Html(redaction.Location)}</td><td>{Html(redaction.Action)}</td><td>{redaction.Count}</td></tr>");
        }
        if (redactions.Count == 0)
        {
            builder.AppendLine("        <tr><td colspan=\"6\">No redaction rows were recorded for the compatible first-slice inputs.</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderRules(StringBuilder builder, IReadOnlyList<ExplorerRule> rules)
    {
        builder.AppendLine("    <section id=\"rules\" aria-labelledby=\"rules-heading\">");
        builder.AppendLine("      <h2 id=\"rules-heading\">Rules</h2>");
        builder.AppendLine("      <table><caption>Explorer rule catalog stubs and observed evidence rule IDs</caption><thead><tr><th>Rule ID</th><th>Title</th><th>Description</th><th>Tier</th><th>Related sections</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var rule in rules.OrderBy(rule => rule.RuleId, StringComparer.Ordinal))
        {
            var limitations = Html(string.Join(" ", rule.Limitations));
            var relatedSections = Html(string.Join(", ", rule.RelatedSections));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(rule.RuleId)}</th><td>{Html(rule.Title)}</td><td>{Html(rule.Description)}</td><td>{Html(rule.EvidenceTier)}</td><td>{relatedSections}</td><td>{limitations}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderEvidenceRows(StringBuilder builder, IReadOnlyList<ExplorerEvidenceRow> rows, bool factStreamProvided)
    {
        builder.AppendLine("    <section id=\"evidence-rows\" aria-labelledby=\"evidence-rows-heading\">");
        builder.AppendLine("      <h2 id=\"evidence-rows-heading\">Evidence Rows</h2>");
        if (rows.Count > EvidenceRowNoScriptLimit)
        {
            builder.AppendLine($"      <p>The no-JavaScript baseline renders the first {EvidenceRowNoScriptLimit} deterministic rows out of {rows.Count}. The full safe row set is available in data/explorer-data.json.</p>");
        }
        builder.AppendLine("      <table data-filterable=\"true\"><caption>Safe evidence rows</caption><thead><tr><th>Evidence</th><th>Rule ID</th><th>Tier</th><th>Kind</th><th>Support ID</th><th>Artifact ID</th><th>Source ID</th><th>Coverage</th><th>Commit SHA</th><th>File span</th><th>Snippet hash</th><th>Extractor</th><th>Limitations</th></tr></thead><tbody>");
        foreach (var row in rows.Take(EvidenceRowNoScriptLimit))
        {
            var span = row.FilePath is null ? "n/a" : $"{row.FilePath}:{row.StartLine}-{row.EndLine}";
            var snippetHash = Html(row.SnippetHash ?? "n/a");
            var extractorVersion = Html(row.ExtractorVersion ?? "unknown");
            var commitSha = Html(row.CommitSha ?? "partial");
            var limitations = Html(string.Join(", ", row.Limitations));
            builder.AppendLine($"        <tr><th scope=\"row\">{Html(row.EvidenceId)}</th><td>{Html(row.RuleId)}</td><td>{Html(row.EvidenceTier)}</td><td>{Html(row.EvidenceKind)}</td><td>{Html(row.SupportId)}</td><td>{Html(row.ArtifactId)}</td><td>{Html(row.SourceId ?? "unknown")}</td><td>{Html(row.CoverageLabel ?? "UnknownCoverage")}</td><td>{commitSha}</td><td>{Html(span)}</td><td>{snippetHash}</td><td>{extractorVersion}</td><td>{limitations}</td></tr>");
        }
        if (rows.Count == 0)
        {
            var message = factStreamProvided
                ? "No static evidence rows were found in the provided fact stream under the current coverage."
                : "Evidence rows are unavailable because no compatible fact stream was provided.";
            builder.AppendLine($"        <tr><td colspan=\"13\">{Html(message)}</td></tr>");
        }
        builder.AppendLine("      </tbody></table>");
        builder.AppendLine("    </section>");
    }

    private static void RenderAbout(StringBuilder builder)
    {
        builder.AppendLine("    <section id=\"about\" aria-labelledby=\"about-heading\">");
        builder.AppendLine("      <h2 id=\"about-heading\">About This Local Explorer</h2>");
        builder.AppendLine("      <p>This is a generated local TraceMap report artifact, separate from the public tracemap.tools site. It uses bundled local assets and safe generated data only.</p>");
        builder.AppendLine("    </section>");
    }

    private static IReadOnlyList<(string Id, string Label)> Sections()
    {
        return
        [
            ("overview", "Evidence Overview"),
            ("coverage", "Coverage"),
            ("compatibility-ledger", "Compatibility Ledger"),
            ("sources", "Sources"),
            ("artifacts", "Artifacts"),
            ("surfaces", "Surfaces"),
            ("paths", "Paths"),
            ("gaps", "Gaps"),
            ("limitations", "Limitations"),
            ("redactions", "Safety & Redactions"),
            ("rules", "Rules"),
            ("evidence-rows", "Evidence Rows"),
            ("about", "About This Local Explorer")
        ];
    }

    private static string Css()
    {
        return """
            :root {
              color-scheme: light;
              --bg: #f7f7f2;
              --text: #17201b;
              --muted: #516059;
              --line: #c9d2c8;
              --panel: #ffffff;
              --accent: #1f6f68;
              --warn: #8a4f00;
            }

            * {
              box-sizing: border-box;
            }

            body {
              margin: 0;
              background: var(--bg);
              color: var(--text);
              font: 15px/1.5 system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            }

            header,
            nav,
            main {
              max-width: 1180px;
              margin: 0 auto;
              padding: 24px;
            }

            header {
              padding-top: 40px;
            }

            h1,
            h2 {
              margin: 0 0 12px;
              line-height: 1.15;
            }

            .eyebrow {
              color: var(--accent);
              font-weight: 700;
              text-transform: uppercase;
              letter-spacing: 0;
            }

            .notice {
              border-left: 4px solid var(--warn);
              padding: 10px 12px;
              background: #fff6e8;
            }

            nav ul {
              display: flex;
              flex-wrap: wrap;
              gap: 8px;
              list-style: none;
              margin: 0;
              padding: 0;
            }

            a {
              color: var(--accent);
            }

            a:focus,
            button:focus,
            input:focus {
              outline: 3px solid #86b8ff;
              outline-offset: 2px;
            }

            section {
              margin: 0 0 28px;
              padding: 18px 0 0;
              border-top: 1px solid var(--line);
            }

            .summary-grid {
              display: grid;
              grid-template-columns: repeat(auto-fit, minmax(150px, 1fr));
              gap: 10px;
            }

            .summary-grid div {
              background: var(--panel);
              border: 1px solid var(--line);
              border-radius: 6px;
              padding: 10px;
            }

            dt {
              color: var(--muted);
              font-size: 12px;
            }

            dd {
              margin: 0;
              font-weight: 700;
              overflow-wrap: anywhere;
            }

            table {
              width: 100%;
              border-collapse: collapse;
              background: var(--panel);
              border: 1px solid var(--line);
              table-layout: fixed;
            }

            caption {
              text-align: left;
              color: var(--muted);
              padding: 8px 0;
            }

            th,
            td {
              border-top: 1px solid var(--line);
              padding: 8px;
              vertical-align: top;
              overflow-wrap: anywhere;
            }

            thead th {
              background: #eaf0ec;
              text-align: left;
            }
            """.ReplaceLineEndings("\n");
    }

    private static string JavaScript()
    {
        return """
            (() => {
              "use strict";
              for (const table of document.querySelectorAll("table[data-filterable='true']")) {
                const label = document.createElement("label");
                label.textContent = "Filter safe rendered rows";
                const input = document.createElement("input");
                input.type = "search";
                input.autocomplete = "off";
                input.setAttribute("aria-label", "Filter safe rendered evidence rows");
                label.append(" ", input);
                table.before(label);
                const rows = Array.from(table.tBodies[0]?.rows ?? []);
                input.addEventListener("input", () => {
                  const needle = input.value.toLowerCase();
                  for (const row of rows) {
                    row.hidden = needle.length > 0 && !row.textContent.toLowerCase().includes(needle);
                  }
                });
              }
            })();
            """.ReplaceLineEndings("\n");
    }

    private static string Readme(ExplorerManifest manifest)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Local Evidence Explorer");
        builder.AppendLine();
        builder.AppendLine("This directory is a generated local TraceMap report artifact. It is not the public `tracemap.tools` website and does not require a TraceMap backend.");
        builder.AppendLine();
        builder.AppendLine("- Open `index.html` from disk or serve this directory with any static file server.");
        builder.AppendLine("- Assets are local under `assets/` and data is local under `data/`.");
        builder.AppendLine("- The explorer renders existing generated TraceMap artifacts and does not rescan source code or derive new impact conclusions.");
        builder.AppendLine($"- Safety profile: `{manifest.SafetyProfile}`.");
        builder.AppendLine($"- Repository identity policy: `{manifest.RepoIdentityPolicy}`.");
        builder.AppendLine($"- Generation timestamp policy: `{manifest.GenerationTimestampPolicy}`.");
        return builder.ToString().ReplaceLineEndings("\n");
    }

    internal static void ValidateGeneratedFilesForSafety(IReadOnlyDictionary<string, string> files)
    {
        foreach (var (path, content) in files)
        {
            if (Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal) || path.Contains('\\', StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: unsafe-file-name at generated artifact.");
            }

            if (path.EndsWith(".map", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: source-map at {path}.");
            }

            if (Regex.IsMatch(content, @"https?://", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
            {
                throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: remote-reference at {path}.");
            }

            var lines = content.ReplaceLineEndings("\n").Split('\n');
            for (var index = 0; index < lines.Length; index++)
            {
                if (UnsafeCategory(lines[index]) is { } category)
                {
                    throw new InvalidOperationException($"UnsafeValueRejected: {UnsafeRejectedRuleId} [{Tier4Unknown}]: {category} at {path}:{index + 1}.");
                }
            }
        }
    }

    private static void ValidateExistingFiles(string outputPath, IReadOnlyDictionary<string, string> files, bool force)
    {
        var outputDirectory = Path.GetFullPath(outputPath);
        if (!Directory.Exists(outputDirectory))
        {
            return;
        }

        var manifestPath = Path.Combine(outputDirectory, "data", "explorer-manifest.json");
        var hasGeneratedManifest = File.Exists(manifestPath) && HasGeneratedManifestMarker(File.ReadAllText(manifestPath));
        foreach (var relativePath in files.Keys)
        {
            var fullPath = Path.Combine(outputDirectory, relativePath);
            if (!File.Exists(fullPath))
            {
                continue;
            }

            if (hasGeneratedManifest && force)
            {
                continue;
            }

            throw new InvalidOperationException(hasGeneratedManifest
                ? $"GeneratedFileStale: {GeneratedFileStaleRuleId} [{Tier4Unknown}]: {relativePath}."
                : $"UserFileCollision: {UserFileCollisionRuleId} [{Tier4Unknown}]: {relativePath}.");
        }
    }

    private static bool HasGeneratedManifestMarker(string content)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            return document.RootElement.TryGetProperty("schemaVersion", out var schemaVersion)
                && (schemaVersion.GetString() == SchemaVersion
                    || PriorSchemaVersions.Contains(schemaVersion.GetString(), StringComparer.Ordinal))
                && document.RootElement.TryGetProperty("tracemapGenerated", out var generated)
                && generated.ValueKind == JsonValueKind.True;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? UnsafeCategory(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (value.Contains("/Users/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/home/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("/private/", StringComparison.OrdinalIgnoreCase)
            || value.Contains("\\Users\\", StringComparison.OrdinalIgnoreCase)
            || Regex.IsMatch(value, @"[A-Za-z]:\\", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase))
        {
            return "local-absolute-path";
        }

        if (value.Contains("://", StringComparison.Ordinal))
        {
            return "raw-remote-or-url";
        }

        if (Regex.IsMatch(value, @"(?i)\b[A-Za-z0-9._%+-]+@[A-Za-z0-9._-]+:[^\s""'<>]+", RegexOptions.CultureInvariant))
        {
            return "raw-remote-or-url";
        }

        if (SqlPattern.IsMatch(value))
        {
            return "raw-sql";
        }

        if (Regex.IsMatch(value, @"(?i)(password|secret|api[_-]?key|token)\s*[:=]", RegexOptions.CultureInvariant))
        {
            return "secret-like-value";
        }

        if (Regex.IsMatch(value, @"(?i)(server|host|data source|user id|uid|pwd)\s*=", RegexOptions.CultureInvariant))
        {
            return "config-or-connection-string";
        }

        if (value.Contains('?', StringComparison.Ordinal) && (value.Contains('/', StringComparison.Ordinal) || value.Contains('&', StringComparison.Ordinal)))
        {
            return "query-string";
        }

        if (value.Contains("System.", StringComparison.Ordinal) && value.Contains("Exception", StringComparison.Ordinal))
        {
            return "stack-trace";
        }

        return null;
    }

    private static string Html(string value)
    {
        return System.Net.WebUtility.HtmlEncode(value);
    }

    private sealed record ExplorerBuildContext(
        string SafetyProfile,
        string? CommitSha,
        string CoverageStatus,
        IReadOnlyList<string> CoverageLabels,
        IReadOnlyList<ExplorerSource> Sources,
        IReadOnlyList<ExplorerInputArtifact> Artifacts,
        IReadOnlyList<ExplorerSurface> Surfaces,
        IReadOnlyList<ExplorerPath> Paths,
        IReadOnlyList<ExplorerEvidenceRow> EvidenceRows,
        IReadOnlyList<ExplorerGap> Gaps,
        IReadOnlyList<ExplorerLimitation> Limitations,
        bool CatalogFilePresent,
        bool CatalogRulesLoaded,
        IReadOnlyList<ExplorerRule> Rules,
        IReadOnlyList<ExplorerRedaction> Redactions);

    private sealed record RuleCatalogLoadResult(
        bool FilePresent,
        IReadOnlyList<RuleCatalogEntry> Entries);

    private sealed record RuleCatalogEntry(
        string RuleId,
        string Title,
        string Description,
        string EvidenceTier,
        IReadOnlyList<string> Limitations);

    private sealed record BoundedArtifactSnapshot(
        string ContentHash,
        byte[]? Content);

    private sealed record ReleaseReviewSnapshotMetadata(
        string Coverage,
        IReadOnlyList<string> CommitShas);
}
