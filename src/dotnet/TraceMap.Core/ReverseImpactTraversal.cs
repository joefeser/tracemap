namespace TraceMap.Core;

public sealed record ReverseImpactOptions(
    string Selector,
    int MaxDepth,
    IReadOnlyList<string>? RelationshipFilters = null,
    bool IncludeContainedMembers = true);

public sealed record ReverseImpactResult(
    string Resolution,
    string TraversalRuleId,
    string Selector,
    int MaxDepth,
    IReadOnlyList<string> RelationshipFilters,
    ReverseImpactSymbol? Seed,
    IReadOnlyList<ReverseImpactSymbol> Candidates,
    IReadOnlyList<ReverseImpactSymbol> TraversalSeeds,
    IReadOnlyList<ReverseImpactItem> Impacts,
    IReadOnlyList<ReverseImpactGap> Gaps,
    IReadOnlyList<string> Limitations);

public sealed record ReverseImpactSymbol(
    string SymbolId,
    string DisplayName,
    string SymbolKind,
    string Language,
    string? AssemblyName,
    string? AssemblyVersion,
    string? ContainingSymbolId);

public sealed record ReverseImpactItem(
    ReverseImpactSymbol Symbol,
    int Depth,
    bool IsDirect,
    string PathId,
    string TraversalSeedSymbolId,
    IReadOnlyList<ReverseImpactHop> Path);

public sealed record ReverseImpactHop(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string? ProjectPath,
    string SourceSymbolId,
    string TargetSymbolId,
    string RelationshipKind,
    string RelationshipFilter,
    string OriginalDirection,
    string TraversalDirection,
    string RuleId,
    string EvidenceTier,
    ReverseImpactEvidence Evidence);

public sealed record ReverseImpactEvidence(
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? SnippetHash,
    string? ExtractorId,
    string? ExtractorVersion);

public sealed record ReverseImpactGap(
    string GapId,
    string GapKind,
    string RuleId,
    string EvidenceTier,
    string Message,
    string? ScanId,
    string? Repo,
    string? CommitSha,
    string? ProjectPath,
    IReadOnlyList<string> RelatedSymbolIds,
    ReverseImpactEvidence Evidence);

/// <summary>
/// Performs deterministic, bounded reverse traversal over canonical relationships already present in facts.
/// It does not extract or infer new relationships.
/// </summary>
public static class ReverseImpactTraversal
{
    public const string TraversalRuleId = RuleIds.ReverseImpactTraversal;
    public const string GapRuleId = RuleIds.ReverseImpactGap;

    private const string Calls = "calls";
    private const string References = "references";
    private const string Inheritance = "inheritance";

