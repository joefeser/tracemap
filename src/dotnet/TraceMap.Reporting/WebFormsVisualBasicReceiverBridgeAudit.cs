using System.Text.Json;
using Microsoft.Data.Sqlite;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static class WebFormsVisualBasicReceiverBridgeAudit
{
    private sealed record Fact(string SourceId, string SourceLabel, string? ProjectPath, string Id,
        string? SourceSymbol, string FilePath, int Line, string RuleId, string Tier,
        IReadOnlyDictionary<string, string> Properties);

    public static IReadOnlyList<string> Run(string indexPath, string packetPath, string surfaceId, bool includePrivateIdentities = false)
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
                select source_index_id,
                       coalesce((select label from index_sources where index_sources.source_index_id=combined_facts.source_index_id), source_index_id),
                       project_path, original_fact_id, source_symbol, file_path, start_line,
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
            .Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value!)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var declarations = new List<Fact>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = """
                select source_index_id,
                       coalesce((select label from index_sources where index_sources.source_index_id=combined_facts.source_index_id), source_index_id),
                       project_path, combined_fact_id, source_symbol, file_path, start_line,
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

        var bodySymbols = new List<(string SourceId, string Symbol, string FilePath, int Line)>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select source_index_id, source_symbol, file_path, start_line from combined_facts "
                + "where source_symbol is not null and trim(source_symbol)<>'' and ("
                + "(fact_type='CallEdge' and (rule_id=$syntax_call_rule or (rule_id=$semantic_call_rule and evidence_tier=$semantic_tier))) "
                + "or (fact_type='MethodInvoked' and rule_id=$semantic_invocation_rule and evidence_tier=$semantic_tier) "
                + "or (fact_type='ObjectCreated' and (rule_id=$syntax_creation_rule or (rule_id=$semantic_creation_rule and evidence_tier=$semantic_tier)))) "
                + "and exists (select 1 from json_each($method_names) names "
                + "where instr(lower(source_symbol), lower(cast(names.value as text))) > 0) "
                + "order by source_index_id, source_symbol, file_path, start_line, combined_fact_id limit 10001;";
            command.Parameters.AddWithValue("$syntax_call_rule", RuleIds.VisualBasicSyntaxCallGraph);
            command.Parameters.AddWithValue("$semantic_call_rule", RuleIds.VisualBasicSemanticCallGraph);
            command.Parameters.AddWithValue("$semantic_invocation_rule", RuleIds.VisualBasicSemanticMethodInvocation);
            command.Parameters.AddWithValue("$syntax_creation_rule", RuleIds.VisualBasicSyntaxObjectCreation);
            command.Parameters.AddWithValue("$semantic_creation_rule", RuleIds.VisualBasicSemanticObjectCreation);
            command.Parameters.AddWithValue("$semantic_tier", EvidenceTiers.Tier1Semantic);
            command.Parameters.AddWithValue("$method_names", JsonSerializer.Serialize(methodNames));
            using var reader = command.ExecuteReader();
            while (reader.Read()) bodySymbols.Add((reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetInt32(3)));
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
            var syntaxDestinations = syntaxTargets
                .SelectMany(declaration => bodyExact
                    .Where(body => body.SourceId == declaration.SourceId)
                    .Select(body => $"{body.SourceId}\0{body.Member.Type}\0{body.Member.Name}\0{body.Member.Arity}"))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count();
            Hit(syntaxDestinations switch { 0 => "target-unavailable", 1 => "ready-syntax", _ => "syntax-target-ambiguous" });
        }

        var semanticDeclarations = declarations.Count(fact => fact.RuleId == RuleIds.VisualBasicSemanticDeclarations);
        var syntaxDeclarations = declarations.Count(fact => fact.RuleId == RuleIds.VisualBasicSyntaxDeclarations);
        var graphInventory = CombinedDependencyPathReporter.BuildGraphInventoryAsync(indexPath)
            .GetAwaiter().GetResult();
        var graphReceiverGaps = graphInventory.Gaps
            .Where(gap => gap.RuleId == "combined.paths.projectless-vb-receiver-bridge.v1")
            .ToArray();
        var graphGapCalls = new Dictionary<string, Fact>(StringComparer.Ordinal);
        var graphGapFactIds = graphReceiverGaps
            .Select(gap => gap.CombinedFactId)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (graphGapFactIds.Length > 0)
        {
            using var command = db.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "select source_index_id, coalesce((select label from index_sources where index_sources.source_index_id=combined_facts.source_index_id), source_index_id), "
                + "project_path, combined_fact_id, source_symbol, file_path, start_line, rule_id, evidence_tier, properties_json "
                + "from combined_facts where combined_fact_id in (select value from json_each($ids)) order by combined_fact_id limit 10001;";
            command.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(graphGapFactIds));
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var fact = ReadFact(reader);
                graphGapCalls[fact.Id] = fact;
            }
            if (graphGapCalls.Count > 10_000) throw new InvalidDataException("ReceiverBridgeAuditInputLimit");
        }
        var output = new List<string> { "receiverBridgeAudit=valid", $"supportingCallFacts={facts.Count}",
            $"syntaxReceiverInvocations={calls.Length}", $"receiverCreations={creations.Length}",
            $"semanticMethodCandidates={semanticDeclarations}", $"syntaxMethodCandidates={syntaxDeclarations}",
            $"receiverBodySymbols={bodySymbols.Select(body => $"{body.SourceId}\0{body.Symbol}").Distinct(StringComparer.OrdinalIgnoreCase).Count()}" };
        output.AddRange(results.OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => $"receiverBridgeStatus.{pair.Key}={pair.Value}"));
        output.AddRange(graphReceiverGaps
            .GroupBy(gap => gap.GapKind, StringComparer.Ordinal)
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"receiverBridgeWholeIndexGraphGap.{group.Key}={group.Count()}"));
        if (includePrivateIdentities)
        {
            output.Add("receiverBridgePrivate=enabled");
            AddPrivateExecProcDownstreamLeaves(output, graphInventory, db, transaction);
            var privateGapIndex = 0;
            foreach (var gap in graphReceiverGaps
                .Where(gap => gap.CombinedFactId is not null
                    && graphGapCalls.TryGetValue(gap.CombinedFactId, out var call)
                    && !string.IsNullOrWhiteSpace(call.SourceSymbol)
                    && methodNames.Any(name => call.SourceSymbol.Contains(name, StringComparison.OrdinalIgnoreCase)))
                .OrderBy(gap => gap.FilePath, StringComparer.Ordinal)
                .ThenBy(gap => gap.StartLine)
                .ThenBy(gap => gap.GapKind, StringComparer.Ordinal)
                .Take(100))
            {
                privateGapIndex++;
                var call = graphGapCalls[gap.CombinedFactId!];
                output.Add($"receiverBridgePrivate.graphGap-{privateGapIndex:D2}.kind={gap.GapKind};file={gap.FilePath};line={gap.StartLine};source={call.SourceSymbol};callee={Value(call, "calleeName") ?? "unavailable"};receiver={Value(call, "receiverName") ?? "unavailable"};arity={Value(call, "argumentCount") ?? "unavailable"};reason={gap.Reason};candidates={gap.CandidateCount ?? 0}");
                if (gap.Reason == "receiver-provenance-unavailable")
                {
                    output.Add($"receiverBridgePrivate.graphGap-{privateGapIndex:D2}.provenance={ReadReceiverProvenanceFacts(db, transaction, call)}");
                }
            }
            output.Add($"receiverBridgePrivate.graphGaps={graphReceiverGaps.Length}");
            output.Add($"receiverBridgePrivate.relevantGraphGaps={privateGapIndex}");
            var privateIndex = 0;
            var callStatusIndex = 0;
            foreach (var call in calls)
            {
                var matches = creations.Where(creation => creation.SourceId == call.SourceId
                    && creation.FilePath.Equals(call.FilePath, StringComparison.OrdinalIgnoreCase)
                    && creation.Line <= call.Line && SameMember(creation, call)
                    && string.Equals(Value(creation, "assignedTo"), Value(call, "receiverName"), StringComparison.OrdinalIgnoreCase)).ToArray();
                var sameMemberCreations = creations.Where(creation => creation.SourceId == call.SourceId
                        && creation.FilePath.Equals(call.FilePath, StringComparison.OrdinalIgnoreCase)
                        && creation.Line <= call.Line && SameMember(creation, call))
                    .OrderBy(creation => creation.Line).ThenBy(creation => creation.Id, StringComparer.Ordinal)
                    .Take(20)
                    .Select(creation => $"line={creation.Line},assignedTo={Value(creation, "assignedTo") ?? "unavailable"},type={SimpleType(Value(creation, "calleeContainingType") ?? Value(creation, "calleeName"))}")
                    .ToArray();
                callStatusIndex++;
                output.Add($"receiverBridgePrivate.callStatus-{callStatusIndex:D2}.line={call.Line};source={call.SourceSymbol ?? "unavailable"};callee={Value(call, "calleeName") ?? "unavailable"};arity={Value(call, "argumentCount") ?? "unavailable"};receiver={Value(call, "receiverName") ?? "unavailable"};matchingCreations={matches.Length};sameMemberCreations={string.Join('|', sameMemberCreations)}");
                if (matches.Length != 1 || !int.TryParse(Value(call, "argumentCount"), out var arity)) continue;
                privateIndex++;
                var name = Value(call, "calleeName") ?? "unavailable";
                var type = SimpleType(Value(matches[0], "calleeContainingType") ?? Value(matches[0], "calleeName"));
                var declarationTypes = declarations
                    .Where(declaration => string.Equals(Value(declaration, "methodName") ?? Value(declaration, "name"), name, StringComparison.OrdinalIgnoreCase))
                    .Select(declaration => Value(declaration, "containingType") ?? "unavailable")
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
                var declarationSources = declarations
                    .Where(declaration => string.Equals(Value(declaration, "methodName") ?? Value(declaration, "name"), name, StringComparison.OrdinalIgnoreCase))
                    .Select(declaration => declaration.SourceId).ToHashSet(StringComparer.Ordinal);
                var nearbyBodies = bodySymbols
                    .Where(body => declarationSources.Contains(body.SourceId)
                        && body.Symbol.Contains(name, StringComparison.OrdinalIgnoreCase))
                    .Select(body => body.Symbol).Distinct(StringComparer.OrdinalIgnoreCase)
                    .Order(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
                var declarationSites = declarations
                    .Where(declaration => string.Equals(Value(declaration, "methodName") ?? Value(declaration, "name"), name, StringComparison.OrdinalIgnoreCase))
                    .Select(declaration => $"source={declaration.SourceLabel};project={declaration.ProjectPath ?? "unavailable"};type={Value(declaration, "containingType") ?? "unavailable"};site={declaration.FilePath}:{declaration.Line};rule={declaration.RuleId};tier={declaration.Tier}")
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(20).ToArray();
                var sameFileBodies = declarations
                    .Where(declaration => string.Equals(Value(declaration, "methodName") ?? Value(declaration, "name"), name, StringComparison.OrdinalIgnoreCase))
                    .SelectMany(declaration => bodySymbols.Where(body => body.SourceId == declaration.SourceId
                            && body.FilePath.Equals(declaration.FilePath, StringComparison.OrdinalIgnoreCase))
                        .Select(body => $"{body.Symbol}@{body.Line}"))
                    .Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).Take(30).ToArray();
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.line={call.Line};callee={name};arity={arity};receiverType={type}");
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.declarationTypes={string.Join('|', declarationTypes)}");
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.declarationSites={string.Join('|', declarationSites)}");
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.receiverTypeMethods={string.Join('|', ReadReceiverTypeMethods(db, transaction, type))}");
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.nearbyBodySymbols={string.Join('|', nearbyBodies)}");
                output.Add($"receiverBridgePrivate.call-{privateIndex:D2}.sameFileBodySymbols={string.Join('|', sameFileBodies)}");
            }
            output.Add($"receiverBridgePrivate.calls={privateIndex}");
            output.Add($"receiverBridgePrivate.callStatuses={callStatusIndex}");
        }
        return output;
    }

    private static string ReadReceiverProvenanceFacts(
        SqliteConnection db,
        SqliteTransaction transaction,
        Fact call)
    {
        var caller = QualifiedMemberKey(call.SourceSymbol);
        var receiver = Value(call, "receiverName");
        if (caller is null || string.IsNullOrWhiteSpace(receiver)) return "caller-or-receiver-unavailable";

        var roots = new List<string>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select coalesce((select label from index_sources where index_sources.source_index_id=combined_facts.source_index_id), source_index_id), properties_json "
                + "from combined_facts where fact_type='TypeDeclared' and rule_id=$rule and evidence_tier=$tier "
                + "and json_valid(properties_json) and lower(coalesce(cast(json_extract(properties_json,'$.name') as text),''))=$name "
                + "order by combined_fact_id limit 21;";
            command.Parameters.AddWithValue("$rule", RuleIds.VisualBasicSyntaxDeclarations);
            command.Parameters.AddWithValue("$tier", EvidenceTiers.Tier3SyntaxOrTextual);
            command.Parameters.AddWithValue("$name", SimpleType(caller.Value.Type).ToLowerInvariant());
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(1)) ?? [];
                roots.Add($"source={reader.GetString(0)},qualified={Value(properties, "qualifiedName") ?? Value(properties, "name") ?? "unavailable"},bases={Value(properties, "baseTypes") ?? "unavailable"}");
            }
        }

        var fields = new List<string>();
        using (var command = db.CreateCommand())
        {
            command.Transaction = transaction;
            command.CommandText = "select coalesce((select label from index_sources where index_sources.source_index_id=combined_facts.source_index_id), source_index_id), properties_json "
                + "from combined_facts where fact_type='FieldDeclared' and rule_id=$rule "
                + "and json_valid(properties_json) and lower(coalesce(cast(json_extract(properties_json,'$.fieldName') as text),''))=$receiver "
                + "order by combined_fact_id limit 21;";
            command.Parameters.AddWithValue("$rule", RuleIds.VisualBasicSyntaxDeclarations);
            command.Parameters.AddWithValue("$receiver", receiver.ToLowerInvariant());
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(1)) ?? [];
                fields.Add($"source={reader.GetString(0)},owner={Value(properties, "qualifiedContainingType") ?? Value(properties, "containingType") ?? "unavailable"},type={Value(properties, "fieldType") ?? Value(properties, "declaredType") ?? "unavailable"}");
            }
        }

        return $"callerType={caller.Value.Type};roots={string.Join('|', roots)};fields={string.Join('|', fields)}";
    }

    private static string? Value(IReadOnlyDictionary<string, string> properties, string key) =>
        properties.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    private static void AddPrivateExecProcDownstreamLeaves(
        List<string> output,
        CombinedPathGraphInventory inventory,
        SqliteConnection db,
        SqliteTransaction transaction)
    {
        var nodes = inventory.Nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);
        var outgoing = inventory.Edges
            .GroupBy(edge => edge.FromNodeId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.EdgeId, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var starts = inventory.Nodes
            .Where(node => QualifiedMemberKey(node.SymbolId ?? node.DisplayName) is { } member
                && member.Name.Equals("ExecProc_Scalar", StringComparison.OrdinalIgnoreCase)
                && member.Arity == 2)
            .OrderBy(node => node.NodeId, StringComparer.Ordinal)
            .ToArray();
        var queue = new Queue<(string NodeId, int Depth)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        foreach (var start in starts)
        {
            queue.Enqueue((start.NodeId, 0));
            visited.Add(start.NodeId);
        }
        var leaves = new List<CombinedPathNode>();
        while (queue.Count > 0 && visited.Count <= 2_000)
        {
            var current = queue.Dequeue();
            if (current.Depth >= 12 || !outgoing.TryGetValue(current.NodeId, out var edges) || edges.Length == 0)
            {
                if (nodes.TryGetValue(current.NodeId, out var leaf)) leaves.Add(leaf);
                continue;
            }
            foreach (var edge in edges)
            {
                if (visited.Add(edge.ToNodeId)) queue.Enqueue((edge.ToNodeId, current.Depth + 1));
            }
        }
        output.Add($"receiverBridgePrivate.execProcStarts={starts.Length}");
        output.Add($"receiverBridgePrivate.execProcReachableNodes={visited.Count}");
        var startIndex = 0;
        foreach (var start in starts)
        {
            startIndex++;
            var startQueue = new Queue<(string NodeId, int Depth)>();
            var startVisited = new HashSet<string>(StringComparer.Ordinal) { start.NodeId };
            startQueue.Enqueue((start.NodeId, 0));
            while (startQueue.Count > 0 && startVisited.Count <= 2_000)
            {
                var current = startQueue.Dequeue();
                if (current.Depth >= 12 || !outgoing.TryGetValue(current.NodeId, out var edges)) continue;
                foreach (var edge in edges)
                {
                    if (startVisited.Add(edge.ToNodeId)) startQueue.Enqueue((edge.ToNodeId, current.Depth + 1));
                }
            }

            var sqlSurfaces = startVisited
                .Select(nodeId => nodes.GetValueOrDefault(nodeId))
                .OfType<CombinedPathNode>()
                .Where(node => node.SurfaceKind is "sql-query" or "sql-persistence")
                .OrderBy(node => node.FilePath, StringComparer.Ordinal)
                .ThenBy(node => node.StartLine ?? 0)
                .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
                .Take(20)
                .ToArray();
            output.Add($"receiverBridgePrivate.execProcStart-{startIndex:D2}.name={start.SymbolId ?? start.DisplayName};file={start.FilePath ?? "unavailable"};line={start.StartLine ?? 0};reachableNodes={startVisited.Count};sqlSurfaces={sqlSurfaces.Length}");
            var surfaceIndex = 0;
            foreach (var surface in sqlSurfaces)
            {
                surfaceIndex++;
                output.Add($"receiverBridgePrivate.execProcStart-{startIndex:D2}.sqlSurface-{surfaceIndex:D2}.name={surface.DisplayName};surface={surface.SurfaceKind};file={surface.FilePath ?? "unavailable"};line={surface.StartLine ?? 0};rule={surface.RuleId ?? "unavailable"};tier={surface.EvidenceTier ?? "unavailable"}");
            }
        }
        var index = 0;
        foreach (var leaf in leaves
            .DistinctBy(node => node.NodeId, StringComparer.Ordinal)
            .OrderBy(node => node.FilePath, StringComparer.Ordinal)
            .ThenBy(node => node.StartLine ?? 0)
            .ThenBy(node => node.DisplayName, StringComparer.Ordinal)
            .Take(20))
        {
            index++;
            var factDetails = ReadSurfaceFactDetails(db, transaction, leaf);
            output.Add($"receiverBridgePrivate.execProcLeaf-{index:D2}.name={leaf.DisplayName};kind={leaf.NodeKind};surface={leaf.SurfaceKind ?? "none"};file={leaf.FilePath ?? "unavailable"};line={leaf.StartLine ?? 0};rule={leaf.RuleId ?? "unavailable"};tier={leaf.EvidenceTier ?? "unavailable"}{factDetails}");
        }
        output.Add($"receiverBridgePrivate.execProcLeaves={index}");
    }

    private static string ReadSurfaceFactDetails(
        SqliteConnection db,
        SqliteTransaction transaction,
        CombinedPathNode node)
    {
        if (node.SurfaceKind is not ("sql-query" or "sql-persistence")
            || string.IsNullOrWhiteSpace(node.FilePath)
            || node.StartLine is null
            || string.IsNullOrWhiteSpace(node.RuleId))
        {
            return string.Empty;
        }

        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select fact_type, coalesce(source_symbol,''), coalesce(target_symbol,''), properties_json "
            + "from combined_facts where file_path=$file collate nocase and start_line=$line and rule_id=$rule "
            + "order by combined_fact_id limit 6;";
        command.Parameters.AddWithValue("$file", node.FilePath);
        command.Parameters.AddWithValue("$line", node.StartLine.Value);
        command.Parameters.AddWithValue("$rule", node.RuleId);
        var details = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var properties = JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(3)) ?? [];
            string Clean(string? value) => string.IsNullOrWhiteSpace(value)
                ? "unavailable"
                : value.Replace(';', ',').Replace('|', '/');
            details.Add($"factType={Clean(reader.GetString(0))},source={Clean(reader.GetString(1))},target={Clean(reader.GetString(2))},receiver={Clean(Value(properties, "receiverName"))},receiverType={Clean(Value(properties, "receiverType"))},resolution={Clean(Value(properties, "resolutionKind"))},operation={Clean(Value(properties, "operationKind"))},sqlSource={Clean(Value(properties, "sqlSourceKind"))}");
        }
        return details.Count == 0 ? string.Empty : $";factDetails={string.Join("||", details)}";
    }

    private static IReadOnlyList<string> ReadReceiverTypeMethods(SqliteConnection db, SqliteTransaction transaction, string receiverType)
    {
        using var command = db.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "select coalesce(s.label, f.source_index_id), f.project_path, f.file_path, f.start_line, f.rule_id, f.evidence_tier, "
            + "coalesce(cast(json_extract(f.properties_json,'$.containingType') as text),''), "
            + "coalesce(cast(json_extract(f.properties_json,'$.methodName') as text),cast(json_extract(f.properties_json,'$.name') as text),''), "
            + "coalesce(cast(json_extract(f.properties_json,'$.parameterCount') as text),'unavailable') "
            + "from combined_facts f left join index_sources s on s.source_index_id=f.source_index_id "
            + "where f.fact_type='MethodDeclared' and json_valid(f.properties_json) and ("
            + "lower(coalesce(cast(json_extract(f.properties_json,'$.containingType') as text),''))=lower($type) or "
            + "lower(coalesce(cast(json_extract(f.properties_json,'$.containingType') as text),'')) like '%.'||lower($type)) "
            + "order by s.label, f.project_path, f.file_path, f.start_line, f.combined_fact_id limit 101;";
        command.Parameters.AddWithValue("$type", receiverType);
        var values = new List<string>();
        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            if (values.Count == 100) throw new InvalidDataException("ReceiverBridgeAuditInputLimit");
            values.Add($"source={reader.GetString(0)};project={(reader.IsDBNull(1) ? "unavailable" : reader.GetString(1))};member={reader.GetString(6)}.{reader.GetString(7)}/{reader.GetString(8)};site={reader.GetString(2)}:{reader.GetInt32(3)};rule={reader.GetString(4)};tier={reader.GetString(5)}");
        }
        return values;
    }

    private static Fact ReadFact(SqliteDataReader reader) => new(reader.GetString(0), reader.GetString(1),
        reader.IsDBNull(2) ? null : reader.GetString(2), reader.GetString(3), reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.GetString(5), reader.GetInt32(6), reader.GetString(7), reader.GetString(8),
        JsonSerializer.Deserialize<Dictionary<string, string>>(reader.GetString(9)) ?? []);
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
