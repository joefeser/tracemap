using Microsoft.Data.Sqlite;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // These types have no path semantics beyond providing symbol-node metadata.
    // Declared surfaces and legacy rules are excluded from compaction in SQL.
    // Unknown/new types retain their full payload until explicitly audited.
    private static readonly string[] SymbolWitnessFactTypes =
    [
        FactTypes.TypeDeclared, FactTypes.MethodDeclared, FactTypes.PropertyDeclared,
        FactTypes.FieldDeclared, FactTypes.ParameterDeclared, FactTypes.EnumDeclared,
        FactTypes.AttributeUsed, FactTypes.MemberAccessName, FactTypes.InvocationName,
        FactTypes.ArgumentPassed, FactTypes.PropertyAccessed, FactTypes.MethodInvoked
    ];

    internal static async Task<CombinedDependencyPathReport> BuildBoundedSingleIndexReportAsync(
        CombinedDependencyPathOptions options,
        ReportInputBudget budget,
        CancellationToken cancellationToken = default)
        => (await BuildBoundedSingleIndexReportWithTraversalAsync(options, budget, cancellationToken)).Report;

    internal static async Task<CombinedDependencyPathBuildResult> BuildBoundedSingleIndexReportWithTraversalAsync(
        CombinedDependencyPathOptions options,
        ReportInputBudget budget,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        await using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString());
        await connection.OpenAsync(cancellationToken);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "pragma query_only=on; pragma temp_store=file; pragma cache_size=-8192;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        if (await TableExistsAsync(connection, "index_sources", cancellationToken))
            throw new InvalidDataException("WebFormsModernizationIndexUnsupported");
        var (source, _) = await ReadSingleSourceAsync(connection, options.IndexPath, cancellationToken);
        try
        {
            var read = await ReadSingleIndexAsync(
                connection,
                options.IndexPath,
                cancellationToken,
                budget,
                options.StartingFactIds,
                options.MaxDepth,
                options.MaxFrontier);
            var endpoints = CombinedDependencyReporter.MatchEndpoints(read.Sources, read.Facts);
            var surfaces = CombinedDependencyReporter.BuildSurfaces(read.Facts, read.Sources);
            var graph = BuildGraph(read, endpoints, surfaces, null, includeLegacyRoots: true, budget);
            return BuildReportWithTraversalObservations(options, read, graph, null);
        }
        catch (ReportInputLimitException exception)
        {
            // A partial graph could hide an overload/dispatch competitor. Never
            // classify any path from it, even if some roots had already loaded.
            var warnings = new List<string>();
            AddSingleCoverageWarnings(source, warnings);
            var read = new CombinedReadResult([source], [], warnings, [], [], new Dictionary<string, long>());
            var graph = new EvidenceGraph([source]);
            graph.Gaps.Add(new CombinedPathGap(
                "gap:webforms:graph-input:" + exception.Limit,
                "GraphInputLimitReached", CombinedDependencyPathClassifications.UnknownAnalysisGap,
                "Graph input exceeded a deterministic admission limit; no paths were classified from incomplete input.",
                source.SourceIndexId, source.Label, null, null, TruncationGapRuleId, EvidenceTiers.Tier4Unknown,
                null, null, exception.Limit));
            var report = BuildReport(options, read, graph, null);
            return new CombinedDependencyPathBuildResult(
                report with
                {
                    ReportCoverage = "ReducedCoverage",
                    Summary = report.Summary with { Truncated = true }
                },
                new Dictionary<string, CombinedDependencyTraversalObservation>(StringComparer.Ordinal));
        }
    }

    internal static async Task<CombinedDependencyPathBuildResult> BuildBoundedCombinedIndexReportWithTraversalAsync(
        CombinedDependencyPathOptions options,
        ReportInputBudget budget,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);
        await using (var connection = new SqliteConnection(new SqliteConnectionStringBuilder
        {
            DataSource = options.IndexPath,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private,
            Pooling = false
        }.ToString()))
        {
            await connection.OpenAsync(cancellationToken);
            if (!await TableExistsAsync(connection, "index_sources", cancellationToken)
                || !await TableExistsAsync(connection, "combined_facts", cancellationToken))
            {
                throw new InvalidDataException("WebFormsModernizationCombinedIndexUnsupported");
            }

            await AssertCombinedInputLimitAsync(connection, "combined_facts", budget.MaxFacts, "graph-facts", cancellationToken);
            if (await ViewExistsAsync(connection, "combined_dependency_edges", cancellationToken))
            {
                await AssertCombinedInputLimitAsync(connection, "combined_dependency_edges", budget.MaxEdges, "graph-edges", cancellationToken);
            }
            await using var bytes = connection.CreateCommand();
            bytes.CommandText = "select coalesce(sum(length(cast(payload_json as blob))), 0) from combined_facts;";
            if (Convert.ToInt64(await bytes.ExecuteScalarAsync(cancellationToken)) > budget.MaxTextBytes)
            {
                throw new ReportInputLimitException("graph-text-bytes");
            }
        }

        var sourcePair = ParseSourcePair(options.SourcePair);
        var (read, graph) = await BuildGraphAsync(
            options.IndexPath,
            sourcePair,
            options.IncludeLegacyRoots || IsLegacyView(options.View),
            allowSingleIndex: false,
            cancellationToken,
            budget);
        return BuildReportWithTraversalObservations(options, read, graph, sourcePair);
    }

    private static async Task AssertCombinedInputLimitAsync(
        SqliteConnection connection,
        string objectName,
        int maximum,
        string limit,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"select count(*) from {objectName};";
        if (Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > maximum)
        {
            throw new ReportInputLimitException(limit);
        }
    }

    internal static string TextByteCountSql(params string[] columns) =>
        string.Join(" + ", columns.Select(column => $"coalesce(length(cast({column} as blob)), 0)"));

    private static string CompactFactQuery(bool hasExtractorVersion, string predicate = "1 = 1")
    {
        var types = string.Join(",", SymbolWitnessFactTypes.Select(type => $"'{type}'"));
        // Use SQLite to discard unconsumed large properties before a managed
        // string is allocated. Invalid JSON remains on the ordinary full path.
        return $$"""
            with projected as (
                select *,
                    fact_type in ({{types}}) and rule_id not like 'legacy.%'
                    and not (fact_type = 'MethodInvoked'
                        and rule_id = '{{RuleIds.CSharpSemanticMethodInvocation}}'
                        and (target_symbol like 'global::System.Data.Common.DbDataAdapter.Fill(%'
                            or target_symbol like 'System.Data.Common.DbDataAdapter.Fill(%'
                            or target_symbol like 'global::System.Data.SqlClient.SqlDataAdapter.Fill(%'
                            or target_symbol like 'System.Data.SqlClient.SqlDataAdapter.Fill(%'
                            or target_symbol like 'global::Microsoft.Data.SqlClient.SqlDataAdapter.Fill(%'
                            or target_symbol like 'Microsoft.Data.SqlClient.SqlDataAdapter.Fill(%'))
                    and case when length(cast(properties_json as blob)) > {{ReportInputBudget.MaxRowTextBytes}} then 0
                        when json_valid(properties_json)
                        then json_type(properties_json, '$.surfaceKind') is null
                        else 0 end as symbol_only
                from facts where {{predicate}}
            ), input as (
                select fact_id, scan_id, repo, commit_sha, fact_type, rule_id, evidence_tier,
                       source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                       case when fact_type = '{{FactTypes.CallEdge}}' and json_valid(properties_json) then
                           json_patch(json_object(
                               'argumentCount', coalesce(cast(json_extract(properties_json, '$.argumentCount') as text), ''),
                               'argumentTypes', coalesce(cast(json_extract(properties_json, '$.argumentTypes') as text), ''),
                               'argumentTypeResolution', coalesce(cast(json_extract(properties_json, '$.argumentTypeResolution') as text), ''),
                               'assignedTo', coalesce(cast(json_extract(properties_json, '$.assignedTo') as text), ''),
                               'callKind', coalesce(cast(json_extract(properties_json, '$.callKind') as text), ''),
                               'calleeContainingType', coalesce(cast(json_extract(properties_json, '$.calleeContainingType') as text), ''),
                               'calleeAssemblyName', coalesce(cast(json_extract(properties_json, '$.calleeAssemblyName') as text), ''),
                               'calleeName', coalesce(cast(json_extract(properties_json, '$.calleeName') as text), ''),
                               'callerAssemblyName', coalesce(cast(json_extract(properties_json, '$.callerAssemblyName') as text), ''),
                               'callerName', coalesce(cast(json_extract(properties_json, '$.callerName') as text), ''),
                               'coverageLabel', coalesce(cast(json_extract(properties_json, '$.coverageLabel') as text), ''),
                               'receiverName', coalesce(cast(json_extract(properties_json, '$.receiverName') as text), ''),
                               'targetSymbolId', coalesce(cast(json_extract(properties_json, '$.targetSymbolId') as text), ''),
                               'targetContainingSymbolId', coalesce(cast(json_extract(properties_json, '$.targetContainingSymbolId') as text), '')),
                               case when json_type(properties_json, '$.lexicalScopeStartLine') is not null
                                      or json_type(properties_json, '$.lexicalScopeEndLine') is not null then
                                   json_object(
                                       'lexicalScopeStartLine', coalesce(cast(json_extract(properties_json, '$.lexicalScopeStartLine') as text), ''),
                                       'lexicalScopeEndLine', coalesce(cast(json_extract(properties_json, '$.lexicalScopeEndLine') as text), ''))
                               else '{}' end)
                            when fact_type = '{{FactTypes.MethodDeclared}}'
                                and rule_id = '{{RuleIds.VisualBasicSemanticDeclarations}}'
                                and json_valid(properties_json) then
                            json_object(
                                'containingType', coalesce(cast(json_extract(properties_json, '$.containingType') as text), ''),
                                'methodName', coalesce(cast(json_extract(properties_json, '$.methodName') as text), ''),
                                'parameterCount', coalesce(cast(json_extract(properties_json, '$.parameterCount') as text), ''))
                            when fact_type = '{{FactTypes.MethodDeclared}}'
                                and rule_id = '{{RuleIds.VisualBasicSyntaxDeclarations}}'
                                and json_valid(properties_json) then
                            json_object(
                                'containingType', coalesce(cast(json_extract(properties_json, '$.containingType') as text), ''),
                                'qualifiedContainingType', coalesce(cast(json_extract(properties_json, '$.qualifiedContainingType') as text), ''),
                                'methodName', coalesce(cast(json_extract(properties_json, '$.methodName') as text), ''),
                                'name', coalesce(cast(json_extract(properties_json, '$.name') as text), ''),
                                'memberIdentity', coalesce(cast(json_extract(properties_json, '$.memberIdentity') as text), ''),
                                'parameterCount', coalesce(cast(json_extract(properties_json, '$.parameterCount') as text), ''),
                                'parameterTypes', coalesce(cast(json_extract(properties_json, '$.parameterTypes') as text), ''))
                            when fact_type = '{{FactTypes.FieldDeclared}}'
                                and rule_id = '{{RuleIds.VisualBasicSyntaxDeclarations}}'
                                and json_valid(properties_json) then
                            json_object(
                                'containingType', coalesce(cast(json_extract(properties_json, '$.containingType') as text), ''),
                                'qualifiedContainingType', coalesce(cast(json_extract(properties_json, '$.qualifiedContainingType') as text), ''),
                                'fieldName', coalesce(cast(json_extract(properties_json, '$.fieldName') as text), ''),
                                'fieldType', coalesce(cast(json_extract(properties_json, '$.fieldType') as text), ''))
                            when fact_type = '{{FactTypes.TypeDeclared}}'
                                and rule_id = '{{RuleIds.VisualBasicSyntaxDeclarations}}'
                                and json_valid(properties_json) then
                            json_object(
                                'name', coalesce(cast(json_extract(properties_json, '$.name') as text), ''),
                                'qualifiedName', coalesce(cast(json_extract(properties_json, '$.qualifiedName') as text), ''),
                                'baseTypes', coalesce(cast(json_extract(properties_json, '$.baseTypes') as text), ''))
                            when symbol_only then '{}'
                            else properties_json end as properties_json,
                       {{(hasExtractorVersion ? "extractor_version" : "null")}} as version, symbol_only
                from projected
            )
            select *, {{TextByteCountSql("fact_id", "scan_id", "repo", "commit_sha", "fact_type", "rule_id", "evidence_tier", "source_symbol", "target_symbol", "contract_element", "file_path", "properties_json", "version")}}
            from input order by fact_id collate binary;
            """;
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadCompactSingleFactsAsync(
        SqliteConnection connection, CombinedReportSource source, bool hasExtractorVersion,
        ReportInputBudget budget, CancellationToken cancellationToken,
        IReadOnlySet<string>? selectedFactIds = null,
        IReadOnlySet<string>? selectedSymbols = null)
    {
        var rows = new List<CombinedFactRow>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        var supportIds = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            var targeted = selectedFactIds is not null;
            command.CommandText = CompactFactQuery(hasExtractorVersion, targeted
                ? "fact_id in (select value from json_each($fact_ids)) "
                    + "or source_symbol in (select value from json_each($symbols)) "
                    + "or (fact_type = 'MethodDeclared' and target_symbol in (select value from json_each($symbols))) "
                    + "or (fact_type in ('SymbolRelationship', 'DependencyRegistered') "
                    + "and target_symbol in (select value from json_each($symbols))) "
                    + "or (fact_type = 'WebFormsEventFlowProjected' "
                    + "and length(cast(properties_json as blob)) <= " + ReportInputBudget.MaxRowTextBytes + " "
                    + "and json_valid(properties_json) "
                    + "and exists (select 1 from json_each($fact_ids) selected "
                    + "where instr(',' || replace(replace(coalesce(json_extract(properties_json, '$.supportingFactIds'), ''), ';', ','), '|', ',') || ',', "
                    + "',' || selected.value || ',') > 0))"
                : "1 = 1");
            if (targeted)
            {
                command.Parameters.AddWithValue("$fact_ids", JsonSerializer.Serialize(selectedFactIds!.Order(StringComparer.Ordinal)));
                command.Parameters.AddWithValue("$symbols", JsonSerializer.Serialize(
                    (selectedSymbols ?? new HashSet<string>(StringComparer.Ordinal)).Order(StringComparer.Ordinal)));
            }
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                budget.VisitFact();
                var bytes = reader.GetInt64(16);
                budget.CheckRow(bytes);
                var sourceSymbol = reader.IsDBNull(7) ? null : reader.GetString(7);
                var targetSymbol = reader.IsDBNull(8) ? null : reader.GetString(8);
                var factType = reader.GetString(4);
                var symbolOnly = reader.GetBoolean(15);
                if (symbolOnly
                    && (!targeted || factType != FactTypes.MethodDeclared)
                    && !NewSymbol(sourceSymbol)
                    && !NewSymbol(targetSymbol)) continue;
                budget.Retain(bytes);
                var row = ReadProjectedFact(reader, source);
                rows.Add(row);
                retainedIds.Add(row.OriginalFactId);
                RecordSymbols(row);
                RecordSupport(row);
            }
        }

        // Path provenance normalizes existing supporting IDs to single:<id>.
        // Keep referenced witnesses even when they add no new symbol metadata.
        foreach (var batch in supportIds.Except(retainedIds, StringComparer.Ordinal).Order(StringComparer.Ordinal).Chunk(256))
        {
            await using var command = connection.CreateCommand();
            var names = batch.Select((_, index) => "$id" + index).ToArray();
            // The interpolated SQL contains generated parameter names only;
            // every fact ID value is bound below.
            command.CommandText = CompactFactQuery(hasExtractorVersion, $"fact_id in ({string.Join(',', names)})"); // nosemgrep: csharp.lang.security.sqli.csharp-sqli
            for (var index = 0; index < batch.Length; index++) command.Parameters.AddWithValue(names[index], batch[index]);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                budget.Retain(reader.GetInt64(16));
                rows.Add(ReadProjectedFact(reader, source));
            }
        }

        // A projectless VB handler call can name only a receiver and a method.
        // Admit semantic method candidates plus the compact syntax declaration
        // metadata needed to prove a typed field and its bounded base chain.
        // The graph rule still requires unique receiver and target identities.
        var bridgeMethodNames = rows
            .Where(row => row.FactType == FactTypes.CallEdge
                && row.RuleId == RuleIds.VisualBasicSyntaxCallGraph
                && string.Equals(row.Properties.GetValueOrDefault("callKind"), "SyntaxInvocation", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(row.Properties.GetValueOrDefault("receiverName")))
            .Select(row => row.Properties.GetValueOrDefault("calleeName"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Select(name => name!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (bridgeMethodNames.Length > 0)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = CompactFactQuery(hasExtractorVersion, $$"""
                json_valid(properties_json) and (
                    (fact_type = '{{FactTypes.MethodDeclared}}'
                        and rule_id = '{{RuleIds.VisualBasicSemanticDeclarations}}'
                        and evidence_tier = '{{EvidenceTiers.Tier1Semantic}}'
                        and cast(json_extract(properties_json, '$.methodName') as text) collate nocase
                            in (select value from json_each($method_names)))
                    or (rule_id = '{{RuleIds.VisualBasicSyntaxDeclarations}}' and (
                        fact_type in ('{{FactTypes.TypeDeclared}}','{{FactTypes.FieldDeclared}}')
                        or (fact_type = '{{FactTypes.MethodDeclared}}'
                            and coalesce(
                                cast(json_extract(properties_json, '$.methodName') as text),
                                cast(json_extract(properties_json, '$.name') as text)) collate nocase
                                in (select value from json_each($method_names)))))
                )
                """);
            command.Parameters.AddWithValue("$method_names", JsonSerializer.Serialize(bridgeMethodNames));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                budget.VisitFact();
                var bytes = reader.GetInt64(16);
                budget.CheckRow(bytes);
                var row = ReadProjectedFact(reader, source);
                if (!retainedIds.Add(row.OriginalFactId))
                {
                    continue;
                }

                budget.Retain(bytes);
                rows.Add(row);
            }

            // Syntax-only receiver targets need their member body facts in
            // the bounded graph, not just declaration metadata. Discover
            // exact type/name/arity body symbols from the retained syntax
            // declarations, then admit every fact owned by those symbols so
            // downstream calls and terminals remain connected.
            var syntaxMembers = rows
                .Where(row => row.FactType == FactTypes.MethodDeclared
                    && row.RuleId == RuleIds.VisualBasicSyntaxDeclarations
                    && int.TryParse(CombinedDependencyReporter.FirstValue(row.Properties, "parameterCount"), out _))
                .Select(row => new
                {
                    Type = VisualBasicContainingType(row),
                    Name = CombinedDependencyReporter.FirstValue(row.Properties, "methodName", "name"),
                    Arity = int.Parse(CombinedDependencyReporter.FirstValue(row.Properties, "parameterCount")!)
                })
                .Where(member => !string.IsNullOrWhiteSpace(member.Type) && !string.IsNullOrWhiteSpace(member.Name))
                .Distinct()
                .ToArray();
            if (syntaxMembers.Length > 0)
            {
                var bodySymbols = new SortedSet<string>(StringComparer.Ordinal);
                await using (var bodyCommand = connection.CreateCommand())
                {
                    bodyCommand.CommandText = "select distinct source_symbol from facts "
                        + "where source_symbol is not null and trim(source_symbol) <> '' "
                        + "and exists (select 1 from json_each($method_names) names "
                        + "where instr(lower(source_symbol), lower(cast(names.value as text))) > 0) "
                        + "order by source_symbol collate binary limit $candidate_limit;";
                    bodyCommand.Parameters.AddWithValue("$method_names", JsonSerializer.Serialize(bridgeMethodNames));
                    bodyCommand.Parameters.AddWithValue("$candidate_limit", budget.MaxFacts + 1L);
                    await using var bodyReader = await bodyCommand.ExecuteReaderAsync(cancellationToken);
                    while (await bodyReader.ReadAsync(cancellationToken))
                    {
                        var symbol = bodyReader.GetString(0);
                        var member = VisualBasicQualifiedMemberKey(symbol);
                        if (member is not null && syntaxMembers.Any(candidate =>
                            candidate.Arity == member.Value.Arity
                            && string.Equals(candidate.Name, member.Value.Name, StringComparison.OrdinalIgnoreCase)
                            && VisualBasicTypeMatches(candidate.Type, member.Value.Type)))
                        {
                            bodySymbols.Add(symbol);
                            if (bodySymbols.Count > budget.MaxFacts)
                                throw new ReportInputLimitException("handler-call-target-frontier");
                        }
                    }
                }

                if (bodySymbols.Count > 0)
                {
                    await using var bodyFactsCommand = connection.CreateCommand();
                    bodyFactsCommand.CommandText = CompactFactQuery(hasExtractorVersion,
                        "source_symbol in (select value from json_each($body_symbols))");
                    bodyFactsCommand.Parameters.AddWithValue("$body_symbols", JsonSerializer.Serialize(bodySymbols));
                    await using var bodyFactsReader = await bodyFactsCommand.ExecuteReaderAsync(cancellationToken);
                    while (await bodyFactsReader.ReadAsync(cancellationToken))
                    {
                        var factType = bodyFactsReader.GetString(4);
                        var symbolOnly = bodyFactsReader.GetBoolean(15);
                        if (symbolOnly && factType != FactTypes.MethodInvoked) continue;
                        budget.VisitFact();
                        var bytes = bodyFactsReader.GetInt64(16);
                        budget.CheckRow(bytes);
                        var row = ReadProjectedFact(bodyFactsReader, source);
                        if (!retainedIds.Add(row.OriginalFactId)) continue;
                        budget.Retain(bytes);
                        rows.Add(row);
                    }
                }
            }
        }
        return rows;

        bool NewSymbol(string? symbol) => !string.IsNullOrWhiteSpace(symbol) && !symbols.Contains(symbol);

        void RecordSymbols(CombinedFactRow row)
        {
            // Mirror BuildGraph's insertion order, including its handler early
            // continue and dependency-surface target exclusion.
            if (row.FactType is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved) return;
            if (!string.IsNullOrWhiteSpace(row.SourceSymbol)) symbols.Add(row.SourceSymbol);
            if (!IsDependencySurfaceFact(row) && !string.IsNullOrWhiteSpace(row.TargetSymbol)) symbols.Add(row.TargetSymbol);
        }

        void RecordSupport(CombinedFactRow row)
        {
            foreach (var id in SplitList(row.Properties.GetValueOrDefault("supportingFactIds"))
                .Concat(SplitList(row.Properties.GetValueOrDefault("supportingEdgeIds"))))
            {
                supportIds.Add(id);
                if (supportIds.Count > budget.MaxFacts) throw new ReportInputLimitException("support-reference-rows");
            }
        }
    }

    private static async Task<IReadOnlySet<string>> ReadSelectedSymbolClosureAsync(
        SqliteConnection connection,
        IReadOnlySet<string> selectedFactIds,
        int maxDepth,
        int maxFrontier,
        int maxSupportingFactIds,
        CancellationToken cancellationToken)
    {
        if (selectedFactIds.Count == 0) return new HashSet<string>(StringComparer.Ordinal);

        var originalIds = selectedFactIds
            .Select(id => id.StartsWith("single:", StringComparison.Ordinal) ? id["single:".Length..] : id)
            .Order(StringComparer.Ordinal)
            .ToArray();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $$"""
                select target_symbol, fact_type,
                       case when length(cast(properties_json as blob)) <= {{ReportInputBudget.MaxRowTextBytes}} and json_valid(properties_json)
                            then properties_json else '{}' end
                from facts where fact_id in (select value from json_each($ids)) order by fact_id collate binary;
                """;
            command.Parameters.AddWithValue("$ids", JsonSerializer.Serialize(originalIds));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0))) symbols.Add(reader.GetString(0));
                if (reader.GetString(1) is FactTypes.WebFormsHandlerResolved or FactTypes.WinFormsHandlerResolved)
                {
                    var properties = ParseProperties(reader.GetString(2));
                    foreach (var value in new[]
                    {
                        properties.GetValueOrDefault("handlerSymbol"),
                        properties.GetValueOrDefault("handlerSymbolId")
                    }.Where(value => !string.IsNullOrWhiteSpace(value)))
                    {
                        symbols.Add(value!);
                    }
                }
            }
        }
        symbols.UnionWith(await ReadHandlerOwnedCallSymbolsAsync(
            connection, originalIds, maxFrontier, maxSupportingFactIds, cancellationToken));
        if (symbols.Count > maxFrontier) throw new ReportInputLimitException("graph-frontier");

        var traversalQueries = new List<string>();
        if (await TableExistsAsync(connection, "call_edges", cancellationToken))
            traversalQueries.Add("select callee_symbol as target_symbol from call_edges where caller_symbol in (select value from json_each($symbols))");
        // Retained Tier-1 CallEdge facts are authoritative evidence even when a
        // resumable or externally assembled index is missing the normalized
        // call_edges projection. UNION grouping below removes ordinary-table
        // duplicates without changing traversal order or bounds.
        traversalQueries.Add("select target_symbol from facts where fact_type='CallEdge' and evidence_tier='Tier1Semantic' and source_symbol in (select value from json_each($symbols))");
        if (await TableExistsAsync(connection, "object_creations", cancellationToken))
            traversalQueries.Add("select created_type as target_symbol from object_creations where caller_symbol in (select value from json_each($symbols))");
        if (await TableExistsAsync(connection, "parameter_forward_edges", cancellationToken))
            traversalQueries.Add("select target_method_symbol as target_symbol from parameter_forward_edges where source_method_symbol in (select value from json_each($symbols))");
        if (await TableExistsAsync(connection, "symbol_relationships", cancellationToken)
            && await TableExistsAsync(connection, "symbols", cancellationToken))
        {
            const string relationships = "from symbol_relationships relationships "
                + "left join symbols source_symbols on source_symbols.scan_id = relationships.scan_id and source_symbols.symbol_id = relationships.source_symbol_id "
                + "left join symbols target_symbols on target_symbols.scan_id = relationships.scan_id and target_symbols.symbol_id = relationships.target_symbol_id ";
            traversalQueries.Add("select coalesce(target_symbols.display_name, relationships.target_symbol_id) as target_symbol "
                + relationships
                + "where coalesce(source_symbols.display_name, relationships.source_symbol_id) in (select value from json_each($symbols))");
            traversalQueries.Add("select coalesce(source_symbols.display_name, relationships.source_symbol_id) as target_symbol "
                + relationships
                + "where coalesce(target_symbols.display_name, relationships.target_symbol_id) in (select value from json_each($symbols))");
        }
        if (traversalQueries.Count == 0) return symbols;

        var frontier = symbols.Order(StringComparer.Ordinal).ToArray();
        for (var depth = 0; depth < maxDepth && frontier.Length > 0; depth++)
        {
            var next = new SortedSet<string>(StringComparer.Ordinal);
            await using var command = connection.CreateCommand();
            // The SQL fragments above are fixed scanner-schema queries; only
            // the frontier values are supplied externally and they are bound
            // through one JSON parameter.
            command.CommandText = $"select target_symbol from ({string.Join(" union all ", traversalQueries)}) "
                + "where target_symbol is not null and trim(target_symbol) <> '' "
                + "group by target_symbol "
                + "order by target_symbol collate binary limit $candidate_limit;"; // nosemgrep: csharp.lang.security.sqli.csharp-sqli
            command.Parameters.AddWithValue("$symbols", JsonSerializer.Serialize(frontier));
            command.Parameters.AddWithValue("$candidate_limit", maxFrontier + 1L);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var symbol = reader.GetString(0);
                if (!symbols.Contains(symbol) && next.Add(symbol) && next.Count > maxFrontier)
                    throw new ReportInputLimitException("graph-frontier");
            }

            if (next.Count > maxFrontier
                || (long)symbols.Count + next.Count > (long)maxFrontier * Math.Max(1, maxDepth + 1))
                throw new ReportInputLimitException("graph-frontier");
            symbols.UnionWith(next);
            frontier = next.ToArray();
        }

        return symbols;
    }

    private static async Task<IReadOnlySet<string>> ReadHandlerOwnedCallSymbolsAsync(
        SqliteConnection connection,
        IReadOnlyList<string> selectedHandlerFactIds,
        int maxTargets,
        int maxSupportingFactIds,
        CancellationToken cancellationToken)
    {
        var supportingEdgeIds = new SortedSet<string>(StringComparer.Ordinal);
        var symbols = new SortedSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = $$"""
                select source_symbol, properties_json
                from facts
                where fact_type = 'WebFormsEventFlowProjected'
                  and length(cast(properties_json as blob)) <= {{ReportInputBudget.MaxRowTextBytes}}
                  and json_valid(properties_json)
                  and exists (
                      select 1 from json_each($handler_ids) selected
                      where instr(',' || replace(replace(coalesce(json_extract(properties_json, '$.supportingFactIds'), ''), ';', ','), '|', ',') || ',',
                                  ',' || selected.value || ',') > 0)
                order by fact_id collate binary;
                """;
            command.Parameters.AddWithValue("$handler_ids", JsonSerializer.Serialize(selectedHandlerFactIds));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0) && !string.IsNullOrWhiteSpace(reader.GetString(0)))
                {
                    symbols.Add(reader.GetString(0));
                    if (symbols.Count > maxTargets) throw new ReportInputLimitException("handler-call-symbol-frontier");
                }
                var properties = ParseProperties(reader.GetString(1));
                foreach (var id in SplitList(properties.GetValueOrDefault("supportingEdgeIds")))
                {
                    supportingEdgeIds.Add(id);
                    // Supporting fact identities are an intermediate admission
                    // set, not graph-frontier symbols. Bound them by the fact
                    // budget; the distinct target symbols derived below remain
                    // independently bounded by maxTargets.
                    if (supportingEdgeIds.Count > maxSupportingFactIds)
                        throw new ReportInputLimitException("handler-call-support-facts");
                }
            }
        }

        if (supportingEdgeIds.Count == 0) return symbols;

        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select target_symbol from facts "
                + "where fact_type = 'CallEdge' and fact_id in (select value from json_each($edge_ids)) "
                + "and evidence_tier = 'Tier1Semantic' "
                + "and target_symbol is not null and trim(target_symbol) <> '' order by fact_id collate binary;";
            command.Parameters.AddWithValue("$edge_ids", JsonSerializer.Serialize(supportingEdgeIds));
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                symbols.Add(reader.GetString(0));
                if (symbols.Count > maxTargets) throw new ReportInputLimitException("handler-call-target-frontier");
            }
        }

        var syntaxCalls = new List<(string SourceSymbol, string FilePath, int StartLine, string RuleId, string EvidenceTier, IReadOnlyDictionary<string, string> Properties)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select source_symbol, file_path, start_line, rule_id, evidence_tier, properties_json from facts "
                + "where fact_type = 'CallEdge' and rule_id in ($syntax_rule_id, $semantic_rule_id) "
                + "and fact_id in (select value from json_each($edge_ids)) "
                + "and length(cast(properties_json as blob)) <= $max_row_bytes and json_valid(properties_json) "
                + "order by file_path collate binary, start_line, fact_id collate binary;";
            command.Parameters.AddWithValue("$syntax_rule_id", RuleIds.VisualBasicSyntaxCallGraph);
            command.Parameters.AddWithValue("$semantic_rule_id", RuleIds.VisualBasicSemanticCallGraph);
            command.Parameters.AddWithValue("$edge_ids", JsonSerializer.Serialize(supportingEdgeIds));
            command.Parameters.AddWithValue("$max_row_bytes", ReportInputBudget.MaxRowTextBytes);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                syntaxCalls.Add((
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.GetString(1),
                    reader.GetInt32(2),
                    reader.GetString(3),
                    reader.GetString(4),
                    ParseProperties(reader.GetString(5))));
            }
        }

        var receiverNames = syntaxCalls
            .Where(call => string.Equals(call.Properties.GetValueOrDefault("callKind"), "SyntaxInvocation", StringComparison.Ordinal))
            .Select(call => call.Properties.GetValueOrDefault("receiverName")?.Split('.').Last())
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var receiverFields = new List<(string ContainingType, string FieldName, string FieldType)>();
        var receiverTypeDeclarations = new List<(string TypeName, string BaseTypes)>();
        if (receiverNames.Length > 0)
        {
            await using var fieldCommand = connection.CreateCommand();
            fieldCommand.CommandText = "select properties_json from facts "
                + "where fact_type = $fact_type and rule_id = $rule_id "
                + "and length(cast(properties_json as blob)) <= $max_row_bytes and json_valid(properties_json) "
                + "and cast(json_extract(properties_json, '$.fieldName') as text) collate nocase "
                + "in (select value from json_each($field_names)) "
                + "order by fact_id collate binary limit $candidate_limit;";
            fieldCommand.Parameters.AddWithValue("$fact_type", FactTypes.FieldDeclared);
            fieldCommand.Parameters.AddWithValue("$rule_id", RuleIds.VisualBasicSyntaxDeclarations);
            fieldCommand.Parameters.AddWithValue("$max_row_bytes", ReportInputBudget.MaxRowTextBytes);
            fieldCommand.Parameters.AddWithValue("$field_names", JsonSerializer.Serialize(receiverNames));
            fieldCommand.Parameters.AddWithValue("$candidate_limit", maxTargets + 1L);
            await using var fieldReader = await fieldCommand.ExecuteReaderAsync(cancellationToken);
            while (await fieldReader.ReadAsync(cancellationToken))
            {
                var properties = ParseProperties(fieldReader.GetString(0));
                receiverFields.Add((
                    NormalizeVisualBasicTypeName(CombinedDependencyReporter.FirstValue(properties, "qualifiedContainingType", "containingType")),
                    properties.GetValueOrDefault("fieldName") ?? string.Empty,
                    NormalizeVisualBasicTypeName(CombinedDependencyReporter.FirstValue(properties, "fieldType", "declaredType"))));
                if (receiverFields.Count > maxTargets)
                    throw new ReportInputLimitException("handler-call-target-frontier");
            }

            await using var typeCommand = connection.CreateCommand();
            typeCommand.CommandText = "select properties_json from facts "
                + "where fact_type = $fact_type and rule_id = $rule_id "
                + "and length(cast(properties_json as blob)) <= $max_row_bytes and json_valid(properties_json) "
                + "order by fact_id collate binary limit $candidate_limit;";
            typeCommand.Parameters.AddWithValue("$fact_type", FactTypes.TypeDeclared);
            typeCommand.Parameters.AddWithValue("$rule_id", RuleIds.VisualBasicSyntaxDeclarations);
            typeCommand.Parameters.AddWithValue("$max_row_bytes", ReportInputBudget.MaxRowTextBytes);
            typeCommand.Parameters.AddWithValue("$candidate_limit", maxTargets + 1L);
            await using var typeReader = await typeCommand.ExecuteReaderAsync(cancellationToken);
            while (await typeReader.ReadAsync(cancellationToken))
            {
                var properties = ParseProperties(typeReader.GetString(0));
                receiverTypeDeclarations.Add((
                    NormalizeVisualBasicTypeName(CombinedDependencyReporter.FirstValue(properties, "qualifiedName", "name")),
                    properties.GetValueOrDefault("baseTypes") ?? string.Empty));
                if (receiverTypeDeclarations.Count > maxTargets)
                    throw new ReportInputLimitException("handler-call-target-frontier");
            }
        }

        var bridgeRequests = new List<(string TypeName, string MethodName, int ArgumentCount)>();
        foreach (var call in syntaxCalls.Where(call =>
            string.Equals(call.Properties.GetValueOrDefault("callKind"), "SyntaxInvocation", StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(call.Properties.GetValueOrDefault("receiverName"))
            && !string.IsNullOrWhiteSpace(call.Properties.GetValueOrDefault("calleeName"))
            && int.TryParse(call.Properties.GetValueOrDefault("argumentCount"), out _)))
        {
            var receiver = call.Properties.GetValueOrDefault("receiverName")!;
            var receiverLookupName = receiver.Split('.').Last();
            var creations = syntaxCalls
                .Where(creation => IsSupportedVisualBasicReceiverCreation(
                        creation.RuleId,
                        creation.EvidenceTier,
                        creation.Properties.GetValueOrDefault("callKind"))
                    && SameVisualBasicContainingMember(creation.SourceSymbol, creation.Properties, call.SourceSymbol, call.Properties)
                    && string.Equals(creation.FilePath, call.FilePath, StringComparison.OrdinalIgnoreCase)
                    && creation.StartLine <= call.StartLine
                    && IsVisualBasicReceiverCreationInScope(creation.Properties, call.StartLine, call.StartLine)
                    && string.Equals(creation.Properties.GetValueOrDefault("assignedTo"), receiverLookupName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var receiverTypes = creations.Length == 1
                ? new[] { NormalizeVisualBasicTypeName(creations[0].Properties.GetValueOrDefault("calleeContainingType")
                    ?? creations[0].Properties.GetValueOrDefault("calleeName")) }
                : [];
            if (creations.Length == 0)
            {
                var caller = VisualBasicQualifiedMemberKey(call.SourceSymbol
                    ?? call.Properties.GetValueOrDefault("callerName"));
                if (caller is not null)
                {
                    receiverTypes = FindBoundedVisualBasicReceiverFieldTypes(
                        caller.Value.Type,
                        receiverLookupName,
                        receiver.StartsWith("MyBase.", StringComparison.OrdinalIgnoreCase),
                        receiverFields,
                        receiverTypeDeclarations);
                }
            }
            if (receiverTypes.Length == 1)
            {
                bridgeRequests.Add((
                    receiverTypes[0],
                    call.Properties.GetValueOrDefault("calleeName")!,
                    int.Parse(call.Properties.GetValueOrDefault("argumentCount")!)));
            }
        }
        if (bridgeRequests.Count == 0)
        {
            return symbols;
        }

        var declarationCandidates = new List<(string TargetSymbol, IReadOnlyDictionary<string, string> Properties)>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "select target_symbol, properties_json from facts "
                + "where fact_type = $fact_type and rule_id = $rule_id and evidence_tier = $evidence_tier "
                + "and target_symbol is not null and trim(target_symbol) <> '' "
                + "and length(cast(properties_json as blob)) <= $max_row_bytes and json_valid(properties_json) "
                + "and cast(json_extract(properties_json, '$.methodName') as text) collate nocase "
                + "in (select value from json_each($method_names)) "
                + "order by target_symbol collate binary limit $candidate_limit;";
            command.Parameters.AddWithValue("$fact_type", FactTypes.MethodDeclared);
            command.Parameters.AddWithValue("$rule_id", RuleIds.VisualBasicSemanticDeclarations);
            command.Parameters.AddWithValue("$evidence_tier", EvidenceTiers.Tier1Semantic);
            command.Parameters.AddWithValue("$max_row_bytes", ReportInputBudget.MaxRowTextBytes);
            command.Parameters.AddWithValue("$method_names", JsonSerializer.Serialize(bridgeRequests.Select(request => request.MethodName).Distinct(StringComparer.OrdinalIgnoreCase)));
            command.Parameters.AddWithValue("$candidate_limit", maxTargets + 1L);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                declarationCandidates.Add((reader.GetString(0), ParseProperties(reader.GetString(1))));
                if (declarationCandidates.Count > maxTargets)
                {
                    throw new ReportInputLimitException("handler-call-target-frontier");
                }
            }
        }

        foreach (var request in bridgeRequests)
        {
            var matches = declarationCandidates
                .Where(candidate => VisualBasicTypeMatches(
                        CombinedDependencyReporter.FirstValue(candidate.Properties, "qualifiedContainingType", "containingType") ?? string.Empty,
                        request.TypeName)
                    && string.Equals(candidate.Properties.GetValueOrDefault("methodName"), request.MethodName, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(candidate.Properties.GetValueOrDefault("parameterCount"), out var parameterCount)
                    && parameterCount == request.ArgumentCount)
                .Select(candidate => candidate.TargetSymbol)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            if (matches.Length == 1)
            {
                symbols.Add(matches[0]);
                if (symbols.Count > maxTargets)
                {
                    throw new ReportInputLimitException("handler-call-target-frontier");
                }
            }
        }

        return symbols;

        static bool IsSupportedVisualBasicReceiverCreation(string? ruleId, string? evidenceTier, string? callKind) =>
            ruleId == RuleIds.VisualBasicSyntaxCallGraph
                && string.Equals(callKind, "SyntaxObjectCreation", StringComparison.Ordinal)
            || ruleId == RuleIds.VisualBasicSemanticCallGraph
                && evidenceTier == EvidenceTiers.Tier1Semantic
                && string.Equals(callKind, "SemanticObjectCreation", StringComparison.Ordinal);
    }

    private static string[] FindBoundedVisualBasicReceiverFieldTypes(
        string callerType,
        string fieldName,
        bool startAtBase,
        IReadOnlyList<(string ContainingType, string FieldName, string FieldType)> fields,
        IReadOnlyList<(string TypeName, string BaseTypes)> typeDeclarations)
    {
        var currentType = NormalizeVisualBasicTypeName(callerType);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var depth = 0; !string.IsNullOrWhiteSpace(currentType) && depth < 16 && visited.Add(currentType); depth++)
        {
            if (!startAtBase)
            {
                var matches = fields
                    .Where(field => string.Equals(field.FieldName, fieldName, StringComparison.OrdinalIgnoreCase)
                        && VisualBasicTypeMatches(field.ContainingType, currentType))
                    .Select(field => field.FieldType)
                    .Where(type => !string.IsNullOrWhiteSpace(type))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                if (matches.Length > 0) return matches;
            }

            var declarations = typeDeclarations
                .Where(declaration => VisualBasicTypeMatches(declaration.TypeName, currentType))
                .ToArray();
            if (declarations.Length != 1) return [];
            var baseNames = SplitVisualBasicBaseTypes(declarations[0].BaseTypes);
            if (baseNames.Length != 1) return [];
            var baseDeclarations = typeDeclarations
                .Where(declaration => VisualBasicTypeMatches(declaration.TypeName, baseNames[0]))
                .ToArray();
            if (baseDeclarations.Length != 1) return [];
            currentType = baseDeclarations[0].TypeName;
            startAtBase = false;
        }
        return [];
    }

    private static CombinedFactRow ReadProjectedFact(SqliteDataReader reader, CombinedReportSource source) => new(
        $"{source.SourceIndexId}:{reader.GetString(0)}", source.SourceIndexId, source.Label,
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12),
        ParseProperties(reader.GetString(13)), reader.IsDBNull(14) ? null : reader.GetString(14));
}
