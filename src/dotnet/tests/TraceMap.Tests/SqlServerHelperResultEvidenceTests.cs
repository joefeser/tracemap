using System.Text.RegularExpressions;
using TraceMap.Core;
using TraceMap.Reporting;
namespace TraceMap.Tests;
/// <summary>Phase 4 helper-result comparison, per-call-site bindings and substitution refusals. Expectations are authored independently of the extractor.</summary>
public sealed partial class SqlServerHelperResultEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private static SqlServerResultColumn Column(string name, int ordinal, string? type = null) => new(name, ordinal, 1, type);
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultShape ItemsShape => Shape(Items, Column("Id", 0, "int"), Column("Name", 1), Column("Total", 2, "decimal(10,2)"));
    private static SqlServerResultDeclarations Declarations(string repo = "synthetic", string revision = Revision, params SqlServerResultShape[] shapes)
        => new("sqlserver-result-declarations/1", repo, revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            shapes.Length == 0 ? [ItemsShape, Shape(Other, Column("Title", 0))] : shapes);
    private static SqlServerDataTableAccess Name(string? name, string member = "Item", string? gap = null)
        => new(member == "IsNull" ? "isnull-name" : "name-indexer", member, "rows-index", 0, name, null, 12, 12, gap);
    private static SqlServerDataTableAccess Ordinal(int? ordinal) => new("ordinal-indexer", "Item", "row-local", 0, null, ordinal, 12, 12, null);
    private static SqlServerHelperCaller Caller(string procedure, params SqlServerDataTableAccess[] accesses)
        => new(11, 11, "Example.Use()", "bounded", null, FactFactory.Hash(procedure, 64), 11, [], [], accesses);
    private static SqlServerHelperResultState State(params SqlServerHelperCaller[] callers) => new("sqlserver-helper-result/1", "bounded", null, "datatable",
        "StoredProcedure", "constructor-parameter", "constructor-command", "fill-datatable", null, 3, 5, 6, callers);
    private static CodeFact Fact(SqlServerHelperResultState? state, string id = "fact", string? version = null, SqlServerDataTableState? table = null, SqlServerDataSetState? set = null)
    {
        var properties = new Dictionary<string, string>();
        if (state is not null) properties["sqlServerHelperResultState"] = SqlServerSchemaEvidence.Json(state);
        if (table is not null) properties["sqlServerDataTableState"] = SqlServerSchemaEvidence.Json(table);
        if (set is not null) properties["sqlServerDataSetState"] = SqlServerSchemaEvidence.Json(set);
        return new(id, "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern,
            EvidenceTiers.Tier1Semantic, "Example.ReadTable(string)", null, null,
            new("Calls.cs", 7, 7, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor), properties);
    }
    private static SqlServerHelperResultBinding Binding(CodeFact fact, int caller, SqlServerObjectIdentity? identity = null, string? digest = null)
    {
        var i = identity ?? Items;
        return new(fact.FactId, caller, i.Schema + "." + i.Procedure, i, digest ?? SqlServerHelperResultEvidence.HelperResultDigest(fact, caller));
    }
    private static SqlServerHelperResultBindings Bindings(params SqlServerHelperResultBinding[] calls)
        => new(SqlServerHelperResultEvidence.BindingsVersion, "synthetic", Revision, calls);
    private static SqlServerHelperResultComparisonReport Compare(CodeFact fact, SqlServerHelperResultBindings? bindings = null, SqlServerResultDeclarations? declarations = null)
    {
        var d = declarations ?? Declarations();
        return SqlServerHelperResultEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), bindings ?? Bindings(Binding(fact, 0)), [fact], fact.Repo, fact.CommitSha);
    }
    private static string[] Statuses(SqlServerHelperResultComparisonReport report, int? caller = null)
        => report.Comparisons.Where(r => caller is null || r.CallerIndex == caller).Select(r => r.Status).ToArray();
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }

    [Theory]
    [InlineData("match", M)]
    [InlineData("missing", X)]
    [InlineData("case-only", U)]
    [InlineData("duplicate-renamed", U)]
    [InlineData("ordinal-out-of-range", X)]
    [InlineData("dynamic", U)]
    [InlineData("incomplete", U)]
    [InlineData("isnull", M)]
    public void Column_identity_and_ordinal_bounds_reuse_the_datarow_decision_per_caller(string mode, string expected)
    {
        var access = mode switch
        {
            "missing" => Name("Title"), "case-only" => Name("name"), "duplicate-renamed" => Name("Name1"), "ordinal-out-of-range" => Ordinal(3),
            "dynamic" => Name(null, gap: "dynamic-column-name"), "isnull" => Name("Total", "IsNull"), _ => Name("Name")
        };
        var shape = mode switch
        {
            "duplicate-renamed" => Shape(Items, Column("Id", 0), Column("Name", 1), Column("Name", 2)),
            "incomplete" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
            _ => ItemsShape
        };
        var report = Compare(Fact(State(Caller("dbo.ReadItems", access))), declarations: Declarations(shapes: [shape]));
        var row = Assert.Single(report.Comparisons);
        Assert.Equal((expected, (int?)0, (int?)0), (row.Status, row.CallerIndex, row.AccessIndex));
        Assert.Equal(expected == U ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, row.Tier);
        Assert.Equal((EvidenceTiers.Tier1Semantic, SqlServerHelperResultEvidence.RuleId, (int?)11, (string?)"Example.Use()"), (row.SourceTier, row.RuleId, row.CallerStartLine, row.Caller));
        Assert.Equal(("partial", "no-aggregate-clean-verdict"), (report.Coverage, report.Verdict));
        Assert.Equal(("sqlserver-helper-result-comparison/1", "sqlserver-helper-result/1", "sqlserver-helper-result-bindings/1", "result-schema"),
            (report.Version, report.ConsumerVersion, report.BindingsVersion, report.SchemaRoute));
        if (mode == "missing") Assert.Equal(["missing-column:Title"], row.Reasons);
        if (mode == "case-only") Assert.Equal(["column-case-policy-unknown:name"], row.Reasons);
        if (mode == "duplicate-renamed") Assert.Equal(["datatable-duplicate-column-renaming-unknown:Name1"], row.Reasons);
        if (mode == "isnull") Assert.Equal(["nullability-not-compared:IsNull", "declared-type-not-compared:decimal(10,2)"], row.UncomparedFacets);
        var markdown = SqlServerHelperResultReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown);
        Assert.Contains("each call site binds separately", markdown);
        Assert.Contains("call #0 at line 11", markdown);
        Assert.Contains("not a sanitized shareable export", markdown);
        Assert.DoesNotContain("public bounded input", markdown);
        Assert.Contains(report.Limitations, l => l.Contains("do not publish private-input reports", StringComparison.Ordinal));
        foreach (var reason in row.Reasons) Assert.Contains(reason, markdown);
    }

    [Fact]
    public void Public_shapes_do_not_sanitize_source_metadata_in_local_reports()
    {
        var caller = Caller("dbo.ReadItems", Name("Name")) with { Caller = "ConfidentialExample.Work()" };
        var fact = Fact(State(caller));
        fact = fact with { Evidence = fact.Evidence with { FilePath = "ConfidentialExample/Work.cs" } };
        var report = Compare(fact);
        var json = SqlServerSchemaEvidence.Json(report);
        var markdown = SqlServerHelperResultReportWriter.Build(report);
        Assert.Contains("ConfidentialExample/Work.cs", json);
        Assert.Contains("ConfidentialExample.Work()", markdown);
        Assert.Contains("not a sanitized shareable export", json);
        Assert.Contains("Do not publish private-input reports", markdown);
    }

    [Fact]
    public void Multiple_callers_with_different_procedures_compare_against_their_own_shapes()
    {
        // Caller 0 reads Name from ReadItems; caller 1 reads Title from ReadOther and Name (absent there) from ReadOther.
        var fact = Fact(State(Caller("dbo.ReadItems", Name("Name")), Caller("dbo.ReadOther", Name("Title"), Name("Name"))));
        var report = Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 1, Other)));
        Assert.Equal([M], Statuses(report, 0));
        Assert.Equal([M, X], Statuses(report, 1));
        Assert.Equal(["missing-column:Name"], report.Comparisons.Single(r => r.CallerIndex == 1 && r.AccessIndex == 1).Reasons);
        Assert.Equal(Other, report.Comparisons.Single(r => r.CallerIndex == 1 && r.AccessIndex == 0).Shape!.Identity);
        // Binding each caller to the other's procedure is a per-caller command conflict, never a borrowed match.
        var swapped = Compare(fact, Bindings(Binding(fact, 0, Other), Binding(fact, 1)));
        Assert.All(swapped.Comparisons, r => Assert.True(r.Status == U && r.Reasons.Contains("command-binding-conflict")));
        // An unbound caller stays unknown while the bound one still compares.
        var partial = Compare(fact, Bindings(Binding(fact, 1, Other)));
        Assert.Equal([U], Statuses(partial, 0));
        Assert.Contains("binding-missing", partial.Comparisons.Single(r => r.CallerIndex == 0).Reasons);
        Assert.Equal([M, X], Statuses(partial, 1));
        Assert.NotEqual(SqlServerHelperResultEvidence.HelperResultDigest(fact, 0), SqlServerHelperResultEvidence.HelperResultDigest(fact, 1));
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 1, Other)))));
    }

    [Fact]
    public void Binding_substitution_across_callers_routes_and_states_is_refused()
    {
        var table = new SqlServerDataTableState("sqlserver-datatable/1", "unknown", "datatable-alias-or-escape", null, null, null, null, null, "none", []);
        var set = new SqlServerDataSetState("sqlserver-dataset/1", "unknown", "fill-overload-unsupported", null, null, null, null, null, null, null, [], [], []);
        var fact = Fact(State(Caller("dbo.ReadItems", Name("Name")), Caller("dbo.ReadOther", Name("Title"))), table: table, set: set);
        var first = SqlServerHelperResultEvidence.HelperResultDigest(fact, 0);
        foreach (var other in new[] { SqlServerDataTableEvidence.DataTableDigest(fact), SqlServerDataSetEvidence.DataSetDigest(fact), SqlServerResultEvidence.ReaderDigest(fact), SqlServerSchemaEvidence.CallDigest(fact) })
            Assert.NotEqual(other, first);
        Assert.Equal([M, U], Statuses(Compare(fact)));
        foreach (var substituted in new[] { SqlServerDataTableEvidence.DataTableDigest(fact), SqlServerDataSetEvidence.DataSetDigest(fact), SqlServerResultEvidence.ReaderDigest(fact),
            SqlServerSchemaEvidence.CallDigest(fact), SqlServerHelperResultEvidence.HelperResultDigest(fact, 1), new string('0', 64) })
            Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0, digest: substituted))));
        // Retargeting caller 0's digest to index 1, an out-of-range or negative index, duplicates and stale envelopes are refused.
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 1, Other, first))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 2, digest: first) with { CallerIndex = 2 })));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0) with { CallerIndex = -1 })));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0), Binding(fact, 0))));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0)) with { Revision = new string('2', 40) }));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0)) with { Version = SqlServerDataSetEvidence.BindingsVersion }));
        Assert.Throws<ArgumentException>(() => Compare(fact, Bindings(Binding(fact, 0) with { CommandText = "dbo.ReadOther" })));
        // A mutated caller (procedure hash or access) no longer matches its digest.
        var mutated = Fact(State(Caller("dbo.ReadOther", Name("Name")), Caller("dbo.ReadOther", Name("Title"))), table: table, set: set);
        Assert.Throws<ArgumentException>(() => Compare(mutated, Bindings(Binding(fact, 0))));
        // Other routes' binding documents are never read as helper bindings and vice versa.
        var good = Bindings(Binding(fact, 0));
        var tableBindings = new SqlServerDataTableBindings(SqlServerDataTableEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, "dbo.ReadItems", Items, SqlServerDataTableEvidence.DataTableDigest(fact))]);
        var setBindings = new SqlServerDataSetBindings(SqlServerDataSetEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, "dbo.ReadItems", Items, SqlServerDataSetEvidence.DataSetDigest(fact))]);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerHelperResultBindings>(SqlServerSchemaEvidence.Json(tableBindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerHelperResultBindings>(SqlServerSchemaEvidence.Json(setBindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataSetBindings>(SqlServerSchemaEvidence.Json(good)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataTableBindings>(SqlServerSchemaEvidence.Json(good)));
    }

    [Fact]
    public void Unavailable_unknown_text_invalid_and_uncalled_states_are_never_helper_evidence()
    {
        static SqlServerHelperResultComparison Only(SqlServerHelperResultComparisonReport report) => Assert.Single(report.Comparisons);
        var bounded = Fact(State(Caller("dbo.ReadItems", Name("Name"))));
        var table = new SqlServerDataTableState("sqlserver-datatable/1", "bounded", null, "StoredProcedure", FactFactory.Hash("dbo.ReadItems", 64), "constructor-command", 3, 4, "none", [Name("Name")]);
        Assert.Contains("helper-result-state-unavailable", Only(Compare(Fact(null, table: table), Bindings())).Reasons);
        Assert.Contains("helper-return-required", Only(Compare(Fact(State() with { Coverage = "unknown", Gap = "helper-return-required" }), Bindings())).Reasons);
        Assert.Contains("text-command-outside-scope", Only(Compare(Fact(State(Caller("dbo.ReadItems", Name("Name"))) with { CommandType = "Text" }))).Reasons);
        Assert.Contains("helper-caller-unavailable", Only(Compare(Fact(State()), Bindings())).Reasons);
        Assert.Contains("no-bounded-column-access", Only(Compare(Fact(State(Caller("dbo.ReadItems"))))).Reasons);
        Assert.Contains("helper-result-local-required", Only(Compare(Fact(State(Caller("dbo.ReadItems") with { Coverage = "unknown", Gap = "helper-result-local-required", CommandTextHash = null })))).Reasons);
        foreach (var invalid in new[] { State(Caller("dbo.ReadItems", Name("Name"))) with { ResultKind = "reader" }, State(Caller("dbo.ReadItems", Name("Name"))) with { TableName = "Table" },
            State(Caller("dbo.ReadItems", Name("Name"))) with { FillOverload = "fill-dataset" }, State(Caller("dbo.ReadItems", Name("Name"))) with { CommandTextSource = "constant" } })
            Assert.Contains("invalid-helper-result-state", Only(Compare(Fact(invalid))).Reasons);
        foreach (var caller in new[] { Caller("dbo.ReadItems", Name("Name")) with { Guards = ["tables-count"] }, Caller("dbo.ReadItems", Name("Name")) with { Selectors = ["tables-index"] },
            Caller("dbo.ReadItems", Name("Name") with { RowSource = "select" }), Caller("dbo.ReadItems", Name("Name")) with { Line = 0 } })
            Assert.Contains("invalid-helper-result-state", Only(Compare(Fact(State(caller)))).Reasons);
        Assert.Contains("helper-result-state-unavailable", Only(Compare(Fact(State(Caller("dbo.ReadItems", Name("Name"))) with { Version = "sqlserver-dataset/1" }))).Reasons);
        var old = Only(Compare(Fact(State(Caller("dbo.ReadItems", Name("Name"))), version: "csharp-semantic/0.22.15")));
        Assert.Equal((U, "csharp-semantic/0.22.15"), (old.Status, old.SourceExtractorVersion));
        Assert.Contains("unsupported-source-extractor-or-tier", old.Reasons);
        Assert.Contains("exact-result-shape-unavailable", Only(Compare(bounded, Bindings(Binding(bounded, 0, Items with { Database = "OtherDatabase" })))).Reasons);
    }

    [Fact]
    public void Helper_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerHelperResultEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerHelperResultComparison", "limitations:", SqlServerHelperResultEvidence.StateVersion,
            SqlServerHelperResultEvidence.BindingsVersion, "SHA-256", "no SQL execution", "Fill executes", "callerIndex", "private", "Recursion", "other documents or compilations" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
