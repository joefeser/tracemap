namespace TraceMap.Core;

/// <summary>One binding per internal-helper call site: the Fill fact plus the caller index whose path, procedure and consumer it names.</summary>
public sealed record SqlServerInternalHelperResultBinding(string FactId, int CallerIndex, string CommandText, SqlServerObjectIdentity Identity,
    string InternalHelperResultEvidenceSha256);
public sealed record SqlServerInternalHelperResultBindingReference(string FactId, int CallerIndex, string InternalHelperResultEvidenceSha256);
public sealed record SqlServerInternalHelperResultBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerInternalHelperResultBinding> Calls,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerInternalHelperResultComparison(string FactId, int? CallerIndex, int? AccessIndex, string RuleId, string Tier, string Status,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> UncomparedFacets, string SourcePath, int SourceStartLine, int SourceEndLine,
    string? CallerPath, int? CallerStartLine, int? CallerEndLine, string? Caller, string Revision, string SourceExtractor, string SourceExtractorVersion,
    string SourceRuleId, string SourceTier, SqlServerDataTableAccess? Access, SqlServerResultShapeReference? Shape, SqlServerResultColumn? Column,
    SqlServerInternalHelperResultBindingReference? Binding);
/// <param name="SchemaRoute">result-schema, ddl-result or ddl-table-result: the unchanged route that admitted the shapes.</param>
/// <param name="SchemaEvidenceSha256">Digest of the admitted public shape export.</param>
public sealed record SqlServerInternalHelperResultComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ConsumerVersion, string BindingsVersion, string SchemaRoute, string SchemaVersion, string SchemaGeneratorSha256, string SchemaInputSha256,
    string SchemaExtractorVersion, string SchemaRuleId, string SchemaTier, string SchemaEvidenceSha256, string Origin, string Repo, string Revision,
    string Coverage, string Verdict, IReadOnlyList<SqlServerResultShape> Shapes, IReadOnlyList<SqlServerInternalHelperResultComparison> Comparisons,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Offline first-result-set column-identity and ordinal-bound comparison for DataRow reads of the DataTable (or first DataSet
/// table) returned by one bounded internal static helper, per call site in any ordinary document of its compilation. Shapes are
/// admitted only through their unchanged origin routes; the consumer identity is the separate
/// <c>sqlserver-internal-helper-result/1</c> state and one <c>sqlserver-internal-helper-result-bindings/1</c> digest per
/// (fact, caller). Phase 4, DataTable, DataSet, reader and parameter bindings never substitute. No execution,
/// caller-completeness, table, row, later-result, type, nullability or value claim.
/// </summary>
public static class SqlServerInternalHelperResultEvidence
{
    public const string RuleId = "database.sqlserver.internal-helper-result-column-comparison.v1";
    public const string BindingsVersion = "sqlserver-internal-helper-result-bindings/1";
    public const string ReportVersion = "sqlserver-internal-helper-result-comparison/1";
    public const string StateVersion = SqlServerInternalHelperResults.Version;
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("internal-helper-result-" + reason); }

    internal static SqlServerInternalHelperResultState? State(CodeFact f)
        => f.Properties.TryGetValue("sqlServerInternalHelperResultState", out var json) ? SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultState>(json) : null;
    // The contract tag and caller index (whose entry carries its path) keep this digest disjoint from every other route and caller.
    private static object Projection(CodeFact f, int callerIndex)
    {
        var state = State(f);
        return new { contract = BindingsVersion, f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier, f.Evidence.FilePath, f.Evidence.StartLine,
            f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, callerIndex,
            helperState = state is null ? null : state with { Callers = [] },
            caller = state is not null && callerIndex >= 0 && callerIndex < state.Callers.Count ? state.Callers[callerIndex] : null };
    }
    /// <summary>Public internal-helper-result digest that the binding for one call site must name.</summary>
    public static string InternalHelperResultDigest(CodeFact fact, int callerIndex) => SqlServerSchemaEvidence.Digest(Projection(fact, callerIndex));

    public static SqlServerInternalHelperResultComparisonReport CompareResultSchema(string exportJson, string declarationsJson,
        SqlServerInternalHelperResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var schema = SqlServerResultEvidence.Admit(exportJson, declarationsJson, repo, revision);
        return Compare("result-schema", schema.Version, schema.GeneratorSha256, schema.BoundedInputSha256, schema.ExtractorVersion, schema.RuleId,
            schema.Tier, schema.Declarations, schema, bindings, facts, repo, revision);
    }

    public static SqlServerInternalHelperResultComparisonReport CompareDdlResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerInternalHelperResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    public static SqlServerInternalHelperResultComparisonReport CompareDdlTableResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerInternalHelperResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlTableResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-table-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    private static bool InvalidState(SqlServerInternalHelperResultState state)
        => state.ResultKind is not ("datatable" or "dataset") || state.CommandTextSource is not ("constructor-parameter" or "command-text-parameter")
            || state.AdapterAssociation is not ("constructor-command" or "select-command-property")
            || (state.ResultKind == "datatable" ? state.FillOverload != "fill-datatable" || state.TableName is not null
                : state.FillOverload is not ("fill-dataset" or "fill-dataset-source-table") || state.TableName is null || !SqlServerSchemaEvidence.Identifier(state.TableName))
            || state.Callers is null || state.Callers.Count > SqlServerHelperResults.MaxCallers || state.Callers.Any(c => c is null);

    private static bool InvalidCaller(SqlServerInternalHelperResultState state, SqlServerInternalHelperCaller caller)
        => !SqlServerSchemaEvidence.PathSafe(caller.Path) || caller.Line <= 0 || caller.EndLine < caller.Line || caller.CommandTextHash is not { Length: 64 }
            || caller.Selectors is null || caller.Guards is null
            || caller.Selectors.Any(s => state.ResultKind == "datatable" || s is not ("table-local" or "tables-index" or "tables-name"))
            || caller.Guards.Any(g => g is not ("foreach" or "rows-count") && !(g == "tables-count" && state.ResultKind == "dataset"))
            || SqlServerDataTableEvidence.InvalidAccesses(caller.Accesses, 1);

    // Only the three origin-specific admission paths above may enter this engine.
    private static SqlServerInternalHelperResultComparisonReport Compare(string route, string schemaVersion, string schemaGenerator, string schemaInput,
        string schemaExtractor, string schemaRule, string schemaTier, SqlServerResultDeclarations declarations, object schemaProjection,
        SqlServerInternalHelperResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
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
        var map = new Dictionary<(string, int), SqlServerInternalHelperResultBinding>();
        foreach (var b in bindings.Calls!)
            Require(b is not null && SqlServerDataTableEvidence.Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd((b.FactId, b.CallerIndex), b)
                && calls.Any(f => f.FactId == b.FactId && State(f) is { } s && b.CallerIndex >= 0 && b.CallerIndex < s.Callers.Count
                    && InternalHelperResultDigest(f, b.CallerIndex) == b.InternalHelperResultEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        var rows = new List<SqlServerInternalHelperResultComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = State(fact);
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic
                || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor))
                reasons.Add("unsupported-source-extractor-or-tier");
            // Readers, scalar, non-query and other channels carry no internal-helper state and are never helper evidence.
            if (state is null) reasons.Add("internal-helper-result-state-unavailable");
            else if (state.Version != StateVersion || state.Coverage != "bounded") reasons.Add(state.Gap ?? "internal-helper-result-state-unavailable");
            else if (state.CommandType != "StoredProcedure") reasons.Add(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            else if (InvalidState(state)) reasons.Add("invalid-internal-helper-result-state");
            else if (state.Callers.Count == 0) reasons.Add("helper-caller-unavailable");
            SqlServerInternalHelperResultComparison Row(int? caller, int? index, string status, IReadOnlyList<string> why, SqlServerDataTableAccess? access,
                SqlServerResultShape? shape, SqlServerResultColumn? column, IReadOnlyList<string> facets, SqlServerInternalHelperResultBinding? binding)
            {
                var entry = caller is { } c ? state!.Callers[c] : null;
                var reference = shape is null ? null : new SqlServerResultShapeReference(shape.Identity, shape.FilePath, shape.StartLine, shape.EndLine,
                    shape.Completeness, shape.UnknownReason, shape.Columns.Count);
                return new(fact.FactId, caller, index, RuleId, status == "unknown" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, status,
                    why.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), facets, fact.Evidence.FilePath, fact.Evidence.StartLine,
                    fact.Evidence.EndLine, entry?.Path, entry?.Line, entry?.EndLine, entry?.Caller, revision, fact.Evidence.ExtractorId,
                    fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier, access, reference, column, binding is null ? null : BindingProjection(binding));
            }
            if (reasons.Count > 0 || state is null)
            {
                rows.Add(Row(null, null, "unknown", reasons, null, null, null, [], null));
                continue;
            }
            for (var c = 0; c < state.Callers.Count; c++)
            {
                var caller = state.Callers[c];
                var why = new List<string>();
                SqlServerResultShape? shape = null;
                if (caller.Coverage != "bounded") why.Add(caller.Gap ?? "helper-caller-unavailable");
                else if (InvalidCaller(state, caller)) why.Add("invalid-internal-helper-result-state");
                if (!map.TryGetValue((fact.FactId, c), out var binding)) why.Add("binding-missing");
                else
                {
                    shape = declarations.Shapes.SingleOrDefault(s => s.Identity == binding.Identity);
                    if (shape is null) why.Add("exact-result-shape-unavailable");
                    // Each caller binds its own procedure argument; another caller's procedure never substitutes.
                    if (caller.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) why.Add("command-binding-conflict");
                }
                if (why.Count > 0 || shape is null)
                {
                    rows.Add(Row(c, null, "unknown", why, null, shape, null, [], binding));
                    continue;
                }
                if (caller.Accesses.Count == 0)
                {
                    rows.Add(Row(c, null, "unknown", ["no-bounded-column-access"], null, shape, null, [], binding));
                    continue;
                }
                var renaming = SqlServerDataTableEvidence.Renaming(shape);
                for (var index = 0; index < caller.Accesses.Count; index++)
                {
                    var access = caller.Accesses[index];
                    var (status, reasonsForAccess, column, facets) = SqlServerDataTableEvidence.CompareAccess(shape, access, renaming);
                    rows.Add(Row(c, index, status, reasonsForAccess, access, shape, column, facets, binding));
                }
            }
        }
        // Bounded local evidence projection, not a public sanitization boundary. Retains locations, caller paths and caller
        // metadata; never hashes raw SQL, procedure text, snippets, original facts or source-file bytes.
        var schemaDigest = SqlServerSchemaEvidence.Digest(schemaProjection);
        var projection = new { route, schema = schemaProjection,
            bindings = bindings.Calls.OrderBy(b => b.FactId, StringComparer.Ordinal).ThenBy(b => b.CallerIndex).Select(BindingProjection),
            calls = calls.Select(f => Projection(f, -1)),
            callers = calls.SelectMany(f => (State(f)?.Callers ?? []).Select((_, i) => Projection(f, i))),
            resolutions = rows.Select(r => new { r.FactId, r.CallerIndex, r.AccessIndex, r.Shape, r.Status, r.Reasons }) };
        return new(ReportVersion, SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(projection), StateVersion, BindingsVersion,
            route, schemaVersion, schemaGenerator, schemaInput, schemaExtractor, schemaRule, schemaTier, schemaDigest, declarations.Origin, repo, revision,
            "partial", "no-aggregate-clean-verdict", declarations.Shapes, rows,
            ["Local evidence report, not a sanitized shareable export: source paths, caller paths, member identities and column names are retained. Public synthetic shape admission does not establish that source facts are public; do not publish private-input reports or their provenance hashes.",
            "Declared result shapes are public synthetic inputs admitted by their unchanged origin route, not compiler, installed catalog, runtime or deployed observations.",
            "First result set only (DataSet helpers: first DataSet table); column identity/existence and ordinal bounds only. No claim that the helper or Fill executes, that any table or row exists, about later result sets, or about CLR/SQL type conversion, nullability, values or runtime exceptions, even for IsNull.",
            "Exactly one helper level: an internal (VB Friend), static, non-virtual, non-generic, non-extension method with a unique name in single-declaration types, whose body is one bounded Fill(DataTable) or Fill(DataSet[, constant]) shape with the command text taken from one string parameter, returning the filled local. Private helpers are Phase 4 evidence only.",
            "Call sites are the invocations in every ordinary document of the helper's registered compilation, each with its repo-relative caller path. InternalsVisibleTo, generated, linked or external documents, scan-scope exclusion, compilation errors and the compilation bounds make the whole helper unknown; method-group, delegate, AddressOf and nameof references in any document do too. Reflection, dynamic dispatch and other compilations are never observed: enumerated call sites are not a runtime-caller completeness claim.",
            "Each call site binds separately through its own (factId, callerIndex) digest: its caller path, constant procedure argument hash, the helper execution and the returned local's consumer. Another caller's procedure, digest or index, and Phase 4, DataTable, DataSet, reader or parameter bindings, never substitute.",
            "Exact-case unique column names only; DataTable and DataTableCollection case-insensitive runtime fallbacks are not claimed, so case-only names stay unknown. Duplicate shape names (which Fill renames) keep absent names unknown. A missing name or out-of-range ordinal is a mismatch only against an admitted complete shape; it does not prove a runtime failure path executes.",
            "Readers, Field<T>, DataColumn and DataRowVersion access, named or typed DataSets, ORMs, DNN wrappers, multi-level helpers and multiple result sets are outside scope.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate report. Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }

    private static SqlServerInternalHelperResultBindingReference BindingProjection(SqlServerInternalHelperResultBinding binding)
        => new(binding.FactId, binding.CallerIndex, binding.InternalHelperResultEvidenceSha256);
}
