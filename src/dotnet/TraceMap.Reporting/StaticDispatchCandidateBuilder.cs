using System.Security.Cryptography;
using System.Text;
using TraceMap.Core;

namespace TraceMap.Reporting;

internal static class StaticDispatchCandidateBuilder
{
    public const string AlgorithmId = "static-dispatch-candidate-bridges.v1";
    public const string CandidateRuleId = "combined.dispatch-candidate.v1";
    public const string GapRuleId = "combined.dispatch-gap.v1";
    public const int DefaultCandidateLimit = 10;
    public const int DefaultMaxOverrideDepth = 5;
    private static readonly string[] DefaultLimitations = ["Static candidate evidence does not prove runtime dispatch or dependency-injection binding."];

    public static StaticDispatchCandidateBuildResult Build(
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        IEnumerable<StaticDispatchRelationshipEdge> relationships,
        Func<string, string?>? extractorVersionFor = null,
        StaticDispatchCandidateBuildOptions? options = null,
        IEnumerable<StaticDispatchRegistrationFact>? registrations = null)
    {
        var candidateLimit = Math.Max(1, options?.CandidateLimit ?? DefaultCandidateLimit);
        var maxOverrideDepth = Math.Clamp(options?.MaxOverrideDepth ?? DefaultMaxOverrideDepth, 1, DefaultMaxOverrideDepth);
        extractorVersionFor ??= static _ => null;
        var candidates = new List<StaticDispatchCandidateEdge>();
        var gaps = new List<StaticDispatchCandidateGap>();
        var allRelationships = relationships.ToArray();
        var registrationFacts = registrations?
            .OrderBy(registration => registration.FactId, StringComparer.Ordinal)
            .ToArray() ?? [];
        var registrationIndex = BuildRegistrationIndex(registrationFacts);
        var compatibleRegistrationFactIds = new HashSet<string>(StringComparer.Ordinal);
        var memberRelationships = allRelationships
            .Where(edge => IsMemberCandidateRelationship(edge.OriginalRelationshipKind))
            .Where(edge => nodes.TryGetValue(edge.FromNodeId, out var implementation)
                && nodes.TryGetValue(edge.ToNodeId, out var abstraction)
                && IsMethodNode(implementation)
                && IsMethodNode(abstraction))
            .ToArray();
        var interfaceRelationships = memberRelationships
            .Where(edge => edge.OriginalRelationshipKind == "ImplementsInterfaceMember")
            .ToArray();
        var overrideRelationships = memberRelationships
            .Where(edge => edge.OriginalRelationshipKind == "Overrides")
            .ToArray();
        var overrideRelationshipsByTarget = overrideRelationships
            .GroupBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => SortRelationships(group, nodes).ToArray(),
                StringComparer.Ordinal);

        foreach (var group in GroupRelationshipsByAbstraction(interfaceRelationships, nodes))
        {
            var sortedRelationships = SortRelationships(group, nodes, registrationIndex).ToArray();
            foreach (var relationship in sortedRelationships)
            {
                AddCompatibleRegistrationFactIds(
                    compatibleRegistrationFactIds,
                    CandidateRegistrationKey(
                        nodes[relationship.ToNodeId].SourceIndexId,
                        relationship.TargetContainingSymbolId,
                        relationship.SourceContainingSymbolId),
                    nodes[relationship.ToNodeId],
                    nodes[relationship.FromNodeId],
                    registrationIndex);
            }
            foreach (var relationship in sortedRelationships.Take(candidateLimit))
            {
                candidates.Add(CreateCandidate(
                    nodes,
                    relationship.ToNodeId,
                    relationship.FromNodeId,
                    relationship,
                    [relationship],
                    StaticDispatchBridgeKinds.InterfaceMember,
                    "interface-candidate"));
            }

            AddFanOutGapIfNeeded(gaps, sortedRelationships.Length, candidateLimit, group.Key, nodes, extractorVersionFor);
        }

