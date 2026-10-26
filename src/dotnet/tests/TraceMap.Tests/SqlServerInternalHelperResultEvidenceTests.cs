using System.Text.RegularExpressions;
using TraceMap.Core;
using TraceMap.Reporting;
namespace TraceMap.Tests;
/// <summary>Phase 5 internal-helper comparison, per-call-site bindings with caller paths and substitution refusals. Expectations are authored independently of the extractor.</summary>
public sealed class SqlServerInternalHelperResultEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private static SqlServerResultColumn Column(string name, int ordinal) => new(name, ordinal, 1, null);
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultDeclarations Declarations()
        => new("sqlserver-result-declarations/1", "synthetic", Revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            [Shape(Items, Column("Id", 0), Column("Name", 1), Column("Total", 2)), Shape(Other, Column("Title", 0))]);
    private static SqlServerDataTableAccess Name(string name) => new("name-indexer", "Item", "rows-index", 0, name, null, 12, 12, null);
    private static SqlServerInternalHelperCaller Caller(string path, string procedure, params SqlServerDataTableAccess[] accesses)
        => new(path, 11, 11, "Callers.Use()", "bounded", null, FactFactory.Hash(procedure, 64), 11, [], [], accesses);
    private static SqlServerInternalHelperResultState State(params SqlServerInternalHelperCaller[] callers) => new("sqlserver-internal-helper-result/1", "bounded",
        null, "datatable", "StoredProcedure", "constructor-parameter", "constructor-command", "fill-datatable", null, 3, 5, 6, callers);
    private static readonly SqlServerHelperResultState Phase4 = new("sqlserver-helper-result/2", "unknown", "helper-accessibility-unsupported", null, null, null,
        null, null, null, null, null, null, []);
    private static CodeFact Fact(SqlServerInternalHelperResultState? state, string? version = null, SqlServerHelperResultState? helper = null)
    {
        var properties = new Dictionary<string, string>();
        if (state is not null) properties["sqlServerInternalHelperResultState"] = SqlServerSchemaEvidence.Json(state);
        properties["sqlServerHelperResultState"] = SqlServerSchemaEvidence.Json(helper ?? Phase4);
        return new("fact", "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern,
            EvidenceTiers.Tier1Semantic, "Data.ReadTable(string)", null, null,
            new("proj/Helpers.cs", 7, 7, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor), properties);
    }
    private static SqlServerInternalHelperResultBinding Binding(CodeFact fact, int caller, SqlServerObjectIdentity? identity = null, string? digest = null)
    {
        var i = identity ?? Items;
        return new(fact.FactId, caller, i.Schema + "." + i.Procedure, i, digest ?? SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(fact, caller));
    }
    private static SqlServerInternalHelperResultBindings Bindings(params SqlServerInternalHelperResultBinding[] calls)
        => new(SqlServerInternalHelperResultEvidence.BindingsVersion, "synthetic", Revision, calls);
    private static SqlServerInternalHelperResultComparisonReport Compare(CodeFact fact, SqlServerInternalHelperResultBindings? bindings = null)
    {
        var d = Declarations();
        return SqlServerInternalHelperResultEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), bindings ?? Bindings(Binding(fact, 0)), [fact], fact.Repo, fact.CommitSha);
    }
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }

    [Fact]
    public void Each_cross_document_caller_compares_against_its_own_shape_and_carries_its_path()
    {
        var fact = Fact(State(Caller("proj/Callers.cs", "dbo.ReadItems", Name("Name")), Caller("proj/More.cs", "dbo.ReadOther", Name("Title")),
            Caller("proj/More.cs", "dbo.ReadItems", Name("Title"))));
        var report = Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 1, Other), Binding(fact, 2)));
        Assert.Equal(("sqlserver-internal-helper-result-comparison/1", "sqlserver-internal-helper-result/1", "sqlserver-internal-helper-result-bindings/1", "partial", "no-aggregate-clean-verdict"),
            (report.Version, report.ConsumerVersion, report.BindingsVersion, report.Coverage, report.Verdict));
        Assert.Equal([M, M, X], report.Comparisons.Select(r => r.Status));
        Assert.Equal(["proj/Callers.cs", "proj/More.cs", "proj/More.cs"], report.Comparisons.Select(r => r.CallerPath));
        Assert.All(report.Comparisons, r => Assert.Equal((SqlServerInternalHelperResultEvidence.RuleId, "proj/Helpers.cs", "Tier1Semantic"), (r.RuleId, r.SourcePath, r.SourceTier)));
        // A binding naming another caller's procedure is a per-row conflict, never a substitution.
        var conflict = Compare(fact, Bindings(Binding(fact, 0, Other)));
        Assert.Contains("command-binding-conflict", conflict.Comparisons[0].Reasons);
        Assert.Equal(["binding-missing"], conflict.Comparisons[1].Reasons);
        var markdown = SqlServerInternalHelperResultReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown, StringComparison.Ordinal);
        Assert.Contains("call #1 at proj/More.cs:11–11 in Callers.Use()", markdown, StringComparison.Ordinal);
        Assert.Contains("not a sanitized shareable export", markdown, StringComparison.Ordinal);
    }

    [Fact]
    public void Phase_4_and_other_route_bindings_digests_and_paths_never_substitute()
    {
        var helper = new SqlServerHelperResultState("sqlserver-helper-result/2", "bounded", null, "datatable", "StoredProcedure", "constructor-parameter",
            "constructor-command", "fill-datatable", null, 3, 5, 6, [new(11, 11, "Callers.Use()", "bounded", null, FactFactory.Hash("dbo.ReadItems", 64), 11, [], [], [Name("Name")])]);
        var fact = Fact(State(Caller("proj/Callers.cs", "dbo.ReadItems", Name("Name")), Caller("proj/More.cs", "dbo.ReadOther", Name("Title"))), helper: helper);
        var first = SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(fact, 0);
        var phase4 = SqlServerHelperResultEvidence.HelperResultDigest(fact, 0);
        foreach (var other in new[] { phase4, SqlServerDataTableEvidence.DataTableDigest(fact), SqlServerDataSetEvidence.DataSetDigest(fact), SqlServerResultEvidence.ReaderDigest(fact), SqlServerSchemaEvidence.CallDigest(fact) })
        {
            Assert.NotEqual(other, first);
            Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0, digest: other))));
        }
        Assert.Equal([M, U], Compare(fact).Comparisons.Select(r => r.Status));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 1, Other, first))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0, digest: SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(fact, 1)))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0) with { CallerIndex = 2 })));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 0))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0)) with { Version = SqlServerHelperResultEvidence.BindingsVersion }));
        // The caller path is inside the digest: moving the caller to another document invalidates its binding.
        var moved = Fact(State(Caller("proj/Other.cs", "dbo.ReadItems", Name("Name")), Caller("proj/More.cs", "dbo.ReadOther", Name("Title"))), helper: helper);
        Assert.Throws<ArgumentException>(() => Compare(moved, Bindings(Binding(fact, 0))));
        // Phase 4 binding documents are not internal bindings, and internal bindings never enter the Phase 4 route.
        var phase4Bindings = new SqlServerHelperResultBindings(SqlServerHelperResultEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, 0, "dbo.ReadItems", Items, phase4)]);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultBindings>(SqlServerSchemaEvidence.Json(phase4Bindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerHelperResultBindings>(SqlServerSchemaEvidence.Json(Bindings(Binding(fact, 0)))));
        var d = Declarations();
        Assert.Throws<ArgumentException>(() => SqlServerHelperResultEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), phase4Bindings with { Version = SqlServerInternalHelperResultEvidence.BindingsVersion }, [fact], fact.Repo, fact.CommitSha));
    }

    [Fact]
    public void Phase_4_state_unknown_unsafe_paths_and_old_extractors_are_never_internal_evidence()
    {
        static SqlServerInternalHelperResultComparison Only(SqlServerInternalHelperResultComparisonReport report) => Assert.Single(report.Comparisons);
        Assert.Contains("internal-helper-result-state-unavailable", Only(Compare(Fact(null), Bindings())).Reasons);
        Assert.Contains("helper-private-phase4-scope", Only(Compare(Fact(State() with { Coverage = "unknown", Gap = "helper-private-phase4-scope" }), Bindings())).Reasons);
        Assert.Contains("helper-caller-unavailable", Only(Compare(Fact(State()), Bindings())).Reasons);
        Assert.Contains("internal-helper-result-state-unavailable", Only(Compare(Fact(State(Caller("proj/Callers.cs", "dbo.ReadItems", Name("Name"))) with { Version = "sqlserver-helper-result/2" }))).Reasons);
        foreach (var path in new[] { "/abs/Callers.cs", "../Callers.cs", "proj/My Callers.cs", "proj\\Callers.cs" })
            Assert.Contains("invalid-internal-helper-result-state", Only(Compare(Fact(State(Caller(path, "dbo.ReadItems", Name("Name")))))).Reasons);
        var old = Only(Compare(Fact(State(Caller("proj/Callers.cs", "dbo.ReadItems", Name("Name"))), version: "csharp-semantic/0.22.17")));
        Assert.Equal((U, "csharp-semantic/0.22.17"), (old.Status, old.SourceExtractorVersion));
        Assert.Contains("unsupported-source-extractor-or-tier", old.Reasons);
    }

    [Fact]
    public void Internal_helper_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerInternalHelperResultEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerInternalHelperResultComparison", "limitations:", SqlServerInternalHelperResultEvidence.StateVersion,
            SqlServerInternalHelperResultEvidence.BindingsVersion, "SHA-256", "no SQL execution", "Fill executes", "callerIndex", "caller path", "InternalsVisibleTo", "generated", "linked", "completeness" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
