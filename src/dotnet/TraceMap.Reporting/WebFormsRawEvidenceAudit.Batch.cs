using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public static partial class WebFormsRawEvidenceAudit
{
    private sealed record BatchWitness(string FactId, string Kind, string? Caller, string? Callee,
        string? FilePath, int StartLine, int EndLine, string RuleId, string Tier);

    private static void WriteBatchInspection(SqliteConnection db, SqliteTransaction transaction,
        JsonElement root, string scan, string commit, string reportPath, string outputPath,
        string?[] handlers, AuditState[] states, Dictionary<string, SortedSet<string>> edges,
        HashSet<string> loaded, int remainingRows, long remainingTextBytes, List<string> output)
    {
        // Read the shared closure once. Keep locations and evidence IDs for every
        // selected call site; do not repeat a database query per handler or leaf.
        var symbols = states.SelectMany(s => s.Visited).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        var chains = root.GetProperty("eventChains").EnumerateArray()
            .Where(c => c.TryGetProperty("handlerFactId", out var h) && handlers.Contains(h.GetString(), StringComparer.Ordinal)
                && c.TryGetProperty("traversalObservation", out var o) && o.ValueKind == JsonValueKind.Object
                && o.GetProperty("stopState").GetString() == "observed-downstream-without-supported-terminal"
                && (!c.TryGetProperty("terminalKind", out var t) || t.ValueKind == JsonValueKind.Null || t.GetString() == ""))
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
                 or (fact_type in ('CallEdge','MethodInvoked') and evidence_tier='Tier1Semantic'
                     and source_symbol in (select value from json_each($symbols)))
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
        var calls = witnesses.Where(w => w.Kind is "CallEdge" or "MethodInvoked" && !string.IsNullOrWhiteSpace(w.Caller) && !string.IsNullOrWhiteSpace(w.Callee)).ToArray();
        var callsByCaller = calls.ToLookup(w => w.Caller!, StringComparer.Ordinal);
        var declarations = witnesses.Where(w => w.Kind == "MethodDeclared" && w.Callee is not null).ToLookup(w => w.Callee!, StringComparer.Ordinal);
        var cases = states.Select((state, index) =>
        {
            var rootSymbol = state.Visited.Single(s => !state.Parents.ContainsKey(s));
            var stops = state.Visited.Where(s => loaded.Contains(s) && (!edges.TryGetValue(s, out var targets) || targets.Count == 0))
                .Order(StringComparer.Ordinal).ToArray();
            return new
            {
                caseId = $"case-{index + 1:D3}",
                handler = rootSymbol,
                handlerFactId = handlers[index],
                handlerLocation = byId.GetValueOrDefault(handlers[index]!),
                bindings = chains.Where(c => c.GetProperty("handlerFactId").GetString() == handlers[index]).Select(c => new
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
                ruleId = "diagnostic.webforms.raw-exact-call-evidence.v1",
                privacy = "LOCAL ONLY: private paths and symbols; do not share this file or photographs of its contents.",
                scanId = scan,
                commitSha = commit,
                sourceReport = Path.GetFullPath(reportPath),
                selectedChainCount = chains.Length,
                selectedHandlerCount = cases.Length,
                scope = "All selected report handlers; independent bounded exact semantic call closure, not report leaf reconstruction or runtime execution.",
                limitations = "Missing calls or exact declarations do not prove absent source. Review every direct handler call, including UI branches. Locations identify retained evidence; use Go To Definition locally. Command values and database execution are not inferred.",
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
            writer.WriteLine("For each case, open the handler location, review all direct calls, then use the stopping-call locations to inspect the leaves. Locations identify call sites; use Go To Definition in Visual Studio to inspect the callee. Complete the result line once. A UI-only handler may legitimately have no database terminal. This report lists retained static calls; it does not establish execution or source completeness.\n");
            writer.WriteLine("Share only case IDs and your result category: ui-only, database-call-present, source-call-missing, definition-unavailable, checkout-mismatch, or uncertain. Add only a generic description of a missing operation.\n");
            foreach (var item in cases)
            {
                writer.WriteLine($"## {item.caseId}\n");
                writer.WriteLine($"Handler: {Safe(item.handler)} — {Location(item.handlerLocation)}\n");
                foreach (var binding in item.bindings) writer.WriteLine($"Binding: {Location(binding.bindingLocation)}\n");
                writer.WriteLine($"Audit bounded: {item.bounded}; visited symbols: {item.visitedSymbolCount}. Case IDs are local to this report.\n");
                writer.WriteLine("Result: **unreviewed**\n");
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
        foreach (var item in cases)
            output.Add($"case={item.caseId}|bounded={item.bounded.ToString().ToLowerInvariant()}|symbols={item.visitedSymbolCount}|directCallSites={callsByCaller[item.handler].Select(w => (w.Callee, w.FilePath, w.StartLine, w.EndLine)).Distinct().Count()}|stoppingSymbols={item.stoppingSymbols.Length}|review=unreviewed");
        output.Add("batchReview=read-private-markdown;share-only-case-ids-and-result-categories");
    }
}
