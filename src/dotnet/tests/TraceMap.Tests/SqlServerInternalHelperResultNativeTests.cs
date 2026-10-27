using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;
using P4 = TraceMap.Tests.SqlServerHelperResultEvidenceTests;
namespace TraceMap.Tests;
/// <summary>Native multi-document fixture matrix for internal helpers; mirrors design.md and scripts/sqlserver-internal-helper-result-evidence/smoke.py.</summary>
public sealed class SqlServerInternalHelperResultNativeTests
{
    private const string M = "bounded-match", X = "mismatch", U = "unknown";
    // Core caller cells (authored before running the extractor): statuses and shape variant, against all three origins.
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreCases = new()
    {
        ["CrossTableName"] = ([M], null), ["CrossTableOrdinal"] = ([M], null), ["CrossTableForeach"] = ([M], null), ["CrossOtherProcedure"] = ([M], null),
        ["CrossTwoCallers#0"] = ([M], null), ["CrossTwoCallers#1"] = ([M], null), ["CrossSetIndex"] = ([M], null), ["CrossSetGuards"] = ([M], null),
        ["CrossTextHelper"] = ([M], null), ["CrossConnectionHelper"] = ([M], null), ["SameDocumentName"] = ([M], null), ["OtherTypeCaller"] = ([M], null),
        ["CrossMissingName"] = ([X], null), ["CrossOrdinalOutOfRange"] = ([X], null), ["CrossOtherProcedureMissing"] = ([X], null), ["CrossRenamed"] = ([X], "renamed"),
        ["CrossCaseOnly"] = ([U], null), ["CrossDynamicName"] = ([U], null), ["CrossAbsentBinding"] = ([U], null), ["CrossIncomplete"] = ([U], "incomplete")
    };
    // Helper state cells keyed by the Fill's enclosing method: exact helper gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> HelperCases = new()
    {
        ["PrivateHelper"] = "helper-private-phase4-scope", ["PublicHelper"] = "helper-accessibility-unsupported",
        ["ProtectedInternalHelper"] = "helper-accessibility-unsupported", ["SplitPartialHelper"] = "helper-partial-type-unsupported",
        ["CrossDelegateHelper"] = "helper-reference-escape", ["CrossNameofHelper"] = "helper-reference-escape",
        ["GeneratedCallerHelper"] = "helper-generated-document-unsupported", ["LinkedCallerHelper"] = "helper-linked-document-unsupported",
        ["LinkedFileHelper"] = "helper-linked-document-unsupported", ["UncalledInternalHelper"] = "bounded:helper-caller-unavailable"
    };
    // Caller state cells keyed by the calling method: exact caller gap, or "bounded:<comparison reason>".
    private static readonly Dictionary<string, string> CallerCases = new()
    {
        ["CrossNamedArgument"] = "helper-named-argument-unsupported", ["CrossDynamicProcedure"] = "helper-procedure-argument-nonconstant",
        ["CrossDiscarded"] = "helper-result-local-required", ["CrossRowWrite"] = "datatable-mutation", ["CrossAlias"] = "datatable-alias-or-escape",
        ["CrossTryCatch"] = "unsupported-control-flow", ["CrossLaterTable"] = "dataset-later-table-unsupported", ["CrossNoReads"] = "bounded:no-bounded-column-access"
    };
    private static readonly string[] BoundedHelpers = ["ReadTable", "ReadSet", "ReadTableText", "ReadTableUsing"];
    private static readonly HashSet<string> OtherProcedure = ["CrossOtherProcedure", "CrossTwoCallers#1", "CrossOtherProcedureMissing"];
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }
    private static SqlServerInternalHelperResultBinding Binding(CodeFact fact, int caller, SqlServerObjectIdentity identity)
        => new(fact.FactId, caller, identity.Schema + "." + identity.Procedure, identity, SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(fact, caller));
    private static SqlServerInternalHelperResultComparisonReport CompareOrigin(string origin, string? variant, SqlServerInternalHelperResultBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var (json, schema, sources) = P4.Origin(origin, variant, repo, revision);
        return origin switch
        {
            "result-schema" => SqlServerInternalHelperResultEvidence.CompareResultSchema(schema, json, bindings, facts, repo, revision),
            "ddl-result" => SqlServerInternalHelperResultEvidence.CompareDdlResult(schema, json, sources, bindings, facts, repo, revision),
            _ => SqlServerInternalHelperResultEvidence.CompareDdlTableResult(schema, json, sources, bindings, facts, repo, revision)
        };
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_multi_document_matrix_binds_each_call_site_with_its_path(string language)
    {
        var sample = Path.Combine(RepoRoot(), "samples/sqlserver-internal-helper-result-evidence");
        var e = language == "vb" ? "vb" : "cs";
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "InternalHelpers.vbproj" : "InternalHelpers.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var fills = calls.Where(f => f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill")
            .OrderBy(f => f.Evidence.FilePath, StringComparer.Ordinal).ThenBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(14, fills.Length);
        Assert.All(calls, f => Assert.Equal(f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill", f.Properties.ContainsKey("sqlServerInternalHelperResultState")));
        var byFill = P4.Keyed(fills.Select(f => (P4.MemberName.Match(f.SourceSymbol!).Groups[1].Value, f)));
        Assert.Equal(BoundedHelpers.Concat(HelperCases.Keys).Order(StringComparer.Ordinal), byFill.Keys.Order(StringComparer.Ordinal));
        Assert.Equal("shared/LinkedHelpers." + e, byFill["LinkedFileHelper"].Evidence.FilePath);
        foreach (var (key, fact) in byFill)
        {
            var state = SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultState>(fact.Properties["sqlServerInternalHelperResultState"]);
            Assert.Equal("sqlserver-internal-helper-result/1", state.Version);
            var expected = HelperCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(state.Coverage == "bounded" && state.Gap is null, $"{language}:{key}:{state.Gap}");
            else
                Assert.True(state.Coverage == "unknown" && state.Gap == expected && state.Callers.Count == 0, $"{language}:{key}:{state.Gap}≠{expected}");
            // Cross-property: Phase 4 keeps internal helpers outside its private boundary; only the private helper is Phase 4 evidence.
            var phase4 = SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(fact.Properties["sqlServerHelperResultState"]);
            Assert.True(key == "PrivateHelper" ? phase4.Coverage == "bounded" && phase4.Callers.Count == 1
                : phase4.Coverage == "unknown" && phase4.Gap == "helper-accessibility-unsupported", $"{language}:{key}:phase4:{phase4.Gap}");
        }
        foreach (var helper in BoundedHelpers)
        {
            var table = SqlServerSchemaEvidence.Read<SqlServerDataTableState>(byFill[helper].Properties["sqlServerDataTableState"]);
            var set = SqlServerSchemaEvidence.Read<SqlServerDataSetState>(byFill[helper].Properties["sqlServerDataSetState"]);
            Assert.True(helper.Contains("Set", StringComparison.Ordinal)
                ? table.Gap == "fill-overload-unsupported" && set.Gap == "dataset-alias-or-escape"
                : table.Gap == "datatable-alias-or-escape" && set.Gap == "fill-overload-unsupported", $"{language}:{helper}:{table.Gap}/{set.Gap}");
        }
        var byCaller = P4.Keyed(BoundedHelpers.SelectMany(h =>
        {
            var state = SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultState>(byFill[h].Properties["sqlServerInternalHelperResultState"]);
            return state.Callers.Select((c, i) => (P4.MemberName.Match(c.Caller!).Groups[1].Value, (Fact: byFill[h], Index: i, Caller: c)));
        }).OrderBy(c => c.Item2.Caller.Path, StringComparer.Ordinal).ThenBy(c => c.Item2.Caller.Line));
        Assert.Equal(CoreCases.Keys.Concat(CallerCases.Keys).Order(StringComparer.Ordinal), byCaller.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, (_, _, caller)) in byCaller)
        {
            var path = key == "SameDocumentName" ? "Helpers" : key == "OtherTypeCaller" ? "MoreCallers" : "Callers";
            Assert.Equal($"{language}/{path}.{e}", caller.Path);
            var expected = CallerCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("bounded:", StringComparison.Ordinal))
                Assert.True(caller.Coverage == "bounded" && caller.Gap is null && caller.CommandTextHash is not null, $"{language}:{key}:{caller.Gap}");
            else
                Assert.True(caller.Coverage == "unknown" && caller.Gap == expected && caller.Accesses.Count == 0, $"{language}:{key}:{caller.Gap}≠{expected}");
        }
        Assert.Equal(FactFactory.Hash("dbo.ReadItems", 64), byCaller["CrossTwoCallers#0"].Caller.CommandTextHash);
        Assert.Equal(FactFactory.Hash("dbo.ReadOther", 64), byCaller["CrossTwoCallers#1"].Caller.CommandTextHash);
        Assert.Same(byCaller["CrossTwoCallers#0"].Fact, byCaller["CrossTwoCallers#1"].Fact);

        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        var plan = CoreCases.SelectMany(c => P4.Origins.Select(o => (Kind: "caller", c.Key, Origin: o, c.Value.Variant, c.Value.Statuses)))
            .Concat(HelperCases.Keys.Select(k => (Kind: "helper", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U })))
            .Concat(CallerCases.Keys.Select(k => (Kind: "caller", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U }))).ToArray();
        Assert.Equal(78, plan.Length);
        foreach (var (kind, key, origin, variant, expected) in plan)
        {
            var (fact, index) = kind == "helper" ? (byFill[key], -1) : (byCaller[key].Fact, byCaller[key].Index);
            var identity = OtherProcedure.Contains(key) ? P4.Other : key == "CrossAbsentBinding" ? P4.Items with { Database = "OtherDatabase" } : P4.Items;
            var bindings = new SqlServerInternalHelperResultBindings(SqlServerInternalHelperResultEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
                index < 0 ? [] : [Binding(fact, index, identity)]);
            var report = CompareOrigin(origin, variant, bindings, calls, fact.Repo, fact.CommitSha);
            var rows = report.Comparisons.Where(r => r.FactId == fact.FactId && (index < 0 || r.CallerIndex == index)).ToArray();
            Assert.True(rows.Select(r => r.Status).SequenceEqual(expected), $"{language}:{key}:{origin}:" + SqlServerSchemaEvidence.Json(rows));
            var gap = kind == "helper" ? HelperCases[key] : CallerCases.GetValueOrDefault(key);
            if (gap is not null) Assert.Contains(gap.StartsWith("bounded:", StringComparison.Ordinal) ? gap[8..] : gap, Assert.Single(rows).Reasons);
            if (index >= 0) Assert.All(rows, r => Assert.Equal(byCaller[key].Caller.Path, r.CallerPath));
            foreach (var row in rows) counts[row.Status]++;
            Assert.All(report.Comparisons.Where(r => !rows.Contains(r)), r => Assert.Equal(U, r.Status));
        }
        Assert.Equal((36, 12, 30), (counts[M], counts[X], counts[U]));

        // Native CLI for each origin: byte-identical repeat, no overwrite; Phase 4 and internal bindings never cross routes.
        var (good, goodIndex, _) = byCaller["CrossTwoCallers#1"];
        var writer = new StringWriter(); var error = new StringWriter();
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var bindingsPath = Path.Combine(temp.Path, "bindings.json");
        File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(new SqlServerInternalHelperResultBindings(SqlServerInternalHelperResultEvidence.BindingsVersion,
            good.Repo, good.CommitSha, [Binding(good, goodIndex, P4.Other)])));
        foreach (var origin in P4.Origins)
        {
            var (json, schema, sources) = P4.Origin(origin, null, good.Repo, good.CommitSha);
            var dir = Path.Combine(temp.Path, origin);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "schema.json"), schema);
            File.WriteAllText(Path.Combine(dir, "origin.json"), json);
            foreach (var (file, text) in sources) File.WriteAllText(Path.Combine(dir, file), text);
            string[] originOptions = origin == "result-schema" ? ["--declarations", Path.Combine(dir, "origin.json")] : ["--input", Path.Combine(dir, "origin.json"), "--source-root", dir];
            var command = new[] { "sqlserver-internal-helper-result", "compare", "--origin", origin }.Concat(originOptions).Concat(new[] { "--schema", Path.Combine(dir, "schema.json"),
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(dir, "report") }).ToArray();
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(dir, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "internal-helper-result-column-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "report", file)), File.ReadAllBytes(Path.Combine(dir, "repeat", file)));
            var readback = SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultComparisonReport>(File.ReadAllText(Path.Combine(dir, "report", "internal-helper-result-column-comparison.json")));
            Assert.Equal([M], readback.Comparisons.Where(r => r.FactId == good.FactId && r.CallerIndex == goodIndex).Select(r => r.Status));
            Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<JsonElement>(schema)), readback.SchemaEvidenceSha256);
            var markdown = File.ReadAllText(Path.Combine(dir, "report", "report.md"));
            foreach (var reason in new[] { "binding-missing", "helper-reference-escape", "helper-linked-document-unsupported", "helper-private-phase4-scope", $"{language}/Callers.{e}" })
                Assert.Contains(reason, markdown);
        }
        var phase4Bindings = Path.Combine(temp.Path, "phase4-bindings.json");
        File.WriteAllText(phase4Bindings, SqlServerSchemaEvidence.Json(new SqlServerHelperResultBindings(SqlServerHelperResultEvidence.BindingsVersion, good.Repo, good.CommitSha,
            [new(good.FactId, goodIndex, "dbo.ReadOther", P4.Other, SqlServerHelperResultEvidence.HelperResultDigest(good, goodIndex))])));
        var jsonDir = Path.Combine(temp.Path, "result-schema");
        string[] Route(string route, string bindings, string output) => [route, "compare", "--origin", "result-schema",
            "--declarations", Path.Combine(jsonDir, "origin.json"), "--schema", Path.Combine(jsonDir, "schema.json"), "--bindings", bindings, "--facts", factsPath,
            "--repo", good.Repo, "--revision", good.CommitSha, "--out", output];
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-internal-helper-result", phase4Bindings, Path.Combine(temp.Path, "x1")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-helper-result", bindingsPath, Path.Combine(temp.Path, "x2")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-dataset-result", bindingsPath, Path.Combine(temp.Path, "x3")), writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "x1")) || Directory.Exists(Path.Combine(temp.Path, "x2")) || Directory.Exists(Path.Combine(temp.Path, "x3")));
    }
}
