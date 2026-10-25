using System.Text.RegularExpressions;

namespace TraceMap.Core;

/// <param name="DeclaredType">Visible, never compared declared type facet (for example a CAST target); null when not declared.</param>
public sealed record SqlServerResultColumn(string Name, int Ordinal, int Line, string? DeclaredType);
/// <param name="Completeness">complete or unknown. An unknown shape never establishes a missing column or ordinal bound.</param>
public sealed record SqlServerResultShape(SqlServerObjectIdentity Identity, string FilePath, int StartLine, int EndLine,
    string ResultSet, string Completeness, string? UnknownReason, IReadOnlyList<SqlServerResultColumn> Columns);
public sealed record SqlServerResultDeclarations(string Version, string Repo, string Revision, string Origin, string IdentifierPolicy,
    IReadOnlyList<SqlServerResultShape> Shapes, SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerResultSchemaExport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string ExtractorVersion, string RuleId, string Tier, SqlServerResultDeclarations Declarations);
public sealed record SqlServerResultBinding(string FactId, string CommandText, SqlServerObjectIdentity Identity, string ReaderEvidenceSha256);
public sealed record SqlServerResultBindings(string Version, string Repo, string Revision, IReadOnlyList<SqlServerResultBinding> Calls,
    SqlServerInputProvenance? Provenance = null);
public sealed record SqlServerResultShapeReference(SqlServerObjectIdentity Identity, string FilePath, int StartLine, int EndLine,
    string Completeness, string? UnknownReason, int ColumnCount);
public sealed record SqlServerResultComparison(string FactId, int? AccessIndex, string RuleId, string Tier, string Status,
    IReadOnlyList<string> Reasons, IReadOnlyList<string> UncomparedFacets, string SourcePath, int SourceStartLine, int SourceEndLine,
    string Revision, string SourceExtractor, string SourceExtractorVersion, string SourceRuleId, string SourceTier,
    SqlServerReaderAccess? Access, SqlServerResultShapeReference? Shape, SqlServerResultColumn? Column, SqlServerResultBinding? Binding);
public sealed record SqlServerResultComparisonReport(string Version, string GeneratorSha256, string BoundedInputSha256,
    string SchemaVersion, string SchemaGeneratorSha256, string SchemaInputSha256, string SchemaExtractorVersion, string SchemaRuleId,
    string SchemaTier, string Origin, string Repo, string Revision, string Coverage, string Verdict,
    IReadOnlyList<SqlServerResultComparison> Comparisons, IReadOnlyList<string> Limitations);

