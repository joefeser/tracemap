using System.Text.Json;
using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public static class WebFormsDatabaseEvidenceAudit
{
    public static IReadOnlyList<string> Run(string indexPath, string inspectionPath, Action<string>? diagnostic = null)
    {
        if (new FileInfo(inspectionPath).Length > 128 * 1024 * 1024) throw new InvalidDataException("RawAuditReportLimit");
        using var document = JsonDocument.Parse(File.ReadAllText(inspectionPath));
        var root = document.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "webforms-local-inspection.v1") throw new InvalidDataException("RawAuditSchemaMismatch");
        var hops = root.GetProperty("hops").EnumerateArray().ToArray();
        var recognized = hops.Where(h => IsFill(h.GetProperty("callee").GetString() ?? "")).ToArray();
        var recognizedFillSymbols = recognized.Select(h => NormalizeFrameworkSymbol(h.GetProperty("callee").GetString() ?? ""))
            .ToHashSet(StringComparer.Ordinal);
        diagnostic?.Invoke($"inspectionHopCount={hops.Length}");
        diagnostic?.Invoke($"inspectionFillNamedHops={hops.Count(h => (h.GetProperty("callee").GetString() ?? "").Contains(".Fill(", StringComparison.Ordinal))}");
        diagnostic?.Invoke($"inspectionRecognizedFrameworkFillHops={recognized.Length}");
        if (recognized.Length == 0) throw new InvalidDataException("RawAuditNoRecognizedFillHop");
        var callers = recognized
            .Select(h => h.GetProperty("caller").GetString()).Distinct(StringComparer.Ordinal).ToArray();
        if (callers.Any(string.IsNullOrEmpty)) throw new InvalidDataException("RawAuditFillCallerIdentityMissing");
        if (callers.Length != 1) throw new InvalidDataException("RawAuditMultipleFillCallers");
        using var db = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = indexPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false }.ToString());
        db.Open();
        using var transaction = db.BeginTransaction(deferred: true);
        var scan = root.GetProperty("scanId").GetString();
        var commit = root.GetProperty("commitSha").GetString();
        using (var cmd = db.CreateCommand())
        {
            cmd.Transaction = transaction;
            cmd.CommandText = "select scan_id,commit_sha from scan_manifest";
            using var r = cmd.ExecuteReader();
            if (!r.Read() || r.GetString(0) != scan || r.GetString(1) != commit || r.Read()) throw new InvalidDataException("RawAuditProvenanceMismatch");
        }
        string[] types = ["SqlCommandDetected", "ObjectCreated", "MethodInvoked", "CallEdge", "ArgumentPassed", "PropertyAccessed", "DatabaseOperationCandidate", "SqlTextUsed", "QueryPatternDetected"];
        var counts = types.ToDictionary(t => t, _ => 0, StringComparer.Ordinal);
        var signals = new SortedDictionary<string, int>(StringComparer.Ordinal);
        void Hit(string key, bool hit) { if (hit) signals[key] = signals.GetValueOrDefault(key) + 1; }
        string[] signalNames = ["command-construction", "adapter-construction", "adapter-constructor-argument", "fill-invocation", "fill-argument", "commandtype-property", "storedprocedure-enum-reference", "command-assigned-variable-retained", "adapter-assigned-variable-retained", "adapter-argument-symbol-retained", "fill-receiver-symbol-retained"];
        foreach (var name in signalNames) signals[name] = 0;
        using var query = db.CreateCommand();
        query.Transaction = transaction;
        query.CommandText = "select fact_type,target_symbol,evidence_tier,properties_json from facts where scan_id=$scan and commit_sha=$commit and source_symbol=$caller limit 10001";
        query.Parameters.AddWithValue("$scan", scan!);
        query.Parameters.AddWithValue("$commit", commit!);
        query.Parameters.AddWithValue("$caller", callers[0]!);
        var rows = 0;
        var semantic = 0;
        var fillWitness = false;
        var commandLocals = new HashSet<string>(StringComparer.Ordinal);
        var adapterCommandArguments = new List<string>();
        var adapterLocals = new HashSet<string>(StringComparer.Ordinal);
        var fillReceivers = new List<string>();
        var storedProcedureAssignments = 0;
        long bytes = 0;
        using var reader = query.ExecuteReader();
        while (reader.Read())
        {
            if (++rows > 10_000) throw new InvalidDataException("RawAuditInputLimit");
            var type = reader.GetString(0);
            var target = reader.IsDBNull(1) ? "" : reader.GetString(1);
            var tier = reader.GetString(2);
            var json = reader.GetString(3);
            bytes += 2L * (target.Length + json.Length);
            if (bytes > 8 * 1024 * 1024) throw new InvalidDataException("RawAuditTextLimit");
            if (counts.ContainsKey(type)) counts[type]++;
            if (tier != "Tier1Semantic") continue;
            semantic++;
            var fill = IsFill(target) && recognizedFillSymbols.Contains(NormalizeFrameworkSymbol(target));
            // Normalize only framework classification, never the exact source-symbol lookup.
            target = FrameworkDisplayName(target);
            if (type is "CallEdge" or "MethodInvoked" && fill) fillWitness = true;
            using var properties = JsonDocument.Parse(json);
            bool Has(string key) => properties.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString());
            string? Value(string key) => Has(key) ? properties.RootElement.GetProperty(key).GetString() : null;
            bool IsType(string name) => target == name || target.StartsWith(name + ".", StringComparison.Ordinal);
            var command = IsType("System.Data.SqlClient.SqlCommand") || IsType("Microsoft.Data.SqlClient.SqlCommand");
            var adapter = IsType("System.Data.SqlClient.SqlDataAdapter") || IsType("Microsoft.Data.SqlClient.SqlDataAdapter");
            Hit("command-construction", type == "ObjectCreated" && command);
            Hit("adapter-construction", type == "ObjectCreated" && adapter);
            Hit("adapter-constructor-argument", type == "ArgumentPassed" && adapter && target.Contains(".SqlDataAdapter(", StringComparison.Ordinal));
            Hit("fill-invocation", type == "MethodInvoked" && fill);
            Hit("fill-argument", type == "ArgumentPassed" && fill);
            Hit("commandtype-property", type == "PropertyAccessed" && ((command && target.EndsWith(".CommandType", StringComparison.Ordinal))
                || target is "System.Data.Common.DbCommand.CommandType" or "System.Data.IDbCommand.CommandType"));
            Hit("storedprocedure-enum-reference", target == "System.Data.CommandType.StoredProcedure");
            Hit("command-assigned-variable-retained", type == "ObjectCreated" && command && Has("assignedTo"));
            Hit("adapter-assigned-variable-retained", type == "ObjectCreated" && adapter && Has("assignedTo"));
            Hit("adapter-argument-symbol-retained", type == "ArgumentPassed" && adapter && Has("argumentSymbol"));
            Hit("fill-receiver-symbol-retained", type == "MethodInvoked" && fill && Has("receiverSymbol"));
            if (type == "ObjectCreated" && command && Value("assignedTo") is { } commandLocal) commandLocals.Add(commandLocal);
            if (type == "ObjectCreated" && adapter && Value("assignedTo") is { } adapterLocal) adapterLocals.Add(adapterLocal);
            if (type == "MethodInvoked" && fill && Value("receiverSymbol") is { } fillReceiver) fillReceivers.Add(fillReceiver);
            if (type == "PropertyAccessed"
                && ((command && target.EndsWith(".CommandType", StringComparison.Ordinal)) || target is "System.Data.Common.DbCommand.CommandType" or "System.Data.IDbCommand.CommandType")
                && Value("accessKind") == "SimpleAssignmentTarget"
                && FrameworkDisplayName(Value("assignedValueSymbol") ?? "") == "System.Data.CommandType.StoredProcedure") storedProcedureAssignments++;
            if (type == "ArgumentPassed" && adapter && Value("argumentSymbol") is { } adapterArgument) adapterCommandArguments.Add(adapterArgument);
        }
        var commandAdapterLocalMatches = adapterCommandArguments.Count(commandLocals.Contains);
        diagnostic?.Invoke("indexProvenance=matched");
        diagnostic?.Invoke($"exactCallerFactRows={rows}");
        diagnostic?.Invoke($"exactCallerFrameworkFillWitness={(fillWitness ? "present" : "missing")}");
        if (!fillWitness) throw new InvalidDataException("RawAuditFillIndexWitnessMissing");
        return new[] { "database-evidence-audit=completed", "provenance=matched", "scope=exact-fill-caller-only", "rule=diagnostic.webforms.database-evidence-census.v1", $"retainedFacts={rows}", $"semanticFacts={semantic}" }
            .Concat(counts.Select(c => $"factType={c.Key}|count={c.Value}"))
            .Concat(signals.Select(s => $"semanticSignal={s.Key}|count={s.Value}"))
            .Append($"commandAdapterLocalNameMatch={commandAdapterLocalMatches}")
            .Append($"adapterFillLocalNameMatch={fillReceivers.Count(adapterLocals.Contains)}")
            .Append($"storedProcedureSimpleAssignments={storedProcedureAssignments}")
            .Append($"fillReceiverIdentity={(fillReceivers.Count > 0 ? "retained" : "unavailable-in-selected-facts")}")
            .Append("linkage=local-name-match-is-same-method-support-not-object-identity;assignment-does-not-prove-value-at-fill")
            .Append("nonClaim=missing-retained-metadata-is-not-missing-source;commandtype-property-is-not-proof-of-storedprocedure-assignment;no-sql-or-private-values-exported")
            .ToArray();
    }

    private static string FrameworkDisplayName(string symbol) => symbol.StartsWith("global::", StringComparison.Ordinal) ? symbol[8..] : symbol;

    private static string NormalizeFrameworkSymbol(string symbol) => symbol.Replace("global::", string.Empty, StringComparison.Ordinal);

    private static bool IsFill(string symbol)
    {
        var display = FrameworkDisplayName(symbol);
        string[] owners = ["System.Data.Common.DbDataAdapter", "System.Data.SqlClient.SqlDataAdapter", "Microsoft.Data.SqlClient.SqlDataAdapter"];
        return owners.Any(owner => display.StartsWith(owner + ".Fill(", StringComparison.Ordinal)
            && display.EndsWith(')')
            && display.Length > owner.Length + ".Fill()".Length);
    }
}
