using TraceMap.Core;

namespace TraceMap.Reporting;

public sealed record RetainedDatabaseOperationAuditRow(string FactId, string RuleId, string EvidenceTier,
    string SourceIndexId, string? CallerNodeId, string TerminalNodeId, string? OperationKind,
    string State, string ScanId, string CommitSha, string FilePath, int StartLine, int EndLine,
    string? ExtractorId, string? ExtractorVersion);

public sealed record RetainedDatabaseCallAuditRow(string FactId, string RuleId, string EvidenceTier,
    string SourceIndexId, string CallerNodeId, string? TargetSymbol, string FilePath,
    int StartLine, int EndLine, string CandidateName, IReadOnlyList<string> OperationFactIds,
    string ScanId, string CommitSha, string? ExtractorId, string? ExtractorVersion);

public sealed record RetainedDatabaseTerminalAudit(int OperationFactCount, int ProjectedOperationCount,
    int ReachableOperationCallerCount, int ReachableOperationTerminalCount,
    int ReachableNamedCallFactCount, bool RowsTruncated,
    IReadOnlyList<RetainedDatabaseOperationAuditRow> Operations,
    IReadOnlyList<RetainedDatabaseCallAuditRow> NamedCalls)
{
    public string RuleId => "diagnostic.graph.source-database-terminal-audit.v1";
    public string Limitation => "Exact retained source-index/caller/site joins only. Candidate names are spelling observations, not trusted database API classification. Missing operation facts do not establish an extractor defect; unsupported APIs, compiler binding and capture version require separate evidence. Reachability is bounded unfiltered graph reachability, not an accepted path or runtime behavior. Compiled IL admission is independent.";
}

public static partial class CombinedDependencyPathReporter
{
    private static RetainedDatabaseTerminalAudit AuditSourceDatabaseTerminals(
        EvidenceGraph graph, CombinedReadResult read, IReadOnlySet<string> reached, int maxRows)
    {
        var operations = new List<RetainedDatabaseOperationAuditRow>();
        var calls = new List<RetainedDatabaseCallAuditRow>();
        // Include the exact site and target: another operation in the same method
        // must not be used as evidence for this call.
        static string Site(CombinedFactRow f) => System.Text.Json.JsonSerializer.Serialize(new
        { f.SourceIndexId, f.SourceSymbol, f.TargetSymbol, f.FilePath, f.StartLine, f.EndLine });
        var operationFacts = FactsOfTypes(read.Facts, FactTypes.DatabaseOperationCandidate)
            .OrderBy(f => f.CombinedFactId, StringComparer.Ordinal).ToArray();
        var bySite = operationFacts.ToLookup(Site, StringComparer.Ordinal);
        var projected = 0;
        var callersReached = 0;
        var terminalsReached = 0;
        foreach (var fact in operationFacts)
        {
            var caller = string.IsNullOrWhiteSpace(fact.SourceSymbol) ? null : SymbolNodeId(fact.SourceIndexId, fact.SourceSymbol);
            var terminal = SurfaceNodeId(fact.CombinedFactId);
            var exists = graph.Nodes.ContainsKey(terminal);
            var callerReached = caller is not null && reached.Contains(caller);
            var terminalReached = reached.Contains(terminal);
            var attached = caller is not null && graph.Outgoing.TryGetValue(caller, out var edges)
                && edges.Any(e => e.ToNodeId == terminal && e.EdgeKind == "surface-evidence"
                    && e.SupportingFactIds.Contains(fact.CombinedFactId));
            if (exists) projected++;
            if (callerReached) callersReached++;
            if (terminalReached) terminalsReached++;
            var state = !exists ? "operation-fact-without-projected-terminal"
                : !attached ? "projected-terminal-without-exact-caller-edge"
                : !callerReached ? "operation-caller-not-reached"
                : !terminalReached ? "attached-terminal-not-reached-within-diagnostic-bounds"
                : "operation-terminal-reached";
            if (operations.Count < maxRows)
                operations.Add(new(fact.CombinedFactId, fact.RuleId, fact.EvidenceTier, fact.SourceIndexId,
                    caller, terminal, fact.Properties.GetValueOrDefault("operationKind"), state,
                    fact.ScanId, fact.CommitSha, SafePath(fact.FilePath), fact.StartLine, fact.EndLine,
                    fact.ExtractorId, fact.ExtractorVersion));
        }
        var namedCalls = 0;
        foreach (var fact in FactsOfTypes(read.Facts, FactTypes.CallEdge, FactTypes.MethodInvoked)
                     .OrderBy(f => f.CombinedFactId, StringComparer.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(fact.SourceSymbol)) continue;
            var caller = SymbolNodeId(fact.SourceIndexId, fact.SourceSymbol);
            if (!reached.Contains(caller)) continue;
            var name = fact.Properties.GetValueOrDefault("calleeName")
                ?? fact.Properties.GetValueOrDefault("methodName") ?? fact.ContractElement ?? "";
            if (!IsDatabaseOperationSpelling(name)) continue;
            namedCalls++;
            if (calls.Count < maxRows)
                calls.Add(new(fact.CombinedFactId, fact.RuleId, fact.EvidenceTier, fact.SourceIndexId,
                    caller, fact.TargetSymbol, SafePath(fact.FilePath), fact.StartLine, fact.EndLine, name,
                    bySite[Site(fact)].Select(f => f.CombinedFactId).Order(StringComparer.Ordinal).Take(maxRows).ToArray(),
                    fact.ScanId, fact.CommitSha, fact.ExtractorId, fact.ExtractorVersion));
        }
        return new(operationFacts.Length, projected, callersReached, terminalsReached, namedCalls,
            operationFacts.Length > maxRows || namedCalls > maxRows, operations, calls);
    }

    private static bool IsDatabaseOperationSpelling(string name) => name.ToLowerInvariant() is
        "executenonquery" or "executenonqueryasync" or "beginexecutenonquery" or "endexecutenonquery"
        or "executereader" or "executereaderasync" or "beginexecutereader" or "endexecutereader"
        or "executescalar" or "executescalarasync" or "executexmlreader" or "executexmlreaderasync"
        or "beginexecutexmlreader" or "endexecutexmlreader" or "fill" or "fillschema" or "update"
        or "writetoserver" or "writetoserverasync";
}
