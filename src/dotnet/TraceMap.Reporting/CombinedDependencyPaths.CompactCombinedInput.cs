using Microsoft.Data.Sqlite;

namespace TraceMap.Reporting;

public static partial class CombinedDependencyPathReporter
{
    // Share the audited single-index property projection, but retain every
    // combined row. No root scope or symbol witness pruning is applied here:
    // cross-source overload, dispatch, attachment and provenance competitors
    // must remain visible to the existing global graph rules.
    internal static async Task<IReadOnlyList<CombinedFactRow>> ReadCompactCombinedFactsAsync(
        SqliteConnection connection, IReadOnlyList<CombinedReportSource> sources,
        bool hasExtractorId, bool hasExtractorVersion, ReportInputBudget budget, CancellationToken token,
        IIndexedCombinedFacts? storage = null)
    {
        await using (var identity = connection.CreateCommand())
        {
            identity.CommandText = """
                select exists(select 1 from combined_facts where combined_fact_id is null
                    or source_index_id is null or original_fact_id is null
                    or combined_fact_id <> source_index_id || ':' || original_fact_id);
                """;
            if (Convert.ToInt64(await identity.ExecuteScalarAsync(token)) != 0)
                throw new InvalidDataException("COMBINED_FACT_NAMESPACE_INVALID");
            identity.CommandText = """
                select exists(select 1 from combined_facts f where not exists
                    (select 1 from index_sources s where s.source_index_id=f.source_index_id));
                """;
            if (Convert.ToInt64(await identity.ExecuteScalarAsync(token)) != 0)
                throw new InvalidDataException("COMBINED_FACT_SOURCE_UNAVAILABLE");
        }
        var rows = storage is null ? new List<CombinedFactRow>() : null;
        foreach (var source in sources.OrderBy(source => source.SourceIndexId, StringComparer.Ordinal))
        {
            token.ThrowIfCancellationRequested();
            await using var command = connection.CreateCommand();
            // The normalized CTE is connection-local and read-only. Source IDs
            // are parameters; no input value is interpolated into SQL. Original
            // IDs retain the combine builder's exact source:<original> framing.
            command.CommandText = $$"""
                with facts as (
                    select original_fact_id as fact_id, scan_id, repo, commit_sha,
                           fact_type, rule_id, evidence_tier, source_symbol, target_symbol,
                           contract_element, file_path, start_line, end_line, properties_json,
                           {{(hasExtractorId ? "extractor_id" : "null")}} as extractor_id,
                           {{(hasExtractorVersion ? "extractor_version" : "null")}} as extractor_version
                    from combined_facts where source_index_id = $source
                ),
                """ + CompactFactQuery(hasExtractorVersion, hasExtractorId)["with ".Length..];
            command.Parameters.AddWithValue("$source", source.SourceIndexId);
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                budget.VisitFact();
                var bytes = reader.GetInt64(17);
                budget.Retain(bytes);
                var fact = ReadProjectedFact(reader, source, combinedParser: true);
                if (storage is null) rows!.Add(fact); else storage.Add(fact);
            }
        }
        return storage ?? (IReadOnlyList<CombinedFactRow>)rows!;
    }
}