        foreach (var group in GroupRelationshipsByAbstraction(overrideRelationships, nodes))
        {
            var overrideResult = BuildOverrideCandidatePaths(group.Key, overrideRelationshipsByTarget, nodes, maxOverrideDepth);
            var sortedPaths = overrideResult.Paths
                .OrderBy(path => RegistrationRank(
                    CandidateRegistrationKey(group.Key, path, nodes),
                    nodes[group.Key],
                    nodes[path.CandidateNodeId],
                    registrationIndex))
                .ThenBy(path => nodes[path.CandidateNodeId].SourceLabel, StringComparer.Ordinal)
                .ThenBy(path => nodes[path.CandidateNodeId].DisplayName, StringComparer.Ordinal)
                .ThenBy(path => path.LeafRelationship.FilePath, StringComparer.Ordinal)
                .ThenBy(path => path.LeafRelationship.StartLine ?? 0)
                .ThenBy(path => path.CandidateNodeId, StringComparer.Ordinal)
                .ToArray();
            foreach (var path in sortedPaths)
            {
                AddCompatibleRegistrationFactIds(
                    compatibleRegistrationFactIds,
                    CandidateRegistrationKey(group.Key, path, nodes),
                    nodes[group.Key],
                    nodes[path.CandidateNodeId],
                    registrationIndex);
            }

            foreach (var path in sortedPaths.Take(candidateLimit))
            {
                candidates.Add(CreateCandidate(
                    nodes,
                    group.Key,
                    path.CandidateNodeId,
                    path.LeafRelationship,
                    path.RelationshipChain,
                    StaticDispatchBridgeKinds.OverrideMember,
                    "override-candidate"));
            }

            AddFanOutGapIfNeeded(gaps, sortedPaths.Length, candidateLimit, group.Key, nodes, extractorVersionFor);
            AddOverrideDepthGapIfNeeded(gaps, overrideResult.TruncatedByDepth, maxOverrideDepth, group.Key, nodes, extractorVersionFor);
        }

        ApplyRegistrationContext(
            candidates,
            gaps,
            nodes,
            registrationFacts,
            registrationIndex,
            compatibleRegistrationFactIds,
            allRelationships.ToDictionary(relationship => relationship.EdgeId, StringComparer.Ordinal),
            extractorVersionFor);

