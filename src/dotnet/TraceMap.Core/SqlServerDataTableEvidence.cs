namespace TraceMap.Core;

public sealed record SqlServerDataTableBinding(string FactId, string CommandText, SqlServerObjectIdentity Identity, string DataTableEvidenceSha256);
public sealed record SqlServerDataTableBindingReference(string FactId, string DataTableEvidenceSha256);
public sealed record SqlServerDataTableBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerDataTableBinding> Calls,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerDataTableComparison(string FactId, int? AccessIndex, string RuleId, string Tier, string Status,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> UncomparedFacets, string SourcePath, int SourceStartLine, int SourceEndLine,
    string Revision, string SourceExtractor, string SourceExtractorVersion, string SourceRuleId, string SourceTier,
    SqlServerDataTableAccess? Access, SqlServerResultShapeReference? Shape, SqlServerResultColumn? Column, SqlServerDataTableBindingReference? Binding);
/// <param name="SchemaRoute">result-schema, ddl-result or ddl-table-result: the unchanged route that admitted the shapes.</param>
/// <param name="SchemaEvidenceSha256">Digest of the admitted public shape export.</param>
public sealed record SqlServerDataTableComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ConsumerVersion, string BindingsVersion, string SchemaRoute, string SchemaVersion, string SchemaGeneratorSha256, string SchemaInputSha256,
    string SchemaExtractorVersion, string SchemaRuleId, string SchemaTier, string SchemaEvidenceSha256, string Origin, string Repo, string Revision,
    string Coverage, string Verdict, IReadOnlyList<SqlServerResultShape> Shapes, IReadOnlyList<SqlServerDataTableComparison> Comparisons,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Offline first-result-set column-identity and ordinal-bound comparison for DataRow reads of the DataTable filled by one
