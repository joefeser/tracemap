using System.Text;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

/// <summary>Opt-in table-backed DDL result shapes (Phase 1 after #912). Expectations are authored independently of the extractor.</summary>
public sealed class SqlServerDdlTableResultEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };
    private const string Tables = "CREATE TABLE dbo.Items (\n  Id int NOT NULL PRIMARY KEY,\n  Name nvarchar(50) NOT NULL,\n  Total decimal(10,2) NULL,\n  Price decimal(10,2) NULL,\n  ItemCode nvarchar(20) NOT NULL\n);\nGO\n";

    // Independently authored #912 per-case expectations, replayed against an equivalent table-backed base shape.
    private static readonly Dictionary<string, string[]> SourceCases = new()
    {
        ["NameMatch"] = [M], ["MissingName"] = [X], ["Renamed"] = [X], ["OrdinalValid"] = [M], ["OrdinalOutOfRange"] = [X],
        ["Reordered"] = [M, M], ["GetOrdinalDirect"] = [M, M], ["GetOrdinalLocal"] = [M, M], ["GetOrdinalMissing"] = [X, X],
        ["CaseOnly"] = [U], ["DuplicateDeclared"] = [U], ["DynamicName"] = [U], ["DynamicOrdinal"] = [U], ["AbsentBinding"] = [U],
        ["ReaderAlias"] = [U], ["ReaderReassignment"] = [U], ["CommandAlias"] = [U], ["OrdinalAlias"] = [M, U], ["OrdinalReassignment"] = [M, U],
        ["HelperEscape"] = [U], ["TwoReaders#0"] = [M], ["TwoReaders#1"] = [M], ["ReadLoop"] = [M, M], ["ReadGuard"] = [M],
        ["UsingLifetime"] = [M], ["NextResult"] = [U], ["ScalarChannel"] = [U], ["NonQueryChannel"] = [U], ["RecordsAffected"] = [U],
        ["Incomplete"] = [U], ["TextCommand"] = [U], ["Conditional"] = [U], ["WithParameter"] = [M], ["NullCheck"] = [M]
    };
    private static readonly Dictionary<string, string> ReplayTemplates = new()
        { ["Renamed"] = "renamed.sql", ["Reordered"] = "reordered.sql", ["DuplicateDeclared"] = "duplicate.sql", ["Incomplete"] = "if.sql" };
    // (source case, template, table file or null, expected statuses), mirrored independently in the native smoke.
    private static readonly (string Key, string Template, string? Tables, string[] Statuses)[] ExtraCases =
    [
        ("NameMatch", "alias-table.sql", "tables.sql", [M]), ("NameMatch", "alias-output.sql", "tables.sql", [M]),
        ("NameMatch", "bracketed.sql", "tables.sql", [M]), ("OrdinalOutOfRange", "wide.sql", "tables.sql", [M]),
        ("NameMatch", "base.sql", null, [U]), ("NameMatch", "view.sql", "tables.sql", [U]), ("NameMatch", "missing-column.sql", "tables.sql", [U]),
        ("NameMatch", "wrong-schema.sql", "tables.sql", [U]), ("NameMatch", "wrong-database.sql", "tables.sql", [U]),
        ("NameMatch", "unqualified.sql", "tables.sql", [U]), ("NameMatch", "case-table.sql", "tables.sql", [U]),
        ("NameMatch", "case-column.sql", "tables.sql", [U]), ("NameMatch", "case-output.sql", "tables.sql", [U]),
        ("NameMatch", "qualifier-unbound.sql", "tables.sql", [U]), ("NameMatch", "qualifier-no-alias.sql", "tables.sql", [U]),
        ("NameMatch", "star.sql", "tables.sql", [U]), ("NameMatch", "join.sql", "tables.sql", [U]), ("NameMatch", "cte.sql", "tables.sql", [U]),
        ("NameMatch", "subquery.sql", "tables.sql", [U]), ("NameMatch", "union.sql", "tables.sql", [U]), ("NameMatch", "temp.sql", "tables.sql", [U]),
        ("NameMatch", "dynamic.sql", "tables.sql", [U]), ("NameMatch", "exec.sql", "tables.sql", [U]), ("NameMatch", "if.sql", "tables.sql", [U]),
        ("NameMatch", "multiple.sql", "tables.sql", [U]), ("NameMatch", "where.sql", "tables.sql", [U]), ("NameMatch", "expression.sql", "tables.sql", [U]),
        ("NameMatch", "hint.sql", "tables.sql", [U]), ("NameMatch", "base.sql", "tables-computed.sql", [U]), ("NameMatch", "base.sql", "tables-case.sql", [U])
    ];

    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }
    private static Dictionary<string, string> Sources(string procedures, string? tables = Tables)
    {
        var sources = new Dictionary<string, string>(StringComparer.Ordinal) { ["procedures.sql"] = procedures };
        if (tables is not null) sources["tables.sql"] = tables;
        return sources;
    }
    private static string Input(IReadOnlyDictionary<string, string> sources, string version = SqlServerDdlTableResultEvidence.InputVersion, string revision = Revision,
        string repo = "synthetic")
        => SqlServerSchemaEvidence.Json(new SqlServerDdlInput(version, repo, revision, Items.Server, Items.Database, true,
            sources.Select(f => new SqlServerDdlFile(f.Key, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(f.Value)))).ToArray()));
    private static SqlServerDdlTableResultExport Ddl(IReadOnlyDictionary<string, string> sources) => SqlServerDdlTableResultEvidence.Produce(Input(sources), sources);
    private static SqlServerDdlTableResultExport Ddl(string procedures, string? tables = Tables) => Ddl(Sources(procedures, tables));
    private static SqlServerResultShape ItemsShape(SqlServerDdlTableResultExport export) => export.Declarations.Shapes.Single(s => s.Identity == Items);

    private static SqlServerReaderState State(params SqlServerReaderAccess[] accesses) => new("sqlserver-reader/1", "bounded", null, "StoredProcedure",
        FactFactory.Hash("dbo.ReadItems", 64), 4, 4, "none", accesses);
    private static SqlServerReaderAccess Name(string name) => new("name-indexer", "Item", name, null, null, 5, 5, null);
    private static SqlServerReaderAccess Ordinal(int ordinal, string member = "GetString") => new("ordinal-getter", member, null, ordinal, "constant", 5, 5, null);
    private static CodeFact Fact(SqlServerReaderState state, string id = "fact") => new(id, "scan", "synthetic", Revision, null,
        FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern, EvidenceTiers.Tier1Semantic, "Example.Read()", null, null,
        new("Calls.cs", 4, 4, null, "CSharpSemanticExtractor", ScannerVersions.CSharpSemanticExtractor),
        new Dictionary<string, string> { ["sqlServerReaderState"] = SqlServerSchemaEvidence.Json(state) });
    private static SqlServerResultBindings Bindings(CodeFact fact, SqlServerObjectIdentity? identity = null)
    {
        var i = identity ?? Items;
        return new("sqlserver-result-bindings/1", fact.Repo, fact.CommitSha, [new(fact.FactId, i.Schema + "." + i.Procedure, i, SqlServerResultEvidence.ReaderDigest(fact))]);
    }
    private static SqlServerDdlTableResultComparisonReport Compare(IReadOnlyDictionary<string, string> sources, CodeFact fact, SqlServerResultBindings? bindings = null)
        => SqlServerDdlTableResultEvidence.Compare(SqlServerSchemaEvidence.Json(Ddl(sources)), Input(sources), sources, bindings ?? Bindings(fact), [fact], fact.Repo, fact.CommitSha);

    // ---------------------------------------------------------------- Accepted grammar

    [Theory]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name, Total FROM dbo.Items;", "Id,Name,Total", "Id,Name,Total")]
    [InlineData("CREATE OR ALTER PROC [dbo].[ReadItems]\nAS\nBEGIN\n  SELECT [Id],\n    \"Name\",\n    Total\n  FROM [dbo].[Items];\nEND", "Id,Name,Total", "Id,Name,Total")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT i.Id, i.Name, i.Total FROM dbo.Items AS i;", "Id,Name,Total", "Id,Name,Total")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT i.Id, Name, i.Total FROM dbo.Items i;", "Id,Name,Total", "Id,Name,Total")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, ItemCode AS Name, Total = Price, Name Label FROM dbo.Items;", "Id,Name,Total,Label", "Id,ItemCode,Price,Name")]
    [InlineData("ALTER PROCEDURE dbo.ReadItems @Filter int AS SELECT Total, Name, Id FROM dbo.Items;", "Total,Name,Id", "Total,Name,Id")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT Name AS Id, Id AS Name FROM dbo.Items;", "Id,Name", "Name,Id")]
    public void Supported_table_projection_is_complete_with_order_output_names_sources_and_no_body_text(string sql, string outputs, string sources)
    {
        var export = Ddl(sql);
        var shape = ItemsShape(export);
        Assert.Equal("complete", shape.Completeness);
        Assert.Equal(outputs.Split(','), shape.Columns.Select(c => c.Name));
        Assert.Equal(Enumerable.Range(0, shape.Columns.Count), shape.Columns.Select(c => c.Ordinal));
        Assert.All(shape.Columns, c => Assert.InRange(c.Line, shape.StartLine, shape.EndLine));
        var projection = Assert.Single(export.Projections);
        Assert.Equal(Items, projection.Procedure);
        Assert.Equal(new SqlServerTableIdentity(Items.Server, Items.Database, "dbo", "Items"), projection.Table);
        Assert.Equal(sources.Split(','), projection.Columns.Select(c => c.SourceColumn));
        var table = Assert.Single(export.Tables);
        Assert.Equal("complete", table.Completeness);
        Assert.Equal(["Id", "Name", "Total", "Price", "ItemCode"], table.Columns.Select(c => c.Name));
        Assert.Equal(["int", "nvarchar(50)", "decimal(10,2)", "decimal(10,2)", "nvarchar(20)"], table.Columns.Select(c => c.DeclaredType));
        // Output declared types come from the backing table column and stay visible, never compared.
        Assert.Equal(projection.Columns.Select(c => table.Columns.Single(t => t.Name == c.SourceColumn).DeclaredType), shape.Columns.Select(c => c.DeclaredType));
        Assert.Equal(projection.Columns.Select(c => table.Columns.Single(t => t.Name == c.SourceColumn).Line), projection.Columns.Select(c => c.SourceColumnLine));
        Assert.Equal("column-type-facets-not-compared", Assert.Single(export.Unsupported).Kind);
        Assert.Empty(export.TableUnsupported);
        Assert.Equal(SqlServerDdlTableResultEvidence.Origin, export.Declarations.Origin);
        Assert.Equal("sqlserver-ddl-table-result-declarations/1", export.Declarations.Version);
        Assert.Equal(SqlServerDdlTableResultEvidence.RuleId, export.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, export.Tier);
        var json = SqlServerSchemaEvidence.Json(export);
        Assert.DoesNotContain("SELECT", json);
        Assert.DoesNotContain("PRIMARY", json);
        Assert.DoesNotContain("\"i\"", json);
        Assert.Equal(json, SqlServerSchemaEvidence.Json(Ddl(sql)));
    }

    [Fact]
    public void Definitions_are_one_unordered_set_across_files_and_constant_projections_keep_their_rules()
    {
        // Procedure file sorts before the table file, and the table follows the procedure in one file: no ordering is inferred.
        var split = Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT Id FROM dbo.Items;");
        Assert.Equal("complete", ItemsShape(split).Completeness);
        var oneFile = Ddl(new Dictionary<string, string> { ["one.sql"] = "CREATE PROCEDURE dbo.ReadItems AS SELECT Id FROM dbo.Items;\nGO\n" + Tables });
        Assert.Equal("complete", ItemsShape(oneFile).Completeness);
        // Without FROM the existing constant-projection rules apply unchanged inside the table route.
        var constant = Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT CAST(1 AS int) AS Id;\nGO\nCREATE PROCEDURE dbo.ReadOther AS SELECT 1, 2 AS Name;");
        Assert.Equal(["complete", "unknown"], constant.Declarations.Shapes.Select(s => s.Completeness));
        Assert.Equal("explicit-alias-required", constant.Declarations.Shapes[1].UnknownReason);
        Assert.Empty(constant.Projections);
        Assert.All(constant.Unsupported, g => Assert.Equal(SqlServerDdlTableResultEvidence.RuleId, g.RuleId));
        // Trailing commas are legal T-SQL in CREATE TABLE and do not add columns.
        Assert.Equal(["Id"], Assert.Single(Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT Id FROM dbo.Items;", "CREATE TABLE dbo.Items (Id int,);").Tables).Columns.Select(c => c.Name));
    }

    // ---------------------------------------------------------------- Unknown shapes (never partial columns)

    [Theory]
    [InlineData("SELECT * FROM dbo.Items;", "select-star-unsupported")]
    [InlineData("SELECT i.* FROM dbo.Items i;", "select-star-unsupported")]
    [InlineData("SELECT i.Id FROM dbo.Items i JOIN dbo.Items j ON j.Id = i.Id;", "join-unsupported")]
    [InlineData("SELECT i.Id FROM dbo.Items i LEFT JOIN dbo.Items j ON j.Id = i.Id;", "join-unsupported")]
    [InlineData("SELECT i.Id FROM dbo.Items i CROSS JOIN dbo.Items j;", "join-unsupported")]
    [InlineData("SELECT i.Id FROM dbo.Items i CROSS APPLY dbo.F(i.Id) f;", "join-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items, dbo.Items2;", "multiple-table-sources-unsupported")]
    [InlineData("SELECT d.Id FROM (SELECT Id FROM dbo.Items) AS d;", "subquery-unsupported")]
    [InlineData("SELECT Id, (SELECT 1) AS Extra FROM dbo.Items;", "subquery-unsupported")]
    [InlineData("WITH c AS (SELECT Id FROM dbo.Items) SELECT Id FROM c;", "cte-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items UNION SELECT Id FROM dbo.Items;", "set-operation-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items EXCEPT SELECT Id FROM dbo.Items;", "set-operation-unsupported")]
    [InlineData("SELECT Id FROM #Items;", "temporary-table-unsupported")]
    [InlineData("SELECT Id FROM ##Items;", "temporary-table-unsupported")]
    [InlineData("SELECT Id FROM @Items;", "table-variable-unsupported")]
    [InlineData("SELECT Id FROM dbo.F(1);", "table-valued-function-unsupported")]
    [InlineData("SELECT Id FROM OPENJSON(N'[]');", "table-reference-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items WITH (NOLOCK);", "table-hint-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items TABLESAMPLE (10 PERCENT);", "table-reference-unsupported")]
    [InlineData("SELECT Id FROM (VALUES (1)) AS v (Id);", "table-reference-unsupported")]
    [InlineData("SELECT p.Id FROM dbo.Items AS i PIVOT (MAX(Total) FOR Name IN ([A])) AS p;", "table-reference-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items FOR SYSTEM_TIME AS OF '2020-01-01';", "table-reference-unsupported")]
    [InlineData("SELECT Id FROM Items;", "table-reference-schema-required")]
    [InlineData("SELECT Id FROM SyntheticDatabase.dbo.Items;", "cross-database-or-server-reference-unsupported")]
    [InlineData("SELECT Id FROM OtherDatabase.dbo.Items;", "cross-database-or-server-reference-unsupported")]
    [InlineData("SELECT Id FROM SyntheticServer.SyntheticDatabase.dbo.Items;", "cross-database-or-server-reference-unsupported")]
    [InlineData("SELECT Id FROM sales.Items;", "table-dependency-missing")]
    [InlineData("SELECT Id FROM dbo.ActiveItems;", "table-dependency-missing")]
    [InlineData("SELECT Id FROM dbo.ReadOther;", "table-dependency-missing")]
    [InlineData("SELECT Id FROM dbo.items;", "table-reference-case-policy-unknown")]
    [InlineData("SELECT Id FROM DBO.Items;", "table-reference-case-policy-unknown")]
    [InlineData("SELECT Id FROM [dbo].[Unit Items];", "table-reference-outside-identifier-vocabulary")]
    [InlineData("SELECT Id, Discount FROM dbo.Items;", "column-dependency-missing")]
    [InlineData("SELECT Id, name FROM dbo.Items;", "column-case-policy-unknown")]
    [InlineData("SELECT x.Id FROM dbo.Items AS i;", "column-qualifier-unbound")]
    [InlineData("SELECT Items.Id FROM dbo.Items AS i;", "column-qualifier-unbound")]
    [InlineData("SELECT I.Id FROM dbo.Items AS i;", "column-qualifier-case-policy-unknown")]
    [InlineData("SELECT Items.Id FROM dbo.Items;", "column-qualifier-without-alias-unsupported")]
    [InlineData("SELECT dbo.Items.Id FROM dbo.Items;", "column-qualifier-unsupported")]
    [InlineData("SELECT Id, Id FROM dbo.Items;", "duplicate-or-case-only-output-name-unsupported")]
    [InlineData("SELECT Id, Name AS id FROM dbo.Items;", "duplicate-or-case-only-output-name-unsupported")]
    [InlineData("SELECT Id AS 'Key' FROM dbo.Items;", "string-alias-unsupported")]
    [InlineData("SELECT Id AS [Item Key] FROM dbo.Items;", "alias-outside-identifier-vocabulary")]
    [InlineData("SELECT UPPER(Name) AS Name FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT (Id) AS Id FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT Id + 1 AS Id FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT 1 AS Id FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT CAST(Id AS bigint) AS Id FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT Name COLLATE Latin1_General_CS_AS AS Name FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT $IDENTITY FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT @Filter AS Id FROM dbo.Items;", "non-column-projection-unsupported")]
    [InlineData("SELECT @Filter = Id FROM dbo.Items;", "select-assignment-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items WHERE Id = @Filter;", "select-clause-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items ORDER BY Id;", "select-clause-unsupported")]
    [InlineData("SELECT TOP (1) Id FROM dbo.Items;", "select-clause-unsupported")]
    [InlineData("SELECT DISTINCT Id FROM dbo.Items;", "select-clause-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items GROUP BY Id;", "select-clause-unsupported")]
    [InlineData("SELECT Id INTO #Copy FROM dbo.Items;", "select-clause-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items FOR JSON PATH;", "select-clause-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items OPTION (RECOMPILE);", "select-clause-unsupported")]
    [InlineData("IF @Filter = 1 SELECT Id FROM dbo.Items;", "conditional-body-unsupported")]
    [InlineData("BEGIN IF @Filter = 1 BEGIN SELECT Id FROM dbo.Items; END END", "conditional-body-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items; SELECT Name FROM dbo.Items;", "body-statement-count-unsupported")]
    [InlineData("SET NOCOUNT ON; SELECT Id FROM dbo.Items;", "body-statement-count-unsupported")]
    [InlineData("BEGIN BEGIN SELECT Id FROM dbo.Items; END END", "nested-block-unsupported")]
    [InlineData("EXEC sys.sp_executesql N'SELECT Id FROM dbo.Items';", "exec-or-dynamic-sql-unsupported")]
    [InlineData("EXEC dbo.ReadOther;", "exec-or-dynamic-sql-unsupported")]
    [InlineData("WHILE @Filter = 0 SELECT Id FROM dbo.Items;", "loop-body-unsupported")]
    [InlineData("BEGIN TRY SELECT Id FROM dbo.Items; END TRY BEGIN CATCH SELECT Id FROM dbo.Items; END CATCH", "try-catch-body-unsupported")]
    [InlineData("RETURN 7;", "return-channel-not-result-set")]
    [InlineData("SELECT Id AS Id;", "non-constant-projection-unsupported")]
    public void Unsupported_procedure_bodies_are_explicit_unknown_shapes_never_partial_columns(string body, string kind)
    {
        var sql = "CREATE PROCEDURE dbo.ReadItems @Filter int AS " + body;
        var export = Ddl(sql);
        var shape = ItemsShape(export);
        Assert.Equal("unknown", shape.Completeness);
        Assert.Equal(kind, shape.UnknownReason);
        Assert.Empty(shape.Columns);
        Assert.Empty(export.Projections);
        var gap = Assert.Single(export.Unsupported);
        Assert.Equal(kind, gap.Kind);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.Tier);
        Assert.Equal(SqlServerDdlTableResultEvidence.RuleId, gap.RuleId);
        Assert.Equal("complete", Assert.Single(export.Tables).Completeness);
        var fact = Fact(State(Name("Name"), Ordinal(9)));
        var rows = Compare(Sources(sql), fact).Comparison.Comparisons;
        Assert.Equal([U, U], rows.Select(r => r.Status));
        Assert.All(rows, r => Assert.Equal(["result-shape-unknown:" + kind], r.Reasons));
    }

    [Theory]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name AS (Id));", "computed-column-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int DEFAULT 1, Name nvarchar(5));", "column-default-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int CONSTRAINT DF_Id DEFAULT 1, Name nvarchar(5));", "column-default-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5) COLLATE Latin1_General_CS_AS);", "column-collation-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id dbo.ItemKey, Name nvarchar(5));", "column-type-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name xml);", "column-type-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int CHECK (Id > 0), Name nvarchar(5));", "column-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int REFERENCES dbo.Other (Id), Name nvarchar(5));", "column-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int PRIMARY KEY WITH (FILLFACTOR = 80), Name nvarchar(5));", "column-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id uniqueidentifier ROWGUIDCOL, Name nvarchar(5));", "column-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int SPARSE NULL, Name nvarchar(5));", "column-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int INDEX IX_Id, Name nvarchar(5));", "column-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), CHECK (Id > 0));", "table-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), CONSTRAINT FK_X FOREIGN KEY (Id) REFERENCES dbo.Other (Id));", "table-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), CONSTRAINT PK_Items PRIMARY KEY (Id) ON [PRIMARY]);", "table-constraint-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5)) ON [PRIMARY];", "table-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5)) WITH (DATA_COMPRESSION = PAGE);", "table-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), INDEX IX_Id (Id));", "table-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5)) AS NODE;", "table-option-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, [Item Name] nvarchar(5));", "column-name-outside-identifier-vocabulary")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), name nvarchar(5));", "duplicate-or-case-only-column-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int, Name nvarchar(5), Name int);", "duplicate-or-case-only-column-unsupported")]
    public void Unsupported_table_definitions_are_unknown_and_never_back_a_shape(string table, string kind)
    {
        var export = Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name FROM dbo.Items;", table);
        var definition = Assert.Single(export.Tables);
        Assert.Equal("unknown", definition.Completeness);
        Assert.Equal(kind, definition.UnknownReason);
        Assert.Empty(definition.Columns);
        var gap = Assert.Single(export.TableUnsupported);
        Assert.Equal(kind, gap.Kind);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.Tier);
        var shape = ItemsShape(export);
        Assert.Equal("table-definition-unsupported", shape.UnknownReason);
        Assert.Empty(shape.Columns);
        Assert.Empty(export.Projections);
    }

    [Fact]
    public void Inert_keys_identity_and_nullability_keep_a_supported_table_definition()
    {
        var export = Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name FROM dbo.Items;",
            "CREATE TABLE [dbo].[Items] (Id int IDENTITY(1,1) NOT NULL CONSTRAINT PK_Items PRIMARY KEY CLUSTERED, Name nvarchar(MAX) NULL UNIQUE, Total rowversion, CONSTRAINT UQ_X UNIQUE NONCLUSTERED (Name, Id));");
        var table = Assert.Single(export.Tables);
        Assert.Equal("complete", table.Completeness);
        Assert.Equal(["int", "nvarchar(MAX)", "rowversion"], table.Columns.Select(c => c.DeclaredType));
        Assert.Equal("complete", ItemsShape(export).Completeness);
    }

    // ---------------------------------------------------------------- Whole-input refusals and contract isolation

    [Theory]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nCREATE TABLE dbo.Items (Id int);", "ambiguous-object")]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nCREATE TABLE DBO.items (Id int);", "ambiguous-object")]
    [InlineData("CREATE TABLE dbo.ReadItems (Id int);", "ambiguous-object")]
    [InlineData("CREATE TABLE dbo.readitems (Id int);", "ambiguous-object")]
    [InlineData("CREATE TABLE Items (Id int);", "explicit-bounded-schema-required")]
    [InlineData("CREATE TABLE #Items (Id int);", "explicit-bounded-schema-required")]
    [InlineData("CREATE TABLE SyntheticDatabase.dbo.Items (Id int);", "explicit-bounded-schema-required")]
    [InlineData("CREATE TABLE dbo.[Unit Items] (Id int);", "explicit-bounded-schema-required")]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nALTER TABLE dbo.Items ADD Name nvarchar(5);", "top-level-statement-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nCREATE VIEW dbo.ActiveItems AS SELECT Id FROM dbo.Items;", "top-level-statement-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nDROP TABLE dbo.Items;", "top-level-statement-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int);\nGO\nCREATE INDEX IX ON dbo.Items (Id);", "top-level-statement-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int NOT NULL PRIMARY);", "malformed-sql")]
    [InlineData("CREATE TABLE dbo.Items (Id int); -- :setvar Owner dbo", "sqlcmd-unresolved")]
    [InlineData("CREATE TABLE dbo.$(Owner) (Id int);", "sqlcmd-unresolved")]
    public void Whole_input_failures_reject_every_shape(string tables, string reason)
        => Assert.Contains("ddl-" + reason, Assert.Throws<ArgumentException>(() => Ddl("CREATE PROCEDURE dbo.ReadItems AS SELECT Id FROM dbo.Items;", tables)).Message);

    [Fact]
    public void Existing_routes_still_reject_table_definitions_and_never_reinterpret_table_artifacts()
    {
        var sources = Sources("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name FROM dbo.Items;");
        var tableInput = Input(sources);
        var export = Ddl(sources);
        // The parameter and constant result routes keep their whole-input CREATE TABLE refusal.
        Assert.Contains("ddl-top-level-statement-unsupported", Assert.Throws<ArgumentException>(() =>
            SqlServerDdlEvidence.Produce(Input(sources, SqlServerDdlEvidence.InputVersion), sources)).Message);
        Assert.Contains("ddl-top-level-statement-unsupported", Assert.Throws<ArgumentException>(() =>
            SqlServerDdlResultEvidence.Produce(Input(sources, SqlServerDdlResultEvidence.InputVersion), sources)).Message);
        // Manifest versions never cross routes.
        Assert.Contains("ddl-unsupported-input-contract", Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(tableInput, sources)).Message);
        Assert.Contains("ddl-unsupported-input-contract", Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Produce(tableInput, sources)).Message);
        foreach (var version in new[] { SqlServerDdlEvidence.InputVersion, SqlServerDdlResultEvidence.InputVersion })
            Assert.Contains("ddl-unsupported-input-contract", Assert.Throws<ArgumentException>(() =>
                SqlServerDdlTableResultEvidence.Produce(Input(sources, version), sources)).Message);
        // Constant-route exports cannot enter the table route and vice versa.
        var constantSources = new Dictionary<string, string> { ["procedures.sql"] = "CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id;" };
        var constant = SqlServerDdlResultEvidence.Produce(Input(constantSources, SqlServerDdlResultEvidence.InputVersion), constantSources);
        Assert.ThrowsAny<Exception>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(constant), Input(constantSources), constantSources, "synthetic", Revision));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(export),
            Input(constantSources, SqlServerDdlResultEvidence.InputVersion), constantSources, "synthetic", Revision));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), tableInput, sources, "synthetic", Revision));
        // Shared validation names the exact table-route version and origin.
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Validate(export.Declarations, ddl: true));
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Validate(export.Declarations with { Origin = "public-synthetic-tsql-ddl-result" },
            SqlServerDdlTableResultEvidence.DeclarationsVersion, SqlServerDdlTableResultEvidence.Origin));
    }

    [Fact]
    public void Admission_refuses_stale_inputs_substitution_and_unexpected_identity()
    {
        var sources = Sources("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name, Total FROM dbo.Items;");
        var input = Input(sources);
        var export = Ddl(sources);
        Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerDdlTableResultEvidence.ReadInput(input)), export.BoundedInputSha256);
        Assert.Equal(SqlServerSchemaEvidence.GeneratorSha256, export.GeneratorSha256);
        var table = export.Tables[0];
        foreach (var bad in new[] { export with { Version = "sqlserver-ddl-result-schema/1" }, export with { GeneratorSha256 = new('0', 64) },
            export with { ParserSha256 = new('0', 64) }, export with { ExtractorVersion = SqlServerDdlResultEvidence.ExtractorVersion },
            export with { RuleId = SqlServerDdlResultEvidence.RuleId }, export with { Tier = EvidenceTiers.Tier1Semantic },
            export with { Unsupported = [] }, export with { TableUnsupported = [new(table.Identity, "x", SqlServerDdlTableResultEvidence.RuleId, EvidenceTiers.Tier4Unknown, "tables.sql", 1, 1)] },
            export with { Tables = [table with { Columns = table.Columns.Take(2).ToArray() }] },
            export with { Tables = [table with { Identity = table.Identity with { Schema = "sales" } }] },
            export with { Projections = [export.Projections[0] with { Columns = export.Projections[0].Columns.Reverse().ToArray() }] },
            export with { Projections = [] },
            export with { Declarations = export.Declarations with { Origin = "public-synthetic-tsql-ddl-result" } },
            export with { Declarations = export.Declarations with { Shapes = [export.Declarations.Shapes[0] with { Columns = export.Declarations.Shapes[0].Columns.Take(1).ToArray() }] } } })
            Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(bad), input, sources, "synthetic", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), input, sources, "synthetic", new('2', 40)));
        Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), input, sources, "other", Revision));
        // Stale table bytes under the old manifest hash fail the digest; a re-hashed changed table fails regeneration.
        var changed = Sources("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name, Total FROM dbo.Items;", Tables.Replace("Total decimal", "Amount decimal", StringComparison.Ordinal));
        Assert.Contains("ddl-source-digest-mismatch", Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), input, changed, "synthetic", Revision)).Message);
        Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), Input(changed), changed, "synthetic", Revision));
        Assert.Equal("column-dependency-missing", ItemsShape(Ddl(changed)).UnknownReason);
        // Omitting the table file from the manifest is a missing dependency, not a guessed shape.
        var noTables = Sources("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, Name, Total FROM dbo.Items;", null);
        Assert.Equal("table-dependency-missing", ItemsShape(Ddl(noTables)).UnknownReason);
        Assert.Contains("ddl-source-set-mismatch", Assert.Throws<ArgumentException>(() => SqlServerDdlTableResultEvidence.Produce(input, noTables)).Message);
        // 257 tables spread over files so the per-file syntax budget is not what fails.
        var many = Enumerable.Range(0, 29).ToDictionary(f => $"t{f:00}.sql",
            f => string.Join("\nGO\n", Enumerable.Range(f * 9, 9).Select(i => $"CREATE TABLE dbo.T{i} (Id int);")), StringComparer.Ordinal);
        many["procedures.sql"] = "CREATE PROCEDURE dbo.ReadItems AS SELECT Id FROM dbo.T0;";
        Assert.Contains("ddl-declaration-limit", Assert.Throws<ArgumentException>(() => Ddl(many)).Message);
    }

    // ---------------------------------------------------------------- Comparison semantics

    [Fact]
    public void Reader_accesses_compare_against_projected_outputs_not_the_whole_table()
    {
        var sources = Sources("CREATE PROCEDURE dbo.ReadItems AS SELECT Id, ItemCode AS Code, Name FROM dbo.Items;");
        var fact = Fact(State(Name("Name"), Name("Code"), Name("ItemCode"), Name("Price"), Name("name"), Ordinal(2), Ordinal(3), Ordinal(-1)));
        var report = Compare(sources, fact);
        Assert.Equal("sqlserver-ddl-table-result-comparison/1", report.Version);
        Assert.Equal("sqlserver-ddl-table-result-column-comparison/1", report.Comparison.Version);
        Assert.Equal(SqlServerDdlTableResultEvidence.Origin, report.Comparison.Origin);
        Assert.Equal(("partial", "no-aggregate-clean-verdict"), (report.Comparison.Coverage, report.Comparison.Verdict));
        var rows = report.Comparison.Comparisons;
        Assert.Equal([M, M, X, X, U, M, X, X], rows.Select(r => r.Status));
        Assert.Equal(["missing-column:ItemCode"], rows[2].Reasons);
        Assert.Equal(["missing-column:Price"], rows[3].Reasons);
        Assert.Equal(["column-case-policy-unknown:name"], rows[4].Reasons);
        Assert.Equal(["ordinal-out-of-range:3"], rows[6].Reasons);
        Assert.Contains("declared-type-not-compared:nvarchar(20)", rows[1].UncomparedFacets);
        Assert.Contains("getter-type-not-compared:GetString", rows[5].UncomparedFacets);
        Assert.All(rows, r => Assert.Equal(r.Status == U ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, r.Tier));
        Assert.All(rows, r => Assert.Equal(EvidenceTiers.Tier1Semantic, r.SourceTier));
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Compare(sources, fact)));
        var markdown = SqlServerResultReportWriter.Build(report);
        foreach (var expected in new[] { "No aggregate clean verdict", "missing-column:ItemCode", "1 Code ← ItemCode", "DDL table: SyntheticServer.SyntheticDatabase.dbo.Items at tables.sql:1–7" })
            Assert.Contains(expected, markdown);
        // Wrong database at binding: exact identity absent, never a short-name fallback.
        var wrong = Assert.Single(Compare(sources, fact, Bindings(fact, Items with { Database = "OtherDatabase" })).Comparison.Comparisons);
        Assert.Equal(U, wrong.Status);
        Assert.Contains("exact-result-shape-unavailable", wrong.Reasons);
        // Parameter bindings never substitute for reader bindings.
        Assert.Throws<ArgumentException>(() => Compare(sources, fact, Bindings(fact) with { Version = "sqlserver-bindings/1" }));
        Assert.Throws<ArgumentException>(() => Compare(sources, fact, Bindings(fact) with { Calls = [Bindings(fact).Calls[0] with { ReaderEvidenceSha256 = SqlServerSchemaEvidence.CallDigest(fact) }] }));
        // Unbound identity text and provenance are neither reported nor hashed.
        var hidden = Compare(sources, fact, Bindings(fact, Items with { Database = "HiddenDatabase" }) with { Provenance = new(new('a', 64), new('a', 64)) });
        Assert.DoesNotContain("HiddenDatabase", SqlServerSchemaEvidence.Json(hidden) + SqlServerResultReportWriter.Build(hidden));
    }

    // ---------------------------------------------------------------- Native fixture matrix and CLI

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_fixture_replay_and_table_cases_match_independent_expectations(string language)
    {
        var root = RepoRoot();
        var sample = Path.Combine(root, "samples/sqlserver-result-column-evidence");
        var templates = Path.Combine(sample, "ddl-table");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Results.vbproj" : "Results.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(34, calls.Length);
        var keyed = new Dictionary<string, CodeFact>(StringComparer.Ordinal);
        foreach (var fact in calls)
        {
            var name = System.Text.RegularExpressions.Regex.Match(fact.SourceSymbol!, @"\.([A-Za-z]+)\(").Groups[1].Value;
            keyed.Add(name == "TwoReaders" ? name + "#" + keyed.Keys.Count(k => k.StartsWith("TwoReaders", StringComparison.Ordinal)) : name, fact);
        }
        Assert.Equal(SourceCases.Keys.Order(StringComparer.Ordinal), keyed.Keys.Order(StringComparer.Ordinal));
        var plan = SourceCases.Select(c => (c.Key, ReplayTemplates.GetValueOrDefault(c.Key, "base.sql"), (string?)"tables.sql", c.Value)).Concat(ExtraCases).ToArray();
        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        foreach (var (key, template, tables, expected) in plan)
        {
            var fact = keyed[key];
            var sources = new Dictionary<string, string>(StringComparer.Ordinal) { [template] = File.ReadAllText(Path.Combine(templates, template)) };
            if (tables is not null) sources[tables] = File.ReadAllText(Path.Combine(templates, tables));
            var identity = key switch { "TwoReaders#1" => Other, "AbsentBinding" => Items with { Database = "OtherDatabase" }, _ => Items };
            var input = Input(sources, revision: fact.CommitSha, repo: fact.Repo);
            var export = SqlServerDdlTableResultEvidence.Produce(input, sources);
            var report = SqlServerDdlTableResultEvidence.Compare(SqlServerSchemaEvidence.Json(export), input, sources, Bindings(fact, identity), calls, fact.Repo, fact.CommitSha);
            var statuses = report.Comparison.Comparisons.Where(r => r.FactId == fact.FactId).Select(r => r.Status).ToArray();
            Assert.True(statuses.SequenceEqual(expected), $"{language}:{key}:{template}:{tables}:" + SqlServerSchemaEvidence.Json(report.Comparison.Comparisons.Where(r => r.FactId == fact.FactId)));
            foreach (var s in statuses) counts[s]++;
            Assert.All(report.Comparison.Comparisons.Where(r => r.FactId != fact.FactId), r => Assert.True(r.Status == U && r.Reasons.Contains("binding-missing")));
        }
        Assert.Equal(64, plan.Length);
        Assert.Equal((22, 5, 44), (counts[M], counts[X], counts[U]));

        // Native CLI: produce, compare, byte-identical repeat, no overwrite, wrong-route refusals.
        var good = keyed["GetOrdinalMissing"];
        var writer = new StringWriter(); var error = new StringWriter();
        var sourceRoot = Path.Combine(temp.Path, "sql");
        Directory.CreateDirectory(sourceRoot);
        foreach (var file in new[] { "base.sql", "tables.sql" }) File.Copy(Path.Combine(templates, file), Path.Combine(sourceRoot, file));
        var cliSources = new[] { "base.sql", "tables.sql" }.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(sourceRoot, f)), StringComparer.Ordinal);
        var inputPath = Path.Combine(temp.Path, "input.json");
        File.WriteAllText(inputPath, SqlServerSchemaEvidence.Json(new SqlServerDdlInput(SqlServerDdlTableResultEvidence.InputVersion, good.Repo, good.CommitSha,
            Items.Server, Items.Database, true, [.. cliSources.Keys.Select(f => new SqlServerDdlFile(f, SqlServerSchemaEvidence.Hash(File.ReadAllBytes(Path.Combine(sourceRoot, f)))))])));
        var schemaPath = Path.Combine(temp.Path, "schema.json");
        string[] produce = ["sqlserver-ddl-table-result", "produce", "--input", inputPath, "--source-root", sourceRoot, "--out", schemaPath];
        Assert.True(await TraceMapCommand.RunAsync(produce, writer, error) == 0, error.ToString());
        var original = File.ReadAllBytes(schemaPath);
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(produce, writer, error));
        Assert.Equal(original, File.ReadAllBytes(schemaPath));
        var bindingsPath = Path.Combine(temp.Path, "bindings.json"); File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(Bindings(good)));
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var command = new[] { "sqlserver-ddl-table-result", "compare", "--input", inputPath, "--source-root", sourceRoot, "--schema", schemaPath,
            "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(temp.Path, "report") };
        Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
        var first = command[^1];
        command[^1] = Path.Combine(temp.Path, "repeat");
        Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
        foreach (var file in new[] { "result-column-comparison.json", "report.md" })
            Assert.Equal(File.ReadAllBytes(Path.Combine(first, file)), File.ReadAllBytes(Path.Combine(command[^1], file)));
        var json = File.ReadAllText(Path.Combine(first, "result-column-comparison.json"));
        Assert.Equal(SqlServerSchemaEvidence.Json(SqlServerDdlTableResultEvidence.Compare(File.ReadAllText(schemaPath), File.ReadAllText(inputPath), cliSources,
            Bindings(good), calls, good.Repo, good.CommitSha)) + "\n", json);
        var readback = SqlServerSchemaEvidence.Read<SqlServerDdlTableResultComparisonReport>(json);
        Assert.Equal([X, X], readback.Comparison.Comparisons.Where(r => r.FactId == good.FactId).Select(r => r.Status));
        var markdown = File.ReadAllText(Path.Combine(first, "report.md"));
        Assert.Contains("missing-column:Price", markdown);
        Assert.Contains("binding-missing", markdown);
        Assert.Contains("0 Id ← Id", markdown);
        // The constant result route and the parameter route refuse the table manifest and export without publishing.
        foreach (var route in new[] { "sqlserver-ddl-result", "sqlserver-ddl" })
        {
            var refused = Path.Combine(temp.Path, route + "-refused");
            Assert.NotEqual(0, await TraceMapCommand.RunAsync([route, "compare", "--input", inputPath, "--source-root", sourceRoot, "--schema", schemaPath,
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", refused], writer, error));
            Assert.False(Directory.Exists(refused));
        }
        var help = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["sqlserver-ddl-table-result", "--help"], help, error));
        Assert.Contains("sqlserver-ddl-table-result produce", help.ToString());
    }
}
