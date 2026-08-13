using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record WebFormsModernizationOptions(
    string IndexPath,
    string OutputDirectory,
    int MaxSurfaces = 1_000,
    int MaxEventChains = 1_000,
    int MaxCandidates = 1_000,
    int MaxGaps = 1_000,
    int MaxDepth = 8,
    int MaxPaths = 1_000);

public sealed record WebFormsModernizationResult(
    WebFormsModernizationPacket Packet,
    string JsonPath,
    string MarkdownPath);

public sealed record WebFormsModernizationPacket(
    string SchemaVersion,
    string PacketId,
    string RuleId,
    string ClaimLevel,
    string Coverage,
    IReadOnlyList<WebFormsModernizationSource> Sources,
    WebFormsModernizationSummary Summary,
    IReadOnlyList<WebFormsModernizationProject> Projects,
    IReadOnlyList<WebFormsModernizationSurface> Surfaces,
    IReadOnlyList<WebFormsModernizationEventChain> EventChains,
    IReadOnlyList<WebFormsModernizationSliceCandidate> StructuralSliceCandidates,
    IReadOnlyList<WebFormsModernizationGap> Gaps,
    IReadOnlyList<string> OwnerQuestions,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsModernizationSource(
    string SourceId,
    string RepositoryId,
    string ScanId,
    string CommitSha,
    string AnalysisLevel,
    string BuildStatus);

public sealed record WebFormsModernizationSummary(
    int ProjectCount,
    int SurfaceCount,
    int EventChainCount,
    int StructuralSliceCandidateCount,
    int GapCount,
    bool Truncated);

public sealed record WebFormsModernizationProject(
    string ProjectId,
    int SurfaceCount,
    IReadOnlyList<WebFormsModernizationEvidence> Evidence,
    IReadOnlyList<string> SupportingFactIds);

public sealed record WebFormsModernizationSurface(
    string SurfaceId,
    string SurfaceKind,
    string ProjectId,
    IReadOnlyList<string> CompositionTargetIds,
    IReadOnlyList<string> ControlIds,
    WebFormsModernizationEvidence Evidence,
    IReadOnlyList<WebFormsModernizationEvidence> SupportingEvidence,
    IReadOnlyList<string> SupportingFactIds);

public sealed record WebFormsModernizationEventChain(
    string ChainId,
    string SurfaceId,
    string EventSourceId,
    string BindingFactId,
    string? HandlerId,
    string? HandlerFactId,
    string Classification,
    string? LegacyPathId,
    string? TerminalKind,
    IReadOnlyList<WebFormsModernizationEvidence> Evidence,
    IReadOnlyList<WebFormsModernizationPathEvidence> PathEvidence,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsModernizationPathEvidence(
    string EvidenceId,
    string EvidenceKind,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsModernizationSliceCandidate(
    string CandidateId,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string? OwnerLabel,
    bool OwnerNamingRequired,
    IReadOnlyList<string> SurfaceIds,
    IReadOnlyList<WebFormsModernizationEvidence> Evidence,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsModernizationEvidence(
    string FactId,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string CommitSha,
    string FilePath,
    int StartLine,
    int EndLine,
    string ExtractorId,
    string ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Limitations);

public sealed record WebFormsModernizationGap(
    string GapId,
    string Classification,
    string ScopeKind,
    string? ScopeId,
    string RuleId,
    string EvidenceTier,
    string CoverageLabel,
    string CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorId,
    string? ExtractorVersion,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> Limitations);

public static class WebFormsModernizationPacketReporter
{
    public const string SchemaVersion = "webforms-modernization-packet.v1";
    public const string PacketRuleId = RuleIds.LegacyWebFormsModernizationPacket;
    private const string ClaimLevel = "local-only";
    private const string UnknownCoverage = "unknown";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    private static readonly IReadOnlyList<string> PacketLimitations =
    [
        "This packet composes deterministic static evidence from one TraceMap snapshot; it does not prove runtime reachability, execution, event firing, postback behavior, validation, authorization, persistence, or production use.",
        "Structural candidates group only declared static surface composition; they do not name business capabilities or prove workflow completeness, parity, migration scope, effort, cloud readiness, target architecture, test completeness, security approval, release approval, or safety to change.",
        "A chain ending at a handler or without a terminal is useful reduced evidence, never proof that no backend behavior exists.",
        "The packet omits snippets, raw SQL, configuration values, URLs, connection strings, credentials, source values, repository remotes, and absolute local paths."
    ];
    private static readonly IReadOnlyList<string> Questions =
    [
        "Which owner-provided capability label should be assigned to each structural candidate?",
        "Which missing or dynamic handler shapes require owner-controlled follow-up evidence?",
        "Which handler-only chains are expected to reach a backend or external boundary?",
        "Which reduced build or analysis gaps must be closed before migration planning?"
    ];

    public static async Task<WebFormsModernizationResult> WriteAsync(
        WebFormsModernizationOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var packet = await BuildAsync(options, cancellationToken);
        var output = Path.GetFullPath(options.OutputDirectory);
        if (Directory.Exists(output) || File.Exists(output))
            throw new InvalidDataException("WebFormsModernizationOutputExists");
        var parent = Path.GetDirectoryName(output) ?? throw new InvalidDataException("WebFormsModernizationOutputInvalid");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, $".{Path.GetFileName(output)}.webforms-modernization-{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(staging);
            var json = Path.Combine(staging, "webforms-modernization.json");
            var markdown = Path.Combine(staging, "webforms-modernization.md");
            await File.WriteAllTextAsync(json, JsonSerializer.Serialize(packet, JsonOptions) + "\n", new UTF8Encoding(false), cancellationToken);
            await File.WriteAllTextAsync(markdown, RenderMarkdown(packet), new UTF8Encoding(false), cancellationToken);
            Directory.Move(staging, output);
            return new(packet, Path.Combine(output, Path.GetFileName(json)), Path.Combine(output, Path.GetFileName(markdown)));
        }
        catch
        {
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
            throw;
        }
    }

    public static async Task<WebFormsModernizationPacket> BuildAsync(
        WebFormsModernizationOptions options,
        CancellationToken cancellationToken = default)
    {
        Validate(options);
        var snapshot = await ReadSnapshotAsync(options.IndexPath, cancellationToken);
        var legacyFlow = await CombinedDependencyPathReporter.BuildReportAsync(new(
            options.IndexPath,
            Path.Combine(Path.GetTempPath(), "tracemap-webforms-modernization-unused"),
            View: LegacyFlowReportConstants.View,
            IncludeLegacyRoots: true,
            MaxDepth: options.MaxDepth,
            MaxPaths: options.MaxPaths), cancellationToken);
        return Build(snapshot, legacyFlow, options);
    }

    internal static WebFormsModernizationPacket Build(
        Snapshot snapshot,
        CombinedDependencyPathReport legacyFlow,
        WebFormsModernizationOptions options)
    {
        var gaps = new List<WebFormsModernizationGap>();
        var sourceAnalysisReduced = IsReducedAnalysisLevel(snapshot.AnalysisLevel);
        if (sourceAnalysisReduced)
            AddGeneratedGap(gaps, options.MaxGaps, snapshot, "SourceAnalysisCoverageReduced", "scan", snapshot.ScanId, []);
        var allFacts = snapshot.Facts.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        foreach (var invalid in allFacts.Where(fact => !HasRequiredProvenance(fact)))
            AddGeneratedGap(gaps, options.MaxGaps, snapshot, "EvidenceProvenanceUnavailable", "fact", invalid.FactId, [invalid.FactId]);
        var facts = allFacts.Where(HasRequiredProvenance).ToArray();
        var factsById = facts.ToDictionary(fact => fact.FactId, StringComparer.Ordinal);
        var truncated = legacyFlow.Summary.Truncated;
        var pageFacts = facts.Where(fact => fact.FactType == FactTypes.WebFormsPageDeclared).ToArray();
        var retainedPages = pageFacts.Take(options.MaxSurfaces).ToArray();
        if (retainedPages.Length < pageFacts.Length)
        {
            truncated = true;
            AddGeneratedGap(gaps, options.MaxGaps, snapshot, "WebFormsModernizationSurfaceLimitReached", "packet", null,
                pageFacts.Skip(retainedPages.Length).Select(fact => fact.FactId));
        }

        var compositionFacts = facts.Where(fact => fact.FactType == FactTypes.WebFormsCompositionDeclared).ToArray();
        var controlFacts = facts.Where(fact => fact.FactType == FactTypes.WebFormsControlDeclared).ToArray();
        var surfaces = retainedPages.Select(page =>
        {
            var surfaceId = SurfaceIdentity(page);
            var controls = controlFacts.Where(fact => fact.Properties.GetValueOrDefault("surfaceIdentity") == surfaceId).ToArray();
            var controlIds = controls.Select(fact => fact.Properties.GetValueOrDefault("controlIdentity") ?? fact.TargetSymbol)
                .Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>().ToHashSet(StringComparer.Ordinal);
            var supportingCompositions = compositionFacts.Where(fact => fact.SourceSymbol == surfaceId
                || (fact.SourceSymbol is not null && controlIds.Contains(fact.SourceSymbol))).ToArray();
            return new WebFormsModernizationSurface(
                surfaceId,
                SafeKind(page.Properties.GetValueOrDefault("directiveKind"), "unknown"),
                ProjectId(page.ProjectPath),
                supportingCompositions.Select(fact => SafeIdentity(fact.TargetSymbol)).Where(value => value is not null).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                controls.Select(fact => SafeIdentity(fact.Properties.GetValueOrDefault("controlIdentity"))).Where(value => value is not null).Cast<string>().Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                Evidence(page, gaps, options.MaxGaps, snapshot),
                supportingCompositions.Concat(controls).OrderBy(fact => fact.FactId, StringComparer.Ordinal).Select(fact => Evidence(fact, gaps, options.MaxGaps, snapshot)).ToArray(),
                supportingCompositions.Select(fact => fact.FactId).Append(page.FactId).Concat(controls.Select(fact => fact.FactId)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray());
        }).OrderBy(surface => surface.SurfaceId, StringComparer.Ordinal).ToArray();

        var projects = surfaces.GroupBy(surface => surface.ProjectId, StringComparer.Ordinal)
            .Select(group => new WebFormsModernizationProject(
                group.Key,
                group.Count(),
                group.SelectMany(surface => surface.SupportingEvidence.Prepend(surface.Evidence)).DistinctBy(evidence => evidence.FactId).OrderBy(evidence => evidence.FactId, StringComparer.Ordinal).ToArray(),
                group.SelectMany(surface => surface.SupportingFactIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray()))
            .OrderBy(project => project.ProjectId, StringComparer.Ordinal).ToArray();

        foreach (var fact in facts.Where(fact => fact.FactType == FactTypes.AnalysisGap && fact.RuleId.StartsWith("legacy.webforms.", StringComparison.Ordinal)))
            AddFactGap(gaps, options.MaxGaps, snapshot, fact);

        var handlersByBinding = facts.Where(fact => fact.FactType == FactTypes.WebFormsHandlerResolved)
            .Where(fact => !string.IsNullOrWhiteSpace(fact.Properties.GetValueOrDefault("bindingFactId")))
            .GroupBy(fact => fact.Properties["bindingFactId"], StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(fact => fact.FactId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var flowFacts = facts.Where(fact => fact.FactType == FactTypes.WebFormsEventFlowProjected).ToArray();
        var bindings = facts.Where(fact => fact.FactType == FactTypes.WebFormsEventBindingDeclared)
            .OrderBy(fact => fact.Evidence.FilePath, StringComparer.Ordinal)
            .ThenBy(fact => fact.Evidence.StartLine)
            .ThenBy(fact => fact.FactId, StringComparer.Ordinal).ToArray();
        var chains = new List<WebFormsModernizationEventChain>();
        var omittedChainSupport = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            if (chains.Count >= options.MaxEventChains)
            {
                truncated = true;
                omittedChainSupport.Add(binding.FactId);
                continue;
            }
            var handlers = handlersByBinding.GetValueOrDefault(binding.FactId) ?? [];
            var handler = handlers.Length == 1 ? handlers[0] : null;
            var flowFact = handler is null ? null : flowFacts.FirstOrDefault(fact => SplitIds(fact.Properties.GetValueOrDefault("supportingFactIds")).Contains(handler.FactId, StringComparer.Ordinal));
            var legacyPaths = legacyFlow.Paths.Where(path => path.SupportingFactIds.Contains(binding.FactId, StringComparer.Ordinal)
                || (handler is not null && (path.SupportingFactIds.Contains(handler.FactId, StringComparer.Ordinal)
                    || path.Nodes.FirstOrDefault()?.SymbolId == handler.TargetSymbol)))
                .OrderBy(path => path.PathId, StringComparer.Ordinal).ToArray();
            var support = new[] { binding, handler, flowFact }.Where(fact => fact is not null).Cast<CodeFact>().ToArray();
            foreach (var legacyPath in legacyPaths.Cast<CombinedPath?>().DefaultIfEmpty())
            {
                if (chains.Count >= options.MaxEventChains)
                {
                    truncated = true;
                    omittedChainSupport.Add(binding.FactId);
                    if (legacyPath is not null) omittedChainSupport.UnionWith(legacyPath.SupportingFactIds);
                    continue;
                }
                var pathEvidence = PathEvidence(legacyPath, snapshot, gaps, options.MaxGaps);
                var requiredPathEvidenceIds = legacyPath is null
                    ? []
                    : legacyPath.Nodes.Where(node => node.RuleId is not null && node.EvidenceTier is not null)
                        .Select(node => node.CombinedFactId ?? node.NodeId)
                        .Concat(legacyPath.Edges.Select(edge => edge.EdgeId))
                        .Distinct(StringComparer.Ordinal).ToArray();
                var retainedPathEvidenceIds = pathEvidence.Select(evidence => evidence.EvidenceId).ToHashSet(StringComparer.Ordinal);
                var pathProvenanceAvailable = legacyPath is null || requiredPathEvidenceIds.All(retainedPathEvidenceIds.Contains);
                var supportedLegacyPath = pathProvenanceAvailable ? legacyPath : null;
                var classification = handler is null
                    ? "handler-unavailable"
                    : supportedLegacyPath is not null ? supportedLegacyPath.Classification
                    : flowFact?.Properties.GetValueOrDefault("flowClassification") ?? "NoBackendEvidence";
                var terminalKind = supportedLegacyPath?.Nodes.LastOrDefault()?.SurfaceKind ?? EmptyToNull(flowFact?.Properties.GetValueOrDefault("terminalSurfaceKind"));
                if (handler is not null && terminalKind is null)
                    AddGeneratedGap(gaps, options.MaxGaps, snapshot, "NoBackendEvidence", "event-chain", binding.FactId, support.Select(fact => fact.FactId));
                chains.Add(new(
                    HashId("chain", [binding.FactId, handler?.FactId ?? "handler-unavailable", supportedLegacyPath?.PathId ?? "no-path"]),
                    binding.Properties.GetValueOrDefault("surfaceIdentity") ?? "surface-unavailable",
                    binding.Properties.GetValueOrDefault("eventSourceIdentity") ?? binding.SourceSymbol ?? "event-source-unavailable",
                    binding.FactId,
                    handler?.Properties.GetValueOrDefault("handlerSymbolId"),
                    handler?.FactId,
                    classification,
                    supportedLegacyPath?.PathId,
                    terminalKind,
                    support.Select(fact => Evidence(fact, gaps, options.MaxGaps, snapshot)).ToArray(),
                    pathProvenanceAvailable ? pathEvidence : [],
                    support.Select(fact => fact.FactId).Concat(supportedLegacyPath?.SupportingFactIds ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    supportedLegacyPath?.SupportingEdgeIds.OrderBy(value => value, StringComparer.Ordinal).ToArray() ?? [],
                    support.Select(fact => fact.RuleId).Concat(supportedLegacyPath?.Edges.Select(edge => edge.RuleId) ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    support.Select(fact => fact.EvidenceTier).Concat(supportedLegacyPath?.Edges.Select(edge => edge.EvidenceTier) ?? []).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    support.Select(fact => fact.Properties.GetValueOrDefault("coverageLabel") ?? UnknownCoverage).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    terminalKind is null && handler is not null
                        ? ["No backend or terminal evidence was composed for this handler in the bounded static snapshot; this is not proof of absence."]
                        : ["The chain is static evidence and does not prove runtime event firing or terminal execution."]));
            }
        }
        if (omittedChainSupport.Count > 0)
        {
            truncated = true;
            AddGeneratedGap(gaps, options.MaxGaps, snapshot, "WebFormsModernizationEventChainLimitReached", "packet", null, omittedChainSupport);
        }

        var retainedPathIds = chains.Select(chain => chain.LegacyPathId).Where(id => id is not null).Cast<string>().ToHashSet(StringComparer.Ordinal);
        foreach (var pathGap in legacyFlow.Gaps.Where(gap =>
                     gap.RuleId?.StartsWith("legacy.webforms.", StringComparison.Ordinal) == true
                     || gap.GapKind.Contains("Truncat", StringComparison.OrdinalIgnoreCase)
                     || gap.EffectiveSupportingFactIds.Any(id => chains.Any(chain => chain.SupportingFactIds.Contains(id, StringComparer.Ordinal)))
                     || (gap.NodeId is not null && retainedPathIds.Contains(gap.NodeId))))
            AddPathGap(gaps, options.MaxGaps, snapshot, pathGap);

        var candidates = BuildCandidates(surfaces, compositionFacts, factsById, options.MaxCandidates, ref truncated, gaps, options.MaxGaps, snapshot);
        if (gaps.Any(gap => gap.Classification == "WebFormsModernizationGapLimitReached")) truncated = true;
        var uniqueGaps = gaps.GroupBy(gap => gap.GapId, StringComparer.Ordinal).Select(group => group.First()).OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray();
        var hasReducedInput = facts.Any(fact => fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            || IsReducedCoverageLabel(fact.Properties.GetValueOrDefault("coverageLabel")));
        var coverage = snapshot.BuildStatus == "Succeeded" && !sourceAnalysisReduced && !truncated && uniqueGaps.Length == 0 && !hasReducedInput
            ? "bounded-static-webforms-modernization"
            : "reduced-static-webforms-modernization";
        var source = new WebFormsModernizationSource(
            HashId("source", [snapshot.Repository, snapshot.ScanId, snapshot.CommitSha]),
            HashId("repository", [snapshot.Repository]),
            snapshot.ScanId,
            snapshot.CommitSha,
            snapshot.AnalysisLevel,
            snapshot.BuildStatus);
        var packetId = HashId("packet", [SchemaVersion, source.SourceId, snapshot.ScanId, snapshot.CommitSha]);
        return new(
            SchemaVersion,
            packetId,
            PacketRuleId,
            ClaimLevel,
            coverage,
            [source],
            new(projects.Length, surfaces.Length, chains.Count, candidates.Count, uniqueGaps.Length, truncated),
            projects,
            surfaces,
            chains.OrderBy(chain => chain.ChainId, StringComparer.Ordinal).ToArray(),
            candidates,
            uniqueGaps,
            Questions,
            PacketLimitations);
    }

    private static IReadOnlyList<WebFormsModernizationSliceCandidate> BuildCandidates(
        IReadOnlyList<WebFormsModernizationSurface> surfaces,
        IReadOnlyList<CodeFact> compositionFacts,
        IReadOnlyDictionary<string, CodeFact> factsById,
        int maxCandidates,
        ref bool truncated,
        List<WebFormsModernizationGap> gaps,
        int maxGaps,
        Snapshot snapshot)
    {
        var surfaceSet = surfaces.Select(surface => surface.SurfaceId).ToHashSet(StringComparer.Ordinal);
        var containingSurfaceByControl = surfaces
            .SelectMany(surface => surface.ControlIds.Select(controlId => (controlId, surface.SurfaceId)))
            .GroupBy(item => item.controlId, StringComparer.Ordinal)
            .Where(group => group.Select(item => item.SurfaceId).Distinct(StringComparer.Ordinal).Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single().SurfaceId, StringComparer.Ordinal);
        var adjacency = surfaces.ToDictionary(surface => surface.SurfaceId, _ => new SortedSet<string>(StringComparer.Ordinal), StringComparer.Ordinal);
        foreach (var fact in compositionFacts)
        {
            var source = SafeIdentity(fact.SourceSymbol);
            var target = SafeIdentity(fact.TargetSymbol);
            if (source is not null && !surfaceSet.Contains(source))
                source = containingSurfaceByControl.GetValueOrDefault(source);
            if (source is null || target is null || !surfaceSet.Contains(source) || !surfaceSet.Contains(target)) continue;
            adjacency[source].Add(target);
            adjacency[target].Add(source);
        }
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<WebFormsModernizationSliceCandidate>();
        foreach (var root in surfaces.Select(surface => surface.SurfaceId).OrderBy(value => value, StringComparer.Ordinal))
        {
            if (!visited.Add(root)) continue;
            var queue = new Queue<string>();
            var component = new SortedSet<string>(StringComparer.Ordinal) { root };
            queue.Enqueue(root);
            while (queue.Count > 0)
            {
                foreach (var next in adjacency[queue.Dequeue()])
                    if (visited.Add(next)) { component.Add(next); queue.Enqueue(next); }
            }
            if (result.Count >= maxCandidates)
            {
                truncated = true;
                var omittedSupport = surfaces.First(surface => surface.SurfaceId == root).SupportingFactIds;
                AddGeneratedGap(gaps, maxGaps, snapshot, "WebFormsModernizationCandidateLimitReached", "packet", null, omittedSupport);
                break;
            }
            var support = surfaces.Where(surface => component.Contains(surface.SurfaceId)).SelectMany(surface => surface.SupportingFactIds)
                .Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
            var evidence = support.Where(factsById.ContainsKey).Select(id => factsById[id]).OrderBy(fact => fact.FactId, StringComparer.Ordinal)
                .Select(fact => Evidence(fact, gaps, maxGaps, snapshot)).ToArray();
            result.Add(new(
                HashId("candidate", component),
                "structural-candidate",
                PacketRuleId,
                WeakestTier(evidence.Select(item => item.EvidenceTier)),
                null,
                true,
                component.ToArray(),
                evidence,
                support,
                evidence.Select(item => item.CoverageLabel).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                ["This grouping reflects only deterministic static surface composition; an owner must name and validate any capability boundary."]));
        }
        return result.OrderBy(candidate => candidate.CandidateId, StringComparer.Ordinal).ToArray();
    }

    private static WebFormsModernizationEvidence Evidence(CodeFact fact, List<WebFormsModernizationGap> gaps, int maxGaps, Snapshot snapshot)
    {
        var coverage = fact.Properties.GetValueOrDefault("coverageLabel");
        if (string.IsNullOrWhiteSpace(coverage))
        {
            coverage = UnknownCoverage;
            AddGeneratedGap(gaps, maxGaps, snapshot, "EvidenceCoverageLabelUnavailable", "fact", fact.FactId, [fact.FactId]);
        }
        var filePath = SafeFilePath(fact.Evidence.FilePath);
        if (filePath == "path-unavailable")
            AddGeneratedGap(gaps, maxGaps, snapshot, "EvidenceFilePathUnavailable", "fact", fact.FactId, [fact.FactId]);
        return new(
            fact.FactId,
            fact.RuleId,
            fact.EvidenceTier,
            coverage,
            fact.CommitSha,
            filePath,
            fact.Evidence.StartLine,
            fact.Evidence.EndLine,
            fact.Evidence.ExtractorId,
            fact.Evidence.ExtractorVersion,
            SplitIds(fact.Properties.GetValueOrDefault("supportingFactIds")),
            SplitIds(fact.Properties.GetValueOrDefault("supportingEdgeIds")),
            Limitations(fact));
    }

    private static IReadOnlyList<WebFormsModernizationPathEvidence> PathEvidence(
        CombinedPath? path,
        Snapshot snapshot,
        List<WebFormsModernizationGap> gaps,
        int maxGaps)
    {
        if (path is null) return [];
        var items = new List<WebFormsModernizationPathEvidence>();
        var pathSupport = path.SupportingFactIds.Concat(path.SupportingEdgeIds).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).Take(256).ToArray();
        AddGeneratedGap(gaps, maxGaps, snapshot, "LegacyPathEvidenceCoverageUnavailable", "legacy-path", path.PathId, pathSupport);
        foreach (var node in path.Nodes.Where(node => node.RuleId is not null && node.EvidenceTier is not null))
        {
            var safePath = node.FilePath is null ? null : SafeFilePath(node.FilePath);
            if (safePath is null or "path-unavailable" || node.StartLine is null or <= 0 || node.EndLine is null || node.EndLine < node.StartLine)
            {
                AddGeneratedGap(gaps, maxGaps, snapshot, "LegacyPathEvidenceProvenanceUnavailable", "path-node", node.NodeId, node.CombinedFactId is null ? [] : [node.CombinedFactId]);
                continue;
            }
            items.Add(new WebFormsModernizationPathEvidence(
                node.CombinedFactId ?? node.NodeId,
                "path-node",
                node.RuleId!,
                node.EvidenceTier!,
                UnknownCoverage,
                node.CommitSha ?? snapshot.CommitSha,
                safePath,
                node.StartLine,
                node.EndLine,
                "CombinedDependencyPathReporter",
                LegacyFlowReportConstants.SchemaVersion,
                node.CombinedFactId is null ? [] : [node.CombinedFactId],
                node.Limitations ?? ["Static legacy-flow node evidence does not prove runtime reachability or execution."]));
        }
        foreach (var edge in path.Edges)
        {
            var safePath = edge.FilePath is null ? null : SafeFilePath(edge.FilePath);
            if (safePath is null or "path-unavailable" || edge.StartLine is null or <= 0 || edge.EndLine is null || edge.EndLine < edge.StartLine)
            {
                AddGeneratedGap(gaps, maxGaps, snapshot, "LegacyPathEvidenceProvenanceUnavailable", "path-edge", edge.EdgeId, edge.SupportingFactIds);
                continue;
            }
            items.Add(new WebFormsModernizationPathEvidence(
            edge.EdgeId,
            "path-edge",
            edge.RuleId,
            edge.EvidenceTier,
            UnknownCoverage,
            snapshot.CommitSha,
            safePath,
            edge.StartLine,
            edge.EndLine,
            "CombinedDependencyPathReporter",
            LegacyFlowReportConstants.SchemaVersion,
            edge.SupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["Static legacy-flow edge evidence does not prove runtime reachability or execution."]));
        }
        return items.OrderBy(item => item.EvidenceId, StringComparer.Ordinal).ToArray();
    }

    private static void AddFactGap(List<WebFormsModernizationGap> gaps, int maxGaps, Snapshot snapshot, CodeFact fact)
    {
        if (gaps.Count >= maxGaps)
        {
            AddGeneratedGap(gaps, maxGaps, snapshot, "WebFormsModernizationGapLimitReached", "packet", null, [fact.FactId]);
            return;
        }
        var evidence = Evidence(fact, gaps, maxGaps, snapshot);
        if (gaps.Count >= maxGaps)
        {
            AddGeneratedGap(gaps, maxGaps, snapshot, "WebFormsModernizationGapLimitReached", "packet", null, [fact.FactId]);
            return;
        }
        gaps.Add(new(
            HashId("gap", [fact.FactId]),
            SafeKind(fact.Properties.GetValueOrDefault("gapKind"), "AnalysisGap"),
            "fact",
            SafeIdentity(fact.TargetSymbol),
            fact.RuleId,
            fact.EvidenceTier,
            evidence.CoverageLabel,
            fact.CommitSha,
            evidence.FilePath,
            evidence.StartLine,
            evidence.EndLine,
            evidence.ExtractorId,
            evidence.ExtractorVersion,
            [fact.FactId],
            evidence.Limitations));
    }

    private static void AddPathGap(List<WebFormsModernizationGap> gaps, int maxGaps, Snapshot snapshot, CombinedPathGap gap)
    {
        if (gaps.Count >= maxGaps)
        {
            AddGeneratedGap(gaps, maxGaps, snapshot, "WebFormsModernizationGapLimitReached", "packet", null, gap.EffectiveSupportingFactIds);
            return;
        }
        gaps.Add(new(
            HashId("gap", [gap.GapId]),
            SafeKind(gap.GapKind, "LegacyFlowGap"),
            "legacy-flow",
            SafeIdentity(gap.NodeId),
            gap.RuleId ?? PacketRuleId,
            gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown,
            UnknownCoverage,
            gap.CommitSha ?? snapshot.CommitSha,
            gap.FilePath is null ? null : SafeFilePath(gap.FilePath),
            gap.StartLine,
            gap.EndLine,
            "CombinedDependencyPathReporter",
            "legacy-flow.v1",
            gap.EffectiveSupportingFactIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            ["The legacy-flow gap preserves incomplete static path coverage and does not prove absence."]));
    }

    private static void AddGeneratedGap(List<WebFormsModernizationGap> gaps, int maxGaps, Snapshot snapshot, string classification, string scopeKind, string? scopeId, IEnumerable<string> support)
    {
        var supporting = support.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        var gapId = HashId("gap", [classification, scopeKind, scopeId ?? "none", .. supporting]);
        if (gaps.Any(gap => gap.GapId == gapId)) return;
        if (gaps.Select(gap => gap.GapId).Distinct(StringComparer.Ordinal).Count() >= maxGaps)
        {
            const string limitClassification = "WebFormsModernizationGapLimitReached";
            if (gaps.Any(gap => gap.Classification == limitClassification)) return;
            var limitSupport = supporting.Take(16).ToArray();
            var limitGap = CreateGeneratedGap(snapshot, limitClassification, "packet", null, limitSupport);
            gaps[^1] = limitGap;
            return;
        }
        gaps.Add(CreateGeneratedGap(snapshot, classification, scopeKind, scopeId, supporting));
    }

    private static WebFormsModernizationGap CreateGeneratedGap(
        Snapshot snapshot,
        string classification,
        string scopeKind,
        string? scopeId,
        IReadOnlyList<string> supporting)
    {
        var gapId = HashId("gap", [classification, scopeKind, scopeId ?? "none", .. supporting]);
        return new(
            gapId,
            classification,
            scopeKind,
            scopeId,
            PacketRuleId,
            EvidenceTiers.Tier4Unknown,
            UnknownCoverage,
            snapshot.CommitSha,
            null,
            null,
            null,
            "WebFormsModernizationPacketReporter",
            "webforms-modernization-packet/1.0.0",
            supporting,
            ["The packet failed closed because required evidence was unavailable or bounded by a deterministic limit."]);
    }

    private static async Task<Snapshot> ReadSnapshotAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(path),
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        }.ToString());
        try
        {
            await connection.OpenAsync(cancellationToken);
            await using (var queryOnly = connection.CreateCommand())
            {
                queryOnly.CommandText = "pragma query_only = on;";
                await queryOnly.ExecuteNonQueryAsync(cancellationToken);
            }
            if (!await TableExistsAsync(connection, "scan_manifest", cancellationToken)
                || !await TableExistsAsync(connection, "facts", cancellationToken)
                || await TableExistsAsync(connection, "index_sources", cancellationToken))
                throw new InvalidDataException("WebFormsModernizationIndexUnsupported");
            await using (var count = connection.CreateCommand())
            {
                count.CommandText = "select count(*) from scan_manifest;";
                if (Convert.ToInt64(await count.ExecuteScalarAsync(cancellationToken)) != 1)
                    throw new InvalidDataException("WebFormsModernizationSnapshotInvalid");
            }
            string repository;
            string scanId;
            string commit;
            string analysis;
            string build;
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = "select scan_id, repo, commit_sha, analysis_level, build_status from scan_manifest limit 1;";
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken)) throw new InvalidDataException("WebFormsModernizationScanIdentityUnavailable");
                scanId = reader.GetString(0);
                repository = reader.GetString(1);
                commit = reader.GetString(2);
                analysis = reader.GetString(3);
                build = reader.GetString(4);
                if (!IsCommitSha(commit))
                    throw new InvalidDataException("WebFormsModernizationCommitIdentityUnavailable");
            }
            var facts = new List<CodeFact>();
            await using (var command = connection.CreateCommand())
            {
                command.CommandText = """
                select fact_id, scan_id, repo, commit_sha, project_path, fact_type, rule_id, evidence_tier,
                       source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                       snippet_hash, extractor_id, extractor_version, properties_json
                from facts where rule_id like 'legacy.webforms.%' order by fact_id;
                """;
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(17)) ?? [];
                    facts.Add(new(
                    reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                    reader.IsDBNull(4) ? null : reader.GetString(4), reader.GetString(5), reader.GetString(6), reader.GetString(7),
                    reader.IsDBNull(8) ? null : reader.GetString(8), reader.IsDBNull(9) ? null : reader.GetString(9), reader.IsDBNull(10) ? null : reader.GetString(10),
                    new(reader.GetString(11), reader.GetInt32(12), reader.GetInt32(13), reader.IsDBNull(14) ? null : reader.GetString(14), reader.GetString(15), reader.GetString(16)),
                        new SortedDictionary<string, string>(properties, StringComparer.Ordinal)));
                }
            }
            if (facts.Any(fact => fact.ScanId != scanId || fact.Repo != repository || fact.CommitSha != commit))
                throw new InvalidDataException("WebFormsModernizationSourceIdentityMismatch");
            return new(repository, scanId, commit, analysis, build, facts);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception exception) when (exception is SqliteException or JsonException or InvalidCastException or FormatException or OverflowException)
        {
            throw new InvalidDataException("WebFormsModernizationIndexUnsupported", exception);
        }
    }

    private static async Task<bool> TableExistsAsync(SqliteConnection connection, string tableName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "select count(*) from sqlite_master where type = 'table' and name = $name;";
        command.Parameters.AddWithValue("$name", tableName);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
    }

    private static string RenderMarkdown(WebFormsModernizationPacket packet)
    {
        var b = new StringBuilder();
        b.AppendLine("# TraceMap Web Forms Modernization Evidence Packet").AppendLine();
        b.AppendLine($"- Schema: `{packet.SchemaVersion}`");
        b.AppendLine($"- Packet: `{packet.PacketId}`");
        b.AppendLine($"- Rule: `{packet.RuleId}`");
        b.AppendLine($"- Claim level: `{packet.ClaimLevel}`");
        b.AppendLine($"- Coverage: `{packet.Coverage}`");
        b.AppendLine($"- Repository: `{packet.Sources.Single().RepositoryId}`");
        b.AppendLine($"- Commit: `{packet.Sources.Single().CommitSha}`");
        b.AppendLine($"- Surfaces: `{packet.Summary.SurfaceCount}`; event chains: `{packet.Summary.EventChainCount}`; structural candidates: `{packet.Summary.StructuralSliceCandidateCount}`; gaps: `{packet.Summary.GapCount}`; truncated: `{packet.Summary.Truncated}`.").AppendLine();
        b.AppendLine("## Surfaces").AppendLine();
        if (packet.Surfaces.Count == 0) b.AppendLine("- No supported Web Forms surfaces were available; see gaps.");
        foreach (var surface in packet.Surfaces)
            b.AppendLine($"- `{surface.SurfaceId}` — `{surface.SurfaceKind}`, project `{surface.ProjectId}`, fact `{surface.Evidence.FactId}`, rule `{surface.Evidence.RuleId}`, tier `{surface.Evidence.EvidenceTier}`, coverage `{surface.Evidence.CoverageLabel}`, span `{surface.Evidence.FilePath}:{surface.Evidence.StartLine}-{surface.Evidence.EndLine}`, extractor `{surface.Evidence.ExtractorId}/{surface.Evidence.ExtractorVersion}`.");
        b.AppendLine().AppendLine("## Event chains").AppendLine();
        if (packet.EventChains.Count == 0) b.AppendLine("- No supported static event chains were composed; this is not proof of absence.");
        foreach (var chain in packet.EventChains)
            b.AppendLine($"- `{chain.ChainId}` — `{chain.EventSourceId}` -> `{chain.HandlerId ?? "handler-unavailable"}` -> `{chain.TerminalKind ?? "terminal-unavailable"}`; classification `{chain.Classification}`; supporting facts {string.Join(", ", chain.SupportingFactIds.Select(id => $"`{id}`"))}.");
        b.AppendLine().AppendLine("## Structural slice candidates").AppendLine();
        foreach (var candidate in packet.StructuralSliceCandidates)
            b.AppendLine($"- `{candidate.CandidateId}` — classification `{candidate.Classification}`, owner naming required `{candidate.OwnerNamingRequired}`, surfaces {string.Join(", ", candidate.SurfaceIds.Select(id => $"`{id}`"))}.");
        b.AppendLine().AppendLine("## Gaps").AppendLine();
        if (packet.Gaps.Count == 0) b.AppendLine("- No packet gaps were emitted within the bounded inputs; this is not a completeness claim.");
        foreach (var gap in packet.Gaps)
            b.AppendLine($"- `{gap.GapId}` — `{gap.Classification}`, rule `{gap.RuleId}`, tier `{gap.EvidenceTier}`, coverage `{gap.CoverageLabel}`, supporting facts {string.Join(", ", gap.SupportingFactIds.Select(id => $"`{id}`"))}.");
        b.AppendLine().AppendLine("## Owner questions").AppendLine();
        foreach (var question in packet.OwnerQuestions) b.AppendLine($"- {question}");
        b.AppendLine().AppendLine("## Limitations and non-claims").AppendLine();
        foreach (var limitation in packet.Limitations) b.AppendLine($"- {limitation}");
        return b.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
    }

    private static void Validate(WebFormsModernizationOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.IndexPath)) throw new ArgumentException("webforms-modernization requires --index <index.sqlite>.");
        if (string.IsNullOrWhiteSpace(options.OutputDirectory)) throw new ArgumentException("webforms-modernization requires --out <directory>.");
        if (!File.Exists(options.IndexPath)) throw new FileNotFoundException("WebFormsModernizationIndexUnavailable");
        if (options.MaxSurfaces <= 0 || options.MaxEventChains <= 0 || options.MaxCandidates <= 0 || options.MaxGaps <= 0 || options.MaxDepth <= 0 || options.MaxPaths <= 0)
            throw new ArgumentOutOfRangeException(nameof(options), "Web Forms modernization bounds must be positive.");
    }

    private static string SurfaceIdentity(CodeFact fact) =>
        fact.Properties.GetValueOrDefault("surfaceIdentity") ?? fact.SourceSymbol ?? HashId("surface", [fact.FactId]);
    private static string ProjectId(string? projectPath) => projectPath is null ? "project-unassigned" : HashId("project", [projectPath]);
    private static string? SafeIdentity(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Length <= 256 ? value : HashId("identity", [value]);
    private static string SafeKind(string? value, string fallback) => string.IsNullOrWhiteSpace(value) || value.Length > 96 ? fallback : value;
    private static string SafeFilePath(string path) => Path.IsPathRooted(path) || path.Contains("..", StringComparison.Ordinal) ? "path-unavailable" : path.Replace('\\', '/');
    private static string? EmptyToNull(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool IsReducedCoverageLabel(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Contains("reduced", StringComparison.OrdinalIgnoreCase)
        || value.Contains("partial", StringComparison.OrdinalIgnoreCase)
        || value.Contains("unknown", StringComparison.OrdinalIgnoreCase)
        || value.Contains("unavailable", StringComparison.OrdinalIgnoreCase);
    private static bool IsReducedAnalysisLevel(string? value) => string.IsNullOrWhiteSpace(value)
        || value.Contains("reduced", StringComparison.OrdinalIgnoreCase)
        || value.Contains("partial", StringComparison.OrdinalIgnoreCase)
        || value.Contains("failed", StringComparison.OrdinalIgnoreCase);
    private static bool IsCommitSha(string? value) => value is { Length: 40 or 64 }
        && value.All(Uri.IsHexDigit);
    private static bool HasRequiredProvenance(CodeFact fact) =>
        !string.IsNullOrWhiteSpace(fact.RuleId)
        && !string.IsNullOrWhiteSpace(fact.EvidenceTier)
        && !string.IsNullOrWhiteSpace(fact.CommitSha)
        && !string.IsNullOrWhiteSpace(fact.Evidence.ExtractorId)
        && !string.IsNullOrWhiteSpace(fact.Evidence.ExtractorVersion)
        && fact.Evidence.StartLine > 0
        && fact.Evidence.EndLine >= fact.Evidence.StartLine;
    private static IReadOnlyList<string> SplitIds(string? value) => string.IsNullOrWhiteSpace(value)
        ? [] : value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Distinct(StringComparer.Ordinal).OrderBy(item => item, StringComparer.Ordinal).ToArray();
    private static IReadOnlyList<string> Limitations(CodeFact fact)
    {
        var value = fact.Properties.GetValueOrDefault("ruleLimitations") ?? fact.Properties.GetValueOrDefault("limitations");
        return string.IsNullOrWhiteSpace(value) ? ["No fact-specific limitation text was persisted; consult the rule catalog."] : [value];
    }
    private static string WeakestTier(IEnumerable<string> tiers)
    {
        var values = tiers.ToArray();
        if (values.Length == 0 || values.Contains(EvidenceTiers.Tier4Unknown, StringComparer.Ordinal)) return EvidenceTiers.Tier4Unknown;
        if (values.Contains(EvidenceTiers.Tier3SyntaxOrTextual, StringComparer.Ordinal)) return EvidenceTiers.Tier3SyntaxOrTextual;
        if (values.Contains(EvidenceTiers.Tier2Structural, StringComparer.Ordinal)) return EvidenceTiers.Tier2Structural;
        return EvidenceTiers.Tier1Semantic;
    }
    private static string HashId(string kind, IEnumerable<string> values)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\0", values.Prepend($"webforms-modernization/{kind}/v1"))));
        return $"{kind}-{Convert.ToHexString(bytes).ToLowerInvariant()[..24]}";
    }

    internal sealed record Snapshot(
        string Repository,
        string ScanId,
        string CommitSha,
        string AnalysisLevel,
        string BuildStatus,
        IReadOnlyList<CodeFact> Facts);
}
