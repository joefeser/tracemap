using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;
using TraceMap.Storage;
using P4 = TraceMap.Tests.SqlServerHelperResultEvidenceTests;
namespace TraceMap.Tests;
/// <summary>Native Field&lt;T&gt; read fixture matrix; mirrors design.md and scripts/sqlserver-field-read-evidence/smoke.py.</summary>
public sealed class SqlServerFieldReadNativeTests
{
    private const string M = "bounded-match", X = "mismatch", U = "unknown", FieldGap = "datarow-field-extension-unsupported";
    // Core cells (authored before running the extractor): statuses and shape variant, against all three origins.
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreFillCases = new()
    {
        ["TableFieldName"] = ([M], null), ["TableFieldOrdinal"] = ([M], null), ["TableFieldRowLocal"] = ([M], null), ["TableFieldForeach"] = ([M], null),
        ["TableFieldMixed"] = ([M, M], null), ["TableFieldMissing"] = ([X], null), ["TableFieldOrdinalOutOfRange"] = ([X], null),
        ["TableFieldCaseOnly"] = ([U], null), ["TableFieldDynamic"] = ([U], null), ["TableFieldRenamed"] = ([X], "renamed"),
        ["TableFieldIncomplete"] = ([U], "incomplete"), ["SetFieldName"] = ([M], null), ["SetFieldTableLocal"] = ([M], null), ["SetFieldMissing"] = ([X], null)
    };
    private static readonly Dictionary<string, (string[] Statuses, string? Variant)> CoreSiteCases = new()
    {
        ["HelperFieldName"] = ([M], null), ["HelperFieldOtherProcedure"] = ([M], null), ["HelperFieldMissing"] = ([X], null),
        ["InternalFieldName"] = ([M], null), ["InternalFieldOrdinal"] = ([M], null), ["InternalFieldSet"] = ([M], null), ["InternalFieldMissing"] = ([X], null)
    };
    // State cells keyed by the Fill's enclosing method: origin gap, or "site:<reason>" for a bounded state whose one site has no Field read.
    private static readonly Dictionary<string, string> StateCases = new()
    {
        ["TableFieldColumn"] = "datatable:datacolumn-access-unsupported", ["TableFieldVersion"] = "datatable:row-version-access-unsupported",
        ["TableSetField"] = "datatable:datatable-mutation", ["TableFieldAsEnumerable"] = "datatable:ambiguous-row-ownership",
        ["TableFieldStatic"] = "datatable:datarow-field-static-call-unsupported", ["TableFieldUnbound"] = "site:field-read-absent",
        ["TableNoField"] = "site:field-read-absent", ["SetFieldLaterTable"] = "dataset:dataset-later-table-unsupported", ["ReadBody"] = "helper:helper-body-unsupported"
    };
    // Unchanged Phase 2/3/4 gap for the same input (null = bounded); every other Field cell keeps FieldGap.
    private static readonly Dictionary<string, string?> BaseGaps = new()
    {
        ["TableSetField"] = "datatable-mutation", ["TableFieldAsEnumerable"] = "ambiguous-row-ownership", ["TableFieldUnbound"] = null, ["TableNoField"] = null,
        ["SetFieldLaterTable"] = "dataset-later-table-unsupported"
    };
    private static readonly Dictionary<string, string> SiteCases = new()
    {
        ["HelperFieldColumn"] = "datacolumn-access-unsupported", ["InternalFieldVersion"] = "row-version-access-unsupported", ["InternalNoField"] = "field-read-absent"
    };
    private static readonly Dictionary<string, (string Origin, string[] Sites)> HelperFills = new()
    {
        ["ReadPrivate"] = ("helper", ["HelperFieldName", "HelperFieldOtherProcedure", "HelperFieldMissing", "HelperFieldColumn"]),
        ["ReadShared"] = ("internal-helper", ["InternalFieldName", "InternalFieldOrdinal", "InternalFieldMissing", "InternalFieldVersion", "InternalNoField"]),
        ["ReadSharedSet"] = ("internal-helper", ["InternalFieldSet"])
    };
    private static string RepoRoot()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        return repo!.FullName;
    }
    private static SqlServerFieldReadState State(CodeFact fact) => SqlServerSchemaEvidence.Read<SqlServerFieldReadState>(fact.Properties["sqlServerFieldReadState"]);
    private static SqlServerFieldReadBinding Binding(CodeFact fact, int site, SqlServerObjectIdentity identity)
        => new(fact.FactId, site, identity.Schema + "." + identity.Procedure, identity, SqlServerFieldReadEvidence.FieldReadDigest(fact, site));
    private static SqlServerFieldReadComparisonReport CompareOrigin(string origin, string? variant, SqlServerFieldReadBindings bindings,
        IReadOnlyList<CodeFact> facts, string repo, string revision)
    {
        var (json, schema, sources) = P4.Origin(origin, variant, repo, revision);
        return origin switch
        {
            "result-schema" => SqlServerFieldReadEvidence.CompareResultSchema(schema, json, bindings, facts, repo, revision),
            "ddl-result" => SqlServerFieldReadEvidence.CompareDdlResult(schema, json, sources, bindings, facts, repo, revision),
            _ => SqlServerFieldReadEvidence.CompareDdlTableResult(schema, json, sources, bindings, facts, repo, revision)
        };
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_matrix_binds_field_reads_on_every_origin_and_keeps_phase2_to_5_unchanged(string language)
    {
        var sample = Path.Combine(RepoRoot(), "samples/sqlserver-field-read-evidence");
        var e = language == "vb" ? "vb" : "cs";
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "scan"),
            ProjectPaths: [Path.Combine(sample, language, language == "vb" ? "FieldReads.vbproj" : "FieldReads.csproj")]));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.DatabaseOperationCandidate && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        var fills = calls.Where(f => f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill")
            .OrderBy(f => f.Evidence.FilePath, StringComparer.Ordinal).ThenBy(f => f.Evidence.StartLine).ToArray();
        Assert.Equal(26, fills.Length);
        Assert.All(calls, f => Assert.Equal(f.Properties.GetValueOrDefault("operationKind") == "data-adapter-fill", f.Properties.ContainsKey("sqlServerFieldReadState")));
        var byFill = P4.Keyed(fills.Select(f => (P4.MemberName.Match(f.SourceSymbol!).Groups[1].Value, f)));
        Assert.Equal(CoreFillCases.Keys.Concat(StateCases.Keys).Concat(HelperFills.Keys).Order(StringComparer.Ordinal), byFill.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, fact) in byFill)
        {
            var state = State(fact);
            Assert.Equal("sqlserver-field-read/1", state.Version);
            var table = SqlServerSchemaEvidence.Read<SqlServerDataTableState>(fact.Properties["sqlServerDataTableState"]);
            var set = SqlServerSchemaEvidence.Read<SqlServerDataSetState>(fact.Properties["sqlServerDataSetState"]);
            var phase4 = SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(fact.Properties["sqlServerHelperResultState"]);
            if (HelperFills.TryGetValue(key, out var helper))
            {
                Assert.True(state.Coverage == "bounded" && state.Origin == helper.Origin, $"{language}:{key}:{state.Gap}");
                Assert.Equal(helper.Sites, state.Sites.Select(s => P4.MemberName.Match(s.Caller!).Groups[1].Value));
                Assert.Equal(helper.Origin == "helper" ? "bounded" : "unknown", phase4.Coverage);
                continue;
            }
            var origin = key.StartsWith("Set", StringComparison.Ordinal) ? "dataset" : "datatable";
            var (baseCoverage, baseGap) = key == "ReadBody" ? (phase4.Coverage, phase4.Gap) : origin == "dataset" ? (set.Coverage, set.Gap) : (table.Coverage, table.Gap);
            var expectedBase = BaseGaps.TryGetValue(key, out var b) ? b : FieldGap;
            Assert.True(expectedBase is null ? baseCoverage == "bounded" : baseCoverage == "unknown" && baseGap == expectedBase, $"{language}:{key}:base:{baseGap}");
            var expected = StateCases.GetValueOrDefault(key);
            if (expected is null || expected.StartsWith("site:", StringComparison.Ordinal))
            {
                Assert.True(state.Coverage == "bounded" && state.Origin == origin && state.OriginGaps.Count == 0 && state.Sites.Count == 1, $"{language}:{key}:{state.Gap}");
                var site = state.Sites[0];
                Assert.Null(site.Path);
                Assert.Equal(key, P4.MemberName.Match(site.Caller!).Groups[1].Value);
                if (expected is null) Assert.True(site.Coverage == "bounded" && site.Accesses.Any(a => a.Member == "Field" && a.TypeArgument is not null), $"{language}:{key}");
                else Assert.True(site.Coverage == "unknown" && site.Gap == expected[5..] && site.Accesses.Count == 0, $"{language}:{key}:{site.Gap}");
            }
            else
            {
                Assert.True(state.Coverage == "unknown" && state.Gap == "field-read-origin-unbound" && state.Sites.Count == 0, $"{language}:{key}:{state.Gap}");
                Assert.Contains(expected, state.OriginGaps);
            }
        }
        var typeArguments = State(byFill["TableFieldRowLocal"]).Sites[0].Accesses.Concat(State(byFill["TableFieldForeach"]).Sites[0].Accesses).Select(a => a.TypeArgument);
        Assert.Equal(["System.Nullable[System.Int32]", "System.Decimal"], typeArguments);
        var bySite = P4.Keyed(HelperFills.Keys.SelectMany(h => State(byFill[h]).Sites.Select((s, i) => (P4.MemberName.Match(s.Caller!).Groups[1].Value, (Fact: byFill[h], Index: i, Site: s)))));
        Assert.Equal(CoreSiteCases.Keys.Concat(SiteCases.Keys).Order(StringComparer.Ordinal), bySite.Keys.Order(StringComparer.Ordinal));
        foreach (var (key, (fact, index, site)) in bySite)
        {
            var origin = State(fact).Origin;
            Assert.Equal(origin == "internal-helper" ? $"{language}/Callers.{e}" : null, site.Path);
            var callers = origin == "internal-helper"
                ? SqlServerSchemaEvidence.Read<SqlServerInternalHelperResultState>(fact.Properties["sqlServerInternalHelperResultState"]).Callers.Select(c => (c.Coverage, c.Gap)).ToArray()
                : SqlServerSchemaEvidence.Read<SqlServerHelperResultState>(fact.Properties["sqlServerHelperResultState"]).Callers.Select(c => (c.Coverage, c.Gap)).ToArray();
            Assert.Equal(key == "InternalNoField" ? ("bounded", (string?)null) : ("unknown", FieldGap), callers[index]);
            var expected = SiteCases.GetValueOrDefault(key);
            if (expected is null) Assert.True(site.Coverage == "bounded" && site.Accesses.Any(a => a.Member == "Field"), $"{language}:{key}:{site.Gap}");
            else Assert.True(site.Coverage == "unknown" && site.Gap == expected && site.Accesses.Count == 0, $"{language}:{key}:{site.Gap}");
        }

        var counts = new Dictionary<string, int> { [M] = 0, [X] = 0, [U] = 0 };
        var plan = CoreFillCases.SelectMany(c => P4.Origins.Select(o => (Kind: "fill", c.Key, Origin: o, c.Value.Variant, c.Value.Statuses)))
            .Concat(CoreSiteCases.SelectMany(c => P4.Origins.Select(o => (Kind: "site", c.Key, Origin: o, c.Value.Variant, c.Value.Statuses))))
            .Concat(StateCases.Keys.Select(k => (Kind: "fill", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U })))
            .Concat(SiteCases.Keys.Select(k => (Kind: "site", Key: k, Origin: "ddl-table-result", Variant: (string?)null, Statuses: new[] { U }))).ToArray();
        Assert.Equal(75, plan.Length);
        foreach (var (kind, key, origin, variant, expected) in plan)
        {
            var (fact, index) = kind == "site" ? (bySite[key].Fact, (int?)bySite[key].Index) : (byFill[key], State(byFill[key]).Sites.Count == 1 ? 0 : (int?)null);
            var identity = key == "HelperFieldOtherProcedure" ? P4.Other : P4.Items;
            var bindings = new SqlServerFieldReadBindings(SqlServerFieldReadEvidence.BindingsVersion, fact.Repo, fact.CommitSha,
                index is { } i ? [Binding(fact, i, identity)] : []);
            var report = CompareOrigin(origin, variant, bindings, calls, fact.Repo, fact.CommitSha);
            var rows = report.Comparisons.Where(r => r.FactId == fact.FactId && r.SiteIndex == index).ToArray();
            Assert.True(rows.Select(r => r.Status).SequenceEqual(expected), $"{language}:{key}:{origin}:" + SqlServerSchemaEvidence.Json(rows));
            var gap = kind == "site" ? SiteCases.GetValueOrDefault(key) : StateCases.GetValueOrDefault(key) is { } s ? s.StartsWith("site:", StringComparison.Ordinal) ? s[5..] : s : null;
            if (gap is not null) Assert.Contains(gap, Assert.Single(rows).Reasons);
            Assert.All(rows.Where(r => r.Access?.Member == "Field"), r => Assert.Contains("field-type-argument-not-compared:" + r.Access!.TypeArgument, r.UncomparedFacets));
            foreach (var row in rows) counts[row.Status]++;
            Assert.All(report.Comparisons.Where(r => !rows.Contains(r)), r => Assert.Equal(U, r.Status));
        }
        Assert.Equal((39, 18, 21), (counts[M], counts[X], counts[U]));

        // Native CLI for each origin: byte-identical repeat, no overwrite; field bindings never enter another route and vice versa.
        var (good, goodIndex, _) = bySite["InternalFieldOrdinal"];
        var writer = new StringWriter(); var error = new StringWriter();
        var factsPath = Path.Combine(temp.Path, "facts.ndjson"); await JsonlFactWriter.WriteAsync(factsPath, calls);
        var bindingsPath = Path.Combine(temp.Path, "bindings.json");
        File.WriteAllText(bindingsPath, SqlServerSchemaEvidence.Json(new SqlServerFieldReadBindings(SqlServerFieldReadEvidence.BindingsVersion,
            good.Repo, good.CommitSha, [Binding(good, goodIndex, P4.Items)])));
        foreach (var origin in P4.Origins)
        {
            var (json, schema, sources) = P4.Origin(origin, null, good.Repo, good.CommitSha);
            var dir = Path.Combine(temp.Path, origin);
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, "schema.json"), schema);
            File.WriteAllText(Path.Combine(dir, "origin.json"), json);
            foreach (var (file, text) in sources) File.WriteAllText(Path.Combine(dir, file), text);
            string[] originOptions = origin == "result-schema" ? ["--declarations", Path.Combine(dir, "origin.json")] : ["--input", Path.Combine(dir, "origin.json"), "--source-root", dir];
            var command = new[] { "sqlserver-field-read", "compare", "--origin", origin }.Concat(originOptions).Concat(new[] { "--schema", Path.Combine(dir, "schema.json"),
                "--bindings", bindingsPath, "--facts", factsPath, "--repo", good.Repo, "--revision", good.CommitSha, "--out", Path.Combine(dir, "report") }).ToArray();
            Assert.True(await TraceMapCommand.RunAsync(command, writer, error) == 0, error.ToString());
            Assert.NotEqual(0, await TraceMapCommand.RunAsync(command, writer, error));
            command[^1] = Path.Combine(dir, "repeat");
            Assert.Equal(0, await TraceMapCommand.RunAsync(command, writer, error));
            foreach (var file in new[] { "field-read-column-comparison.json", "report.md" })
                Assert.Equal(File.ReadAllBytes(Path.Combine(dir, "report", file)), File.ReadAllBytes(Path.Combine(dir, "repeat", file)));
            var readback = SqlServerSchemaEvidence.Read<SqlServerFieldReadComparisonReport>(File.ReadAllText(Path.Combine(dir, "report", "field-read-column-comparison.json")));
            Assert.Equal([M], readback.Comparisons.Where(r => r.FactId == good.FactId && r.SiteIndex == goodIndex).Select(r => r.Status));
            Assert.Equal(SqlServerSchemaEvidence.Digest(SqlServerSchemaEvidence.Read<JsonElement>(schema)), readback.SchemaEvidenceSha256);
            var markdown = File.ReadAllText(Path.Combine(dir, "report", "report.md"));
            foreach (var reason in new[] { "binding-missing", "field-read-absent", "datatable:datacolumn-access-unsupported", "helper:helper-body-unsupported", $"{language}/Callers.{e}" })
                Assert.Contains(reason, markdown);
        }
        var internalBindings = Path.Combine(temp.Path, "internal-bindings.json");
        File.WriteAllText(internalBindings, SqlServerSchemaEvidence.Json(new SqlServerInternalHelperResultBindings(SqlServerInternalHelperResultEvidence.BindingsVersion,
            good.Repo, good.CommitSha, [new(good.FactId, goodIndex, "dbo.ReadItems", P4.Items, SqlServerInternalHelperResultEvidence.InternalHelperResultDigest(good, goodIndex))])));
        var jsonDir = Path.Combine(temp.Path, "result-schema");
        string[] Route(string route, string bindings, string output) => [route, "compare", "--origin", "result-schema",
            "--declarations", Path.Combine(jsonDir, "origin.json"), "--schema", Path.Combine(jsonDir, "schema.json"), "--bindings", bindings, "--facts", factsPath,
            "--repo", good.Repo, "--revision", good.CommitSha, "--out", output];
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-field-read", internalBindings, Path.Combine(temp.Path, "x1")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-internal-helper-result", bindingsPath, Path.Combine(temp.Path, "x2")), writer, error));
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Route("sqlserver-datatable-result", bindingsPath, Path.Combine(temp.Path, "x3")), writer, error));
        Assert.False(Directory.Exists(Path.Combine(temp.Path, "x1")) || Directory.Exists(Path.Combine(temp.Path, "x2")) || Directory.Exists(Path.Combine(temp.Path, "x3")));
    }
}