        return new StaticDispatchCandidateBuildResult(
            candidates
                .OrderBy(candidate => EvidenceTierRank(candidate.EvidenceTier))
                .ThenBy(candidate => RegistrationContextRank(candidate.RegistrationContext))
                .ThenBy(candidate => candidate.BridgeKind, StringComparer.Ordinal)
                .ThenBy(candidate => nodes[candidate.CandidateSymbolId].SourceLabel, StringComparer.Ordinal)
                .ThenBy(candidate => nodes[candidate.CandidateSymbolId].DisplayName, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.FilePath, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.StartLine ?? 0)
                .ThenBy(candidate => candidate.EndLine ?? 0)
                .ThenBy(candidate => candidate.CandidateSymbolId, StringComparer.Ordinal)
                .ThenBy(candidate => candidate.CandidateId, StringComparer.Ordinal)
                .ToArray(),
            gaps
                .OrderBy(gap => gap.GapKind, StringComparer.Ordinal)
                .ThenBy(gap => gap.SourceLabel, StringComparer.Ordinal)
                .ThenBy(gap => gap.FilePath, StringComparer.Ordinal)
                .ThenBy(gap => gap.StartLine ?? 0)
                .ThenBy(gap => gap.GapId, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ApplyRegistrationContext(
        List<StaticDispatchCandidateEdge> candidates,
        List<StaticDispatchCandidateGap> gaps,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        IReadOnlyList<StaticDispatchRegistrationFact> registrations,
        IReadOnlyDictionary<RegistrationCompatibilityKey, StaticDispatchRegistrationFact[]> registrationIndex,
        IReadOnlySet<string> compatibleRegistrationFactIds,
        IReadOnlyDictionary<string, StaticDispatchRelationshipEdge> relationshipsById,
        Func<string, string?> extractorVersionFor)
    {
        if (registrations.Count == 0)
        {
            return;
        }

        var annotatedRegistrationFactIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < candidates.Count; index++)
        {
            var candidate = candidates[index];
            var abstraction = nodes[candidate.AbstractionSymbolId];
            var implementation = nodes[candidate.CandidateSymbolId];
            var registrationKey = CandidateRegistrationKey(candidate, relationshipsById);
            var matchingRegistrations = registrationKey is not null
                && registrationIndex.TryGetValue(registrationKey.Value, out var indexedRegistrations)
                    ? indexedRegistrations
                        .Where(registration => RegistrationDisplaysMatch(registration, abstraction, implementation))
                        .ToArray()
                    : [];
            if (matchingRegistrations.Length == 0)
            {
                continue;
            }

            var registrationFactIds = matchingRegistrations
                .Select(registration => registration.FactId)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            annotatedRegistrationFactIds.UnionWith(registrationFactIds);
            var supportingFactIds = candidate.SupportingFactIds
                .Concat(registrationFactIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var limitations = candidate.Limitations
                .Append("Static DI registration context supports review ordering only and does not prove runtime binding, registration order, object lifetime, or execution.")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            candidates[index] = candidate with
            {
                CandidateId = $"dispatch-candidate:{Hash($"{candidate.CandidateId}:{string.Join("|", registrationFactIds)}", 16)}",
                State = StaticDispatchCandidateStates.WeakerCandidate,
                EvidenceTier = WeakestEvidenceTier([
                    candidate.EvidenceTier,
                    .. matchingRegistrations.Select(registration => registration.EvidenceTier)
                ]),
                SupportingFactIds = supportingFactIds,
                SupportingRegistrationFactIds = registrationFactIds,
                RegistrationContext = StaticDispatchRegistrationContexts.Candidate,
                Limitations = limitations
            };
        }

        var methodNodesByContainingType = nodes.Values
            .Where(IsMethodNode)
            .Select(node => (Node: node, ContainingType: ContainingTypeDisplay(node.DisplayName)))
            .Where(item => !string.IsNullOrWhiteSpace(item.ContainingType))
            .GroupBy(
                item => new RegistrationServiceDisplayKey(item.Node.SourceIndexId, NormalizeDisplayName(item.ContainingType ?? string.Empty)),
                item => item.Node)
            .ToDictionary(
                group => group.Key,
                group => group
                    .OrderBy(node => node.DisplayName, StringComparer.Ordinal)
                    .ThenBy(node => node.NodeId, StringComparer.Ordinal)
                    .ToArray());
        foreach (var registration in registrations)
        {
            if (!methodNodesByContainingType.TryGetValue(
                    new RegistrationServiceDisplayKey(registration.SourceIndexId, NormalizeDisplayName(registration.ServiceType)),
                    out var matchingAbstractions))
            {
                continue;
            }

            if (annotatedRegistrationFactIds.Contains(registration.FactId)
                || compatibleRegistrationFactIds.Contains(registration.FactId))
            {
                continue;
            }

            var (gapKind, reason, message) = registration.Shape switch
            {
                StaticDispatchRegistrationShapes.OpenGeneric => (
                    "GenericCandidateNeedsReview",
                    "open-generic-registration",
                    "Open or partially closed registration evidence requires call-site generic closure and remains review context; no dispatch candidate was selected from it."),
                StaticDispatchRegistrationShapes.Unsupported => (
                    "UnsupportedRegistrationShape",
                    "unsupported-registration-shape",
                    "The registration uses a factory, keyed/named, instance, scanning, custom-container, or otherwise unsupported shape; no dispatch candidate was selected from it."),
                StaticDispatchRegistrationShapes.ObservationOnly => (
                    "RegistrationCompatibilityUnproven",
                    "registration-observation-only",
                    "Registration evidence is syntax-only or otherwise insufficient to prove compatibility; no dispatch candidate was selected from it."),
                _ => (
                    "RegistrationCompatibilityUnproven",
                    "registration-compatibility-unproven",
                    "Registration evidence does not agree with a relationship-backed implementation candidate; no dispatch candidate was created from registration evidence alone.")
            };
            var abstraction = matchingAbstractions[0];
            gaps.Add(new StaticDispatchCandidateGap(
                $"gap:dispatch:registration:{Hash($"{gapKind}:{registration.FactId}:{abstraction.NodeId}", 16)}",
                gapKind,
                StaticDispatchCandidateStates.CandidateGap,
                message,
                registration.SourceIndexId,
                registration.SourceLabel,
                abstraction.NodeId,
                GapRuleId,
                EvidenceTiers.Tier4Unknown,
                registration.FilePath,
                registration.StartLine,
                reason,
                registration.CommitSha,
                registration.ExtractorVersion ?? extractorVersionFor(registration.SourceIndexId),
                "dependency-registration-context",
                registration.EndLine,
                [registration.FactId]));
        }
    }

    private static IOrderedEnumerable<IGrouping<string, StaticDispatchRelationshipEdge>> GroupRelationshipsByAbstraction(
        IEnumerable<StaticDispatchRelationshipEdge> relationships,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes)
    {
        return relationships
            .GroupBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .OrderBy(group => nodes[group.Key].SourceLabel, StringComparer.Ordinal)
            .ThenBy(group => nodes[group.Key].DisplayName, StringComparer.Ordinal)
            .ThenBy(group => group.Key, StringComparer.Ordinal);
    }

    private static IEnumerable<StaticDispatchRelationshipEdge> SortRelationships(
        IEnumerable<StaticDispatchRelationshipEdge> relationships,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        IReadOnlyDictionary<RegistrationCompatibilityKey, StaticDispatchRegistrationFact[]>? registrationIndex = null)
    {
        return relationships
            .OrderBy(edge => RegistrationRank(
                CandidateRegistrationKey(
                    nodes[edge.ToNodeId].SourceIndexId,
                    edge.TargetContainingSymbolId,
                    edge.SourceContainingSymbolId),
                nodes[edge.ToNodeId],
                nodes[edge.FromNodeId],
                registrationIndex))
            .ThenBy(edge => nodes[edge.FromNodeId].SourceLabel, StringComparer.Ordinal)
            .ThenBy(edge => nodes[edge.FromNodeId].DisplayName, StringComparer.Ordinal)
            .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
            .ThenBy(edge => edge.StartLine ?? 0)
            .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal);
    }

    private static OverrideCandidatePathResult BuildOverrideCandidatePaths(
        string abstractionNodeId,
        IReadOnlyDictionary<string, StaticDispatchRelationshipEdge[]> byTarget,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        int maxOverrideDepth)
    {
        var results = new List<OverrideCandidatePath>();
        var seenCandidates = new HashSet<string>(StringComparer.Ordinal);
        var frontier = new List<OverrideTraversalFrame>
        {
            new(abstractionNodeId, [], [abstractionNodeId])
        };

        for (var depth = 1; depth <= maxOverrideDepth && frontier.Count > 0; depth++)
        {
            var next = new List<OverrideTraversalFrame>();
            foreach (var frame in frontier
                .OrderBy(item => nodes[item.CurrentNodeId].SourceLabel, StringComparer.Ordinal)
                .ThenBy(item => nodes[item.CurrentNodeId].DisplayName, StringComparer.Ordinal)
                .ThenBy(item => item.CurrentNodeId, StringComparer.Ordinal))
            {
                if (!byTarget.TryGetValue(frame.CurrentNodeId, out var outgoing))
                {
                    continue;
                }

                foreach (var relationship in outgoing)
                {
                    if (frame.VisitedNodeIds.Contains(relationship.FromNodeId))
                    {
                        continue;
                    }

                    var chain = frame.RelationshipChain.Append(relationship).ToArray();
                    var visited = frame.VisitedNodeIds.Append(relationship.FromNodeId).ToArray();
                    if (!seenCandidates.Add(relationship.FromNodeId))
                    {
                        continue;
                    }

                    results.Add(new OverrideCandidatePath(
                        relationship.FromNodeId,
                        relationship,
                        chain));
                    next.Add(new OverrideTraversalFrame(
                        relationship.FromNodeId,
                        chain,
                        visited));
                }
            }

            frontier = next;
        }

        var truncatedByDepth = frontier.Any(frame =>
            byTarget.TryGetValue(frame.CurrentNodeId, out var outgoing)
            && outgoing.Any(relationship => !frame.VisitedNodeIds.Contains(relationship.FromNodeId)));
        var sortedResults = results
            .OrderBy(path => nodes[path.CandidateNodeId].SourceLabel, StringComparer.Ordinal)
            .ThenBy(path => nodes[path.CandidateNodeId].DisplayName, StringComparer.Ordinal)
            .ThenBy(path => path.LeafRelationship.FilePath, StringComparer.Ordinal)
            .ThenBy(path => path.LeafRelationship.StartLine ?? 0)
            .ThenBy(path => path.CandidateNodeId, StringComparer.Ordinal)
            .ToArray();
        return new OverrideCandidatePathResult(sortedResults, truncatedByDepth);
    }

    private static StaticDispatchCandidateEdge CreateCandidate(
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        string abstractionNodeId,
        string candidateNodeId,
        StaticDispatchRelationshipEdge leafRelationship,
        IReadOnlyList<StaticDispatchRelationshipEdge> relationshipChain,
        string bridgeKind,
        string edgeKind)
    {
        var abstractionNode = nodes[abstractionNodeId];
        var relationshipIds = relationshipChain
            .Select(edge => edge.EdgeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var supportingEdges = relationshipChain
            .SelectMany(edge => edge.SupportingCombinedEdgeIds.Append(edge.EdgeId))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        var candidateHashInput = relationshipChain.Count == 1
            && string.Equals(leafRelationship.ToNodeId, abstractionNodeId, StringComparison.Ordinal)
            && string.Equals(leafRelationship.FromNodeId, candidateNodeId, StringComparison.Ordinal)
            ? $"{leafRelationship.EdgeId}:{leafRelationship.ToNodeId}:{leafRelationship.FromNodeId}"
            : $"{abstractionNodeId}:{candidateNodeId}:{string.Join("|", relationshipIds)}";
        return new StaticDispatchCandidateEdge(
            $"dispatch-candidate:{Hash(candidateHashInput, 16)}",
            AlgorithmId,
            StaticDispatchCandidateStates.SymbolBackedCandidate,
            abstractionNode.SourceIndexId,
            abstractionNode.SourceLabel,
            null,
            abstractionNodeId,
            candidateNodeId,
            candidateNodeId,
            null,
            leafRelationship.OriginalRelationshipKind,
            bridgeKind,
            edgeKind,
            WeakestEvidenceTier(relationshipChain),
            CandidateRuleId,
            relationshipChain
                .SelectMany(edge => edge.SupportingFactIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            supportingEdges,
            relationshipIds,
            [],
            StaticDispatchRegistrationContexts.None,
            leafRelationship.FilePath,
            leafRelationship.StartLine,
            leafRelationship.EndLine,
            DefaultLimitations,
            []);
    }

    private static void AddFanOutGapIfNeeded(
        List<StaticDispatchCandidateGap> gaps,
        int candidateCount,
        int candidateLimit,
        string abstractionNodeId,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        Func<string, string?> extractorVersionFor)
    {
        if (candidateCount <= candidateLimit)
        {
            return;
        }

        var abstractionNode = nodes[abstractionNodeId];
        gaps.Add(new StaticDispatchCandidateGap(
            $"gap:dispatch:fanout:{Hash($"{abstractionNodeId}:{candidateCount}", 16)}",
            "DispatchCandidateFanOut",
            StaticDispatchCandidateStates.CandidateGap,
            $"Static dispatch candidate derivation found {candidateCount} candidates for `{abstractionNode.DisplayName}`; only the first {candidateLimit} deterministic candidates were traversed.",
            abstractionNode.SourceIndexId,
            abstractionNode.SourceLabel,
            abstractionNode.NodeId,
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            abstractionNode.FilePath,
            abstractionNode.StartLine,
            "dispatch-candidate-fanout",
            abstractionNode.CommitSha,
            extractorVersionFor(abstractionNode.SourceIndexId),
            "combined-symbol-relationships",
            abstractionNode.EndLine,
            [],
            candidateCount,
            candidateLimit));
    }

    private static void AddOverrideDepthGapIfNeeded(
        List<StaticDispatchCandidateGap> gaps,
        bool truncatedByDepth,
        int maxOverrideDepth,
        string abstractionNodeId,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        Func<string, string?> extractorVersionFor)
    {
        if (!truncatedByDepth)
        {
            return;
        }

        var abstractionNode = nodes[abstractionNodeId];
        gaps.Add(new StaticDispatchCandidateGap(
            $"gap:dispatch:override-depth:{Hash($"{abstractionNodeId}:{maxOverrideDepth}", 16)}",
            "DispatchCandidateTruncatedByLimit",
            StaticDispatchCandidateStates.CandidateGap,
            $"Static override candidate chain traversal for `{abstractionNode.DisplayName}` reached the max depth of {maxOverrideDepth}; deeper override candidates were not traversed.",
            abstractionNode.SourceIndexId,
            abstractionNode.SourceLabel,
            abstractionNode.NodeId,
            GapRuleId,
            EvidenceTiers.Tier4Unknown,
            abstractionNode.FilePath,
            abstractionNode.StartLine,
            "override-depth",
            abstractionNode.CommitSha,
            extractorVersionFor(abstractionNode.SourceIndexId),
            "combined-symbol-relationships",
            abstractionNode.EndLine,
            [],
            CandidateLimit: maxOverrideDepth));
    }

    private static bool IsMemberCandidateRelationship(string? originalRelationshipKind)
    {
        return originalRelationshipKind is "ImplementsInterfaceMember" or "Overrides";
    }

    private static bool IsMethodNode(StaticDispatchCandidateNode node)
    {
        return string.Equals(node.NodeKind, "Method", StringComparison.Ordinal)
            || node.DisplayName.IndexOf('(', StringComparison.Ordinal) >= 0;
    }

    private static IReadOnlyDictionary<RegistrationCompatibilityKey, StaticDispatchRegistrationFact[]> BuildRegistrationIndex(
        IReadOnlyList<StaticDispatchRegistrationFact> registrations)
    {
        return registrations
            .Where(registration => registration.Shape == StaticDispatchRegistrationShapes.ClosedTypePair)
            .Where(registration => IsStrongRegistrationEvidence(registration.EvidenceTier))
            .Where(registration => !string.IsNullOrWhiteSpace(registration.ServiceTypeSymbolId)
                && !string.IsNullOrWhiteSpace(registration.ImplementationTypeSymbolId))
            .GroupBy(
                registration => new RegistrationCompatibilityKey(
                    registration.SourceIndexId,
                    registration.ServiceTypeSymbolId ?? string.Empty,
                    registration.ImplementationTypeSymbolId ?? string.Empty))
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(registration => registration.FactId, StringComparer.Ordinal).ToArray());
    }

    private static RegistrationCompatibilityKey? CandidateRegistrationKey(
        string sourceIndexId,
        string? serviceTypeSymbolId,
        string? implementationTypeSymbolId)
    {
        return string.IsNullOrWhiteSpace(serviceTypeSymbolId) || string.IsNullOrWhiteSpace(implementationTypeSymbolId)
            ? null
            : new RegistrationCompatibilityKey(sourceIndexId, serviceTypeSymbolId, implementationTypeSymbolId);
    }

    private static RegistrationCompatibilityKey? CandidateRegistrationKey(
        string abstractionNodeId,
        OverrideCandidatePath path,
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes)
    {
        var abstractionRelationship = path.RelationshipChain
            .FirstOrDefault(relationship => relationship.ToNodeId == abstractionNodeId);
        var implementationRelationship = path.RelationshipChain
            .FirstOrDefault(relationship => relationship.FromNodeId == path.CandidateNodeId);
        return CandidateRegistrationKey(
            nodes[abstractionNodeId].SourceIndexId,
            abstractionRelationship?.TargetContainingSymbolId,
            implementationRelationship?.SourceContainingSymbolId);
    }

    private static RegistrationCompatibilityKey? CandidateRegistrationKey(
        StaticDispatchCandidateEdge candidate,
        IReadOnlyDictionary<string, StaticDispatchRelationshipEdge> relationshipsById)
    {
        var candidateRelationships = candidate.SupportingRelationshipIds
            .Where(relationshipsById.ContainsKey)
            .Select(id => relationshipsById[id])
            .ToArray();
        return CandidateRegistrationKey(
            candidate.SourceIndexId,
            candidateRelationships
                .FirstOrDefault(relationship => relationship.ToNodeId == candidate.AbstractionSymbolId)
                ?.TargetContainingSymbolId,
            candidateRelationships
                .FirstOrDefault(relationship => relationship.FromNodeId == candidate.CandidateSymbolId)
                ?.SourceContainingSymbolId);
    }

    private static int RegistrationRank(
        RegistrationCompatibilityKey? key,
        StaticDispatchCandidateNode abstraction,
        StaticDispatchCandidateNode implementation,
        IReadOnlyDictionary<RegistrationCompatibilityKey, StaticDispatchRegistrationFact[]>? registrationIndex)
    {
        return key is not null
            && registrationIndex is not null
            && registrationIndex.TryGetValue(key.Value, out var registrations)
            && registrations.Any(registration => RegistrationDisplaysMatch(registration, abstraction, implementation))
                ? 0
                : 1;
    }

    private static void AddCompatibleRegistrationFactIds(
        HashSet<string> compatibleFactIds,
        RegistrationCompatibilityKey? key,
        StaticDispatchCandidateNode abstraction,
        StaticDispatchCandidateNode implementation,
        IReadOnlyDictionary<RegistrationCompatibilityKey, StaticDispatchRegistrationFact[]> registrationIndex)
    {
        if (key is null || !registrationIndex.TryGetValue(key.Value, out var registrations))
        {
            return;
        }

        compatibleFactIds.UnionWith(registrations
            .Where(registration => RegistrationDisplaysMatch(registration, abstraction, implementation))
            .Select(registration => registration.FactId));
    }

    private static bool RegistrationDisplaysMatch(
        StaticDispatchRegistrationFact registration,
        StaticDispatchCandidateNode abstraction,
        StaticDispatchCandidateNode implementation)
    {
        return IsMemberOfType(abstraction.DisplayName, registration.ServiceType)
            && IsMemberOfType(implementation.DisplayName, registration.ImplementationType);
    }

    private static string? ContainingTypeDisplay(string memberDisplayName)
    {
        var normalized = NormalizeDisplayName(memberDisplayName);
        var parameterStart = normalized.IndexOf('(', StringComparison.Ordinal);
        var memberPrefix = parameterStart < 0 ? normalized : normalized[..parameterStart];
        var memberSeparator = memberPrefix.LastIndexOf('.');
        return memberSeparator < 0 ? null : memberPrefix[..memberSeparator];
    }

    private static string WeakestEvidenceTier(IReadOnlyList<StaticDispatchRelationshipEdge> relationships)
    {
        return relationships
            .Select(edge => NormalizeEvidenceTier(edge.EvidenceTier))
            .OrderByDescending(EvidenceTierRank)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? EvidenceTiers.Tier4Unknown;
    }

    private static string WeakestEvidenceTier(IEnumerable<string> tiers)
    {
        return tiers
            .Select(NormalizeEvidenceTier)
            .OrderByDescending(EvidenceTierRank)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? EvidenceTiers.Tier4Unknown;
    }

    private static bool IsStrongRegistrationEvidence(string evidenceTier)
    {
        return evidenceTier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural;
    }

    private static bool IsMemberOfType(string memberDisplayName, string typeDisplayName)
    {
        var member = NormalizeDisplayName(memberDisplayName);
        var type = NormalizeDisplayName(typeDisplayName);
        if (member.StartsWith($"{type}.", StringComparison.Ordinal))
        {
            return true;
        }

        var memberContainingType = ContainingTypeDisplay(member);
        // The registration index has already required exact compiler-backed type-definition IDs.
        // Displays may still differ because relationship members retain <T> while registrations
        // retain their closed type arguments, so compare their display definitions only here.
        return memberContainingType is not null
            && ContainsGenericArguments(memberContainingType)
            && ContainsGenericArguments(type)
            && string.Equals(
                RemoveGenericArguments(memberContainingType),
                RemoveGenericArguments(type),
                StringComparison.Ordinal);
    }

    private static bool ContainsGenericArguments(string value)
    {
        return value.IndexOf('<', StringComparison.Ordinal) >= 0;
    }

    private static string RemoveGenericArguments(string value)
    {
        var result = new StringBuilder(value.Length);
        var depth = 0;
        foreach (var character in value)
        {
            if (character == '<')
            {
                depth++;
                continue;
            }

            if (character == '>' && depth > 0)
            {
                depth--;
                continue;
            }

            if (depth == 0)
            {
                result.Append(character);
            }
        }

        return result.ToString();
    }

    private static string NormalizeDisplayName(string value)
    {
        return value.Trim().Replace("global::", string.Empty, StringComparison.Ordinal);
    }

    private static int RegistrationContextRank(string context)
    {
        return context == StaticDispatchRegistrationContexts.Candidate ? 0 : 1;
    }

    private static string NormalizeEvidenceTier(string tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic
                or EvidenceTiers.Tier2Structural
                or EvidenceTiers.Tier3SyntaxOrTextual
                or EvidenceTiers.Tier4Unknown => tier,
            _ => EvidenceTiers.Tier4Unknown
        };
    }

    private static int EvidenceTierRank(string tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 1,
            EvidenceTiers.Tier2Structural => 2,
            EvidenceTiers.Tier3SyntaxOrTextual => 3,
            EvidenceTiers.Tier4Unknown => 4,
            _ => 5
        };
    }

