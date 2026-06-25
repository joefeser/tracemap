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

    public static StaticDispatchCandidateBuildResult Build(
        IReadOnlyDictionary<string, StaticDispatchCandidateNode> nodes,
        IEnumerable<StaticDispatchRelationshipEdge> relationships,
        StaticDispatchCandidateBuildOptions? options = null)
    {
        var candidateLimit = options?.CandidateLimit ?? DefaultCandidateLimit;
        var candidates = new List<StaticDispatchCandidateEdge>();
        var gaps = new List<StaticDispatchCandidateGap>();
        var relationshipGroups = relationships
            .Where(edge => IsMemberCandidateRelationship(edge.OriginalRelationshipKind))
            .Where(edge => nodes.TryGetValue(edge.FromNodeId, out var implementation)
                && nodes.TryGetValue(edge.ToNodeId, out var abstraction)
                && IsMethodNode(implementation)
                && IsMethodNode(abstraction))
            .GroupBy(edge => edge.ToNodeId, StringComparer.Ordinal)
            .OrderBy(group => nodes.TryGetValue(group.Key, out var node) ? node.SourceLabel : string.Empty, StringComparer.Ordinal)
            .ThenBy(group => nodes.TryGetValue(group.Key, out var node) ? node.DisplayName : string.Empty, StringComparer.Ordinal)
            .ThenBy(group => group.Key, StringComparer.Ordinal)
            .ToArray();

        foreach (var group in relationshipGroups)
        {
            var sortedRelationships = group
                .OrderBy(edge => nodes[edge.FromNodeId].SourceLabel, StringComparer.Ordinal)
                .ThenBy(edge => nodes[edge.FromNodeId].DisplayName, StringComparer.Ordinal)
                .ThenBy(edge => edge.FilePath, StringComparer.Ordinal)
                .ThenBy(edge => edge.StartLine ?? 0)
                .ThenBy(edge => edge.EdgeId, StringComparer.Ordinal)
                .ToArray();

            foreach (var relationship in sortedRelationships.Take(candidateLimit))
            {
                var bridgeKind = relationship.OriginalRelationshipKind == "Overrides"
                    ? StaticDispatchBridgeKinds.OverrideMember
                    : StaticDispatchBridgeKinds.InterfaceMember;
                var edgeKind = bridgeKind == StaticDispatchBridgeKinds.OverrideMember
                    ? "override-candidate"
                    : "interface-candidate";
                candidates.Add(new StaticDispatchCandidateEdge(
                    $"dispatch-candidate:{Hash($"{relationship.EdgeId}:{relationship.ToNodeId}:{relationship.FromNodeId}", 16)}",
                    AlgorithmId,
                    StaticDispatchCandidateStates.SymbolBackedCandidate,
                    nodes[relationship.ToNodeId].SourceIndexId,
                    nodes[relationship.ToNodeId].SourceLabel,
                    null,
                    relationship.ToNodeId,
                    relationship.FromNodeId,
                    relationship.FromNodeId,
                    null,
                    relationship.OriginalRelationshipKind,
                    bridgeKind,
                    edgeKind,
                    relationship.EvidenceTier,
                    CandidateRuleId,
                    relationship.SupportingFactIds,
                    relationship.SupportingCombinedEdgeIds
                        .Append(relationship.EdgeId)
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray(),
                    [relationship.EdgeId],
                    [],
                    "none",
                    relationship.FilePath,
                    relationship.StartLine,
                    relationship.EndLine,
                    ["Static candidate evidence does not prove runtime dispatch or dependency-injection binding."],
                    []));
            }

            if (sortedRelationships.Length > candidateLimit && nodes.TryGetValue(group.Key, out var abstractionNode))
            {
                gaps.Add(new StaticDispatchCandidateGap(
                    $"gap:dispatch:fanout:{Hash($"{group.Key}:{sortedRelationships.Length}", 16)}",
                    "DispatchCandidateFanOut",
                    StaticDispatchCandidateStates.CandidateGap,
                    $"Static dispatch candidate derivation found {sortedRelationships.Length} candidates for `{abstractionNode.DisplayName}`; only the first {candidateLimit} deterministic candidates were traversed.",
                    abstractionNode.SourceIndexId,
                    abstractionNode.SourceLabel,
                    abstractionNode.NodeId,
                    GapRuleId,
                    EvidenceTiers.Tier4Unknown,
                    abstractionNode.FilePath,
                    abstractionNode.StartLine,
                    "dispatch-candidate-fanout",
                    abstractionNode.CommitSha,
                    abstractionNode.ExtractorVersion,
                    "combined-symbol-relationships",
                    abstractionNode.EndLine));
            }
        }

        return new StaticDispatchCandidateBuildResult(candidates, gaps);
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

    private static string Hash(string value, int length)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        var text = Convert.ToHexString(bytes).ToLowerInvariant();
        return text[..Math.Min(length, text.Length)];
    }
}

internal sealed record StaticDispatchCandidateBuildOptions(int CandidateLimit = StaticDispatchCandidateBuilder.DefaultCandidateLimit);

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

internal sealed record StaticDispatchCandidateNode(
    string NodeId,
    string NodeKind,
    string DisplayName,
    string SourceIndexId,
    string SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine,
    string? ExtractorVersion);

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
