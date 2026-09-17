using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static class WebFormsVisualBasicReceiverBridgeAudit
{
    private sealed record Fact(string SourceId, string Id, string? SourceSymbol, string FilePath, int Line,
        string RuleId, string Tier, IReadOnlyDictionary<string, string> Properties);

    public static IReadOnlyList<string> Run(string indexPath, string packetPath, string surfaceId)
    {
        if (new FileInfo(packetPath).Length is <= 0 or > 512 * 1024 * 1024)
            throw new InvalidDataException("ReceiverBridgeAuditInputLimit");
        using var packet = JsonDocument.Parse(File.ReadAllText(packetPath));
        var root = packet.RootElement;
        if (root.GetProperty("schemaVersion").GetString() != "webforms-modernization-packet.v1")
            throw new InvalidDataException("ReceiverBridgeAuditSchemaMismatch");
        var primary = root.GetProperty("sources")[0];
        var scan = primary.GetProperty("scanId").GetString();
        var commit = primary.GetProperty("commitSha").GetString();
        var selectedChains = root.GetProperty("eventChains").EnumerateArray()
            .Where(chain => chain.GetProperty("surfaceId").GetString() == surfaceId)
            .ToArray();
        var ids = selectedChains
            .SelectMany(chain => chain.GetProperty("supportingEdgeIds").EnumerateArray().Select(id => id.GetString())
                .Concat(chain.TryGetProperty("callEvidence", out var evidence)
                    ? evidence.EnumerateArray().Select(item => item.GetProperty("callEvidenceId").GetString())
                    : []))
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();

        using var db = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = indexPath, Mode = SqliteOpenMode.ReadOnly, Pooling = false
        }.ToString());
        db.Open();
        using var transaction = db.BeginTransaction(deferred: true);
        var facts = new List<Fact>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select source_index_id, original_fact_id, source_symbol, file_path, start_line,
                       rule_id, evidence_tier, properties_json
                from combined_facts
                where scan_id=$scan and commit_sha=$commit and fact_type='CallEdge'
                  and (original_fact_id in (select value from json_each($ids))
                       or combined_fact_id in (select value from json_each($ids)))
                order by file_path, start_line, combined_fact_id;
                """;
            command.Parameters.AddWithValue("$scan", scan!);
            command.Parameters.AddWithValue("$commit", commit!);
            command.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(ids));
            using var reader = command.ExecuteReader();
            while (reader.Read()) facts.Add(ReadFact(reader));
        }
        if (facts.Count > 10_000) throw new InvalidDataException("ReceiverBridgeAuditInputLimit");

        var calls = facts.Where(fact => fact.RuleId == RuleIds.VisualBasicSyntaxCallGraph
            && Value(fact, "callKind") == "SyntaxInvocation"
            && !string.IsNullOrWhiteSpace(Value(fact, "receiverName")))
            .ToArray();
        var creations = facts.Where(IsCreation).ToArray();
        var methodNames = calls.Select(call => Value(call, "calleeName"))
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var declarations = new List<Fact>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select source_index_id, combined_fact_id, source_symbol, file_path, start_line,
                       rule_id, evidence_tier, properties_json
                from combined_facts
                where fact_type='MethodDeclared'
                  and ((rule_id=$semantic_rule and evidence_tier=$semantic_tier)
                       or (rule_id=$syntax_rule and evidence_tier=$syntax_tier))
                  and json_valid(properties_json)
                  and coalesce(cast(json_extract(properties_json,'$.methodName') as text),
                               cast(json_extract(properties_json,'$.name') as text)) collate nocase
                      in (select value from json_each($names))
                order by combined_fact_id limit 10001;
                """;
            command.Parameters.AddWithValue("$semantic_rule", RuleIds.VisualBasicSemanticDeclarations);
            command.Parameters.AddWithValue("$semantic_tier", EvidenceTiers.Tier1Semantic);
            command.Parameters.AddWithValue("$syntax_rule", RuleIds.VisualBasicSyntaxDeclarations);
            command.Parameters.AddWithValue("$syntax_tier", EvidenceTiers.Tier3SyntaxOrTextual);
            command.Parameters.AddWithValue("$names", JsonSerializer.Serialize(methodNames));
            using var reader = command.ExecuteReader();
            while (reader.Read()) declarations.Add(ReadFact(reader));
        }
        if (declarations.Count > 10_000) throw new InvalidDataException("ReceiverBridgeAuditInputLimit");

        var bodySymbols = new List<(string SourceId, string Symbol)>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select distinct source_index_id, source_symbol from combined_facts "
                + "where fact_type='CallEdge' and source_symbol is not null and trim(source_symbol)<>'' "
                + "and (rule_id=$syntax_rule or (rule_id=$semantic_rule and evidence_tier=$semantic_tier)) "
                + "order by source_index_id, source_symbol limit 10001;";
            command.Parameters.AddWithValue("$syntax_rule", RuleIds.VisualBasicSyntaxCallGraph);
            command.Parameters.AddWithValue("$semantic_rule", RuleIds.VisualBasicSemanticCallGraph);
            command.Parameters.AddWithValue("$semantic_tier", EvidenceTiers.Tier1Semantic);
            using var reader = command.ExecuteReader();
            while (reader.Read()) bodySymbols.Add((reader.GetString(0), reader.GetString(1)));
        }
        if (bodySymbols.Count > 10_000) throw new InvalidDataException("ReceiverBridgeAuditInputLimit");

        var results = new Dictionary<string, int>(StringComparer.Ordinal);
        void Hit(string value) => results[value] = results.GetValueOrDefault(value) + 1;
        void Add(string value, int count)
        {
            if (count > 0) results[value] = results.GetValueOrDefault(value) + count;
        }
        foreach (var call in calls)
        {
            if (!int.TryParse(Value(call, "argumentCount"), out var arity) || string.IsNullOrWhiteSpace(Value(call, "calleeName")))
            { Hit("invocation-metadata-unavailable"); continue; }
            var receiver = Value(call, "receiverName");
            var matches = creations.Where(creation => creation.SourceId == call.SourceId
                && creation.FilePath.Equals(call.FilePath, StringComparison.OrdinalIgnoreCase)
                && creation.Line <= call.Line
                && SameMember(creation, call)
                && string.Equals(Value(creation, "assignedTo"), receiver, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0) { Hit("receiver-creation-unavailable"); continue; }
            if (matches.Length != 1) { Hit("receiver-creation-ambiguous"); continue; }
            var type = SimpleType(Value(matches[0], "calleeContainingType") ?? Value(matches[0], "calleeName"));
            if (string.IsNullOrWhiteSpace(type)) { Hit("receiver-type-unavailable"); continue; }
            var targets = declarations.Where(declaration => declaration.RuleId == RuleIds.VisualBasicSemanticDeclarations &&
                SimpleType(Value(declaration, "containingType")).Equals(type, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Value(declaration, "methodName"), Value(call, "calleeName"), StringComparison.OrdinalIgnoreCase)
                && int.TryParse(Value(declaration, "parameterCount"), out var count) && count == arity).ToArray();
            if (targets.Length != 0)
            {
                Hit(targets.Length == 1 ? "ready-semantic" : "semantic-target-ambiguous");
                continue;
            }
            var callName = Value(call, "calleeName")!;
            var syntaxNameTargets = declarations.Where(declaration => declaration.RuleId == RuleIds.VisualBasicSyntaxDeclarations
                    && string.Equals(Value(declaration, "methodName") ?? Value(declaration, "name"), callName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var syntaxTypeTargets = syntaxNameTargets.Where(declaration =>
                    SimpleType(Value(declaration, "containingType")).Equals(type, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var bodyMembers = bodySymbols
                .Select(body => new { body.SourceId, Member = QualifiedMemberKey(body.Symbol) })
                .Where(body => body.Member is not null)
                .Select(body => new { body.SourceId, Member = body.Member!.Value })
                .ToArray();
            var bodyNameArity = bodyMembers.Where(body => body.Member.Name.Equals(callName, StringComparison.OrdinalIgnoreCase)
                    && body.Member.Arity == arity).ToArray();
            var bodyTypeName = bodyMembers.Where(body => body.Member.Type.Equals(type, StringComparison.OrdinalIgnoreCase)
                    && body.Member.Name.Equals(callName, StringComparison.OrdinalIgnoreCase)).ToArray();
            var bodyExact = bodyNameArity.Where(body => body.Member.Type.Equals(type, StringComparison.OrdinalIgnoreCase)).ToArray();
            Add("candidate.syntax-name", syntaxNameTargets.Length);
            Add("candidate.syntax-type-name", syntaxTypeTargets.Length);
            Add("candidate.body-name-arity", bodyNameArity.Length);
            Add("candidate.body-type-name", bodyTypeName.Length);
            Add("candidate.body-exact", bodyExact.Length);
            var syntaxTargets = syntaxTypeTargets.Where(declaration => bodyExact.Any(body => body.SourceId == declaration.SourceId)).ToArray();
            if (syntaxTargets.Length == 0)
            {
                if (syntaxTypeTargets.Length == 0 && syntaxNameTargets.Length > 0) Hit("syntax-declaration-type-mismatch");
                else if (bodyExact.Length == 0 && bodyNameArity.Length > 0) Hit("body-type-mismatch");
                else if (bodyExact.Length == 0 && bodyTypeName.Length > 0) Hit("body-arity-mismatch");
                else if (bodyExact.Length == 0) Hit("body-member-unavailable");
                else Hit("declaration-body-source-mismatch");
                continue;
            }
            Hit(syntaxTargets.Length switch { 0 => "target-unavailable", 1 => "ready-syntax", _ => "syntax-target-ambiguous" });
        }

        var semanticDeclarations = declarations.Count(fact => fact.RuleId == RuleIds.VisualBasicSemanticDeclarations);
        var syntaxDeclarations = declarations.Count(fact => fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations);
        var output = new List<string> { "receiverBridgeAudit=valid", $"supportingCallFacts={facts.Count}",
            $"syntaxReceiverInvocations={calls.Length}", $"receiverCreations={creations.Length}",
            $"semanticMethodCandidates={semanticDeclarations}", $"syntaxMethodCandidates={syntaxDeclarations}",
            $"receiverBodySymbols={bodySymbols.Count}" };
        output.AddRange(results.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"receiverBridgeStatus.{pair.Key}={pair.Value}"));
        return output;
    }

    private static Fact ReadFact(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.GetInt32(4), reader.GetString(5), reader.GetString(6),
        JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(7)) ?? []);
    private static string? Value(Fact fact, string key) => fact.Properties.GetValueOrDefault(key);
    private static bool IsCreation(Fact fact) =>
        fact.RuleId == RuleIds.VisualBasicSyntaxCallGraph && Value(fact, "callKind") == "SyntaxObjectCreation"
        || fact.RuleId == RuleIds.VisualBasicSemanticCallGraph && fact.Tier == EvidenceTiers.Tier1Semantic && Value(fact, "callKind") == "SemanticObjectCreation";
    private static bool SameMember(Fact left, Fact right)
    {
        if (!string.IsNullOrWhiteSpace(left.SourceSymbol) && string.Equals(left.SourceSymbol.Trim(), right.SourceSymbol?.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
        var l = MemberKey(left.SourceSymbol ?? Value(left, "callerName"));
        var r = MemberKey(right.SourceSymbol ?? Value(right, "callerName"));
        return l is not null && r is not null && l.Value.Arity == r.Value.Arity && l.Value.Name.Equals(r.Value.Name, StringComparison.OrdinalIgnoreCase);
    }
    private static (string Name, int Arity)? MemberKey(string? value)
    {
        var qualified = QualifiedMemberKey(value);
        return qualified is null ? null : (qualified.Value.Name, qualified.Value.Arity);
    }
    private static (string Type, string Name, int Arity)? QualifiedMemberKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var text = value.Trim().Replace("Global::", string.Empty, StringComparison.OrdinalIgnoreCase);
        var slash = text.LastIndexOf('/');
        if (slash > 0 && int.TryParse(text[(slash + 1)..], out var slashArity))
        {
            var member = text[..slash];
            var dot = member.LastIndexOf('.');
            return dot <= 0 ? null : (SimpleType(member[..dot]), member[(dot + 1)..], slashArity);
        }
        var open = text.IndexOf('(');
        if (open <= 0 || !text.EndsWith(')')) return null;
        var qualifiedMember = text[..open];
        var memberDot = qualifiedMember.LastIndexOf('.');
        if (memberDot <= 0) return null;
        var parameters = text[(open + 1)..^1];
        var depth = 0; var arity = parameters.Length == 0 ? 0 : 1;
        foreach (var character in parameters) { if (character == '(') depth++; else if (character == ')' && depth > 0) depth--; else if (character == ',' && depth == 0) arity++; }
        return (SimpleType(qualifiedMember[..memberDot]), qualifiedMember[(memberDot + 1)..], arity);
    }
    private static string SimpleType(string? value) => string.IsNullOrWhiteSpace(value) ? string.Empty
        : value.Trim().Replace("Global::", string.Empty, StringComparison.OrdinalIgnoreCase).Split('.').Last();
}
