using System.Globalization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record PackageDecisionOptions(
    string DecisionPath,
    string IndexPath,
    string OutputPath,
    string Format = "markdown",
    string? Source = null,
    string? Ecosystem = null,
    string? DecisionId = null,
    string? Classification = null,
    int MaxFindings = 200,
    int MaxGaps = 1000,
    bool ExitCode = false,
    string? AsOf = null,
    IReadOnlyList<string>? IndexPaths = null,
    IReadOnlyList<string>? Labels = null,
    string? ManifestPath = null,
    bool IncludePaths = false,
    bool IncludeReverse = false,
    int MaxDepth = 8,
    int MaxPaths = 100,
    int MaxFrontier = 10000,
    int MaxRoots = 100,
    int MaxPathsPerRoot = 5,
    string? BeforeManifestPath = null,
    string? AfterManifestPath = null,
    string? AdvisoryProfilePath = null,
    string? DeploymentReferencesPath = null);

public sealed record PackageDecisionResult(PackageDecisionDocument Report, string? MarkdownPath, string? JsonPath)
{
    public bool ExitCodeTriggered => Report.ExactMatches.Any(row => row.DecisionKind is "reject" or "revoke");
}

public sealed record PackageDecisionDocument(
    string ReportType,
    string Version,
    string Mode,
    PackageDecisionQuery Query,
    IReadOnlyList<PackageDecisionRecordRow> DecisionRecords,
    IReadOnlyList<PackageDecisionSource> Sources,
    IReadOnlyList<PackageDecisionCorrelationRow> ExactMatches,
    IReadOnlyList<PackageDecisionCorrelationRow> DigestMismatches,
    IReadOnlyList<PackageDecisionCorrelationRow> PossibleMatches,
    IReadOnlyList<PackageDecisionCorrelationRow> AmbiguousReferences,
    IReadOnlyList<PackageDecisionExclusion> ExcludedSources,
    IReadOnlyList<PackageDecisionStaleReference> StaleReferences,
    IReadOnlyList<PackageDecisionExternalReference> RuntimeUnprovenReferences,
    IReadOnlyList<PackageDecisionGap> Gaps,
    PackageDecisionSummary Summary,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<PackageDecisionArtifactChange>? ArtifactChanges = null,
    IReadOnlyList<PackageDecisionAdvisoryClaim>? AdvisoryClaims = null,
    object? PathContext = null,
    object? ReverseContext = null);

public sealed record PackageDecisionQuery(
    string DecisionPathHash,
    string IndexPathHash,
    string Source,
    string? Ecosystem,
    string? DecisionId,
    string? Classification,
    int MaxFindings,
    int MaxGaps,
    bool ExitCode,
    string? AsOf);

public sealed record PackageDecisionRecordRow(
    string? DecisionId,
    string? DecisionKind,
    string? Ecosystem,
    string? PackageName,
    string? ArtifactVersion,
    string? RegistryOrigin,
    string? ArtifactDigestAlgorithm,
    string? ArtifactDigest,
    string? ProducerId,
    string? PolicyVersion,
    string? DecisionTimeUtc,
    string? RecordDigest,
    string? Classification,
    string RuleId,
    string EvidenceTier,
    string? SupersedesDecisionId = null,
    string? SourceRepoHash = null,
    string? SourceCommitSha = null);

public sealed record PackageDecisionSource(
    string Label,
    string SourceIndexId,
    string ScanId,
    string RepoIdentityHash,
    string? CommitSha,
    string ScannerVersion,
    string? Language,
    string AnalysisLevel,
    string BuildStatus,
    string CoverageStatus,
    string? ScannedAt,
    string? ContainerLabel = null,
    string? OriginalSourceLabel = null);

public sealed record PackageDecisionContext(
    string Status,
    string Classification,
    IReadOnlyList<PackageDecisionContextRow> Rows,
    IReadOnlyList<PackageDecisionGap> Gaps,
    int OmittedCount,
    IReadOnlyList<string> Limitations);

public sealed record PackageDecisionContextRow(
    string ContextId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string SourceLabel,
    string SourceIndexId,
    string FactId,
    string PackageName,
    string Message,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<KeyValuePair<string, string>> Metadata,
    string FilePath,
    int StartLine,
    int EndLine,
    string? CommitSha,
    string? ExtractorId,
    string? ExtractorVersion,
    IReadOnlyList<string>? RuleIds = null);

public sealed record PackageDecisionCorrelationRow(
    string RowId,
    string Classification,
    string? MatchBasis,
    string DecisionId,
    string DecisionKind,
    string Ecosystem,
    string PackageName,
    string ArtifactVersion,
    string? RegistryOriginJoin,
    string SourceLabel,
    string SourceIndexId,
    string ScanId,
    string RepoIdentityHash,
    string? CommitSha,
    string DependencyRelation,
    PackageDecisionEvidence Evidence,
    bool SnapshotPredatesDecision,
    IReadOnlyList<PackageDecisionNote> Notes);

public sealed record PackageDecisionEvidence(
    string FactId,
    string OriginalFactId,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? ExtractorId,
    string? ExtractorVersion,
    string FilePath,
    int StartLine,
    int EndLine,
    string? ResolvedVersion = null,
    string? LockfilePath = null,
    string? LockfileHash = null,
    string? ArtifactDigestAlgorithm = null,
    string? ArtifactDigest = null,
    string? Version = null,
    string? VersionHash = null);

public sealed record PackageDecisionNote(string Code, string Message);
public sealed record PackageDecisionExclusion(string RowId, string Classification, string SourceLabel, string SourceIndexId, string ScanId, string? CommitSha, string RuleId, string EvidenceTier, string Message);
public sealed record PackageDecisionStaleReference(string RowId, string Classification, string DecisionId, string SourceLabel, string ScanId, string? CommitSha, string RuleId, string EvidenceTier, bool SnapshotPredatesDecision, string Message);
public sealed record PackageDecisionExternalReference(
    string RowId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string Message,
    string? ReferenceId = null,
    string? ReferenceKind = null,
    string? Ecosystem = null,
    string? PackageName = null,
    string? ArtifactVersion = null,
    string? RegistryOrigin = null,
    string? ArtifactDigestAlgorithm = null,
    string? ArtifactDigest = null,
    string? ProducerId = null,
    string? ProducerVersion = null,
    string? SourceRepoHash = null,
    string? CommitSha = null,
    string? JoinBasis = null,
    IReadOnlyList<string>? MatchedSourceLabels = null,
    IReadOnlyList<string>? MatchedFactIds = null);
public sealed record PackageDecisionChangeSide(
    string Side,
    string SourceLabel,
    string SourceIndexId,
    string ScanId,
    string RepoIdentityHash,
    string? CommitSha,
    PackageDecisionEvidence Evidence);
public sealed record PackageDecisionArtifactChange(
    string ChangeId,
    string Classification,
    string ChangeKind,
    string DecisionId,
    string DecisionKind,
    string Ecosystem,
    string PackageName,
    string ArtifactVersion,
    string SourceLabel,
    PackageDecisionChangeSide? Before,
    PackageDecisionChangeSide? After,
    string RuleId,
    string EvidenceTier,
    string Message,
    IReadOnlyList<PackageDecisionNote> Notes);
public sealed record PackageDecisionGap(
    string GapId,
    string Classification,
    string Message,
    string RuleId,
    string EvidenceTier,
    string? DecisionId = null,
    string? SourceLabel = null,
    string? Ecosystem = null,
    string? SourceIndexId = null,
    string? ScanId = null,
    string? CommitSha = null,
    string? ExtractorId = null,
    string? ExtractorVersion = null,
    string? FilePath = null,
    int? StartLine = null,
    int? EndLine = null,
    IReadOnlyList<string>? SupportingFactIds = null);

public sealed record PackageDecisionSummary(
    int SourceCount,
    int RecordCount,
    int ExactCount,
    int DigestMismatchCount,
    int PossibleCount,
    int AmbiguousCount,
    int ExcludedCount,
    int StaleCount,
    int RuntimeUnprovenCount,
    int GapCount,
    string Coverage,
    bool FindingCapReached,
    bool GapCapReached,
    int ArtifactChangeCount = 0);

