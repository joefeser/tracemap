using System.Text.RegularExpressions;
using TraceMap.Core;
using TraceMap.Reporting;
namespace TraceMap.Tests;
/// <summary>Phase 6 Field&lt;T&gt; read comparison, per-site bindings and substitution refusals. Expectations are authored independently of the extractor.</summary>
public sealed class SqlServerFieldReadEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultDeclarations Declarations()
        => new("sqlserver-result-declarations/1", "synthetic", Revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            [Shape(Items, new SqlServerResultColumn("Id", 0, 1, "int"), new SqlServerResultColumn("Name", 1, 1, null), new SqlServerResultColumn("Total", 2, 1, null)),
                Shape(Other, new SqlServerResultColumn("Title", 0, 1, null))]);
    private static SqlServerFieldReadAccess Field(string? name, int? ordinal = null, string? type = "System.String")
        => new(name is null ? "field-ordinal" : "field-name", "Field", "rows-index", 0, name, ordinal, type, 12, 12, null);
    private static SqlServerFieldReadAccess Indexer(string name) => new("name-indexer", "Item", "rows-index", 0, name, null, null, 12, 12, null);
    private static SqlServerFieldReadSite Site(string procedure, string? path = null, params SqlServerFieldReadAccess[] accesses)
        => new(path, 11, 11, "Callers.Use()", "bounded", null, FactFactory.Hash(procedure, 64), [], [], accesses);
    private static SqlServerFieldReadState Direct(params SqlServerFieldReadAccess[] accesses) => new("sqlserver-field-read/1", "bounded", null, "datatable", [],
        "datatable", "StoredProcedure", null, "constructor-command", "fill-datatable", null, [Site("dbo.ReadItems", null, accesses)]);
    private static SqlServerFieldReadState Internal(params SqlServerFieldReadSite[] sites) => new("sqlserver-field-read/1", "bounded", null, "internal-helper", [],
        "datatable", "StoredProcedure", "constructor-parameter", "constructor-command", "fill-datatable", null, sites);
    private static CodeFact Fact(SqlServerFieldReadState? state, string? version = null)
    {
        var properties = new Dictionary<string, string>();
        if (state is not null) properties["sqlServerFieldReadState"] = SqlServerSchemaEvidence.Json(state);
        properties["sqlServerDataTableState"] = SqlServerSchemaEvidence.Json(new SqlServerDataTableState("sqlserver-datatable/1", "unknown",
            "datarow-field-extension-unsupported", null, null, null, null, null, "none", []));
        return new("fact", "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern,
            EvidenceTiers.Tier1Semantic, "Tables.M()", null, null,
            new("proj/Tables.cs", 7, 7, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor), properties);
    }
    private static SqlServerFieldReadBinding Binding(CodeFact fact, int site, SqlServerObjectIdentity? identity = null, string? digest = null)
    {
        var i = identity ?? Items;
        return new(fact.FactId, site, i.Schema + "." + i.Procedure, i, digest ?? SqlServerFieldReadEvidence.FieldReadDigest(fact, site));
    }
    private static SqlServerFieldReadBindings Bindings(params SqlServerFieldReadBinding[] calls) => new(SqlServerFieldReadEvidence.BindingsVersion, "synthetic", Revision, calls);
    private static SqlServerFieldReadComparisonReport Compare(CodeFact fact, SqlServerFieldReadBindings? bindings = null)
    {
        var d = Declarations();
        return SqlServerFieldReadEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), bindings ?? Bindings(Binding(fact, 0)), [fact], fact.Repo, fact.CommitSha);
    }
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }

    [Fact]
    public void Field_reads_compare_names_and_ordinals_and_record_but_never_compare_the_type_argument()
    {
        var fact = Fact(Direct(Indexer("Id"), Field("Name"), Field(null, 0, "System.Int64"), Field("Missing"), Field(null, 3), Field("name")));
        var report = Compare(fact);
        Assert.Equal(("sqlserver-field-read-comparison/1", "sqlserver-field-read/1", "sqlserver-field-read-bindings/1", "partial", "no-aggregate-clean-verdict"),
            (report.Version, report.ConsumerVersion, report.BindingsVersion, report.Coverage, report.Verdict));
        Assert.Equal([M, M, M, X, X, U], report.Comparisons.Select(r => r.Status));
        Assert.All(report.Comparisons, r => Assert.Equal((SqlServerFieldReadEvidence.RuleId, "datatable", 0, "proj/Tables.cs"), (r.RuleId, r.Origin, r.SiteIndex, r.SourcePath)));
        // Field<long> over a declared int column is still a match: the type argument is a facet, never a comparison.
        Assert.Equal(["declared-type-not-compared:int", "field-type-argument-not-compared:System.Int64"], report.Comparisons[2].UncomparedFacets);
        Assert.Equal(["declared-type-not-compared:int"], report.Comparisons[0].UncomparedFacets);
        var markdown = SqlServerFieldReadReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown, StringComparison.Ordinal);
        Assert.Contains("datatable site #0 at proj/Tables.cs:11–11 in Callers.Use()", markdown, StringComparison.Ordinal);
        Assert.Contains("field-ordinal Field of System.Int64", markdown, StringComparison.Ordinal);
        Assert.Contains("not a sanitized shareable export", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Each_internal_helper_site_binds_its_own_procedure_and_path()
    {
        var fact = Fact(Internal(Site("dbo.ReadItems", "proj/Callers.cs", Field("Name")), Site("dbo.ReadOther", "proj/More.cs", Field("Title")),
            new("proj/More.cs", 14, 14, "More.Use()", "unknown", "field-read-absent", null, [], [], [])));
        var report = Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 1, Other), Binding(fact, 2)));
        Assert.Equal([M, M, U], report.Comparisons.Select(r => r.Status));
        Assert.Equal(["proj/Callers.cs", "proj/More.cs", "proj/More.cs"], report.Comparisons.Select(r => r.SitePath));
        Assert.Equal(["field-read-absent"], report.Comparisons[2].Reasons);
        var conflict = Compare(fact, Bindings(Binding(fact, 0, Other)));
        Assert.Contains("command-binding-conflict", conflict.Comparisons[0].Reasons);
        Assert.Equal(["binding-missing"], conflict.Comparisons[1].Reasons);
    }

    [Fact]
    public void Other_route_bindings_digests_indices_and_tampering_never_substitute()
    {
        var fact = Fact(Internal(Site("dbo.ReadItems", "proj/Callers.cs", Field("Name")), Site("dbo.ReadOther", "proj/More.cs", Field("Title"))));
        var first = SqlServerFieldReadEvidence.FieldReadDigest(fact, 0);
        foreach (var other in new[] { SqlServerDataTableEvidence.DataTableDigest(fact), SqlServerDataSetEvidence.DataSetDigest(fact), SqlServerHelperResultEvidence.HelperResultDigest(fact, 0),
            SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(fact, 0), SqlServerResultEvidence.ReaderDigest(fact), SqlServerSchemaEvidence.CallDigest(fact) })
        {
            Assert.NotEqual(other, first);
            Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0, digest: other))));
        }
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 1, Other, first))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0, digest: SqlServerFieldReadEvidence.FieldReadDigest(fact, 1)))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0) with { SiteIndex = 2 })));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 0))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0)) with { Version = SqlServerInternalHelperResultEvidence.BindingsVersion }));
        // The type argument and the access are inside the digest.
        var retyped = Fact(Internal(Site("dbo.ReadItems", "proj/Callers.cs", Field("Name", type: "System.Int32")), Site("dbo.ReadOther", "proj/More.cs", Field("Title"))));
        Assert.Throws<ArgumentException>(() => Compare(retyped, Bindings(Binding(fact, 0))));
        // Other routes' binding documents are refused by the strict readers in both directions.
        var internalBindings = new SqlServerInternalHelperResultBindings(SqlServerInternalHelperResultEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, 0, "dbo.ReadItems", Items, first)]);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerFieldReadBindings>(SqlServerSchemaEvidence.Json(internalBindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultBindings>(SqlServerSchemaEvidence.Json(Bindings(Binding(fact, 0)))));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataTableBindings>(SqlServerSchemaEvidence.Json(Bindings(Binding(fact, 0)))));
    }

    [Fact]
    public void Unknown_unbound_tampered_and_old_extractor_states_are_never_field_evidence()
    {
        static SqlServerFieldReadComparison Only(SqlServerFieldReadComparisonReport report) => Assert.Single(report.Comparisons);
        Assert.Contains("field-read-state-unavailable", Only(Compare(Fact(null), Bindings())).Reasons);
        var unbound = new SqlServerFieldReadState("sqlserver-field-read/1", "unknown", "field-read-origin-unbound",
            null, ["datatable:datacolumn-access-unsupported", "dataset:fill-overload-unsupported", "helper:helper-return-required", "internal-helper:helper-return-required"],
            null, null, null, null, null, null, []);
        var row = Only(Compare(Fact(unbound), Bindings()));
        Assert.Equal(U, row.Status);
        Assert.Contains("datatable:datacolumn-access-unsupported", row.Reasons);
        Assert.Contains("field-read-origin-unbound", row.Reasons);
        Assert.Contains("helper-caller-unavailable", Only(Compare(Fact(Internal()), Bindings())).Reasons);
        Assert.Contains("field-read-state-unavailable", Only(Compare(Fact(Direct(Field("Name")) with { Version = "sqlserver-datatable/1" }))).Reasons);
        // A bounded site needs a Field read, a bounded type argument and a consistent kind/member; paths follow the origin.
        foreach (var tampered in new[] { Direct(Indexer("Name")), Direct(Field("Name", type: "System.Nullable<System.Int32>")), Direct(Field("Name") with { Member = "Item" }),
            Direct(Indexer("Name") with { TypeArgument = "System.String" }), Direct(Field("Name") with { Gap = null, TypeArgument = null }),
            Direct(Field("Name")) with { Sites = [Site("dbo.ReadItems", "proj/Tables.cs", Field("Name"))] },
            Direct(Field("Name")) with { OriginGaps = ["datatable:x"] }, Direct(Field("Name")) with { ResultKind = "dataset" },
            Direct(Field("Name")) with { Gap = "contradictory-gap" },
            Direct(Field("Name")) with { Sites = [Site("dbo.ReadItems", null, Field("Name")) with { Gap = "contradictory-gap" }] } })
            Assert.Contains("invalid-field-read-state", Only(Compare(Fact(tampered))).Reasons);
        foreach (var path in new[] { "/abs/Callers.cs", "../Callers.cs", "proj/My Callers.cs", "proj\\Callers.cs" })
            Assert.Contains("invalid-field-read-state", Only(Compare(Fact(Internal(Site("dbo.ReadItems", path, Field("Name")))))).Reasons);
        foreach (var version in new[] { "csharp-semantic/0.22.19", "vb-semantic/0.10.22" })
        {
            var old = Only(Compare(Fact(Direct(Field("Name")), version)));
            Assert.Equal((U, version), (old.Status, old.SourceExtractorVersion));
            Assert.Contains("unsupported-source-extractor-or-tier", old.Reasons);
        }
    }

    [Fact]
    public void Derived_report_binds_exact_generator_and_field_projection_without_claiming_raw_fact_authentication()
    {
        var fact = Fact(Direct(Field("Name")));
        var report = Compare(fact);
        Assert.Equal(SqlServerSchemaEvidence.GeneratorSha256, report.GeneratorSha256);
        Assert.Matches("^[0-9a-f]{64}$", report.BoundedInputSha256);
        Assert.Equal(report.BoundedInputSha256, Compare(fact).BoundedInputSha256);
        var changed = Fact(Direct(Field("Name", type: "System.Int32")));
        Assert.NotEqual(SqlServerFieldReadEvidence.FieldReadDigest(fact, 0), SqlServerFieldReadEvidence.FieldReadDigest(changed, 0));
        Assert.NotEqual(report.BoundedInputSha256, Compare(changed).BoundedInputSha256);
        Assert.Contains(report.Limitations, l => l.Contains("integrity only, not authenticity", StringComparison.Ordinal));
    }

    [Fact]
    public void Field_read_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerFieldReadEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerFieldReadComparison", "limitations:", SqlServerFieldReadEvidence.StateVersion,
            SqlServerFieldReadEvidence.BindingsVersion, "SHA-256", "no SQL execution", "Fill or a helper executes", "siteIndex", "field-read-absent",
            "datarow-field-extension-unsupported", "DataColumn", "DataRowVersion", "SetField", "never compared", "b03f5f7f11d50a3a", "b77a5c561934e089" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
