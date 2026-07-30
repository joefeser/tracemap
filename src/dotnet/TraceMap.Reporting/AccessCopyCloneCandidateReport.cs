using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record AccessCopyCloneOptions(
    string IndexPath,
    string OutputDirectory,
    int MaxCandidates = 1_000,
    int MaxFlowPaths = 1_000,
    int MaxGaps = 1_000);

public sealed record AccessCopyCloneResult(
    AccessCopyCloneReport Report,
    string MarkdownPath,
    string JsonPath);

public sealed record AccessCopyCloneReport(
    string SchemaVersion,
    string RepositoryId,
    string CommitSha,
    string Coverage,
    AccessCopyCloneQuery Query,
    AccessCopyCloneSummary Summary,
    IReadOnlyList<AccessCopyCloneCandidate> Candidates,
    IReadOnlyList<AccessCopyCloneGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record AccessCopyCloneQuery(int MaxCandidates, int MaxFlowPaths, int MaxGaps);
public sealed record AccessCopyCloneSummary(
    int CandidateCount,
    int CandidatePathCount,
    int GapCount,
    bool Truncated);

public sealed record AccessCopyCloneCandidate(
    string CandidateId,
    string Classification,
    string Shape,
    string QueryNodeId,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyList<AccessCopyCloneParticipant> Participants,
    IReadOnlyList<string> FlowPathIds,
    IReadOnlyList<AccessCopyCloneEvidence> Evidence,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> Limitations);

public sealed record AccessCopyCloneParticipant(
    string NodeId,
    string NodeKind,
    string Role,
    AccessCopyCloneEvidence Evidence);

public sealed record AccessCopyCloneEvidence(
    string FactId,
    string RuleId,
    string EvidenceTier,
    string CommitSha,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    string CoverageLabel,
    IReadOnlyList<string> Limitations);

public sealed record AccessCopyCloneGap(
    string GapId,
    string Classification,
    string ScopeKind,
    string? ScopeId,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public static class AccessCopyCloneCandidateReporter
{
    public const string SchemaVersion = "tracemap.access-copy-clone-candidate.v1";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly IReadOnlyDictionary<string, (string Classification, string Shape)> SupportedQueryKinds =
        new Dictionary<string, (string, string)>(StringComparer.Ordinal)
        {
            ["append"] = ("Candidate", "bulk-append-shape"),
            ["make-table"] = ("Candidate", "table-creation-shape"),
            ["update"] = ("NeedsReview", "update-in-place-shape"),
            ["bulk"] = ("NeedsReview", "bulk-mutation-shape"),
            ["compound"] = ("NeedsReview", "compound-mutation-shape")
        };
    private static readonly IReadOnlyList<string> ReportLimitations =
    [
        "Candidate classifications describe static Access query shapes; they do not prove cloning, copying, business intent, source-to-target direction, row equivalence, transactionality, execution, or production use.",
        "Dependency roles remain unknown because the persisted Access query facts do not retain role-specific source, target, or field-correspondence evidence.",
        "The report does not prove generated-key handling, parent/child sequencing, loop behavior, runtime reachability, completeness, correctness, migration safety, safety to run, or release approval.",
        "Opaque stable identities are rendered; raw names, SQL, VBA, expressions, macro bodies, literals, values, row counts, connections, customer identity, and local paths are omitted."
    ];

    public static async Task<AccessCopyCloneResult> WriteAsync(
        AccessCopyCloneOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var report = await BuildReportAsync(options, cancellationToken);
        var output = Path.GetFullPath(options.OutputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidDataException("AccessCopyCloneOutputExists");
        var parent = Path.GetDirectoryName(output) ?? throw new InvalidDataException("AccessCopyCloneOutputInvalid");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(output)}.access-copy-clone-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            var markdown = Path.Combine(staging, "access-copy-clone.md");
            var json = Path.Combine(staging, "access-copy-clone.json");
            await File.WriteAllTextAsync(markdown, RenderMarkdown(report), new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(json, JsonSerializer.Serialize(report, JsonOptions) + "\n", new UTF8Encoding(false), cancellationToken);
            Directory.Move(staging, output);
            return new(report, Path.Combine(output, "access-copy-clone.md"), Path.Combine(output, "access-copy-clone.json"));
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true);
            throw;
        }
    }

    public static async Task<AccessCopyCloneReport> BuildReportAsync(
        AccessCopyCloneOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var (repository, commitSha, facts) = await AccessScreenDataFlowReporter.ReadFactsAsync(options.IndexPath, cancellationToken);
        return Build(repository, commitSha, facts, options.MaxCandidates, options.MaxFlowPaths, options.MaxGaps);
    }

    internal static AccessCopyCloneReport Build(
        string repository,
        string? commitSha,
        IReadOnlyList<CodeFact> facts,
        int maxCandidates,
        int maxFlowPaths,
        int maxGaps)
    {
        var safeFacts = facts.Where(IsSafeAccessFact).OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var safeFactsById = safeFacts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        var flow = AccessScreenDataFlowReporter.Build(repository, commitSha, safeFacts, 12, maxFlowPaths, maxGaps);
        var dependencies = safeFacts
            .Where(fact => fact.FactType == FactTypes.AccessQueryDependencyCandidate && SafeStableKey(fact.SourceSymbol))
            .GroupBy(fact => fact.SourceSymbol!, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var externalObjects = safeFacts
            .Where(fact => fact.FactType == FactTypes.AccessExternalLinkDeclared && SafeStableKey(fact.SourceSymbol))
            .Select(fact => fact.SourceSymbol!)
            .ToHashSet(StringComparer.Ordinal);
        var gaps = new List<AccessCopyCloneGap>();
        var candidates = new List<AccessCopyCloneCandidate>();
        var truncated = flow.Summary.Truncated;
        var flowPathsByStableKey = flow.Paths
            .SelectMany(path => path.Nodes.Select(node => (node.StableKey, Path: path)))
            .GroupBy(item => item.StableKey, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Path).DistinctBy(path => path.PathId)
                    .OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        if (flow.Summary.Truncated)
            AddGap(
                gaps,
                maxGaps,
                ref truncated,
                Gap(
                    "AccessCopyCloneFlowPathLimitReached",
                    "flow",
                    null,
                    flow.Paths.SelectMany(path => path.SupportingFactIds).ToArray()));

        foreach (var fact in safeFacts.Where(fact => fact.FactType == FactTypes.AccessQueryDeclared))
        {
            var queryKind = SafeCategory(fact.Properties.GetValueOrDefault("queryKind"), "unknown");
            if (!SupportedQueryKinds.TryGetValue(queryKind, out var supported))
                continue;
            if (!SafeStableKey(fact.TargetSymbol))
            {
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneUnsafeStableIdentity", "saved-query", null, fact.FactId));
                continue;
            }
            if (candidates.Count >= maxCandidates)
            {
                truncated = true;
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneCandidateLimitReached", "report", null));
                break;
            }

            var queryNodeId = NodeId(fact.TargetSymbol!);
            var queryDependencies = dependencies.GetValueOrDefault(fact.TargetSymbol!) ?? [];
            var participants = queryDependencies
                .Where(dependency => SafeStableKey(dependency.TargetSymbol))
                .Select(dependency => new AccessCopyCloneParticipant(
                    NodeId(dependency.TargetSymbol!),
                    SafeTargetKind(dependency.Properties.GetValueOrDefault("targetKind")),
                    "dependency-role-unknown",
                    Evidence(dependency)))
                .OrderBy(participant => participant.NodeId, StringComparer.Ordinal)
                .ThenBy(participant => participant.Evidence.FactId, StringComparer.Ordinal)
                .ToArray();
            var flowPaths = flowPathsByStableKey.GetValueOrDefault(fact.TargetSymbol!) ?? [];
            var paths = flowPaths.Select(path => path.PathId).ToArray();
            var primaryEvidence = Evidence(fact);
            var evidence = queryDependencies
                .Concat(flowPaths
                    .SelectMany(path => path.SupportingFactIds)
                    .Distinct(StringComparer.Ordinal)
                    .Where(safeFactsById.ContainsKey)
                    .Select(factId => safeFactsById[factId]))
                .Append(fact)
                .GroupBy(item => item.FactId, StringComparer.Ordinal)
                .Select(group => Evidence(group.First()))
                .OrderBy(item => item.FactId, StringComparer.Ordinal)
                .ToArray();
            var supportingFacts = evidence.Select(item => item.FactId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var limitations = evidence.SelectMany(item => item.Limitations)
                .Append("dependency-role-unknown")
                .Append("no-field-correspondence")
                .Append("no-copy-or-clone-conclusion")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var coverageLabels = evidence.Select(item => item.CoverageLabel)
                .Append(paths.Length == 0 ? "flow-path-unavailable" : "flow-path-candidate")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var candidate = new AccessCopyCloneCandidate(
                Id("candidate", fact.FactId, queryKind),
                supported.Classification,
                supported.Shape,
                queryNodeId,
                RuleIds.LegacyAccessCopyCloneCandidate,
                evidence.Select(item => item.EvidenceTier).OrderBy(TierRank).FirstOrDefault()
                    ?? EvidenceTiers.Tier4Unknown,
                primaryEvidence.CommitSha,
                primaryEvidence.FilePath,
                primaryEvidence.StartLine,
                primaryEvidence.EndLine,
                primaryEvidence.ExtractorId,
                primaryEvidence.ExtractorVersion,
                participants,
                paths,
                evidence,
                supportingFacts,
                evidence.Select(item => item.RuleId).Append(RuleIds.LegacyAccessCopyCloneCandidate)
                    .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                evidence.Select(item => item.EvidenceTier).Distinct(StringComparer.Ordinal)
                    .OrderBy(TierRank).ThenBy(value => value, StringComparer.Ordinal).ToArray(),
                coverageLabels,
                limitations);
            candidates.Add(candidate);

            AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneRoleDirectionUnavailable", "candidate", candidate.CandidateId, supportingFacts));
            if (queryKind is "append" or "make-table" or "bulk" or "compound")
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneFieldCorrespondenceUnavailable", "candidate", candidate.CandidateId, supportingFacts));
            if (paths.Length == 0)
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneFlowPathUnavailable", "candidate", candidate.CandidateId, supportingFacts));
            if (queryDependencies.Any(dependency => dependency.Properties.GetValueOrDefault("coverageLabel") != "complete"
                    && dependency.Properties.GetValueOrDefault("coverageLabel") != "direct-static-reference"))
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneDependencyPartial", "candidate", candidate.CandidateId, supportingFacts));
            if (participants.Length > 1)
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneDependencyFanOutNeedsReview", "candidate", candidate.CandidateId, supportingFacts));
            if (queryDependencies.Any(dependency => dependency.TargetSymbol is not null && externalObjects.Contains(dependency.TargetSymbol)))
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneExternalParticipantPartial", "candidate", candidate.CandidateId, supportingFacts));
        }

        foreach (var fact in safeFacts.Where(fact => fact.FactType == FactTypes.AnalysisGap))
        {
            var classification = fact.Properties.GetValueOrDefault("classification") ?? string.Empty;
            if (classification.Contains("Dynamic", StringComparison.Ordinal)
                || classification.Contains("Macro", StringComparison.Ordinal)
                || classification.Contains("Vba", StringComparison.Ordinal)
                || classification.Contains("Query", StringComparison.Ordinal))
                AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneUpstreamEvidenceGap", "upstream", null, fact.FactId));
        }

        if (candidates.Count == 0)
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneCandidateEvidenceUnavailable", "database", null));
        if (flow.Gaps.Any(gap => gap.Classification == "AccessFlowCycleDetected"))
            AddGap(gaps, maxGaps, ref truncated, Gap("AccessCopyCloneFlowCycleNeedsReview", "flow", null));
        if (candidates.Count > 1)
            AddGap(gaps, maxGaps, ref truncated, Gap(
                "AccessCopyCloneParentChildSequenceUnavailable",
                "candidate-set",
                null,
                candidates.SelectMany(candidate => candidate.SupportingFactIds).ToArray()));

        var orderedGaps = gaps
            .GroupBy(gap => gap.GapId, StringComparer.Ordinal)
            .Select(group => group.First())
            .OrderBy(gap => gap.Classification, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .Take(maxGaps)
            .ToArray();
        return new(
            SchemaVersion,
            flow.RepositoryId,
            flow.CommitSha,
            orderedGaps.Length == 0 && !truncated ? "complete" : "partial",
            new(maxCandidates, maxFlowPaths, maxGaps),
            new(candidates.Count, candidates.Sum(candidate => candidate.FlowPathIds.Count), orderedGaps.Length, truncated),
            candidates.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray(),
            orderedGaps,
            ReportLimitations);
    }

    private static AccessCopyCloneEvidence Evidence(CodeFact fact) => new(
        fact.FactId,
        fact.RuleId,
        SafeTier(fact.EvidenceTier),
        SafeCommit(fact.CommitSha) ?? "unknown",
        SafeEvidencePath(fact.Evidence.FilePath),
        fact.Evidence.StartLine,
        fact.Evidence.EndLine,
        SafeToken(fact.Evidence.ExtractorId),
        SafeToken(fact.Evidence.ExtractorVersion),
        SafeCategory(fact.Properties.GetValueOrDefault("coverageLabel"), "unknown"),
        SafeLimitations(fact.Properties.GetValueOrDefault("limitations")));

    private static AccessCopyCloneGap Gap(
        string classification,
        string scopeKind,
        string? scopeId,
        params string[] supportingFactIds) => new(
            Id("gap", classification, scopeId ?? "global", SupportingFactsDigest(supportingFactIds)),
            classification,
            scopeKind,
            scopeId,
            RuleIds.LegacyAccessCopyCloneCandidate,
            EvidenceTiers.Tier4Unknown,
            supportingFactIds.Where(SafeFactId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["static-candidate-gap", "not-clean-absence", "no-copy-or-clone-conclusion"]);

    private static void AddGap(
        List<AccessCopyCloneGap> gaps,
        int maxGaps,
        ref bool truncated,
        AccessCopyCloneGap gap)
    {
        if (gaps.Count < maxGaps) gaps.Add(gap);
        else
        {
            truncated = true;
            if (maxGaps > 0 && !gaps.Any(item => item.Classification == "AccessCopyCloneGapLimitReached"))
                gaps[^1] = Gap("AccessCopyCloneGapLimitReached", "report", null);
        }
    }

    private static bool IsSafeAccessFact(CodeFact fact) =>
        SafeFactId(fact.FactId)
        && fact.RuleId.StartsWith("legacy.access.", StringComparison.Ordinal)
        && fact.RuleId.Length <= 128
        && fact.RuleId.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '.');
    private static bool SafeFactId(string value) =>
        value.StartsWith("fact-", StringComparison.Ordinal)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    private static bool SafeStableKey(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 192
        && value.StartsWith("access-", StringComparison.Ordinal)
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-');
    private static string NodeId(string stableKey) => $"access-copy-node-{FactFactory.Hash(stableKey, 32)}";
    private static string Id(params string[] parts) => $"access-copy-{FactFactory.Hash(string.Join('|', parts), 32)}";
    private static string SafeTargetKind(string? value) => value is "table" or "query" or "saved-query" ? value : "unknown";
    private static string SafeCategory(string? value, string fallback) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character == '-')
            ? value
            : fallback;
    private static string SafeToken(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Length <= 128
        && value.All(character => char.IsAsciiLetterOrDigit(character) || character is '-' or '_' or '.' or '/')
            ? value
            : "unknown";
    private static string? SafeCommit(string? value) =>
        value is { Length: 40 or 64 } && value.All(Uri.IsHexDigit)
            ? value.ToLowerInvariant()
            : null;
    private static string SafeEvidencePath(string value)
    {
        var normalized = value.Replace('\\', '/');
        var driveRooted = normalized.Length >= 3
            && char.IsAsciiLetter(normalized[0])
            && normalized[1] == ':'
            && normalized[2] == '/';
        return !string.IsNullOrWhiteSpace(normalized)
            && !normalized.StartsWith("/", StringComparison.Ordinal)
            && !normalized.StartsWith("//", StringComparison.Ordinal)
            && !driveRooted
            && !normalized.Contains(':', StringComparison.Ordinal)
            && !normalized.Any(character => character < 0x20)
            && !Path.IsPathFullyQualified(normalized)
            && !normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(segment => segment is "." or "..")
                ? normalized
                : "unavailable";
    }
    private static string SupportingFactsDigest(IEnumerable<string> values)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var value in values.Where(SafeFactId).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal))
        {
            hash.AppendData(Encoding.UTF8.GetBytes(value));
            hash.AppendData([0]);
        }
        return Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant();
    }
    private static string[] SafeLimitations(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(item => item.Length <= 128 && item.All(character => char.IsAsciiLetterOrDigit(character) || character == '-'))
                .Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    private static string SafeTier(string value) => value is
        EvidenceTiers.Tier1Semantic
        or EvidenceTiers.Tier2Structural
        or EvidenceTiers.Tier3SyntaxOrTextual
        or EvidenceTiers.Tier4Unknown
            ? value
            : EvidenceTiers.Tier4Unknown;
    private static int TierRank(string tier) => tier switch
    {
        EvidenceTiers.Tier4Unknown => 0,
        EvidenceTiers.Tier3SyntaxOrTextual => 1,
        EvidenceTiers.Tier2Structural => 2,
        _ => 3
    };

    private static string RenderMarkdown(AccessCopyCloneReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine("# Microsoft Access Copy/Clone Static Candidates");
        builder.AppendLine();
        builder.AppendLine($"- Schema: `{report.SchemaVersion}`");
        builder.AppendLine($"- Repository: `{report.RepositoryId}`");
        builder.AppendLine($"- Commit: `{report.CommitSha}`");
        builder.AppendLine($"- Coverage: `{report.Coverage}`");
        builder.AppendLine($"- Candidates: `{report.Summary.CandidateCount}`");
        builder.AppendLine($"- Flow references: `{report.Summary.CandidatePathCount}`");
        builder.AppendLine($"- Gaps: `{report.Summary.GapCount}`");
        builder.AppendLine();
        builder.AppendLine("## Candidates");
        builder.AppendLine();
        if (report.Candidates.Count == 0)
            builder.AppendLine("- No supported candidate evidence was available; this is not clean absence.");
        foreach (var candidate in report.Candidates)
        {
            builder.AppendLine($"### `{candidate.CandidateId}`");
            builder.AppendLine();
            builder.AppendLine($"- Classification: `{candidate.Classification}`");
            builder.AppendLine($"- Shape: `{candidate.Shape}`");
            builder.AppendLine($"- Query: `{candidate.QueryNodeId}`");
            builder.AppendLine($"- Primary evidence: rule `{candidate.RuleId}`, tier `{candidate.EvidenceTier}`, commit `{candidate.CommitSha}`, span `{candidate.FilePath}:{candidate.StartLine}-{candidate.EndLine}`, extractor `{candidate.ExtractorId}/{candidate.ExtractorVersion}`");
            builder.AppendLine($"- Flow paths: {Format(candidate.FlowPathIds)}");
            builder.AppendLine($"- Supporting facts: {Format(candidate.SupportingFactIds)}");
            builder.AppendLine($"- Rules: {Format(candidate.RuleIds)}");
            builder.AppendLine($"- Evidence tiers: {Format(candidate.EvidenceTiers)}");
            builder.AppendLine($"- Coverage: {Format(candidate.CoverageLabels)}");
            builder.AppendLine("- Participants:");
            if (candidate.Participants.Count == 0) builder.AppendLine("  - none observed");
            foreach (var participant in candidate.Participants)
                builder.AppendLine($"  - `{participant.Role}` / `{participant.NodeKind}` / `{participant.NodeId}`; fact `{participant.Evidence.FactId}`, rule `{participant.Evidence.RuleId}`, tier `{participant.Evidence.EvidenceTier}`.");
            builder.AppendLine("- Evidence:");
            foreach (var evidence in candidate.Evidence)
                builder.AppendLine($"  - fact `{evidence.FactId}`, rule `{evidence.RuleId}`, tier `{evidence.EvidenceTier}`, coverage `{evidence.CoverageLabel}`, span `{evidence.FilePath}:{evidence.StartLine}-{evidence.EndLine}`, extractor `{evidence.ExtractorId}/{evidence.ExtractorVersion}`.");
            builder.AppendLine($"- Limitations: {string.Join("; ", candidate.Limitations)}");
            builder.AppendLine();
        }
        builder.AppendLine("## Gaps");
        builder.AppendLine();
        foreach (var gap in report.Gaps)
            builder.AppendLine($"- `{gap.Classification}` ({gap.ScopeKind}); rule `{gap.RuleId}`, tier `{gap.EvidenceTier}`, supporting facts {Format(gap.SupportingFactIds)}.");
        builder.AppendLine();
        builder.AppendLine("## Limitations");
        builder.AppendLine();
        foreach (var limitation in report.Limitations) builder.AppendLine($"- {limitation}");
        return builder.ToString();
    }

    private static string Format(IReadOnlyList<string> values) =>
        values.Count == 0 ? "none" : string.Join(", ", values.Select(value => $"`{value}`"));

    private static void Validate(AccessCopyCloneOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath) || !File.Exists(options.IndexPath))
            throw new InvalidDataException("AccessCopyCloneIndexUnavailable");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory))
            throw new InvalidDataException("AccessCopyCloneOutputRequired");
        if (options.MaxCandidates is <= 0 or > 10_000
            || options.MaxFlowPaths is <= 0 or > 10_000
            || options.MaxGaps is <= 0 or > 10_000)
            throw new InvalidDataException("AccessCopyCloneBoundsInvalid");
        if (string.Equals(
                Path.GetFullPath(options.IndexPath),
                Path.GetFullPath(options.OutputDirectory),
                OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
            throw new InvalidDataException("AccessCopyCloneOutputInvalid");
        var indexPath = Path.GetFullPath(options.IndexPath);
        for (var current = Path.GetFullPath(options.OutputDirectory);
             !string.IsNullOrEmpty(current);
             current = Path.GetDirectoryName(current))
        {
            if ((Directory.Exists(current) || File.Exists(current))
                && File.GetAttributes(current).HasFlag(FileAttributes.ReparsePoint)
                && !IsSameOrAncestor(current, indexPath))
                throw new InvalidDataException("AccessCopyCloneOutputInvalid");
        }
    }

    private static bool IsSameOrAncestor(string ancestor, string path)
    {
        var comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedAncestor = Path.TrimEndingDirectorySeparator(ancestor);
        var normalizedPath = Path.TrimEndingDirectorySeparator(path);
        return string.Equals(normalizedAncestor, normalizedPath, comparison)
            || normalizedPath.StartsWith(normalizedAncestor + Path.DirectorySeparatorChar, comparison);
    }
}