    private static readonly string[] DefaultFilters = [Calls, Inheritance, References];
    private static readonly HashSet<string> ImpactRelationshipKinds = new(StringComparer.Ordinal)
    {
        "ExtendsInterface",
        "ImplementsInterface",
        "ImplementsInterfaceMember",
        "InheritsFrom",
        "Overrides"
    };
    private static readonly HashSet<string> MemberKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "Event",
        "Field",
        "Method",
        "Property"
    };

    public static ReverseImpactResult Analyze(IEnumerable<CodeFact> inputFacts, ReverseImpactOptions options)
    {
        ArgumentNullException.ThrowIfNull(inputFacts);
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(options.Selector))
        {
            throw new ArgumentException("Reverse impact requires a canonical symbol id or exact display-name selector.", nameof(options));
        }

        if (options.MaxDepth is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "Reverse impact depth must be between 1 and 20.");
        }

        var facts = inputFacts
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var filters = NormalizeFilters(options.RelationshipFilters);
        var symbols = BuildSymbolInventory(facts);
        var resolution = ResolveSelector(options.Selector.Trim(), symbols);
        if (resolution.Seed is null)
        {
            var resolutionKind = resolution.Candidates.Count == 0 ? "NotFound" : "Ambiguous";
            var gapKind = resolutionKind == "NotFound" ? "SelectorNotFound" : "AmbiguousSelector";
            var message = resolutionKind == "NotFound"
                ? $"Selector `{options.Selector.Trim()}` did not exactly match a canonical symbol id or display name."
                : $"Selector `{options.Selector.Trim()}` matched {resolution.Candidates.Count} canonical symbols; traversal was not performed.";
            return new ReverseImpactResult(
                resolutionKind,
                TraversalRuleId,
                options.Selector.Trim(),
                options.MaxDepth,
                filters,
                null,
                resolution.Candidates,
                [],
                [],
                [CreateDerivedGap(gapKind, message, resolution.Candidates.Select(candidate => candidate.SymbolId))],
                Limitations());
        }

        var seed = resolution.Seed;
        var traversalSeeds = new List<ReverseImpactSymbol> { seed };
        if (options.IncludeContainedMembers && string.Equals(seed.SymbolKind, "NamedType", StringComparison.OrdinalIgnoreCase))
        {
            traversalSeeds.AddRange(symbols.Values
                .Where(symbol => string.Equals(symbol.ContainingSymbolId, seed.SymbolId, StringComparison.Ordinal)
                    && MemberKinds.Contains(symbol.SymbolKind))
                .OrderBy(symbol => symbol.SymbolId, StringComparer.Ordinal));
        }

        traversalSeeds = traversalSeeds
            .DistinctBy(symbol => symbol.SymbolId, StringComparer.Ordinal)
            .OrderBy(symbol => symbol.SymbolId, StringComparer.Ordinal)
            .ToList();

        var gaps = new List<ReverseImpactGap>();
        var edges = BuildEdges(facts, filters, gaps);
        var incoming = edges
            .GroupBy(edge => edge.TargetSymbolId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(EdgeSortKey, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var startIds = traversalSeeds.Select(symbol => symbol.SymbolId).ToHashSet(StringComparer.Ordinal);
        var visited = new HashSet<string>(startIds, StringComparer.Ordinal);
        var frontier = new Queue<TraversalState>(traversalSeeds.Select(symbol => new TraversalState(symbol.SymbolId, symbol.SymbolId, [])));
        var impacts = new List<ReverseImpactItem>();

        while (frontier.Count > 0)
        {
            var state = frontier.Dequeue();
            if (state.Path.Count >= options.MaxDepth || !incoming.TryGetValue(state.SymbolId, out var candidates))
            {
                continue;
            }

            foreach (var edge in candidates)
            {
                if (!visited.Add(edge.SourceSymbolId))
                {
                    continue;
                }

                var path = state.Path.Append(edge).ToArray();
                var symbol = symbols.GetValueOrDefault(edge.SourceSymbolId) ?? UnknownSymbol(edge.SourceSymbolId);
                impacts.Add(new ReverseImpactItem(
                    symbol,
                    path.Length,
                    path.Length == 1,
                    PathId(state.TraversalSeedSymbolId, path),
                    state.TraversalSeedSymbolId,
                    path.Select(ToHop).ToArray()));
                frontier.Enqueue(new TraversalState(edge.SourceSymbolId, state.TraversalSeedSymbolId, path));
            }
        }

        var visitedIds = visited.ToHashSet(StringComparer.Ordinal);
        gaps.AddRange(facts
            .Where(fact => fact.FactType == FactTypes.AnalysisGap)
            .Select(fact => FromAnalysisGap(fact, RelatedSymbolIds(fact)))
            .Where(gap => gap.RelatedSymbolIds.Count == 0 || gap.RelatedSymbolIds.Any(visitedIds.Contains)));

        return new ReverseImpactResult(
            "Resolved",
            TraversalRuleId,
            options.Selector.Trim(),
            options.MaxDepth,
            filters,
            seed,
            [],
            traversalSeeds,
            impacts
                .OrderBy(impact => impact.Depth)
                .ThenBy(impact => impact.Symbol.SymbolId, StringComparer.Ordinal)
                .ThenBy(impact => impact.PathId, StringComparer.Ordinal)
                .ToArray(),
            gaps
                .DistinctBy(gap => gap.GapId, StringComparer.Ordinal)
                .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                .ToArray(),
            Limitations());
    }

    private static IReadOnlyList<string> NormalizeFilters(IReadOnlyList<string>? requested)
    {
        var filters = requested is null || requested.Count == 0
            ? DefaultFilters
            : requested.Select(value => value.Trim().ToLowerInvariant()).ToArray();
        var unknown = filters.Where(value => !DefaultFilters.Contains(value, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException($"Unsupported reverse-impact relationship filter(s): {string.Join(", ", unknown)}.");
        }

        return filters.Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    }

    private static Dictionary<string, ReverseImpactSymbol> BuildSymbolInventory(IReadOnlyList<CodeFact> facts)
    {
        var occurrences = new Dictionary<string, List<ReverseImpactSymbol>>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            foreach (var role in new[] { "source", "target", "argument", "parameter", "origin", "constructor" })
            {
                var id = Property(fact, $"{role}SymbolId");
                if (string.IsNullOrWhiteSpace(id))
                {
                    continue;
                }

                var candidate = new ReverseImpactSymbol(
                    id,
                    Property(fact, $"{role}SymbolDisplayName") ?? (role == "source" ? fact.SourceSymbol : fact.TargetSymbol) ?? id,
                    Property(fact, $"{role}SymbolKind") ?? "Unknown",
                    Property(fact, $"{role}SymbolLanguage") ?? "unknown",
                    NullIfEmpty(Property(fact, $"{role}SymbolAssemblyName")),
                    NullIfEmpty(Property(fact, $"{role}SymbolAssemblyVersion")),
                    NullIfEmpty(Property(fact, $"{role}ContainingSymbolId")));
                if (!occurrences.TryGetValue(id, out var candidates))
                {
                    candidates = [];
                    occurrences[id] = candidates;
                }

                candidates.Add(candidate);

                if (!string.IsNullOrWhiteSpace(candidate.ContainingSymbolId))
                {
                    var containingKind = MemberKinds.Contains(candidate.SymbolKind) ? "NamedType" : "Unknown";
                    if (!occurrences.TryGetValue(candidate.ContainingSymbolId, out var containingCandidates))
                    {
                        containingCandidates = [];
                        occurrences[candidate.ContainingSymbolId] = containingCandidates;
                    }

                    containingCandidates.Add(new ReverseImpactSymbol(
                        candidate.ContainingSymbolId,
                        candidate.ContainingSymbolId,
                        containingKind,
                        candidate.Language,
                        candidate.AssemblyName,
                        candidate.AssemblyVersion,
                        null));
                }
            }
        }

        return occurrences.ToDictionary(
            pair => pair.Key,
            pair => MergeSymbolOccurrences(pair.Key, pair.Value),
            StringComparer.Ordinal);
    }

    private static ReverseImpactSymbol MergeSymbolOccurrences(string symbolId, IReadOnlyList<ReverseImpactSymbol> occurrences)
    {
        string Required(Func<ReverseImpactSymbol, string> selector, string fallback) => occurrences
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "Unknown", StringComparison.OrdinalIgnoreCase) && !string.Equals(value, "unknown", StringComparison.OrdinalIgnoreCase))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? fallback;
        string? Optional(Func<ReverseImpactSymbol, string?> selector) => occurrences
            .Select(selector)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();

        return new ReverseImpactSymbol(
            symbolId,
            Required(symbol => symbol.DisplayName, symbolId),
            Required(symbol => symbol.SymbolKind, "Unknown"),
            Required(symbol => symbol.Language, "unknown"),
            Optional(symbol => symbol.AssemblyName),
            Optional(symbol => symbol.AssemblyVersion),
            Optional(symbol => symbol.ContainingSymbolId));
    }

    private static (ReverseImpactSymbol? Seed, IReadOnlyList<ReverseImpactSymbol> Candidates) ResolveSelector(
        string selector,
        IReadOnlyDictionary<string, ReverseImpactSymbol> symbols)
    {
        if (symbols.TryGetValue(selector, out var canonical))
        {
            return (canonical, []);
        }

        var candidates = symbols.Values
            .Where(symbol => string.Equals(symbol.DisplayName, selector, StringComparison.OrdinalIgnoreCase))
            .OrderBy(symbol => symbol.SymbolId, StringComparer.Ordinal)
            .ToArray();
        return candidates.Length == 1 ? (candidates[0], []) : (null, candidates);
    }

    private static IReadOnlyList<ImpactEdge> BuildEdges(
        IReadOnlyList<CodeFact> facts,
        IReadOnlyList<string> filters,
        List<ReverseImpactGap> gaps)
    {
        var edges = new List<ImpactEdge>();
        foreach (var fact in facts)
        {
            var relationship = RelationshipFor(fact);
            if (relationship is null || !filters.Contains(relationship.Value.Filter, StringComparer.Ordinal))
            {
                continue;
            }

            var sourceId = Property(fact, "sourceSymbolId");
            var targetId = Property(fact, "targetSymbolId");
            if (string.IsNullOrWhiteSpace(sourceId) || string.IsNullOrWhiteSpace(targetId))
            {
                gaps.Add(CreateDerivedGap(
                    "RelationshipMissingCanonicalIdentity",
                    $"Impact-relevant fact `{fact.FactId}` was excluded because both canonical relationship endpoints were not present.",
                    RelatedSymbolIds(fact),
                    fact));
                continue;
            }

            edges.Add(new ImpactEdge(fact, sourceId, targetId, relationship.Value.Kind, relationship.Value.Filter));
        }

        return edges
            .DistinctBy(edge => $"{edge.Fact.FactId}\u001f{edge.SourceSymbolId}\u001f{edge.TargetSymbolId}\u001f{edge.RelationshipKind}", StringComparer.Ordinal)
            .OrderBy(EdgeSortKey, StringComparer.Ordinal)
            .ToArray();
    }

    private static (string Kind, string Filter)? RelationshipFor(CodeFact fact)
    {
        if (fact.FactType == FactTypes.CallEdge)
        {
            return ("Calls", Calls);
        }

        if (fact.FactType == FactTypes.PropertyAccessed)
        {
            return ("References", References);
        }

        if (fact.FactType == FactTypes.SymbolRelationship)
        {
            var kind = Property(fact, "relationshipKind");
            return kind is not null && ImpactRelationshipKinds.Contains(kind) ? (kind, Inheritance) : null;
        }

        return null;
    }

    private static ReverseImpactHop ToHop(ImpactEdge edge) => new(
        edge.Fact.FactId,
        edge.Fact.ScanId,
        edge.Fact.Repo,
        edge.Fact.CommitSha,
        edge.Fact.ProjectPath,
        edge.SourceSymbolId,
        edge.TargetSymbolId,
        edge.RelationshipKind,
        edge.RelationshipFilter,
        "SourceToTarget",
        "TargetToSource",
        edge.Fact.RuleId,
        edge.Fact.EvidenceTier,
        ToEvidence(edge.Fact.Evidence));

    private static ReverseImpactGap FromAnalysisGap(CodeFact fact, IReadOnlyList<string> relatedIds) => new(
        fact.FactId,
        "AnalysisGap",
        fact.RuleId,
        fact.EvidenceTier,
        Property(fact, "message") ?? fact.ContractElement ?? "Analysis is incomplete for this scan.",
        fact.ScanId,
        fact.Repo,
        fact.CommitSha,
        fact.ProjectPath,
        relatedIds,
        ToEvidence(fact.Evidence));

    private static ReverseImpactGap CreateDerivedGap(
        string kind,
        string message,
        IEnumerable<string> relatedIds,
        CodeFact? sourceFact = null)
    {
        var ids = relatedIds.Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray();
        return new ReverseImpactGap(
            $"gap:{FactFactory.Hash($"{kind}\u001f{message}\u001f{string.Join(";", ids)}", 24)}",
            kind,
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            message,
            sourceFact?.ScanId,
            sourceFact?.Repo,
            sourceFact?.CommitSha,
            sourceFact?.ProjectPath,
            ids,
            ToEvidence(sourceFact?.Evidence));
    }

    private static IReadOnlyList<string> RelatedSymbolIds(CodeFact fact) =>
        new[] { "source", "target", "argument", "parameter", "origin", "constructor" }
            .Select(role => Property(fact, $"{role}SymbolId"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static ReverseImpactEvidence ToEvidence(EvidenceSpan? evidence) => new(
        NullIfEmpty(evidence?.FilePath),
        evidence is null ? null : evidence.StartLine,
        evidence is null ? null : evidence.EndLine,
        NullIfEmpty(evidence?.SnippetHash),
        NullIfEmpty(evidence?.ExtractorId),
        NullIfEmpty(evidence?.ExtractorVersion));

    private static string EdgeSortKey(ImpactEdge edge) =>
        $"{edge.SourceSymbolId}\u001f{edge.RelationshipFilter}\u001f{edge.RelationshipKind}\u001f{edge.TargetSymbolId}\u001f{edge.Fact.Evidence?.FilePath}\u001f{edge.Fact.Evidence?.StartLine:D10}\u001f{edge.Fact.FactId}";

    private static string PathId(string traversalSeed, IReadOnlyList<ImpactEdge> path) =>
        $"impact-path:{FactFactory.Hash($"{traversalSeed}\u001f{string.Join(";", path.Select(edge => edge.Fact.FactId))}", 32)}";

    private static ReverseImpactSymbol UnknownSymbol(string symbolId) =>
        new(symbolId, symbolId, "Unknown", "unknown", null, null, null);

    private static string? Property(CodeFact fact, string name) =>
        fact.Properties is not null && fact.Properties.TryGetValue(name, out var value) ? value : null;

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static IReadOnlyList<string> Limitations() =>
    [
        "Results include only the explicit impact-relevant relationship families requested by the query; other fact types are not traversed.",
        "Canonical endpoints require existing semantic identity properties. Syntax-only relationships without canonical IDs are reported as gaps and are not name-matched.",
        "Type queries expand only directly contained methods, properties, fields, and events proven by existing containing-symbol identity.",
        "Paths are bounded static evidence chains, not proof of runtime execution, reachability, severity, or completeness."
    ];

    private sealed record ImpactEdge(
        CodeFact Fact,
        string SourceSymbolId,
        string TargetSymbolId,
        string RelationshipKind,
        string RelationshipFilter);

    private sealed record TraversalState(
        string SymbolId,
        string TraversalSeedSymbolId,
        IReadOnlyList<ImpactEdge> Path);
}
