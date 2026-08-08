namespace TraceMap.Core;

public sealed record ReverseImpactOptions(
    string Selector,
    int MaxDepth,
    IReadOnlyList<string>? RelationshipFilters = null,
    bool IncludeContainedMembers = true);

public sealed record ReverseImpactResult(
    string SchemaVersion,
    string Resolution,
    string TraversalRuleId,
    string Selector,
    int MaxDepth,
    IReadOnlyList<string> RelationshipFilters,
    ReverseImpactSnapshot? Snapshot,
    ReverseImpactSymbol? Seed,
    IReadOnlyList<ReverseImpactSymbol> Candidates,
    IReadOnlyList<ReverseImpactSymbol> TraversalSeeds,
    IReadOnlyList<ReverseImpactItem> Impacts,
    IReadOnlyList<ReverseImpactGap> Gaps,
    IReadOnlyList<string> Limitations);

public static class ReverseImpactContract
{
    public const string SchemaVersion = "tracemap.reverse-impact.v1";

    public static IReadOnlyList<string> SupportedResolutions { get; } = Array.AsReadOnly(
    [
        ReverseImpactResolutions.Resolved,
        ReverseImpactResolutions.NotFound,
        ReverseImpactResolutions.Ambiguous
    ]);

    public static IReadOnlyList<string> SupportedGapKinds { get; } = Array.AsReadOnly(
    [
        ReverseImpactGapKinds.AmbiguousSelector,
        ReverseImpactGapKinds.AnalysisGap,
        ReverseImpactGapKinds.AnalysisGapMissingEvidence,
        ReverseImpactGapKinds.RelationshipMissingCanonicalIdentity,
        ReverseImpactGapKinds.RelationshipMissingEvidence,
        ReverseImpactGapKinds.SelectorNotFound
    ]);

    public static bool IsSupportedSchema(string? schemaVersion) =>
        string.Equals(schemaVersion, SchemaVersion, StringComparison.Ordinal);
}

public static class ReverseImpactResolutions
{
    public const string Resolved = "Resolved";
    public const string NotFound = "NotFound";
    public const string Ambiguous = "Ambiguous";
}

public static class ReverseImpactGapKinds
{
    public const string AmbiguousSelector = "AmbiguousSelector";
    public const string AnalysisGap = "AnalysisGap";
    public const string AnalysisGapMissingEvidence = "AnalysisGapMissingEvidence";
    public const string RelationshipMissingCanonicalIdentity = "RelationshipMissingCanonicalIdentity";
    public const string RelationshipMissingEvidence = "RelationshipMissingEvidence";
    public const string SelectorNotFound = "SelectorNotFound";
}

public sealed record ReverseImpactSnapshot(
    string ScanId,
    string Repo,
    string CommitSha);

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
    string RuleId,
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

public sealed class ReverseImpactInputException : ArgumentException
{
    public ReverseImpactInputException(string errorCode, string message, string? parameterName = null)
        : base(message, parameterName)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

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
    private static readonly string[] SymbolRoles = ["source", "target", "argument", "parameter", "origin", "constructor"];
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

