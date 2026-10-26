namespace TraceMap.Core;

public sealed record SqlServerDataSetBinding(string FactId, string CommandText, SqlServerObjectIdentity Identity, string DataSetEvidenceSha256);
public sealed record SqlServerDataSetBindingReference(string FactId, string DataSetEvidenceSha256);
public sealed record SqlServerDataSetBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerDataSetBinding> Calls,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerDataSetComparison(string FactId, int? AccessIndex, string RuleId, string Tier, string Status,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> UncomparedFacets, string SourcePath, int SourceStartLine, int SourceEndLine,
    string Revision, string SourceExtractor, string SourceExtractorVersion, string SourceRuleId, string SourceTier,
    SqlServerDataTableAccess? Access, SqlServerResultShapeReference? Shape, SqlServerResultColumn? Column, SqlServerDataSetBindingReference? Binding);
/// <param name="SchemaRoute">result-schema, ddl-result or ddl-table-result: the unchanged route that admitted the shapes.</param>
/// <param name="SchemaEvidenceSha256">Digest of the admitted public shape export.</param>
public sealed record SqlServerDataSetComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ConsumerVersion, string BindingsVersion, string SchemaRoute, string SchemaVersion, string SchemaGeneratorSha256, string SchemaInputSha256,
    string SchemaExtractorVersion, string SchemaRuleId, string SchemaTier, string SchemaEvidenceSha256, string Origin, string Repo, string Revision,
    string Coverage, string Verdict, IReadOnlyList<SqlServerResultShape> Shapes, IReadOnlyList<SqlServerDataSetComparison> Comparisons,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Offline first-result-set column-identity and ordinal-bound comparison for DataRow reads of the first table of the DataSet
