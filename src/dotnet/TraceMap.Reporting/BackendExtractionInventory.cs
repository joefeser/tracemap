namespace TraceMap.Reporting;

/// <summary>A bounded projection of retained query evidence, not full-index discovery.</summary>
public sealed record BackendExtractionOperation(string NodeReference, string Family,
    string ConnectionState, IReadOnlyList<int> VariantIndexes, IReadOnlyList<string> QueryRootReferences,
    string HumanDecision, IReadOnlyList<string> ContractReviewObligations,
    IReadOnlyList<int> CandidateVariantIndexes);

public sealed record BackendExtractionInventory(string RuleId, string Scope,
    string Coverage, bool Truncated, int RetainedGapCount,
    IReadOnlyList<BackendExtractionOperation> Operations, IReadOnlyList<string> Limitations)
{
    public string QuerySurface { get; init; } = "unspecified";
}

public static class BackendExtractionInventoryBuilder
{
    public const string Rule = "workflow.backend-extraction.retained-report.v1";

    // References retain full node evidence, including differing callsite/command-binding variants.
    // Do not collapse records by display name or procedure hash.
    internal static BackendExtractionInventory Create(GroupedCompiledPathHandoff handoff)
    {
        var connections = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        var candidateVariants = handoff.Variants.Select(variant =>
            RequiresReview(variant.Path.Classification)
            || variant.EdgeReferences.Any(reference => IsCandidate(handoff.Edges[reference]))
            || variant.NodeReferences.Any(node => handoff.Nodes[node].EvidenceTier is not ("Tier1Semantic" or "Tier2Structural"))).ToArray();
        for (var index = 0; index < handoff.Variants.Count; index++)
            foreach (var reference in handoff.Variants[index].NodeReferences.Distinct(StringComparer.Ordinal))
            {
                if (!connections.TryGetValue(reference, out var paths)) connections.Add(reference, paths = []);
                paths.Add(index);
            }
        var operations = new List<BackendExtractionOperation>();
        foreach (var (reference, node) in handoff.Nodes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            var family = node.SurfaceKind switch
            {
                "database-api" or "sql-query" => "database",
                "sql-persistence" when handoff.DatabaseCoverageLimitations is not null => "database",
                "http-client" => "outbound-http",
                _ => null
            };
            if (family is null) continue;
            var variants = connections.GetValueOrDefault(reference) ?? [];
            // These are query roots, not automatically verified page/event entry points.
            var roots = variants.Select(index => handoff.Variants[index].NodeReferences.First())
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var state = variants.Count == 0 ? "found-not-connected-in-supplied-query"
                : "retained-query-path";
            var candidates = variants.Where(index => candidateVariants[index]).ToArray();
            if (variants.Count > 0 && candidates.Length == variants.Count) state = "candidate-query-path";
            else if (candidates.Length > 0) state = "mixed-retained-and-candidate-query-paths";
            var missing = family == "database"
                ? new[] { "authorized-readable-command-or-procedure-contract", "parameter-and-result-contract", "transaction-and-authorization-context" }
                : new[] { "authorized-destination-and-http-contract", "request-and-response-contract", "authentication-and-error-contract" };
            operations.Add(new(reference, family, state, variants.ToArray(), roots, "undecided", missing, candidates));
        }
        var surface = handoff.Header.Query.ToSurface ?? "unspecified";
        return new(Rule, "retained-query-report-not-full-index", handoff.Header.ReportCoverage,
            handoff.Header.Summary.Truncated, handoff.Header.Gaps.Count, operations,
            [surface is "database-api" or "sql-query"
                ? "Database-filtered query: HTTP coverage is not assessed. HTTP-only handlers may be excluded upstream; incidental retained HTTP records do not establish HTTP inventory coverage."
                : "Query surface: " + surface + ". Only supplied retained evidence is assessed; other surface families may be excluded upstream.",
             "Only database-api, sql-query and http-client surfaces retained by the supplied report are listed. Other provider/service families and operations outside this query may be absent.",
             "Rows are distinct evidence records, not a count of unique business operations or procedures. Query roots are not verified UI entry points; inspect each ordered variant and its tiers.",
             "Found without a retained path does not mean unused. Connection evidence does not decide retain, retire or replace; human decisions remain undecided.",
             "Readable SQL, procedure definitions, secrets and HTTP payloads are not reconstructed. Missing-input categories are review obligations, not claims that such data is absent from the application.",
             "Generator and bounded-input identities are inherited from the containing grouped handoff; this projection does not authenticate the original index or prove complete migration coverage."])
        { QuerySurface = surface };
    }

    private static bool RequiresReview(string classification) => classification is not
        (CombinedDependencyPathClassifications.StrongStaticPath or CombinedDependencyPathClassifications.ProbableStaticPath);

    private static bool IsCandidate(CombinedPathEdge edge) =>
        edge.EvidenceTier is not ("Tier1Semantic" or "Tier2Structural")
        || edge.EdgeKind.Contains("candidate", StringComparison.Ordinal)
        || edge.CandidateCount > 1
        || edge.Classification is "NeedsReviewPath" or "NeedsReviewStaticPath" or "UnknownAnalysisGap" or "AnalysisGap" or "ReducedCoverage"
        || RequiresReview(CombinedDependencyPathReporter.Classify([edge]));
}