public static class PackageDecisionCorrelationReporter
{
    public const string RuleId = "package.decision.correlation.v1";
    public const string AdvisoryRuleId = PackageDecisionAdvisoryProfileReader.RuleId;
    private const string SwiftDependencyLockfileFactType = "SwiftDependencyLockfileEntryDeclared";
    private const string Version = "1.0";
    private const string CrossSnapshotWording = "cross-snapshot portfolio evidence, not a single coherent release state";
    private const string RuntimeUnprovenLimitation = "TraceMap did not verify the build, deployment, installation, reachability, or runtime load.";
    private const string AdvisoryLimitation = "Advisory claims are external producer opinions rendered with producer provenance; TraceMap neither validates nor endorses them and never converts them into facts, rungs, vulnerabilities, or severity statements.";
    private const int MaxComparisonFactsPerPairing = 1000;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };
    private static readonly IReadOnlyList<string> Limitations =
    [
        "Correlation is static evidence over snapshots. It does not prove restore success, installed versions, runtime loading, deployment reachability, exploitability, or business impact.",
        "Exact matches prove only that the decided artifact identity appears in snapshot evidence; they do not prove execution, harm, or that remediation is required.",
        "Possible matches cannot distinguish whether a reference is the decided artifact; they bound review and never conclude exact identity.",
        "Excluded sources prove absence only within the scanned snapshot's stated coverage; reduced coverage converts exclusion into a gap.",
        "Record digests prove record and artifact-identity integrity; they do not authenticate the producer or confer authority. Provenance is lineage, not trust.",
        "scannedAt is producer-declared and non-authoritative; staleness flags are advisory context.",
        "Adapter capability varies by ecosystem. Missing lockfile digest and direct/transitive capability are reported as explicit gaps.",
        $"Before/after comparison retains bounded evidence representatives and examines at most {MaxComparisonFactsPerPairing} matching facts per decision/source pairing; exceeding that bound emits TruncatedByLimit and suppresses the affected change claim.",
        "This command is read-only evidence reporting and never grants admission, rejection, revocation, blocking, approval, or enforcement authority."
    ];

    public static async Task<PackageDecisionResult> WriteAsync(PackageDecisionOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.DecisionPath)) throw new ArgumentException("package-decision requires --decision <path>.", nameof(options));
        var comparisonMode = !string.IsNullOrWhiteSpace(options.BeforeManifestPath) || !string.IsNullOrWhiteSpace(options.AfterManifestPath);
        if (comparisonMode && (string.IsNullOrWhiteSpace(options.BeforeManifestPath) || string.IsNullOrWhiteSpace(options.AfterManifestPath)))
            throw new ArgumentException("package-decision requires --before-manifest and --after-manifest together.");
        var inputPaths = InputPaths(options);
        if (!comparisonMode && inputPaths.Count == 0 && string.IsNullOrWhiteSpace(options.ManifestPath)) throw new ArgumentException("package-decision requires --index <path> or --manifest <portfolio.json>.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputPath)) throw new ArgumentException("package-decision requires --out <path>.", nameof(options));
        var format = options.Format.Equals("md", StringComparison.OrdinalIgnoreCase) ? "markdown" : options.Format.ToLowerInvariant();
        if (format is not "markdown" and not "json") throw new ArgumentException("package-decision --format must be markdown or json.", nameof(options));
        if (options.IndexPaths is { Count: > 0 } && options.Labels is { Count: > 0 } && options.IndexPaths.Count != options.Labels.Count)
            throw new ArgumentException("package-decision requires one --label for each --index.", nameof(options));
        if (!string.IsNullOrWhiteSpace(options.ManifestPath) && inputPaths.Count > 0)
            throw new ArgumentException("package-decision --manifest cannot be mixed with --index inputs.", nameof(options));
        if (comparisonMode && (inputPaths.Count > 0 || !string.IsNullOrWhiteSpace(options.ManifestPath)))
            throw new ArgumentException("package-decision --before-manifest/--after-manifest cannot be mixed with --index or --manifest inputs.", nameof(options));
        var inputSpecs = ResolveInputSpecs(options);
        RejectOutputAlias(
            options.DecisionPath,
            inputSpecs.Select(spec => spec.IndexPath)
                .Append(options.ManifestPath)
                .Append(options.BeforeManifestPath)
                .Append(options.AfterManifestPath)
                .Append(options.AdvisoryProfilePath)
                .Append(options.DeploymentReferencesPath)
                .OfType<string>()
                .ToArray(),
            options.OutputPath);
        DateTimeOffset? asOf = null;
        if (!string.IsNullOrWhiteSpace(options.AsOf))
        {
            if (!TryParseAsOf(options.AsOf, out var parsedAsOf))
                throw new InvalidDataException("package-decision --as-of must be RFC3339 UTC.");
            asOf = parsedAsOf;
        }

        var admission = await PackageDecisionRecordReader.ReadAsync(options.DecisionPath, cancellationToken);
        if (!admission.Accepted)
            throw new InvalidDataException("package-decision decision input admission failed.");
        PackageDecisionAdvisoryProfile? advisoryProfile = null;
        if (!string.IsNullOrWhiteSpace(options.AdvisoryProfilePath))
        {
            advisoryProfile = await PackageDecisionAdvisoryProfileReader.ReadAsync(options.AdvisoryProfilePath, cancellationToken);
            if (!advisoryProfile.Accepted)
                throw new InvalidDataException("package-decision advisory profile admission failed.");
        }

        PackageDecisionDeploymentReferenceAdmission? deploymentReferences = null;
        if (!string.IsNullOrWhiteSpace(options.DeploymentReferencesPath))
        {
            deploymentReferences = await PackageDecisionDeploymentReferenceReader.ReadAsync(options.DeploymentReferencesPath, cancellationToken);
            if (!deploymentReferences.Accepted)
                throw new InvalidDataException("package-decision deployment reference admission failed.");
        }

        var index = await ReadInputsAsync(inputSpecs, cancellationToken);
        var unfilteredSources = index.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray();
        var sources = unfilteredSources;
        if (!string.IsNullOrWhiteSpace(options.Source))
            sources = sources.Where(source => SourceMatches(index.RawLabels.GetValueOrDefault(source.SourceIndexId) ?? source.Label, options.Source)).ToArray();

        var records = admission.Records
            .Where(record => string.IsNullOrWhiteSpace(options.Ecosystem) || string.Equals(record.Ecosystem, options.Ecosystem, StringComparison.OrdinalIgnoreCase))
            .Where(record => string.IsNullOrWhiteSpace(options.DecisionId) || string.Equals(record.DecisionId, options.DecisionId, StringComparison.Ordinal))
            .OrderBy(record => record.Ecosystem, StringComparer.Ordinal)
            .ThenBy(record => NormalizeName(record.Ecosystem, record.PackageName), StringComparer.Ordinal)
            .ThenBy(record => record.ArtifactVersion, StringComparer.Ordinal)
            .ThenBy(record => record.ProducerId, StringComparer.Ordinal)
            .ThenBy(record => record.DecisionId, StringComparer.Ordinal)
            .ToArray();
        var boundedAdmissionGaps = admission.Gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).Take(Math.Max(1, options.MaxGaps)).ToArray();
        var recordRows = admission.Records.Select(record => ToRecordRow(record, records.Contains(record) ? null : "SelectorNoMatch")).Cast<PackageDecisionRecordRow>().ToList();
        foreach (var gap in boundedAdmissionGaps)
            recordRows.Add(new PackageDecisionRecordRow(gap.DecisionId, null, null, null, null, null, null, null, gap.ProducerId, null, null, null, gap.Classification, gap.RuleId, gap.EvidenceTier));

        var exact = new List<PackageDecisionCorrelationRow>();
        var mismatch = new List<PackageDecisionCorrelationRow>();
        var possible = new List<PackageDecisionCorrelationRow>();
        var ambiguous = new List<PackageDecisionCorrelationRow>();
        var excluded = new List<PackageDecisionExclusion>();
        var stale = new List<PackageDecisionStaleReference>();
        var gaps = boundedAdmissionGaps.Select(gap => new PackageDecisionGap(gap.GapId, gap.Classification, gap.Message, gap.RuleId, gap.EvidenceTier, gap.DecisionId)).ToList();
        var externalInputGaps = (advisoryProfile?.Gaps ?? []).Select(gap => new PackageDecisionGap(gap.GapId, gap.Classification, gap.Message, gap.RuleId, gap.EvidenceTier))
            .Concat((deploymentReferences?.Gaps ?? []).Select(gap => new PackageDecisionGap(gap.GapId, gap.Classification, gap.Message, gap.RuleId, gap.EvidenceTier)))
            .OrderBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
        foreach (var gap in externalInputGaps.Take(Math.Max(1, options.MaxGaps) - gaps.Count))
            gaps.Add(gap);
        var findingCapReached = false;
        var gapCapReached = admission.Gaps.Count + externalInputGaps.Length > Math.Max(1, options.MaxGaps);
        var pairingRows = new Dictionary<(string RecordKey, string SourceIndexId), List<PackageDecisionCorrelationRow>>();
        var truncatedPairings = new HashSet<(string RecordKey, string SourceIndexId)>();
        var blockedSources = new HashSet<string>(StringComparer.Ordinal);
        if (sources.Length == 0 && options.Source is not null)
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector-source:{Hash(options.Source)}", "SelectorNoMatch", "The requested source selector matched no source snapshot.", RuleId, EvidenceTiers.Tier4Unknown, SourceLabel: SafeInputLabel(options.Source)));
        foreach (var record in records)
        {
            if (findingCapReached && !comparisonMode)
                break;
            foreach (var source in sources)
            {
                if (findingCapReached && !comparisonMode)
                    break;
                var recordKey = $"{record.ProducerId}\u001f{record.DecisionId}";
                if (!pairingRows.TryAdd((recordKey, source.SourceIndexId), []))
                    pairingRows[(recordKey, source.SourceIndexId)] = [];
                var sourceFacts = PackageEvidenceRows(index.Facts, source.SourceIndexId);
                var rows = sourceFacts.Where(fact => string.Equals(fact.Properties.GetValueOrDefault("ecosystem"), record.Ecosystem, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(NormalizeName(record.Ecosystem, record.PackageName), NormalizeName(record.Ecosystem, fact.Properties.GetValueOrDefault("packageName") ?? string.Empty), StringComparison.Ordinal)).ToArray();
                var sourceGaps = index.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId).ToArray();
                if (sourceGaps.Any(gap => string.Equals(gap.Category, "DuplicateSourceIdentity", StringComparison.Ordinal)))
                {
                    blockedSources.Add(source.SourceIndexId);
                    AddGap(gaps, options.MaxGaps, ref gapCapReached, SourceGap("UnknownAnalysisGap", "Duplicate portfolio source identity prevents a trustworthy correlation for this pairing.", record, source));
                    continue;
                }

                var capabilityFacts = rows.Length == 0 ? sourceFacts : rows;
                if (!FullCommit(source.CommitSha))
                {
                    blockedSources.Add(source.SourceIndexId);
                    AddGap(gaps, options.MaxGaps, ref gapCapReached, SourceGap("UnknownAnalysisGap", "The source snapshot does not provide a full commit SHA; correlation is unavailable.", record, source));
                    continue;
                }
                if (capabilityFacts.All(fact => string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("artifactDigest"))))
                    AddGap(gaps, options.MaxGaps, ref gapCapReached, SourceGap("LockfileDigestUnavailable", "The selected package evidence does not provide an artifact digest capability.", record, source));
                if (capabilityFacts.All(fact => !fact.Properties.ContainsKey("dependencyRelation")))
                    AddGap(gaps, options.MaxGaps, ref gapCapReached, SourceGap("DirectTransitiveUnavailable", "The selected package evidence does not prove direct versus transitive dependency relation.", record, source));

                if (rows.Length == 0)
                {
                    if (SourceHasCredibilityGap(source, index.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId)) || !HasEcosystemCapability(sourceFacts, record.Ecosystem))
                    {
                        AddGap(gaps, options.MaxGaps, ref gapCapReached, SourceGap("UnknownAnalysisGap", "No matching package evidence was found under reduced coverage or unsupported ecosystem capability.", record, source));
                    }
                    else
                    {
                        excluded.Add(new PackageDecisionExclusion($"pd-excluded:{Hash(record.DecisionId + source.SourceIndexId)}", "ExcludedSource", source.Label, source.SourceIndexId, source.ScanId, SafeCommit(source.CommitSha), RuleId, EvidenceTiers.Tier2Structural, "The package was not referenced in this statically covered snapshot; this does not prove safety."));
                    }
                    continue;
                }

                var processedPairingFacts = 0;
                foreach (var fact in rows)
                {
                    if (comparisonMode && processedPairingFacts++ >= MaxComparisonFactsPerPairing)
                    {
                        truncatedPairings.Add((recordKey, source.SourceIndexId));
                        AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap(
                            $"pd-comparison-cap:{Hash(string.Join('\u001f', recordKey, source.SourceIndexId))}",
                            "TruncatedByLimit",
                            "The comparison fact limit was reached for one decision/source pairing; no artifact change is claimed for that pairing.",
                            RuleId,
                            EvidenceTiers.Tier4Unknown,
                            DecisionId: record.DecisionId,
                            SourceLabel: source.Label,
                            SourceIndexId: source.SourceIndexId,
                            ScanId: source.ScanId,
                            CommitSha: SafeCommit(source.CommitSha)));
                        break;
                    }

                    var row = Correlate(record, source, fact, index.ScannedAt.GetValueOrDefault(source.SourceIndexId), asOf);
                    if (comparisonMode)
                        AddPairingRepresentative(pairingRows[(recordKey, source.SourceIndexId)], row);
                    if (CountRows(exact, mismatch, possible, ambiguous) >= options.MaxFindings)
                    {
                        findingCapReached = true;
                        if (!comparisonMode)
                            break;
                        continue;
                    }

                    switch (row.Classification)
                    {
                        case "ExactArtifactMatch": exact.Add(row); break;
                        case "ArtifactDigestMismatch": mismatch.Add(row); break;
                        case "PossibleNameVersionMatch": possible.Add(row); break;
                        default: ambiguous.Add(row); break;
                    }
                    if (row.SnapshotPredatesDecision)
                        stale.Add(new PackageDecisionStaleReference(row.RowId, row.Classification, row.DecisionId, row.SourceLabel, row.ScanId, row.CommitSha, RuleId, row.Evidence.EvidenceTier, true, "The scan predates the producer-declared decision time; later remediation is not represented."));
                }
            }
        }

        if (options.Ecosystem is not null && !admission.Records.Any(record => string.Equals(record.Ecosystem, options.Ecosystem, StringComparison.OrdinalIgnoreCase)))
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector-ecosystem:{Hash(options.Ecosystem)}", "SelectorNoMatch", "The requested ecosystem selector matched no admitted decision record.", RuleId, EvidenceTiers.Tier4Unknown, Ecosystem: options.Ecosystem));
        if (options.DecisionId is not null && !admission.Records.Any(record => string.Equals(record.DecisionId, options.DecisionId, StringComparison.Ordinal)))
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector-decision:{Hash(options.DecisionId)}", "SelectorNoMatch", "The requested decision selector matched no admitted decision record.", RuleId, EvidenceTiers.Tier4Unknown, DecisionId: options.DecisionId));
        if (options.Classification is not null && CountRows(Filter(exact, options.Classification), Filter(mismatch, options.Classification), Filter(possible, options.Classification), Filter(ambiguous, options.Classification)) == 0)
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector-classification:{Hash(options.Classification)}", "SelectorNoMatch", "The requested classification selector matched no correlation row.", RuleId, EvidenceTiers.Tier4Unknown));
        if (records.Length == 0 && options.Source is null && options.Ecosystem is null && options.DecisionId is null)
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector:{Hash("selector")}", "SelectorNoMatch", "No admitted decision record was available for correlation.", RuleId, EvidenceTiers.Tier4Unknown));
        if (findingCapReached)
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-cap:{Hash("findings")}", "TruncatedByLimit", "The package decision correlation finding limit was reached; coverage gaps remain reported.", RuleId, EvidenceTiers.Tier4Unknown));

        var selectedExact = Filter(exact, options.Classification);
        var selectedMismatch = Filter(mismatch, options.Classification);
        var selectedPossible = Filter(possible, options.Classification);
        var selectedAmbiguous = Filter(ambiguous, options.Classification);
        var runtimeUnproven = BuildRuntimeUnprovenReferences(deploymentReferences, index, sources, options.Ecosystem);
        var artifactChanges = comparisonMode
            ? BuildArtifactChanges(records, unfilteredSources, sources, pairingRows, truncatedPairings, blockedSources, index, options, gaps, ref gapCapReached)
            : null;
        var contextRows = selectedExact.Concat(selectedMismatch).Concat(selectedPossible).Concat(selectedAmbiguous).ToArray();
        var pathContext = options.IncludePaths ? BuildContext(index, contextRows, options, reverse: false) : null;
        var reverseContext = options.IncludeReverse ? BuildContext(index, contextRows, options, reverse: true) : null;
        var limitations = Limitations.ToList();
        if (advisoryProfile is not null) limitations.Add(AdvisoryLimitation);
        if (deploymentReferences is not null) limitations.Add($"{RuntimeUnprovenLimitation} Deployment references never upgrade a rung, never count as exact matches, and never trigger --exit-code.");
        if (comparisonMode) limitations.Add($"Before/after artifact changes are {CrossSnapshotWording}; possible-only changes never claim artifact replacement.");
        var report = new PackageDecisionDocument(
            "package-decision-correlation", Version, comparisonMode ? "DecisionComparisonV1" : "DecisionSnapshotV1",
            new PackageDecisionQuery($"value-hash:{CombinedReportHelpers.Hash(options.DecisionPath, 16)}", $"value-hash:{InputIdentity(inputSpecs, options.ManifestPath, options.BeforeManifestPath, options.AfterManifestPath)}", options.Source is null ? "default" : SafeInputLabel(options.Source), options.Ecosystem, options.DecisionId, options.Classification, options.MaxFindings, options.MaxGaps, options.ExitCode, options.AsOf),
            recordRows.OrderBy(row => row.Classification ?? string.Empty, StringComparer.Ordinal).ThenBy(row => row.ProducerId ?? string.Empty, StringComparer.Ordinal).ThenBy(row => row.DecisionId ?? string.Empty, StringComparer.Ordinal).ToArray(),
            sources.Select(source => ToSource(source, index.ScannedAt.GetValueOrDefault(source.SourceIndexId), index.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId))).ToArray(),
            selectedExact, selectedMismatch, selectedPossible, selectedAmbiguous,
            excluded.OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.RowId, StringComparer.Ordinal).ToArray(),
            stale.OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.RowId, StringComparer.Ordinal).ToArray(),
            runtimeUnproven,
            gaps.OrderBy(gap => gap.Classification, StringComparer.Ordinal).ThenBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            new PackageDecisionSummary(sources.Length, records.Length, selectedExact.Count, selectedMismatch.Count, selectedPossible.Count, selectedAmbiguous.Count, excluded.Count, stale.Count, runtimeUnproven.Count, gaps.Count, gaps.Count > 0 || findingCapReached ? "ReducedCoverage" : "FullEvidenceAvailable", findingCapReached, gapCapReached, artifactChanges?.Count ?? 0),
            limitations,
            ArtifactChanges: artifactChanges,
            AdvisoryClaims: advisoryProfile?.Claims,
            PathContext: pathContext,
            ReverseContext: reverseContext);
        var (markdownPath, jsonPath) = await CombinedReportHelpers.WriteOutputsAsync(options.OutputPath, format, "package-decision-report.md", "package-decision-report.json", report, RenderMarkdown, JsonOptions, cancellationToken);
        return new PackageDecisionResult(report, markdownPath, jsonPath);
    }

    private static PackageDecisionRecordRow ToRecordRow(PackageDecisionRecord record, string? classification) => new(record.DecisionId, record.DecisionKind, record.Ecosystem, record.PackageName, record.ArtifactVersion, record.RegistryOrigin, record.ArtifactDigestAlgorithm, record.ArtifactDigest, record.ProducerId, record.PolicyVersion, record.DecisionTimeUtc.ToString("O", CultureInfo.InvariantCulture), record.RecordDigest, classification, PackageDecisionRecordReader.RuleId, PackageDecisionRecordReader.EvidenceTier, record.SupersedesDecisionId, record.SourceRepoHash, record.SourceCommitSha);

    private static PackageDecisionCorrelationRow Correlate(PackageDecisionRecord record, CombinedReportSource source, CombinedFactRow fact, DateTimeOffset? scannedAt, DateTimeOffset? asOf)
    {
        var properties = fact.Properties;
        var resolved = properties.GetValueOrDefault("resolvedVersion");
        var version = properties.GetValueOrDefault("version");
        var resolvedVersionEqual = resolved is not null && string.Equals(resolved.Trim(), record.ArtifactVersion.Trim(), StringComparison.Ordinal);
        var declaredVersionEqual = resolved is null && version is not null && IsLiteralVersion(version) && string.Equals(version.Trim(), record.ArtifactVersion.Trim(), StringComparison.Ordinal);
        var evidenceVersion = resolved ?? version;
        var evidenceDigest = properties.GetValueOrDefault("artifactDigest");
        var evidenceAlgorithm = properties.GetValueOrDefault("artifactDigestAlgorithm");
        var digestEqual = record.ArtifactDigest is not null && evidenceDigest is not null && string.Equals(record.ArtifactDigestAlgorithm, evidenceAlgorithm, StringComparison.Ordinal) && FixedTimeDigestEquals(record.ArtifactDigest, evidenceDigest);
        var versionEqual = resolvedVersionEqual || declaredVersionEqual;
        var digestMismatch = record.ArtifactDigest is not null && evidenceDigest is not null && versionEqual && !digestEqual;
        var classification = digestEqual && resolvedVersionEqual ? "ExactArtifactMatch" : digestMismatch ? "ArtifactDigestMismatch" : versionEqual ? "PossibleNameVersionMatch" : "AmbiguousIdentity";
        var matchBasis = classification == "PossibleNameVersionMatch" ? (resolvedVersionEqual ? "resolved-version" : declaredVersionEqual ? "declared-exact" : null) : null;
        if (classification == "PossibleNameVersionMatch" && matchBasis is null) classification = "AmbiguousIdentity";
        var origin = properties.GetValueOrDefault("registryOrigin");
        var originJoin = record.RegistryOrigin is null || origin is null ? "absent" : string.Equals(record.RegistryOrigin, origin, StringComparison.OrdinalIgnoreCase) ? "exact" : "origin-mismatch";
        var notes = new List<PackageDecisionNote>();
        if (originJoin == "origin-mismatch") notes.Add(new("registry-origin-mismatch", "Registry origins differ; the possible rung is capped unless digests are equal."));
        if (classification == "AmbiguousIdentity") notes.Add(new(digestEqual ? "digest-version-conflict" : "version-unknown", digestEqual ? "Artifact digest matched but version evidence conflicted; TraceMap did not choose an identity." : "Name evidence exists but exact version evidence is unavailable; TraceMap does not resolve ranges."));
        if (asOf.HasValue && record.DecisionTimeUtc > asOf.Value) notes.Add(new("not-yet-effective", "The producer-declared decision time is later than the supplied deterministic --as-of value."));
        if (properties.ContainsKey("lockfilePath") && evidenceDigest is null) notes.Add(new("LockfileDigestUnavailable", "Lockfile evidence does not include an artifact digest."));
        if (!properties.ContainsKey("dependencyRelation")) notes.Add(new("DirectTransitiveUnavailable", "Direct versus transitive relation is not proven by this evidence."));
        var stale = scannedAt.HasValue && scannedAt.Value < record.DecisionTimeUtc;
        var rowId = $"pdr:{Hash(string.Join('\u001f', record.ProducerId, record.DecisionId, source.SourceIndexId, fact.CombinedFactId))}";
        var safeResolvedVersion = IsLiteralVersion(resolved ?? string.Empty) ? resolved : null;
        var safeVersion = IsLiteralVersion(version ?? string.Empty) ? version : null;
        var safeVersionHash = SafeVersionHash(properties.GetValueOrDefault("versionHash")) ?? (version is not null && safeVersion is null ? $"version-hash:{Hash(version)}" : null);
        var evidence = new PackageDecisionEvidence(fact.CombinedFactId, fact.OriginalFactId, fact.FactType, fact.RuleId, fact.EvidenceTier, fact.ExtractorId, fact.ExtractorVersion, CombinedReportHelpers.SafePath(fact.FilePath), fact.StartLine, fact.EndLine, safeResolvedVersion, SafeProperty(properties, "lockfilePath"), SafeLockfileHash(properties.GetValueOrDefault("lockfileHash")), evidenceAlgorithm, SafeDigest(evidenceDigest, evidenceAlgorithm), safeVersion, safeVersionHash);
        return new PackageDecisionCorrelationRow(rowId, classification, matchBasis, record.DecisionId, record.DecisionKind, record.Ecosystem, record.PackageName, record.ArtifactVersion, originJoin, source.Label, source.SourceIndexId, source.ScanId, $"repo-hash:{CombinedReportHelpers.Hash(source.RepoName, 16)}", SafeCommit(source.CommitSha), properties.GetValueOrDefault("dependencyRelation") ?? "unknown", evidence, stale, notes.OrderBy(note => note.Code, StringComparer.Ordinal).ToArray());
    }

    private static IReadOnlyList<PackageDecisionExternalReference> BuildRuntimeUnprovenReferences(
        PackageDecisionDeploymentReferenceAdmission? admission,
        IndexRead index,
        IReadOnlyList<CombinedReportSource> sources,
        string? ecosystemSelector)
    {
        if (admission is null)
            return [];

        var references = admission.References;
        if (!string.IsNullOrWhiteSpace(ecosystemSelector))
            references = references.Where(reference => string.Equals(reference.Ecosystem, ecosystemSelector, StringComparison.OrdinalIgnoreCase)).ToArray();

        var evidenceBySource = sources
            .Select(source => (source, rows: PackageEvidenceRows(index.Facts, source.SourceIndexId)))
            .ToArray();
        var result = new List<PackageDecisionExternalReference>();
        foreach (var reference in references)
        {
            var joined = evidenceBySource
                .SelectMany(entry => entry.rows
                    .Where(fact => string.Equals(fact.Properties.GetValueOrDefault("ecosystem"), reference.Ecosystem, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(NormalizeName(reference.Ecosystem, reference.PackageName), NormalizeName(reference.Ecosystem, fact.Properties.GetValueOrDefault("packageName") ?? string.Empty), StringComparison.Ordinal))
                    .Select(fact => (entry.source, fact)))
                .ToArray();
            bool NameVersionJoin(CombinedFactRow fact)
            {
                var resolved = fact.Properties.GetValueOrDefault("resolvedVersion");
                var version = fact.Properties.GetValueOrDefault("version");
                var resolvedEqual = resolved is not null && string.Equals(resolved.Trim(), reference.ArtifactVersion.Trim(), StringComparison.Ordinal);
                var declaredEqual = resolved is null && version is not null && IsLiteralVersion(version) && string.Equals(version.Trim(), reference.ArtifactVersion.Trim(), StringComparison.Ordinal);
                return resolvedEqual || declaredEqual;
            }

            var evaluated = joined.Select(pair =>
            {
                var digest = pair.fact.Properties.GetValueOrDefault("artifactDigest");
                var algorithm = pair.fact.Properties.GetValueOrDefault("artifactDigestAlgorithm");
                var nameVersionMatches = NameVersionJoin(pair.fact);
                var digestMatches = nameVersionMatches && reference.ArtifactDigest is not null && digest is not null
                    && string.Equals(reference.ArtifactDigestAlgorithm, algorithm, StringComparison.Ordinal)
                    && FixedTimeDigestEquals(reference.ArtifactDigest, digest);
                return (pair.source, pair.fact, digestMatches, nameVersionMatches);
            }).ToArray();
            var identityJoined = evaluated.Where(pair => pair.digestMatches || pair.nameVersionMatches).ToArray();
            var joinBasis = identityJoined.Any(pair => pair.digestMatches) ? "digest" : identityJoined.Length > 0 ? "name-version" : "unmatched";
            var matchedLabels = identityJoined.Select(pair => pair.source.Label)
                .Concat(sources.Where(source => reference.CommitSha is not null && FullCommit(source.CommitSha) && string.Equals(source.CommitSha, reference.CommitSha, StringComparison.OrdinalIgnoreCase)).Select(source => source.Label))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(label => label, StringComparer.Ordinal)
                .Take(10)
                .ToArray();
            var matchedFactIds = identityJoined.Select(pair => pair.fact.CombinedFactId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Take(20)
                .ToArray();
            result.Add(new PackageDecisionExternalReference(
                $"pdr-ref:{Hash(string.Join('\u001f', reference.ProducerId, reference.ReferenceId))}",
                "RuntimeUnprovenReference",
                RuleId,
                EvidenceTiers.Tier4Unknown,
                RuntimeUnprovenLimitation,
                reference.ReferenceId,
                reference.ReferenceKind,
                reference.Ecosystem,
                reference.PackageName,
                reference.ArtifactVersion,
                reference.RegistryOrigin,
                reference.ArtifactDigestAlgorithm,
                reference.ArtifactDigest,
                reference.ProducerId,
                reference.ProducerVersion,
                reference.SourceRepoHash,
                reference.CommitSha,
                joinBasis,
                matchedLabels,
                matchedFactIds));
        }

        return result
            .OrderBy(row => row.Ecosystem, StringComparer.Ordinal)
            .ThenBy(row => row.PackageName, StringComparer.Ordinal)
            .ThenBy(row => row.ReferenceId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<PackageDecisionArtifactChange> BuildArtifactChanges(
        IReadOnlyList<PackageDecisionRecord> records,
        IReadOnlyList<CombinedReportSource> unfilteredSources,
        IReadOnlyList<CombinedReportSource> selectedSources,
        IReadOnlyDictionary<(string RecordKey, string SourceIndexId), List<PackageDecisionCorrelationRow>> pairingRows,
        IReadOnlySet<(string RecordKey, string SourceIndexId)> truncatedPairings,
        IReadOnlySet<string> blockedSources,
        IndexRead index,
        PackageDecisionOptions options,
        List<PackageDecisionGap> gaps,
        ref bool gapCapReached)
    {
        var selectedIds = selectedSources.Select(source => source.SourceIndexId).ToHashSet(StringComparer.Ordinal);
        var changes = new List<PackageDecisionArtifactChange>();
        foreach (var labelGroup in unfilteredSources
            .Where(source => index.SourceSides.ContainsKey(source.SourceIndexId))
            .GroupBy(source => index.SourceSides[source.SourceIndexId].BaseLabel, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var before = labelGroup.Where(source => index.SourceSides[source.SourceIndexId].Side == "before").ToArray();
            var after = labelGroup.Where(source => index.SourceSides[source.SourceIndexId].Side == "after").ToArray();
            if (before.Length > 1 || after.Length > 1)
            {
                AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap(
                    $"pd-comparison-label:{Hash(labelGroup.Key)}",
                    "UnknownAnalysisGap",
                    "The comparison label matched multiple sources on one side; the pairing is ambiguous and no artifact change was claimed.",
                    RuleId,
                    EvidenceTiers.Tier4Unknown,
                    SourceLabel: labelGroup.Key));
                continue;
            }

            var identityAmbiguous = before.Length == 1 && after.Length == 1
                && !string.Equals(CombinedReportHelpers.Hash(before[0].RepoName, 16), CombinedReportHelpers.Hash(after[0].RepoName, 16), StringComparison.Ordinal);
            if (identityAmbiguous)
            {
                AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap(
                    $"pd-comparison-identity:{Hash(labelGroup.Key)}",
                    "IdentityAmbiguous",
                    $"Source `{SafeInputLabel(labelGroup.Key)}` has different repo identity across portfolio snapshots.",
                    RuleId,
                    EvidenceTiers.Tier4Unknown,
                    SourceLabel: labelGroup.Key));
            }

            foreach (var record in records)
            {
                var recordKey = $"{record.ProducerId}\u001f{record.DecisionId}";
                var beforeSelected = before.Length == 1 && selectedIds.Contains(before[0].SourceIndexId);
                var afterSelected = after.Length == 1 && selectedIds.Contains(after[0].SourceIndexId);
                if (!beforeSelected && !afterSelected)
                    continue;
                // A label that exists on both sides is only comparable when both sides are in selector scope;
                // a filtered-out side is unseen, not absent, so no added/removed claim is made for it.
                if (before.Length == 1 && after.Length == 1 && (!beforeSelected || !afterSelected))
                    continue;
                var blockedSide = before.Any(source => blockedSources.Contains(source.SourceIndexId)) || after.Any(source => blockedSources.Contains(source.SourceIndexId));
                var bothSidesInScope = before.Length == 1 && after.Length == 1 && beforeSelected && afterSelected;
                var truncatedSide = before.Any(source => truncatedPairings.Contains((recordKey, source.SourceIndexId)))
                    || after.Any(source => truncatedPairings.Contains((recordKey, source.SourceIndexId)));
                if (truncatedSide)
                    continue;
                if (blockedSide && bothSidesInScope)
                {
                    AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap(
                        $"pd-comparison-blocked:{Hash(string.Join('\u001f', recordKey, labelGroup.Key))}",
                        "UnknownAnalysisGap",
                        "A comparison side has duplicate source identity or an unknown commit SHA; the pairing is not comparable and no artifact change was claimed.",
                        RuleId,
                        EvidenceTiers.Tier4Unknown,
                        DecisionId: record.DecisionId,
                        SourceLabel: labelGroup.Key));
                    continue;
                }

                var beforeRows = beforeSelected && before.Length == 1 && pairingRows.TryGetValue((recordKey, before[0].SourceIndexId), out var rowsBefore) ? rowsBefore : null;
                var afterRows = afterSelected && after.Length == 1 && pairingRows.TryGetValue((recordKey, after[0].SourceIndexId), out var rowsAfter) ? rowsAfter : null;
                var beforeMatch = MatchingRows(beforeRows);
                var afterMatch = MatchingRows(afterRows);
                if (beforeMatch.Count == 0 && afterMatch.Count == 0)
                    continue;
                if (bothSidesInScope && (beforeRows is null || afterRows is null))
                    continue;

                string changeKind;
                PackageDecisionChangeSide? beforeSide;
                PackageDecisionChangeSide? afterSide;
                var notes = new List<PackageDecisionNote>();
                if (beforeMatch.Count == 0)
                {
                    changeKind = "added";
                    beforeSide = null;
                    afterSide = ChangeSide("after", BestRow(afterMatch));
                }
                else if (afterMatch.Count == 0)
                {
                    changeKind = "removed";
                    beforeSide = ChangeSide("before", BestRow(beforeMatch));
                    afterSide = null;
                }
                else
                {
                    var beforeDigests = DistinctDigests(beforeMatch);
                    var afterDigests = DistinctDigests(afterMatch);
                    if (beforeDigests.Count > 1 || afterDigests.Count > 1)
                    {
                        beforeSide = ChangeSide("before", BestRow(beforeMatch));
                        afterSide = ChangeSide("after", BestRow(afterMatch));
                        changeKind = "identity-ambiguous";
                        notes.Add(new("digest-ambiguous-side", "One comparison side carries multiple distinct artifact digests for the decided name and version; TraceMap did not choose an identity."));
                    }
                    else if (beforeDigests.Count == 1 && afterDigests.Count == 1)
                    {
                        if (string.Equals(beforeDigests[0], afterDigests[0], StringComparison.Ordinal))
                            continue;
                        beforeSide = ChangeSide("before", BestRowForDigest(beforeMatch, beforeDigests[0]));
                        afterSide = ChangeSide("after", BestRowForDigest(afterMatch, afterDigests[0]));
                        changeKind = "artifact-replaced";
                    }
                    else
                    {
                        beforeSide = ChangeSide("before", BestRow(beforeMatch));
                        afterSide = ChangeSide("after", BestRow(afterMatch));
                        if (Fingerprint(BestRow(beforeMatch)) == Fingerprint(BestRow(afterMatch)))
                            continue;
                        changeKind = "possible-only";
                        notes.Add(new("digest-evidence-absent", "At least one comparison side lacks artifact digest evidence, so TraceMap did not claim artifact replacement."));
                    }
                }

                if (identityAmbiguous)
                {
                    changeKind = "identity-ambiguous";
                    notes.Add(new("identity-ambiguous", "The paired source label has different repo identity across portfolio snapshots; the change classification was downgraded."));
                }

                var classification = changeKind switch
                {
                    "artifact-replaced" => "ArtifactReplaced",
                    "identity-ambiguous" => "AmbiguousIdentityChange",
                    _ => "PossibleArtifactChange"
                };
                var tiers = new[] { beforeSide?.Evidence.EvidenceTier, afterSide?.Evidence.EvidenceTier };
                changes.Add(new PackageDecisionArtifactChange(
                    $"pdc:{Hash(string.Join('\u001f', record.ProducerId, record.DecisionId, labelGroup.Key, changeKind))}",
                    classification,
                    changeKind,
                    record.DecisionId,
                    record.DecisionKind,
                    record.Ecosystem,
                    record.PackageName,
                    record.ArtifactVersion,
                    SafeInputLabel(labelGroup.Key),
                    beforeSide,
                    afterSide,
                    RuleId,
                    WeakestTier(tiers),
                    ChangeMessage(changeKind),
                    notes.OrderBy(note => note.Code, StringComparer.Ordinal).ToArray()));
            }
        }

        var ordered = changes
            .OrderBy(change => ChangeRank(change.Classification))
            .ThenBy(change => change.Ecosystem, StringComparer.Ordinal)
            .ThenBy(change => change.PackageName, StringComparer.Ordinal)
            .ThenBy(change => change.ArtifactVersion, StringComparer.Ordinal)
            .ThenBy(change => change.ChangeId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length > Math.Max(1, options.MaxFindings))
        {
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap(
                $"pd-cap:{Hash("changes")}",
                "TruncatedByLimit",
                "The package decision artifact change limit was reached; change rows were truncated.",
                RuleId,
                EvidenceTiers.Tier4Unknown));
            return ordered.Take(Math.Max(1, options.MaxFindings)).ToArray();
        }

        return ordered;
    }

    private static string ChangeMessage(string changeKind) => changeKind switch
    {
        "artifact-replaced" => $"The same package name and exact version resolved to different artifact digests across the before and after portfolios; {CrossSnapshotWording}.",
        "possible-only" => $"Package evidence changed across the before and after portfolios, but at least one side lacks artifact digest evidence, so TraceMap did not claim artifact replacement; {CrossSnapshotWording}.",
        "added" => $"Evidence for the decided package version appears only in the after portfolio; {CrossSnapshotWording}.",
        "removed" => $"Evidence for the decided package version appears only in the before portfolio; {CrossSnapshotWording}.",
        _ => $"The paired source label has ambiguous or conflicting identity across the before and after portfolios, so TraceMap downgraded the change and did not claim artifact replacement; {CrossSnapshotWording}."
    };

    private static int ChangeRank(string classification) => classification switch
    {
        "ArtifactReplaced" => 0,
        "PossibleArtifactChange" => 1,
        _ => 2
    };

    private static IReadOnlyList<PackageDecisionCorrelationRow> MatchingRows(IReadOnlyList<PackageDecisionCorrelationRow>? rows) =>
        rows?.Where(row => row.Classification is "ExactArtifactMatch" or "ArtifactDigestMismatch" or "PossibleNameVersionMatch").ToArray() ?? [];

    private static PackageDecisionCorrelationRow BestRow(IReadOnlyList<PackageDecisionCorrelationRow> rows) =>
        rows.OrderBy(row => RungRank(row.Classification)).ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal).ThenBy(row => row.Evidence.StartLine).ThenBy(row => row.RowId, StringComparer.Ordinal).First();

    private static PackageDecisionCorrelationRow BestRowForDigest(IReadOnlyList<PackageDecisionCorrelationRow> rows, string digest) =>
        BestRow(rows.Where(row => string.Equals(row.Evidence.ArtifactDigest, digest, StringComparison.Ordinal)).ToArray());

    private static void AddPairingRepresentative(List<PackageDecisionCorrelationRow> rows, PackageDecisionCorrelationRow candidate)
    {
        if (candidate.Classification is not ("ExactArtifactMatch" or "ArtifactDigestMismatch" or "PossibleNameVersionMatch"))
            return;

        var digest = candidate.Evidence.ArtifactDigest;
        var matchingIndex = rows.FindIndex(row => string.Equals(row.Evidence.ArtifactDigest, digest, StringComparison.Ordinal));
        if (matchingIndex >= 0)
        {
            if (CompareRows(candidate, rows[matchingIndex]) < 0)
                rows[matchingIndex] = candidate;
            return;
        }

        if (digest is null || rows.Count(row => row.Evidence.ArtifactDigest is not null) < 2)
            rows.Add(candidate);
    }

    private static int CompareRows(PackageDecisionCorrelationRow left, PackageDecisionCorrelationRow right)
    {
        var rank = RungRank(left.Classification).CompareTo(RungRank(right.Classification));
        if (rank != 0) return rank;
        var path = string.Compare(left.Evidence.FilePath, right.Evidence.FilePath, StringComparison.Ordinal);
        if (path != 0) return path;
        var line = left.Evidence.StartLine.CompareTo(right.Evidence.StartLine);
        return line != 0 ? line : string.Compare(left.RowId, right.RowId, StringComparison.Ordinal);
    }

    private static int RungRank(string classification) => classification switch
    {
        "ExactArtifactMatch" => 0,
        "ArtifactDigestMismatch" => 1,
        _ => 2
    };

    private static IReadOnlyList<string> DistinctDigests(IReadOnlyList<PackageDecisionCorrelationRow> rows) =>
        rows.Select(row => row.Evidence.ArtifactDigest).OfType<string>().Distinct(StringComparer.Ordinal).OrderBy(digest => digest, StringComparer.Ordinal).ToArray();

    private static string Fingerprint(PackageDecisionCorrelationRow row) => string.Join('\u001f',
        row.Evidence.FilePath,
        row.Evidence.StartLine.ToString(CultureInfo.InvariantCulture),
        row.Evidence.EndLine.ToString(CultureInfo.InvariantCulture),
        row.Evidence.ResolvedVersion ?? string.Empty,
        row.Evidence.Version ?? string.Empty,
        row.Evidence.LockfileHash ?? string.Empty,
        row.RegistryOriginJoin ?? string.Empty);

    private static PackageDecisionChangeSide? ChangeSide(string side, PackageDecisionCorrelationRow row) =>
        new(side, row.SourceLabel, row.SourceIndexId, row.ScanId, row.RepoIdentityHash, row.CommitSha, row.Evidence);

    private static string? SafeProperty(IReadOnlyDictionary<string, string> properties, string key) => properties.TryGetValue(key, out var value) && value.Length <= 160 && !Path.IsPathRooted(value) && !value.StartsWith("C:\\", StringComparison.OrdinalIgnoreCase) && !value.Contains("://", StringComparison.Ordinal) ? value : null;
    private static string? SafeLockfileHash(string? value) => value is not null && value.Length == 32 && value.All(character => character is >= 'a' and <= 'f' or >= '0' and <= '9') ? value : null;
    private static string? SafeVersionHash(string? value)
    {
        if (value is null)
            return null;
        var hash = value.StartsWith("version-hash:", StringComparison.Ordinal) ? value[13..] : value;
        return hash.Length is 32 or 64 && hash.All(character => character is >= 'a' and <= 'f' or >= '0' and <= '9')
            ? $"version-hash:{hash}"
            : null;
    }
    private static string? SafeDigest(string? value, string? algorithm) => value is not null && ((algorithm == "sha256" && value.Length == 64 && value.All(character => character is >= 'a' and <= 'f' or >= '0' and <= '9')) || (algorithm == "sha512-base64" && value.Length <= 128 && value.All(character => char.IsLetterOrDigit(character) || character is '+' or '/' or '='))) ? value : null;
    private static bool IsLiteralVersion(string version) => !version.Contains('^') && !version.Contains('~') && !version.Contains('>') && !version.Contains('<') && !version.Contains('*') && !version.Contains("git", StringComparison.OrdinalIgnoreCase) && !version.Contains("${", StringComparison.Ordinal);
    private static string NormalizeName(string ecosystem, string value) => ecosystem.ToLowerInvariant() switch { "nuget" or "npm" => value.Trim().ToLowerInvariant(), "python" => value.Trim().ToLowerInvariant().Replace('-', '_').Replace('.', '_'), _ => value.Trim() };
    private static string? SafeCommit(string value) => FullCommit(value) ? value.ToLowerInvariant() : null;
    private static string Hash(string value) => CombinedReportHelpers.Hash(value, 24);
    private static string GapId(string kind, PackageDecisionRecord record, CombinedReportSource source) => $"pd-gap:{Hash(string.Join('\u001f', kind, record.DecisionId, source.SourceIndexId))}";
    private static int CountRows(params IReadOnlyCollection<PackageDecisionCorrelationRow>[] rows) => rows.Sum(row => row.Count);
    private static IReadOnlyList<PackageDecisionCorrelationRow> Filter(List<PackageDecisionCorrelationRow> rows, string? classification) => rows.Where(row => string.IsNullOrWhiteSpace(classification) || string.Equals(row.Classification, classification, StringComparison.OrdinalIgnoreCase)).OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.Evidence.FilePath, StringComparer.Ordinal).ThenBy(row => row.Evidence.StartLine).ThenBy(row => row.RowId, StringComparer.Ordinal).ToArray();
    private static void AddGap(List<PackageDecisionGap> gaps, int max, ref bool capReached, PackageDecisionGap gap) { if (gaps.Count < Math.Max(1, max)) gaps.Add(gap); else capReached = true; }

    private static bool SourceHasCredibilityGap(CombinedReportSource source, IEnumerable<CombinedKnownGapRow> knownGaps) => !FullCommit(source.CommitSha) || !string.Equals(source.BuildStatus, "Succeeded", StringComparison.OrdinalIgnoreCase) || !string.Equals(source.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.OrdinalIgnoreCase) || knownGaps.Any();
    private static bool FullCommit(string value) => !string.IsNullOrWhiteSpace(value) && value.Length is 40 or 64 && value.All(Uri.IsHexDigit);
    private static PackageDecisionSource ToSource(CombinedReportSource source, DateTimeOffset? scannedAt, IEnumerable<CombinedKnownGapRow> gaps)
    {
        var separator = source.Label.IndexOf('/', StringComparison.Ordinal);
        var container = separator > 0 ? source.Label[..separator] : source.Label == "default" ? null : source.Label;
        var original = separator > 0 ? source.Label[(separator + 1)..] : source.Label == "default" ? "default" : null;
        return new(source.Label, source.SourceIndexId, source.ScanId, $"repo-hash:{CombinedReportHelpers.Hash(source.RepoName, 16)}", SafeCommit(source.CommitSha), source.ScannerVersion, source.Language, source.AnalysisLevel, source.BuildStatus, SourceHasCredibilityGap(source, gaps) ? "ReducedCoverage" : "FullEvidenceAvailable", scannedAt?.ToString("O", CultureInfo.InvariantCulture), container, original);
    }

    private static PackageDecisionGap SourceGap(string classification, string message, PackageDecisionRecord record, CombinedReportSource source) =>
        new(GapId(classification, record, source), classification, message, RuleId, EvidenceTiers.Tier4Unknown, record.DecisionId, source.Label, record.Ecosystem, source.SourceIndexId, source.ScanId, SafeCommit(source.CommitSha));

    private static bool HasEcosystemCapability(IEnumerable<CombinedFactRow> facts, string ecosystem) => facts.Any(fact => string.Equals(fact.Properties.GetValueOrDefault("ecosystem"), ecosystem, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The correlation's package-evidence rows: native PackageReferenced facts plus Swift lockfile
    /// facts projected through the smallest deterministic seam. Swift lockfile rows never carry an
    /// artifact digest, so they can only ever correlate at the possible rung.
    /// </summary>
    private static IReadOnlyList<CombinedFactRow> PackageEvidenceRows(IEnumerable<CombinedFactRow> facts, string sourceIndexId) =>
        facts.Where(fact => fact.SourceIndexId == sourceIndexId)
            .Select(fact => fact.FactType == FactTypes.PackageReferenced ? fact : ProjectSwiftLockfileEvidence(fact))
            .OfType<CombinedFactRow>()
            .ToArray();

    private static CombinedFactRow? ProjectSwiftLockfileEvidence(CombinedFactRow fact)
    {
        if (fact.FactType != SwiftDependencyLockfileFactType)
        {
            return null;
        }

        var properties = new Dictionary<string, string>(fact.Properties, StringComparer.Ordinal)
        {
            ["ecosystem"] = "swift"
        };
        if (!properties.ContainsKey("packageName")
            && properties.TryGetValue("normalizedDependencyIdentity", out var identity)
            && !string.IsNullOrWhiteSpace(identity))
        {
            properties["packageName"] = identity;
        }

        return fact with { Properties = properties };
    }

    private static bool SourceMatches(string label, string selector)
    {
        if (string.Equals(label, selector, StringComparison.Ordinal)) return true;
        var separator = label.IndexOf('/', StringComparison.Ordinal);
        return separator > 0 && (string.Equals(label[..separator], selector, StringComparison.Ordinal) || string.Equals(label[(separator + 1)..], selector, StringComparison.Ordinal));
    }

    private static PackageDecisionContext BuildContext(IndexRead index, IReadOnlyList<PackageDecisionCorrelationRow> rows, PackageDecisionOptions options, bool reverse)
    {
        var combined = new CombinedReadResult(index.Sources, index.KnownGaps, [], index.Facts, index.Edges, new Dictionary<string, long>(StringComparer.Ordinal));
        CombinedPathGraphInventory graph;
        try
        {
            graph = CombinedDependencyPathReporter.BuildGraphInventory(combined);
        }
        catch
        {
            var evidence = rows.FirstOrDefault();
            return new PackageDecisionContext(
                "unavailable",
                "UnknownAnalysisGap",
                [],
                [new PackageDecisionGap(
                    $"pd-context-unavailable:{Hash(reverse ? "reverse" : "paths")}",
                    "UnknownAnalysisGap",
                    "The existing combined dependency graph inventory was unavailable for optional context.",
                    RuleId,
                    EvidenceTiers.Tier4Unknown,
                    SourceLabel: evidence?.SourceLabel,
                    SourceIndexId: evidence?.SourceIndexId,
                    ScanId: evidence?.ScanId,
                    CommitSha: evidence?.CommitSha,
                    ExtractorId: evidence?.Evidence.ExtractorId,
                    ExtractorVersion: evidence?.Evidence.ExtractorVersion,
                    FilePath: evidence?.Evidence.FilePath,
                    StartLine: evidence?.Evidence.StartLine,
                    EndLine: evidence?.Evidence.EndLine,
                    SupportingFactIds: evidence is null ? [] : [evidence.Evidence.FactId])],
                0,
                ["Optional context is static graph evidence only and never changes a package correlation rung."]);
        }

        var selected = rows
            .Select(row => (row, node: graph.Nodes.FirstOrDefault(node => string.Equals(node.CombinedFactId, row.Evidence.FactId, StringComparison.Ordinal) || string.Equals(node.CombinedFactId, row.Evidence.OriginalFactId, StringComparison.Ordinal))))
            .Where(pair => pair.node is not null)
            .OrderBy(pair => pair.row.SourceLabel, StringComparer.Ordinal)
            .ThenBy(pair => pair.row.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(pair => pair.row.Evidence.StartLine)
            .ThenBy(pair => pair.row.RowId, StringComparer.Ordinal)
            .ToArray();
        var nodesById = graph.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var incoming = graph.Edges
            .GroupBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(edge => edge.EdgeKind, StringComparer.Ordinal).ThenBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var contextRows = new List<PackageDecisionContextRow>();
        var gaps = new List<PackageDecisionGap>();
        var sourcesById = graph.Sources
            .GroupBy(source => source.SourceIndexId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var roots = new HashSet<string>(StringComparer.Ordinal);
        var pathsByRoot = new Dictionary<string, int>(StringComparer.Ordinal);
        var omitted = 0;
        foreach (var pair in selected)
        {
            var node = pair.node!;
            var queue = new Queue<ContextTraversalState>();
            queue.Enqueue(new ContextTraversalState(node.NodeId, [node.NodeId], []));
            var visitedStates = 0;
            var reachedRoot = false;
            while (queue.Count > 0)
            {
                if (queue.Count > Math.Max(1, options.MaxFrontier) || ++visitedStates > Math.Max(1, options.MaxFrontier))
                {
                    omitted++;
                    gaps.Add(ContextGap("frontier", pair.row, "TruncatedByLimit", "Optional graph context reached the deterministic frontier limit."));
                    break;
                }

                var state = queue.Dequeue();
                if (!nodesById.TryGetValue(state.NodeId, out var current)) continue;
                var allIncoming = incoming.TryGetValue(current.NodeId, out var incomingEdges) ? incomingEdges : [];
                var candidates = allIncoming.Where(edge => !state.NodeIds.Contains(edge.FromNodeId, StringComparer.Ordinal)).ToArray();
                if (allIncoming.Length == 0 && state.EdgeIds.Count > 0)
                {
                    reachedRoot = true;
                    var rootId = current.NodeId;
                    if (reverse)
                    {
                        if (!roots.Contains(rootId) && roots.Count >= Math.Max(1, options.MaxRoots))
                        {
                            omitted++;
                            continue;
                        }
                        roots.Add(rootId);
                        pathsByRoot.TryGetValue(rootId, out var pathCount);
                        if (pathCount >= Math.Max(1, options.MaxPathsPerRoot))
                        {
                            omitted++;
                            continue;
                        }
                        pathsByRoot[rootId] = pathCount + 1;
                    }
                    else if (contextRows.Count >= Math.Max(1, options.MaxPaths))
                    {
                        omitted++;
                        continue;
                    }

                    var pathNodeIds = state.NodeIds.Reverse().ToArray();
                    var pathEdgeIds = state.EdgeIds.Reverse().ToArray();
                    var pathNodes = pathNodeIds.Select(id => nodesById[id]).ToArray();
                    var pathEdges = pathEdgeIds.Select(id => graph.Edges.Single(edge => edge.EdgeId == id)).ToArray();
                    var supportingFactIds = pathNodes.Select(pathNode => pathNode.CombinedFactId)
                        .OfType<string>()
                        .Concat(pathEdges.SelectMany(edge => edge.SupportingFactIds))
                        .Append(pair.row.Evidence.FactId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray();
                    var ruleIds = pathEdges.Select(edge => edge.RuleId)
                        .Concat(reverse ? [CombinedReverseReporter.PathRuleId] : [])
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(id => id, StringComparer.Ordinal)
                        .ToArray();
                    var pathClassification = reverse
                        ? CombinedReverseReporter.ClassifyPath(pathEdges, pathNodes, sourcesById)
                        : CombinedDependencyPathReporter.Classify(pathEdges);
                    contextRows.Add(new PackageDecisionContextRow(
                        $"pd-context:{Hash(string.Join('\u001f', reverse ? "reverse" : "paths", pair.row.RowId, string.Join('|', pathNodeIds), string.Join('|', pathEdgeIds)))}",
                        pathClassification,
                        reverse ? CombinedReverseReporter.PathRuleId : "combined.paths.surface-evidence.v1",
                        WeakestTier(pathNodes.Select(pathNode => pathNode.EvidenceTier).Concat(pathEdges.Select(edge => edge.EvidenceTier)).Append(pair.row.Evidence.EvidenceTier)),
                        pair.row.SourceLabel,
                        pair.row.SourceIndexId,
                        pair.row.Evidence.FactId,
                        pair.row.PackageName,
                        reverse ? "Bounded reverse traversal found a static path from a graph root to the package-config surface." : "Bounded forward path evidence connects a graph root to the package-config surface.",
                        supportingFactIds,
                        pathEdgeIds,
                        CombinedReportHelpers.SortedMetadata([
                            new("direction", reverse ? "reverse-from-package-surface" : "forward-to-package-surface"),
                            new("rootNodeId", rootId),
                            new("packageNodeId", node.NodeId),
                            new("pathLength", pathEdgeIds.Length.ToString(CultureInfo.InvariantCulture)),
                            new("maxDepth", options.MaxDepth.ToString(CultureInfo.InvariantCulture)),
                            new("sourceLabel", node.SourceLabel)
                        ]),
                        pair.row.Evidence.FilePath,
                        pair.row.Evidence.StartLine,
                        pair.row.Evidence.EndLine,
                        pair.row.CommitSha,
                        pair.row.Evidence.ExtractorId,
                        pair.row.Evidence.ExtractorVersion,
                        ruleIds));
                    continue;
                }

                if (state.EdgeIds.Count >= Math.Max(1, options.MaxDepth))
                {
                    omitted++;
                    gaps.Add(ContextGap("depth", pair.row, "TruncatedByLimit", "Optional graph context reached the deterministic depth limit."));
                    continue;
                }

                foreach (var edge in candidates)
                    queue.Enqueue(new ContextTraversalState(edge.FromNodeId, [.. state.NodeIds, edge.FromNodeId], [.. state.EdgeIds, edge.EdgeId]));
            }

            if (!reachedRoot)
                gaps.Add(ContextGap("no-path", pair.row, CombinedDependencyPathClassifications.UnknownAnalysisGap, $"No bounded static {(reverse ? "reverse" : "forward")} path reached a graph root for the package-config surface."));
        }

        foreach (var missing in rows.Where(row => selected.All(pair => !ReferenceEquals(pair.row, row))))
            gaps.Add(ContextGap("surface-unavailable", missing, CombinedDependencyPathClassifications.UnknownAnalysisGap, "The correlated package fact did not map to a package-config graph surface."));

        foreach (var gap in graph.Gaps.Where(gap => gap.Classification is "TruncatedByLimit" or CombinedDependencyPathClassifications.UnknownAnalysisGap))
        {
            gaps.Add(new PackageDecisionGap($"pd-context-gap:{Hash(gap.GapId)}", gap.Classification, gap.Message, gap.RuleId ?? RuleId, gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown, SourceLabel: gap.SourceLabel, SourceIndexId: gap.SourceIndexId, CommitSha: gap.CommitSha, ExtractorVersion: gap.ExtractorVersion, FilePath: gap.FilePath, StartLine: gap.StartLine, EndLine: gap.EndLine, SupportingFactIds: gap.EffectiveSupportingFactIds));
        }
        if (omitted > 0)
        {
            gaps.Add(new PackageDecisionGap($"pd-context-truncated:{Hash(reverse ? "reverse" : "paths")}", "TruncatedByLimit", $"Optional {(reverse ? "reverse" : "path")} context exceeded its deterministic cap; {omitted} context rows were omitted.", RuleId, EvidenceTiers.Tier4Unknown));
        }
        if (contextRows.Count == 0)
        {
            gaps.Add(new PackageDecisionGap($"pd-context-selector:{Hash(reverse ? "reverse" : "paths")}", "UnknownAnalysisGap", $"No package-config surface was available for optional {(reverse ? "reverse" : "path")} context.", RuleId, EvidenceTiers.Tier4Unknown));
        }
        var status = omitted > 0 ? "truncated" : contextRows.Count > 0 ? "available" : "unavailable";
        var classification = gaps.Any(gap => gap.Classification == "UnknownAnalysisGap") ? "UnknownAnalysisGap" : omitted > 0 ? "TruncatedByLimit" : contextRows.Count > 0 ? "Available" : "UnknownAnalysisGap";
        return new PackageDecisionContext(status, classification, contextRows.OrderBy(row => row.ContextId, StringComparer.Ordinal).ToArray(), gaps.GroupBy(gap => gap.GapId, StringComparer.Ordinal).Select(group => group.First()).OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(), omitted, ["Optional context is static graph evidence only; it does not prove runtime reachability and never upgrades a correlation rung."]);
    }

    private static PackageDecisionGap ContextGap(string reason, PackageDecisionCorrelationRow row, string classification, string message) =>
        new(
            $"pd-context-{reason}:{Hash(row.RowId)}",
            classification,
            message,
            RuleId,
            EvidenceTiers.Tier4Unknown,
            row.DecisionId,
            row.SourceLabel,
            row.Ecosystem,
            row.SourceIndexId,
            row.ScanId,
            row.CommitSha,
            row.Evidence.ExtractorId,
            row.Evidence.ExtractorVersion,
            row.Evidence.FilePath,
            row.Evidence.StartLine,
            row.Evidence.EndLine,
            [row.Evidence.FactId]);

    private static string WeakestTier(IEnumerable<string?> tiers)
    {
        var values = tiers.OfType<string>().ToArray();
        if (values.Contains(EvidenceTiers.Tier4Unknown, StringComparer.Ordinal)) return EvidenceTiers.Tier4Unknown;
        if (values.Contains(EvidenceTiers.Tier3SyntaxOrTextual, StringComparer.Ordinal)) return EvidenceTiers.Tier3SyntaxOrTextual;
        if (values.Contains(EvidenceTiers.Tier2Structural, StringComparer.Ordinal)) return EvidenceTiers.Tier2Structural;
        return EvidenceTiers.Tier1Semantic;
    }

    private sealed record ContextTraversalState(string NodeId, IReadOnlyList<string> NodeIds, IReadOnlyList<string> EdgeIds);

    private static bool TryParseAsOf(string value, out DateTimeOffset parsed)
    {
        var formats = new[]
        {
            "yyyy-MM-dd'T'HH:mm:ss'Z'",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
            "yyyy-MM-dd'T'HH:mm:sszzz",
            "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
        };
        return DateTimeOffset.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            && parsed.Offset == TimeSpan.Zero;
    }

    private static IReadOnlyList<string> InputPaths(PackageDecisionOptions options)
    {
        if (options.IndexPaths is { Count: > 0 })
        {
            return options.IndexPaths.Where(path => !string.IsNullOrWhiteSpace(path)).ToArray();
        }

        return string.IsNullOrWhiteSpace(options.IndexPath) ? [] : [options.IndexPath];
    }

    private static void RejectOutputAlias(string decisionPath, IReadOnlyList<string> inputPaths, string outputPath)
    {
        var fullOutput = Path.GetFullPath(outputPath);
        var outputCandidates = Directory.Exists(fullOutput) || string.IsNullOrWhiteSpace(Path.GetExtension(fullOutput))
            ? new[] { Path.Combine(fullOutput, "package-decision-report.md"), Path.Combine(fullOutput, "package-decision-report.json") }
            : [fullOutput];
        var inputIdentities = new[] { decisionPath }.Concat(inputPaths).Select(InputFileIdentity).ToArray();
        if (outputCandidates.Select(OutputFileIdentity).Any(outputIdentity => inputIdentities.Any(inputIdentity => FileIdentityEquals(outputIdentity, inputIdentity))))
            throw new InvalidDataException("package-decision --out must not alias a decision, manifest, or index input.");
    }

    private static FileIdentityRecord InputFileIdentity(string path)
    {
        var identity = ReadFileIdentity(path);
        if (identity.FileSystemId is null)
            throw new InvalidDataException("package-decision could not establish the filesystem identity of an input.");
        return identity;
    }

    private static FileIdentityRecord OutputFileIdentity(string path) => ReadFileIdentity(path);

    private static FileIdentityRecord ReadFileIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            for (var pass = 0; pass < 32; pass++)
            {
                var resolved = ResolvePathLinks(fullPath);
                if (string.Equals(resolved, fullPath, StringComparison.Ordinal))
                    return new FileIdentityRecord(resolved, ExistingFileSystemId(resolved));
                fullPath = resolved;
            }

            throw new InvalidDataException("package-decision could not establish a stable file identity for alias protection.");
        }
        catch (IOException exception)
        {
            throw new InvalidDataException("package-decision could not establish a stable file identity for alias protection.", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new InvalidDataException("package-decision could not establish a stable file identity for alias protection.", exception);
        }
    }

    private static FileSystemId? ExistingFileSystemId(string path)
    {
        if (!File.Exists(path)) return null;
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        if (OperatingSystem.IsWindows())
        {
            if (!GetFileInformationByHandle(handle, out var information))
                throw new IOException("Could not read the Windows file identity.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            return new FileSystemId(
                information.VolumeSerialNumber,
                ((ulong)information.FileIndexHigh << 32) | information.FileIndexLow);
        }

        var buffer = Marshal.AllocHGlobal(256);
        try
        {
            if (FStat(handle.DangerousGetHandle().ToInt32(), buffer) != 0)
                throw new IOException("Could not read the Unix file identity.", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
            return OperatingSystem.IsMacOS()
                ? new FileSystemId(unchecked((uint)Marshal.ReadInt32(buffer, 0)), unchecked((ulong)Marshal.ReadInt64(buffer, 8)))
                : new FileSystemId(unchecked((ulong)Marshal.ReadInt64(buffer, 0)), unchecked((ulong)Marshal.ReadInt64(buffer, 8)));
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ResolvePathLinks(string fullPath)
    {
        var root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrWhiteSpace(root)) return fullPath;

        var current = root;
        foreach (var segment in Path.GetRelativePath(root, fullPath).Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == ".") continue;
            var candidate = Path.Combine(current, segment);
            FileSystemInfo entry = Directory.Exists(candidate)
                ? new DirectoryInfo(candidate)
                : new FileInfo(candidate);
            var target = entry.Exists ? entry.ResolveLinkTarget(true) : null;
            current = target?.FullName ?? candidate;
        }

        return Path.GetFullPath(current);
    }

    private static bool FileIdentityEquals(FileIdentityRecord left, FileIdentityRecord right) =>
        (left.FileSystemId is not null && left.FileSystemId == right.FileSystemId)
        || string.Equals(
            left.CanonicalPath,
            right.CanonicalPath,
            OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal);

    private sealed record FileIdentityRecord(string CanonicalPath, FileSystemId? FileSystemId);
    private sealed record FileSystemId(ulong Volume, ulong File);

    [StructLayout(LayoutKind.Sequential)]
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastAccessTime;
        public System.Runtime.InteropServices.ComTypes.FILETIME LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);

    [DllImport("libc", EntryPoint = "fstat", SetLastError = true)]
    private static extern int FStat(int fileDescriptor, IntPtr buffer);

    private static IReadOnlyList<PackageInputSpec> ResolveInputSpecs(PackageDecisionOptions options)
    {
        var specs = new List<PackageInputSpec>();
        if (!string.IsNullOrWhiteSpace(options.BeforeManifestPath) && !string.IsNullOrWhiteSpace(options.AfterManifestPath))
        {
            foreach (var spec in ReadPortfolioManifest(options.BeforeManifestPath!))
                specs.Add(spec with { Label = $"before/{spec.Label}", Side = "before", BaseLabel = spec.Label });
            foreach (var spec in ReadPortfolioManifest(options.AfterManifestPath!))
                specs.Add(spec with { Label = $"after/{spec.Label}", Side = "after", BaseLabel = spec.Label });
        }
        else if (!string.IsNullOrWhiteSpace(options.ManifestPath))
        {
            specs.AddRange(ReadPortfolioManifest(options.ManifestPath!));
        }
        else
        {
            var paths = InputPaths(options);
            var labels = options.Labels ?? [];
            for (var i = 0; i < paths.Count; i++)
            {
                specs.Add(new PackageInputSpec(labels.Count == paths.Count ? labels[i] : i == 0 && paths.Count == 1 ? "default" : $"source-{i + 1}", paths[i], null, null));
            }
        }

        if (specs.Count == 0) throw new InvalidDataException("package-decision did not receive any readable index input.");
        if (specs.GroupBy(spec => spec.Label, StringComparer.Ordinal).Any(group => group.Count() > 1))
            throw new InvalidDataException("package-decision inputs contain duplicate labels.");
        return specs;
    }

    private static async Task<IndexRead> ReadInputsAsync(IReadOnlyList<PackageInputSpec> specs, CancellationToken cancellationToken)
    {
        var multiple = specs.Count > 1;
        var sources = new List<CombinedReportSource>();
        var facts = new List<CombinedFactRow>();
        var edges = new List<CombinedDependencyEdgeRow>();
        var knownGaps = new List<CombinedKnownGapRow>();
        var scannedAt = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        var rawLabels = new Dictionary<string, string>(StringComparer.Ordinal);
        var sourceSides = new Dictionary<string, PackageDecisionSourceSide>(StringComparer.Ordinal);
        var identities = new Dictionary<string, CombinedReportSource>(StringComparer.Ordinal);
        foreach (var spec in specs.OrderBy(spec => spec.Label, StringComparer.Ordinal))
        {
            var read = await ReadIndexAsync(spec.IndexPath, cancellationToken);
            foreach (var source in read.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal))
            {
                var sourceId = multiple ? $"{CombinedReportHelpers.Hash(spec.Label, 12)}:{source.SourceIndexId}" : source.SourceIndexId;
                if (spec.Side is not null && spec.BaseLabel is not null)
                    sourceSides[sourceId] = new PackageDecisionSourceSide(spec.Side, ComparisonPairLabel(spec.BaseLabel, source.Label));
                // Comparison sides compose "before/<label>"/"after/<label>"; each part is safety-checked
                // separately because the composed spec label is not itself a single safe identifier.
                var safeSpecLabel = spec.Side is null ? SafeInputLabel(spec.Label) : $"{spec.Side}/{SafeInputLabel(spec.BaseLabel ?? spec.Label)}";
                var safeSourceLabel = SafeInputLabel(source.Label);
                var rawLabel = source.Label == "default" ? spec.Label : spec.Label == "default" ? source.Label : $"{spec.Label}/{source.Label}";
                var displayLabel = source.Label == "default" ? safeSpecLabel : spec.Label == "default" ? safeSourceLabel : $"{safeSpecLabel}/{safeSourceLabel}";
                var composed = source with { SourceIndexId = sourceId, Label = displayLabel };
                var identity = FullCommit(source.CommitSha)
                    ? $"repo:{source.RepoName}:commit:{source.CommitSha.ToLowerInvariant()}"
                    : $"scan:{source.ScanId}";
                if (identities.ContainsKey(identity))
                {
                    knownGaps.Add(new CombinedKnownGapRow(sourceId, displayLabel, "DuplicateSourceIdentity", 1, "Portfolio inputs contain duplicate scan identity; this pairing is not correlated."));
                }
                else
                {
                    identities[identity] = composed;
                }
                if (!string.IsNullOrWhiteSpace(spec.ExpectedCommitSha) && !string.Equals(spec.ExpectedCommitSha, source.CommitSha, StringComparison.OrdinalIgnoreCase))
                    knownGaps.Add(new CombinedKnownGapRow(sourceId, displayLabel, "ExpectedCommitMismatch", 1, "Portfolio manifest commit hint did not match the source snapshot."));
                if (!string.IsNullOrWhiteSpace(spec.ExpectedRepoIdentity) && !string.Equals(spec.ExpectedRepoIdentity, $"repo-hash:{CombinedReportHelpers.Hash(source.RepoName, 16)}", StringComparison.Ordinal))
                    knownGaps.Add(new CombinedKnownGapRow(sourceId, displayLabel, "ExpectedRepoIdentityMismatch", 1, "Portfolio manifest repository hint did not match the source snapshot."));
                sources.Add(composed);
                rawLabels[sourceId] = rawLabel;
                var sourceFacts = read.Facts.Where(fact => fact.SourceIndexId == source.SourceIndexId)
                    .Select(fact => fact with
                    {
                        SourceIndexId = sourceId,
                        SourceLabel = displayLabel,
                        CombinedFactId = multiple ? NamespacedEvidenceId(sourceId, fact.CombinedFactId) : fact.CombinedFactId
                    })
                    .ToArray();
                facts.AddRange(sourceFacts);
                edges.AddRange(read.Edges.Where(edge => edge.SourceIndexId == source.SourceIndexId).Select(edge => edge with
                {
                    SourceIndexId = sourceId,
                    SourceLabel = displayLabel,
                    EdgeId = multiple ? NamespacedEvidenceId(sourceId, edge.EdgeId) : edge.EdgeId
                }));
                knownGaps.AddRange(read.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId).Select(gap => gap with { SourceIndexId = sourceId, SourceLabel = displayLabel }));
                if (read.ScannedAt.TryGetValue(source.SourceIndexId, out var scanned)) scannedAt[sourceId] = scanned;
            }
        }
        return new IndexRead(sources, facts, edges, knownGaps, scannedAt, rawLabels, sourceSides);
    }

    private static string NamespacedEvidenceId(string sourceId, string evidenceId) =>
        $"composed:{CombinedReportHelpers.Hash(sourceId, 16)}:{evidenceId}";

    private static string SafeInputLabel(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length is > 0 and <= 128
            && trimmed.All(character => char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-')
                ? trimmed
                : $"source-label-hash:{CombinedReportHelpers.Hash(trimmed, 16)}";
    }

    private static string ComparisonPairLabel(string manifestLabel, string expandedSourceLabel) =>
        expandedSourceLabel == "default"
            ? SafeInputLabel(manifestLabel)
            : $"pair-{CombinedReportHelpers.Hash(string.Join('\u001f', manifestLabel, expandedSourceLabel), 16)}";

    private static string InputIdentity(IReadOnlyList<PackageInputSpec> specs, string? manifestPath, string? beforeManifestPath, string? afterManifestPath)
    {
        var components = new List<string>();
        if (!string.IsNullOrWhiteSpace(manifestPath))
            components.Add($"manifest:{FileContentHash(manifestPath)}");
        if (!string.IsNullOrWhiteSpace(beforeManifestPath))
            components.Add($"before-manifest:{FileContentHash(beforeManifestPath)}");
        if (!string.IsNullOrWhiteSpace(afterManifestPath))
            components.Add($"after-manifest:{FileContentHash(afterManifestPath)}");
        components.AddRange(specs
            .OrderBy(spec => spec.Label, StringComparer.Ordinal)
            .Select(spec => string.Join('\u001f', spec.Label, FileContentHash(spec.IndexPath), spec.ExpectedRepoIdentity ?? string.Empty, spec.ExpectedCommitSha ?? string.Empty)));
        return CombinedReportHelpers.Hash(string.Join('\u001e', components), 16);
    }

    private static bool FixedTimeDigestEquals(string left, string right)
    {
        var leftBytes = Encoding.UTF8.GetBytes(left);
        var rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static string FileContentHash(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static IReadOnlyList<PackageInputSpec> ReadPortfolioManifest(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            var root = document.RootElement;
            if (!root.TryGetProperty("version", out var version) || version.ValueKind != JsonValueKind.String || version.GetString() != "1.0")
                throw new InvalidDataException("package-decision portfolio manifest version is unsupported.");
            if (!root.TryGetProperty("inputs", out var inputElement) || inputElement.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("package-decision portfolio manifest requires an inputs array.");
            var baseDirectory = Path.GetDirectoryName(Path.GetFullPath(path)) ?? Directory.GetCurrentDirectory();
            var specs = new List<PackageInputSpec>();
            foreach (var entry in inputElement.EnumerateArray())
            {
                var label = RequiredManifestString(entry, "label");
                var index = RequiredManifestString(entry, "indexPath");
                var resolved = Path.IsPathFullyQualified(index) ? index : Path.GetFullPath(Path.Combine(baseDirectory, index));
                specs.Add(new PackageInputSpec(label, resolved, OptionalManifestString(entry, "expectedRepoIdentity"), OptionalManifestString(entry, "expectedCommitSha")));
            }
            if (specs.GroupBy(spec => spec.Label, StringComparer.Ordinal).Any(group => group.Count() > 1))
                throw new InvalidDataException("package-decision portfolio manifest contains duplicate labels.");
            return specs;
        }
        catch (InvalidDataException) { throw; }
        catch (Exception ex) { throw new InvalidDataException("package-decision portfolio manifest could not be parsed.", ex); }
    }

    private static string RequiredManifestString(JsonElement element, string property)
    {
        var value = OptionalManifestString(element, property);
        return string.IsNullOrWhiteSpace(value) ? throw new InvalidDataException($"package-decision portfolio manifest input requires {property}.") : value;
    }

    private static string? OptionalManifestString(JsonElement element, string property) => element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private sealed record PackageInputSpec(string Label, string IndexPath, string? ExpectedRepoIdentity, string? ExpectedCommitSha, string? Side = null, string? BaseLabel = null);

    private static async Task<IndexRead> ReadIndexAsync(string path, CancellationToken cancellationToken)
    {
        var connectionString = new SqliteConnectionStringBuilder { DataSource = path, Mode = SqliteOpenMode.ReadOnly }.ToString();
        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        var hasSources = await TableExists(connection, "index_sources", cancellationToken);
        var hasCombinedFacts = await TableExists(connection, "combined_facts", cancellationToken);
        if (hasSources && hasCombinedFacts)
        {
            var read = await CombinedDependencyReporter.ReadAsync(connection, cancellationToken);
            var scanned = await ReadCombinedScannedAtAsync(connection, cancellationToken);
            return new IndexRead(read.Sources, read.Facts, read.Edges, read.KnownGaps, scanned, read.Sources.ToDictionary(source => source.SourceIndexId, source => source.Label, StringComparer.Ordinal), new Dictionary<string, PackageDecisionSourceSide>(StringComparer.Ordinal));
        }
        if (!await TableExists(connection, "scan_manifest", cancellationToken) || !await TableExists(connection, "facts", cancellationToken)) throw new InvalidDataException("package-decision requires a TraceMap single index or combined index.");
        await using var manifestCommand = connection.CreateCommand();
        manifestCommand.CommandText = "select manifest_json from scan_manifest order by scanned_at desc limit 1;";
        var manifestJson = Convert.ToString(await manifestCommand.ExecuteScalarAsync(cancellationToken)) ?? throw new InvalidDataException("TraceMap index does not contain a scan manifest.");
        var manifest = JsonSerializer.Deserialize<ScanManifest>(manifestJson, JsonOptions) ?? throw new InvalidDataException("TraceMap scan manifest could not be parsed.");
        var source = new CombinedReportSource("default", "default", CombinedReportHelpers.Hash(manifest.RemoteUrl ?? manifest.RepoName, 16), manifest.ScanId, manifest.RepoName, manifest.RemoteUrl, manifest.Branch, manifest.CommitSha, manifest.ScannerVersion, LanguageFromScanner(manifest.ScannerVersion), LanguageFromScanner(manifest.ScannerVersion), false, manifest.ScanRootRelativePath, manifest.ScanRootPathHash, manifest.GitRootHash, manifest.AnalysisLevel, manifest.BuildStatus);
        await using var factCommand = connection.CreateCommand();
        var hasExtractorId = await ColumnExistsAsync(connection, "facts", "extractor_id", cancellationToken);
        var hasExtractorVersion = await ColumnExistsAsync(connection, "facts", "extractor_version", cancellationToken);
        var extractorIdExpression = hasExtractorId ? "extractor_id" : "null";
        var extractorVersionExpression = hasExtractorVersion ? "extractor_version" : "null";
        factCommand.CommandText = $"select fact_id, scan_id, repo, commit_sha, fact_type, rule_id, evidence_tier, source_symbol, target_symbol, contract_element, file_path, start_line, end_line, properties_json, {extractorVersionExpression}, {extractorIdExpression} from facts order by file_path, start_line, fact_type, fact_id;";
        var facts = new List<CombinedFactRow>();
        await using var reader = await factCommand.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken)) facts.Add(new CombinedFactRow(reader.GetString(0), "default", "default", reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3), reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12), ParseProperties(reader.GetString(13)), reader.IsDBNull(14) ? null : reader.GetString(14), reader.IsDBNull(15) ? null : reader.GetString(15)));
        var known = manifest.KnownGaps.Select((gap, index) => new CombinedKnownGapRow("default", "default", $"manifest-gap-{index + 1}", 1, gap)).ToArray();
        return new IndexRead([source], facts, [], known, new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal) { ["default"] = manifest.ScannedAt }, new Dictionary<string, string>(StringComparer.Ordinal) { ["default"] = "default" }, new Dictionary<string, PackageDecisionSourceSide>(StringComparer.Ordinal));
    }

    private static async Task<IReadOnlyDictionary<string, DateTimeOffset?>> ReadCombinedScannedAtAsync(SqliteConnection connection, CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal);
        await using var command = connection.CreateCommand();
        command.CommandText = "select source_index_id, manifest_json from index_sources order by source_index_id;";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var sourceId = reader.GetString(0);
            try
            {
                var manifest = JsonSerializer.Deserialize<ScanManifest>(reader.GetString(1), JsonOptions);
                result[sourceId] = manifest?.ScannedAt;
            }
            catch (JsonException)
            {
                result[sourceId] = null;
            }
        }
        return result;
    }
    private static string? LanguageFromScanner(string scanner) => scanner.Contains("typescript", StringComparison.OrdinalIgnoreCase) ? "typescript" : scanner.Contains("python", StringComparison.OrdinalIgnoreCase) ? "python" : scanner.Contains("jvm", StringComparison.OrdinalIgnoreCase) ? "jvm" : "csharp";
    private static async Task<bool> TableExists(SqliteConnection connection, string name, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "select count(*) from sqlite_master where type='table' and name=$name;"; command.Parameters.AddWithValue("$name", name); return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0; }
    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "select count(*) from pragma_table_info($table) where name = $column;"; command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0; }
    private static IReadOnlyDictionary<string, string> ParseProperties(string json) => JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)?.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
    private sealed record IndexRead(IReadOnlyList<CombinedReportSource> Sources, IReadOnlyList<CombinedFactRow> Facts, IReadOnlyList<CombinedDependencyEdgeRow> Edges, IReadOnlyList<CombinedKnownGapRow> KnownGaps, IReadOnlyDictionary<string, DateTimeOffset?> ScannedAt, IReadOnlyDictionary<string, string> RawLabels, IReadOnlyDictionary<string, PackageDecisionSourceSide> SourceSides);
    private sealed record PackageDecisionSourceSide(string Side, string BaseLabel);

    private static string RenderMarkdown(PackageDecisionDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Package Decision Correlation Report\n");
        builder.AppendLine($"- Mode: `{report.Mode}`\n- Version: `{report.Version}`\n- Coverage: `{report.Summary.Coverage}`\n- Sources: `{report.Summary.SourceCount}`\n- Records: `{report.Summary.RecordCount}`\n- Exact matches: `{report.Summary.ExactCount}`\n- Digest mismatches: `{report.Summary.DigestMismatchCount}`\n- Possible matches: `{report.Summary.PossibleCount}`\n- Ambiguous references: `{report.Summary.AmbiguousCount}`\n- Excluded sources: `{report.Summary.ExcludedCount}`\n- Stale references: `{report.Summary.StaleCount}`\n- Runtime-unproven references: `{report.Summary.RuntimeUnprovenCount}`\n- Artifact changes: `{report.Summary.ArtifactChangeCount}`\n- Gaps: `{report.Summary.GapCount}`\n");
        SectionRecords(builder, report);
        SectionRows(builder, "Exact Matches", report.ExactMatches);
        SectionRows(builder, "Possible Matches", report.PossibleMatches);
        SectionRows(builder, "Artifact Identity Mismatches", report.DigestMismatches);
        SectionRows(builder, "Ambiguous References", report.AmbiguousReferences);
        builder.AppendLine("## Excluded Sources\n");
        foreach (var row in report.ExcludedSources) builder.AppendLine($"- `{Cell(row.SourceLabel)}`: {Cell(row.Message)} ({Cell(row.RuleId)}, {Cell(row.EvidenceTier)})");
        builder.AppendLine("\n## Stale and Runtime-Unproven References\n");
        foreach (var row in report.StaleReferences) builder.AppendLine($"- `{Cell(row.SourceLabel)}` `{Cell(row.Classification)}`: {Cell(row.Message)}");
        if (report.StaleReferences.Count == 0) builder.AppendLine("No stale references were observed.");
        foreach (var row in report.RuntimeUnprovenReferences)
            builder.AppendLine($"- `{Cell(row.ReferenceId)}` `{Cell(row.ReferenceKind)}` `{Cell(row.Ecosystem)}` `{Cell(row.PackageName)}` `{Cell(row.ArtifactVersion)}` (producer `{Cell(row.ProducerId)}` version `{Cell(row.ProducerVersion)}`, join `{Cell(row.JoinBasis)}`): {Cell(row.Message)} ({Cell(row.RuleId)}, {Cell(row.EvidenceTier)})");
        if (report.RuntimeUnprovenReferences.Count == 0) builder.AppendLine("No runtime-unproven references were supplied.");
        builder.AppendLine();
        RenderAdvisoryClaims(builder, report);
        RenderContext(builder, "Optional Path Context", report.PathContext);
        RenderContext(builder, "Optional Reverse Context", report.ReverseContext);
        RenderArtifactChanges(builder, report);
        builder.AppendLine("## Gaps\n");
        foreach (var gap in report.Gaps) builder.AppendLine($"- `{Cell(gap.Classification)}`: {Cell(gap.Message)} ({Cell(gap.RuleId)}, {Cell(gap.EvidenceTier)})");
        if (report.Gaps.Count == 0) builder.AppendLine("No gaps were recorded.\n");
        builder.AppendLine("## Limitations\n");
        foreach (var limitation in report.Limitations) builder.AppendLine($"- {Cell(limitation)}");
        return builder.ToString();
    }

    private static void RenderAdvisoryClaims(StringBuilder builder, PackageDecisionDocument report)
    {
        builder.AppendLine("## Advisory Claims (external)\n");
        if (report.AdvisoryClaims is not { Count: > 0 } claims)
        {
            builder.AppendLine("No advisory profile was supplied.\n");
            return;
        }

        var first = claims[0];
        builder.AppendLine($"- Producer: `{Cell(first.ProducerId)}` version `{Cell(first.ProducerVersion)}`");
        builder.AppendLine($"- Profile digest: `{Cell(first.ProfileDigest)}` ({CanonicalJsonDigest.Algorithm})");
        foreach (var claim in claims)
            builder.AppendLine($"- `{Cell(claim.ClaimId)}` `{Cell(claim.ClaimKind)}` `{Cell(claim.Ecosystem)}` `{Cell(claim.PackageName)}` predicate `{Cell(claim.VersionPredicateKind)}`{(claim.VersionPredicateVersion is null ? string.Empty : $" `{Cell(claim.VersionPredicateVersion)}`")} framework `{Cell(claim.Framework)}` ({Cell(claim.RuleId)}, {Cell(claim.EvidenceTier)})");
        builder.AppendLine($"- {AdvisoryLimitation}\n");
    }

    private static void RenderArtifactChanges(StringBuilder builder, PackageDecisionDocument report)
    {
        builder.AppendLine("## Before/After Artifact Changes\n");
        if (report.ArtifactChanges is not { Count: > 0 } changes)
        {
            builder.AppendLine("Before/after artifact changes require --before-manifest and --after-manifest.\n");
            return;
        }

        builder.AppendLine("| Package | Version | Decision | Change | Classification | Source | Before evidence | After evidence |\n| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var change in changes)
            builder.AppendLine($"| {Cell(change.PackageName)} | {Cell(change.ArtifactVersion)} | {Cell(change.DecisionKind)} | `{Cell(change.ChangeKind)}` | `{Cell(change.Classification)}` | {Cell(change.SourceLabel)} | {Cell(ChangeSideCell(change.Before))} | {Cell(ChangeSideCell(change.After))} |");
        builder.AppendLine();
        foreach (var change in changes)
        {
            builder.AppendLine($"- `{Cell(change.ChangeId)}` `{Cell(change.Classification)}` `{Cell(change.ChangeKind)}`: {Cell(change.Message)} ({Cell(change.RuleId)}, {Cell(change.EvidenceTier)})");
            foreach (var note in change.Notes) builder.AppendLine($"  - Note `{Cell(note.Code)}`: {Cell(note.Message)}");
        }
        builder.AppendLine();
    }

    private static string ChangeSideCell(PackageDecisionChangeSide? side) => side is null
        ? "absent"
        : $"{side.Side} {side.Evidence.FilePath}:{side.Evidence.StartLine}-{side.Evidence.EndLine} commit {side.CommitSha ?? "unknown"}";

    private static void SectionRecords(StringBuilder builder, PackageDecisionDocument report)
    {
        builder.AppendLine("## Decision Records\n");
        foreach (var record in report.DecisionRecords.OrderBy(record => record.DecisionId ?? string.Empty, StringComparer.Ordinal)) builder.AppendLine($"- `{Cell(record.DecisionId ?? "unidentified")}` `{Cell(record.DecisionKind ?? "rejected")}` `{Cell(record.Classification ?? "admitted")}` rule `{Cell(record.RuleId)}`");
        if (report.DecisionRecords.Count == 0) builder.AppendLine("No decision records were admitted.\n");
    }

    private static void SectionRows(StringBuilder builder, string title, IReadOnlyList<PackageDecisionCorrelationRow> rows)
    {
        builder.AppendLine($"## {title}\n");
        if (rows.Count == 0) { builder.AppendLine("No rows.\n"); return; }
        builder.AppendLine("| Package | Version | Decision | Source | Commit | Relation | File:line | Rule |\n| --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var row in rows) builder.AppendLine($"| {Cell(row.PackageName)} | {Cell(row.ArtifactVersion)} | {Cell(row.DecisionKind)} | {Cell(row.SourceLabel)} | {Cell(row.CommitSha)} | {Cell(row.DependencyRelation)} | {Cell($"{row.Evidence.FilePath}:{row.Evidence.StartLine}-{row.Evidence.EndLine}")} | {Cell(RuleId)} |");
    }

    private static void RenderContext(StringBuilder builder, string title, object? context)
    {
        builder.AppendLine($"## {title}\n");
        if (context is not PackageDecisionContext typed)
        {
            builder.AppendLine("Not requested.\n");
            return;
        }

        builder.AppendLine($"- Status: `{Cell(typed.Status)}`");
        builder.AppendLine($"- Classification: `{Cell(typed.Classification)}`\n");
        foreach (var row in typed.Rows)
        {
            builder.AppendLine($"- `{Cell(row.PackageName)}` `{Cell(row.SourceLabel)}` `{Cell(row.Classification)}` ({Cell(row.RuleId)}, {Cell(row.EvidenceTier)}): {Cell(row.Message)}");
        }
        foreach (var gap in typed.Gaps)
        {
            builder.AppendLine($"- Gap `{Cell(gap.Classification)}`: {Cell(gap.Message)} ({Cell(gap.RuleId)}, {Cell(gap.EvidenceTier)})");
        }
        if (typed.Rows.Count == 0 && typed.Gaps.Count == 0) builder.AppendLine("No context rows.\n");
    }

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);
}