    private static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }
}

internal sealed record StaticDispatchCandidateBuildOptions(
    int CandidateLimit = StaticDispatchCandidateBuilder.DefaultCandidateLimit,
    int MaxOverrideDepth = StaticDispatchCandidateBuilder.DefaultMaxOverrideDepth);

internal sealed record StaticDispatchCandidateBuildResult(
    IReadOnlyList<StaticDispatchCandidateEdge> Edges,
    IReadOnlyList<StaticDispatchCandidateGap> Gaps);

internal static class StaticDispatchCandidateStates
{
    public const string SymbolBackedCandidate = nameof(SymbolBackedCandidate);
    public const string WeakerCandidate = nameof(WeakerCandidate);
    public const string CandidateGap = nameof(CandidateGap);
}

internal static class StaticDispatchBridgeKinds
{
    public const string InterfaceMember = "interface-member";
    public const string OverrideMember = "override-member";
}

internal static class StaticDispatchRegistrationContexts
{
    public const string None = "none";
    public const string Candidate = "registration-context-candidate";
}

internal static class StaticDispatchRegistrationShapes
{
    public const string ClosedTypePair = "closed-type-pair";
    public const string OpenGeneric = "open-generic";
    public const string ObservationOnly = "observation-only";
    public const string Unsupported = "unsupported";
}

