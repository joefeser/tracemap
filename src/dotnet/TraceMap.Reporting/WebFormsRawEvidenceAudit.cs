using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

/// <summary>Read-only diagnostic, independent of report graph compaction and terminal classification.</summary>
public static class WebFormsRawEvidenceAudit
{
    public static IReadOnlyList<string> Run(string indexPath, string reportPath, int maxRows = 500_000)
    {
        if (maxRows < 1 || maxRows > 500_000) throw new InvalidDataException("RawAuditInvalidLimit");
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

        var handlers = root.GetProperty("eventChains").EnumerateArray()
            .Where(c => (!c.TryGetProperty("terminalKind", out var t) || t.ValueKind == JsonValueKind.Null || t.GetString() == "")
                && c.TryGetProperty("traversalObservation", out var o) && o.ValueKind == JsonValueKind.Object
                && o.GetProperty("stopState").GetString() == "observed-downstream-without-supported-terminal")
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
        if (seeds.Count == 0) return output;

        var evidence = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var edges = new Dictionary<string, SortedSet<string>>(StringComparer.Ordinal);
        var watch = Stopwatch.StartNew();
        long bytes = 0;
        var rows = 0;
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandTimeout = 30;
            cmd.CommandText = """
                select fact_type, source_symbol, target_symbol, evidence_tier from facts
                where scan_id=$scan and commit_sha=$commit and fact_type in
                ('CallEdge','MethodInvoked','MethodDeclared')
                limit $limit
                """;
            cmd.Parameters.AddWithValue("$scan", scan!);
            cmd.Parameters.AddWithValue("$commit", commit!);
            cmd.Parameters.AddWithValue("$limit", maxRows + 1);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                if (++rows > maxRows || watch.Elapsed > TimeSpan.FromSeconds(60)) throw new InvalidDataException("RawAuditInputLimit");
                var type = r.GetString(0);
                var from = r.IsDBNull(1) ? null : r.GetString(1);
                var to = r.IsDBNull(2) ? null : r.GetString(2);
                var tier = r.GetString(3);
                bytes += 2L * ((from?.Length ?? 0) + (to?.Length ?? 0));
                if (bytes > 64 * 1024 * 1024) throw new InvalidDataException("RawAuditTextLimit");
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
        }
        output.Add($"rawFactRows={rows}");
        var n = 0;
        foreach (var seed in seeds)
        {
            var visited = new HashSet<string>(StringComparer.Ordinal) { seed };
            var queue = new Queue<(string Symbol, int Depth)>();
            queue.Enqueue((seed, 0));
            var bounded = false;
            var work = 0;
            while (queue.TryDequeue(out var item))
            {
                if (!edges.TryGetValue(item.Symbol, out var targets)) continue;
                foreach (var target in targets)
                {
                    if (++work > 10_000) { bounded = true; break; }
                    if (visited.Contains(target)) continue;
                    if (item.Depth >= 10 || visited.Count >= 500) { bounded = true; continue; }
                    visited.Add(target);
                    queue.Enqueue((target, item.Depth + 1));
                }
                if (work > 10_000) break;
            }
            bool Has(string symbol, string kind) => evidence.TryGetValue(symbol, out var kinds) && kinds.Contains(kind);
            output.Add($"handler={++n:D3}|symbols={visited.Count}|bounded={bounded.ToString().ToLowerInvariant()}|semanticInvocationSources={visited.Count(s => Has(s, "MethodInvoked:semantic"))}|semanticCallSources={visited.Count(s => Has(s, "CallEdge:semantic"))}|invocationWithoutCallFact={visited.Count(s => Has(s, "MethodInvoked:semantic") && !Has(s, "CallEdge:semantic"))}|exactDeclarationTargets={visited.Count(s => Has(s, "MethodDeclared:semantic") || Has(s, "MethodDeclared:nonsemantic"))}|withoutSelectedSourceWitness={visited.Count(s => !evidence.ContainsKey(s))}");
        }
        output.Add("nonClaim=not-report-leaf-identities;not-runtime-execution;missing-exact-witness-is-not-source-absence;declaration-targets-may-use-different-symbol-format");
        return output;
    }
}