/// <summary>
/// Offline first-result-set column-identity and ordinal-bound comparison. Admission proves integrity of
/// public synthetic inputs only; no type, nullability, cardinality, value, execution or deployment claim.
/// </summary>
public static class SqlServerResultEvidence
{
    public const string ExtractorVersion = "sqlserver-synthetic-result-declarations/1";
    public const string SchemaRuleId = "database.sqlserver.synthetic-result-shape.v1";
    public const string RuleId = "database.sqlserver.result-column-comparison.v1";
    public const int MaxShapes = 256;
    public const int MaxColumns = 1024;
    private static readonly Regex TypeFacet = new(@"^[A-Za-z_][A-Za-z0-9_]{0,63}(\([A-Za-z0-9]{1,5}(,[0-9]{1,3})?\))?$", RegexOptions.CultureInvariant);
    private static readonly Regex ReasonSlug = new("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.CultureInvariant);
    private static void Require([System.Diagnostics.CodeAnalysis.DoesNotReturnIf(false)] bool ok, string reason) { if (!ok) throw new ArgumentException("result-" + reason); }
    private static bool Identity(SqlServerObjectIdentity? i) => i is not null && SqlServerSchemaEvidence.Identifier(i.Server)
        && SqlServerSchemaEvidence.Identifier(i.Database) && SqlServerSchemaEvidence.Identifier(i.Schema) && SqlServerSchemaEvidence.Identifier(i.Procedure);

    internal static void Validate(SqlServerResultDeclarations d, bool ddl = false)
    {
        Require(d.Provenance is null || SqlServerSchemaEvidence.Sha(d.Provenance.GeneratorSha256) && SqlServerSchemaEvidence.Sha(d.Provenance.BoundedInputSha256), "invalid-input-provenance");
        Require(d.Version == (ddl ? "sqlserver-ddl-result-declarations/1" : "sqlserver-result-declarations/1")
            && d.Origin == (ddl ? "public-synthetic-tsql-ddl-result" : "independent-synthetic-result-declarations")
            && d.IdentifierPolicy == "ordinal-exact-no-aliases", "unsupported-declaration-contract");
        Require(SqlServerSchemaEvidence.PathSafe(d.Repo) && !d.Repo.Contains('/') && SqlServerSchemaEvidence.Sha(d.Revision, 40), "invalid-repo-or-revision");
        Require(d.Shapes is { Count: > 0 and <= MaxShapes }, "shape-limit");
        var identities = new HashSet<SqlServerObjectIdentity>();
        foreach (var s in d.Shapes)
        {
            Require(s is not null && Identity(s.Identity) && identities.Add(s.Identity), "duplicate-or-invalid-object");
            Require(SqlServerSchemaEvidence.PathSafe(s!.FilePath) && s.StartLine > 0 && s.EndLine >= s.StartLine && s.ResultSet == "first", "invalid-shape-location");
            Require(s.Columns is { Count: <= MaxColumns } && (s.Completeness == "complete" ? s.UnknownReason is null && s.Columns.Count > 0
                : s.Completeness == "unknown" && s.UnknownReason is not null && ReasonSlug.IsMatch(s.UnknownReason)), "invalid-completeness-state");
            for (var i = 0; i < s.Columns!.Count; i++)
            {
                var c = s.Columns[i];
                // Duplicate names are legal in SQL result sets; they are admitted and compared as unknown.
                Require(c is not null && SqlServerSchemaEvidence.Identifier(c.Name) && c.Ordinal == i && c.Line >= s.StartLine && c.Line <= s.EndLine
                    && (c.DeclaredType is null || TypeFacet.IsMatch(c.DeclaredType)), "invalid-column");
            }
        }
    }

    public static SqlServerResultSchemaExport Produce(string declarationsJson)
    {
        var d = SqlServerSchemaEvidence.Read<SqlServerResultDeclarations>(declarationsJson);
        Validate(d);
        return new("sqlserver-result-schema/1", SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(d), ExtractorVersion,
            SchemaRuleId, EvidenceTiers.Tier2Structural, d);
    }

    public static SqlServerResultSchemaExport Admit(string exportJson, string declarationsJson, string repo, string revision)
    {
        var expected = Produce(declarationsJson);
        var actual = SqlServerSchemaEvidence.Read<SqlServerResultSchemaExport>(exportJson);
        Require(actual.Version == "sqlserver-result-schema/1" && actual.ExtractorVersion == ExtractorVersion, "unsupported-export-version");
        Require(SqlServerSchemaEvidence.Json(actual) == SqlServerSchemaEvidence.Json(expected), "schema-integrity-or-generator-mismatch");
        Require(actual.Declarations.Repo == repo && actual.Declarations.Revision == revision, "unexpected-input-identity");
        return actual;
    }

    internal static SqlServerReaderState? ReaderState(CodeFact f)
        => f.Properties.TryGetValue("sqlServerReaderState", out var json) ? SqlServerSchemaEvidence.Read<SqlServerReaderState>(json) : null;
    private static object ReaderProjection(CodeFact f) => new { f.FactId, f.Repo, f.CommitSha, f.RuleId, f.EvidenceTier,
        f.Evidence.FilePath, f.Evidence.StartLine, f.Evidence.EndLine, f.Evidence.ExtractorId, f.Evidence.ExtractorVersion, state = ReaderState(f) };
    /// <summary>Public reader-evidence digest that a result binding must name. Distinct from the parameter CallDigest.</summary>
    public static string ReaderDigest(CodeFact fact) => SqlServerSchemaEvidence.Digest(ReaderProjection(fact));

    public static SqlServerResultComparisonReport Compare(SqlServerResultSchemaExport schema, SqlServerResultBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        // Library callers cannot bypass admission.
        schema = Admit(SqlServerSchemaEvidence.Json(schema), SqlServerSchemaEvidence.Json(schema.Declarations), repo, revision);
        return CompareAdmitted(schema.Version, schema.GeneratorSha256, schema.BoundedInputSha256, schema.ExtractorVersion, schema.RuleId,
            schema.Tier, schema.Declarations, schema, bindings, facts, repo, revision);
    }

    // Only origin-specific admission paths may enter the shared comparison engine.
    internal static SqlServerResultComparisonReport CompareAdmitted(string schemaVersion, string schemaGenerator, string schemaInput,
        string schemaExtractor, string schemaRule, string schemaTier, SqlServerResultDeclarations declarations, object schemaProjection,
        SqlServerResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        Require(bindings.Provenance is null || SqlServerSchemaEvidence.Sha(bindings.Provenance.GeneratorSha256) && SqlServerSchemaEvidence.Sha(bindings.Provenance.BoundedInputSha256), "invalid-binding-provenance");
        Require(bindings.Version == "sqlserver-result-bindings/1" && bindings.Repo == repo && bindings.Revision == revision
            && bindings.Calls is { Count: <= 4096 }, "invalid-binding-envelope");
        Require(facts.Count <= 1000000, "fact-limit");
        var calls = facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate).OrderBy(f => f.FactId, StringComparer.Ordinal).ToArray();
        Require(calls.All(f => f.Repo == repo && f.CommitSha == revision && f.Evidence is not null && f.Properties is not null
            && !string.IsNullOrWhiteSpace(f.FactId) && SqlServerSchemaEvidence.PathSafe(f.Evidence.FilePath) && f.Evidence.StartLine > 0
            && f.Evidence.EndLine >= f.Evidence.StartLine), "invalid-call-identity-or-location");
        Require(calls.Select(f => f.FactId).Distinct(StringComparer.Ordinal).Count() == calls.Length, "duplicate-call-identity");
        var map = new Dictionary<string, SqlServerResultBinding>(StringComparer.Ordinal);
        foreach (var b in bindings.Calls!)
            Require(b is not null && Identity(b.Identity) && !string.IsNullOrEmpty(b.FactId)
                && b.CommandText == b.Identity.Schema + "." + b.Identity.Procedure && map.TryAdd(b.FactId, b)
                && calls.Any(f => f.FactId == b.FactId && ReaderDigest(f) == b.ReaderEvidenceSha256), "invalid-ambiguous-stale-or-substituted-binding");
        var rows = new List<SqlServerResultComparison>();
        foreach (var fact in calls)
        {
            var reasons = new List<string>();
            var state = ReaderState(fact);
            SqlServerResultShape? shape = null;
            if (fact.RuleId != RuleIds.DatabaseOperationCallPattern || fact.EvidenceTier != EvidenceTiers.Tier1Semantic
                || fact.Evidence.ExtractorVersion is not (ScannerVersions.CSharpSemanticExtractor or ScannerVersions.VisualBasicSemanticExtractor))
                reasons.Add("unsupported-source-extractor-or-tier");
            // Scalar, non-query and other channels carry no reader state and are never result-set evidence.
            if (state is null) reasons.Add("reader-state-unavailable");
            else if (state.Version != SqlServerReaderColumns.Version || state.Coverage != "bounded") reasons.Add(state.Gap ?? "reader-state-unavailable");
            else if (state.CommandType != "StoredProcedure") reasons.Add(state.CommandType == "Text" ? "text-command-outside-scope" : "stored-procedure-intent-unavailable");
            else if (state.Accesses is null || state.Accesses.Count > SqlServerReaderColumns.MaxAccesses || state.Accesses.Any(a => a is null
                || a.Kind is not ("name-indexer" or "ordinal-indexer" or "ordinal-getter" or "get-ordinal") || a.Line < fact.Evidence.StartLine || a.EndLine < a.Line
                || (a.Name is not null && !SqlServerSchemaEvidence.Identifier(a.Name)) || (a.Gap is null && a.Name is null && a.Ordinal is null)))
                reasons.Add("invalid-reader-state");
            if (!map.TryGetValue(fact.FactId, out var binding)) reasons.Add("binding-missing");
            else
            {
                shape = declarations.Shapes.SingleOrDefault(s => s.Identity == binding.Identity);
                if (shape is null) reasons.Add("exact-result-shape-unavailable");
                if (state?.CommandTextHash != FactFactory.Hash(binding.CommandText, 64)) reasons.Add("command-binding-conflict");
            }
            var reference = shape is null ? null : new SqlServerResultShapeReference(shape.Identity, shape.FilePath, shape.StartLine, shape.EndLine,
                shape.Completeness, shape.UnknownReason, shape.Columns.Count);
            SqlServerResultComparison Row(int? index, string status, IReadOnlyList<string> why, SqlServerReaderAccess? access, SqlServerResultColumn? column, IReadOnlyList<string> facets)
                => new(fact.FactId, index, RuleId, status == "unknown" ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, status,
                    why.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray(), facets, fact.Evidence.FilePath, fact.Evidence.StartLine,
                    fact.Evidence.EndLine, revision, fact.Evidence.ExtractorId, fact.Evidence.ExtractorVersion, fact.RuleId, fact.EvidenceTier,
                    access, reference, column, binding);
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
            for (var index = 0; index < state.Accesses.Count; index++)
            {
                var access = state.Accesses[index];
                var (status, why, column) = CompareAccess(shape, access);
                var facets = new List<string>();
                if (access.Member is not ("Item" or "GetOrdinal" or "GetValue")) facets.Add((access.Member == "IsDBNull" ? "nullability" : "getter-type") + "-not-compared:" + access.Member);
                if (column?.DeclaredType is { } declared) facets.Add("declared-type-not-compared:" + declared);
                rows.Add(Row(index, status, why, access, column, facets));
            }
        }
        // Allowlist projection only: never hash raw SQL, snippets, original facts or private source bytes.
        var projection = new { schema = schemaProjection, bindings, calls = calls.Select(ReaderProjection) };
        return new("sqlserver-result-comparison/1", SqlServerSchemaEvidence.GeneratorSha256, SqlServerSchemaEvidence.Digest(projection),
            schemaVersion, schemaGenerator, schemaInput, schemaExtractor, schemaRule, schemaTier, declarations.Origin, repo, revision,
            "partial", "no-aggregate-clean-verdict", rows,
            ["Declared result shapes are public synthetic inputs, not compiler, installed catalog, runtime or deployed observations.",
            "First result set only; column identity/existence and ordinal bounds only. No CLR/SQL type conversion, nullability, row cardinality, value or runtime-exception claim, even for typed getters or IsDBNull.",
            "Exact-case unique names only; case-only, collation-dependent and duplicate names, dynamic names/ordinals, unknown or incomplete shapes and ambiguous bindings remain unknown.",
            "A missing name or out-of-range ordinal is a mismatch only against an admitted complete shape; it does not prove a runtime failure path executes.",
            "Fresh signed SqlCommand local with constant StoredProcedure intent and a directly assigned SqlDataReader from that command's ExecuteReader(); aliases, reassignment, escapes, helpers, NextResult and other control flow remain unknown.",
            "Scalar, non-query, RETURN, OUTPUT and affected-row channels are never result-set evidence; DataTable/DataSet, adapters, ORMs and DNN wrappers are outside scope.",
            "Reachability, source build/scan gaps and truncation are not cleared by this separate result-column report.",
            "Self-consistent hashes prove integrity only, not authenticity, SQL execution, deployment or private acceptance."]);
    }