internal sealed record OverrideCandidatePath(
    string CandidateNodeId,
    StaticDispatchRelationshipEdge LeafRelationship,
    IReadOnlyList<StaticDispatchRelationshipEdge> RelationshipChain);

internal sealed record OverrideCandidatePathResult(
    IReadOnlyList<OverrideCandidatePath> Paths,
    bool TruncatedByDepth);

internal sealed record OverrideTraversalFrame(
    string CurrentNodeId,
    IReadOnlyList<StaticDispatchRelationshipEdge> RelationshipChain,
    IReadOnlyList<string> VisitedNodeIds);

internal readonly record struct RegistrationCompatibilityKey(
    string SourceIndexId,
    string ServiceTypeSymbolId,
    string ImplementationTypeSymbolId);

internal readonly record struct RegistrationServiceDisplayKey(
    string SourceIndexId,
    string ServiceTypeDisplay);

internal sealed record StaticDispatchCandidateNode(
    string NodeId,
    string NodeKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine);

internal sealed record StaticDispatchRelationshipEdge(
    string EdgeId,
    string EdgeKind,
    string? OriginalRelationshipKind,
    string FromNodeId,
    string ToNodeId,
    string EvidenceTier,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingCombinedEdgeIds,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? SourceContainingSymbolId = null,
    string? TargetContainingSymbolId = null);

