using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
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
    string? AsOf = null);

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
    IReadOnlyList<PackageDecisionCorrelationRow>? ArtifactChanges = null,
    object? AdvisoryClaims = null,
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
    string? ScannedAt);

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
public sealed record PackageDecisionExternalReference(string RowId, string Classification, string RuleId, string EvidenceTier, string Message);
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
    int? EndLine = null);

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
    bool GapCapReached);

public static class PackageDecisionCorrelationReporter
{
    public const string RuleId = "package.decision.correlation.v1";
    private const string Version = "1.0";
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
        "This command is read-only evidence reporting and never grants admission, rejection, revocation, blocking, approval, or enforcement authority."
    ];

    public static async Task<PackageDecisionResult> WriteAsync(PackageDecisionOptions options, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(options.DecisionPath)) throw new ArgumentException("package-decision requires --decision <path>.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.IndexPath)) throw new ArgumentException("package-decision requires --index <path>.", nameof(options));
        if (string.IsNullOrWhiteSpace(options.OutputPath)) throw new ArgumentException("package-decision requires --out <path>.", nameof(options));
        var format = options.Format.Equals("md", StringComparison.OrdinalIgnoreCase) ? "markdown" : options.Format.ToLowerInvariant();
        if (format is not "markdown" and not "json") throw new ArgumentException("package-decision --format must be markdown or json.", nameof(options));
        RejectOutputAlias(options.DecisionPath, options.IndexPath, options.OutputPath);
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
        var index = await ReadIndexAsync(options.IndexPath, cancellationToken);
        var sources = index.Sources.OrderBy(source => source.Label, StringComparer.Ordinal).ThenBy(source => source.SourceIndexId, StringComparer.Ordinal).ToArray();
        if (!string.IsNullOrWhiteSpace(options.Source))
            sources = sources.Where(source => string.Equals(source.Label, options.Source, StringComparison.Ordinal)).ToArray();

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
        var findingCapReached = false;
        var gapCapReached = admission.Gaps.Count > Math.Max(1, options.MaxGaps);
        if (sources.Length == 0 && options.Source is not null)
            AddGap(gaps, options.MaxGaps, ref gapCapReached, new PackageDecisionGap($"pd-selector-source:{Hash(options.Source)}", "SelectorNoMatch", "The requested source selector matched no source snapshot.", RuleId, EvidenceTiers.Tier4Unknown, SourceLabel: options.Source));
        foreach (var record in records)
        {
            foreach (var source in sources)
            {
                var sourceFacts = index.Facts.Where(fact => fact.SourceIndexId == source.SourceIndexId && fact.FactType == FactTypes.PackageReferenced).ToArray();
                var rows = sourceFacts.Where(fact => string.Equals(fact.Properties.GetValueOrDefault("ecosystem"), record.Ecosystem, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(NormalizeName(record.Ecosystem, record.PackageName), NormalizeName(record.Ecosystem, fact.Properties.GetValueOrDefault("packageName") ?? string.Empty), StringComparison.Ordinal)).ToArray();

                var capabilityFacts = rows.Length == 0 ? sourceFacts : rows;
                if (!FullCommit(source.CommitSha))
                {
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

                foreach (var fact in rows)
                {
                    if (CountRows(exact, mismatch, possible, ambiguous) >= options.MaxFindings)
                    {
                        findingCapReached = true;
                        break;
                    }
                    var row = Correlate(record, source, fact, index.ScannedAt.GetValueOrDefault(source.SourceIndexId), asOf);
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
        var report = new PackageDecisionDocument(
            "package-decision-correlation", Version, "DecisionSnapshotV1",
            new PackageDecisionQuery($"value-hash:{CombinedReportHelpers.Hash(options.DecisionPath, 16)}", $"value-hash:{CombinedReportHelpers.Hash(options.IndexPath, 16)}", options.Source ?? "default", options.Ecosystem, options.DecisionId, options.Classification, options.MaxFindings, options.MaxGaps, options.ExitCode, options.AsOf),
            recordRows.OrderBy(row => row.Classification ?? string.Empty, StringComparer.Ordinal).ThenBy(row => row.ProducerId ?? string.Empty, StringComparer.Ordinal).ThenBy(row => row.DecisionId ?? string.Empty, StringComparer.Ordinal).ToArray(),
            sources.Select(source => ToSource(source, index.ScannedAt.GetValueOrDefault(source.SourceIndexId), index.KnownGaps.Where(gap => gap.SourceIndexId == source.SourceIndexId))).ToArray(),
            selectedExact, selectedMismatch, selectedPossible, selectedAmbiguous,
            excluded.OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.RowId, StringComparer.Ordinal).ToArray(),
            stale.OrderBy(row => row.SourceLabel, StringComparer.Ordinal).ThenBy(row => row.RowId, StringComparer.Ordinal).ToArray(), [],
            gaps.OrderBy(gap => gap.Classification, StringComparer.Ordinal).ThenBy(gap => gap.GapId, StringComparer.Ordinal).ToArray(),
            new PackageDecisionSummary(sources.Length, records.Length, selectedExact.Count, selectedMismatch.Count, selectedPossible.Count, selectedAmbiguous.Count, excluded.Count, stale.Count, 0, gaps.Count, gaps.Count > 0 || findingCapReached ? "ReducedCoverage" : "FullEvidenceAvailable", findingCapReached, gapCapReached),
            Limitations);
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
        var digestEqual = record.ArtifactDigest is not null && evidenceDigest is not null && string.Equals(record.ArtifactDigestAlgorithm, evidenceAlgorithm, StringComparison.Ordinal) && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(record.ArtifactDigest), Encoding.UTF8.GetBytes(evidenceDigest));
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

    private static string? SafeProperty(IReadOnlyDictionary<string, string> properties, string key) => properties.TryGetValue(key, out var value) && value.Length <= 160 && !Path.IsPathRooted(value) && !value.StartsWith("C:\\", StringComparison.OrdinalIgnoreCase) && !value.Contains("://", StringComparison.Ordinal) ? value : null;
    private static string? SafeLockfileHash(string? value) => value is not null && value.Length == 32 && value.All(character => character is >= 'a' and <= 'f' or >= '0' and <= '9') ? value : null;
    private static string? SafeVersionHash(string? value) => value is not null && value.StartsWith("version-hash:", StringComparison.Ordinal) && value.Length <= 80 && value[13..].All(character => character is >= 'a' and <= 'f' or >= '0' and <= '9') ? value : null;
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
    private static PackageDecisionSource ToSource(CombinedReportSource source, DateTimeOffset? scannedAt, IEnumerable<CombinedKnownGapRow> gaps) => new(source.Label, source.SourceIndexId, source.ScanId, $"repo-hash:{CombinedReportHelpers.Hash(source.RepoName, 16)}", SafeCommit(source.CommitSha), source.ScannerVersion, source.Language, source.AnalysisLevel, source.BuildStatus, SourceHasCredibilityGap(source, gaps) ? "ReducedCoverage" : "FullEvidenceAvailable", scannedAt?.ToString("O", CultureInfo.InvariantCulture));

    private static PackageDecisionGap SourceGap(string classification, string message, PackageDecisionRecord record, CombinedReportSource source) =>
        new(GapId(classification, record, source), classification, message, RuleId, EvidenceTiers.Tier4Unknown, record.DecisionId, source.Label, record.Ecosystem, source.SourceIndexId, source.ScanId, SafeCommit(source.CommitSha));

    private static bool HasEcosystemCapability(IEnumerable<CombinedFactRow> facts, string ecosystem) => facts.Any(fact => string.Equals(fact.Properties.GetValueOrDefault("ecosystem"), ecosystem, StringComparison.OrdinalIgnoreCase));

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

    private static void RejectOutputAlias(string decisionPath, string indexPath, string outputPath)
    {
        var fullOutput = Path.GetFullPath(outputPath);
        if (Directory.Exists(fullOutput) || string.IsNullOrWhiteSpace(Path.GetExtension(fullOutput)))
            return;

        var outputIdentity = FileIdentity(fullOutput);
        if (outputIdentity is not null && (PathIdentityEquals(outputIdentity, FileIdentity(decisionPath)) || PathIdentityEquals(outputIdentity, FileIdentity(indexPath))))
            throw new InvalidDataException("package-decision --out must not alias the decision or index input.");
    }

    private static string? FileIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        try
        {
            var target = new FileInfo(fullPath).ResolveLinkTarget(true);
            return target?.FullName ?? fullPath;
        }
        catch (IOException)
        {
            return fullPath;
        }
        catch (UnauthorizedAccessException)
        {
            return fullPath;
        }
    }

    private static bool PathIdentityEquals(string? left, string? right) => left is not null && right is not null && string.Equals(left, right, StringComparison.Ordinal);

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
            var scanned = read.Sources.ToDictionary(source => source.SourceIndexId, source => ParseScannedAt(read, source), StringComparer.Ordinal);
            return new IndexRead(read.Sources, read.Facts, read.KnownGaps, scanned);
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
        return new IndexRead([source], facts, known, new Dictionary<string, DateTimeOffset?>(StringComparer.Ordinal) { ["default"] = manifest.ScannedAt });
    }

    private static DateTimeOffset? ParseScannedAt(CombinedReadResult read, CombinedReportSource source) => null;
    private static string? LanguageFromScanner(string scanner) => scanner.Contains("typescript", StringComparison.OrdinalIgnoreCase) ? "typescript" : scanner.Contains("python", StringComparison.OrdinalIgnoreCase) ? "python" : scanner.Contains("jvm", StringComparison.OrdinalIgnoreCase) ? "jvm" : "csharp";
    private static async Task<bool> TableExists(SqliteConnection connection, string name, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "select count(*) from sqlite_master where type='table' and name=$name;"; command.Parameters.AddWithValue("$name", name); return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0; }
    private static async Task<bool> ColumnExistsAsync(SqliteConnection connection, string table, string column, CancellationToken cancellationToken) { await using var command = connection.CreateCommand(); command.CommandText = "select count(*) from pragma_table_info($table) where name = $column;"; command.Parameters.AddWithValue("$table", table); command.Parameters.AddWithValue("$column", column); return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0; }
    private static IReadOnlyDictionary<string, string> ParseProperties(string json) => JsonSerializer.Deserialize<Dictionary<string, string?>>(json, JsonOptions)?.Where(pair => pair.Value is not null).ToDictionary(pair => pair.Key, pair => pair.Value!, StringComparer.Ordinal) ?? new Dictionary<string, string>(StringComparer.Ordinal);
    private sealed record IndexRead(IReadOnlyList<CombinedReportSource> Sources, IReadOnlyList<CombinedFactRow> Facts, IReadOnlyList<CombinedKnownGapRow> KnownGaps, IReadOnlyDictionary<string, DateTimeOffset?> ScannedAt);

    private static string RenderMarkdown(PackageDecisionDocument report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# TraceMap Package Decision Correlation Report\n");
        builder.AppendLine($"- Version: `{report.Version}`\n- Coverage: `{report.Summary.Coverage}`\n- Sources: `{report.Summary.SourceCount}`\n- Records: `{report.Summary.RecordCount}`\n- Exact matches: `{report.Summary.ExactCount}`\n- Digest mismatches: `{report.Summary.DigestMismatchCount}`\n- Possible matches: `{report.Summary.PossibleCount}`\n- Ambiguous references: `{report.Summary.AmbiguousCount}`\n- Excluded sources: `{report.Summary.ExcludedCount}`\n- Gaps: `{report.Summary.GapCount}`\n");
        SectionRecords(builder, report);
        SectionRows(builder, "Exact Matches", report.ExactMatches);
        SectionRows(builder, "Possible Matches", report.PossibleMatches);
        SectionRows(builder, "Artifact Identity Mismatches", report.DigestMismatches);
        SectionRows(builder, "Ambiguous References", report.AmbiguousReferences);
        builder.AppendLine("## Excluded Sources\n");
        foreach (var row in report.ExcludedSources) builder.AppendLine($"- `{Cell(row.SourceLabel)}`: {Cell(row.Message)} ({Cell(row.RuleId)}, {Cell(row.EvidenceTier)})");
        builder.AppendLine("\n## Stale and Runtime-Unproven References\n");
        foreach (var row in report.StaleReferences) builder.AppendLine($"- `{Cell(row.SourceLabel)}` `{Cell(row.Classification)}`: {Cell(row.Message)}");
        builder.AppendLine(report.StaleReferences.Count == 0 ? "No stale references were observed.\n" : string.Empty);
        builder.AppendLine("## Advisory Claims (external)\n\nNo advisory claims were supplied in PR1.\n");
        builder.AppendLine("## Optional Path and Reverse Context\n\nNot requested in PR1.\n");
        builder.AppendLine("## Before/After Artifact Changes\n\nNot requested in PR1.\n");
        builder.AppendLine("## Gaps\n");
        foreach (var gap in report.Gaps) builder.AppendLine($"- `{Cell(gap.Classification)}`: {Cell(gap.Message)} ({Cell(gap.RuleId)}, {Cell(gap.EvidenceTier)})");
        if (report.Gaps.Count == 0) builder.AppendLine("No gaps were recorded.\n");
        builder.AppendLine("## Limitations\n");
        foreach (var limitation in report.Limitations) builder.AppendLine($"- {Cell(limitation)}");
        return builder.ToString();
    }

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

    private static string Cell(string? value) => CombinedReportHelpers.Cell(value);
}
