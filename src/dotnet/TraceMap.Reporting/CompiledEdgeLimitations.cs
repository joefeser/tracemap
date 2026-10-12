namespace TraceMap.Reporting;

/// <summary>Deterministic rule-owned explanations; never derived from method names.</summary>
public static class CompiledEdgeLimitations
{
    public static IReadOnlyList<string>? ForRule(string ruleId) => ruleId switch
    {
        "combined.paths.constructed-generic-member.v1" =>
        ["The target is a retained generic definition, not a specialized constructed body. Downstream generic IL is not specialized; this edge does not establish additional database paths or complete coverage.",
         "Unique static member resolution does not establish runtime dispatch or source/build authenticity. Separate provenance gaps remain applicable."],
        _ => null
    };
}
