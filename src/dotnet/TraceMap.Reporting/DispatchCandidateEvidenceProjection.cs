using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record DispatchCandidateEvidenceSummary(
    string Status,
    string Classification,
    int CandidateCount,
    int SymbolBackedCandidateCount,
    int WeakerCandidateCount,
    int RegistrationContextCandidateCount,
    int GapCount,
    bool FanOutOrTruncated,
    IReadOnlyDictionary<string, int> CandidatesBySource,
    IReadOnlyDictionary<string, int> CandidatesByBridgeKind,
    IReadOnlyDictionary<string, int> GapsByKind,
    IReadOnlyList<string> CoverageLabels,
    IReadOnlyList<string> RuleIds,
    IReadOnlyList<string> EvidenceTiers,
    IReadOnlyList<string> SupportingFactIds,
    IReadOnlyList<string> SupportingEdgeIds,
    IReadOnlyList<string> Limitations);

internal static class DispatchCandidateEvidenceProjection
{
    public const string ReportRuleId = "combined.report.dispatch-candidate-summary.v1";
    public const string PortfolioRuleId = "portfolio.context.dispatch-candidate.v1";
    public const string VaultEdgeRuleId = "vault-export.graph.dispatch-candidate.v1";
    public const string VaultGapRuleId = "vault-export.gap.dispatch-candidate.v1";
    public const string DocsChunkRuleId = "docs-export.chunk.dispatch-candidate.v1";
    public const string DocsGapRuleId = "docs-export.gap.dispatch-candidate.v1";
    public const string ReviewClassification = "NeedsReviewStaticCandidate";

    public static DispatchCandidateEvidenceSummary Summarize(CombinedPathGraphInventory inventory)
    {
        var edges = CandidateEdges(inventory).ToArray();
        var gaps = CandidateGaps(inventory).ToArray();
        var nodeById = inventory.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var fanOutOrTruncated = gaps.Any(gap => gap.GapKind is "DispatchCandidateFanOut" or "DispatchCandidateTruncatedByLimit");
        var reduced = inventory.CoverageWarnings.Count > 0
            || gaps.Length > 0
            || inventory.Sources.Any(IsReducedCoverage);
        return new DispatchCandidateEvidenceSummary(
            "available",
            reduced ? "NeedsReviewStaticCandidatePartial" : ReviewClassification,
            edges.Length,
            edges.Count(edge => edge.CandidateState == StaticDispatchCandidateStates.SymbolBackedCandidate),
            edges.Count(edge => edge.CandidateState == StaticDispatchCandidateStates.WeakerCandidate),
            edges.Count(edge => edge.RegistrationContext == StaticDispatchRegistrationContexts.Candidate),
            gaps.Length,
            fanOutOrTruncated,
            CountBy(edges, edge => SourceLabel(edge, nodeById)),
            CountBy(edges, edge => edge.CandidateBridgeKind ?? "unknown"),
            CountBy(gaps, gap => gap.GapKind),
            inventory.Sources
                .Select(source => IsReducedCoverage(source) ? "reduced-static-evidence" : "static-evidence")
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            edges.Select(edge => edge.RuleId)
                .Concat(gaps.Select(gap => gap.RuleId ?? StaticDispatchCandidateBuilder.GapRuleId))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            edges.Select(edge => edge.EvidenceTier)
                .Concat(gaps.Select(gap => gap.EvidenceTier ?? EvidenceTiers.Tier4Unknown))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            edges.SelectMany(edge => edge.SupportingFactIds)
                .Concat(edges.SelectMany(edge => edge.SupportingRegistrationFactIds ?? []))
                .Concat(gaps.Select(gap => gap.CombinedFactId).Where(value => !string.IsNullOrWhiteSpace(value)).Cast<string>())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            edges.SelectMany(edge => edge.SupportingCombinedEdgeIds)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            [
                "Static dispatch candidates are bounded review context and do not prove runtime dispatch, dependency-injection binding, selected implementation, reachability, or impact.",
                "Candidate absence is coverage-relative; reduced or unsupported evidence remains explicit gap state."
            ]);
    }

    public static DispatchCandidateEvidenceSummary Summarize(
        CombinedPathGraphInventory inventory,
        string sourceIndexId)
    {
        var selectedNodes = inventory.Nodes
            .Where(node => string.Equals(node.SourceIndexId, sourceIndexId, StringComparison.Ordinal))
            .ToArray();
        var selectedNodeIds = selectedNodes
            .Select(node => node.NodeId)
            .ToHashSet(StringComparer.Ordinal);
        return Summarize(new CombinedPathGraphInventory(
            inventory.Sources
                .Where(source => string.Equals(source.SourceIndexId, sourceIndexId, StringComparison.Ordinal))
                .ToArray(),
            [],
            selectedNodes,
            inventory.Edges
                .Where(edge => selectedNodeIds.Contains(edge.FromNodeId))
                .ToArray(),
            inventory.Gaps
                .Where(gap => string.Equals(gap.SourceIndexId, sourceIndexId, StringComparison.Ordinal))
                .ToArray()));
    }

    public static IEnumerable<CombinedPathEdge> CandidateEdges(CombinedPathGraphInventory inventory) =>
        inventory.Edges.Where(edge => edge.RuleId == StaticDispatchCandidateBuilder.CandidateRuleId);

    public static IEnumerable<CombinedPathGap> CandidateGaps(CombinedPathGraphInventory inventory) =>
        inventory.Gaps.Where(gap => gap.RuleId == StaticDispatchCandidateBuilder.GapRuleId);

    private static IReadOnlyDictionary<string, int> CountBy<T>(IEnumerable<T> values, Func<T, string> keySelector) =>
        values.GroupBy(keySelector, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

    private static string SourceLabel(CombinedPathEdge edge, IReadOnlyDictionary<string, CombinedPathNode> nodes) =>
        nodes.TryGetValue(edge.FromNodeId, out var source) ? source.SourceLabel : "unknown";

    private static bool IsReducedCoverage(CombinedReportSource source) =>
        !string.Equals(source.BuildStatus, "Succeeded", StringComparison.Ordinal)
        || !string.Equals(source.AnalysisLevel, "Level1SemanticAnalysis", StringComparison.Ordinal);
}
