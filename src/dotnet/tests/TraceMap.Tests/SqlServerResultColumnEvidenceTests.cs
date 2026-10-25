using System.Text;
using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlServerResultColumnEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    private static readonly SqlServerObjectIdentity Items = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItems");
    private static readonly SqlServerObjectIdentity Other = Items with { Procedure = "ReadOther" };

    // Independently authored expectations (ordered statuses of every row of the one selected execution).
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

    private static SqlServerResultColumn Column(string name, int ordinal, string? type = null) => new(name, ordinal, 1, type);
    private static SqlServerResultShape Shape(SqlServerObjectIdentity identity, params SqlServerResultColumn[] columns)
        => new(identity, "declarations.json", 1, 1, "first", "complete", null, columns);
    private static SqlServerResultShape ItemsShape => Shape(Items, Column("Id", 0, "int"), Column("Name", 1), Column("Total", 2, "decimal(10,2)"));
    private static SqlServerResultDeclarations Declarations(string repo = "synthetic", string revision = Revision, params SqlServerResultShape[] shapes)
        => new("sqlserver-result-declarations/1", repo, revision, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            shapes.Length == 0 ? [ItemsShape, Shape(Other, Column("Title", 0))] : shapes);
    private static SqlServerReaderState State(params SqlServerReaderAccess[] accesses) => new("sqlserver-reader/1", "bounded", null, "StoredProcedure",
        FactFactory.Hash("dbo.ReadItems", 64), 4, 4, "none", accesses);
    private static SqlServerReaderAccess Name(string? name, string? gap = null) => new("name-indexer", "Item", name, null, null, 5, 5, gap);
    private static SqlServerReaderAccess Ordinal(int? ordinal, string member = "GetInt32", string? gap = null) => new("ordinal-getter", member, null, ordinal, ordinal is null ? null : "constant", 5, 5, gap);
    private static CodeFact Fact(SqlServerReaderState? state, string id = "fact", string? version = null) => new(id, "scan", "synthetic", Revision, null,
        FactTypes.DatabaseOperationCandidate, RuleIds.DatabaseOperationCallPattern, EvidenceTiers.Tier1Semantic, "Example.Read()", null, null,
        new("Calls.cs", 4, 4, null, "CSharpSemanticExtractor", version ?? ScannerVersions.CSharpSemanticExtractor),
        state is null ? new Dictionary<string, string>() : new Dictionary<string, string> { ["sqlServerReaderState"] = SqlServerSchemaEvidence.Json(state) });
    private static SqlServerResultBindings Bindings(CodeFact fact, SqlServerObjectIdentity? identity = null)
    {
        var i = identity ?? Items;
        return new("sqlserver-result-bindings/1", fact.Repo, fact.CommitSha, [new(fact.FactId, i.Schema + "." + i.Procedure, i, SqlServerResultEvidence.ReaderDigest(fact))]);
    }
    private static SqlServerResultComparisonReport Compare(CodeFact fact, SqlServerResultDeclarations? declarations = null, SqlServerResultBindings? bindings = null)
        => SqlServerResultEvidence.Compare(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(declarations ?? Declarations())),
            bindings ?? Bindings(fact), [fact], fact.Repo, fact.CommitSha);
    private static string[] Statuses(SqlServerResultComparisonReport report, string factId = "fact")
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
    [InlineData("case-only-access", U)]
    [InlineData("case-only-declared", U)]
    [InlineData("duplicate-declared", U)]
    [InlineData("dynamic-name", U)]
    [InlineData("ordinal-valid", M)]
    [InlineData("ordinal-last", M)]
    [InlineData("ordinal-out-of-range", X)]
    [InlineData("ordinal-negative", X)]
    [InlineData("ordinal-with-duplicate-names", M)]
    [InlineData("incomplete-missing", U)]
    [InlineData("incomplete-ordinal", U)]
    public void Column_identity_and_ordinal_bounds_only_with_unknown_precedence(string mode, string expected)
    {
        var access = mode switch
        {
            "missing" or "incomplete-missing" => Name("Title"), "case-only-access" => Name("name"), "dynamic-name" => Name(null, "dynamic-column-name"),
            "ordinal-valid" => Ordinal(0), "ordinal-last" => Ordinal(2), "ordinal-out-of-range" or "incomplete-ordinal" => Ordinal(3), "ordinal-negative" => Ordinal(-1),
            "ordinal-with-duplicate-names" => Ordinal(2), _ => Name("Name")
        };
        var shape = mode switch
        {
            "case-only-declared" => Shape(Items, Column("Id", 0), Column("Name", 1), Column("name", 2)),
            "duplicate-declared" or "ordinal-with-duplicate-names" => Shape(Items, Column("Id", 0), Column("Name", 1), Column("Name", 2)),
            "incomplete-missing" or "incomplete-ordinal" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
            _ => ItemsShape
        };
        var report = Compare(Fact(State(access)), Declarations(shapes: [shape]));
        var row = Assert.Single(report.Comparisons);
        Assert.Equal(expected, row.Status);
        Assert.Equal(expected == U ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, row.Tier);
        Assert.Equal(EvidenceTiers.Tier1Semantic, row.SourceTier);
        Assert.Equal(SqlServerResultEvidence.RuleId, row.RuleId);
        Assert.Equal("partial", report.Coverage);
        Assert.Equal("no-aggregate-clean-verdict", report.Verdict);
        if (mode == "missing") Assert.Equal(["missing-column:Title"], row.Reasons);
        if (mode == "ordinal-out-of-range") Assert.Equal(["ordinal-out-of-range:3"], row.Reasons);
        if (mode.StartsWith("incomplete", StringComparison.Ordinal)) Assert.Equal(["result-shape-unknown:authored-incomplete"], row.Reasons);
        if (mode == "match")
        {
            Assert.Equal("Name", row.Column!.Name);
            Assert.Empty(row.UncomparedFacets);
        }
        if (mode == "ordinal-valid")
        {
            // A typed getter and declared type remain visible facets, never a type-contract verdict.
            Assert.Contains("getter-type-not-compared:GetInt32", row.UncomparedFacets);
            Assert.Contains("declared-type-not-compared:int", row.UncomparedFacets);
        }
        var markdown = SqlServerResultReportWriter.Build(report);
        Assert.Contains("No aggregate clean verdict", markdown);
        foreach (var reason in row.Reasons) Assert.Contains(reason, markdown);
    }

    [Theory]
    [InlineData("no-state")]
    [InlineData("unknown-state")]
    [InlineData("text")]
    [InlineData("intent-unavailable")]
    [InlineData("unbound")]
    [InlineData("exact-identity-absent")]
    [InlineData("case-identity")]
    [InlineData("command-conflict")]
    [InlineData("extractor-version")]
    [InlineData("no-access")]
    [InlineData("invalid-access-span")]
    public void Call_level_gaps_stay_one_unknown_row_and_never_borrow_shapes(string mode)
    {
        var state = State(Name("Name"));
        state = mode switch
        {
            "unknown-state" => state with { Coverage = "unknown", Gap = "reader-alias-or-escape", Accesses = [] },
            "text" => state with { CommandType = "Text" }, "intent-unavailable" => state with { CommandType = null },
            "command-conflict" => state with { CommandTextHash = FactFactory.Hash("dbo.ReadOther", 64) },
            "no-access" => state with { Accesses = [] }, "invalid-access-span" => state with { Accesses = [Name("Name") with { Line = 1 }] },
            _ => state
        };
        var fact = Fact(mode == "no-state" ? null : state, version: mode == "extractor-version" ? "csharp-semantic/0.22.9" : null);
        var bindings = mode switch
        {
            "unbound" => Bindings(fact) with { Calls = [] },
            "exact-identity-absent" => Bindings(fact, Items with { Database = "OtherDatabase" }),
            "case-identity" => Bindings(fact, Items with { Procedure = "readitems" }),
            _ => Bindings(fact)
        };
        var row = Assert.Single(Compare(fact, bindings: bindings).Comparisons);
        Assert.Equal(U, row.Status);
        Assert.Null(row.AccessIndex);
        Assert.Contains(mode switch
        {
            "no-state" => "reader-state-unavailable", "unknown-state" => "reader-alias-or-escape", "text" => "text-command-outside-scope",
            "intent-unavailable" => "stored-procedure-intent-unavailable", "unbound" => "binding-missing",
            "exact-identity-absent" or "case-identity" => "exact-result-shape-unavailable", "command-conflict" => "command-binding-conflict",
            "extractor-version" => "unsupported-source-extractor-or-tier", "no-access" => "no-bounded-column-access", _ => "invalid-reader-state"
        }, row.Reasons);
    }

    [Fact]
    public void Bindings_are_exact_reader_digests_and_older_parameter_contracts_are_refused()
    {
        var fact = Fact(State(Name("Name")));
        var good = Bindings(fact);
        foreach (var bad in new[] { good with { Version = "sqlserver-bindings/1" }, good with { Revision = new('2', 40) }, good with { Repo = "other" },
            good with { Calls = [good.Calls[0] with { ReaderEvidenceSha256 = new('0', 64) }] },
            good with { Calls = [good.Calls[0] with { ReaderEvidenceSha256 = SqlServerSchemaEvidence.CallDigest(fact) }] },
            good with { Calls = [good.Calls[0], good.Calls[0]] }, good with { Calls = [good.Calls[0] with { FactId = "absent" }] },
            good with { Calls = [good.Calls[0] with { CommandText = "ReadItems" }] },
            good with { Provenance = new("bad", new('0', 64)) } })
            Assert.Throws<ArgumentException>(() => Compare(fact, bindings: bad));
        // A changed reader state invalidates the earlier digest (mutated source/artifact bytes).
        var mutated = Fact(State(Name("Title")));
        Assert.Throws<ArgumentException>(() => Compare(mutated, bindings: good));
        // Parameter-only receipts never become result-shape evidence and vice versa.
        var parameterBindings = JsonSerializer.Serialize(new { version = "sqlserver-bindings/1", repo = "synthetic", revision = Revision,
            calls = new[] { new { factId = "fact", commandText = "dbo.ReadItems", identity = Items, sourceEvidenceSha256 = new string('0', 64) } } },
            SqlServerSchemaEvidence.JsonOptions);
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Read<SqlServerResultBindings>(parameterBindings));
        var parameterDeclarations = File.ReadAllText(Path.Combine(RepoRoot(), "samples/sqlserver-parameter-evidence/declarations-base.json"));
        Assert.ThrowsAny<Exception>(() => SqlServerResultEvidence.Produce(parameterDeclarations));
        Assert.ThrowsAny<Exception>(() => SqlServerSchemaEvidence.Produce(SqlServerSchemaEvidence.Json(Declarations())));
    }

    [Fact]
    public void Reports_are_deterministic_and_hash_only_projected_public_inputs()
    {
        var fact = Fact(State(Name("Name"), Ordinal(9)));
        var report = Compare(fact);
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Compare(fact)));
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(SqlServerSchemaEvidence.Read<SqlServerResultComparisonReport>(SqlServerSchemaEvidence.Json(report))));
        Assert.Equal(SqlServerSchemaEvidence.GeneratorSha256, report.GeneratorSha256);
        Assert.Equal([M, X], Statuses(report));
        var withPrivate = fact with { Properties = new Dictionary<string, string>(fact.Properties) { ["rawSql"] = "PRIVATE", ["connectionString"] = "SECRET",
            ["sqlServerParameterState"] = "{}" } };
        var projected = Compare(withPrivate);
        Assert.Equal(report.BoundedInputSha256, projected.BoundedInputSha256);
        Assert.DoesNotContain("PRIVATE", SqlServerSchemaEvidence.Json(projected) + SqlServerResultReportWriter.Build(projected));
        Assert.NotEqual(report.BoundedInputSha256, Compare(fact, Declarations(shapes: [ItemsShape])).BoundedInputSha256);
        Assert.DoesNotContain("scannedAt", SqlServerSchemaEvidence.Json(report));
    }

    // ---------------------------------------------------------------- JSON declaration admission

    [Theory]
    [InlineData("version")]
    [InlineData("generator")]
    [InlineData("input-hash")]
    [InlineData("extractor")]
    [InlineData("tier")]
    [InlineData("rule")]
    [InlineData("payload")]
    [InlineData("substitution")]
    [InlineData("revision")]
    [InlineData("repo")]
    public void Export_admission_refuses_tampering_and_unexpected_identity(string mode)
    {
        var d = Declarations();
        var e = SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d));
        e = mode switch
        {
            "version" => e with { Version = "sqlserver-schema/1" }, "generator" => e with { GeneratorSha256 = new('0', 64) },
            "input-hash" => e with { BoundedInputSha256 = new('0', 64) }, "extractor" => e with { ExtractorVersion = "old" },
            "tier" => e with { Tier = EvidenceTiers.Tier1Semantic }, "rule" => e with { RuleId = "wrong" },
            "payload" => e with { Declarations = d with { Origin = "runtime" } }, _ => e
        };
        if (mode == "substitution") d = d with { Shapes = [ItemsShape with { Columns = [Column("Id", 0), Column("Title", 1)] }] };
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Admit(SqlServerSchemaEvidence.Json(e), SqlServerSchemaEvidence.Json(d),
            mode == "repo" ? "other" : "synthetic", mode == "revision" ? new('2', 40) : Revision));
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Compare(e with { Declarations = e.Declarations with { Revision = new('3', 40) } },
            Bindings(Fact(State(Name("Name")))), [Fact(State(Name("Name")))], "synthetic", Revision));
    }

    [Theory]
    [InlineData("duplicate-identity")]
    [InlineData("invalid-identity")]
    [InlineData("ordinal-gap")]
    [InlineData("line-outside-shape")]
    [InlineData("invalid-name")]
    [InlineData("complete-with-reason")]
    [InlineData("complete-without-columns")]
    [InlineData("unknown-without-reason")]
    [InlineData("unknown-bad-reason")]
    [InlineData("bad-state")]
    [InlineData("second-result-set")]
    [InlineData("declared-type")]
    [InlineData("null-column")]
    [InlineData("origin")]
    [InlineData("policy")]
    [InlineData("bad-revision")]
    [InlineData("provenance")]
    [InlineData("no-shapes")]
    public void Malformed_or_ambiguous_declarations_are_not_admitted(string mode)
    {
        var s = ItemsShape;
        var d = mode switch
        {
            "duplicate-identity" => Declarations(shapes: [s, s with { Columns = [Column("Other", 0)] }]),
            "invalid-identity" => Declarations(shapes: [s with { Identity = Items with { Schema = "odd schema" } }]),
            "ordinal-gap" => Declarations(shapes: [s with { Columns = [Column("Id", 0), Column("Name", 2)] }]),
            "line-outside-shape" => Declarations(shapes: [s with { Columns = [Column("Id", 0) with { Line = 2 }] }]),
            "invalid-name" => Declarations(shapes: [s with { Columns = [Column("Unit Price", 0)] }]),
            "complete-with-reason" => Declarations(shapes: [s with { UnknownReason = "x" }]),
            "complete-without-columns" => Declarations(shapes: [s with { Columns = [] }]),
            "unknown-without-reason" => Declarations(shapes: [s with { Completeness = "unknown" }]),
            "unknown-bad-reason" => Declarations(shapes: [s with { Completeness = "unknown", UnknownReason = "Not A Slug" }]),
            "bad-state" => Declarations(shapes: [s with { Completeness = "partial" }]),
            "second-result-set" => Declarations(shapes: [s with { ResultSet = "second" }]),
            "declared-type" => Declarations(shapes: [s with { Columns = [Column("Id", 0, "int; DROP")] }]),
            "null-column" => Declarations(shapes: [s with { Columns = [null!] }]),
            "origin" => Declarations() with { Origin = "public-synthetic-tsql-ddl-result" },
            "policy" => Declarations() with { IdentifierPolicy = "case-insensitive" },
            "bad-revision" => Declarations() with { Revision = "HEAD" },
            "provenance" => Declarations() with { Provenance = new(new('0', 64), "short") },
            _ => Declarations() with { Shapes = [] }
        };
        Assert.Throws<ArgumentException>(() => SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(d)));
    }

    [Fact]
    public void Strict_json_reader_rejects_unknown_fields_duplicates_and_accepts_the_committed_sample()
    {
        var json = SqlServerSchemaEvidence.Json(Declarations());
        Assert.ThrowsAny<Exception>(() => SqlServerResultEvidence.Produce(json.Replace("\"repo\":", "\"extra\":1,\"repo\":", StringComparison.Ordinal)));
        Assert.ThrowsAny<Exception>(() => SqlServerResultEvidence.Produce(json.Replace("\"repo\":\"synthetic\"", "\"repo\":\"synthetic\",\"repo\":\"synthetic\"", StringComparison.Ordinal)));
        Assert.ThrowsAny<Exception>(() => SqlServerResultEvidence.Produce(new string(' ', SqlServerSchemaEvidence.MaxBytes + 1)));
        var sample = SqlServerResultEvidence.Produce(File.ReadAllText(Path.Combine(RepoRoot(), "samples/sqlserver-result-column-evidence/result-declarations-base.json")));
        Assert.Equal(2, sample.Declarations.Shapes.Count);
        Assert.Equal(SqlServerSchemaEvidence.Digest(sample.Declarations), sample.BoundedInputSha256);
        Assert.Equal(SqlServerResultEvidence.SchemaRuleId, sample.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, sample.Tier);
    }

    // ---------------------------------------------------------------- DDL projection origin

    private static Dictionary<string, string> Sources(string sql) => new(StringComparer.Ordinal) { ["public.sql"] = sql };
    private static string Input(IReadOnlyDictionary<string, string> sources, string version = SqlServerDdlResultEvidence.InputVersion, string repo = "synthetic", string revision = Revision)
        => SqlServerSchemaEvidence.Json(new SqlServerDdlInput(version, repo, revision, Items.Server, Items.Database, true,
            sources.Select(f => new SqlServerDdlFile(f.Key, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(f.Value)))).ToArray()));
    private static SqlServerDdlResultExport Ddl(string sql) => SqlServerDdlResultEvidence.Produce(Input(Sources(sql)), Sources(sql));

    [Theory]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT CAST(1 AS int) AS Id, N'synthetic' AS Name, CAST(2.5 AS decimal(10,2)) AS Total;")]
    [InlineData("CREATE OR ALTER PROC [dbo].[ReadItems]\nAS\nBEGIN\n  SELECT 1 AS [Id],\n  Name = N'synthetic',\n  NULL AS \"Total\";\nEND")]
    [InlineData("ALTER PROCEDURE dbo.ReadItems @Filter int AS BEGIN SELECT 0x01 AS Id, 'x' AS Name, CAST(NULL AS varchar(MAX)) AS Total END")]
    public void Supported_projection_is_complete_with_spans_facets_and_no_body_text(string sql)
    {
        var export = Ddl(sql);
        var shape = Assert.Single(export.Declarations.Shapes);
        Assert.Equal(Items, shape.Identity);
        Assert.Equal("complete", shape.Completeness);
        Assert.Equal(["Id", "Name", "Total"], shape.Columns.Select(c => c.Name));
        Assert.Equal([0, 1, 2], shape.Columns.Select(c => c.Ordinal));
        Assert.All(shape.Columns, c => Assert.InRange(c.Line, shape.StartLine, shape.EndLine));
        Assert.Equal("public-synthetic-tsql-ddl-result", export.Declarations.Origin);
        Assert.Equal(SqlServerDdlResultEvidence.RuleId, export.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, export.Tier);
        Assert.Equal(SqlServerDdlEvidence.ParserVersion, export.ParserVersion);
        Assert.Equal("column-type-facets-not-compared", Assert.Single(export.Unsupported).Kind);
        var json = SqlServerSchemaEvidence.Json(export);
        Assert.DoesNotContain("synthetic'", json);
        Assert.DoesNotContain("SELECT", json);
        Assert.DoesNotContain("2.5", json);
        if (sql.StartsWith("CREATE PROCEDURE", StringComparison.Ordinal)) Assert.Equal(["int", null, "decimal(10,2)"], shape.Columns.Select(c => c.DeclaredType));
        if (sql.StartsWith("CREATE OR ALTER", StringComparison.Ordinal)) Assert.Equal([4, 5, 6], shape.Columns.Select(c => c.Line));
    }

    [Theory]
    [InlineData("IF 1 = 1 SELECT 1 AS Id;", "conditional-body-unsupported")]
    [InlineData("BEGIN IF 1 = 1 BEGIN SELECT 1 AS Id; END END", "conditional-body-unsupported")]
    [InlineData("BEGIN BEGIN SELECT 1 AS Id; END END", "nested-block-unsupported")]
    [InlineData("EXEC sys.sp_executesql N'SELECT 1 AS Id';", "exec-or-dynamic-sql-unsupported")]
    [InlineData("EXEC dbo.Other;", "exec-or-dynamic-sql-unsupported")]
    [InlineData("BEGIN SELECT 1 AS Id; SELECT 2 AS Extra; END", "body-statement-count-unsupported")]
    [InlineData("SELECT 1 AS Id; SELECT 2 AS Extra;", "body-statement-count-unsupported")]
    [InlineData("SET NOCOUNT ON; SELECT 1 AS Id;", "body-statement-count-unsupported")]
    [InlineData("SELECT * FROM dbo.Items;", "select-star-unsupported")]
    [InlineData("SELECT Id FROM dbo.Items;", "from-dependency-unsupported")]
    [InlineData("SELECT i.Id FROM dbo.Items i JOIN dbo.Other o ON o.Id = i.Id;", "from-dependency-unsupported")]
    [InlineData("SELECT 1 AS Id UNION ALL SELECT 2;", "set-operation-unsupported")]
    [InlineData("SELECT @Name = N'x';", "select-assignment-unsupported")]
    [InlineData("SELECT 1, 2 AS Name;", "explicit-alias-required")]
    [InlineData("SELECT 1 AS 'Id';", "explicit-alias-required")]
    [InlineData("SELECT 1 AS [Unit Price];", "alias-outside-identifier-vocabulary")]
    [InlineData("SELECT 1 AS Id, 2 AS id;", "duplicate-or-case-only-alias-unsupported")]
    [InlineData("SELECT 1 AS Id, 2 AS Id;", "duplicate-or-case-only-alias-unsupported")]
    [InlineData("SELECT -1 AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT @Name AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT (SELECT 1) AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT CONVERT(int, 1) AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT CAST(N'x' AS nvarchar(5)) COLLATE Latin1_General_CI_AS AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT CAST(1 AS dbo.Alias) AS Id;", "non-constant-projection-unsupported")]
    [InlineData("SELECT TOP (1) 1 AS Id;", "select-clause-unsupported")]
    [InlineData("SELECT DISTINCT 1 AS Id;", "select-clause-unsupported")]
    [InlineData("SELECT 1 AS Id WHERE 1 = 1;", "select-clause-unsupported")]
    [InlineData("WITH c AS (SELECT 1 AS Id) SELECT 1 AS Id;", "select-clause-unsupported")]
    [InlineData("SELECT 1 AS Id INTO #T;", "select-clause-unsupported")]
    [InlineData("SELECT 1 AS Id FOR XML PATH;", "select-clause-unsupported")]
    [InlineData("RETURN 7;", "return-channel-not-result-set")]
    [InlineData("WHILE 1 = 0 SELECT 1 AS Id;", "loop-body-unsupported")]
    [InlineData("BEGIN TRY SELECT 1 AS Id; END TRY BEGIN CATCH SELECT 2 AS Id; END CATCH", "try-catch-body-unsupported")]
    [InlineData("DECLARE @x int;", "body-statement-unsupported")]
    public void Unsupported_bodies_are_explicit_unknown_shapes_never_partial_columns(string body, string kind)
    {
        var export = Ddl("CREATE PROCEDURE dbo.ReadItems @Name nvarchar(20) OUTPUT AS " + body);
        var shape = Assert.Single(export.Declarations.Shapes);
        Assert.Equal("unknown", shape.Completeness);
        Assert.Equal(kind, shape.UnknownReason);
        Assert.Empty(shape.Columns);
        var gap = Assert.Single(export.Unsupported);
        Assert.Equal(kind, gap.Kind);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.Tier);
        // Parameter admission of the same procedure is unchanged by the result-shape path.
        var sql = "CREATE PROCEDURE dbo.ReadItems @Name nvarchar(20) OUTPUT AS " + body;
        var parameters = SqlServerDdlEvidence.Produce(Input(Sources(sql), SqlServerDdlEvidence.InputVersion), Sources(sql));
        Assert.Equal("InputOutput", Assert.Single(Assert.Single(parameters.Declarations.Procedures).Parameters).Direction);
        var fact = Fact(State(Name("Name")));
        var row = Assert.Single(DdlCompare(sql, fact).Comparison.Comparisons);
        Assert.Equal(U, row.Status);
        Assert.Equal(["result-shape-unknown:" + kind], row.Reasons);
    }

    private static SqlServerDdlResultComparisonReport DdlCompare(string sql, CodeFact fact, SqlServerResultBindings? bindings = null)
        => SqlServerDdlResultEvidence.Compare(SqlServerSchemaEvidence.Json(Ddl(sql)), Input(Sources(sql)), Sources(sql), bindings ?? Bindings(fact), [fact], fact.Repo, fact.CommitSha);

    [Theory]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT FROM;", "malformed-sql")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id; -- :r other.sql", "sqlcmd-unresolved")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT N'$(Owner)' AS Id;", "sqlcmd-unresolved")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id;\nGO\nCREATE PROCEDURE DBO.readitems AS SELECT 2 AS Id;", "ambiguous-object")]
    [InlineData("CREATE PROCEDURE ReadItems AS SELECT 1 AS Id;", "explicit-bounded-schema-required")]
    [InlineData("CREATE PROCEDURE dbo.ReadItems @Name xml AS SELECT 1 AS Id;", "alias-clr-or-table-type-unsupported")]
    [InlineData("CREATE TABLE dbo.Items (Id int);", "top-level-statement-unsupported")]
    public void Whole_input_failures_are_preserved(string sql, string reason)
        => Assert.Contains("ddl-" + reason, Assert.Throws<ArgumentException>(() => Ddl(sql)).Message);

    [Fact]
    public void Ddl_admission_bounds_versions_and_substitution_fail_closed()
    {
        const string sql = "CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id, N'x' AS Name, 2 AS Total;";
        var export = Ddl(sql);
        var input = Input(Sources(sql));
        Assert.Equal(SqlServerSchemaEvidence.Json(export), SqlServerSchemaEvidence.Json(Ddl(sql)));
        Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerDdlResultEvidence.ReadInput(input)), export.BoundedInputSha256);
        foreach (var bad in new[] { export with { Version = "sqlserver-ddl-schema/1" }, export with { GeneratorSha256 = new('0', 64) },
            export with { ParserSha256 = new('0', 64) }, export with { ExtractorVersion = "sqlserver-ddl-parameters/1" }, export with { Tier = EvidenceTiers.Tier1Semantic },
            export with { Unsupported = [] }, export with { Declarations = export.Declarations with { Origin = "independent-synthetic-result-declarations" } },
            export with { Declarations = export.Declarations with { Shapes = [export.Declarations.Shapes[0] with { Columns = [Column("Id", 0)] }] } } })
            Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(bad), input, Sources(sql), "synthetic", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), input, Sources(sql + " "), "synthetic", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(export), input, Sources(sql), "synthetic", new('2', 40)));
        // The opt-in route requires its own manifest version; parameter manifests and exports never reinterpret.
        Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Produce(Input(Sources(sql), SqlServerDdlEvidence.InputVersion), Sources(sql)));
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(input, Sources(sql)));
        var parameterExport = SqlServerDdlEvidence.Produce(Input(Sources(sql), SqlServerDdlEvidence.InputVersion), Sources(sql));
        Assert.ThrowsAny<Exception>(() => SqlServerDdlResultEvidence.Admit(SqlServerSchemaEvidence.Json(parameterExport), input, Sources(sql), "synthetic", Revision));
        var big = Enumerable.Range(0, 5).ToDictionary(i => $"f{i}.sql", i => $"CREATE PROC dbo.P{i} AS SELECT 1 AS Id;" + new string(' ', 220000));
        Assert.Contains("input-size-limit", Assert.Throws<ArgumentException>(() => SqlServerDdlResultEvidence.Produce(Input(big), big)).Message);
        Assert.Contains("token-limit", Assert.Throws<ArgumentException>(() =>
            Ddl("CREATE PROC dbo.P AS SELECT " + string.Join(',', Enumerable.Range(0, 17000).Select(i => $"1 AS C{i}")) + ";")).Message);
        var wide = "CREATE PROC dbo.P AS SELECT " + string.Join(',', Enumerable.Range(0, 1025).Select(i => $"1 AS C{i}")) + ";";
        Assert.Equal("column-limit", Assert.Single(Ddl(wide).Declarations.Shapes).UnknownReason);
        var bom = "\uFEFF" + sql;
        Assert.Equal("complete", Assert.Single(Ddl(bom).Declarations.Shapes).Completeness);
    }

    [Fact]
    public void Multiple_procedures_keep_independent_shapes_and_exact_bindings()
    {
        const string sql = "CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id, N'x' AS Name, 2 AS Total;\nGO\nCREATE PROCEDURE dbo.ReadOther AS IF 1 = 1 SELECT N'x' AS Title;\nGO\n";
        var export = Ddl(sql);
        Assert.Equal(["complete", "unknown"], export.Declarations.Shapes.Select(s => s.Completeness));
        var fact = Fact(State(Name("Name"), Name("Price")));
        Assert.Equal([M, X], DdlCompare(sql, fact).Comparison.Comparisons.Select(r => r.Status));
        var other = Fact(State(Name("Title")) with { CommandTextHash = FactFactory.Hash("dbo.ReadOther", 64) });
        var row = Assert.Single(DdlCompare(sql, other, Bindings(other, Other)).Comparison.Comparisons);
        Assert.Equal(["result-shape-unknown:conditional-body-unsupported"], row.Reasons);
        var report = DdlCompare(sql, fact);
        Assert.Equal("sqlserver-ddl-result-comparison/1", report.Version);
        Assert.Equal("sqlserver-ddl-result-column-comparison/1", report.Comparison.Version);
        var markdown = SqlServerResultReportWriter.Build(report);
        Assert.Contains("public.sql:1–1", markdown);
        Assert.Contains("conditional-body-unsupported", markdown);
        Assert.Contains("missing-column:Price", markdown);
        Assert.Contains("No aggregate clean verdict", markdown);
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(DdlCompare(sql, fact)));
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

    private static SqlServerReaderState[] Extract(string language, string body)
    {
        Microsoft.CodeAnalysis.Compilation compilation;
        Microsoft.CodeAnalysis.SyntaxTree tree;
        if (language == "cs")
        {
            tree = Microsoft.CodeAnalysis.CSharp.CSharpSyntaxTree.ParseText("#pragma warning disable CS0618\nusing System.Data; using System.Data.SqlClient; class Example { static SqlDataReader Field; static void Helper(object o) {} static object Run(string name, int index, bool flag) {\n" + body + "\n} }");
            compilation = Microsoft.CodeAnalysis.CSharp.CSharpCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.CSharp.CSharpCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary));
        }
        else
        {
            tree = Microsoft.CodeAnalysis.VisualBasic.VisualBasicSyntaxTree.ParseText("Imports System.Data\nImports System.Data.SqlClient\nModule Example\nDim Field As SqlDataReader\nSub Helper(o As Object)\nEnd Sub\nFunction Run(name As String, index As Integer, flag As Boolean) As Object\n" + body + "\nEnd Function\nEnd Module");
            compilation = Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilation.Create("Synthetic", [tree], References(), new Microsoft.CodeAnalysis.VisualBasic.VisualBasicCompilationOptions(Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary, optionInfer: true));
        }
        Assert.DoesNotContain(compilation.GetDiagnostics(), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Error);
        var model = compilation.GetSemanticModel(tree);
        return tree.GetRoot().DescendantNodes().Select(n => model.GetOperation(n)).OfType<Microsoft.CodeAnalysis.Operations.IInvocationOperation>()
            .Where(o => o.TargetMethod.Name.Contains("ExecuteReader", StringComparison.Ordinal)).Distinct()
            .OrderBy(o => o.Syntax.SpanStart).Select(o => { var p = new SortedDictionary<string, string>(); SqlServerReaderColumns.Add(p, o); return SqlServerSchemaEvidence.Read<SqlServerReaderState>(p["sqlServerReaderState"]); }).ToArray();
    }

    private const string CsCommand = "var command = new SqlCommand(\"dbo.ReadItems\"); command.CommandType = CommandType.StoredProcedure;\n";
    private const string VbCommand = "Dim command = New SqlCommand(\"dbo.ReadItems\")\ncommand.CommandType = CommandType.StoredProcedure\n";

    [Theory]
    [InlineData("cs", "using-declaration", "using var reader = command.ExecuteReader(); return reader[\"Name\"];", null)]
    [InlineData("cs", "connection", "var connection = new SqlConnection(); command.Connection = connection; var reader = command.ExecuteReader(); return reader[\"Name\"];", null)]
    [InlineData("cs", "close", "var reader = command.ExecuteReader(); var v = reader[\"Name\"]; reader.Close(); return v;", null)]
    [InlineData("cs", "interface-local", "IDataReader reader = command.ExecuteReader(); return reader[\"Name\"];", "direct-reader-local-required")]
    [InlineData("cs", "behavior", "var reader = command.ExecuteReader(CommandBehavior.SchemaOnly); return reader[\"Name\"];", "command-behavior-unsupported")]
    [InlineData("cs", "async", "var reader = command.ExecuteReaderAsync().Result; return reader[\"Name\"];", "async-or-apm-execution-unsupported")]
    [InlineData("cs", "inline", "return command.ExecuteReader()[\"Name\"];", "direct-reader-local-required")]
    [InlineData("cs", "prior-execution", "command.ExecuteNonQuery(); var reader = command.ExecuteReader(); return reader[\"Name\"];", "prior-execution")]
    [InlineData("cs", "command-helper", "Helper(command); var reader = command.ExecuteReader(); return reader[\"Name\"];", "command-alias-or-escape")]
    [InlineData("cs", "command-reassignment", "var reader = command.ExecuteReader(); command = null; return reader[\"Name\"];", "command-reassignment")]
    [InlineData("cs", "command-dispose", "command.Dispose(); var reader = command.ExecuteReader(); return reader[\"Name\"];", "unsupported-command-member")]
    [InlineData("cs", "dynamic-text", "command.CommandText = name; var reader = command.ExecuteReader(); return reader[\"Name\"];", null)]
    [InlineData("cs", "field-escape", "var reader = command.ExecuteReader(); Field = reader; return reader[\"Name\"];", "reader-alias-or-escape")]
    [InlineData("cs", "lambda", "var reader = command.ExecuteReader(); System.Func<object> f = () => reader[\"Name\"]; return f();", "unsupported-control-flow")]
    [InlineData("cs", "do-while", "var reader = command.ExecuteReader(); object v = null; do { v = reader[\"Name\"]; } while (reader.Read()); return v;", "unsupported-control-flow")]
    [InlineData("cs", "two-reads", "var reader = command.ExecuteReader(); if (reader.Read()) { reader.Read(); } return reader[\"Name\"];", "multiple-read-calls")]
    [InlineData("cs", "read-outside-guard", "var reader = command.ExecuteReader(); var ok = reader.Read(); return reader[\"Name\"];", "read-outside-guard")]
    [InlineData("cs", "has-rows", "var reader = command.ExecuteReader(); return reader.HasRows;", "unsupported-reader-member")]
    [InlineData("cs", "field-count", "var reader = command.ExecuteReader(); return reader.GetInt32(reader.FieldCount - 1);", "unsupported-reader-member")]
    [InlineData("cs", "read-async", "var reader = command.ExecuteReader(); return reader.ReadAsync().Result;", "unsupported-reader-member")]
    [InlineData("cs", "ternary", "var reader = command.ExecuteReader(); return flag ? reader[\"Name\"] : null;", "unsupported-control-flow")]
    [InlineData("cs", "initializer", "var c2 = new SqlCommand(\"dbo.ReadItems\") { CommandType = CommandType.StoredProcedure }; var reader = c2.ExecuteReader(); return reader[\"Name\"];", "implicit-receiver-unsupported")]
    [InlineData("cs", "parameter-command", "var reader = Make().ExecuteReader(); return reader[\"Name\"];", "fresh-command-local-required")]
    [InlineData("cs", "ordinal-indexer", "var reader = command.ExecuteReader(); return reader[1];", null)]
    [InlineData("vb", "as-new", "Dim c2 As New SqlCommand(\"dbo.ReadItems\")\nc2.CommandType = CommandType.StoredProcedure\nDim reader = c2.ExecuteReader()\nReturn reader(\"Name\")", null)]
    [InlineData("vb", "using-block", "Using reader = command.ExecuteReader()\nReturn reader(\"Name\")\nEnd Using\nReturn Nothing", null)]
    [InlineData("vb", "bang", "Dim reader = command.ExecuteReader()\nReturn reader!Name", null)]
    [InlineData("vb", "do-while", "Dim reader = command.ExecuteReader()\nDim v As Object = Nothing\nDo While reader.Read()\nv = reader(\"Name\")\nLoop\nReturn v", null)]
    [InlineData("vb", "do-until", "Dim reader = command.ExecuteReader()\nDim v As Object = Nothing\nDo Until reader.Read()\nv = reader(\"Name\")\nLoop\nReturn v", "unsupported-control-flow")]
    [InlineData("vb", "with", "Dim reader = command.ExecuteReader()\nWith reader\nReturn .Item(\"Name\")\nEnd With", "implicit-receiver-unsupported")]
    [InlineData("vb", "helper", "Dim reader = command.ExecuteReader()\nHelper(reader)\nReturn reader(\"Name\")", "reader-alias-or-escape")]
    [InlineData("vb", "field-escape", "Dim reader = command.ExecuteReader()\nField = reader\nReturn reader(\"Name\")", "reader-alias-or-escape")]
    [InlineData("vb", "next-result", "Dim reader = command.ExecuteReader()\nreader.NextResult()\nReturn reader(\"Name\")", "next-result-unsupported")]
    [InlineData("vb", "lambda", "Dim reader = command.ExecuteReader()\nDim f As System.Func(Of Object) = Function() reader(\"Name\")\nReturn f()", "unsupported-control-flow")]
    public void Extractor_supports_only_the_bounded_subset_and_refuses_aliases_escapes_and_control_flow(string language, string shape, string body, string? gap)
    {
        var prefix = language == "cs" ? CsCommand : VbCommand;
        if (shape == "parameter-command") body = body.Replace("Make()", "new SqlCommand(\"dbo.ReadItems\")", StringComparison.Ordinal);
        var state = Extract(language, prefix + body)[0];
        Assert.True(gap is null ? state.Coverage == "bounded" : state.Coverage == "unknown" && state.Gap == gap, shape + ": " + SqlServerSchemaEvidence.Json(state));
        if (gap is not null) Assert.Empty(state.Accesses);
        if (shape == "dynamic-text") Assert.Null(state.CommandTextHash);
        else if (gap is null)
        {
            Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), state.CommandTextHash);
            Assert.Equal("StoredProcedure", state.CommandType);
            var access = Assert.Single(state.Accesses);
            if (shape == "ordinal-indexer") Assert.Equal(("ordinal-indexer", (int?)1, "constant"), (access.Kind, access.Ordinal, access.OrdinalSource));
            else Assert.Equal("Name", access.Name);
        }
        if (shape == "do-while" && gap is null) Assert.Equal("while", state.ReadGuard);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Two_readers_and_ordinals_bind_to_their_exact_reader_instance(string language)
    {
        var body = language == "cs"
            ? CsCommand + "var other = new SqlCommand(\"dbo.ReadOther\"); other.CommandType = CommandType.StoredProcedure;\nvar first = command.ExecuteReader(); var second = other.ExecuteReader();\nvar ordinal = second.GetOrdinal(\"Title\");\nvar a = first.GetString(ordinal); var b = first[\"Name\"]; var c = second.GetString(ordinal); return first.GetString(first.GetOrdinal(\"Id\"));"
            : VbCommand + "Dim other = New SqlCommand(\"dbo.ReadOther\")\nother.CommandType = CommandType.StoredProcedure\nDim first = command.ExecuteReader()\nDim second = other.ExecuteReader()\nDim ordinal = second.GetOrdinal(\"Title\")\nDim a = first.GetString(ordinal)\nDim b = first(\"Name\")\nDim c = second.GetString(ordinal)\nReturn first.GetString(first.GetOrdinal(\"Id\"))";
        var states = Extract(language, body);
        Assert.Equal(2, states.Length);
        var first = states[0];
        var second = states[1];
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), first.CommandTextHash);
        Assert.Equal(FactFactory.Hash("dbo.ReadOther", 64), second.CommandTextHash);
        // The ordinal from the other reader never becomes this reader's column evidence.
        Assert.Equal(["ordinal-from-other-reader", null, null, null], first.Accesses.Select(a => a.Gap));
        Assert.Equal([null, "Name", "Id", "Id"], first.Accesses.Select(a => a.Name));
        Assert.Equal(["Title", "Title"], second.Accesses.Select(a => a.Name));
        Assert.Equal(["get-ordinal", "ordinal-getter"], second.Accesses.Select(a => a.Kind));
        Assert.Equal("get-ordinal-local", second.Accesses[1].OrdinalSource);
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Work_bounds_and_identifier_vocabulary_fail_closed(string language)
    {
        var accesses = language == "cs"
            ? string.Concat(Enumerable.Range(0, 257).Select(i => $"var v{i} = reader.GetValue({i % 3});\n")) + "return null;"
            : string.Concat(Enumerable.Range(0, 257).Select(i => $"Dim v{i} = reader.GetValue({i % 3})\n")) + "Return Nothing";
        var reader = language == "cs" ? "var reader = command.ExecuteReader();\n" : "Dim reader = command.ExecuteReader()\n";
        var prefix = language == "cs" ? CsCommand : VbCommand;
        Assert.Equal("access-limit", Extract(language, prefix + reader + accesses)[0].Gap);
        var padding = language == "cs" ? string.Concat(Enumerable.Range(0, 6000).Select(i => $"int unused{i} = 1;\n")) : string.Concat(Enumerable.Range(0, 6000).Select(i => $"Dim unused{i} = 1\n"));
        var tail = language == "cs" ? "return reader[\"Name\"];" : "Return reader(\"Name\")";
        Assert.Equal("operation-limit", Extract(language, prefix + padding + reader + tail)[0].Gap);
        var odd = language == "cs" ? "return reader[\"Unit Price\"];" : "Return reader(\"Unit Price\")";
        var access = Assert.Single(Extract(language, prefix + reader + odd)[0].Accesses);
        Assert.Null(access.Name);
        Assert.Equal("column-name-outside-identifier-vocabulary", access.Gap);
    }

    // ---------------------------------------------------------------- Native fixture matrix and CLI

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_fixture_matrix_binds_exact_executions_and_matches_independent_expectations(string language)
    {
        var root = RepoRoot();
        var sample = Path.Combine(root, "samples/sqlserver-result-column-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Results.vbproj" : "Results.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic)
            .OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(34, calls.Length);
        var keyed = new List<(CodeFact Fact, string Key)>();
        foreach (var fact in calls)
        {
            var name = System.Text.RegularExpressions.Regex.Match(fact.SourceSymbol!, @"\.([A-Za-z]+)\(").Groups[1].Value;
            keyed.Add((fact, name == "TwoReaders" ? name + "#" + keyed.Count(k => k.Key.StartsWith("TwoReaders", StringComparison.Ordinal)) : name));
        }
        Assert.Equal(SourceCases.Keys.Order(StringComparer.Ordinal), keyed.Select(k => k.Key).Order(StringComparer.Ordinal));
        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        foreach (var (fact, key) in keyed)
        {
            var shape = key switch
            {
                "Renamed" => ItemsShape with { Columns = [Column("Id", 0), Column("DisplayName", 1), Column("Total", 2)] },
                "Reordered" => ItemsShape with { Columns = [Column("Total", 0), Column("Name", 1), Column("Id", 2)] },
                "DuplicateDeclared" => ItemsShape with { Columns = [Column("Id", 0), Column("Name", 1), Column("Name", 2)] },
                "Incomplete" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
                _ => ItemsShape
            };
            var identity = key switch { "TwoReaders#1" => Other, "AbsentBinding" => Items with { Database = "OtherDatabase" }, _ => Items };
            var report = SqlServerResultEvidence.Compare(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(
                Declarations(fact.Repo, fact.CommitSha, shape, Shape(Other, Column("Title", 0))))), Bindings(fact, identity), calls, fact.Repo, fact.CommitSha);
            var statuses = Statuses(report, fact.FactId);
            Assert.True(statuses.SequenceEqual(SourceCases[key]), language + ":" + key + ":" + SqlServerSchemaEvidence.Json(report.Comparisons.Where(r => r.FactId == fact.FactId)));
            foreach (var s in statuses) counts[s]++;
            Assert.All(report.Comparisons.Where(r => r.FactId != fact.FactId), r => Assert.True(r.Status == U && r.Reasons.Contains("binding-missing")));
        }
        Assert.Equal(18, counts[M]); Assert.Equal(5, counts[X]); Assert.Equal(18, counts[U]);

        // Native CLI: both routes, byte-identical repeats, no overwrite, unknowns in machine and human output.
        var good = keyed.Single(k => k.Key == "GetOrdinalMissing").Fact;
        var writer = new StringWriter(); var error = new StringWriter();
        var declarationsPath = Path.Combine(temp.Path, "declarations.json");
        File.WriteAllText(declarationsPath, SqlServerSchemaEvidence.Json(Declarations(good.Repo, good.CommitSha)));
        var schemaPath = Path.Combine(temp.Path, "schema.json");
        Assert.True(await TraceMapCommand.RunAsync(["sqlserver-result-schema", "produce", "--declarations", declarationsPath, "--out", schemaPath], writer, error) == 0, error.ToString());
        var original = File.ReadAllBytes(schemaPath);
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(["sqlserver-result-schema", "produce", "--declarations", declarationsPath, "--out", schemaPath], writer, error));
        Assert.Equal(original, File.ReadAllBytes(schemaPath));
        var bindingsPath = Path.Combine(temp.Path, "bindings.json"); File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(Bindings(good)));
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var outputPath = Path.Combine(temp.Path, "report");
        var command = new[] { "sqlserver-result-schema", "compare", "--declarations", declarationsPath, "--schema", schemaPath, "--bindings", bindingsPath,
            "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", outputPath };
        Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
        command[^1] = Path.Combine(temp.Path, "repeat");
        Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
        foreach (var file in new[] { "result-column-comparison.json", "report.md" })
            Assert.Equal(File.ReadAllBytes(Path.Combine(outputPath, file)), File.ReadAllBytes(Path.Combine(command[^1], file)));
        var readback = SqlServerSchemaEvidence.Read<SqlServerResultComparisonReport>(File.ReadAllText(Path.Combine(outputPath, "result-column-comparison.json")));
        Assert.Equal([X, X], Statuses(readback, good.FactId));
        var markdown = File.ReadAllText(Path.Combine(outputPath, "report.md"));
        Assert.Contains("missing-column:Price", markdown);
        Assert.Contains("binding-missing", markdown);
        Assert.Contains("reader-alias-or-escape", markdown);
        Assert.Contains("reader-state-unavailable", markdown);

        var ddlRoot = Path.Combine(sample, "ddl");
        var ddlBase = File.ReadAllText(Path.Combine(ddlRoot, "base.sql"));
        var input = SqlServerSchemaEvidence.Json(new SqlServerDdlInput(SqlServerDdlResultEvidence.InputVersion, good.Repo, good.CommitSha, Items.Server, Items.Database, true,
            [new("base.sql", SqlServerSchemaEvidence.Hash(File.ReadAllBytes(Path.Combine(ddlRoot, "base.sql"))))]));
        var inputPath = Path.Combine(temp.Path, "ddl-input.json"); File.WriteAllText(inputPath, input);
        var ddlSchema = Path.Combine(temp.Path, "ddl-schema.json");
        Assert.True(await TraceMapCommand.RunAsync(["sqlserver-ddl-result", "produce", "--input", inputPath, "--source-root", ddlRoot, "--out", ddlSchema], writer, error) == 0, error.ToString());
        var ddlCommand = new[] { "sqlserver-ddl-result", "compare", "--input", inputPath, "--source-root", ddlRoot, "--schema", ddlSchema, "--bindings", bindingsPath,
            "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(temp.Path, "ddl-report") };
        Assert.True(await TraceMapCommand.RunAsync(ddlCommand, writer, error) == 0, error.ToString());
        var ddlReport = SqlServerSchemaEvidence.Read<SqlServerDdlResultComparisonReport>(File.ReadAllText(Path.Combine(temp.Path, "ddl-report", "result-column-comparison.json")));
        Assert.Equal([X, X], Statuses(ddlReport.Comparison, good.FactId));
        Assert.Equal(SqlServerSchemaEvidence.Json(SqlServerDdlResultEvidence.Compare(File.ReadAllText(ddlSchema), input, new Dictionary<string, string> { ["base.sql"] = ddlBase },
            Bindings(good), calls, good.Repo, good.CommitSha)) + "\n", File.ReadAllText(Path.Combine(temp.Path, "ddl-report", "result-column-comparison.json")));
        // The old parameter route does not accept the new result export.
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(["sqlserver-schema", "compare", "--declarations", declarationsPath, "--schema", schemaPath, "--bindings", bindingsPath,
            "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(temp.Path, "old-route")], writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "old-route")));
    }

    [Fact]
    public async Task Cli_refuses_links_malformed_input_and_incomplete_staging_without_publication()
    {
        using var temp = new TempDirectory();
        const string sql = "CREATE PROCEDURE dbo.ReadItems AS SELECT 1 AS Id;";
        File.WriteAllText(Path.Combine(temp.Path, "input.json"), Input(Sources(sql)));
        var target = Path.Combine(temp.Path, "out.json");
        string[] Produce() => ["sqlserver-ddl-result", "produce", "--input", Path.Combine(temp.Path, "input.json"), "--source-root", temp.Path, "--out", target];
        var writer = new StringWriter(); var error = new StringWriter();
        File.WriteAllText(Path.Combine(temp.Path, "public.sql"), "malformed SQL");
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Produce(), writer, error));
        Assert.False(File.Exists(target));
        if (!OperatingSystem.IsWindows())
        {
            File.WriteAllText(Path.Combine(temp.Path, "actual.sql"), sql);
            File.Delete(Path.Combine(temp.Path, "public.sql"));
            File.CreateSymbolicLink(Path.Combine(temp.Path, "public.sql"), Path.Combine(temp.Path, "actual.sql"));
            error = new StringWriter();
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(Produce(), writer, error));
            Assert.Contains("ddl-source-link-refused", error.ToString());
            Assert.False(File.Exists(target));
        }
        var report = Path.Combine(temp.Path, "report");
        Assert.Throws<IOException>(() => SqlServerSchemaCommand.PublishNewDirectory(report,
            staging => File.WriteAllText(Path.Combine(staging, "result-column-comparison.json"), "partial"),
            requiredFiles: ["result-column-comparison.json", "report.md"]));
        Assert.False(Directory.Exists(report));
        Assert.Empty(Directory.GetDirectories(temp.Path, ".sqlserver-report-*"));
    }
}