    private static (string Status, IReadOnlyList<string> Reasons, SqlServerResultColumn? Column) CompareAccess(SqlServerResultShape shape, SqlServerReaderAccess access)
    {
        if (shape.Completeness != "complete") return ("unknown", ["result-shape-unknown:" + shape.UnknownReason], null);
        if (access.Gap is not null) return ("unknown", [access.Gap], null);
        if (access.Name is { } name)
        {
            var exact = shape.Columns.Where(c => c.Name == name).ToArray();
            var folded = shape.Columns.Where(c => string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length > 1) return ("unknown", ["duplicate-declared-column:" + name], null);
            // Provider lookup falls back to case-insensitive matching under an unresolved collation.
            if (folded.Length > exact.Length) return ("unknown", ["column-case-policy-unknown:" + name], exact.SingleOrDefault());
            if (exact.Length == 1) return ("bounded-match", [], exact[0]);
            return ("mismatch", ["missing-column:" + name], null);
        }
        var ordinal = access.Ordinal!.Value;
        if (ordinal >= 0 && ordinal < shape.Columns.Count) return ("bounded-match", [], shape.Columns[ordinal]);
        return ("mismatch", ["ordinal-out-of-range:" + ordinal.ToString(System.Globalization.CultureInfo.InvariantCulture)], null);
    }
}
