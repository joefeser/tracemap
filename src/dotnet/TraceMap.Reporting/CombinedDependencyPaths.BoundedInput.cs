using Microsoft.Data.Sqlite;
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
            var read = await ReadSingleIndexAsync(connection, options.IndexPath, cancellationToken, budget);
            var endpoints = CombinedDependencyReporter.MatchEndpoints(read.Sources, read.Facts);
            var surfaces = CombinedDependencyReporter.BuildSurfaces(read.Facts, read.Sources);
            var graph = BuildGraph(read, endpoints, surfaces, null, includeLegacyRoots: true, budget);
            return BuildReport(options, read, graph, null);
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
            return report with
            {
                ReportCoverage = "ReducedCoverage",
                Summary = report.Summary with { Truncated = true }
            };
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
                    and case when length(cast(properties_json as blob)) > {{ReportInputBudget.MaxRowTextBytes}} then 0
                        when json_valid(properties_json)
                        then json_type(properties_json, '$.surfaceKind') is null
                        else 0 end as symbol_only
                from facts where {{predicate}}
            ), input as (
                select fact_id, scan_id, repo, commit_sha, fact_type, rule_id, evidence_tier,
                       source_symbol, target_symbol, contract_element, file_path, start_line, end_line,
                       case when symbol_only then '{}' else properties_json end as properties_json,
                       {{(hasExtractorVersion ? "extractor_version" : "null")}} as version, symbol_only
                from projected
            )
            select *, {{TextByteCountSql("fact_id", "scan_id", "repo", "commit_sha", "fact_type", "rule_id", "evidence_tier", "source_symbol", "target_symbol", "contract_element", "file_path", "properties_json", "version")}}
            from input order by fact_id collate binary;
            """;
    }

    private static async Task<IReadOnlyList<CombinedFactRow>> ReadCompactSingleFactsAsync(
        SqliteConnection connection, CombinedReportSource source, bool hasExtractorVersion,
        ReportInputBudget budget, CancellationToken cancellationToken)
    {
        var rows = new List<CombinedFactRow>();
        var symbols = new HashSet<string>(StringComparer.Ordinal);
        var retainedIds = new HashSet<string>(StringComparer.Ordinal);
        var supportIds = new HashSet<string>(StringComparer.Ordinal);
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = CompactFactQuery(hasExtractorVersion);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                budget.VisitFact();
                var bytes = reader.GetInt64(16);
                budget.CheckRow(bytes);
                var sourceSymbol = reader.IsDBNull(7) ? null : reader.GetString(7);
                var targetSymbol = reader.IsDBNull(8) ? null : reader.GetString(8);
                var symbolOnly = reader.GetBoolean(15);
                if (symbolOnly && !NewSymbol(sourceSymbol) && !NewSymbol(targetSymbol)) continue;
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
            foreach (var id in SplitList(row.Properties.GetValueOrDefault("supportingFactIds")))
            {
                supportIds.Add(id);
                if (supportIds.Count > budget.MaxFacts) throw new ReportInputLimitException("support-reference-rows");
            }
        }
    }

    private static CombinedFactRow ReadProjectedFact(SqliteDataReader reader, CombinedReportSource source) => new(
        $"{source.SourceIndexId}:{reader.GetString(0)}", source.SourceIndexId, source.Label,
        reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.GetString(4), reader.GetString(5), reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7), reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetString(9), reader.GetString(10), reader.GetInt32(11), reader.GetInt32(12),
        ParseProperties(reader.GetString(13)), reader.IsDBNull(14) ? null : reader.GetString(14));
}
