using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;
namespace TraceMap.Tests;
/// <summary>Phase 3: SqlDataAdapter.Fill(DataSet) -> first-table DataRow consumer evidence. Expectations are authored independently of the extractor.</summary>
public sealed class SqlServerDataSetEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private static readonly string[] Origins = ["result-schema", "ddl-result", "ddl-table-result"];
    // Mirrors design.md and scripts/sqlserver-dataset-evidence/smoke.py (authored before running the extractor).
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreCases = new()
    {
        ["IndexZero"] = ([M], null), ["DefaultName"] = ([M], null), ["SourceTableName"] = ([M], null), ["SourceTableIndexZero"] = ([M], null),
        ["SelectCommandProperty"] = ([M], null), ["DiscardFill"] = ([M], null), ["TableLocal"] = ([M], null), ["RowLocalOrdinal"] = ([M], null),
        ["ForeachRows"] = ([M, M], null), ["TableLocalForeach"] = ([M], null), ["TablesCountGuard"] = ([M], null), ["RowsCountGuard"] = ([M], null),
        ["NestedGuards"] = ([M], null), ["IsNullName"] = ([M], null), ["IsNullOrdinal"] = ([M], null), ["BangName"] = ([M], null),
        ["UsingLifetime"] = ([M], null), ["WithParameter"] = ([M], null), ["TwoFills#0"] = ([M], null), ["TwoFills#1"] = ([M], null),
        ["MixedSelectors"] = ([M, M], null), ["ConvertedOrdinal"] = ([M], null), ["Reordered"] = ([M, M], "reordered"),
        ["MissingName"] = ([X], null), ["OrdinalOutOfRange"] = ([X], null), ["IsNullMissing"] = ([X], null), ["Renamed"] = ([X], "renamed"),
        ["CaseOnly"] = ([U], null), ["DuplicateDeclared"] = ([U], "duplicate"), ["DuplicateRenamed"] = ([U], "duplicate"), ["DynamicName"] = ([U], null),
        ["DynamicOrdinal"] = ([U], null), ["NonIdentifierName"] = ([U], null), ["Incomplete"] = ([U], "incomplete"), ["AbsentBinding"] = ([U], null)
    };
    // State cells: exact state gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> StateCases = new()
    {
        ["FillDataTable"] = "fill-overload-unsupported", ["FillRange"] = "fill-overload-unsupported", ["FillResultUsed"] = "fill-result-use-unsupported",
        ["DynamicSourceTable"] = "dynamic-source-table-unsupported", ["NonIdentifierSourceTable"] = "source-table-outside-identifier-vocabulary",
        ["EmptySourceTable"] = "invalid-source-table", ["SecondTableIndex"] = "dataset-later-table-unsupported", ["NegativeTableIndex"] = "invalid-table-index",
        ["DynamicTableIndex"] = "dynamic-table-selector-unsupported", ["DynamicTableName"] = "dynamic-table-selector-unsupported",
        ["LaterTableName"] = "dataset-table-name-unbound", ["CaseOnlyTableName"] = "dataset-table-name-case-unknown",
        ["SourceNameMismatch"] = "dataset-table-name-unbound", ["TableNamespace"] = "dataset-table-namespace-unsupported",
        ["TablesCountUse"] = "dataset-tables-count-unsupported", ["TablesCountWrongGuard"] = "unsupported-control-flow", ["TablesAdd"] = "dataset-mutation",
        ["TablesAlias"] = "dataset-tables-alias-or-escape", ["TablesMember"] = "unsupported-dataset-tables-member", ["TablesForeach"] = "unsupported-control-flow",
        ["MergeDataSet"] = "dataset-mutation", ["ReadXml"] = "dataset-mutation", ["Relations"] = "dataset-relations-unsupported",
        ["EnforceConstraints"] = "dataset-mutation", ["CaseSensitiveDataSet"] = "dataset-mutation", ["CopyDataSet"] = "ambiguous-row-ownership",
        ["DataSetMember"] = "unsupported-dataset-member", ["NamedDataSet"] = "dataset-constructor-unsupported",
        ["DataSetInitializer"] = "dataset-initializer-unsupported", ["DataSetReassigned"] = "dataset-reassignment", ["DataSetAlias"] = "dataset-alias-or-escape",
        ["DataSetEscape"] = "dataset-alias-or-escape", ["RefDataSet"] = "dataset-alias-or-escape", ["UseBeforeFill"] = "dataset-use-before-fill",
        ["SharedDataSet"] = "fresh-dataset-required", ["TypedDataSet"] = "fresh-dataset-local-required", ["HelperFill"] = "fresh-sqldataadapter-local-required",
        ["RepeatedFill#0"] = "repeated-fill", ["RepeatedFill#1"] = "repeated-fill", ["AdapterReused#0"] = "adapter-reused", ["AdapterReused#1"] = "adapter-reused",
        ["FillSchemaFirst"] = "fill-schema-unsupported", ["TableMappings"] = "adapter-table-mappings-unsupported",
        ["MissingSchemaAction"] = "adapter-missing-schema-action-unsupported", ["MissingMappingAction"] = "adapter-missing-mapping-action-unsupported",
        ["CommandTypeAfterAdapter"] = "command-mutation-after-adapter-association", ["TextCommand"] = "bounded:text-command-outside-scope",
        ["TableLocalReassigned"] = "datatable-reassignment", ["TableLocalEscape"] = "datatable-alias-or-escape",
        ["TableColumns"] = "datatable-schema-access-unsupported", ["TableSelect"] = "ambiguous-row-ownership", ["LinqRows"] = "ambiguous-row-ownership",
        ["TableClear"] = "datatable-mutation", ["RowsAdd"] = "datatable-mutation", ["RowWrite"] = "datatable-mutation",
        ["FieldExtension"] = "datarow-field-extension-unsupported", ["DynamicRowIndex"] = "dynamic-row-index-unsupported", ["RowAlias"] = "row-alias-or-escape",
        ["ForeachObjectRow"] = "foreach-row-type-unsupported", ["GuardElse"] = "unsupported-control-flow", ["TablesAndRowsGuard"] = "unsupported-control-flow",
        ["OtherDataSetGuard"] = "unsupported-control-flow", ["TryCatch"] = "unsupported-control-flow", ["NoReads"] = "bounded:no-bounded-column-access"
    };
    private static SqlServerResultColumn Column(string name, int ordinal, string? type = null) => new(name, ordinal, 1, type);
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultShape ItemsShape => Shape(Items, Column("Id", 0, "int"), Column("Name", 1), Column("Total", 2, "decimal(10,2)"));
    private static SqlServerResultDeclarations Declarations(string repo = "synthetic", string revision = Revision, params SqlServerResultShape[] shapes)
        => new("sqlserver-result-declarations/1", repo, revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            shapes.Length == 0 ? [ItemsShape, Shape(Other, Column("Title", 0))] : shapes);
    private static SqlServerDataSetState State(params SqlServerDataTableAccess[] accesses) => new("sqlserver-dataset/1", "bounded", null, "StoredProcedure",
        FactFactory.Hash("dbo.ReadItems", 64), "constructor-command", "fill-dataset", "Table", 3, 4, ["tables-index"], [], accesses);
    private static SqlServerDataTableAccess Name(string? name, string member = "Item", string? gap = null)
        => new(member == "IsNull" ? "isnull-name" : "name-indexer", member, "rows-index", 0, name, null, 6, 6, gap);
    private static SqlServerDataTableAccess Ordinal(int? ordinal) => new("ordinal-indexer", "Item", "row-local", 0, null, ordinal, 6, 6, null);
    private static CodeFact Fact(SqlServerDataSetState? state, string id = "fact", string? version = null, SqlServerDataTableState? table = null)
    {
        var properties = new Dictionary<string, string>();
        if (state is not null) properties["sqlServerDataSetState"] = SqlServerSchemaEvidence.Json(state);
        if (table is not null) properties["sqlServerDataTableState"] = SqlServerSchemaEvidence.Json(table);
        return new(id, "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern,
            EvidenceTiers.Tier1Semantic, "Example.Run()", null, null,
            new("Calls.cs", 5, 5, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor), properties);
    }
    private static SqlServerDataSetBindings Bindings(CodeFact fact, SqlServerObjectIdentity? identity = null, string? digest = null)
    {
        var i = identity ?? Items;
        return new(SqlServerDataSetEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, i.Schema + "." + i.Procedure, i, digest ?? SqlServerDataSetEvidence.DataSetDigest(fact))]);
    }
    private static SqlServerDataSetComparisonReport Compare(CodeFact fact, SqlServerResultDeclarations? declarations = null, SqlServerDataSetBindings? bindings = null)
    {
        var d = declarations ?? Declarations();
        return SqlServerDataSetEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), bindings ?? Bindings(fact), [fact], fact.Repo, fact.CommitSha);
    }
    private static string[] Statuses(SqlServerDataSetComparisonReport report, string factId = "fact")
        => report.Comparisons.Where(r => r.FactId == factId).Select(r => r.Status).ToArray();
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }

    // ---------------------------------------------------------------- Comparison semantics
    [Theory]
    [InlineData("match", M)]
    [InlineData("missing", X)]
    [InlineData("case-only", U)]
    [InlineData("duplicate-renamed", U)]
    [InlineData("ordinal-out-of-range", X)]
    [InlineData("dynamic", U)]
    [InlineData("incomplete", U)]
    [InlineData("isnull", M)]
    public void Column_identity_and_ordinal_bounds_reuse_the_datarow_decision(string mode, string expected)
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
        var report = Compare(Fact(State(access)), Declarations(shapes: [shape]));
        var row = Assert.Single(report.Comparisons);
        Assert.Equal(expected, row.Status);
        Assert.Equal(expected == U ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, row.Tier);
        Assert.Equal(EvidenceTiers.Tier1Semantic, row.SourceTier);
        Assert.Equal(SqlServerDataSetEvidence.RuleId, row.RuleId);
        Assert.Equal(("partial", "no-aggregate-clean-verdict"), (report.Coverage, report.Verdict));
        Assert.Equal(("sqlserver-dataset-comparison/1", "sqlserver-dataset/1", "sqlserver-dataset-bindings/1", "result-schema"),
            (report.Version, report.ConsumerVersion, report.BindingsVersion, report.SchemaRoute));
        if (mode == "missing") Assert.Equal(["missing-column:Title"], row.Reasons);
        if (mode == "case-only") Assert.Equal(["column-case-policy-unknown:name"], row.Reasons);
        if (mode == "duplicate-renamed") Assert.Equal(["datatable-duplicate-column-renaming-unknown:Name1"], row.Reasons);
        if (mode == "ordinal-out-of-range") Assert.Equal(["ordinal-out-of-range:3"], row.Reasons);
        if (mode == "isnull") Assert.Equal(["nullability-not-compared:IsNull", "declared-type-not-compared:decimal(10,2)"], row.UncomparedFacets);
        var markdown = SqlServerDataSetReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown);
        Assert.Contains("first DataSet table", markdown);
        foreach (var reason in row.Reasons) Assert.Contains(reason, markdown);
    }

    [Fact]
    public void DataTable_reader_and_invalid_states_are_never_dataset_evidence()
    {
        // A bounded DataTable state on the same fact is never read as DataSet evidence.
        var table = new SqlServerDataTableState("sqlserver-datatable/1", "bounded", null, "StoredProcedure", FactFactory.Hash("dbo.ReadItems", 64),
            "constructor-command", 3, 4, "none", [Name("Name")]);
        Assert.Contains("dataset-state-unavailable", Assert.Single(Compare(Fact(null, table: table)).Comparisons).Reasons);
        Assert.Contains("fill-overload-unsupported", Assert.Single(Compare(Fact(State() with { Coverage = "unknown", Gap = "fill-overload-unsupported" }, table: table)).Comparisons).Reasons);
        Assert.Contains("text-command-outside-scope", Assert.Single(Compare(Fact(State(Name("Name")) with { CommandType = "Text" })).Comparisons).Reasons);
        Assert.Contains("no-bounded-column-access", Assert.Single(Compare(Fact(State())).Comparisons).Reasons);
        Assert.Contains("command-binding-conflict", Assert.Single(Compare(Fact(State(Name("Name")) with { CommandTextHash = FactFactory.Hash("dbo.ReadOther", 64) })).Comparisons).Reasons);
        foreach (var invalid in new[] { State(Name("Name")) with { Selectors = ["tables-1"] }, State(Name("Name")) with { Guards = ["rows-count-and-foreach"] },
            State(Name("Name")) with { FillOverload = "fill-datatable" }, State(Name("Name")) with { TableName = "Unit Price" },
            State(Name("Name") with { RowSource = "select" }) })
            Assert.Contains("invalid-dataset-state", Assert.Single(Compare(Fact(invalid)).Comparisons).Reasons);
        Assert.Contains("dataset-state-unavailable", Assert.Single(Compare(Fact(State(Name("Name")) with { Version = "sqlserver-datatable/1" })).Comparisons).Reasons);
        var old = Assert.Single(Compare(Fact(State(Name("Name")), version: "csharp-semantic/0.22.14")).Comparisons);
        Assert.Equal((U, "csharp-semantic/0.22.14", EvidenceTiers.Tier1Semantic), (old.Status, old.SourceExtractorVersion, old.SourceTier));
        Assert.Contains("unsupported-source-extractor-or-tier", old.Reasons);
        Assert.Contains("binding-missing", Assert.Single(Compare(Fact(State(Name("Name"))), bindings: new(SqlServerDataSetEvidence.BindingsVersion, "synthetic", Revision, [])).Comparisons).Reasons);
        Assert.Contains("exact-result-shape-unavailable", Assert.Single(Compare(Fact(State(Name("Name"))), bindings: Bindings(Fact(State(Name("Name"))), Items with { Database = "OtherDatabase" })).Comparisons).Reasons);
    }

    [Fact]
    public void Bindings_refuse_datatable_reader_parameter_stale_duplicate_and_wrong_contract_substitution()
    {
        var table = new SqlServerDataTableState("sqlserver-datatable/1", "unknown", "fill-overload-unsupported", null, null, null, null, null, "none", []);
        var fact = Fact(State(Name("Name")), table: table);
        var digest = SqlServerDataSetEvidence.DataSetDigest(fact);
        Assert.NotEqual(SqlServerDataTableEvidence.DataTableDigest(fact), digest);
        Assert.NotEqual(SqlServerResultEvidence.ReaderDigest(fact), digest);
        Assert.NotEqual(SqlServerSchemaEvidence.CallDigest(fact), digest);
        Assert.Equal([M], Statuses(Compare(fact)));
        foreach (var substituted in new[] { SqlServerDataTableEvidence.DataTableDigest(fact), SqlServerResultEvidence.ReaderDigest(fact), SqlServerSchemaEvidence.CallDigest(fact), new string('0', 64) })
            Assert.Throws<ArgumentException>(() => Compare(fact, bindings: Bindings(fact, digest: substituted)));
        var good = Bindings(fact);
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Version = SqlServerDataTableEvidence.BindingsVersion }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Calls = [good.Calls[0], good.Calls[0]] }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Revision = new string('2', 40) }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Calls = [good.Calls[0] with { CommandText = "dbo.ReadOther" }] }));
        Assert.Throws<ArgumentException>(() => Compare(Fact(State(Name("Title")), table: table), bindings: good));
        // DataTable bindings JSON cannot be read as DataSet bindings and vice versa.
        var tableBindings = new SqlServerDataTableBindings(SqlServerDataTableEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, "dbo.ReadItems", Items, SqlServerDataTableEvidence.DataTableDigest(fact))]);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataSetBindings>(SqlServerSchemaEvidence.Json(tableBindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataTableBindings>(SqlServerSchemaEvidence.Json(good)));
        Assert.Equal(SqlServerSchemaEvidence.Json(Compare(fact)), SqlServerSchemaEvidence.Json(Compare(fact)));
    }

    // ---------------------------------------------------------------- Source extractor (direct Roslyn)
    private static IEnumerable<Microsoft.CodeAnalysis.MetadataReference> References()
    {
        using var assets = JsonDocument.Parse(File.ReadAllText(Path.Combine(RepoRoot(), "samples/sqlserver-parameter-evidence/csharp/obj/project.assets.json")));
        var package = assets.RootElement.GetProperty("libraries").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("path").GetString()!;
        var compile = assets.RootElement.GetProperty("targets").GetProperty("net10.0").GetProperty("System.Data.SqlClient/4.9.0").GetProperty("compile")
            .EnumerateObject().Single(p => p.Name.EndsWith(".dll", StringComparison.Ordinal)).Name;
        var provider = assets.RootElement.GetProperty("packageFolders").EnumerateObject().Select(p => Path.Combine(p.Name, package, compile)).First(File.Exists);
        return ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Append(provider).Distinct(StringComparer.Ordinal).Select(p => Microsoft.CodeAnalysis.MetadataReference.CreateFromFile(p)).ToArray();
    }
    private static (SqlServerDataSetState DataSet, SqlServerDataTableState DataTable)[] Extract(string language, string body)
    {
        Microsoft.CodeAnalysis.Compilation compilation;
        Microsoft.CodeAnalysis.SyntaxTree tree;
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("#pragma warning disable CS0618, CS0219, CS0168\nusing System; using System.Data; using System.Data.SqlClient; class Example { static DataSet Field; static void Helper(object o) {} static void OnMerge(object s, MergeFailedEventArgs e) {} static object Run(string name, int index, bool flag) {\n" + body + "\n} }");
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Imports System\nImports System.Data\nImports System.Data.SqlClient\nModule Example\nSub Helper(o As Object)\nEnd Sub\nFunction Run(name As String, index As Integer, flag As Boolean) As Object\n" + body + "\nEnd Function\nEnd Module");
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>()
            .Where(o => o.TargetMethod.Name == "Fill").Distinct().OrderBy(o => o.Syntax.SpanStart)
            .Select(o =>
            {
                var p = new SortedDictionary<string, string>();
                SqlServerDataTableConsumers.Add(p, o);
                SqlServerDataSetConsumers.Add(p, o);
                return (SqlServerSchemaEvidence.Read<SqlServerDataSetState>(p["sqlServerDataSetState"]), SqlServerSchemaEvidence.Read<SqlServerDataTableState>(p["sqlServerDataTableState"]));
            }).ToArray();
    }
    private const string CsPrefix = "var command = new SqlCommand(\"dbo.ReadItems\"); command.CommandType = CommandType.StoredProcedure;\n";
    private const string CsFill = "var adapter = new SqlDataAdapter(command); var results = new DataSet(); adapter.Fill(results);\n";
    private const string VbPrefix = "Dim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\n";
    private const string VbFill = "Dim adapter = New SqlDataAdapter(command)\nDim results = New DataSet()\nadapter.Fill(results)\n";

    [Theory]
    [InlineData("cs", "using var adapter = new SqlDataAdapter(command); using var results = new DataSet(); adapter.Fill(results); return results.Tables[0].Rows[0][\"Name\"];", "fill-dataset", "Table", "tables-index", "")]
    [InlineData("cs", "var adapter = new SqlDataAdapter(); adapter.SelectCommand = command; var results = new DataSet(); adapter.Fill(results, \"Items\"); adapter.Dispose(); results.Dispose(); return results.Tables[\"Items\"].Rows[0][\"Name\"];", "fill-dataset-source-table", "Items", "tables-name", "")]
    [InlineData("cs", "var adapter = new SqlDataAdapter(command); var results = new DataSet(); adapter.Fill(srcTable: \"Items\", dataSet: results); var table = results.Tables[0]; return table.Rows[0].IsNull(1);", "fill-dataset-source-table", "Items", "table-local,tables-index", "")]
    [InlineData("cs", "var adapter = new SqlDataAdapter(command); var results = new DataSet(); adapter.Fill(results); if (results.Tables.Count > 0) { if (results.Tables[\"Table\"].Rows.Count > 0) { foreach (DataRow row in results.Tables[0].Rows) { Helper(row[\"Name\"]); } } } return null;", "fill-dataset", "Table", "tables-index,tables-name", "foreach,rows-count,tables-count")]
    [InlineData("vb", "Using adapter As New SqlDataAdapter(command), results As New DataSet()\nadapter.Fill(results)\nReturn results.Tables!Table.Rows(0)!Name\nEnd Using", "fill-dataset", "Table", "tables-name", "")]
    [InlineData("vb", "Dim adapter = New SqlDataAdapter()\nadapter.SelectCommand = command\nDim results = New DataSet()\nCall adapter.Fill(results, \"Items\")\nDim table = results.Tables(\"Items\")\nIf results.Tables.Count > 0 Then\nFor Each row As DataRow In table.Rows\nHelper(row(\"Name\"))\nNext\nEnd If\nReturn Nothing", "fill-dataset-source-table", "Items", "table-local,tables-name", "foreach,tables-count")]
    public void Supported_cells_are_bounded_with_exact_overload_selector_and_guards(string language, string body, string overload, string tableName, string selectors, string guards)
    {
        var (state, table) = Assert.Single(Extract(language, (language == "cs" ? CsPrefix : VbPrefix) + body));
        Assert.Equal(("bounded", (string?)null, "StoredProcedure", overload, tableName), (state.Coverage, state.Gap, state.CommandType, state.FillOverload, state.TableName));
        Assert.Equal(selectors, string.Join(",", state.Selectors));
        Assert.Equal(guards, string.Join(",", state.Guards));
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), state.CommandTextHash);
        Assert.NotEmpty(state.Accesses);
        Assert.All(state.Accesses, a => Assert.True(a.Gap is null && (a.Name is not null || a.Ordinal is not null)));
        // The sibling DataTable state keeps refusing every DataSet overload.
        Assert.Equal(("unknown", "fill-overload-unsupported"), (table.Coverage, table.Gap));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Fill_datatable_keeps_datatable_state_and_is_never_dataset_state(string language)
    {
        var body = language == "cs"
            ? CsPrefix + "var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.Rows[0][\"Name\"];"
            : VbPrefix + "Dim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\nReturn table.Rows(0)(\"Name\")";
        var (state, table) = Assert.Single(Extract(language, body));
        Assert.Equal(("unknown", "fill-overload-unsupported"), (state.Coverage, state.Gap));
        Assert.Equal(("bounded", (string?)null), (table.Coverage, table.Gap));
        Assert.Empty(state.Accesses);
    }

    [Theory]
    [InlineData("results.Tables[0].Dispose(); return null;", "unsupported-datatable-member")]
    [InlineData("object table = results.Tables[\"Table\"]; return table;", "datatable-alias-or-escape")]
    [InlineData("results.Tables.Remove(\"Table\"); return null;", "dataset-mutation")]
    [InlineData("results.MergeFailed += OnMerge; return null;", "unsupported-dataset-member")]
    [InlineData("var view = results.DefaultViewManager; return null;", "unsupported-dataset-member")]
    [InlineData("var changes = results.GetChanges(); return null;", "ambiguous-row-ownership")]
    [InlineData("results.AcceptChanges(); return null;", "dataset-mutation")]
    [InlineData("var otherCommand = new SqlCommand(\"dbo.ReadItems\"); var other = new SqlDataAdapter(otherCommand); other.Fill(results.Tables[0]); return null;", "repeated-fill")]
    [InlineData("var otherCommand = new SqlCommand(\"dbo.ReadItems\"); var other = new SqlDataAdapter(otherCommand); other.Fill(results); return null;", "repeated-fill")]
    [InlineData("Helper(results.Tables); return null;", "dataset-tables-alias-or-escape")]
    [InlineData("return results.Tables[0]?.Rows[0][\"Name\"];", "unsupported-control-flow")]
    [InlineData("return results.Tables[(int)1.2].Rows[0][\"Name\"];", "dataset-later-table-unsupported")]
    [InlineData("var table = results.Tables[0]; return table.Rows[1][\"Name\"] ?? \"x\";", "unsupported-control-flow")]
    [InlineData("var tables = results.Tables[0].Rows; return tables[0][\"Name\"];", "rows-alias-or-escape")]
    public void DataSet_table_and_flow_edge_cells_fail_closed_with_exact_gaps(string tail, string gap)
    {
        var (state, _) = Extract("cs", CsPrefix + CsFill + tail)[0];
        Assert.Equal(("unknown", gap), (state.Coverage, state.Gap));
        Assert.Empty(state.Accesses);
        Assert.Equal([U], Statuses(Compare(Fact(state))));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Typed_field_and_null_source_datasets_and_bounds_are_explicit(string language)
    {
        var typed = language == "cs"
            ? CsPrefix + "var adapter = new SqlDataAdapter(command); DataSet results = new DataSet(); var typed = Field; adapter.Fill(typed); return null;"
            : VbPrefix + "Dim adapter = New SqlDataAdapter(command)\nDim typed As DataSet = Nothing\nadapter.Fill(typed)\nReturn Nothing";
        Assert.Equal("fresh-dataset-required", Extract(language, typed)[0].DataSet.Gap);
        var nullSource = language == "cs" ? CsPrefix + "var adapter = new SqlDataAdapter(command); var results = new DataSet(); adapter.Fill(results, (string)null); return null;"
            : VbPrefix + "Dim adapter = New SqlDataAdapter(command)\nDim results = New DataSet()\nadapter.Fill(results, CStr(Nothing))\nReturn Nothing";
        Assert.Equal("invalid-source-table", Extract(language, nullSource)[0].DataSet.Gap);
        var many = language == "cs" ? string.Concat(Enumerable.Range(0, 257).Select(i => $"var v{i} = results.Tables[0].Rows[0][{i % 3}];\n")) + "return null;"
            : string.Concat(Enumerable.Range(0, 257).Select(i => $"Dim v{i} = results.Tables(0).Rows(0)({i % 3})\n")) + "Return Nothing";
        Assert.Equal("access-limit", Extract(language, (language == "cs" ? CsPrefix + CsFill : VbPrefix + VbFill) + many)[0].DataSet.Gap);
        var padding = language == "cs" ? string.Concat(Enumerable.Range(0, 6000).Select(i => $"int unused{i} = 1;\n")) + "return null;"
            : string.Concat(Enumerable.Range(0, 6000).Select(i => $"Dim unused{i} = 1\n")) + "Return Nothing";
        Assert.Equal("operation-limit", Extract(language, (language == "cs" ? CsPrefix + CsFill : VbPrefix + VbFill) + padding)[0].DataSet.Gap);
    }

    // ---------------------------------------------------------------- Native fixture matrix and CLI
    private static (string Json, string Schema, IReadOnlyDictionary<string, string> Sources) Origin(string origin, string? variant, string repo, string revision)
    {
        var shapes = Path.Combine(RepoRoot(), "samples/sqlserver-result-column-evidence");
        if (origin == "result-schema")
        {
            var items = variant switch
            {
                "renamed" => ItemsShape with { Columns = [Column("Id", 0, "int"), Column("DisplayName", 1), Column("Total", 2, "decimal(10,2)")] },
                "reordered" => ItemsShape with { Columns = [Column("Total", 0, "decimal(10,2)"), Column("Name", 1), Column("Id", 2, "int")] },
                "duplicate" => ItemsShape with { Columns = [Column("Id", 0, "int"), Column("Name", 1), Column("Name", 2)] },
                "incomplete" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
                _ => ItemsShape
            };
            var declarations = SqlServerSchemaEvidence.Json(Declarations(repo, revision, items, Shape(Other, Column("Title", 0))));
            return (declarations, SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(declarations)), new Dictionary<string, string>());
        }
        var folder = Path.Combine(shapes, origin == "ddl-result" ? "ddl" : "ddl-table");
        var template = variant switch { "renamed" => "renamed.sql", "reordered" => "reordered.sql", "duplicate" => "duplicate.sql", "incomplete" => "if.sql", _ => "base.sql" };
        var files = origin == "ddl-result" ? new[] { template } : [template, "tables.sql"];
        var sources = files.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(folder, f)), StringComparer.Ordinal);
        var input = SqlServerSchemaEvidence.Json(new SqlServerDdlInput(origin == "ddl-result" ? SqlServerDdlResultEvidence.InputVersion : SqlServerDdlTableResultEvidence.InputVersion,
            repo, revision, Items.Server, Items.Database, true, [.. files.Select(f => new SqlServerDdlFile(f, SqlServerSchemaEvidence.Hash(File.ReadAllBytes(Path.Combine(folder, f)))))]));
        var export = origin == "ddl-result" ? SqlServerSchemaEvidence.Json(SqlServerDdlResultEvidence.Produce(input, sources))
            : SqlServerSchemaEvidence.Json(SqlServerDdlTableResultEvidence.Produce(input, sources));
        return (input, export, sources);
    }
    private static SqlServerDataSetComparisonReport CompareOrigin(string origin, string? variant, SqlServerDataSetBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var (json, schema, sources) = Origin(origin, variant, repo, revision);
        return origin switch
        {
            "result-schema" => SqlServerDataSetEvidence.CompareResultSchema(schema, json, bindings, facts, repo, revision),
            "ddl-result" => SqlServerDataSetEvidence.CompareDdlResult(schema, json, sources, bindings, facts, repo, revision),
            _ => SqlServerDataSetEvidence.CompareDdlTableResult(schema, json, sources, bindings, facts, repo, revision)
        };
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_fixture_matrix_binds_exact_fills_and_matches_independent_expectations(string language)
    {
        var sample = Path.Combine(RepoRoot(), "samples/sqlserver-dataset-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "DataSets.vbproj" : "DataSets.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var fills = calls.Where(f => f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill").OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(99, fills.Length);
        // Both sibling consumer properties live on Fill facts only.
        Assert.All(calls, f => Assert.Equal(f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill", f.Properties.ContainsKey("sqlServerDataSetState")));
        Assert.All(fills, f => Assert.True(f.Properties.ContainsKey("sqlServerDataTableState") && !f.Properties.ContainsKey("sqlServerReaderState")));
        var seen = fills.Select(f => Regex.Match(f.SourceSymbol!, @"\.([A-Za-z]+)\(").Groups[1].Value).GroupBy(n => n).ToDictionary(g => g.Key, g => g.Count());
        var keyed = new Dictionary<string, CodeFact>(StringComparer.Ordinal);
        var index = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var fact in fills)
        {
            var name = Regex.Match(fact.SourceSymbol!, @"\.([A-Za-z]+)\(").Groups[1].Value;
            var n = index.GetValueOrDefault(name);
            index[name] = n + 1;
            keyed.Add(seen[name] > 1 ? $"{name}#{n}" : name, fact);
        }
        Assert.Equal(CoreCases.Keys.Concat(StateCases.Keys).Order(StringComparer.Ordinal), keyed.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, fact) in keyed)
        {
            var state = SqlServerSchemaEvidence.Read<SqlServerDataSetState>(fact.Properties["sqlServerDataSetState"]);
            var expected = StateCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(state.Coverage == "bounded" && state.Gap is null, $"{language}:{key}:{state.Gap}");
            else
                Assert.True(state.Coverage == "unknown" && state.Gap == expected && state.Accesses.Count == 0, $"{language}:{key}:{state.Gap}≠{expected}");
            // Cross-property: only the Fill(DataTable) site has bounded DataTable state; DataSet overloads stay refused there.
            var table = SqlServerSchemaEvidence.Read<SqlServerDataTableState>(fact.Properties["sqlServerDataTableState"]);
            Assert.True(key == "FillDataTable" ? table.Coverage == "bounded" : table.Gap == "fill-overload-unsupported", $"{language}:{key}:datatable:{table.Gap}");
        }
        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        var plan = CoreCases.SelectMany(c => Origins.Select(o => (c.Key, Origin: o, c.Value.Variant, c.Value.Statuses)))
            .Concat(StateCases.Keys.Select(k => (Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U }))).ToArray();
        Assert.Equal(169, plan.Length);
        foreach (var (key, origin, variant, expected) in plan)
        {
            var fact = keyed[key];
            var identity = key switch { "TwoFills#1" => Other, "AbsentBinding" => Items with { Database = "OtherDatabase" }, _ => Items };
            var report = CompareOrigin(origin, variant, Bindings(fact, identity), calls, fact.Repo, fact.CommitSha);
            var rows = report.Comparisons.Where(r => r.FactId == fact.FactId).ToArray();
            Assert.True(rows.Select(r => r.Status).SequenceEqual(expected), $"{language}:{key}:{origin}:" + SqlServerSchemaEvidence.Json(rows));
            if (StateCases.TryGetValue(key, out var gap)) Assert.Contains(gap.StartsWith("bounded:", StringComparison.Ordinal) ? gap[8..] : gap, Assert.Single(rows).Reasons);
            foreach (var row in rows) counts[row.Status]++;
            Assert.All(report.Comparisons.Where(r => r.FactId != fact.FactId), r => Assert.True(r.Status == U && r.Reasons.Contains("binding-missing")));
            Assert.Equal(origin, report.SchemaRoute);
        }
        Assert.Equal((78, 12, 88), (counts[M], counts[X], counts[U]));

        // Native CLI for each origin: byte-identical repeat, no overwrite, wrong-origin and DataTable-binding refusals.
        var good = keyed["ForeachRows"];
        var writer = new StringWriter(); var error = new StringWriter();
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var bindingsPath = Path.Combine(temp.Path, "bindings.json"); File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(Bindings(good)));
        foreach (var origin in Origins)
        {
            var (json, schema, sources) = Origin(origin, null, good.Repo, good.CommitSha);
            var dir = Path.Combine(temp.Path, origin);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "schema.json"), schema);
            File.WriteAllText(Path.Combine(dir, "origin.json"), json);
            foreach (var (file, text) in sources) File.WriteAllText(Path.Combine(dir, file), text);
            string[] originOptions = origin == "result-schema" ? ["--declarations", Path.Combine(dir, "origin.json")] : ["--input", Path.Combine(dir, "origin.json"), "--source-root", dir];
            var command = new[] { "sqlserver-dataset-result", "compare", "--origin", origin }.Concat(originOptions).Concat(new[] { "--schema", Path.Combine(dir, "schema.json"),
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(dir, "report") }).ToArray();
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(dir, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "dataset-column-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "report", file)), File.ReadAllBytes(Path.Combine(dir, "repeat", file)));
            var readback = SqlServerSchemaEvidence.Read<SqlServerDataSetComparisonReport>(File.ReadAllText(Path.Combine(dir, "report", "dataset-column-comparison.json")));
            Assert.Equal([M, M], Statuses(readback, good.FactId));
            Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<JsonElement>(schema)), readback.SchemaEvidenceSha256);
            var markdown = File.ReadAllText(Path.Combine(dir, "report", "report.md"));
            Assert.Contains("binding-missing", markdown);
            Assert.Contains("fill-overload-unsupported", markdown);
            Assert.Contains("repeated-fill", markdown);
            var wrong = (string[])command.Clone();
            wrong[3] = origin == "ddl-result" ? "ddl-table-result" : origin == "ddl-table-result" ? "ddl-result" : "ddl-table-result";
            wrong[^1] = Path.Combine(dir, "wrong-origin");
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(wrong, writer, error));
            Assert.False(Directory.Exists(wrong[^1]));
        }
        // DataTable bindings are refused by the DataSet route, and DataSet bindings by the DataTable route.
        var tableBindings = Path.Combine(temp.Path, "datatable-bindings.json");
        File.WriteAllText(tableBindings, SqlServerSchemaEvidence.Json(new SqlServerDataTableBindings(SqlServerDataTableEvidence.BindingsVersion, good.Repo, good.CommitSha,
            [new(good.FactId, "dbo.ReadItems", Items, SqlServerDataTableEvidence.DataTableDigest(good))])));
        var jsonDir = Path.Combine(temp.Path, "result-schema");
        string[] Route(string route, string bindings, string output) => [route, "compare", "--origin", "result-schema",
            "--declarations", Path.Combine(jsonDir, "origin.json"), "--schema", Path.Combine(jsonDir, "schema.json"), "--bindings", bindings, "--facts", factsPath,
            "--repo", good.Repo, "--revision", good.CommitSha, "--out", output];
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-dataset-result", tableBindings, Path.Combine(temp.Path, "x1")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-datatable-result", bindingsPath, Path.Combine(temp.Path, "x2")), writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "x1")) || Directory.Exists(Path.Combine(temp.Path, "x2")));
    }

    [Fact]
    public void DataSet_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerDataSetEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerDataSetComparison", "limitations:", SqlServerDataSetEvidence.StateVersion,
            SqlServerDataSetEvidence.BindingsVersion, "SHA-256", "no SQL execution", "Fill executes", "TableMappings", "case-insensitive", "Tables[1]" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
