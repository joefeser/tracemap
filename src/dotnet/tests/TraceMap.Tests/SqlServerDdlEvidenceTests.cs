using System.Text;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class SqlServerDdlEvidenceTests
{
    private const string Revision = "1111111111111111111111111111111111111111";
    private const string Simple = "CREATE PROCEDURE dbo.ReadItem @Name nvarchar(20) AS SELECT 1;";
    private static readonly SqlServerObjectIdentity Identity = new("SyntheticServer", "SyntheticDatabase", "dbo", "ReadItem");
    private static Dictionary<string, string> Sources(string sql = Simple) => new(StringComparer.Ordinal) { ["public.sql"] = sql };
    private static string Input(IReadOnlyDictionary<string, string> sources, string repo = "synthetic", string revision = Revision)
        => SqlServerSchemaEvidence.Json(new SqlServerDdlInput("sqlserver-ddl-input/1", repo, revision, Identity.Server, Identity.Database,
            true, sources.Select(f => new SqlServerDdlFile(f.Key, SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes(f.Value)))).ToArray()));
    private static SqlServerDdlExport Export(string sql = Simple) => SqlServerDdlEvidence.Produce(Input(Sources(sql)), Sources(sql));
    private static CodeFact Fact(string language = "cs") => new("fact", "scan", "synthetic", Revision, null, FactTypes.DatabaseOperationCandidate,
        RuleIds.DatabaseOperationCallPattern, EvidenceTiers.Tier1Semantic, "Example.Read()", null, null,
        new(language == "cs" ? "Calls.cs" : "Calls.vb", 10, 10, null, language == "cs" ? "CSharpSemanticExtractor" : "VisualBasicSemanticExtractor",
            language == "cs" ? ScannerVersions.CSharpSemanticExtractor : ScannerVersions.VisualBasicSemanticExtractor),
        new Dictionary<string, string> { ["sqlServerParameterState"] = SqlServerSchemaEvidence.Json(new SqlServerCallState("sqlserver-call/1", "bounded", "StoredProcedure",
            SqlServerSchemaEvidence.Hash(Encoding.UTF8.GetBytes("dbo.ReadItem")), [new("@Name", "NVarChar", 20, "Input", 3, "utf16-code-units")], null)) });
    private static SqlServerBindings Binding(CodeFact fact, SqlServerObjectIdentity? identity = null) => new("sqlserver-bindings/1", fact.Repo, fact.CommitSha,
        [new(fact.FactId, (identity ?? Identity).Schema + "." + (identity ?? Identity).Procedure, identity ?? Identity, SqlServerSchemaEvidence.CallDigest(fact))]);
    private static SqlServerDdlComparisonReport Compare(string sql = Simple, CodeFact? fact = null, SqlServerBindings? binding = null)
    {
        var f = fact ?? Fact();
        return SqlServerDdlEvidence.Compare(SqlServerSchemaEvidence.Json(Export(sql)), Input(Sources(sql)), Sources(sql), binding ?? Binding(f), [f], f.Repo, f.CommitSha);
    }

    [Theory]
    [InlineData("CREATE PROCEDURE")]
    [InlineData("ALTER PROCEDURE")]
    [InlineData("CREATE OR ALTER PROCEDURE")]
    [InlineData("CREATE PROC")]
    public void Statement_forms_quotes_comments_multiline_MAX_and_OUTPUT_have_independent_spans(string form)
    {
        var sql = "-- fake CREATE PROCEDURE dbo.Fake @X int AS SELECT 0;\n" + form + " [dbo].\"ReadItem\"\n/* parameter */ @Name nvarchar(MAX)\n, @Count int OUTPUT\nAS\nBEGIN\nSELECT N'CREATE PROCEDURE dbo.Fake AS SELECT 0';\nRETURN 7;\nEND;\nGO\n";
        var e = Export(sql);
        var p = Assert.Single(e.Declarations.Procedures);
        Assert.Equal(Identity, p.Identity);
        Assert.Equal("public.sql", p.FilePath);
        Assert.Equal(2, p.StartLine); Assert.Equal(9, p.EndLine);
        Assert.Equal(new SqlServerDeclarationParameter("@Name", "NVarChar", -1, "utf16-code-units", "Input", false, null, null, null, 3), p.Parameters[0]);
        Assert.Equal(new SqlServerDeclarationParameter("@Count", "Int", null, "not-applicable", "InputOutput", false, null, null, null, 4), p.Parameters[1]);
        Assert.Equal("public-synthetic-tsql-ddl", e.Declarations.Origin);
        Assert.Equal("Tier2Structural", e.Tier);
        Assert.Equal(SqlServerDdlEvidence.RuleId, e.RuleId);
        Assert.Empty(e.Unsupported);
        Assert.DoesNotContain("SELECT", SqlServerSchemaEvidence.Json(e));
    }

    [Theory]
    [InlineData("CREATE PROCEDURE ReadItem @Name int AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name dbo.NameAlias AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name dbo.NameTable READONLY AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name dbo.ClrType AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name sys.nvarchar(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name xml AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name nvarchar AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name nvarchar(4001) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name nvarchar(0) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name decimal(2,3) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name nvarchar(20) COLLATE Latin1_General_CI_AS AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name integer AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name dec(10,2) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name numeric(10,2) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name char varying(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name character varying(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name nchar varying(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name national character varying(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name national char varying(20) AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem WITH ENCRYPTION AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem AS EXTERNAL NAME Assembly.Class.Method;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem;2 AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem AS")]
    [InlineData(":r other.sql\n" + Simple)]
    [InlineData(":setvar Owner dbo\n" + Simple)]
    [InlineData("CREATE PROCEDURE $(Owner).ReadItem AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem AS SELECT N'$(Unresolved)';")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem AS SELECT 1;\nGO 2")]
    [InlineData("SELECT N'CREATE PROCEDURE dbo.Fake AS SELECT 1';")]
    [InlineData("-- CREATE PROCEDURE dbo.Fake AS SELECT 1;")]
    [InlineData("CREATE TABLE dbo.Items (Id int);")]
    [InlineData("CREATE PROCEDURE [odd schema].ReadItem AS SELECT 1;")]
    [InlineData("CREATE PROCEDURE dbo.ReadItem @Name int, @name int AS SELECT 1;")]
    public void Unsupported_or_malformed_inputs_reject_whole_admission(string sql)
        => Assert.Throws<ArgumentException>(() => Export(sql));

    [Theory]
    [InlineData("ALTER PROCEDURE dbo.ReadItem @Name nvarchar(20) AS SELECT 2;")]
    [InlineData("CREATE OR ALTER PROCEDURE dbo.ReadItem @Name int AS SELECT 2;")]
    [InlineData("CREATE PROCEDURE DBO.readitem @Name nvarchar(20) AS SELECT 2;")]
    public void Duplicate_conflicting_and_case_only_definitions_never_apply_migration_order(string second)
    {
        Assert.Throws<ArgumentException>(() => Export(Simple + "\nGO\n" + second));
        var sources = Sources(); sources.Add("second.sql", second);
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(Input(sources), sources));
    }

    [Theory]
    [InlineData("int", "Int", null)]
    [InlineData("INT", "Int", null)]
    [InlineData("[int]", "Int", null)]
    [InlineData("\"bigint\"", "BigInt", null)]
    [InlineData("Bit", "Bit", null)]
    [InlineData("[nvarchar](20)", "NVarChar", 20)]
    [InlineData("VARCHAR(max)", "VarChar", -1)]
    public void Canonical_type_spellings_admit_with_case_and_delimiters(string spelling, string sqlType, int? size)
    {
        var p = Assert.Single(Assert.Single(Export($"CREATE PROC dbo.ReadItem @Name {spelling} AS SELECT 1;").Declarations.Procedures).Parameters);
        Assert.Equal(sqlType, p.SqlType); Assert.Equal(size, p.Size);
    }

    [Fact]
    public void Leading_UTF8_BOM_is_hashed_exactly_and_admitted_with_unchanged_spans()
    {
        var sql = "\uFEFFCREATE PROC dbo.ReadItem\r\n@Name nvarchar(20)\r\nAS SELECT 1;";
        var e = Export(sql);
        Assert.Equal(SqlServerSchemaEvidence.Hash([0xEF, 0xBB, 0xBF, .. Encoding.UTF8.GetBytes(sql[1..])]), Assert.Single(e.Input.Files).Sha256);
        var p = Assert.Single(e.Declarations.Procedures);
        Assert.Equal(1, p.StartLine); Assert.Equal(3, p.EndLine);
        Assert.Equal(2, Assert.Single(e.ParameterSpans).StartLine);
        Assert.NotEqual(e.BoundedInputSha256, Export(sql[1..]).BoundedInputSha256);
        // Only one leading mark is tolerated; an embedded or repeated U+FEFF stays malformed.
        Assert.Contains("malformed-sql", Assert.Throws<ArgumentException>(() => Export("\uFEFF" + sql)).Message);
    }

    [Fact]
    public void Multiple_distinct_batches_are_retained_without_body_interpretation()
    {
        var e = Export(Simple + "\nGO\nALTER PROCEDURE other.ReadItem @Count int OUT AS EXEC(N'SELECT 1'); RETURN 7;\nGO");
        Assert.Equal(2, e.Declarations.Procedures.Count);
        Assert.Equal("other", e.Declarations.Procedures[1].Identity.Schema);
        Assert.Single(e.Declarations.Procedures[1].Parameters);
        Assert.DoesNotContain("resultColumns", SqlServerSchemaEvidence.Json(e));
    }

    [Theory]
    [InlineData("cs")]
    [InlineData("vb")]
    public void Defaults_facets_and_channels_stay_unknown_and_preserve_mismatches(string language)
    {
        var fact = Fact(language);
        var defaultReport = Compare("CREATE PROCEDURE dbo.ReadItem @Name nvarchar(20) = N'default-value' AS SELECT 1;", fact);
        var defaultGap = Assert.Single(defaultReport.DdlEvidence.Unsupported);
        Assert.Equal("default-semantics-unsupported", defaultGap.Kind);
        Assert.Equal("Tier4Unknown", defaultGap.Tier);
        Assert.Equal(1, defaultGap.StartLine); Assert.Equal(1, defaultGap.EndLine);
        Assert.Equal("unknown", Assert.Single(defaultReport.Comparison.Comparisons).Status);
        Assert.DoesNotContain("default-value", SqlServerSchemaEvidence.Json(defaultReport));
        var facet = Compare("CREATE PROCEDURE dbo.ReadItem @Name decimal(10,2) OUTPUT AS RETURN 7;", fact);
        var row = Assert.Single(facet.Comparison.Comparisons);
        Assert.Equal("unknown", row.Status);
        Assert.Contains("type:@Name", row.Reasons);
        Assert.Contains("direction:@Name", row.Reasons);
        Assert.Contains("ddl-precision-scale-semantics-unsupported:@Name", row.Reasons);
        var text = SqlServerParameterReportWriter.Build(facet);
        Assert.Contains("precision-scale-semantics-unsupported", text);
        Assert.Contains("No aggregate clean verdict", text);
        Assert.Contains("public-synthetic-tsql-ddl", text);
        Assert.Equal("partial", facet.Comparison.Coverage);
    }

    [Theory]
    [InlineData("server")]
    [InlineData("database")]
    [InlineData("schema")]
    [InlineData("case")]
    [InlineData("unbound")]
    public void Logical_identity_is_exact_and_never_short_name_bound(string mode)
    {
        var identity = mode switch { "server" => Identity with { Server = "Other" }, "database" => Identity with { Database = "other" },
            "schema" => Identity with { Schema = "other" }, _ => Identity with { Procedure = "readitem" } };
        var binding = Binding(Fact(), identity);
        if (mode == "unbound") binding = binding with { Calls = [] };
        var row = Assert.Single(Compare(binding: binding).Comparison.Comparisons);
        Assert.Equal("unknown", row.Status); Assert.Null(row.Declaration);
    }

    [Fact]
    public void Admission_validates_exact_origin_versions_digests_sources_parser_and_identity()
    {
        var e = Export(); var input = Input(Sources());
        var variants = new[] { e with { Version = "sqlserver-schema/1" }, e with { GeneratorSha256 = new('0', 64) },
            e with { BoundedInputSha256 = new('0', 64) }, e with { ParserSha256 = new('0', 64) }, e with { ParserVersion = "old" },
            e with { ExtractorVersion = "old" }, e with { Tier = "Tier1Semantic" }, e with { RuleId = "wrong" },
            e with { Declarations = e.Declarations with { Origin = "independent-synthetic-declarations" } },
            e with { Declarations = e.Declarations with { Procedures = [] } },
            e with { Unsupported = [new(Identity, "@Name", "fake", "rule", "Tier4Unknown", "public.sql", 1, 1)] } };
        foreach (var changed in variants)
            Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Admit(SqlServerSchemaEvidence.Json(changed), input, Sources(), "synthetic", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Admit(SqlServerSchemaEvidence.Json(e), input, Sources(Simple + " "), "synthetic", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Admit(SqlServerSchemaEvidence.Json(e), input, Sources(), "synthetic", new('2', 40)));
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Admit(SqlServerSchemaEvidence.Json(e), input, Sources(), "other", Revision));
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(input, new Dictionary<string, string>()));
        Assert.Throws<ArgumentException>(() => SqlServerSchemaEvidence.Produce(SqlServerSchemaEvidence.Json(e.Declarations)));
        Assert.Throws<ArgumentException>(() => SqlServerSchemaEvidence.Compare(new(e.Version, e.GeneratorSha256, e.BoundedInputSha256,
            e.ExtractorVersion, e.Declarations, e.RuleId, e.Tier), Binding(Fact()), [Fact()], "synthetic", Revision));
        var expected = SqlServerSchemaEvidence.Read<SqlServerDdlInput>(input);
        foreach (var bad in new[] { expected with { PublicSynthetic = false }, expected with { Version = "old" }, expected with { Files = [expected.Files[0], expected.Files[0]] },
            expected with { Files = [expected.Files[0] with { Path = "../public.sql" }] }, expected with { Revision = "HEAD" },
            expected with { Files = [expected.Files[0] with { Sha256 = new('f', 64) }] } })
            Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(SqlServerSchemaEvidence.Json(bad), Sources()));
        Assert.Throws<ArgumentException>(() => Compare(binding: Binding(Fact()) with { Calls = [Binding(Fact()).Calls[0] with { SourceEvidenceSha256 = new('0', 64) }] }));
    }

    [Fact]
    public void Bounds_fail_closed_and_do_not_publish_partial_success()
    {
        Assert.Throws<ArgumentException>(() => Export(Simple + new string(' ', SqlServerDdlEvidence.MaxFileBytes)));
        var many = Enumerable.Range(0, 33).ToDictionary(i => $"file{i}.sql", _ => Simple);
        Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(Input(many), many));
        var large = Enumerable.Range(0, 5).ToDictionary(i => $"file{i}.sql", i => $"CREATE PROC dbo.P{i} AS SELECT 1;" + new string(' ', 220000));
        Assert.Contains("input-size-limit", Assert.Throws<ArgumentException>(() => SqlServerDdlEvidence.Produce(Input(large), large)).Message);
        var parameters = string.Join(",", Enumerable.Range(0, 129).Select(i => $"@P{i} int"));
        Assert.Throws<ArgumentException>(() => Export("CREATE PROC dbo.P " + parameters + " AS SELECT 1;"));
        var procedures = string.Join("\nGO\n", Enumerable.Range(0, 257).Select(i => $"CREATE PROC dbo.P{i} AS SELECT 1;"));
        Assert.Throws<ArgumentException>(() => Export(procedures));
        var wide = string.Join("\nGO\n", Enumerable.Range(0, 128).Select(i =>
            $"CREATE PROC dbo.Wide{i} " + string.Join(',', Enumerable.Range(0, 128).Select(j => $"@P{j} int")) + " AS SELECT 1;"));
        Assert.Contains("export-size-limit", Assert.Throws<ArgumentException>(() => Export(wide)).Message);
        var tokens = "CREATE PROC dbo.P AS SELECT " + string.Join(',', Enumerable.Repeat("1", 50000)) + ";";
        Assert.Contains("token-limit", Assert.Throws<ArgumentException>(() => Export(tokens)).Message);
        Assert.Contains("syntax-complexity-limit", Assert.Throws<ArgumentException>(() => Export("CREATE PROC dbo.P AS SELECT " + new string('(', 129) + "1" + new string(')', 129))).Message);
    }

    [Theory]
    [InlineData("unary")]
    [InlineData("join")]
    public void Recursive_constructs_within_token_bounds_parse_without_overflowing_the_caller_stack(string shape)
    {
        // Each shape crashed the process with an uncatchable stack overflow (exit 134) when
        // parsed on a default 1-8 MiB stack, while staying under every documented bound.
        var body = shape == "unary"
            ? "SELECT " + string.Concat(Enumerable.Repeat("-~", 45000)) + "1;"
            : "SELECT 1 FROM a" + string.Concat(Enumerable.Repeat(" JOIN a", 9000)) + string.Concat(Enumerable.Repeat(" ON 1=1", 9000)) + ";";
        var sql = "CREATE PROC dbo.ReadItem @Name nvarchar(20) AS " + body;
        Assert.True(Encoding.UTF8.GetByteCount(sql) <= SqlServerDdlEvidence.MaxFileBytes);
        var e = Export(sql);
        Assert.Equal(Identity, Assert.Single(e.Declarations.Procedures).Identity);
        Assert.Contains("malformed-sql", Assert.Throws<ArgumentException>(() => Export(sql + " ON")).Message);
    }

    [Fact]
    public void Export_and_reports_are_repeatable_and_bind_only_admitted_public_inputs()
    {
        var e = Export();
        Assert.Equal(SqlServerSchemaEvidence.Hash(File.ReadAllBytes(typeof(SqlServerSchemaEvidence).Assembly.Location)), e.GeneratorSha256);
        Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<SqlServerDdlInput>(Input(Sources()))), e.BoundedInputSha256);
        Assert.Equal(SqlServerSchemaEvidence.Json(e), SqlServerSchemaEvidence.Json(Export()));
        var report = Compare();
        Assert.Equal(SqlServerSchemaEvidence.Json(report), SqlServerSchemaEvidence.Json(Compare()));
        var props = new Dictionary<string, string>(Fact().Properties) { ["rawSql"] = "PRIVATE", ["connectionString"] = "SECRET" };
        var projected = Compare(fact: Fact() with { Properties = props });
        Assert.Equal(report.BoundedInputSha256, projected.BoundedInputSha256);
        Assert.DoesNotContain("PRIVATE", SqlServerSchemaEvidence.Json(projected));
        Assert.NotEqual(e.BoundedInputSha256, Export(Simple + " ").BoundedInputSha256);
        Assert.Equal("bounded-match", Assert.Single(report.Comparison.Comparisons).Status);
    }

    [Fact]
    public void Parameter_spans_cover_multiline_declarations_and_RETURN_is_not_a_parameter()
    {
        var sql = "CREATE PROC dbo.ReadItem\n@Name\nnvarchar(20)\nAS SELECT 1; SELECT 2; RETURN 7;";
        var e = Export(sql);
        var span = Assert.Single(e.ParameterSpans);
        Assert.Equal(2, span.StartLine); Assert.Equal(3, span.EndLine);
        Assert.Contains("public.sql:2–3", SqlServerParameterReportWriter.Build(Compare(sql)));
        var fact = Fact();
        var state = SqlServerSchemaEvidence.Read<SqlServerCallState>(fact.Properties["sqlServerParameterState"]);
        var props = new Dictionary<string, string>(fact.Properties) { ["sqlServerParameterState"] = SqlServerSchemaEvidence.Json(state with {
            Parameters = state.Parameters.Append(new SqlServerCallParameter("@Return", "Int", null, "ReturnValue", 4, "not-applicable")).ToArray() }) };
        Assert.Equal("bounded-match", Assert.Single(Compare(sql, fact with { Properties = props }).Comparison.Comparisons).Status);
        var outputProps = new Dictionary<string, string>(fact.Properties) { ["sqlServerParameterState"] = SqlServerSchemaEvidence.Json(state with {
            Parameters = [state.Parameters[0] with { Direction = "Output" }] }) };
        var outputRow = Assert.Single(Compare("CREATE PROC dbo.ReadItem @Name nvarchar(20) OUTPUT AS RETURN 7;", fact with { Properties = outputProps }).Comparison.Comparisons);
        Assert.Equal("unknown", outputRow.Status);
        Assert.Contains("output-call-input-value-unproven:@Name", outputRow.Reasons);
    }

    [Fact]
    public async Task CLI_refuses_linked_sources_and_malformed_input_before_output_publication()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "input.json"), Input(Sources()));
        var target = Path.Combine(temp.Path, "output.json");
        string[] Command() => ["sqlserver-ddl", "produce", "--input", Path.Combine(temp.Path, "input.json"), "--source-root", temp.Path, "--out", target];
        var output = new StringWriter(); var error = new StringWriter();
        File.WriteAllText(Path.Combine(temp.Path, "public.sql"), "malformed SQL");
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Command(), output, error));
        Assert.False(File.Exists(target));
        if (OperatingSystem.IsWindows()) return; // Windows symlink privilege is not assumed.
        File.WriteAllText(Path.Combine(temp.Path, "actual.sql"), Simple);
        File.Delete(Path.Combine(temp.Path, "public.sql"));
        File.CreateSymbolicLink(Path.Combine(temp.Path, "public.sql"), Path.Combine(temp.Path, "actual.sql"));
        error = new StringWriter();
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Command(), output, error));
        Assert.Contains("ddl-source-link-refused", error.ToString());
        Assert.False(File.Exists(target));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_DDL_route_retains_independent_52_case_expectations_and_unbound_gaps(string language)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        var sample = Path.Combine(repo!.FullName, "samples/sqlserver-parameter-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Parameters.vbproj" : "Parameters.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(26, calls.Length);
        // Independently authored expected outcomes, never inferred from parser/parameter output.
        var expected = new Dictionary<string, string>
        {
            ["Good"] = "bounded-match", ["Max"] = "bounded-match", ["InputOutput"] = "bounded-match",
            ["SizeMismatch"] = "mismatch", ["TypeMismatch"] = "mismatch", ["DirectionMismatch"] = "mismatch", ["Extra"] = "mismatch", ["Missing"] = "mismatch",
            ["MissingSize"] = "unknown", ["Ansi"] = "unknown", ["Decimal"] = "unknown", ["CaseName"] = "unknown", ["Channels"] = "unknown", ["DefaultOmitted"] = "unknown",
            ["DynamicName"] = "unknown", ["Conditional"] = "unknown", ["Alias"] = "unknown", ["ParameterAlias"] = "unknown", ["Mutation"] = "unknown",
            ["ParameterReassignment"] = "unknown", ["HelperBoundary"] = "unknown", ["Text"] = "unknown", ["DynamicCommand"] = "unknown", ["Duplicate"] = "unknown"
        };
        var counts = new Dictionary<string, int> { ["bounded-match"] = 0, ["mismatch"] = 0, ["unknown"] = 0 };
        foreach (var fact in calls)
        {
            var name = expected.Keys.SingleOrDefault(k => fact.SourceSymbol!.Contains("." + k + "(", StringComparison.Ordinal)) ?? "TwoCommands";
            var two = calls.Where(f => f.SourceSymbol!.Contains(".TwoCommands(", StringComparison.Ordinal)).OrderBy(f => f.Evidence.StartLine).ToArray();
            var status = name == "TwoCommands" ? (fact.FactId == two[0].FactId ? "mismatch" : "bounded-match") : expected[name];
            var parameter = name switch { "Max" => "@Name nvarchar(MAX)", "Ansi" => "@Name varchar(20)", "Decimal" => "@Name decimal(10,2)",
                "InputOutput" or "Channels" => "@Name nvarchar(20) OUTPUT", "DefaultOmitted" => "@Name nvarchar(20) = NULL", _ => "@Name nvarchar(20)" };
            var sql = "CREATE OR ALTER PROCEDURE [dbo].[ReadItem]\n" + parameter + "\nAS SELECT 1; RETURN 7;";
            var sources = Sources(sql); var input = Input(sources, fact.Repo, fact.CommitSha);
            var export = SqlServerDdlEvidence.Produce(input, sources);
            var report = SqlServerDdlEvidence.Compare(SqlServerSchemaEvidence.Json(export), input, sources, Binding(fact), calls, fact.Repo, fact.CommitSha);
            var row = Assert.Single(report.Comparison.Comparisons, r => r.FactId == fact.FactId);
            Assert.True(row.Status == status, language + ":" + name + ":" + SqlServerSchemaEvidence.Json(row));
            counts[row.Status]++;
            Assert.Equal(25, report.Comparison.Comparisons.Count(r => r.FactId != fact.FactId && r.Status == "unknown" && r.Reasons.Contains("binding-missing")));
            if (name == "Channels") Assert.Contains(row.CallState!.Parameters, p => p.Direction == "ReturnValue");
            if (name != "Good") continue;
            File.WriteAllText(Path.Combine(temp.Path, "public.sql"), sql);
            var inputPath = Path.Combine(temp.Path, "input.json"); File.WriteAllText(inputPath, input);
            var schemaPath = Path.Combine(temp.Path, "schema.json");
            var writer = new StringWriter(); var error = new StringWriter();
            var produce = new[] { "sqlserver-ddl", "produce", "--input", inputPath, "--source-root", temp.Path, "--out", schemaPath };
            Assert.True(await TraceMapCommand.RunAsync(produce, writer, error) == 0, error.ToString());
            var original = File.ReadAllBytes(schemaPath);
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(produce, writer, error));
            Assert.Equal(original, File.ReadAllBytes(schemaPath));
            var bindingPath = Path.Combine(temp.Path, "bindings.json"); File.WriteAllText(bindingPath, SqlServerSchemaEvidence.Json(Binding(fact)));
            var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
            var outputPath = Path.Combine(temp.Path, "report");
            var command = new[] { "sqlserver-ddl", "compare", "--input", inputPath, "--source-root", temp.Path, "--schema", schemaPath, "--bindings", bindingPath,
                "--facts", factsPath, "--repo", fact.Repo, "--revision", fact.CommitSha, "--out", outputPath };
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(temp.Path, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "parameter-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(outputPath, file)), File.ReadAllBytes(Path.Combine(command[^1], file)));
            Assert.Contains("binding-missing", File.ReadAllText(Path.Combine(outputPath, "report.md")));
        }
        Assert.Equal(4, counts["bounded-match"]); Assert.Equal(6, counts["mismatch"]); Assert.Equal(16, counts["unknown"]);
    }
}
