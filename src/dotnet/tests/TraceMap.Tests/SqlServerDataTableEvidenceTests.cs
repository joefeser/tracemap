using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;
namespace TraceMap.Tests;
/// <summary>Phase 2: SqlDataAdapter.Fill -> DataTable/DataRow consumer evidence. Expectations are authored independently of the extractor.</summary>
public sealed class SqlServerDataTableEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private static readonly string[] Origins = ["result-schema", "ddl-result", "ddl-table-result"];
    // Mirrors design.md and scripts/sqlserver-datatable-evidence/smoke.py (authored before running the extractor).
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreCases = new()
    {
        ["CtorName"] = ([M], null), ["SelectCommandProperty"] = ([M], null), ["DiscardFill"] = ([M], null), ["RowLocalOrdinal"] = ([M], null),
        ["ForeachRows"] = ([M, M], null), ["CountGuard"] = ([M], null), ["CountGuardForeach"] = ([M], null), ["IsNullName"] = ([M], null),
        ["IsNullOrdinal"] = ([M], null), ["BangName"] = ([M], null), ["UsingLifetime"] = ([M], null), ["WithParameter"] = ([M], null),
        ["TwoFills#0"] = ([M], null), ["TwoFills#1"] = ([M], null), ["ConvertedOrdinal"] = ([M], null),
        ["MissingName"] = ([X], null), ["OrdinalOutOfRange"] = ([X], null), ["IsNullMissing"] = ([X], null), ["Renamed"] = ([X], "renamed"),
        ["Reordered"] = ([M, M], "reordered"), ["CaseOnly"] = ([U], null), ["DuplicateDeclared"] = ([U], "duplicate"),
        ["DuplicateRenamed"] = ([U], "duplicate"), ["DynamicName"] = ([U], null), ["DynamicOrdinal"] = ([U], null),
        ["NonIdentifierName"] = ([U], null), ["Incomplete"] = ([U], "incomplete"), ["AbsentBinding"] = ([U], null)
    };
    // State cells: exact state gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> StateCases = new()
    {
        ["TextCommand"] = "bounded:text-command-outside-scope", ["NoCommandType"] = "bounded:stored-procedure-intent-unavailable",
        ["CommandTypeAfterAdapter"] = "command-mutation-after-adapter-association", ["ParameterAfterAdapter"] = "command-mutation-after-adapter-association",
        ["CommandAlias"] = "command-alias-or-escape", ["CommandExecuted"] = "command-executed-outside-fill",
        ["ConditionalCommandText"] = "unsupported-control-flow", ["TextAdapterConstructor"] = "adapter-constructor-unsupported",
        ["AdapterInitializer"] = "adapter-initializer-unsupported", ["SelectCommandTwice"] = "multiple-select-command-writes",
        ["SelectCommandUnbound"] = "select-command-unbound", ["SelectCommandAfterFill"] = "select-command-after-fill",
        ["SelectCommandViaAdapter"] = "unsupported-adapter-member", ["FillDataSet"] = "fill-overload-unsupported",
        ["FillDataSetTable"] = "fill-overload-unsupported", ["FillRange"] = "fill-overload-unsupported",
        ["FillResultUsed"] = "fill-result-use-unsupported", ["RepeatedFill#0"] = "repeated-fill", ["RepeatedFill#1"] = "repeated-fill",
        ["AdapterReused#0"] = "adapter-reused", ["AdapterReused#1"] = "adapter-reused", ["FillSchemaFirst"] = "fill-schema-unsupported",
        ["TableMappings"] = "adapter-table-mappings-unsupported", ["MissingSchemaAction"] = "adapter-missing-schema-action-unsupported",
        ["MissingMappingAction"] = "adapter-missing-mapping-action-unsupported", ["AcceptChangesDuringFill"] = "adapter-accept-changes-during-fill-unsupported",
        ["ContinueUpdateOnError"] = "adapter-continue-update-on-error-unsupported", ["AdapterAlias"] = "adapter-alias-or-escape",
        ["AdapterReassignment"] = "adapter-reassignment", ["NamedTable"] = "datatable-constructor-unsupported",
        ["TableReassigned"] = "datatable-reassignment", ["TableAlias"] = "datatable-alias-or-escape", ["TableEscape"] = "datatable-alias-or-escape",
        ["RefEscape"] = "datatable-alias-or-escape", ["ColumnsAdd"] = "datatable-schema-access-unsupported", ["CaseSensitive"] = "datatable-mutation",
        ["TableNameWrite"] = "datatable-mutation", ["MergeTable"] = "datatable-mutation", ["ClearTable"] = "datatable-mutation",
        ["RowsAdd"] = "datatable-mutation", ["RowWrite"] = "datatable-mutation", ["OtherTableRows"] = "bounded:no-bounded-column-access",
        ["SelectRows"] = "ambiguous-row-ownership", ["DefaultView"] = "ambiguous-row-ownership", ["CopyTable"] = "ambiguous-row-ownership",
        ["LinqRows"] = "ambiguous-row-ownership", ["FieldExtension"] = "datarow-field-extension-unsupported",
        ["DataColumnIndexer"] = "datacolumn-access-unsupported", ["RowVersion"] = "row-version-access-unsupported",
        ["DynamicRowIndex"] = "dynamic-row-index-unsupported", ["RowReassignment"] = "row-reassignment", ["RowAlias"] = "row-alias-or-escape",
        ["RowMember"] = "unsupported-row-member", ["UnrelatedTableGuard"] = "unsupported-control-flow", ["ReaderGuard"] = "unsupported-control-flow",
        ["AdapterGuard"] = "fill-result-use-unsupported", ["GuardBeforeFill"] = "table-use-before-fill",
        ["GuardWrongComparison"] = "unsupported-control-flow", ["GuardElse"] = "unsupported-control-flow", ["SwitchFlow"] = "unsupported-control-flow",
        ["TryCatch"] = "unsupported-control-flow", ["LambdaFlow"] = "unsupported-control-flow",
        ["HelperFill"] = "fresh-sqldataadapter-local-required", ["ForeachObjectRow"] = "foreach-row-type-unsupported"
    };
    private static SqlServerResultColumn Column(string name, int ordinal, string? type = null) => new(name, ordinal, 1, type);
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultShape ItemsShape => Shape(Items, Column("Id", 0, "int"), Column("Name", 1), Column("Total", 2, "decimal(10,2)"));
    private static SqlServerResultDeclarations Declarations(string repo = "synthetic", string revision = Revision, params SqlServerResultShape[] shapes)
        => new("sqlserver-result-declarations/1", repo, revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            shapes.Length == 0 ? [ItemsShape, Shape(Other, Column("Title", 0))] : shapes);
    private static SqlServerDataTableState State(params SqlServerDataTableAccess[] accesses) => new("sqlserver-datatable/1", "bounded", null, "StoredProcedure",
        FactFactory.Hash("dbo.ReadItems", 64), "constructor-command", 3, 4, "none", accesses);
    private static SqlServerDataTableAccess Name(string? name, string member = "Item", string? gap = null)
        => new(member == "IsNull" ? "isnull-name" : "name-indexer", member, "rows-index", 0, name, null, 6, 6, gap);
    private static SqlServerDataTableAccess Ordinal(int? ordinal, string? gap = null) => new("ordinal-indexer", "Item", "row-local", 0, null, ordinal, 6, 6, gap);
    private static CodeFact Fact(SqlServerDataTableState? state, string id = "fact", string? version = null, SqlServerReaderState? reader = null)
    {
        var properties = new Dictionary<string, string>();
        if (state is not null) properties["sqlServerDataTableState"] = SqlServerSchemaEvidence.Json(state);
        if (reader is not null) properties["sqlServerReaderState"] = SqlServerSchemaEvidence.Json(reader);
        return new(id, "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern,
            EvidenceTiers.Tier1Semantic, "Example.Run()", null, null,
            new("Calls.cs", 5, 5, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor), properties);
    }
    private static SqlServerDataTableBindings Bindings(CodeFact fact, SqlServerObjectIdentity? identity = null, string? digest = null)
    {
        var i = identity ?? Items;
        return new(SqlServerDataTableEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
            [new(fact.FactId, i.Schema + "." + i.Procedure, i, digest ?? SqlServerDataTableEvidence.DataTableDigest(fact))]);
    }
    private static SqlServerDataTableComparisonReport Compare(CodeFact fact, SqlServerResultDeclarations? declarations = null, SqlServerDataTableBindings? bindings = null,
        IReadOnlyList<CodeFact>? facts = null)
    {
        var d = declarations ?? Declarations();
        return SqlServerDataTableEvidence.CompareResultSchema(SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d))),
            SqlServerSchemaEvidence.Json(d), bindings ?? Bindings(fact), facts ?? [fact], fact.Repo, fact.CommitSha);
    }
    private static string[] Statuses(SqlServerDataTableComparisonReport report, string factId = "fact")
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
    [InlineData("duplicate-name", U)]
    [InlineData("duplicate-renamed", U)]
    [InlineData("case-only-duplicate-renamed", U)]
    [InlineData("duplicate-ordinal", M)]
    [InlineData("ordinal-out-of-range", X)]
    [InlineData("ordinal-negative", X)]
    [InlineData("dynamic", U)]
    [InlineData("incomplete", U)]
    [InlineData("isnull", M)]
    public void Column_identity_ordinal_bounds_and_datatable_decisions(string mode, string expected)
    {
        var access = mode switch
        {
            "missing" => Name("Title"), "case-only" => Name("name"), "duplicate-renamed" or "case-only-duplicate-renamed" => Name("Name1"),
            "duplicate-ordinal" => Ordinal(2), "ordinal-out-of-range" => Ordinal(3), "ordinal-negative" => Ordinal(-1),
            "dynamic" => Name(null, gap: "dynamic-column-name"), "isnull" => Name("Total", "IsNull"), _ => Name("Name")
        };
        var shape = mode switch
        {
            "duplicate-name" or "duplicate-renamed" or "duplicate-ordinal" => Shape(Items, Column("Id", 0), Column("Name", 1), Column("Name", 2)),
            "case-only-duplicate-renamed" => Shape(Items, Column("Id", 0), Column("Name", 1), Column("name", 2)),
            "incomplete" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
            _ => ItemsShape
        };
        var report = Compare(Fact(State(access)), Declarations(shapes: [shape]));
        var row = Assert.Single(report.Comparisons);
        Assert.Equal(expected, row.Status);
        Assert.Equal(expected == U ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, row.Tier);
        Assert.Equal(EvidenceTiers.Tier1Semantic, row.SourceTier);
        Assert.Equal(SqlServerDataTableEvidence.RuleId, row.RuleId);
        Assert.Equal(("partial", "no-aggregate-clean-verdict"), (report.Coverage, report.Verdict));
        Assert.Equal(("sqlserver-datatable-comparison/1", "sqlserver-datatable/1", "sqlserver-datatable-bindings/1", "result-schema"),
            (report.Version, report.ConsumerVersion, report.BindingsVersion, report.SchemaRoute));
        if (mode == "missing") Assert.Equal(["missing-column:Title"], row.Reasons);
        // DataTable lookup would fall back case-insensitively at runtime; that is deliberately not claimed.
        if (mode == "case-only") Assert.Equal(["column-case-policy-unknown:name"], row.Reasons);
        // Fill renames duplicate result names (Name, Name1): an absent name proves nothing against a duplicate shape.
        if (mode is "duplicate-renamed" or "case-only-duplicate-renamed") Assert.Equal(["datatable-duplicate-column-renaming-unknown:Name1"], row.Reasons);
        if (mode == "ordinal-out-of-range") Assert.Equal(["ordinal-out-of-range:3"], row.Reasons);
        if (mode == "incomplete") Assert.Equal(["result-shape-unknown:authored-incomplete"], row.Reasons);
        if (mode == "isnull")
        {
            Assert.Contains("nullability-not-compared:IsNull", row.UncomparedFacets);
            Assert.Contains("declared-type-not-compared:decimal(10,2)", row.UncomparedFacets);
        }
        var markdown = SqlServerDataTableReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown);
        foreach (var reason in row.Reasons) Assert.Contains(reason, markdown);
    }

    [Fact]
    public void Reader_and_other_channels_are_never_datatable_evidence_and_state_problems_stay_unknown()
    {
        var reader = new SqlServerReaderState("sqlserver-reader/1", "bounded", null, "StoredProcedure", FactFactory.Hash("dbo.ReadItems", 64), 4, 4, "none",
            [new("name-indexer", "Item", "Name", null, null, 5, 5, null)]);
        var readerFact = Fact(null, reader: reader);
        var row = Assert.Single(Compare(readerFact).Comparisons);
        Assert.Equal(U, row.Status);
        Assert.Contains("datatable-state-unavailable", row.Reasons);
        Assert.Contains("datatable-state-unavailable", Assert.Single(Compare(Fact(null)).Comparisons).Reasons);
        Assert.Contains("repeated-fill", Assert.Single(Compare(Fact(State() with { Coverage = "unknown", Gap = "repeated-fill" })).Comparisons).Reasons);
        Assert.Contains("text-command-outside-scope", Assert.Single(Compare(Fact(State(Name("Name")) with { CommandType = "Text" })).Comparisons).Reasons);
        Assert.Contains("no-bounded-column-access", Assert.Single(Compare(Fact(State())).Comparisons).Reasons);
        Assert.Contains("command-binding-conflict", Assert.Single(Compare(Fact(State(Name("Name")) with { CommandTextHash = FactFactory.Hash("dbo.ReadOther", 64) })).Comparisons).Reasons);
        Assert.Contains("invalid-datatable-state", Assert.Single(Compare(Fact(State(Name("Name") with { RowSource = "select" }))).Comparisons).Reasons);
        Assert.Contains("invalid-datatable-state", Assert.Single(Compare(Fact(State() with { Version = "sqlserver-datatable/1", Accesses = [Name("Name") with { Kind = "get-ordinal" }] })).Comparisons).Reasons);
        Assert.Contains("datatable-state-unavailable", Assert.Single(Compare(Fact(State(Name("Name")) with { Version = "sqlserver-reader/1" })).Comparisons).Reasons);
        // Source tier and extractor stay visible; an older extractor is never upgraded by structural correlation.
        var old = Fact(State(Name("Name")), version: "csharp-semantic/0.22.13");
        var oldRow = Assert.Single(Compare(old).Comparisons);
        Assert.Equal((U, "csharp-semantic/0.22.13", EvidenceTiers.Tier1Semantic), (oldRow.Status, oldRow.SourceExtractorVersion, oldRow.SourceTier));
        Assert.Contains("unsupported-source-extractor-or-tier", oldRow.Reasons);
        Assert.Contains("binding-missing", Assert.Single(Compare(Fact(State(Name("Name"))), bindings: new(SqlServerDataTableEvidence.BindingsVersion, "synthetic", Revision, [])).Comparisons).Reasons);
        Assert.Contains("exact-result-shape-unavailable", Assert.Single(Compare(Fact(State(Name("Name"))), bindings: Bindings(Fact(State(Name("Name"))), Items with { Database = "OtherDatabase" })).Comparisons).Reasons);
    }

    [Fact]
    public void Bindings_refuse_reader_parameter_stale_duplicate_and_wrong_contract_substitution()
    {
        var reader = new SqlServerReaderState("sqlserver-reader/1", "bounded", null, "StoredProcedure", FactFactory.Hash("dbo.ReadItems", 64), 4, 4, "none", []);
        var fact = Fact(State(Name("Name")), reader: reader);
        var digest = SqlServerDataTableEvidence.DataTableDigest(fact);
        // Distinct consumer identity: never equal to the reader or parameter digests of the same fact.
        Assert.NotEqual(SqlServerResultEvidence.ReaderDigest(fact), digest);
        Assert.NotEqual(SqlServerSchemaEvidence.CallDigest(fact), digest);
        Assert.Equal([M], Statuses(Compare(fact)));
        foreach (var substituted in new[] { SqlServerResultEvidence.ReaderDigest(fact), SqlServerSchemaEvidence.CallDigest(fact), new string('0', 64) })
            Assert.Throws<ArgumentException>(() => Compare(fact, bindings: Bindings(fact, digest: substituted)));
        var good = Bindings(fact);
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Version = "sqlserver-result-bindings/1" }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Calls = [good.Calls[0], good.Calls[0]] }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Revision = new string('2', 40) }));
        Assert.Throws<ArgumentException>(() => Compare(fact, bindings: good with { Calls = [good.Calls[0] with { CommandText = "dbo.ReadOther" }] }));
        // A digest computed before the state changed no longer binds the mutated fact.
        var mutated = Fact(State(Name("Title")), reader: reader);
        Assert.Throws<ArgumentException>(() => Compare(mutated, bindings: good));
        // Reader bindings JSON cannot be read as DataTable bindings and vice versa.
        var readerBindings = new SqlServerResultBindings("sqlserver-result-bindings/1", fact.Repo, fact.CommitSha,
            [new(fact.FactId, "dbo.ReadItems", Items, SqlServerResultEvidence.ReaderDigest(fact))]);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerDataTableBindings>(SqlServerSchemaEvidence.Json(readerBindings)));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerResultBindings>(SqlServerSchemaEvidence.Json(good)));
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
    private static SqlServerDataTableState[] Extract(string language, string body)
    {
        Microsoft.CodeAnalysis.Compilation compilation;
        Microsoft.CodeAnalysis.SyntaxTree tree;
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("#pragma warning disable CS0618, CS0219, CS0168\nusing System; using System.Data; using System.Data.SqlClient; class Example { static DataTable Field; static SqlCommand SharedCommand; static void Helper(object o) {} static void OnError(object s, FillErrorEventArgs e) {} static object Run(string name, int index, bool flag) {\n" + body + "\n} }");
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Imports System\nImports System.Data\nImports System.Data.SqlClient\nModule Example\nDim Field As DataTable\nSub Helper(o As Object)\nEnd Sub\nFunction Run(name As String, index As Integer, flag As Boolean) As Object\n" + body + "\nEnd Function\nEnd Module");
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>()
            .Where(o => o.TargetMethod.Name == "Fill").Distinct().OrderBy(o => o.Syntax.SpanStart)
            .Select(o => { var p = new SortedDictionary<string, string>(); SqlServerDataTableConsumers.Add(p, o); return SqlServerSchemaEvidence.Read<SqlServerDataTableState>(p["sqlServerDataTableState"]); }).ToArray();
    }
    private const string CsPrefix = "var command = new SqlCommand(\"dbo.ReadItems\"); command.CommandType = CommandType.StoredProcedure;\n";
    private const string CsFill = "var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table);\n";
    private const string VbPrefix = "Dim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\n";
    private const string VbFill = "Dim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nadapter.Fill(table)\n";

    [Theory]
    [InlineData("cs", "using var adapter = new SqlDataAdapter(command); using var table = new DataTable(); adapter.Fill(table); return table.Rows[0][\"Name\"];", "none", "constructor-command")]
    [InlineData("cs", "var adapter = new SqlDataAdapter(); var table = new DataTable(); adapter.SelectCommand = command; adapter.Fill(table); adapter.Dispose(); table.Dispose(); return table.Rows[0][\"Name\"];", "none", "select-command-property")]
    [InlineData("cs", "var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); foreach (DataRow row in table.Rows) { Helper(row[\"Name\"]); } if (table.Rows.Count > 0) { return table.Rows[0].IsNull(1); } return null;", "rows-count-and-foreach", "constructor-command")]
    [InlineData("vb", "Using adapter As New SqlDataAdapter(command), table As New DataTable()\nadapter.Fill(table)\nReturn table.Rows(0)!Name\nEnd Using", "none", "constructor-command")]
    [InlineData("vb", "Dim adapter = New SqlDataAdapter()\nadapter.SelectCommand = command\nDim table = New DataTable()\nadapter.Fill(table)\nIf table.Rows.Count > 0 Then\nFor Each row As DataRow In table.Rows\nHelper(row(\"Name\"))\nNext\nEnd If\nReturn Nothing", "rows-count-and-foreach", "select-command-property")]
    public void Supported_cells_are_bounded_with_exact_association_guard_and_constant_reads(string language, string body, string guard, string association)
    {
        var state = Assert.Single(Extract(language, (language == "cs" ? CsPrefix : VbPrefix) + body));
        Assert.Equal(("bounded", (string?)null, "StoredProcedure", guard, association), (state.Coverage, state.Gap, state.CommandType, state.Guard, state.AdapterAssociation));
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), state.CommandTextHash);
        Assert.NotEmpty(state.Accesses);
        Assert.All(state.Accesses, a => Assert.True(a.Gap is null && (a.Name is not null || a.Ordinal is not null)));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Guards_bind_to_the_selected_table_not_an_unrelated_table_reader_or_adapter(string language)
    {
        var guards = language == "cs"
            ? new[] { "var other = new DataTable(); if (other.Rows.Count > 0) { return table.Rows[0][\"Name\"]; }",
                "var gateCommand = new SqlCommand(\"dbo.ReadOther\"); var gate = gateCommand.ExecuteReader(); if (gate.Read()) { return table.Rows[0][\"Name\"]; }",
                "var other = new DataTable(); foreach (DataRow row in other.Rows) { Helper(table.Rows[0][\"Name\"]); }",
                "if (table.Rows.Count > 1) { return table.Rows[0][\"Name\"]; }", "if (table.Rows.Count != 0) { return table.Rows[0][\"Name\"]; }",
                "if (flag) { return table.Rows[0][\"Name\"]; }", "while (table.Rows.Count > 0) { return table.Rows[0][\"Name\"]; }" }
            : new[] { "Dim other = New DataTable()\nIf other.Rows.Count > 0 Then\nReturn table.Rows(0)(\"Name\")\nEnd If",
                "Dim gateCommand = New SqlCommand(\"dbo.ReadOther\")\nDim gate = gateCommand.ExecuteReader()\nIf gate.Read() Then\nReturn table.Rows(0)(\"Name\")\nEnd If",
                "Dim other = New DataTable()\nFor Each row As DataRow In other.Rows\nHelper(table.Rows(0)(\"Name\"))\nNext",
                "If table.Rows.Count > 1 Then\nReturn table.Rows(0)(\"Name\")\nEnd If", "If table.Rows.Count <> 0 Then\nReturn table.Rows(0)(\"Name\")\nEnd If",
                "If flag Then\nReturn table.Rows(0)(\"Name\")\nEnd If", "While table.Rows.Count > 0\nReturn table.Rows(0)(\"Name\")\nEnd While" };
        foreach (var guard in guards)
        {
            var body = language == "cs" ? CsPrefix + CsFill + guard + "\nreturn null;" : VbPrefix + VbFill + guard + "\nReturn Nothing";
            var state = Extract(language, body)[0];
            Assert.True(state.Coverage == "unknown" && state.Gap == "unsupported-control-flow", guard + " => " + state.Gap);
            Assert.Equal([U], Statuses(Compare(Fact(state))));
        }
        // The adapter's Fill result is never a guard.
        var adapterGuard = language == "cs" ? CsPrefix + "var adapter = new SqlDataAdapter(command); var table = new DataTable(); if (adapter.Fill(table) > 0) { return table.Rows[0][\"Name\"]; } return null;"
            : VbPrefix + "Dim adapter = New SqlDataAdapter(command)\nDim table = New DataTable()\nIf adapter.Fill(table) > 0 Then\nReturn table.Rows(0)(\"Name\")\nEnd If\nReturn Nothing";
        Assert.Equal("fill-result-use-unsupported", Assert.Single(Extract(language, adapterGuard)).Gap);
    }

    [Theory]
    [InlineData("command.CommandText = (command.CommandText = \"dbo.ReadOther\") + \"Suffix\";", "nested-command-assignment")]
    [InlineData("var saved = command.CommandText = \"dbo.ReadOther\";", "nested-command-assignment")]
    [InlineData("var ignored = index switch { 0 => command.CommandText = \"dbo.ReadOther\", _ => \"x\" };", "unsupported-control-flow")]
    [InlineData("ref var alias = ref command;", "command-alias-or-escape")]
    [InlineData("command = new SqlCommand(\"dbo.ReadOther\");", "command-reassignment")]
    [InlineData("Helper(command);", "command-alias-or-escape")]
    [InlineData("command.Prepare();", "unsupported-command-member")]
    [InlineData("command.CommandTimeout = 5;", "unsupported-command-assignment")]
    [InlineData("var reader = command.ExecuteReader();", "command-executed-outside-fill")]
    public void The_912_command_protections_are_preserved_before_the_association(string mutation, string gap)
    {
        var state = Assert.Single(Extract("cs", CsPrefix + mutation + "\n" + CsFill + "return table.Rows[0][\"Name\"];"));
        Assert.Equal(("unknown", gap), (state.Coverage, state.Gap));
        Assert.Null(state.CommandTextHash);
        Assert.Equal([U], Statuses(Compare(Fact(state))));
    }

    [Theory]
    [InlineData("var adapter = new SqlDataAdapter(); var table = new DataTable(); var saved = adapter.SelectCommand = command; adapter.Fill(table); return table.Rows[0][\"Name\"];", "select-command-write-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(); var table = new DataTable(); adapter.SelectCommand = new SqlCommand(\"dbo.ReadItems\"); adapter.Fill(table); return table.Rows[0][\"Name\"];", "select-command-local-required")]
    [InlineData("var adapter = new SqlDataAdapter(SharedCommand); var table = new DataTable(); adapter.Fill(table); return table.Rows[0][\"Name\"];", "select-command-local-required")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.FillError += OnError; adapter.Fill(table); return table.Rows[0][\"Name\"];", "unsupported-adapter-member")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.FillLoadOption = LoadOption.Upsert; adapter.Fill(table); return table.Rows[0][\"Name\"];", "adapter-fill-load-option-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.ReturnProviderSpecificTypes = true; adapter.Fill(table); return table.Rows[0][\"Name\"];", "adapter-provider-specific-types-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); adapter.Update(table); return table.Rows[0][\"Name\"];", "adapter-reused")]
    [InlineData("var adapter = new SqlDataAdapter(command); var otherCommand = new SqlCommand(\"dbo.ReadItems\"); var other = new SqlDataAdapter(otherCommand); var table = new DataTable(); adapter.Fill(table); other.Fill(table); return table.Rows[0][\"Name\"];", "repeated-fill")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable { TableName = \"Items\" }; adapter.Fill(table); return table.Rows[0][\"Name\"];", "datatable-initializer-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = Field; adapter.Fill(table); return table.Rows[0][\"Name\"];", "fresh-datatable-required")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.Rows[-1][\"Name\"];", "invalid-row-index")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); var rows = table.Rows; return rows[0][\"Name\"];", "rows-alias-or-escape")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); Helper(table.Rows.Count); return table.Rows[0][\"Name\"];", "unsupported-rows-member")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); table.Rows.RemoveAt(0); return null;", "datatable-mutation")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); table.Rows[0].SetField(\"Name\", \"x\"); return null;", "datatable-mutation")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); table.Rows[0].Delete(); return null;", "unsupported-row-member")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.Rows[0].IsNull(new DataColumn(\"Name\"));", "datacolumn-access-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); object row = table.Rows[0]; return row;", "row-alias-or-escape")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.PrimaryKey;", "datatable-schema-access-unsupported")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.GetChanges();", "ambiguous-row-ownership")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); var copy = table.Rows; adapter.Fill(table); return null;", "table-use-before-fill")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table?.Rows[0][\"Name\"];", "unsupported-control-flow")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); return table.Rows[0][\"Name\"] ?? \"x\";", "unsupported-control-flow")]
    [InlineData("var adapter = new SqlDataAdapter(command); var table = new DataTable(); adapter.Fill(table); dynamic row = table.Rows[0]; return row.Name;", "unsupported-control-flow")]
    public void Adapter_table_row_and_flow_edge_cells_fail_closed_with_exact_gaps(string body, string gap)
    {
        var state = Extract("cs", CsPrefix + body)[0];
        Assert.Equal(("unknown", gap), (state.Coverage, state.Gap));
        Assert.Empty(state.Accesses);
        Assert.Equal([U], Statuses(Compare(Fact(state))));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Converted_and_nonconstant_reads_and_bounds_are_explicit(string language)
    {
        var reads = language == "cs" ? "var a = table.Rows[0][unchecked((byte)257)]; var b = table.Rows[0][name]; var c = table.Rows[0][index]; return table.Rows[0][\"Unit Price\"];"
            : "Dim a = table.Rows(0)(CInt(0.4))\nDim b = table.Rows(0)(name)\nDim c = table.Rows(0)(index)\nReturn table.Rows(0)(\"Unit Price\")";
        var state = Assert.Single(Extract(language, (language == "cs" ? CsPrefix + CsFill : VbPrefix + VbFill) + reads));
        Assert.Equal("bounded", state.Coverage);
        Assert.Equal(language == "cs" ? 1 : 0, state.Accesses[0].Ordinal);
        Assert.Equal(["dynamic-column-name", "dynamic-ordinal", "column-name-outside-identifier-vocabulary"], state.Accesses.Skip(1).Select(a => a.Gap));
        Assert.Equal([M, U, U, U], Statuses(Compare(Fact(state))));
        var many = language == "cs" ? string.Concat(Enumerable.Range(0, 257).Select(i => $"var v{i} = table.Rows[0][{i % 3}];\n")) + "return null;"
            : string.Concat(Enumerable.Range(0, 257).Select(i => $"Dim v{i} = table.Rows(0)({i % 3})\n")) + "Return Nothing";
        Assert.Equal("access-limit", Extract(language, (language == "cs" ? CsPrefix + CsFill : VbPrefix + VbFill) + many)[0].Gap);
        var padding = language == "cs" ? string.Concat(Enumerable.Range(0, 6000).Select(i => $"int unused{i} = 1;\n")) + "return null;"
            : string.Concat(Enumerable.Range(0, 6000).Select(i => $"Dim unused{i} = 1\n")) + "Return Nothing";
        Assert.Equal("operation-limit", Extract(language, (language == "cs" ? CsPrefix + CsFill : VbPrefix + VbFill) + padding)[0].Gap);
    }

    // ---------------------------------------------------------------- Native fixture matrix and CLI
    private static (string Json, string Schema, IReadOnlyDictionary<string, string> Sources) Origin(string origin, string? variant, string repo, string revision)
    {
        var shapes = Path.Combine(RepoRoot(), "samples/sqlserver-result-column-evidence");
        if (origin == "result-schema")
        {
            var items = ItemsShape;
            items = variant switch
            {
                "renamed" => items with { Columns = [Column("Id", 0, "int"), Column("DisplayName", 1), Column("Total", 2, "decimal(10,2)")] },
                "reordered" => items with { Columns = [Column("Total", 0, "decimal(10,2)"), Column("Name", 1), Column("Id", 2, "int")] },
                "duplicate" => items with { Columns = [Column("Id", 0, "int"), Column("Name", 1), Column("Name", 2)] },
                "incomplete" => items with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
                _ => items
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
    private static SqlServerDataTableComparisonReport CompareOrigin(string origin, string? variant, SqlServerDataTableBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var (json, schema, sources) = Origin(origin, variant, repo, revision);
        return origin switch
        {
            "result-schema" => SqlServerDataTableEvidence.CompareResultSchema(schema, json, bindings, facts, repo, revision),
            "ddl-result" => SqlServerDataTableEvidence.CompareDdlResult(schema, json, sources, bindings, facts, repo, revision),
            _ => SqlServerDataTableEvidence.CompareDdlTableResult(schema, json, sources, bindings, facts, repo, revision)
        };
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_fixture_matrix_binds_exact_fills_and_matches_independent_expectations(string language)
    {
        var sample = Path.Combine(RepoRoot(), "samples/sqlserver-datatable-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "DataTables.vbproj" : "DataTables.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var fills = calls.Where(f => f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill").OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(92, fills.Length);
        // Only Fill facts carry DataTable state; the reader property stays on ExecuteReader facts only.
        Assert.All(calls, f => Assert.Equal(f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill", f.Properties.ContainsKey("sqlServerDataTableState")));
        Assert.All(fills, f => Assert.False(f.Properties.ContainsKey("sqlServerReaderState")));
        var keyed = new Dictionary<string, CodeFact>(StringComparer.Ordinal);
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var fact in fills)
        {
            var name = Regex.Match(fact.SourceSymbol!, @"\.([A-Za-z]+)\(").Groups[1].Value;
            seen[name] = seen.GetValueOrDefault(name) + 1;
        }
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
            var state = SqlServerSchemaEvidence.Read<SqlServerDataTableState>(fact.Properties["sqlServerDataTableState"]);
            var expected = StateCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(state.Coverage == "bounded" && state.Gap is null, $"{language}:{key}:{state.Gap}");
            else
                Assert.True(state.Coverage == "unknown" && state.Gap == expected, $"{language}:{key}:{state.Gap}≠{expected}");
        }
        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        var plan = CoreCases.SelectMany(c => Origins.Select(o => (c.Key, Origin: o, c.Value.Variant, c.Value.Statuses)))
            .Concat(StateCases.Keys.Select(k => (Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U }))).ToArray();
        Assert.Equal(148, plan.Length);
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
        Assert.Equal((54, 12, 88), (counts[M], counts[X], counts[U]));

        // Native CLI for each origin: byte-identical repeat, no overwrite, wrong-origin and reader-binding refusals.
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
            var command = new[] { "sqlserver-datatable-result", "compare", "--origin", origin }.Concat(originOptions).Concat(new[] { "--schema", Path.Combine(dir, "schema.json"),
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(dir, "report") }).ToArray();
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(dir, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "datatable-column-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "report", file)), File.ReadAllBytes(Path.Combine(dir, "repeat", file)));
            var readback = SqlServerSchemaEvidence.Read<SqlServerDataTableComparisonReport>(File.ReadAllText(Path.Combine(dir, "report", "datatable-column-comparison.json")));
            Assert.Equal([M, M], Statuses(readback, good.FactId));
            Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<JsonElement>(schema)), readback.SchemaEvidenceSha256);
            var markdown = File.ReadAllText(Path.Combine(dir, "report", "report.md"));
            Assert.Contains("binding-missing", markdown);
            Assert.Contains("datatable-state-unavailable", markdown);
            Assert.Contains("repeated-fill", markdown);
            // The wrong origin label for the same artifacts is refused without publication.
            var wrong = (string[])command.Clone();
            wrong[3] = origin == "ddl-result" ? "ddl-table-result" : origin == "ddl-table-result" ? "ddl-result" : "ddl-table-result";
            wrong[^1] = Path.Combine(dir, "wrong-origin");
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(wrong, writer, error));
            Assert.False(Directory.Exists(wrong[^1]));
        }
        // Reader-result bindings are refused by the DataTable route, and DataTable bindings by the reader route.
        var readerBindings = Path.Combine(temp.Path, "reader-bindings.json");
        File.WriteAllText(readerBindings, SqlServerSchemaEvidence.Json(new SqlServerResultBindings("sqlserver-result-bindings/1", good.Repo, good.CommitSha,
            [new(good.FactId, "dbo.ReadItems", Items, SqlServerResultEvidence.ReaderDigest(good))])));
        var jsonDir = Path.Combine(temp.Path, "result-schema");
        string[] Route(string route, string bindings, string output) => [route, "compare", .. (route == "sqlserver-datatable-result" ? new[] { "--origin", "result-schema" } : Array.Empty<string>()),
            "--declarations", Path.Combine(jsonDir, "origin.json"), "--schema", Path.Combine(jsonDir, "schema.json"), "--bindings", bindings, "--facts", factsPath,
            "--repo", good.Repo, "--revision", good.CommitSha, "--out", output];
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-datatable-result", readerBindings, Path.Combine(temp.Path, "x1")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-result-schema", bindingsPath, Path.Combine(temp.Path, "x2")), writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "x1")) || Directory.Exists(Path.Combine(temp.Path, "x2")));
    }

    [Fact]
    public void DataTable_rule_has_one_catalog_entry_with_evidence_and_limits()
    {
        var catalog = File.ReadAllText(Path.Combine(RepoRoot(), "rules", "rule-catalog.yml"));
        var entry = Assert.Single(Regex.Matches(catalog, $@"(?m)^  - id: {Regex.Escape(SqlServerDataTableEvidence.RuleId)}\r?$").Cast<Match>());
        var end = catalog.IndexOf("\n  - id:", entry.Index + entry.Length, StringComparison.Ordinal);
        var block = catalog[entry.Index..(end < 0 ? catalog.Length : end)];
        foreach (var required in new[] { "Tier2Structural", "Tier4Unknown", "SqlServerDataTableComparison", "limitations:", SqlServerDataTableEvidence.StateVersion,
            SqlServerDataTableEvidence.BindingsVersion, "SHA-256", "no SQL execution", "Fill executes", "TableMappings", "case-insensitive" })
            Assert.Contains(required, block, StringComparison.Ordinal);
    }
}
