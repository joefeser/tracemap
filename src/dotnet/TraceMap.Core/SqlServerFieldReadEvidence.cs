namespace TraceMap.Core;

/// <summary>One binding per field-read site: the Fill fact plus the site index whose procedure and Field reads it names.</summary>
public sealed record SqlServerFieldReadBinding(string FactId, int SiteIndex, string CommandText, SqlServerObjectIdentity Identity, string FieldReadEvidenceSha256);
public sealed record SqlServerFieldReadBindingReference(string FactId, int SiteIndex, string FieldReadEvidenceSha256);
public sealed record SqlServerFieldReadBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerFieldReadBinding> Calls,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerFieldReadComparison(string FactId, int? SiteIndex, int? AccessIndex, string RuleId, string Tier, string Status,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> UncomparedFacets, string SourcePath, int SourceStartLine, int SourceEndLine,
    string? Origin, string? SitePath, int? SiteStartLine, int? SiteEndLine, string? SiteMember, string Revision, string SourceExtractor,
    string SourceExtractorVersion, string SourceRuleId, string SourceTier, SqlServerFieldReadAccess? Access, SqlServerResultShapeReference? Shape,
    SqlServerResultColumn? Column, SqlServerFieldReadBindingReference? Binding);
/// <param name="SchemaRoute">result-schema, ddl-result or ddl-table-result: the unchanged route that admitted the shapes.</param>
/// <param name="SchemaEvidenceSha256">Digest of the admitted public shape export.</param>
public sealed record SqlServerFieldReadComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ConsumerVersion, string BindingsVersion, string SchemaRoute, string SchemaVersion, string SchemaGeneratorSha256, string SchemaInputSha256,
    string SchemaExtractorVersion, string SchemaRuleId, string SchemaTier, string SchemaEvidenceSha256, string Origin, string Repo, string Revision,
    string Coverage, string Verdict, IReadOnlyList<SqlServerResultShape> Shapes, IReadOnlyList<SqlServerFieldReadComparison> Comparisons,
    IReadOnlyList<string> Limitations);

