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
    IReadOnlyList<ExplorerSource> Sources,
    IReadOnlyList<ExplorerInputArtifact> Artifacts,
    IReadOnlyList<ExplorerEvidenceRow> EvidenceRows,
    IReadOnlyList<ExplorerGap> Gaps,
    IReadOnlyList<ExplorerLimitation> Limitations,
    IReadOnlyList<ExplorerRule> Rules,
    IReadOnlyList<ExplorerRedaction> Redactions);

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
    public const string SchemaVersion = "tracemap-static-html-evidence-explorer.v1";
    public const string GeneratorName = "tracemap-static-html-evidence-explorer";

    public const string UnsupportedSchemaRuleId = "explorer.input.unsupported-schema.v1";
    public const string ProvenanceConflictRuleId = "explorer.input.provenance-conflict.v1";
    public const string MissingCommitRuleId = "explorer.input.missing-commit.v1";
    public const string RedactedDisplayValueRuleId = "explorer.render.redacted-display-value.v1";
    public const string OmittedUnsafeValueRuleId = "explorer.render.omitted-unsafe-value.v1";
    public const string CatalogUnavailableRuleId = "explorer.render.catalog-unavailable.v1";
    public const string NoNetworkAssetsRuleId = "explorer.render.no-network-assets.v1";
    public const string PartialSectionRuleId = "explorer.render.partial-section.v1";
    public const string SectionStatusRuleId = "explorer.render.section-status.v1";
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

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly Regex CommitShaPattern = new("^[0-9a-fA-F]{7,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

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
        var evidenceRows = new List<ExplorerEvidenceRow>();
        var gaps = new List<ExplorerGap>();
        var limitations = new List<ExplorerLimitation>();
        var redactions = new Dictionary<(string RuleId, string Category, string Location, string Action), int>();
        var coverageLabels = new SortedSet<string>(StringComparer.Ordinal);
        ScanManifest? manifest = null;

        var manifestPath = Path.Combine(inputDirectory, "scan-manifest.json");
        if (File.Exists(manifestPath))
        {
            manifest = await ReadJsonAsync<ScanManifest>(manifestPath, cancellationToken);
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

            if (manifest is null && factCommitShas.Length == 0)
            {
                gaps.Add(CreateGap(
                    "missing-commit-facts",
                    MissingCommitRuleId,
                    "missing-commit",
                    "artifact:facts-ndjson",
                    "evidence-rows",
                    "PartialAnalysis",
                    "facts.ndjson does not contain usable commit metadata and no scan manifest was provided.",
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
        var catalogRules = await AddRuleCatalogArtifactAsync(inputDirectory, safetyProfile, artifacts, gaps, redactions, cancellationToken);
        await AddUnsupportedJsonArtifactsAsync(inputDirectory, safetyProfile, artifacts, gaps, cancellationToken);

        limitations.Add(CreateLimitation(
            "claim-level-conflict-detection-deferred",
            ProvenanceConflictRuleId,
            "claim-level-conflict-detection-deferred",
            "artifacts",
            "claim-level",
            "Claim-level conflict detection across multiple compatible structured artifacts is deferred in this explorer slice. Existing rendered data still uses the selected safety profile and documents this limitation.",
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
            .Where(ruleId => catalogRules.Count == 0 || !catalogRuleIds.Contains(ruleId))
            .ToArray();
        if (observedRulesWithoutCatalog.Length > 0)
        {
            var catalogProvided = catalogRules.Count > 0;
            gaps.Add(CreateGap(
                catalogProvided ? "rule-catalog-observed-entry-unavailable" : "rule-catalog-unavailable",
                CatalogUnavailableRuleId,
                catalogProvided ? "catalog-entry-unavailable" : "catalog-unavailable",
                "artifact:facts-ndjson",
                "rules",
                coverageLabels.Count == 0 ? "UnknownCoverage" : coverageLabels.First(),
                catalogProvided
                    ? "facts.ndjson references rule IDs that are not present in the compatible rule catalog artifact; those observed rules remain partial."
                    : "facts.ndjson references rule IDs that are rendered with observed metadata only because no compatible rule catalog artifact was provided.",
                observedRulesWithoutCatalog));
        }

        var rules = BuildExplorerRules(evidenceRows, catalogRules);
        var source = BuildSource(manifest, safetyProfile, artifacts, gaps, limitations, redactions, coverageLabels);
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
            [source],
            artifacts.OrderBy(artifact => artifact.ArtifactId, StringComparer.Ordinal).ToArray(),
            evidenceRows,
            gaps.OrderBy(gap => gap.RuleId, StringComparer.Ordinal).ThenBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            limitations.OrderBy(limitation => limitation.RuleId, StringComparer.Ordinal).ThenBy(limitation => limitation.LimitationId, StringComparer.Ordinal).ToArray(),
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
            SurfaceCount: 0,
            PathCount: 0,
            ReducerResultCount: 0,
            context.EvidenceRows.Count,
            context.Gaps.Count,
            context.Limitations.Count,
            context.Rules.Count,
            context.Redactions.Sum(redaction => redaction.Count),
            OmittedCount: context.Gaps.Count(gap => gap.GapKind is "not-provided" or "unsupported"),
            context.CoverageLabels,
            ReducerOutputPresent: false);

        return new ExplorerData(
            SchemaVersion,
            summary,
            BuildSectionStatuses(context),
            context.Sources,
            context.Artifacts,
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

    private static async Task<IReadOnlyList<RuleCatalogEntry>> AddRuleCatalogArtifactAsync(
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
            return [];
        }

        var path = candidates[0];
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
            "supported"));

        var text = await File.ReadAllTextAsync(path, cancellationToken);
        var entries = ParseRuleCatalog(text, redactions);
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

        return entries;
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
            artifacts.Select(artifact => artifact.ArtifactId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
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
        IReadOnlyList<string> supportIds)
    {
        return new ExplorerGap(
            $"gap:{idPart}",
            ruleId,
            Tier4Unknown,
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
        var observedRuleIds = evidenceRows
            .Select(row => row.RuleId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var catalogRule in catalogRules)
        {
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
                RelatedSectionsForCatalogRule(catalogRule.RuleId, observedRuleIds));
        }

        var observedRules = evidenceRows
            .GroupBy(row => row.RuleId, StringComparer.Ordinal)
            .Where(group => !rules.ContainsKey(group.Key))
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => new ExplorerRule(
                group.Key,
                "Observed evidence rule",
                "Rule ID observed in safe evidence rows from facts.ndjson. Full rule catalog metadata was not provided in this explorer slice.",
                ObservedEvidenceTier(group.Select(row => row.EvidenceTier)),
                [
                    "Observed rule rows preserve safe evidence-row rule IDs only.",
                    "Without a compatible rule catalog artifact, title, description, and limitations are partial and must not strengthen the underlying evidence."
                ],
                ["evidence-rows", "rules"]))
            .ToArray();
        foreach (var observedRule in observedRules)
        {
            rules[observedRule.RuleId] = observedRule;
        }

        return rules.Values
            .OrderBy(rule => rule.RuleId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RelatedSectionsForCatalogRule(string ruleId, IReadOnlySet<string> observedRuleIds)
    {
        var sections = new SortedSet<string>(StringComparer.Ordinal)
        {
            "rules"
        };
        if (observedRuleIds.Contains(ruleId))
        {
            sections.Add("evidence-rows");
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
        var ruleCatalogProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "rule-catalog");
        var unsupportedJsonProvided = context.Artifacts.Any(artifact => artifact.ArtifactKind == "unsupported-json");
        var coverageLabel = context.CoverageLabels.FirstOrDefault() ?? "UnknownCoverage";
        var evidenceRowsStatus = factsProvided
            ? (context.EvidenceRows.Count == 0
                ? "no-evidence-under-current-coverage"
                : SectionStatusFromGaps(context.Gaps, "evidence-rows", true))
            : "not-provided";
        var evidenceRowsMessage = factsProvided
            ? (context.EvidenceRows.Count == 0
                ? "facts.ndjson was provided and compatible, but no static evidence rows were present under the current coverage."
                : "Evidence rows are rendered from facts.ndjson after safety filtering and deterministic ordering.")
            : "Evidence rows are unavailable because no compatible fact stream was provided.";
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
                factsProvided ? ["artifact:facts-ndjson"] : ["input-directory"]),
            SectionStatus(
                "surfaces",
                "Surfaces",
                sqliteProvided ? "not-rendered-in-current-slice" : "not-provided",
                "PartialAnalysis",
                sqliteProvided
                    ? "index.sqlite was hashed as provenance, but static surface extraction from SQLite is deferred in this explorer slice."
                    : "Surface rendering requires a compatible surface artifact or future SQLite reader and is unavailable here.",
                sqliteProvided ? ["artifact:sqlite-index"] : ["input-directory"]),
            SectionStatus(
                "paths",
                "Paths",
                sqliteProvided ? "not-rendered-in-current-slice" : "not-provided",
                "PartialAnalysis",
                sqliteProvided
                    ? "index.sqlite was hashed as provenance, but dependency and route path rendering from SQLite is deferred in this explorer slice."
                    : "Path rendering requires a compatible path artifact or future SQLite reader and is unavailable here.",
                sqliteProvided ? ["artifact:sqlite-index"] : ["input-directory"]),
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
                ruleCatalogProvided
                    ? "Rules include compatible rule catalog rows plus built-in explorer rules and observed fallback rows for any uncataloged evidence rule IDs."
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
            ["overview", "gaps", "limitations", "artifacts", "evidence-rows"]);
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

    private static IReadOnlyList<RuleCatalogEntry> ParseRuleCatalog(
        string text,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions)
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
                SafeRuleCatalogEvidenceTier(evidenceTier),
                limitations
                    .Select(value => SafeRuleCatalogText(value, "rule-catalog.limitations", redactions))
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .ToArray()));
        }

        foreach (var rawLine in text.ReplaceLineEndings("\n").Split('\n'))
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

            if (trimmed.EndsWith(":", StringComparison.Ordinal) && !trimmed.StartsWith("-", StringComparison.Ordinal))
            {
                listContext = trimmed[..^1].Trim();
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

    private static string SafeRuleCatalogEvidenceTier(string? value)
    {
        return value is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural or EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown
            ? value
            : Tier4Unknown;
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
        RenderSources(builder, data.Sources);
        RenderArtifacts(builder, data.Artifacts);
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
            ("sources", "Sources"),
            ("artifacts", "Artifacts"),
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
                && schemaVersion.GetString() == SchemaVersion
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

        if (Regex.IsMatch(value, @"(?i)\b(select\s+(\*|[\w\[\]"".]+(?:\s*,\s*[\w\[\]"".]+)*)\s+from|insert\s+into|update\s+[\w\[\]"".]+\s+set|delete\s+from|merge\s+into)\b", RegexOptions.CultureInvariant))
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
        IReadOnlyList<ExplorerEvidenceRow> EvidenceRows,
        IReadOnlyList<ExplorerGap> Gaps,
        IReadOnlyList<ExplorerLimitation> Limitations,
        IReadOnlyList<ExplorerRule> Rules,
        IReadOnlyList<ExplorerRedaction> Redactions);

    private sealed record RuleCatalogEntry(
        string RuleId,
        string Title,
        string Description,
        string EvidenceTier,
        IReadOnlyList<string> Limitations);
}