        var unvalidatedFacts = inputFacts.Cast<CodeFact?>().ToArray();
        ValidateFacts(unvalidatedFacts);
        var facts = unvalidatedFacts
            .Cast<CodeFact>()
            .OrderBy(fact => fact.FactId, StringComparer.Ordinal)
            .ToArray();
        var filters = NormalizeFilters(options.RelationshipFilters);
        var selector = options.Selector.Trim();
        var snapshot = Snapshot(facts);
        var symbols = BuildSymbolInventory(facts);
        var resolution = ResolveSelector(selector, symbols);
        if (resolution.Seed is null)
        {
            var resolutionKind = resolution.Candidates.Count == 0 ? ReverseImpactResolutions.NotFound : ReverseImpactResolutions.Ambiguous;
            var gapKind = resolutionKind == ReverseImpactResolutions.NotFound ? ReverseImpactGapKinds.SelectorNotFound : ReverseImpactGapKinds.AmbiguousSelector;
            var message = resolutionKind == ReverseImpactResolutions.NotFound
                ? $"Selector `{selector}` did not exactly match a canonical symbol id or display name."
                : $"Selector `{selector}` matched {resolution.Candidates.Count} canonical symbols; traversal was not performed.";
            var candidateIds = resolution.Candidates.Select(candidate => candidate.SymbolId).ToHashSet(StringComparer.Ordinal);
            var applicableGaps = ApplicableAnalysisGaps(
                facts,
                candidateIds,
                includeUnscoped: resolutionKind == ReverseImpactResolutions.NotFound);
            return new ReverseImpactResult(
                ReverseImpactContract.SchemaVersion,
                resolutionKind,
                TraversalRuleId,
                selector,
                options.MaxDepth,
                filters,
                snapshot,
                null,
                resolution.Candidates,
                [],
                [],
                applicableGaps
                    .Append(CreateDerivedGap(gapKind, message, resolution.Candidates.Select(candidate => candidate.SymbolId)))
                    .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                    .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                    .ToArray(),
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

        var relationshipGaps = new List<ReverseImpactGap>();
        var edges = BuildEdges(facts, filters, relationshipGaps);
        var incoming = edges
            .GroupBy(edge => edge.TargetSymbolId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(EdgeSortKey, StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal);
        var startIds = traversalSeeds.Select(symbol => symbol.SymbolId).ToHashSet(StringComparer.Ordinal);
        var visitedSymbols = new HashSet<string>(startIds, StringComparer.Ordinal);
        var visitedStates = new HashSet<(string TraversalSeedSymbolId, string SymbolId)>();
        foreach (var traversalSeed in traversalSeeds)
        {
            visitedStates.Add((traversalSeed.SymbolId, traversalSeed.SymbolId));
        }

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
                if (!visitedStates.Add((state.TraversalSeedSymbolId, edge.SourceSymbolId)))
                {
                    continue;
                }

                visitedSymbols.Add(edge.SourceSymbolId);
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

        var gaps = ApplicableAnalysisGaps(facts, visitedSymbols, includeUnscoped: false)
            .Concat(relationshipGaps.Where(gap =>
                gap.RelatedSymbolIds.Any(visitedSymbols.Contains)
                || (gap.GapKind == ReverseImpactGapKinds.RelationshipMissingCanonicalIdentity && gap.RelatedSymbolIds.Count == 0)))
            .ToArray();

        return new ReverseImpactResult(
            ReverseImpactContract.SchemaVersion,
            ReverseImpactResolutions.Resolved,
            TraversalRuleId,
            selector,
            options.MaxDepth,
            filters,
            snapshot,
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
        if (requested?.Any(value => value is null) == true)
        {
            throw new ReverseImpactInputException(
                "InvalidRelationshipFilter",
                "Reverse-impact relationship filters cannot contain null values.",
                nameof(ReverseImpactOptions.RelationshipFilters));
        }

        var filters = requested is null || requested.Count == 0
            ? DefaultFilters
            : requested.Select(value => value.Trim().ToLowerInvariant()).ToArray();
        var unknown = filters.Where(value => !DefaultFilters.Contains(value, StringComparer.Ordinal)).Distinct(StringComparer.Ordinal).ToArray();
        if (unknown.Length > 0)
        {
            throw new ArgumentException($"Unsupported reverse-impact relationship filter(s): {string.Join(", ", unknown)}.");
        }

        return Array.AsReadOnly(filters
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray());
    }

    private static void ValidateFacts(IReadOnlyList<CodeFact?> facts)
    {
        for (var index = 0; index < facts.Count; index++)
        {
            var fact = facts[index];
            if (fact is null)
            {
                throw new ReverseImpactInputException(
                    "NullFact",
                    $"Reverse-impact input fact at index {index} is null.",
                    "inputFacts");
            }

            ValidateRequired(fact.FactId, "FactId", fact.FactId, index);
            ValidateRequired(fact.ScanId, "ScanId", fact.FactId, index);
            ValidateRequired(fact.Repo, "Repo", fact.FactId, index);
            ValidateRequired(fact.CommitSha, "CommitSha", fact.FactId, index);
            ValidateRequired(fact.FactType, "FactType", fact.FactId, index);
            ValidateRequired(fact.RuleId, "RuleId", fact.FactId, index);
            ValidateRequired(fact.EvidenceTier, "EvidenceTier", fact.FactId, index);
            if (fact.Properties is null)
            {
                throw new ReverseImpactInputException(
                    "NullFactProperties",
                    $"Reverse-impact input fact `{fact.FactId}` at index {index} has null Properties.",
                    "inputFacts");
            }

            foreach (var property in fact.Properties)
            {
                if (string.IsNullOrWhiteSpace(property.Key) || property.Value is null)
                {
                    throw new ReverseImpactInputException(
                        "InvalidFactProperty",
                        $"Reverse-impact input fact `{fact.FactId}` at index {index} has a null/blank property key or null value.",
                        "inputFacts");
                }
            }

            foreach (var role in SymbolRoles)
            {
                var propertyName = $"{role}SymbolId";
                if (fact.Properties.TryGetValue(propertyName, out var symbolId) && string.IsNullOrWhiteSpace(symbolId))
                {
                    throw new ReverseImpactInputException(
                        "InvalidCanonicalEndpoint",
                        $"Reverse-impact input fact `{fact.FactId}` at index {index} has a blank `{propertyName}` value.",
                        "inputFacts");
                }
            }

            if (fact.Evidence is not null)
            {
                ValidateRequired(fact.Evidence.FilePath, "Evidence.FilePath", fact.FactId, index);
                ValidateRequired(fact.Evidence.ExtractorId, "Evidence.ExtractorId", fact.FactId, index);
                ValidateRequired(fact.Evidence.ExtractorVersion, "Evidence.ExtractorVersion", fact.FactId, index);
                if (fact.Evidence.StartLine < 1 || fact.Evidence.EndLine < 1 || fact.Evidence.EndLine < fact.Evidence.StartLine)
                {
                    throw new ReverseImpactInputException(
                        "InvalidEvidenceSpan",
                        $"Reverse-impact input fact `{fact.FactId}` at index {index} has an invalid evidence line span.",
                        "inputFacts");
                }
            }
        }

        var snapshots = facts
            .Cast<CodeFact>()
            .Select(fact => (fact.ScanId, fact.Repo, fact.CommitSha))
            .Distinct()
            .OrderBy(snapshot => snapshot.ScanId, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.Repo, StringComparer.Ordinal)
            .ThenBy(snapshot => snapshot.CommitSha, StringComparer.Ordinal)
            .ToArray();
        var duplicateFactId = facts
            .Cast<CodeFact>()
            .GroupBy(fact => fact.FactId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault();
        if (duplicateFactId is not null)
        {
            throw new ReverseImpactInputException(
                "DuplicateFactId",
                $"Reverse-impact input contains duplicate fact id `{duplicateFactId}`.",
                "inputFacts");
        }

        if (snapshots.Length > 1)
        {
            throw new ReverseImpactInputException(
                "MixedSnapshot",
                $"Reverse-impact input contains {snapshots.Length} distinct (ScanId, Repo, CommitSha) snapshots; supply exactly one snapshot.",
                "inputFacts");
        }
    }

    private static void ValidateRequired(string? value, string field, string? factId, int index)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ReverseImpactInputException(
                "MissingRequiredFactField",
                $"Reverse-impact input fact `{factId ?? $"index:{index}"}` at index {index} has a null/blank `{field}` value.",
                "inputFacts");
        }
    }

    private static ReverseImpactSnapshot? Snapshot(IReadOnlyList<CodeFact> facts) => facts.Count == 0
        ? null
        : new ReverseImpactSnapshot(facts[0].ScanId, facts[0].Repo, facts[0].CommitSha);

    private static IReadOnlyList<ReverseImpactGap> ApplicableAnalysisGaps(
        IReadOnlyList<CodeFact> facts,
        IReadOnlySet<string> applicableSymbolIds,
        bool includeUnscoped)
    {
        var gaps = new List<ReverseImpactGap>();
        foreach (var fact in facts.Where(fact => fact.FactType == FactTypes.AnalysisGap))
        {
            var relatedIds = RelatedSymbolIds(fact);
            if (!includeUnscoped && !relatedIds.Any(applicableSymbolIds.Contains))
            {
                continue;
            }

            gaps.Add(fact.Evidence is null
                ? CreateDerivedGap(
                    ReverseImpactGapKinds.AnalysisGapMissingEvidence,
                    $"Analysis-gap fact `{fact.FactId}` has missing evidence provenance and cannot be represented as ordinary gap evidence.",
                    relatedIds,
                    fact)
                : FromAnalysisGap(fact, relatedIds));
        }

        return gaps
            .DistinctBy(gap => gap.GapId, StringComparer.Ordinal)
            .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
            .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
            .ToArray();
    }

    private static Dictionary<string, ReverseImpactSymbol> BuildSymbolInventory(IReadOnlyList<CodeFact> facts)
    {
        var occurrences = new Dictionary<string, List<ReverseImpactSymbol>>(StringComparer.Ordinal);
        foreach (var fact in facts)
        {
            foreach (var role in SymbolRoles)
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
        var displayName = occurrences
            .Select(symbol => symbol.DisplayName)
            .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, symbolId, StringComparison.Ordinal))
            .OrderBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? symbolId;
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
            displayName,
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
                    ReverseImpactGapKinds.RelationshipMissingCanonicalIdentity,
                    $"Impact-relevant fact `{fact.FactId}` was excluded because both canonical relationship endpoints were not present.",
                    RelatedSymbolIds(fact),
                    fact));
                continue;
            }

            if (fact.Evidence is null)
            {
                gaps.Add(CreateDerivedGap(
                    ReverseImpactGapKinds.RelationshipMissingEvidence,
                    $"Impact-relevant fact `{fact.FactId}` was excluded because its relationship-site evidence was missing.",
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
        ToEvidence(edge.Fact.Evidence, edge.Fact.RuleId));

    private static ReverseImpactGap FromAnalysisGap(CodeFact fact, IReadOnlyList<string> relatedIds) => new(
        fact.FactId,
        ReverseImpactGapKinds.AnalysisGap,
        fact.RuleId,
        fact.EvidenceTier,
        Property(fact, "message") ?? fact.ContractElement ?? "Analysis is incomplete for this scan.",
        fact.ScanId,
        fact.Repo,
        fact.CommitSha,
        fact.ProjectPath,
        relatedIds,
        ToEvidence(fact.Evidence, fact.RuleId));

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
            ToEvidence(sourceFact?.Evidence, GapRuleId));
    }

    private static IReadOnlyList<string> RelatedSymbolIds(CodeFact fact) =>
        SymbolRoles
            .Select(role => Property(fact, $"{role}SymbolId"))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static ReverseImpactEvidence ToEvidence(EvidenceSpan? evidence, string ruleId) => new(
        ruleId,
        NullIfEmpty(evidence?.FilePath),
        evidence is null ? null : evidence.StartLine,
        evidence is null ? null : evidence.EndLine,
        NullIfEmpty(evidence?.SnippetHash),
        NullIfEmpty(evidence?.ExtractorId),
        NullIfEmpty(evidence?.ExtractorVersion));

    private static string EdgeSortKey(ImpactEdge edge) =>
        $"{edge.SourceSymbolId}\u001f{edge.RelationshipFilter}\u001f{edge.RelationshipKind}\u001f{edge.TargetSymbolId}\u001f{edge.Fact.Evidence?.FilePath}\u001f{edge.Fact.Evidence?.StartLine:D10}\u001f{edge.Fact.FactId}";

    private static string PathId(string traversalSeed, IReadOnlyList<ImpactEdge> path) =>
        $"impact-path:{FactFactory.Hash($"{path[0].Fact.ScanId}\u001f{path[0].Fact.Repo}\u001f{path[0].Fact.CommitSha}\u001f{traversalSeed}\u001f{string.Join(";", path.Select(edge => edge.Fact.FactId))}", 32)}";

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
        "A query accepts facts from exactly one scan, repository, and commit snapshot; mixed snapshots fail closed before selector resolution or graph construction.",
        "Resolved queries include only gaps tied by canonical identity to the seed or visited path. Unscoped analysis gaps are retained only when a selector is not found because reduced coverage may explain the missing seed.",
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
