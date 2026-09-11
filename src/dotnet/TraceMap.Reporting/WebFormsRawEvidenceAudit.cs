using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

/// <summary>Read-only diagnostic, independent of report graph compaction and terminal classification.</summary>
public static partial class WebFormsRawEvidenceAudit
{
    public static IReadOnlyList<string> Run(string indexPath, string reportPath, int maxRows = 500_000, int maxTextBytes = 64 * 1024 * 1024, string? inspectionPath = null, string? startingMethodName = null, bool inspectAllHandlers = false)
    {
        if (inspectAllHandlers && (inspectionPath is null || startingMethodName is not null)) throw new InvalidDataException("RawAuditInvalidLimit");
        if (maxRows < 1 || maxRows > 500_000) throw new InvalidDataException("RawAuditInvalidLimit");
        if (maxTextBytes < 1 || maxTextBytes > 64 * 1024 * 1024) throw new InvalidDataException("RawAuditInvalidLimit");
        if (new FileInfo(reportPath).Length > 128 * 1024 * 1024) throw new InvalidDataException("RawAuditReportLimit");
        using var packet = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = packet.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "webforms-modernization-packet.v1")
            throw new InvalidDataException("RawAuditSchemaMismatch");
        var sources = root.GetProperty("sources");
        if (sources.GetArrayLength() != 1) throw new InvalidDataException("RawAuditSourceMismatch");
        var source = sources[0];
        var scan = source.GetProperty("scanId").GetString();
        var commit = source.GetProperty("commitSha").GetString();
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = indexPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString());
        db.Open();
        using var transaction = db.BeginTransaction(deferred: true);
        using (var identity = db.CreateCommand())
        {
            identity.Transaction = transaction;
            identity.CommandText = "select scan_id, commit_sha from scan_manifest";
            using var reader = identity.ExecuteReader();
            if (!reader.Read() || reader.GetString(0) != scan || reader.GetString(1) != commit || reader.Read())
                throw new InvalidDataException("RawAuditProvenanceMismatch");
        }

        static bool IsPriorityChain(JsonElement c) =>
            (!c.TryGetProperty("terminalKind", out var t) || t.ValueKind == JsonValueKind.Null || t.GetString() == "")
                && c.TryGetProperty("traversalObservation", out var o) && o.ValueKind == JsonValueKind.Object
                && o.GetProperty("stopState").GetString() == "observed-downstream-without-supported-terminal";
        var handlers = root.GetProperty("eventChains").EnumerateArray()
            .Where(IsPriorityChain)
            .Select(c => c.GetProperty("handlerFactId").GetString()).Where(id => !string.IsNullOrEmpty(id))
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        if (startingMethodName is not null) handlers = [];
        if (handlers.Length > 32) throw new InvalidDataException("RawAuditHandlerLimit");
        var seeds = new List<string>();
        if (startingMethodName is not null)
        {
            if (!System.Text.RegularExpressions.Regex.IsMatch(startingMethodName, "^[A-Za-z_][A-Za-z0-9_.]{0,255}$"))
                throw new InvalidDataException("RawAuditMethodHintInvalid");
            using var cmd = db.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = """
                select source_symbol, target_symbol from facts
                where scan_id=$scan and commit_sha=$commit and evidence_tier='Tier1Semantic'
                and fact_type in ('CallEdge','MethodInvoked')
                and (instr(source_symbol,$hint)>0 or instr(target_symbol,$hint)>0) limit 50001
                """;
            cmd.Parameters.AddWithValue("$scan", scan!);
            cmd.Parameters.AddWithValue("$commit", commit!);
            cmd.Parameters.AddWithValue("$hint", startingMethodName);
            var candidates = new HashSet<string>(StringComparer.Ordinal);
            using var reader = cmd.ExecuteReader();
            var count = 0;
            long textBytes = 0;
            while (reader.Read())
            {
                if (++count > 50_000) throw new InvalidDataException("RawAuditInputLimit");
                for (var column = 0; column < 2; column++)
                {
                    if (reader.IsDBNull(column)) continue;
                    var symbol = reader.GetString(column);
                    textBytes += 2L * symbol.Length;
                    if (textBytes > maxTextBytes) throw new InvalidDataException("RawAuditTextLimit");
                    var paren = symbol.IndexOf('(');
                    if (paren < 1) continue;
                    var name = symbol[..paren];
                    if (name == startingMethodName || name.EndsWith("." + startingMethodName, StringComparison.Ordinal)) candidates.Add(symbol);
                    if (candidates.Count > 1) throw new InvalidDataException("RawAuditMethodAmbiguous");
                }
            }
            if (candidates.Count != 1) throw new InvalidDataException("RawAuditMethodNotFound");
            seeds.Add(candidates.Single());
        }
        foreach (var id in handlers)
        {
            using var cmd = db.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = "select target_symbol, properties_json from facts where fact_id=$id and scan_id=$scan and commit_sha=$commit and fact_type='WebFormsHandlerResolved'";
            cmd.Parameters.AddWithValue("$id", id!);
            cmd.Parameters.AddWithValue("$scan", scan!);
            cmd.Parameters.AddWithValue("$commit", commit!);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) throw new InvalidDataException("RawAuditHandlerNotFound");
            using var props = JsonDocument.Parse(r.GetString(1));
            string? symbol = null;
            foreach (var key in new[] { "handlerSymbol", "handlerSymbolId" })
                if (props.RootElement.TryGetProperty(key, out var value) && !string.IsNullOrWhiteSpace(value.GetString()))
                { symbol = value.GetString(); break; }
            symbol ??= r.IsDBNull(0) ? null : r.GetString(0);
            if (string.IsNullOrWhiteSpace(symbol)) throw new InvalidDataException("RawAuditHandlerSymbolMissing");
            seeds.Add(symbol);
        }
        var output = new List<string> { "raw-webforms-evidence=completed", "provenance=matched", $"selectedHandlers={seeds.Count}",
            $"rule={RuleIds.DiagnosticWebFormsRawExactCallEvidence}", "scope=independent-exact-semantic-call-closure-not-report-leaves" };
        output.Add(startingMethodName is null ? "selection=report-handlers" : "selection=unique-method-hint;not-page-or-event-selection");
        if (seeds.Count == 0)
        {
            if (inspectionPath is not null) throw new InvalidDataException("RawAuditInspectionUnavailable");
            return output;
        }

        var evidence = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var watch = Stopwatch.StartNew();
        long bytes = 0;
        var rows = 0;
        var loaded = new HashSet<string>(StringComparer.Ordinal);
        void LoadSymbols(IEnumerable<string> symbols)
        {
            var selected = symbols.Where(s => !loaded.Contains(s)).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if (selected.Length == 0) return;
            if (watch.Elapsed > TimeSpan.FromSeconds(60)) throw new InvalidDataException("RawAuditInputLimit");
            using var cmd = db.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandTimeout = 30;
            cmd.CommandText = """
                select fact_type, source_symbol, target_symbol, evidence_tier from facts
                where scan_id=$scan and commit_sha=$commit and fact_type in
                ('CallEdge','MethodInvoked','MethodDeclared')
                and ((fact_type='MethodDeclared' and target_symbol in (select value from json_each($symbols)))
                  or (fact_type in ('CallEdge','MethodInvoked') and source_symbol in (select value from json_each($symbols))))
                limit $limit
                """;
            cmd.Parameters.AddWithValue("$scan", scan!);
            cmd.Parameters.AddWithValue("$commit", commit!);
            cmd.Parameters.AddWithValue("$symbols", JsonSerializer.Serialize(selected));
            cmd.Parameters.AddWithValue("$limit", maxRows - rows + 1);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (++rows > maxRows || watch.Elapsed > TimeSpan.FromSeconds(60)) throw new InvalidDataException("RawAuditInputLimit");
                var type = r.GetString(0);
                var from = r.IsDBNull(1) ? null : r.GetString(1);
                var to = r.IsDBNull(2) ? null : r.GetString(2);
                var tier = r.GetString(3);
                bytes += 2L * ((from?.Length ?? 0) + (to?.Length ?? 0));
                if (bytes > maxTextBytes) throw new InvalidDataException("RawAuditTextLimit");
                var owner = type == "MethodDeclared" ? to : from;
                if (!string.IsNullOrEmpty(owner))
                {
                    if (!evidence.TryGetValue(owner, out var kinds)) evidence[owner] = kinds = new(StringComparer.Ordinal);
                    kinds.Add(type + ":" + (tier == "Tier1Semantic" ? "semantic" : "nonsemantic"));
                }
                // No bare-name reconciliation, syntax projection, or runtime dispatch inference.
                if (tier == "Tier1Semantic" && type is "CallEdge" or "MethodInvoked" && !string.IsNullOrEmpty(from) && !string.IsNullOrEmpty(to))
                {
                    if (!edges.TryGetValue(from, out var targets)) edges[from] = targets = new(StringComparer.Ordinal);
                    targets.Add(to);
                }
            }
            loaded.UnionWith(selected);
        }
        // Batch all handlers' current frontiers: at most twelve fact queries,
        // not one whole-index materialization or one query per visited symbol.
        var states = seeds.Select(seed => new AuditState(seed)).ToArray();
        for (var depth = 0; depth <= 10; depth++)
        {
            LoadSymbols(states.SelectMany(s => s.Frontier));
            foreach (var state in states)
            {
                var next = new List<string>();
                foreach (var symbol in state.Frontier)
                {
                    if (!edges.TryGetValue(symbol, out var targets)) continue;
                    foreach (var target in targets)
                    {
                        if (++state.Work > 10_000) { state.Bounded = true; break; }
                        if (state.Visited.Contains(target)) continue;
                        if (depth >= 10 || state.Visited.Count >= 500) { state.Bounded = true; continue; }
                        state.Visited.Add(target);
                        state.Parents[target] = symbol;
                        next.Add(target);
                    }
                    if (state.Work > 10_000) break;
                }
                state.Frontier = state.Work > 10_000 ? [] : next;
            }
        }
        // A work-bound stop may leave admitted symbols unexpanded. Read their
        // witnesses once for the summary without expanding their outgoing edges.
        LoadSymbols(states.SelectMany(s => s.Visited));
        output.Add($"rawFactRows={rows}");
        output.Add($"rawSymbolsQueried={loaded.Count}");
        var n = 0;
        foreach (var state in states)
        {
            var visited = state.Visited;
            var bounded = state.Bounded;
            bool Has(string symbol, string kind) => evidence.TryGetValue(symbol, out var kinds) && kinds.Contains(kind);
            output.Add($"handler={++n:D3}|symbols={visited.Count}|bounded={bounded.ToString().ToLowerInvariant()}|semanticInvocationSources={visited.Count(s => Has(s, "MethodInvoked:semantic"))}|semanticCallSources={visited.Count(s => Has(s, "CallEdge:semantic"))}|invocationWithoutCallFact={visited.Count(s => Has(s, "MethodInvoked:semantic") && !Has(s, "CallEdge:semantic"))}|exactDeclarationTargets={visited.Count(s => Has(s, "MethodDeclared:semantic") || Has(s, "MethodDeclared:nonsemantic"))}|withoutSelectedSourceWitness={visited.Count(s => !evidence.ContainsKey(s))}");
        }
        output.Add("nonClaim=not-report-leaf-identities;not-runtime-execution;missing-exact-witness-is-not-source-absence;declaration-targets-may-use-different-symbol-format");
        if (inspectAllHandlers)
        {
            WriteBatchInspection(db, transaction, root, scan!, commit!, reportPath, inspectionPath!, handlers!, states, edges, loaded,
                maxRows - rows, maxTextBytes - bytes, output);
            return output;
        }
        if (inspectionPath is not null)
        {
            // Pick one reproducible, unbounded exact-call stopping point, not a
            // guessed source definition or a purported report terminal.
            var selected = states.Select((state, index) => new { State = state, Index = index })
                .Where(s => !s.State.Bounded)
                .SelectMany(s => s.State.Visited.Where(symbol => symbol != seeds[s.Index]
                    && (!edges.TryGetValue(symbol, out var targets) || targets.Count == 0))
                    .OrderBy(symbol => symbol.Contains(".Fill(", StringComparison.Ordinal) ? 0 : 1)
                    .ThenBy(symbol => symbol, StringComparer.Ordinal).Select(symbol => new { s.State, s.Index, Symbol = symbol }))
                .FirstOrDefault();
            if (selected is null) throw new InvalidDataException("RawAuditInspectionUnavailable");
            JsonElement? chain = startingMethodName is null ? root.GetProperty("eventChains").EnumerateArray().First(c =>
                IsPriorityChain(c) && c.GetProperty("handlerFactId").GetString() == handlers[selected.Index]) : null;
            var symbols = new List<string> { selected.Symbol };
            while (selected.State.Parents.TryGetValue(symbols[^1], out var parent)) symbols.Add(parent);
            symbols.Reverse();

            object Location(string? factId, string? caller = null, string? callee = null)
            {
                using var cmd = db.CreateCommand();
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    select fact_id, file_path, start_line, end_line, rule_id, evidence_tier
                    from facts where scan_id=$scan and commit_sha=$commit and
                    (($id is not null and fact_id=$id) or
                     ($id is null and source_symbol=$caller and target_symbol=$callee
                      and evidence_tier='Tier1Semantic' and fact_type in ('CallEdge','MethodInvoked')))
                    order by fact_id limit 1
                    """;
                cmd.Parameters.AddWithValue("$scan", scan!);
                cmd.Parameters.AddWithValue("$commit", commit!);
                cmd.Parameters.AddWithValue("$id", (object?)factId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$caller", (object?)caller ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$callee", (object?)callee ?? DBNull.Value);
                using var r = cmd.ExecuteReader();
                if (!r.Read() || r.IsDBNull(1) || string.IsNullOrWhiteSpace(r.GetString(1))
                    || r.IsDBNull(2) || r.IsDBNull(3) || r.GetInt32(2) < 1 || r.GetInt32(3) < r.GetInt32(2))
                    return new { availability = "source-location-unavailable" };
                return new
                {
                    availability = "retained-evidence-location",
                    factId = r.GetString(0),
                    filePath = r.GetString(1),
                    startLine = r.GetInt32(2),
                    endLine = r.GetInt32(3),
                    ruleId = r.GetString(4),
                    evidenceTier = r.GetString(5)
                };
            }
            var hops = symbols.Zip(symbols.Skip(1), (caller, callee) => new
            {
                caller,
                callee,
                locationKind = "call-site-not-callee-definition",
                location = Location(null, caller, callee)
            }).ToArray();
            var directFacts = new List<DirectCallWitness>();
            using (var cmd = db.CreateCommand())
            {
                cmd.Transaction = transaction;
                cmd.CommandText = """
                    select fact_id, target_symbol, file_path, start_line, end_line, rule_id, evidence_tier
                    from facts where scan_id=$scan and commit_sha=$commit and source_symbol=$handler
                    and evidence_tier='Tier1Semantic' and fact_type in ('CallEdge','MethodInvoked')
                    and target_symbol is not null and target_symbol<>''
                    order by file_path, start_line, end_line, target_symbol, fact_id limit $limit
                    """;
                cmd.Parameters.AddWithValue("$scan", scan!);
                cmd.Parameters.AddWithValue("$commit", commit!);
                cmd.Parameters.AddWithValue("$handler", symbols[0]);
                cmd.Parameters.AddWithValue("$limit", Math.Min(maxRows, 2000) + 1);
                using var r = cmd.ExecuteReader();
                while (r.Read())
                {
                    if (directFacts.Count >= Math.Min(maxRows, 2000)) throw new InvalidDataException("RawAuditInputLimit");
                    directFacts.Add(new(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                        r.IsDBNull(3) ? 0 : r.GetInt32(3), r.IsDBNull(4) ? 0 : r.GetInt32(4), r.GetString(5), r.GetString(6)));
                }
            }
            var branchCache = new Dictionary<string, object>(StringComparer.Ordinal);
            object Branch(string callee)
            {
                if (branchCache.TryGetValue(callee, out var cached)) return cached;
                var seen = new HashSet<string>(StringComparer.Ordinal) { callee };
                var pending = new Queue<string>();
                pending.Enqueue(callee);
                var stops = new SortedSet<string>(StringComparer.Ordinal);
                var partial = false;
                var work = 0;
                while (pending.TryDequeue(out var node))
                {
                    if (!loaded.Contains(node)) { partial = true; continue; }
                    if (!edges.TryGetValue(node, out var next) || next.Count == 0) { stops.Add(node); continue; }
                    foreach (var target in next)
                    {
                        if (++work > 10_000) { partial = true; break; }
                        if (seen.Contains(target)) continue;
                        if (seen.Count >= 500) { partial = true; continue; }
                        seen.Add(target);
                        pending.Enqueue(target);
                    }
                    if (work > 10_000) break;
                }
                var result = new
                {
                    status = partial ? "bounded-or-unloaded-branch" : stops.Count == 0 ? "no-leaf-in-retained-call-graph" : "retained-stopping-symbols-found",
                    stoppingSymbols = stops.ToArray(),
                    meaning = "Each stopping symbol has no selected exact semantic outgoing call evidence. It is not proof of no runtime calls or a database terminal. Cycles are visited once."
                };
                branchCache[callee] = result;
                return result;
            }
            var directCalls = directFacts.GroupBy(f => new { f.Callee, f.FilePath, f.StartLine, f.EndLine })
                .Select((group, index) => new
                {
                    callSiteOrdinal = index + 1,
                    caller = symbols[0],
                    callee = group.Key.Callee,
                    locationKind = "call-site-not-callee-definition",
                    locationAvailability = !string.IsNullOrWhiteSpace(group.Key.FilePath) && group.Key.StartLine > 0 && group.Key.EndLine >= group.Key.StartLine
                        ? "retained-evidence-location" : "source-location-unavailable",
                    filePath = group.Key.FilePath,
                    startLine = group.Key.StartLine,
                    endLine = group.Key.EndLine,
                    witnesses = group.Select(f => new { factId = f.FactId, ruleId = f.RuleId, evidenceTier = f.EvidenceTier }).ToArray(),
                    branch = Branch(group.Key.Callee)
                }).ToArray();
            var privateReport = new
            {
                privacy = "LOCAL ONLY: contains private paths and symbols. Do not share this file or photograph its contents.",
                schemaVersion = "webforms-local-inspection.v1",
                ruleId = RuleIds.DiagnosticWebFormsRawExactCallEvidence,
                scanId = scan,
                commitSha = commit,
                sourceReport = Path.GetFullPath(reportPath),
                instructions = "Start with directCalls: compare ALL retained direct call sites against the selected event handler in Visual Studio. Entries are source-location order, NOT proven execution order. Each entry has its own branch summary; a finished UI-only branch does not end the parent handler. Locations are CALL SITES, not callee definitions. Use Go To Definition locally. Confirm the checkout matches the recorded commit. Do not execute the application or call a database.",
                selection = startingMethodName is null ? "report-handler" : "unique-method-hint-not-event-handler",
                surfaceId = chain?.GetProperty("surfaceId").GetString(),
                bindingLocation = Location(chain?.GetProperty("bindingFactId").GetString()),
                handler = symbols[0],
                handlerEvidenceLocation = Location(startingMethodName is null ? handlers[selected.Index] : null),
                directCallSiteCount = directCalls.Length,
                directCalls,
                directCallScope = "All retained exact Tier1 CallEdge/MethodInvoked sites owned by this handler; not a claim that every source call was extracted. Same-target same-span witnesses are grouped; repeated sites on different lines remain separate.",
                samplePathScope = "The following hops are only ONE sample branch, not the complete handler sequence. See directCalls for sibling calls.",
                sampleSelection = "Prefer a retained stopping symbol named Fill, otherwise ordinal first. Name preference is not proof of a database operation. Method hints require one exact semantic symbol candidate; no page binding is inferred.",
                hops,
                stoppingSymbol = selected.Symbol,
                stoppingReason = "no-selected-exact-semantic-call-or-invocation-outgoing-edge",
                stoppingDefinitionLocation = "not-established-use-go-to-definition-locally",
                nonClaim = "Independent raw-call sample, not a report leaf reconstruction. Missing exact edges do not prove absent source, absent runtime behavior, or an external method.",
                shareBackOnly = "Report direct-call-list-matches-source, source-call-missing-from-list, definition-unavailable, or checkout-mismatch. If a call is missing, describe only its kind (application method, framework control operation, database operation). Do not send private names, paths, SQL, or source."
            };
            using var file = new FileStream(inspectionPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(file, privateReport, new JsonSerializerOptions { WriteIndented = true });
            output.Add("localInspection=created;selectedSamples=1;keep-file-on-work-machine");
            output.Add($"localInspectionDirectCallSites={directCalls.Length}");
        }
        return output;
    }

    private sealed record DirectCallWitness(string FactId, string Callee, string? FilePath, int StartLine, int EndLine, string RuleId, string EvidenceTier);

    private sealed class AuditState(string seed)
    {
        public HashSet<string> Visited { get; } = new(StringComparer.Ordinal) { seed };
        public List<string> Frontier { get; set; } = [seed];
        public Dictionary<string, string> Parents { get; } = new(StringComparer.Ordinal);
        public int Work { get; set; }
        public bool Bounded { get; set; }
    }
}
