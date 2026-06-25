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
        StaticDispatchCandidateBuildOptions? options = null)
    {
        var candidateLimit = Math.Max(1, options?.CandidateLimit ?? DefaultCandidateLimit);
        var maxOverrideDepth = Math.Clamp(options?.MaxOverrideDepth ?? DefaultMaxOverrideDepth, 1, DefaultMaxOverrideDepth);
        extractorVersionFor ??= static _ => null;
        var candidates = new List<StaticDispatchCandidateEdge>();
        var gaps = new List<StaticDispatchCandidateGap>();
        var memberRelationships = relationships
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
            var sortedRelationships = SortRelationships(group, nodes).ToArray();
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
            foreach (var path in overrideResult.Paths.Take(candidateLimit))
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

            AddFanOutGapIfNeeded(gaps, overrideResult.Paths.Count, candidateLimit, group.Key, nodes, extractorVersionFor);
            AddOverrideDepthGapIfNeeded(gaps, overrideResult.TruncatedByDepth, maxOverrideDepth, group.Key, nodes, extractorVersionFor);
        }

        return new StaticDispatchCandidateBuildResult(candidates, gaps);
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
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes)
    {
        return relationships
            .OrderBy(edge => nodes[edge.FromNodeId].SourceLabel, StringComparer.Ordinal)
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
            "none",
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
            abstractionNode.EndLine));
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
            abstractionNode.EndLine));
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

    private static string WeakestEvidenceTier(IReadOnlyList<StaticDispatchRelationshipEdge> relationships)
    {
        return relationships
            .Select(edge => NormalizeEvidenceTier(edge.EvidenceTier))
            .OrderByDescending(EvidenceTierRank)
            .ThenBy(value => value, StringComparer.Ordinal)
            .FirstOrDefault() ?? EvidenceTiers.Tier4Unknown;
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
    int? EndLine);

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
    int? EndLine);