/// filled by one exact <c>SqlDataAdapter.Fill(DataSet[, constant])</c>. Shapes are admitted only through their unchanged origin
/// routes (JSON, constant DDL, table-backed DDL); the consumer identity is the separate <c>sqlserver-dataset/1</c> state and
/// <c>sqlserver-dataset-bindings/1</c> digest. No execution, table, row, later-result, type, nullability or value claim.
/// </summary>
public static class SqlServerDataSetEvidence
{
    public const string RuleId = "database.sqlserver.dataset-column-comparison.v1";
    public const string BindingsVersion = "sqlserver-dataset-bindings/1";
    public const string ReportVersion = "sqlserver-dataset-comparison/1";
    public const string StateVersion = SqlServerDataSetConsumers.Version;
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("dataset-" + reason); }

    internal static SqlServerDataSetState? State(CodeFact f)
        => f.Properties.TryGetValue("sqlServerDataSetState", out var json) ? SqlServerSchemaEvidence.Read<SqlServerDataSetState>(json) : null;
    // The contract tag keeps this digest disjoint from the DataTable, reader and parameter projections.
    private static object Projection(CodeFact f) => new { contract = BindingsVersion, f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier,
        f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, dataSetState = State(f) };
    /// <summary>Public DataSet-evidence digest that a DataSet binding must name.</summary>
    public static string DataSetDigest(CodeFact fact) => SqlServerSchemaEvidence.Digest(Projection(fact));

    public static SqlServerDataSetComparisonReport CompareResultSchema(string exportJson, string declarationsJson, SqlServerDataSetBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var schema = SqlServerResultEvidence.Admit(exportJson, declarationsJson, repo, revision);
        return Compare("result-schema", schema.Version, schema.GeneratorSha256, schema.BoundedInputSha256, schema.ExtractorVersion, schema.RuleId,
            schema.Tier, schema.Declarations, schema, bindings, facts, repo, revision);
    }

    public static SqlServerDataSetComparisonReport CompareDdlResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerDataSetBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    public static SqlServerDataSetComparisonReport CompareDdlTableResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerDataSetBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlTableResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-table-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    private static bool InvalidState(SqlServerDataSetState state, int startLine)
        => state.FillOverload is not ("fill-dataset" or "fill-dataset-source-table") || state.TableName is null || !SqlServerSchemaEvidence.Identifier(state.TableName)
            || state.Selectors is null || state.Selectors.Any(s => s is not ("table-local" or "tables-index" or "tables-name"))
            || state.Guards is null || state.Guards.Any(g => g is not ("foreach" or "rows-count" or "tables-count"))
            || SqlServerDataTableEvidence.InvalidAccesses(state.Accesses, startLine);

    // Only the three origin-specific admission paths above may enter this engine.
    private static SqlServerDataSetComparisonReport Compare(string route, string schemaVersion, string schemaGenerator, string schemaInput,
        string schemaExtractor, string schemaRule, string schemaTier, SqlServerResultDeclarations declarations, object schemaProjection,
        SqlServerDataSetBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        Require(bindings.Provenance is null || SqlServerSchemaEvidence.Sha(bindings.Provenance.GeneratorSha256) && SqlServerSchemaEvidence.Sha(bindings.Provenance.BoundedInputSha256), "invalid-binding-provenance");
        Require(bindings.Version == BindingsVersion && bindings.Repo == repo && bindings.Revision == revision
            && bindings.Calls is { Count: <= 4096 }, "invalid-binding-envelope");
        Require(facts.Count <= 1000000, "fact-limit");
        var calls = facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).OrderBy(f => f.FactId, StringComparer.Ordinal).ToArray();
        Require(calls.All(f => f.Repo == repo && f.CommitSha == revision && f.Evidence is not null && f.Properties is not null
            && !string.IsNullOrWhiteSpace(f.FactId) && SqlServerSchemaEvidence.PathSafe(f.Evidence.FilePath) && f.Evidence.StartLine > 0
            && f.Evidence.EndLine >= f.Evidence.StartLine), "invalid-call-identity-or-location");
        Require(calls.Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() == calls.Length, "duplicate-call-identity");
        var map = new Dictionary<string, SqlServerDataSetBinding>(StringComparer.Ordinal);
        foreach (var b in bindings.Calls!)
            Require(b is not null && SqlServerDataTableEvidence.Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd(b.FactId, b)
                && calls.Any(f => f.FactId == b.FactId && DataSetDigest(f) == b.DataSetEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        var rows = new List<SqlServerDataSetComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = State(fact);
            SqlServerResultShape? shape = null;
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic
                || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor))
                reasons.Add("unsupported-source-extractor-or-tier");
            // Readers, DataTable fills, scalar, non-query and other channels carry no bounded DataSet state.
            if (state is null) reasons.Add("dataset-state-unavailable");
            else if (state.Version != StateVersion || state.Coverage != "bounded") reasons.Add(state.Gap ?? "dataset-state-unavailable");
            else if (state.CommandType != "StoredProcedure") reasons.Add(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            else if (InvalidState(state, fact.Evidence.StartLine)) reasons.Add("invalid-dataset-state");
            if (!map.TryGetValue(fact.FactId, out var binding)) reasons.Add("binding-missing");
            else
            {
                shape = declarations.Shapes.SingleOrDefault(s => s.Identity == binding.Identity);
                if (shape is null) reasons.Add("exact-result-shape-unavailable");
                if (state?.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) reasons.Add("command-binding-conflict");
            }
            var reference = shape is null ? null : new SqlServerResultShapeReference(shape.Identity, shape.FilePath, shape.StartLine, shape.EndLine,
                shape.Completeness, shape.UnknownReason, shape.Columns.Count);
            SqlServerDataSetComparison Row(int? index, string status, IReadOnlyList<string> why, SqlServerDataTableAccess? access, SqlServerResultColumn? column, IReadOnlyList<string> facets)
                => new(fact.FactId, index, RuleId, status == "unknown" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, status,
                    why.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), facets, fact.Evidence.FilePath, fact.Evidence.StartLine,
                    fact.Evidence.EndLine, revision, fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier,
                    access, reference, column, binding is null ? null : BindingProjection(binding));
            if (reasons.Count > 0 || shape is null || state is null)
            {
                rows.Add(Row(null, "unknown", reasons, null, null, []));
                continue;
            }
            if (state.Accesses.Count == 0)
            {
                rows.Add(Row(null, "unknown", ["no-bounded-column-access"], null, null, []));
                continue;
            }
            // An admitted complete shape has at least one column, so the first result creates the first DataSet table.
            var renaming = SqlServerDataTableEvidence.Renaming(shape);
            for (var index = 0; index < state.Accesses.Count; index++)
            {
                var access = state.Accesses[index];
                var (status, why, column, facets) = SqlServerDataTableEvidence.CompareAccess(shape, access, renaming);
                rows.Add(Row(index, status, why, access, column, facets));
            }
        }
        // Allowlist projection only: never hash raw SQL, snippets, original facts or private source bytes.
        var schemaDigest = SqlServerSchemaEvidence.Digest(schemaProjection);
        var projection = new { route, schema = schemaProjection,
            bindings = bindings.Calls.OrderBy(b => b.FactId, StringComparer.Ordinal).Select(BindingProjection),
            calls = calls.Select(Projection),
            resolutions = rows.Select(r => new { r.FactId, r.AccessIndex, r.Shape, r.Status, r.Reasons }) };
        return new(ReportVersion, SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(projection), StateVersion, BindingsVersion,
            route, schemaVersion, schemaGenerator, schemaInput, schemaExtractor, schemaRule, schemaTier, schemaDigest, declarations.Origin, repo, revision,
            "partial", "no-aggregate-clean-verdict", declarations.Shapes, rows,
            ["Declared result shapes are public synthetic inputs admitted by their unchanged origin route, not compiler, installed catalog, runtime or deployed observations.",
            "First result set and first DataSet table only; column identity/existence and ordinal bounds only. No claim that Fill executes, that any table or row exists, about later result sets, or about CLR/SQL type conversion, nullability, values or runtime exceptions, even for IsNull.",
            "Tables[0] and the exact-case default table name (Table, or the constant source table) of a fresh adapter with default mappings select the first table; DataTableCollection's runtime case-insensitive fallback is not claimed, so case-only table names stay unknown, as do later tables and other names.",
            "Exact-case unique column names only. DataTable column lookup falls back to a case-insensitive match at runtime; that is not claimed, so case-only names stay unknown. Duplicate shape names (which Fill renames) keep absent names unknown.",
            "A missing name or out-of-range ordinal is a mismatch only against an admitted complete shape; it does not prove a runtime failure path executes.",
            "Fresh signed SqlCommand local with constant StoredProcedure intent (the #912 rules, cut off at the adapter association), one fresh SqlDataAdapter, one fresh untyped DataSet() filled exactly once by Fill(DataSet) or Fill(DataSet, constant), and constant reads through rows of its first table.",
            "Aliases, reassignment, escapes, helpers, repeated Fill, adapter options and mappings, command mutation after association, DataSet/table/row mutation, Relations, Merge, ReadXml, typed or named DataSets, ambiguous row ownership and other control flow remain unknown; guards are bound to the selected DataSet and table.",
            "Readers, Fill(DataTable) (the separate DataTable route), Field<T>, DataColumn and DataRowVersion access, ORMs, DNN wrappers and multiple result sets are outside scope.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate report. Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }

    private static SqlServerDataSetBindingReference BindingProjection(SqlServerDataSetBinding binding) => new(binding.FactId, binding.DataSetEvidenceSha256);
}