/// <summary>
/// Offline first-result-set column-identity and ordinal-bound comparison for <c>Field&lt;T&gt;</c> / <c>Field(Of T)</c> reads (and the
/// indexer/IsNull reads at the same site) on DataRows bound by the unchanged Phase 2-5 rules, per site. Shapes are admitted only
/// through their unchanged origin routes; the consumer identity is the separate <c>sqlserver-field-read/1</c> state and one
/// <c>sqlserver-field-read-bindings/1</c> digest per (fact, site). DataTable, DataSet, helper, internal-helper, reader and parameter
/// bindings never substitute. The type argument is never compared. No execution, row, later-result, type, conversion, nullability
/// or value claim.
/// </summary>
public static class SqlServerFieldReadEvidence
{
    public const string RuleId = "database.sqlserver.field-read-column-comparison.v1";
    public const string BindingsVersion = "sqlserver-field-read-bindings/1";
    public const string ReportVersion = "sqlserver-field-read-comparison/1";
    public const string StateVersion = SqlServerFieldReads.Version;
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("field-read-" + reason); }

    internal static SqlServerFieldReadState? State(CodeFact f)
        => f.Properties.TryGetValue("sqlServerFieldReadState", out var json) ? SqlServerSchemaEvidence.Read<SqlServerFieldReadState>(json) : null;
    // The contract tag and site index keep this digest disjoint from every other route and from the other sites of one fact.
    private static object Projection(CodeFact f, int siteIndex)
    {
        var state = State(f);
        return new { contract = BindingsVersion, f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier, f.Evidence.FilePath, f.Evidence.StartLine,
            f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, siteIndex,
            fieldState = state is null ? null : state with { Sites = [] },
            site = state is not null && siteIndex >= 0 && siteIndex < state.Sites.Count ? state.Sites[siteIndex] : null };
    }
    /// <summary>Public field-read digest that the binding for one site must name.</summary>
    public static string FieldReadDigest(CodeFact fact, int siteIndex) => SqlServerSchemaEvidence.Digest(Projection(fact, siteIndex));

    public static SqlServerFieldReadComparisonReport CompareResultSchema(string exportJson, string declarationsJson, SqlServerFieldReadBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var schema = SqlServerResultEvidence.Admit(exportJson, declarationsJson, repo, revision);
        return Compare("result-schema", schema.Version, schema.GeneratorSha256, schema.BoundedInputSha256, schema.ExtractorVersion, schema.RuleId,
            schema.Tier, schema.Declarations, schema, bindings, facts, repo, revision);
    }

    public static SqlServerFieldReadComparisonReport CompareDdlResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerFieldReadBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    public static SqlServerFieldReadComparisonReport CompareDdlTableResult(string exportJson, string inputJson, IReadOnlyDictionary<string, string> sources,
        SqlServerFieldReadBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var ddl = SqlServerDdlTableResultEvidence.Admit(exportJson, inputJson, sources, repo, revision);
        return Compare("ddl-table-result", ddl.Version, ddl.GeneratorSha256, ddl.BoundedInputSha256, ddl.ExtractorVersion, ddl.RuleId, ddl.Tier,
            ddl.Declarations, ddl, bindings, facts, repo, revision);
    }

    private static bool InvalidState(SqlServerFieldReadState state)
    {
        if (state.Gap is not null || state.Sites is null || state.OriginGaps is not { Count: 0 } || state.Sites.Any(s => s is null)) return true;
        var direct = state.Origin is "datatable" or "dataset";
        if (direct ? state.Sites.Count != 1 || state.CommandTextSource is not null
                : state.Origin is not ("helper" or "internal-helper") || state.Sites.Count > SqlServerHelperResults.MaxCallers
                    || state.CommandTextSource is not ("constructor-parameter" or "command-text-parameter")) return true;
        if (direct && state.ResultKind != state.Origin) return true;
        return state.ResultKind is not ("datatable" or "dataset") || state.AdapterAssociation is not ("constructor-command" or "select-command-property")
            || (state.ResultKind == "datatable" ? state.FillOverload != "fill-datatable" || state.TableName is not null
                : state.FillOverload is not ("fill-dataset" or "fill-dataset-source-table") || state.TableName is null || !SqlServerSchemaEvidence.Identifier(state.TableName));
    }

    private static bool InvalidSite(SqlServerFieldReadState state, SqlServerFieldReadSite site, int startLine)
        => site.Gap is not null || (state.Origin == "internal-helper" ? !SqlServerSchemaEvidence.PathSafe(site.Path) : site.Path is not null)
            || site.Line <= 0 || site.EndLine < site.Line || site.CommandTextHash is not { Length: 64 } || site.Selectors is null || site.Guards is null
            || site.Selectors.Any(s => state.ResultKind == "datatable" || s is not ("table-local" or "tables-index" or "tables-name"))
            || site.Guards.Any(g => g is not ("foreach" or "rows-count") && !(g == "tables-count" && state.ResultKind == "dataset"))
            || InvalidAccesses(site.Accesses, state.Origin is "datatable" or "dataset" ? startLine : 1);

    /// <summary>Phase 2 access kinds plus Field reads; a bounded site carries at least one Field read and every type argument is bounded.</summary>
    internal static bool InvalidAccesses(IReadOnlyList<SqlServerFieldReadAccess>? accesses, int startLine)
        => accesses is null || accesses.Count == 0 || accesses.Count > SqlServerDataTableConsumers.MaxAccesses || !accesses.Any(a => a?.Member == "Field")
            || accesses.Any(a => a is null || (a.Kind, a.Member) is not (("name-indexer" or "ordinal-indexer", "Item") or ("isnull-name" or "isnull-ordinal", "IsNull")
                    or ("field-name" or "field-ordinal", "Field"))
                || (a.Member == "Field" ? a.TypeArgument is not null && !SqlServerDataTableConsumers.BoundedTypeArgument(a.TypeArgument) : a.TypeArgument is not null)
                || (a.Member == "Field" && a.TypeArgument is null && a.Gap is null)
                || SqlServerDataTableEvidence.InvalidAccesses([a.ToDataTableAccess() with { Kind = "name-indexer", Member = "Item" }], startLine));

    // Only the three origin-specific admission paths above may enter this engine.
    private static SqlServerFieldReadComparisonReport Compare(string route, string schemaVersion, string schemaGenerator, string schemaInput,
        string schemaExtractor, string schemaRule, string schemaTier, SqlServerResultDeclarations declarations, object schemaProjection,
        SqlServerFieldReadBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
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
        var map = new Dictionary<(string, int), SqlServerFieldReadBinding>();
        foreach (var b in bindings.Calls!)
            Require(b is not null && SqlServerDataTableEvidence.Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd((b.FactId, b.SiteIndex), b)
                && calls.Any(f => f.FactId == b.FactId && State(f) is { } s && b.SiteIndex >= 0 && b.SiteIndex < s.Sites.Count
                    && FieldReadDigest(f, b.SiteIndex) == b.FieldReadEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        var rows = new List<SqlServerFieldReadComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = State(fact);
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic
                || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor))
                reasons.Add("unsupported-source-extractor-or-tier");
            // Readers, scalar, non-query and other channels carry no field-read state and are never Field evidence.
            if (state is null) reasons.Add("field-read-state-unavailable");
            else if (state.Version != StateVersion || state.Coverage != "bounded")
            {
                reasons.Add(state.Gap ?? "field-read-state-unavailable");
                // The specific per-origin refusal (for example datatable:datacolumn-access-unsupported) stays explicit.
                if (state.Version == StateVersion && state.OriginGaps is { Count: <= 4 } gaps && gaps.All(g => g is { Length: <= 160 })) reasons.AddRange(gaps);
            }
            else if (state.CommandType != "StoredProcedure") reasons.Add(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            else if (InvalidState(state)) reasons.Add("invalid-field-read-state");
            else if (state.Sites.Count == 0) reasons.Add("helper-caller-unavailable");
            SqlServerFieldReadComparison Row(int? siteIndex, int? index, string status, IReadOnlyList<string> why, SqlServerFieldReadAccess? access,
                SqlServerResultShape? shape, SqlServerResultColumn? column, IReadOnlyList<string> facets, SqlServerFieldReadBinding? binding)
            {
                var site = siteIndex is { } s ? state!.Sites[s] : null;
                var reference = shape is null ? null : new SqlServerResultShapeReference(shape.Identity, shape.FilePath, shape.StartLine, shape.EndLine,
                    shape.Completeness, shape.UnknownReason, shape.Columns.Count);
                return new(fact.FactId, siteIndex, index, RuleId, status == "unknown" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, status,
                    why.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), facets, fact.Evidence.FilePath, fact.Evidence.StartLine,
                    fact.Evidence.EndLine, site is null ? null : state!.Origin, site?.Path, site?.Line, site?.EndLine, site?.Caller, revision,
                    fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier, access, reference, column,
                    binding is null ? null : BindingProjection(binding));
            }
            if (reasons.Count > 0 || state is null)
            {
                rows.Add(Row(null, null, "unknown", reasons, null, null, null, [], null));
                continue;
            }
            for (var s = 0; s < state.Sites.Count; s++)
            {
                var site = state.Sites[s];
                var why = new List<string>();
                SqlServerResultShape? shape = null;
                if (site.Coverage != "bounded") why.Add(site.Gap ?? "field-read-site-unavailable");
                else if (InvalidSite(state, site, fact.Evidence.StartLine)) why.Add("invalid-field-read-state");
                if (!map.TryGetValue((fact.FactId, s), out var binding)) why.Add("binding-missing");
                else
                {
                    shape = declarations.Shapes.SingleOrDefault(x => x.Identity == binding.Identity);
                    if (shape is null) why.Add("exact-result-shape-unavailable");
                    // Each site binds its own command text or procedure argument; another site's procedure never substitutes.
                    // An unknown site has no hash to conflict with; it stays unknown for its own reason.
                    if (site.Coverage == "bounded" && site.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) why.Add("command-binding-conflict");
                }
                if (why.Count > 0 || shape is null)
                {
                    rows.Add(Row(s, null, "unknown", why, null, shape, null, [], binding));
                    continue;
                }
                var renaming = SqlServerDataTableEvidence.Renaming(shape);
                for (var index = 0; index < site.Accesses.Count; index++)
                {
                    var access = site.Accesses[index];
                    var (status, accessReasons, column, facets) = SqlServerDataTableEvidence.CompareAccess(shape, access.ToDataTableAccess(), renaming);
                    // Recorded, never compared: Field<T> makes no CLR/SQL type, conversion, nullability or DBNull claim.
                    if (access.Member == "Field") facets = [.. facets, "field-type-argument-not-compared:" + (access.TypeArgument ?? "unavailable")];
                    rows.Add(Row(s, index, status, accessReasons, access, shape, column, facets, binding));
                }
            }
        }
        // Bounded local evidence projection, not a public sanitization boundary. Retains locations, site paths and member identities;
        // never hashes raw SQL, procedure text, snippets, original facts or source-file bytes.
        var schemaDigest = SqlServerSchemaEvidence.Digest(schemaProjection);
        var projection = new { route, schema = schemaProjection,
            bindings = bindings.Calls.OrderBy(b => b.FactId, StringComparer.Ordinal).ThenBy(b => b.SiteIndex).Select(BindingProjection),
            calls = calls.Select(f => Projection(f, -1)),
            sites = calls.SelectMany(f => (State(f)?.Sites ?? []).Select((_, i) => Projection(f, i))),
            resolutions = rows.Select(r => new { r.FactId, r.SiteIndex, r.AccessIndex, r.Shape, r.Status, r.Reasons }) };
        return new(ReportVersion, SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(projection), StateVersion, BindingsVersion,
            route, schemaVersion, schemaGenerator, schemaInput, schemaExtractor, schemaRule, schemaTier, schemaDigest, declarations.Origin, repo, revision,
            "partial", "no-aggregate-clean-verdict", declarations.Shapes, rows,
            ["Local evidence report, not a sanitized shareable export: source paths, site paths, member identities, column names and type arguments are retained. Public synthetic shape admission does not establish that source facts are public; do not publish private-input reports or their provenance hashes.",
            "Declared result shapes are public synthetic inputs admitted by their unchanged origin route, not compiler, installed catalog, runtime or deployed observations.",
            "First result set only (DataSet: first DataSet table); column identity/existence and ordinal bounds only. No claim that Fill or a helper executes, that any table or row exists, about later result sets, or about CLR/SQL type conversion, nullability, DBNull handling, values or runtime exceptions.",
            "Exactly one widening of the unchanged DataTable, DataSet, private-helper and internal-helper row rules: DataRowExtensions.Field<T>(string) and Field<T>(int) in the C# extension form and the VB reduced form, with the defining assembly checked. The generic type argument is recorded but never compared.",
            "Field<T>(DataColumn), DataRowVersion overloads, SetField, AsEnumerable and LINQ pipelines, explicit static calls, Field reads inside helper bodies, rows of tables the rules did not bind, lookalike assemblies and unrenderable type arguments remain unknown. A site without a bound Field read is field-read-absent here and stays the evidence of its own DataTable, DataSet or helper route.",
            "Each site binds separately through its own (factId, siteIndex) digest. DataTable, DataSet, helper, internal-helper, reader and parameter bindings or digests, and another site's index, digest or procedure, never substitute.",
            "Exact-case unique column names only; DataTable and DataTableCollection case-insensitive runtime fallbacks are not claimed, so case-only names stay unknown. Duplicate shape names (which Fill renames) keep absent names unknown. A missing name or out-of-range ordinal is a mismatch only against an admitted complete shape; it does not prove a runtime failure path executes.",
            "Readers, named or typed DataSets, ORMs, DNN wrappers, multi-level helpers and multiple result sets are outside scope; helper caller completeness is not claimed.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate report. Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }

    private static SqlServerFieldReadBindingReference BindingProjection(SqlServerFieldReadBinding binding)
        => new(binding.FactId, binding.SiteIndex, binding.FieldReadEvidenceSha256);
}
