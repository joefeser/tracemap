using System.Text.Json;
using System.Text.RegularExpressions;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;
namespace TraceMap.Tests;
/// <summary>Native fixture matrix for the Phase 4 helper boundary; mirrors design.md and scripts/sqlserver-helper-result-evidence/smoke.py.</summary>
public sealed partial class SqlServerHelperResultEvidenceTests
{
    private static readonly string[] Origins = ["result-schema", "ddl-result", "ddl-table-result"];
    // Core caller cells (authored before running the extractor): statuses and shape variant, against all three origins.
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreCases = new()
    {
        ["TableName"] = ([M], null), ["TableOrdinal"] = ([M], null), ["TableForeach"] = ([M], null), ["TableRowsGuard"] = ([M], null),
        ["OtherProcedure"] = ([M], null), ["TwoCallers#0"] = ([M], null), ["TwoCallers#1"] = ([M], null), ["SetIndex"] = ([M], null),
        ["SetDefaultName"] = ([M], null), ["SetSourceName"] = ([M], null), ["SetTableLocalGuards"] = ([M], null), ["TextHelper"] = ([M], null),
        ["ConnectionHelper"] = ([M], null), ["ConstProcedure"] = ([M], null),
        ["MissingName"] = ([X], null), ["OrdinalOutOfRange"] = ([X], null), ["OtherProcedureMissing"] = ([X], null), ["SetMissing"] = ([X], null),
        ["Renamed"] = ([X], "renamed"), ["CaseOnly"] = ([U], null), ["DynamicName"] = ([U], null), ["AbsentBinding"] = ([U], null),
        ["Incomplete"] = ([U], "incomplete")
    };
    // Helper state cells keyed by the Fill's enclosing method: exact helper gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> HelperCases = new()
    {
        ["PublicHelper"] = "helper-accessibility-unsupported", ["InternalHelper"] = "helper-accessibility-unsupported",
        ["InstanceHelper"] = "helper-static-required", ["VirtualHelper"] = "helper-virtual-or-interface-unsupported",
        ["GenericHelper"] = "helper-generic-unsupported", ["OverloadedHelper#0"] = "helper-overload-ambiguous", ["OverloadedHelper#1"] = "helper-overload-ambiguous",
        ["ExtensionHelper"] = "helper-extension-unsupported", ["PartialHelper"] = "helper-partial-type-unsupported", ["RefHelper"] = "helper-parameter-unsupported",
        ["OptionalHelper"] = "helper-parameter-unsupported", ["TwoStringHelper"] = "helper-parameter-unsupported", ["ObjectHelper"] = "helper-return-type-unsupported",
        ["RecursiveHelper"] = "helper-recursion", ["DelegateHelper"] = "helper-reference-escape", ["MutatingHelper"] = "datatable-mutation",
        ["GuardedHelper"] = "unsupported-control-flow", ["ExecutingHelper"] = "command-executed-outside-fill", ["MultipleReturnHelper"] = "helper-multiple-returns",
        ["ConstantTextHelper"] = "helper-command-text-parameter-required", ["ParameterWriteHelper"] = "helper-parameter-mutation",
        ["CallingHelper"] = "helper-body-unsupported", ["DisposedHelper"] = "helper-result-disposed", ["UncalledHelper"] = "bounded:helper-caller-unavailable",
        ["TextCommandHelper"] = "bounded:text-command-outside-scope", ["InlineFill"] = "helper-return-required", ["CallerRefill"] = "helper-return-required"
    };
    // Caller state cells keyed by the calling method: exact caller gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> CallerCases = new()
    {
        ["CallerNamedArgument"] = "helper-named-argument-unsupported", ["CallerDynamicProcedure"] = "helper-procedure-argument-nonconstant",
        ["CallerNullProcedure"] = "helper-procedure-argument-invalid", ["CallerDiscarded"] = "helper-result-local-required",
        ["CallerChained"] = "helper-result-local-required", ["CallerPassed"] = "helper-result-local-required", ["CallerObjectLocal"] = "helper-result-local-required",
        ["CallerRowWrite"] = "datatable-mutation", ["CallerRefill"] = "repeated-fill", ["CallerReassigned#0"] = "datatable-reassignment",
        ["CallerReassigned#1"] = "helper-result-local-required", ["CallerAlias"] = "datatable-alias-or-escape", ["CallerTryCatch"] = "unsupported-control-flow",
        ["CallerLambda"] = "unsupported-control-flow", ["CallerLaterTable"] = "dataset-later-table-unsupported", ["CallerSetMutation"] = "dataset-mutation",
        ["CallerSourceNameMismatch"] = "dataset-table-name-unbound", ["CallerConnectionArgument"] = "helper-connection-argument-unsupported",
        ["CallerNoReads"] = "bounded:no-bounded-column-access"
    };
    private static readonly string[] BoundedHelpers = ["ReadTable", "ReadSet", "ReadNamedSet", "ReadTableText", "ReadTableUsing"];
    private static readonly Regex MemberName = new(@"\.([A-Za-z]+)(?:<[^>]*>|\(Of [^)]*\))?\(", RegexOptions.CultureInvariant);

