using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class WebFormsRawEvidenceAudit
{
    private sealed record BatchWitness(string FactId, string Kind, string? Caller, string? Callee,
        string? FilePath, int StartLine, int EndLine, string RuleId, string Tier);

    private static bool IsKnownUiControlEndpoint(string symbol) => symbol is
        "global::System.Web.UI.WebControls.ListControl.ClearSelection()" or
        "global::System.Web.UI.WebControls.ListItemCollection.Clear()" or
        "global::System.Web.UI.Control.DataBind()" or
        "global::System.Web.UI.WebControls.BaseDataList.DataBind()" or
        "global::System.Web.UI.WebControls.BaseDataBoundControl.DataBind()" or
        "global::Telerik.Web.UI.RadGrid.DataBind()";

    private static string? RetainedTerminalEvidenceFamily(BatchWitness witness)
    {
        if (witness.Tier != EvidenceTiers.Tier1Semantic) return null;
        return witness.Kind switch
        {
            FactTypes.DatabaseOperationCandidate or FactTypes.DbChangeSaved or FactTypes.DapperCallDetected or
                FactTypes.SqlCommandDetected => "database",
            FactTypes.HttpCallDetected => "http",
            FactTypes.CallbackBoundary or FactTypes.AsyncBoundary => "callback-or-async",
            _ => null
        };
    }

    private static void WriteBatchInspection(SqliteConnection db, SqliteTransaction transaction,
        JsonElement root, string scan, string commit, string indexPath, string reportPath, string outputPath,
        string?[] handlers, AuditState[] states, Dictionary<string, SortedSet<string>> edges,
        HashSet<string> loaded, int remainingRows, long remainingTextBytes, List<string> output,
        bool includeEveryResolvedHandler)
    {
        // Read the shared closure once. Keep locations and evidence IDs for every
        // selected call site; do not repeat a database query per handler or leaf.
        var symbols = states.SelectMany(s => s.Visited).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var chains = root.GetProperty("eventChains").EnumerateArray()
            .Where(c => c.TryGetProperty("handlerFactId", out var h) && handlers.Contains(h.GetString(), StringComparer.Ordinal)
                && (includeEveryResolvedHandler ||
                    (c.TryGetProperty("traversalObservation", out var o) && o.ValueKind == JsonValueKind.Object
                     && o.GetProperty("stopState").GetString() == "observed-downstream-without-supported-terminal"
                     && (!c.TryGetProperty("terminalKind", out var t) || t.ValueKind == JsonValueKind.Null || t.GetString() == ""))))
            .ToArray();
        var selectedIds = handlers.Concat(chains.Select(c => c.GetProperty("bindingFactId").GetString()))
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var witnesses = new List<BatchWitness>();
        var rowLimit = Math.Min(20_000, remainingRows);
        long textBytes = 0;
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandTimeout = 30;
            command.CommandText = """
                select fact_id, fact_type, source_symbol, target_symbol, file_path,
                       start_line, end_line, rule_id, evidence_tier
                from facts where scan_id=$scan and commit_sha=$commit and
                (fact_id in (select value from json_each($ids))
                 or source_symbol in (select value from json_each($symbols))
                 or (fact_type='MethodDeclared' and target_symbol in (select value from json_each($symbols))))
                order by file_path, start_line, end_line, target_symbol, fact_id limit $limit
                """;
            command.Parameters.AddWithValue("$scan", scan);
            command.Parameters.AddWithValue("$commit", commit);
            command.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(selectedIds));
            command.Parameters.AddWithValue("$symbols", JsonSerializer.Serialize(symbols));
            command.Parameters.AddWithValue("$limit", rowLimit + 1);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                if (witnesses.Count >= rowLimit) throw new InvalidDataException("RawAuditInputLimit");
                string? Text(int column)
                {
                    if (reader.IsDBNull(column)) return null;
                    var value = reader.GetString(column);
                    textBytes += 2L * value.Length;
                    if (textBytes > remainingTextBytes) throw new InvalidDataException("RawAuditTextLimit");
                    return value;
                }
                witnesses.Add(new(Text(0)!, Text(1)!, Text(2), Text(3), Text(4),
                    reader.IsDBNull(5) ? 0 : reader.GetInt32(5), reader.IsDBNull(6) ? 0 : reader.GetInt32(6), Text(7)!, Text(8)!));
            }
        }

        var byId = witnesses.ToDictionary(w => w.FactId, StringComparer.Ordinal);
        var calls = witnesses.Where(w => (w.Kind is "CallEdge" or "MethodInvoked") && w.Tier == "Tier1Semantic"
            && !string.IsNullOrWhiteSpace(w.Caller) && !string.IsNullOrWhiteSpace(w.Callee)).ToArray();
        var callsByCaller = calls.ToLookup(w => w.Caller!, StringComparer.Ordinal);
        var declarations = witnesses.Where(w => w.Kind == "MethodDeclared" && w.Callee is not null).ToLookup(w => w.Callee!, StringComparer.Ordinal);
        var orderedCases = states.Select((state, index) =>
            {
                var handlerFactId = handlers[index]!;
                var handlerChains = chains.Where(c => c.GetProperty("handlerFactId").GetString() == handlerFactId).ToArray();
                var bindingPath = handlerChains
                    .Select(c => byId.GetValueOrDefault(c.GetProperty("bindingFactId").GetString()!)?.FilePath)
                    .Where(path => !string.IsNullOrWhiteSpace(path)).Order(StringComparer.Ordinal).FirstOrDefault();
                var surfaceId = handlerChains.Select(c => c.GetProperty("surfaceId").GetString())
                    .Where(value => !string.IsNullOrWhiteSpace(value)).Order(StringComparer.Ordinal).FirstOrDefault();
                var rootSymbol = state.Visited.Single(s => !state.Parents.ContainsKey(s));
                return new
                {
                    State = state,
                    HandlerFactId = handlerFactId,
                    HandlerChains = handlerChains,
                    RootSymbol = rootSymbol,
                    ItemSortKey = bindingPath ?? byId.GetValueOrDefault(handlerFactId)?.FilePath ?? surfaceId ?? "item-unavailable"
                };
            })
            .OrderBy(item => item.ItemSortKey, StringComparer.Ordinal)
            .ThenBy(item => item.RootSymbol, StringComparer.Ordinal)
            .ThenBy(item => item.HandlerFactId, StringComparer.Ordinal)
            .ToArray();
        var cases = orderedCases.Select((entry, index) =>
        {
            var state = entry.State;
            var rootSymbol = entry.RootSymbol;
            var stops = state.Visited.Where(s => loaded.Contains(s) && (!edges.TryGetValue(s, out var targets) || targets.Count == 0))
                .Order(StringComparer.Ordinal).ToArray();
            var uiControlEndpoints = stops.Where(IsKnownUiControlEndpoint).ToArray();
            var unresolvedOtherLeaves = stops.Where(s => !IsKnownUiControlEndpoint(s)).ToArray();
            var terminalEvidence = witnesses.Where(w => !string.IsNullOrWhiteSpace(w.Caller) && state.Visited.Contains(w.Caller!) &&
                    RetainedTerminalEvidenceFamily(w) is not null)
                .OrderBy(w => w.FactId, StringComparer.Ordinal).ToArray();
            var terminalEvidenceFamilies = terminalEvidence.Select(RetainedTerminalEvidenceFamily).Where(value => value is not null)
                .Select(value => value!).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            var terminalEvidenceConclusion = terminalEvidenceFamilies.Length switch
            {
                0 => "no-supported-terminal-evidence-observed",
                1 => $"{terminalEvidenceFamilies[0]}-evidence-observed",
                _ => "multiple-terminal-evidence-families-observed"
            };
            var evidenceConclusion = terminalEvidence.Length > 0
                ? unresolvedOtherLeaves.Length == 0
                    ? "retained-terminal-evidence-observed-no-other-unresolved-leaves"
                    : "retained-terminal-evidence-observed-with-unresolved-leaves"
                : uiControlEndpoints.Length == 0
                ? "no-supported-backend-terminal-observed"
                : unresolvedOtherLeaves.Length == 0
                    ? "ui-control-operations-observed-no-other-unresolved-leaves"
                    : "ui-control-operations-observed-with-unresolved-leaves";
            return new
            {
                caseId = $"case-{index + 1:D3}",
                handler = rootSymbol,
                handlerFactId = entry.HandlerFactId,
                chainIds = entry.HandlerChains.Select(chain => chain.TryGetProperty("chainId", out var value) ? value.GetString() : null)
                    .Where(value => value is not null).ToArray(),
                handlerLocation = byId.GetValueOrDefault(entry.HandlerFactId),
                bindings = entry.HandlerChains.Select(c => new
                {
                    surfaceId = c.GetProperty("surfaceId").GetString(),
                    bindingLocation = byId.GetValueOrDefault(c.GetProperty("bindingFactId").GetString()!)
                }).ToArray(),
                bounded = state.Bounded,
                visitedSymbolCount = state.Visited.Count,
                methods = state.Visited.Order(StringComparer.Ordinal).Select(symbol => new
                {
                    symbol,
                    loaded = loaded.Contains(symbol),
                    exactDeclarationLocations = declarations[symbol].ToArray(),
                    incomingCallSites = calls.Where(w => w.Callee == symbol && state.Visited.Contains(w.Caller!)).ToArray(),
                    outgoingCallSites = callsByCaller[symbol].ToArray(),
                    stopReason = !loaded.Contains(symbol) ? "not-loaded-within-audit-bounds"
                        : stops.Contains(symbol, StringComparer.Ordinal) ? "no-retained-exact-semantic-outgoing-call"
                        : "retained-outgoing-calls"
                }).ToArray(),
                stoppingSymbols = stops,
                uiControlEndpoints,
                unresolvedOtherLeaves,
                terminalEvidence,
                terminalEvidenceFamilies,
                terminalEvidenceConclusion,
                evidenceConclusion,
                backendTerminalConclusion = terminalEvidenceFamilies.Any(value => value is "database" or "http")
                    ? "retained-backend-evidence-observed"
                    : "no-supported-backend-terminal-observed",
                reviewResult = "unreviewed"
            };
        }).ToArray();
        var jsonPath = Path.ChangeExtension(outputPath, ".json");
        var markdownPath = Path.ChangeExtension(outputPath, ".md");
        if (File.Exists(jsonPath) || File.Exists(markdownPath)) throw new IOException("Inspection already exists.");
        var jsonOptions = new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        using (var file = new FileStream(jsonPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            JsonSerializer.Serialize(file, new
            {
                schemaVersion = "webforms-batch-inspection.v1",
                ruleId = RuleIds.DiagnosticWebFormsRawExactCallEvidence,
                privacy = "LOCAL ONLY: private paths and symbols; do not share this file or photographs of its contents.",
                generatorSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(typeof(WebFormsRawEvidenceAudit).Assembly.Location))).ToLowerInvariant(),
                sourceReportSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(reportPath))).ToLowerInvariant(),
                sourceIndexSha256 = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(indexPath))).ToLowerInvariant(),
                scanId = scan,
                commitSha = commit,
                sourceReport = Path.GetFullPath(reportPath),
                availability = cases.Length == 0 ? "no-semantic-handler-cases" : "available",
                selectedChainCount = chains.Length,
                selectedHandlerCount = cases.Length,
                scope = includeEveryResolvedHandler
                    ? "Every resolved handler on the selected page; independent bounded exact semantic call closure, not report leaf reconstruction or runtime execution."
                    : "All selected report handlers; independent bounded exact semantic call closure, not report leaf reconstruction or runtime execution.",
                limitations = "Missing calls or exact declarations do not prove absent source. Review every direct handler call, including UI branches. Locations identify retained evidence; use Go To Definition locally. Command values and database execution are not inferred.",
                retainedFactCount = witnesses.Count,
                retainedFactKinds = witnesses.GroupBy(w => w.Kind, StringComparer.Ordinal).OrderBy(group => group.Key, StringComparer.Ordinal)
                    .Select(group => new { factType = group.Key, count = group.Count() }).ToArray(),
                retainedFacts = witnesses,
                cases
            }, jsonOptions);

        static string Safe(string? value) => (value ?? "unavailable").Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal).Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("|", "&#124;", StringComparison.Ordinal).Replace("`", "&#96;", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        static string Location(BatchWitness? w) => w is not null && !string.IsNullOrWhiteSpace(w.FilePath) && w.StartLine > 0
            ? $"{Safe(w.FilePath)}:{w.StartLine}" : "location unavailable";
        using (var file = new FileStream(markdownPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        using (var writer = new StreamWriter(file, new UTF8Encoding(false)))
        {
            writer.WriteLine("# Local review of all terminal-free handlers\n");
            writer.WriteLine("PRIVATE: keep this report and its JSON on the work machine.\n");
            writer.WriteLine($"Selected chains: {chains.Length}; distinct handlers: {cases.Length}; scan: {Safe(scan)}; commit: {Safe(commit)}.\n");
            if (cases.Length == 0) writer.WriteLine("No compiler-resolved handler cases were available for this supplemental exact-semantic review. The primary application workbench remains valid; this is not proof that the application has no handlers or behavior.\n");
            writer.WriteLine("For each case, the evidence conclusion distinguishes exact allowlisted UI/control endpoints from other unresolved leaves. Open unresolved leaves only when a stronger manual conclusion is needed. Locations identify call sites; use Go To Definition in Visual Studio to inspect the callee. This report lists retained static calls; it does not establish execution, source completeness, or absence of backend behavior.\n");
            writer.WriteLine($"Retained page-closure facts: {witnesses.Count}. The JSON includes their fact IDs, kinds, symbols, locations, rules, and tiers. Raw source and arbitrary fact properties are intentionally omitted.\n");
            writer.WriteLine("Share only case IDs and your result category: ui-only, database-call-present, source-call-missing, definition-unavailable, checkout-mismatch, or uncertain. Add only a generic description of a missing operation.\n");
            foreach (var item in cases)
            {
                writer.WriteLine($"## {item.caseId}\n");
                writer.WriteLine($"Handler: {Safe(item.handler)} — {Location(item.handlerLocation)}\n");
                foreach (var binding in item.bindings) writer.WriteLine($"Binding: {Location(binding.bindingLocation)}\n");
                writer.WriteLine($"Traversal limit reached: {item.bounded}; visited symbols: {item.visitedSymbolCount}. Case IDs are local to this report.\n");
                writer.WriteLine($"Evidence conclusion: **{item.evidenceConclusion}**\n");
                writer.WriteLine($"Observed UI/control endpoints: {item.uiControlEndpoints.Length}; other unresolved leaves: {item.unresolvedOtherLeaves.Length}; retained terminal evidence: {item.terminalEvidence.Length} ({string.Join(", ", item.terminalEvidenceFamilies)}); terminal conclusion: {item.terminalEvidenceConclusion}.\n");
                writer.WriteLine("Manual result: **unreviewed**\n");
                writer.WriteLine("### Direct handler calls\n");
                foreach (var call in callsByCaller[item.handler].GroupBy(w => new { w.Callee, w.FilePath, w.StartLine, w.EndLine }))
                    writer.WriteLine($"- {Safe(call.Key.Callee)} — {Location(call.First())}");
                writer.WriteLine("\n### Stopping calls\n");
                foreach (var method in item.methods.Where(m => m.stopReason != "retained-outgoing-calls"))
                {
                    writer.WriteLine($"- {Safe(method.symbol)} — {method.stopReason}");
                    foreach (var call in method.incomingCallSites.GroupBy(w => new { w.Caller, w.FilePath, w.StartLine, w.EndLine }))
                        writer.WriteLine($"  - Called by {Safe(call.Key.Caller)} at {Location(call.First())}");
                    foreach (var declaration in method.exactDeclarationLocations)
                        writer.WriteLine($"  - Exact declaration evidence: {Location(declaration)}");
                }
                writer.WriteLine("\n### All retained call sites\n");
                writer.WriteLine("| Caller | Callee | Call-site location | Rule | Tier |\n|---|---|---|---|---|");
                foreach (var method in item.methods)
                    foreach (var call in method.outgoingCallSites)
                        writer.WriteLine($"| {Safe(call.Caller)} | {Safe(call.Callee)} | {Location(call)} | {Safe(call.RuleId)} | {Safe(call.Tier)} |");
                writer.WriteLine();
            }
        }
        output.Add($"batchInspection=created|chains={chains.Length}|handlers={cases.Length}|boundedHandlers={cases.Count(c => c.bounded)}");
        if (cases.Length == 0) output.Add("batchReview=not-applicable;reason=no-semantic-handler-cases;primary-workbench-remains-valid");
        foreach (var item in cases)
            output.Add($"case={item.caseId}|bounded={item.bounded.ToString().ToLowerInvariant()}|symbols={item.visitedSymbolCount}|directCallSites={callsByCaller[item.handler].Select(w => (w.Callee, w.FilePath, w.StartLine, w.EndLine)).Distinct().Count()}|stoppingSymbols={item.stoppingSymbols.Length}|uiControlEndpoints={item.uiControlEndpoints.Length}|unresolvedOtherLeaves={item.unresolvedOtherLeaves.Length}|terminalEvidence={item.terminalEvidence.Length}|terminalFamilies={string.Join(",", item.terminalEvidenceFamilies)}|terminalConclusion={item.terminalEvidenceConclusion}|evidence={item.evidenceConclusion}|review=unreviewed");
        if (cases.Length > 0) output.Add("batchReview=read-private-markdown;share-only-case-ids-and-result-categories");
    }
}
