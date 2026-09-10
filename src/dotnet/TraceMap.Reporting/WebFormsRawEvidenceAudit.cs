using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

/// <summary>Read-only diagnostic, independent of report graph compaction and terminal classification.</summary>
public static class WebFormsRawEvidenceAudit
{
    public static IReadOnlyList<string> Run(string indexPath, string reportPath, int maxRows = 500_000, int maxTextBytes = 64 * 1024 * 1024, string? inspectionPath = null)
    {
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
        if (handlers.Length > 32) throw new InvalidDataException("RawAuditHandlerLimit");
        var seeds = new List<string>();
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
            "rule=diagnostic.webforms.raw-exact-call-evidence.v1", "scope=independent-exact-semantic-call-closure-not-report-leaves" };
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
        if (inspectionPath is not null)
        {
            // Pick one reproducible, unbounded exact-call stopping point, not a
            // guessed source definition or a purported report terminal.
            var selected = states.Select((state, index) => new { State = state, Index = index })
                .Where(s => !s.State.Bounded)
                .SelectMany(s => s.State.Visited.Where(symbol => symbol != seeds[s.Index]
                    && (!edges.TryGetValue(symbol, out var targets) || targets.Count == 0))
                    .Order(StringComparer.Ordinal).Select(symbol => new { s.State, s.Index, Symbol = symbol }))
                .FirstOrDefault();
            if (selected is null) throw new InvalidDataException("RawAuditInspectionUnavailable");
            var chain = root.GetProperty("eventChains").EnumerateArray().First(c =>
                IsPriorityChain(c) && c.GetProperty("handlerFactId").GetString() == handlers[selected.Index]);
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
            var privateReport = new
            {
                privacy = "LOCAL ONLY: contains private paths and symbols. Do not share this file or photograph its contents.",
                schemaVersion = "webforms-local-inspection.v1",
                ruleId = "diagnostic.webforms.raw-exact-call-evidence.v1",
                scanId = scan,
                commitSha = commit,
                sourceReport = Path.GetFullPath(reportPath),
                instructions = "Open the last hop's file at startLine in Visual Studio using your application checkout. This is the CALL SITE. Use Go To Definition locally to inspect the callee. Confirm the checkout matches the recorded commit. Do not execute the application or call a database for this inspection.",
                surfaceId = chain.GetProperty("surfaceId").GetString(),
                bindingLocation = Location(chain.GetProperty("bindingFactId").GetString()),
                handler = symbols[0],
                handlerEvidenceLocation = Location(handlers[selected.Index]),
                hops,
                stoppingSymbol = selected.Symbol,
                stoppingReason = "no-selected-exact-semantic-call-or-invocation-outgoing-edge",
                stoppingDefinitionLocation = "not-established-use-go-to-definition-locally",
                nonClaim = "Independent raw-call sample, not a report leaf reconstruction. Missing exact edges do not prove absent source, absent runtime behavior, or an external method.",
                shareBackOnly = "Report one category: source-body-found, metadata-or-external-definition, generated-source, definition-unavailable, or checkout-mismatch. Say whether the body contains another call, a database operation, or neither; do not send private names, paths, SQL, or source."
            };
            using var file = new FileStream(inspectionPath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(file, privateReport, new JsonSerializerOptions { WriteIndented = true });
            output.Add("localInspection=created;selectedSamples=1;keep-file-on-work-machine");
        }
        return output;
    }

    private sealed class AuditState(string seed)
    {
        public HashSet<string> Visited { get; } = new(StringComparer.Ordinal) { seed };
        public List<string> Frontier { get; set; } = [seed];
        public Dictionary<string, string> Parents { get; } = new(StringComparer.Ordinal);
        public int Work { get; set; }
        public bool Bounded { get; set; }
    }
}