internal sealed record StaticDispatchRegistrationFact(
    string FactId,
    string SourceIndexId,
    string SourceLabel,
    string ServiceType,
    string ImplementationType,
    string? ServiceTypeSymbolId,
    string? ImplementationTypeSymbolId,
    string RegistrationKind,
    string Shape,
    string EvidenceTier,
    string RuleId,
    string FilePath,
    int StartLine,
    int EndLine,
    string? CommitSha,
    string? ExtractorVersion);

internal sealed record StaticDispatchCandidateEdge(
    string CandidateId,
    string AlgorithmId,
    string State,
    string SourceIndexId,
    string SourceLabel,
    string? CallEdgeId,
    string AbstractionSymbolId,
    string CandidateSymbolId,
    string? CandidateMemberSymbolId,
    string? CandidateTypeSymbolId,
    string? RelationshipKind,
    string BridgeKind,
    string ConsumerEdgeKind,
    string EvidenceTier,
    string RuleId,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> SupportingRelationshipIds,
    IReadOnlyList<string> SupportingRegistrationFactIds,
    string RegistrationContext,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<string> Gaps);

internal sealed record StaticDispatchCandidateGap(
    string GapId,
    string GapKind,
    string State,
    string Message,
    string? SourceIndexId,
    string? SourceLabel,
    string? NodeId,
    string RuleId,
    string EvidenceTier,
    string? FilePath,
    int? StartLine,
    string? Reason,
    string? CommitSha,
    string? ExtractorVersion,
    string? EvidenceScope,
    int? EndLine,
    IReadOnlyList<string> SupportingFactIds,
    int? CandidateCount = null,
    int? CandidateLimit = null);