    private static Dictionary<string, T> Keyed<T>(IEnumerable<(string Name, T Item)> items)
    {
        var all = items.ToArray();
        var counts = all.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.Count());
        var seen = new Dictionary<string, int>(StringComparer.Ordinal);
        var keyed = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var (name, item) in all)
        {
            var n = seen.GetValueOrDefault(name);
            seen[name] = n + 1;
            keyed.Add(counts[name] > 1 ? $"{name}#{n}" : name, item);
        }
        return keyed;
    }
    private static (string Json, string Schema, IReadOnlyDictionary<string, string> Sources) Origin(string origin, string? variant, string repo, string revision)
    {
        var shapes = Path.Combine(RepoRoot(), "samples/sqlserver-result-column-evidence");
        if (origin == "result-schema")
        {
            var items = variant switch
            {
                "renamed" => ItemsShape with { Columns = [Column("Id", 0, "int"), Column("DisplayName", 1), Column("Total", 2, "decimal(10,2)")] },
                "incomplete" => ItemsShape with { Completeness = "unknown", UnknownReason = "authored-incomplete" },
                _ => ItemsShape
            };
            var declarations = SqlServerSchemaEvidence.Json(Declarations(repo, revision, items, Shape(Other, Column("Title", 0))));
            return (declarations, SqlServerSchemaEvidence.Json(SqlServerResultEvidence.Produce(declarations)), new Dictionary<string, string>());
        }
        var folder = Path.Combine(shapes, origin == "ddl-result" ? "ddl" : "ddl-table");
        var template = variant switch { "renamed" => "renamed.sql", "incomplete" => "if.sql", _ => "base.sql" };
        var files = origin == "ddl-result" ? new[] { template } : [template, "tables.sql"];
        var sources = files.ToDictionary(f => f, f => File.ReadAllText(Path.Combine(folder, f)), StringComparer.Ordinal);
        var input = SqlServerSchemaEvidence.Json(new SqlServerDdlInput(origin == "ddl-result" ? SqlServerDdlResultEvidence.InputVersion : SqlServerDdlTableResultEvidence.InputVersion,
            repo, revision, Items.Server, Items.Database, true, [.. files.Select(f => new SqlServerDdlFile(f, SqlServerSchemaEvidence.Hash(File.ReadAllBytes(Path.Combine(folder, f)))))]));
        var export = origin == "ddl-result" ? SqlServerSchemaEvidence.Json(SqlServerDdlResultEvidence.Produce(input, sources))
            : SqlServerSchemaEvidence.Json(SqlServerDdlTableResultEvidence.Produce(input, sources));
        return (input, export, sources);
    }
    private static SqlServerHelperResultComparisonReport CompareOrigin(string origin, string? variant, SqlServerHelperResultBindings bindings, IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var (json, schema, sources) = Origin(origin, variant, repo, revision);
        return origin switch
        {
            "result-schema" => SqlServerHelperResultEvidence.CompareResultSchema(schema, json, bindings, facts, repo, revision),
            "ddl-result" => SqlServerHelperResultEvidence.CompareDdlResult(schema, json, sources, bindings, facts, repo, revision),
            _ => SqlServerHelperResultEvidence.CompareDdlTableResult(schema, json, sources, bindings, facts, repo, revision)
        };
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_fixture_matrix_binds_each_call_site_and_matches_independent_expectations(string language)
    {
        var sample = Path.Combine(RepoRoot(), "samples/sqlserver-helper-result-evidence");
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "Helpers.vbproj" : "Helpers.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var fills = calls.Where(f => f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill").OrderBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(32, fills.Length);
        // The sibling helper property lives on Fill facts only, next to the unchanged DataTable and DataSet properties.
        Assert.All(calls, f => Assert.Equal(f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill", f.Properties.ContainsKey("sqlServerHelperResultState")));
        Assert.All(fills, f => Assert.True(f.Properties.ContainsKey("sqlServerDataTableState") && f.Properties.ContainsKey("sqlServerDataSetState")));
        var byFill = Keyed(fills.Select(f => (MemberName.Match(f.SourceSymbol!).Groups[1].Value, f)));
        Assert.Equal(BoundedHelpers.Concat(HelperCases.Keys).Order(StringComparer.Ordinal), byFill.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, fact) in byFill)
        {
            var state = SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(fact.Properties["sqlServerHelperResultState"]);
            var expected = HelperCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(state.Coverage == "bounded" && state.Gap is null, $"{language}:{key}:{state.Gap}");
            else
                Assert.True(state.Coverage == "unknown" && state.Gap == expected && state.Callers.Count == 0, $"{language}:{key}:{state.Gap}≠{expected}");
        }
        // Cross-property: helpers keep their Phase 2/3 escape gaps; the inline Fill keeps a bounded DataTable state.
        foreach (var helper in BoundedHelpers)
        {
            var table = SqlServerSchemaEvidence.Read<SqlServerDataTableState>(byFill[helper].Properties["sqlServerDataTableState"]);
            var set = SqlServerSchemaEvidence.Read<SqlServerDataSetState>(byFill[helper].Properties["sqlServerDataSetState"]);
            Assert.True(helper.Contains("Set", StringComparison.Ordinal)
                ? table.Gap == "fill-overload-unsupported" && set.Gap == "dataset-alias-or-escape"
                : table.Gap == "datatable-alias-or-escape" && set.Gap == "fill-overload-unsupported", $"{language}:{helper}:{table.Gap}/{set.Gap}");
        }
        Assert.Equal("bounded", SqlServerSchemaEvidence.Read<SqlServerDataTableState>(byFill["InlineFill"].Properties["sqlServerDataTableState"]).Coverage);
        var byCaller = Keyed(BoundedHelpers.SelectMany(h =>
        {
            var state = SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(byFill[h].Properties["sqlServerHelperResultState"]);
            return state.Callers.Select((c, i) => (MemberName.Match(c.Caller!).Groups[1].Value, (Fact: byFill[h], Index: i, Caller: c)));
        }).OrderBy(c => c.Item2.Caller.Line));
        Assert.Equal(CoreCases.Keys.Concat(CallerCases.Keys).Order(StringComparer.Ordinal), byCaller.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, (_, _, caller)) in byCaller)
        {
            var expected = CallerCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(caller.Coverage == "bounded" && caller.Gap is null && caller.CommandTextHash is not null, $"{language}:{key}:{caller.Gap}");
            else
                Assert.True(caller.Coverage == "unknown" && caller.Gap == expected && caller.Accesses.Count == 0, $"{language}:{key}:{caller.Gap}≠{expected}");
        }
        // Different procedure arguments are recorded per call site.
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), byCaller["TwoCallers#0"].Caller.CommandTextHash);
        Assert.Equal(FactFactory.Hash("dbo.ReadOther", 64), byCaller["TwoCallers#1"].Caller.CommandTextHash);
        Assert.Same(byCaller["TwoCallers#0"].Fact, byCaller["TwoCallers#1"].Fact);

        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        var plan = CoreCases.SelectMany(c => Origins.Select(o => (Kind: "caller", c.Key, Origin: o, c.Value.Variant, c.Value.Statuses)))
            .Concat(HelperCases.Keys.Select(k => (Kind: "helper", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U })))
            .Concat(CallerCases.Keys.Select(k => (Kind: "caller", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U }))).ToArray();
        Assert.Equal(115, plan.Length);
        foreach (var (kind, key, origin, variant, expected) in plan)
        {
            var (fact, index) = kind == "helper" ? (byFill[key], -1) : (byCaller[key].Fact, byCaller[key].Index);
            var identity = key switch
            {
                "OtherProcedure" or "TwoCallers#1" or "OtherProcedureMissing" or "CallerReassigned#1" => Other,
                "AbsentBinding" => Items with { Database = "OtherDatabase" }, _ => Items
            };
            var bindings = Bindings(index < 0 ? [] : [Binding(fact, index, identity)]);
            var report = CompareOrigin(origin, variant, bindings with { Repo = fact.Repo, Revision = fact.CommitSha }, calls, fact.Repo, fact.CommitSha);
            var rows = report.Comparisons.Where(r => r.FactId == fact.FactId && (index < 0 || r.CallerIndex == index)).ToArray();
            Assert.True(rows.Select(r => r.Status).SequenceEqual(expected), $"{language}:{key}:{origin}:" + SqlServerSchemaEvidence.Json(rows));
            var gap = kind == "helper" ? HelperCases[key] : CallerCases.GetValueOrDefault(key);
            if (gap is not null) Assert.Contains(gap.StartsWith("bounded:", StringComparison.Ordinal) ? gap[8..] : gap, Assert.Single(rows).Reasons);
            foreach (var row in rows) counts[row.Status]++;
            Assert.All(report.Comparisons.Where(r => !rows.Contains(r)), r => Assert.Equal(U, r.Status));
            Assert.Equal(origin, report.SchemaRoute);
        }
        Assert.Equal((42, 15, 58), (counts[M], counts[X], counts[U]));

        // Native CLI for each origin: byte-identical repeat, no overwrite, wrong-origin and other-route binding refusals.
        var (good, goodIndex, _) = byCaller["TwoCallers#1"];
        var writer = new StringWriter(); var error = new StringWriter();
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var bindingsPath = Path.Combine(temp.Path, "bindings.json");
        File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(Bindings(Binding(good, goodIndex, Other)) with { Repo = good.Repo, Revision = good.CommitSha }));
        foreach (var origin in Origins)
        {
            var (json, schema, sources) = Origin(origin, null, good.Repo, good.CommitSha);
            var dir = Path.Combine(temp.Path, origin);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "schema.json"), schema);
            File.WriteAllText(Path.Combine(dir, "origin.json"), json);
            foreach (var (file, text) in sources) File.WriteAllText(Path.Combine(dir, file), text);
            string[] originOptions = origin == "result-schema" ? ["--declarations", Path.Combine(dir, "origin.json")] : ["--input", Path.Combine(dir, "origin.json"), "--source-root", dir];
            var command = new[] { "sqlserver-helper-result", "compare", "--origin", origin }.Concat(originOptions).Concat(new[] { "--schema", Path.Combine(dir, "schema.json"),
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(dir, "report") }).ToArray();
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(dir, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "helper-result-column-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "report", file)), File.ReadAllBytes(Path.Combine(dir, "repeat", file)));
            var readback = SqlServerSchemaEvidence.Read<SqlServerHelperResultComparisonReport>(File.ReadAllText(Path.Combine(dir, "report", "helper-result-column-comparison.json")));
            Assert.Equal([M], readback.Comparisons.Where(r => r.FactId == good.FactId && r.CallerIndex == goodIndex).Select(r => r.Status));
            Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<JsonElement>(schema)), readback.SchemaEvidenceSha256);
            var markdown = File.ReadAllText(Path.Combine(dir, "report", "report.md"));
            foreach (var reason in new[] { "binding-missing", "helper-return-required", "helper-recursion", "helper-result-local-required" }) Assert.Contains(reason, markdown);
            var wrong = (string[])command.Clone();
            wrong[3] = origin == "ddl-result" ? "ddl-table-result" : origin == "ddl-table-result" ? "ddl-result" : "ddl-table-result";
            wrong[^1] = Path.Combine(dir, "wrong-origin");
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(wrong, writer, error));
            Assert.False(Directory.Exists(wrong[^1]));
        }
        // DataSet bindings are refused by the helper route, and helper bindings by the DataSet and DataTable routes.
        var setBindings = Path.Combine(temp.Path, "dataset-bindings.json");
        File.WriteAllText(setBindings, SqlServerSchemaEvidence.Json(new SqlServerDataSetBindings(SqlServerDataSetEvidence.BindingsVersion, good.Repo, good.CommitSha,
            [new(good.FactId, "dbo.ReadOther", Other, SqlServerDataSetEvidence.DataSetDigest(good))])));
        var jsonDir = Path.Combine(temp.Path, "result-schema");
        string[] Route(string route, string bindings, string output) => [route, "compare", "--origin", "result-schema",
            "--declarations", Path.Combine(jsonDir, "origin.json"), "--schema", Path.Combine(jsonDir, "schema.json"), "--bindings", bindings, "--facts", factsPath,
            "--repo", good.Repo, "--revision", good.CommitSha, "--out", output];
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-helper-result", setBindings, Path.Combine(temp.Path, "x1")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-dataset-result", bindingsPath, Path.Combine(temp.Path, "x2")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-datatable-result", bindingsPath, Path.Combine(temp.Path, "x3")), writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "x1")) || Directory.Exists(Path.Combine(temp.Path, "x2")) || Directory.Exists(Path.Combine(temp.Path, "x3")));
    }
}