/// exact <c>SqlDataAdapter.Fill</c>. Shapes are admitted only through their unchanged origin routes (JSON, constant DDL,
/// table-backed DDL); the consumer identity is the separate <c>sqlserver-datatable/1</c> state and
/// <c>sqlserver-datatable-bindings/1</c> digest. No execution, row, type, nullability or value claim.
/// </summary>
public static class SqlServerDataTableEvidence
{
    public const string RuleId = "database.sqlserver.datatable-column-comparison.v1";
    public const string BindingsVersion = "sqlserver-datatable-bindings/1";
    public const string ReportVersion = "sqlserver-datatable-comparison/1";
    public const string StateVersion = SqlServerDataTableConsumers.Version;
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("datatable-" + reason); }
    internal static bool Identity(SqlServerObjectIdentity? i) => i is not null && SqlServerSchemaEvidence.Identifier(i.Server)
        && SqlServerSchemaEvidence.Identifier(i.Database) && SqlServerSchemaEvidence.Identifier(i.Schema) && SqlServerSchemaEvidence.Identifier(i.Procedure);

    internal static SqlServerDataTableState? State(CodeFact f)
        => f.Properties.TryGetValue("sqlServerDataTableState", out var json) ? SqlServerSchemaEvidence.Read<SqlServerDataTableState>(json) : null;
    // The contract tag keeps this digest disjoint from the reader (ReaderDigest) and parameter (CallDigest) projections.
    private static object Projection(CodeFact f) => new { contract = BindingsVersion, f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier,
        f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, dataTableState = State(f) };
    /// <summary>Public DataTable-evidence digest that a DataTable binding must name.</summary>
    public static string DataTableDigest(CodeFact fact) => SqlServerSchemaEvidence.Digest(Projection(fact));

    public static SqlServerDataTableComparisonReport CompareResultSchema(string exportJson, string declarationsJson, SqlServerDataTableBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var schema = SqlServerResultEvidence.Admit(exportJson, declarationsJson, repo, revision);
        return Compare("result-schema", schema.Version, schema.GeneratorSha256, schema.BoundedInputSha256, schema.ExtractorVersion, schema.RuleId,
            schema.Tier, schema.Declarations, schema, bindings, facts, repo, revision);
    }

    public static SqlServerDataTableComparisonReport CompareDdlResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerDataTableBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    public static SqlServerDataTableComparisonReport CompareDdlTableResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerDataTableBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlTableResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-table-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    // Only the three origin-specific admission paths above may enter this engine.
    private static SqlServerDataTableComparisonReport Compare(string route, string schemaVersion, string schemaGenerator, string schemaInput,
        string schemaExtractor, string schemaRule, string schemaTier, SqlServerResultDeclarations declarations, object schemaProjection,
        SqlServerDataTableBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
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
        var map = new Dictionary<string, SqlServerDataTableBinding>(StringComparer.Ordinal);
        foreach (var b in bindings.Calls!)
            Require(b is not null && Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd(b.FactId, b)
                && calls.Any(f => f.FactId == b.FactId && DataTableDigest(f) == b.DataTableEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        var rows = new List<SqlServerDataTableComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = State(fact);
            SqlServerResultShape? shape = null;
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic
                || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor))
                reasons.Add("unsupported-source-extractor-or-tier");
            // Readers, scalar, non-query and other channels carry no DataTable state and are never DataTable evidence.
            if (state is null) reasons.Add("datatable-state-unavailable");
            else if (state.Version != StateVersion || state.Coverage != "bounded") reasons.Add(state.Gap ?? "datatable-state-unavailable");
            else if (state.CommandType != "StoredProcedure") reasons.Add(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            else if (InvalidAccesses(state.Accesses, fact.Evidence.StartLine)) reasons.Add("invalid-datatable-state");
            if (!map.TryGetValue(fact.FactId, out var binding)) reasons.Add("binding-missing");
            else
            {
                shape = declarations.Shapes.SingleOrDefault(s => s.Identity == binding.Identity);
                if (shape is null) reasons.Add("exact-result-shape-unavailable");
                if (state?.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) reasons.Add("command-binding-conflict");
            }
            var reference = shape is null ? null : new SqlServerResultShapeReference(shape.Identity, shape.FilePath, shape.StartLine, shape.EndLine,
                shape.Completeness, shape.UnknownReason, shape.Columns.Count);
            SqlServerDataTableComparison Row(int? index, string status, IReadOnlyList<string> why, SqlServerDataTableAccess? access, SqlServerResultColumn? column, IReadOnlyList<string> facets)
                => new(fact.FactId, index, RuleId, status == "unknown" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, status,
                    why.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), facets, fact.Evidence.FilePath, fact.Evidence.StartLine,
                    fact.Evidence.EndLine, revision, fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier,
                    access, reference, column, binding is null ? null : BindingProjection(binding));
            if (reasons.Count > 0 || shape is null || state is null)
            {
                rows.Add(Row(null, "unknown", reasons, null, null, []));
                continue;
            }
            if (state.Accesses!.Count == 0)
            {
                rows.Add(Row(null, "unknown", ["no-bounded-column-access"], null, null, []));
                continue;
            }
            var renaming = Renaming(shape);
            for (var index = 0; index < state.Accesses.Count; index++)
            {
                var access = state.Accesses[index];
                var (status, why, column, facets) = CompareAccess(shape, access, renaming);
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
            "First result set only; column identity/existence and ordinal bounds only. No claim that Fill executes, that rows exist, or about CLR/SQL type conversion, nullability, values or runtime exceptions, even for IsNull.",
            "Exact-case unique names only. DataTable column lookup falls back to a case-insensitive match at runtime; that is not claimed, so case-only names stay unknown. Duplicate shape names (which Fill renames) keep absent names unknown.",
            "A missing name or out-of-range ordinal is a mismatch only against an admitted complete shape; it does not prove a runtime failure path executes.",
            "Fresh signed SqlCommand local with constant StoredProcedure intent (the #912 rules, cut off at the adapter association), one fresh SqlDataAdapter, one fresh DataTable() filled exactly once by Fill(DataTable), and constant reads through a directly associated DataRow.",
            "Aliases, reassignment, escapes, helpers, repeated Fill, adapter options and mappings, command mutation after association, table/row mutation, ambiguous row ownership and other control flow remain unknown; guards are bound to the selected table.",
            "Readers, DataSet, Field<T>, DataColumn and DataRowVersion access, ORMs, DNN wrappers and multiple result sets are outside scope.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate report. Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }

    /// <summary>Shared DataRow access-state validation (DataTable and DataSet consumers).</summary>
    internal static bool InvalidAccesses(IReadOnlyList<SqlServerDataTableAccess>? accesses, int startLine)
        => accesses is null || accesses.Count > SqlServerDataTableConsumers.MaxAccesses || accesses.Any(a => a is null
            || a.Kind is not ("name-indexer" or "ordinal-indexer" or "isnull-name" or "isnull-ordinal") || a.Member is not ("Item" or "IsNull")
            || a.RowSource is not ("rows-index" or "row-local" or "foreach-row") || a.Line < startLine || a.EndLine < a.Line
            || (a.Name is not null && !SqlServerSchemaEvidence.Identifier(a.Name)) || (a.Gap is null && a.Name is null && a.Ordinal is null));

    /// <summary>Fill renames duplicate result names (Name, Name1, ...); with any duplicate an absent name proves nothing.</summary>
    internal static bool Renaming(SqlServerResultShape shape)
        => shape.Columns.Select(c => c.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count() != shape.Columns.Count;

    /// <summary>Shared DataRow access decision over the #912 column engine plus the Fill renaming and IsNull facets.</summary>
    internal static (string Status, IReadOnlyList<string> Reasons, SqlServerResultColumn? Column, IReadOnlyList<string> Facets) CompareAccess(
        SqlServerResultShape shape, SqlServerDataTableAccess access, bool renaming)
    {
        var (status, why, column) = SqlServerResultEvidence.CompareColumn(shape, access.Gap, access.Name, access.Ordinal);
        if (status == "mismatch" && renaming && access.Name is { } name)
            (status, why, column) = ("unknown", ["datatable-duplicate-column-renaming-unknown:" + name], null);
        var facets = new List<string>();
        if (access.Member == "IsNull") facets.Add("nullability-not-compared:IsNull");
        if (column?.DeclaredType is { } declared) facets.Add("declared-type-not-compared:" + declared);
        return (status, why, column, facets);
    }

    private static SqlServerDataTableBindingReference BindingProjection(SqlServerDataTableBinding binding) => new(binding.FactId, binding.DataTableEvidenceSha256);
}
