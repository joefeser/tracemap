using System.Security.Cryptography;
using TraceMap.Core;
using TraceMap.Cli;
using Xunit.Abstractions;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleExtractorTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_scan_emits_guarded_lifecycle_with_all_invoked_body_links(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var fixture = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var inputs = new[] { "end-to-end", "wrapper" }.Select(role => Path.Combine(fixture, role, "bin", config,
            "net48", $"PublicSql.{language}.{role}.dll")).ToArray();
        var scanDirectory = IlBodyEvidenceExtractorShared.TempOutput();
        using var scanOutput = new StringWriter();
        using var scanError = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(["scan", "--repo", Path.Combine(fixture, "end-to-end"),
            "--out", scanDirectory, "--il-body-evidence", "--compiled-input", inputs[0], "--compiled-input", inputs[1]],
            scanOutput, scanError) == 0, scanError.ToString());
        var scan = (Manifest: SqlServerSchemaEvidence.Read<ScanManifest>(File.ReadAllText(Path.Combine(scanDirectory, "scan-manifest.json"))),
            Facts: File.ReadLines(Path.Combine(scanDirectory, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray());
        var replayDirectory = IlBodyEvidenceExtractorShared.TempOutput();
        Assert.True(await TraceMapCommand.RunAsync(["scan", "--repo", Path.Combine(fixture, "end-to-end"),
            "--out", replayDirectory, "--il-body-evidence", "--compiled-input", inputs[0], "--compiled-input", inputs[1]],
            scanOutput, scanError) == 0, scanError.ToString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(scanDirectory, "facts.ndjson")),
            File.ReadAllBytes(Path.Combine(replayDirectory, "facts.ndjson")));
        var candidates = scan.Facts.Where(f => f.FactType == FactTypes.ManagedIlSqlLifecycleCandidate).ToArray();
        Assert.NotEmpty(candidates);
        var compiledBodies = Bodies(language);
        foreach (var (method, expectedExecutions) in new[] { ("ArrayParameterRead", 1), ("ArrayPropertyRead", 1), ("ArraySeparateExecutions", 2) })
        {
            var body = Assert.Single(compiledBodies, b => b.Observation.ObjectBody?.Name == method);
            var candidate = Assert.Single(candidates, c => c.TargetSymbol == body.Observation.BodyIdentity);
            var result = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(candidate.Properties["lifecycle"]);
            Assert.Equal(expectedExecutions, result.Executions.Count);
            Assert.Equal($"execution:{expectedExecutions - 1}", result.Returned!.Value.Execution);
        }
        foreach (var method in new[] { "ArrayWrongElement", "ArrayWrongCustomType", "ArrayRepeatedEnumeration", "ArrayUnsupportedCollection",
            "ArrayUnsupportedMutation", "ArrayMutatedElement", "ArrayEscaped", "ArrayTooLarge", "ArrayInvalidAttachment", "ArrayIncompleteRead" })
        {
            var body = Assert.Single(compiledBodies, b => b.Observation.ObjectBody?.Name == method);
            Assert.DoesNotContain(candidates, c => c.TargetSymbol == body.Observation.BodyIdentity);
            Assert.Contains(scan.Facts, f => f.TargetSymbol == body.Observation.BodyIdentity
                && f.RuleId == RuleIds.DotNetIlSqlLifecycle && f.FactType == FactTypes.AnalysisGap);
        }
        var scoreBody = Assert.Single(Bodies(language), b => b.Observation.ObjectBody?.Name == "Score").Observation.BodyIdentity;
        var score = candidates.Where(f => f.TargetSymbol == scoreBody).ToArray();
        Assert.Equal(2, score.Length);
        Assert.Equal(new[] { "argument:0=false", "argument:0=true" }, score.Select(f => f.Properties["guard"]).Order().ToArray());
        var byId = scan.Facts.ToDictionary(f => f.FactId);
        foreach (var candidate in candidates)
        {
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
            Assert.Equal(RuleIds.DotNetIlSqlLifecycle, candidate.RuleId);
            Assert.Equal(scan.Manifest.IlBodyProvenance!.GeneratorSha256, candidate.Properties["ilGeneratorSha256"]);
            using var links = System.Text.Json.JsonDocument.Parse(candidate.Properties["bodyLinks"]);
            foreach (var link in links.RootElement.EnumerateArray())
            {
                var body = byId[link.GetProperty("bodyFactId").GetString()!];
                Assert.Equal(FactTypes.ManagedIlBodyDeclared, body.FactType);
                Assert.Equal(body.TargetSymbol, link.GetProperty("body").GetString());
                Assert.Equal(body.Properties["rawFileSha256"], link.GetProperty("binary").GetString());
            }
            var state = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(candidate.Properties["lifecycle"]);
            Assert.Empty(state.Gaps); Assert.NotNull(state.Returned); Assert.NotEmpty(state.Bodies!);
            Assert.DoesNotContain("public.first", candidate.Properties["lifecycle"], StringComparison.Ordinal);
        }
        var firstIdentity = new SqlServerObjectIdentity("synthetic", "sample", "public", "first");
        var secondIdentity = new SqlServerObjectIdentity("synthetic", "sample", "public", "second");
        var declarations = new SqlServerResultDeclarations("sqlserver-result-declarations/1", scan.Manifest.RepoName,
            scan.Manifest.CommitSha, "independent-synthetic-result-declarations", "ordinal-exact-no-aliases",
            [new(firstIdentity, "schema/first.sql", 1, 3, "first", "complete", null,
                [new("Name", 0, 1, null), new("Score", 1, 2, null)]),
             new(secondIdentity, "schema/second.sql", 1, 2, "first", "complete", null, [new("Count", 0, 1, null)])]);
        var schema = SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(declarations));
        var bound = score.Select(fact =>
        {
            var state = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(fact.Properties["lifecycle"]);
            var read = state.Returned!;
            return new SqlServerLifecycleBinding(fact.FactId, SqlServerCompiledLifecycleEvidence.EvidenceDigest(fact),
                read.Table, "Score", read.Site.Body, read.Site.Binary, read.Site.Offset, read.Site.Invocation,
                [new("execution:0", "public.first", firstIdentity), new("execution:1", "public.second", secondIdentity)]);
        }).ToArray();
        var bindings = new SqlServerLifecycleBindings("sqlserver-compiled-lifecycle-bindings/1", scan.Manifest.RepoName, scan.Manifest.CommitSha, bound);
        var report = SqlServerCompiledLifecycleEvidence.Compare(schema, bindings, scan.Facts, scan.Manifest.RepoName, scan.Manifest.CommitSha);
        Assert.Equal(2, report.Comparisons.Count);
        Assert.All(report.Comparisons, row => Assert.Equal("match", row.Status));
        Assert.Equal("application", Assert.Single(report.Comparisons, r => r.Guard == "argument:0=true").ValueOrigin);
        Assert.Equal("sql", Assert.Single(report.Comparisons, r => r.Guard == "argument:0=false").ValueOrigin);
        foreach (var substituted in new[]
        {
            bound[0] with { ConsumerTable = "allocation:wrong" },
            bound[0] with { ReadBinary = new string('0', 64) },
            bound[0] with { EvidenceSha256 = new string('0', 64) },
            bound[0] with { Executions = [new("execution:0", "public.second", secondIdentity), new("execution:1", "public.first", firstIdentity)] }
        })
            Assert.Throws<ArgumentException>(() => SqlServerCompiledLifecycleEvidence.Compare(schema,
                bindings with { Candidates = [substituted] }, scan.Facts, scan.Manifest.RepoName, scan.Manifest.CommitSha));
        var missing = declarations with { Shapes = [declarations.Shapes[0] with { Columns = [new("Name", 0, 1, null)] }, declarations.Shapes[1]] };
        Assert.All(SqlServerCompiledLifecycleEvidence.Compare(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(missing)), bindings,
            scan.Facts, scan.Manifest.RepoName, scan.Manifest.CommitSha).Comparisons, row => Assert.Equal("mismatch", row.Status));
        var incomplete = missing with { Shapes = [missing.Shapes[0] with { Completeness = "unknown", UnknownReason = "partial-shape" }, missing.Shapes[1]] };
        Assert.All(SqlServerCompiledLifecycleEvidence.Compare(SqlServerResultEvidence.Produce(SqlServerSchemaEvidence.Json(incomplete)), bindings,
            scan.Facts, scan.Manifest.RepoName, scan.Manifest.CommitSha).Comparisons, row => Assert.Equal("unknown", row.Status));
        var directory = IlBodyEvidenceExtractorShared.TempOutput();
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "declarations.json"), SqlServerSchemaEvidence.Json(declarations));
        File.WriteAllText(Path.Combine(directory, "schema.json"), SqlServerSchemaEvidence.Json(schema));
        File.WriteAllText(Path.Combine(directory, "bindings.json"), SqlServerSchemaEvidence.Json(bindings));
        File.WriteAllLines(Path.Combine(directory, "facts.ndjson"), scan.Facts.Select(SqlServerSchemaEvidence.Json));
        string[] Command(string destination) => ["sqlserver-compiled-lifecycle", "compare",
            "--declarations", Path.Combine(directory, "declarations.json"), "--schema", Path.Combine(directory, "schema.json"),
            "--bindings", Path.Combine(directory, "bindings.json"), "--facts", Path.Combine(directory, "facts.ndjson"),
            "--repo", scan.Manifest.RepoName, "--revision", scan.Manifest.CommitSha, "--out", Path.Combine(directory, destination)];
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(Command("report1"), stdout, stderr) == 0, stderr.ToString());
        Assert.True(await TraceMapCommand.RunAsync(Command("report2"), stdout, stderr) == 0, stderr.ToString());
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(Command("report1"), stdout, stderr));
        Assert.Equal(File.ReadAllText(Path.Combine(directory, "report1", "compiled-lifecycle-comparison.json")),
            File.ReadAllText(Path.Combine(directory, "report2", "compiled-lifecycle-comparison.json")));
        Assert.Contains("Coverage: partial", File.ReadAllText(Path.Combine(directory, "report1", "report.md")), StringComparison.Ordinal);

        var ownerDeclarations = declarations with
        {
            Version = "sqlserver-owner-result-declarations/1",
            Origin = SqlServerOwnerResultEvidence.Origin
        };
        File.WriteAllText(Path.Combine(directory, "owner.json"), SqlServerSchemaEvidence.Json(ownerDeclarations));
        string[] ProduceOwner() => ["sqlserver-compiled-lifecycle", "produce-owner-schema",
            "--declarations", Path.Combine(directory, "owner.json"), "--out", Path.Combine(directory, "owner-schema.json")];
        Assert.True(await TraceMapCommand.RunAsync(ProduceOwner(), stdout, stderr) == 0, stderr.ToString());
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(ProduceOwner(), stdout, stderr));
        string[] OwnerCommand(string destination)
        {
            var command = Command(destination);
            command[1] = "compare-owner";
            command[3] = Path.Combine(directory, "owner.json");
            command[5] = Path.Combine(directory, "owner-schema.json");
            return command;
        }
        Assert.True(await TraceMapCommand.RunAsync(OwnerCommand("owner-report1"), stdout, stderr) == 0, stderr.ToString());
        Assert.True(await TraceMapCommand.RunAsync(OwnerCommand("owner-report2"), stdout, stderr) == 0, stderr.ToString());
        var ownerReportJson = File.ReadAllText(Path.Combine(directory, "owner-report1", "compiled-lifecycle-comparison.json"));
        Assert.Equal(ownerReportJson, File.ReadAllText(Path.Combine(directory, "owner-report2", "compiled-lifecycle-comparison.json")));
        var ownerReport = SqlServerSchemaEvidence.Read<SqlServerLifecycleReport>(ownerReportJson);
        Assert.Equal(SqlServerOwnerResultEvidence.Origin, ownerReport.SchemaOrigin);
        Assert.All(ownerReport.Comparisons, row => Assert.Equal("match", row.Status));
        Assert.Contains(SqlServerOwnerResultEvidence.Limitation, ownerReport.Limitations);
        var wrongRoute = OwnerCommand("wrong-route");
        wrongRoute[1] = "compare";
        Assert.NotEqual(0, await TraceMapCommand.RunAsync(wrongRoute, stdout, stderr));
        Assert.False(Directory.Exists(Path.Combine(directory, "wrong-route")));

        // Independently authored scenario/column/execution matrix. Extraction supplies locations,
        // never the expected command identity, column spelling, result or execution order.
        (string Method, string Column, string Status, string ValueExecution, string[] Commands, int Guards)[] matrix =
        {
            ("First", "Name", "match", "execution:0", new[] { "first", "second" }, 2),
            ("Second", "Count", "match", "execution:1", new[] { "first", "second" }, 2),
            ("Typed", "Name", "match", "execution:0", new[] { "first", "second" }, 2),
            ("Score", "Score", "match", "execution:0", new[] { "first", "second" }, 2),
            ("Missing", "Absent", "mismatch", "execution:0", new[] { "first", "second" }, 1),
            ("Refresh", "Score", "match", "execution:2", new[] { "first", "second", "first" }, 1),
            ("Reload", "Score", "match", "execution:2", new[] { "first", "second", "first", "second" }, 1),
            ("DifferentRefresh", "Score", "match", "execution:2", new[] { "first", "second", "second" }, 1)
        };
        var allBindings = new List<SqlServerLifecycleBinding>();
        var expected = new Dictionary<string, (string Method, string Status, string Execution)>();
        var bodyInputs = Bodies(language);
        foreach (var item in matrix)
        {
            var methodBody = Assert.Single(bodyInputs, b => b.Input.Role == "end-to-end" && b.Observation.ObjectBody?.Name == item.Method).Observation.BodyIdentity;
            var selected = candidates.Where(c => c.TargetSymbol == methodBody).ToArray();
            Assert.Equal(item.Guards, selected.Length);
            foreach (var fact in selected)
            {
                var state = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(fact.Properties["lifecycle"]);
                var read = state.Returned!;
                Assert.Equal(item.Commands.Length, state.Executions.Count);
                allBindings.Add(new(fact.FactId, SqlServerCompiledLifecycleEvidence.EvidenceDigest(fact),
                    read.Table, item.Column, read.Site.Body, read.Site.Binary, read.Site.Offset, read.Site.Invocation,
                    item.Commands.Select((command, index) => new SqlServerLifecycleExecutionBinding($"execution:{index}",
                        "public." + command, command == "first" ? firstIdentity : secondIdentity)).ToArray()));
                expected.Add(fact.FactId, (item.Method, item.Status, item.ValueExecution));
            }
        }
        Assert.Equal(12, allBindings.Count);
        foreach (var unsupported in new[] { "Escaped", "CrossRow", "LoopCarried", "ClearedRead", "DuplicateTable", "EmptyLoop" })
        {
            var methodBody = Assert.Single(bodyInputs, b => b.Input.Role == "end-to-end" && b.Observation.ObjectBody?.Name == unsupported).Observation.BodyIdentity;
            Assert.DoesNotContain(candidates, c => c.TargetSymbol == methodBody);
            Assert.Contains(scan.Facts, f => f.TargetSymbol == methodBody && f.FactType == FactTypes.AnalysisGap
                && f.RuleId == RuleIds.DotNetIlSqlLifecycle);
        }
        File.WriteAllText(Path.Combine(directory, "bindings.json"), SqlServerSchemaEvidence.Json(bindings with { Candidates = allBindings }));
        Assert.True(await TraceMapCommand.RunAsync(Command("matrix"), stdout, stderr) == 0, stderr.ToString());
        var actual = SqlServerSchemaEvidence.Read<SqlServerLifecycleReport>(File.ReadAllText(Path.Combine(directory, "matrix", "compiled-lifecycle-comparison.json")));
        Assert.Equal(12, actual.Comparisons.Count);
        foreach (var row in actual.Comparisons)
        {
            var e = expected[row.FactId];
            Assert.Equal(e.Status, row.Status);
            var application = e.Method == "Score" && row.Guard == "argument:0=true";
            var unknownValue = e.Method is "Missing" or "DifferentRefresh";
            Assert.Equal(unknownValue ? "unknown" : application ? "application" : "sql", row.ValueOrigin);
            Assert.Equal(application || unknownValue ? null : e.Execution, row.ValueExecution);
            Assert.Equal(row.ColumnHash, row.ValueColumnHash);
        }
        SqlServerLifecycleComparison Row(string method, string guard = "unconditional") => Assert.Single(actual.Comparisons,
            r => expected[r.FactId].Method == method && r.Guard == guard);
        var first = Row("First", "argument:0=false");
        Assert.NotEqual(first.Table, Row("Second", "argument:0=false").Table);
        Assert.Equal(first.Table, Row("Refresh").Table);
        Assert.NotEqual(first.Table, Row("Reload").Table);
        Assert.Equal(new[] { "execution:0", "execution:2" }, Row("Refresh").SchemaExecutions);
        Assert.Equal("execution:2", Assert.Single(Row("Reload").SchemaExecutions));
        Assert.NotEqual(allBindings.Single(b => b.FactId == first.FactId).ReadBody,
            allBindings.Single(b => b.FactId == Row("Typed", "argument:0=false").FactId).ReadBody);
        var overlay = Row("Score", "argument:0=true");
        var failures = actual.PotentialFailures.Where(f => f.FactId == overlay.FactId).ToArray();
        var merges = failures.Where(f => f.Operation == "table-merge").ToArray();
        Assert.Equal(2, merges.Length);
        Assert.Equal("execution:0", Assert.Single(merges[1].Before, t => t.Allocation == overlay.Table).Execution);
        var second = Row("Second", "argument:0=true");
        Assert.Null(Assert.Single(merges[1].Before, t => t.Allocation == second.Table).Execution);
        var write = Assert.Single(failures, f => f.Operation == "row-write");
        Assert.True(write.MayPartiallyMutate); Assert.True(write.MayHaveCompletedEarlierRows);
        Assert.Empty(Assert.Single(write.Before, t => t.Allocation == overlay.Table).ApplicationWrittenColumns);
        Assert.NotEmpty(Assert.Single(write.NormalCompletion, t => t.Allocation == overlay.Table).ApplicationWrittenColumns);
        var projectionFailure = Assert.Single(failures, f => f.Operation == "list-add");
        Assert.True(projectionFailure.MayHaveCompletedEarlierRows);
        var beforeList = Assert.Single(projectionFailure.BeforeObjects, o => o.Kind == "list");
        var afterList = Assert.Single(projectionFailure.NormalCompletionObjects, o => o.Kind == "list");
        Assert.Equal(beforeList.Allocation, afterList.Allocation);
        Assert.Empty(beforeList.Slots);
        Assert.Single(afterList.Slots);
        Assert.All(failures, f => Assert.Contains("catch-continuation-not-executed", f.Limitation, StringComparison.Ordinal));
        output.WriteLine($"CLI matrix {language}: 12 selected alternatives, 11 schema matches/1 mismatch; 2 value origins unknown; 6 unsupported controls withheld; explicit partial failure boundaries.");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Malformed_offsets_and_substituted_type_operands_withhold_all_lineage(string language)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        foreach (var opcode in new[] { "box", "newarr", "castclass", "isinst", "invalid-offset" })
        {
            var changed = 0;
            var corrupted = bodies.Select(body => body with
            {
                Observation = body.Observation with
                {
                    ObjectBody = body.Observation.ObjectBody! with
                    {
                        Instructions = body.Observation.ObjectBody!.Instructions.Select(line =>
                        {
                            var parts = line.Split(':', 4);
                            if (opcode == "invalid-offset" && body == root) { parts[1] = "not-hex"; changed++; }
                            else if (parts[2] == opcode) { parts[3] = "t:untrusted-type"; changed++; }
                            return string.Join(':', parts);
                        }).ToArray()
                    }
                }
            }).ToArray();
            Assert.True(changed > 0);
            var changedRoot = Assert.Single(corrupted, b => b.Observation.MethodIdentity == root.Observation.MethodIdentity);
            var result = IlSqlLifecycleExtractor.Extract(changedRoot, corrupted, true, new(200_000));
            Assert.NotEmpty(result.Gaps);
            Assert.Empty(result.Executions); Assert.Empty(result.Reads); Assert.Null(result.Returned);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Unknown_guard_keeps_both_alternatives_and_failure_boundaries_are_not_atomic(string language)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        var branches = IlSqlLifecycleExtractor.ExtractGuarded(root, bodies, new(400_000));
        Assert.Equal(2, branches.Count);
        Assert.All(branches, b => { Assert.Empty(b.Result.Gaps); Assert.Equal("argument:0", b.Predicate); });
        var noOverlay = Assert.Single(branches, b => !b.Value).Result;
        var overlay = Assert.Single(branches, b => b.Value).Result;
        Assert.Equal("sql", noOverlay.Returned!.Value.Kind);
        Assert.Equal("application", overlay.Returned!.Value.Kind);
        Assert.Equal(noOverlay.Returned.SchemaExecutions, overlay.Returned.SchemaExecutions);
        var failures = Assert.IsAssignableFrom<IReadOnlyList<IlSqlLifecycleExtractor.Failure>>(overlay.Failures);
        var merges = failures.Where(f => f.Operation == "table-merge").ToArray();
        Assert.Equal(2, merges.Length);
        var firstDestination = overlay.Returned.Table;
        Assert.Equal("execution:0", Assert.Single(merges[1].Before, s => s.Allocation == firstDestination).Execution);
        var changed = Assert.Single(merges[1].NormalCompletion, after => after.Execution == "execution:1"
            && merges[1].Before.Any(before => before.Allocation == after.Allocation && before.RowsCleared));
        Assert.Null(Assert.Single(merges[1].Before, s => s.Allocation == changed.Allocation).Execution);
        var write = Assert.Single(failures, f => f.Operation == "row-write");
        Assert.True(write.MayPartiallyMutate); Assert.True(write.MayHaveCompletedEarlierRows);
        Assert.Empty(Assert.Single(write.Before, s => s.Allocation == firstDestination).Writes);
        Assert.NotEmpty(Assert.Single(write.NormalCompletion, s => s.Allocation == firstDestination).Writes);
        var projection = Assert.Single(failures, f => f.Operation == "list-add");
        Assert.True(projection.MayHaveCompletedEarlierRows);
        Assert.Equal(write.CatchHandler.Body, projection.CatchHandler.Body);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Execution_table_and_projection_identities_survive_refresh_and_reload(string language)
    {
        var bodies = Bodies(language);
        IlSqlLifecycleExtractor.Result Run(string name)
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
            Assert.Empty(result.Gaps); Assert.NotNull(result.Returned); return result;
        }
        var first = Run("First"); var second = Run("Second"); var typed = Run("Typed");
        Assert.Equal("execution:0", first.Returned!.Value.Execution);
        Assert.Equal("execution:1", second.Returned!.Value.Execution);
        Assert.NotEqual(first.Returned.Table, second.Returned.Table);
        Assert.Equal(first.Returned.ColumnHash, typed.Returned!.ColumnHash);
        Assert.NotEqual(first.Returned.Site.Body, typed.Returned.Site.Body);
        Assert.Equal("execution:0", typed.Returned.Value.Execution);
        var refreshed = Run("Refresh");
        Assert.Equal(3, refreshed.Executions.Count);
        Assert.Equal("execution:2", refreshed.Returned!.Value.Execution);
        Assert.Equal(first.Returned.Table, refreshed.Returned.Table);
        Assert.Equal(new[] { "execution:0", "execution:2" }, refreshed.Returned.SchemaExecutions);
        var reloaded = Run("Reload");
        Assert.Equal(4, reloaded.Executions.Count);
        Assert.Equal("execution:2", reloaded.Returned!.Value.Execution);
        Assert.NotEqual(first.Returned.Table, reloaded.Returned.Table);
        Assert.Equal("execution:2", Assert.Single(reloaded.Returned.SchemaExecutions));
    }

    [Theory]
    [InlineData("csharp", "ArrayParameterRead", 1)]
    [InlineData("vb", "ArrayParameterRead", 1)]
    [InlineData("csharp", "ArrayPropertyRead", 1)]
    [InlineData("vb", "ArrayPropertyRead", 1)]
    [InlineData("csharp", "ArraySeparateExecutions", 2)]
    [InlineData("vb", "ArraySeparateExecutions", 2)]
    public void ArrayList_custom_parameters_are_resolved_through_exact_wrapper_bodies(string language, string method, int executions)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == method);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Returned);
        Assert.Equal(executions, result.Executions.Count);
        Assert.Equal($"execution:{executions - 1}", result.Returned.Value.Execution);
        Assert.Equal($"execution:{executions - 1}", Assert.Single(result.Returned.SchemaExecutions));
        var customConstructor = Assert.Single(bodies, b => b.Observation.ObjectBody is { Name: ".ctor", ParameterCount: 3 } meta
            && meta.OwnerType.Contains(method == "ArrayPropertyRead" ? "PropertyParameter" : "FieldParameter", StringComparison.Ordinal));
        Assert.Contains(result.Bodies!, b => b.Body == customConstructor.Observation.BodyIdentity);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ArrayList_missing_ambiguous_and_untrusted_metadata_never_admits_a_candidate(string language)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "ArrayPropertyRead");
        var getter = Assert.Single(bodies, b => b.Observation.ObjectBody is { Name: "get_Size" } m
            && m.OwnerType.Contains("PropertyParameter", StringComparison.Ordinal));
        var missing = bodies.Where(b => b != getter).ToArray();
        var ambiguous = bodies.Concat(new[] { getter }).ToArray();
        var incomplete = bodies.Select(b => b == getter ? b with { Input = b.Input with { GapKinds = ["synthetic-incomplete-metadata"] } } : b).ToArray();
        var unknown = bodies.Select(b => b == root ? b with
        {
            Observation = b.Observation with
            {
                ObjectBody = b.Observation.ObjectBody! with
                {
                    Instructions = b.Observation.ObjectBody!.Instructions.Select(line =>
                    {
                        var parts = line.Split(':', 4);
                        return parts[2] == "ldstr" ? $"{parts[0]}:{parts[1]}:ldnull:" : line;
                    }).ToArray()
                }
            }
        } : b).ToArray();
        var forged = bodies.Select(b => b with
        {
            Observation = b.Observation with
            {
                Calls = b.Observation.Calls.Select(call => call.TargetIdentity.Contains("ArrayList", StringComparison.Ordinal)
                    ? call with { TargetIdentity = call.TargetIdentity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal) }
                    : call).ToArray()
            }
        }).ToArray();
        foreach (var variant in new[] { missing, ambiguous, incomplete, unknown, forged })
        {
            var changedRoot = Assert.Single(variant, b => b.Observation.MethodIdentity == root.Observation.MethodIdentity);
            var result = IlSqlLifecycleExtractor.Extract(changedRoot, variant, false, new(200_000));
            Assert.NotEmpty(result.Gaps); Assert.Empty(result.Executions); Assert.Empty(result.Reads); Assert.Null(result.Returned);
        }
        var limited = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(1));
        Assert.Equal("lifecycle-index-limit", Assert.Single(limited.Gaps));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void ArrayList_refusals_identify_the_failed_protocol_leg(string language)
    {
        var bodies = Bodies(language);
        foreach (var (method, gap) in new[] {
            ("ArrayUnsupportedCollection", "lifecycle-unknown-call:"),
            ("ArrayIncompleteRead", "lifecycle-collection-enumeration-incomplete"),
            ("ArrayTooLarge", "lifecycle-collection-mutated-or-limit"),
            ("ArrayMutatedElement", "lifecycle-parameter-object-mutated"),
            ("ArrayRepeatedEnumeration", "lifecycle-collection-enumerated-twice"),
            ("ArrayInvalidAttachment", "lifecycle-parameter-reused-or-limit") })
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == method);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
            Assert.StartsWith(gap, Assert.Single(result.Gaps), StringComparison.Ordinal);
            Assert.Empty(result.Executions); Assert.Empty(result.Reads); Assert.Null(result.Returned);
        }
    }

    [Theory]
    [InlineData("csharp", "ArrayUnsupportedMutation")]
    [InlineData("vb", "ArrayUnsupportedMutation")]
    [InlineData("csharp", "ArrayMutatedElement")]
    [InlineData("vb", "ArrayMutatedElement")]
    [InlineData("csharp", "ArrayEscaped")]
    [InlineData("vb", "ArrayEscaped")]
    [InlineData("csharp", "ArrayTooLarge")]
    [InlineData("vb", "ArrayTooLarge")]
    [InlineData("csharp", "ArrayInvalidAttachment")]
    [InlineData("vb", "ArrayInvalidAttachment")]
    [InlineData("csharp", "CrossRow")]
    [InlineData("csharp", "ArrayWrongElement")]
    [InlineData("vb", "ArrayWrongElement")]
    [InlineData("csharp", "ArrayWrongCustomType")]
    [InlineData("vb", "ArrayWrongCustomType")]
    [InlineData("csharp", "ArrayRepeatedEnumeration")]
    [InlineData("vb", "ArrayRepeatedEnumeration")]
    [InlineData("vb", "CrossRow")]
    [InlineData("csharp", "LoopCarried")]
    [InlineData("vb", "LoopCarried")]
    [InlineData("csharp", "Escaped")]
    [InlineData("vb", "Escaped")]
    [InlineData("csharp", "ClearedRead")]
    [InlineData("vb", "ClearedRead")]
    [InlineData("csharp", "DuplicateTable")]
    [InlineData("vb", "DuplicateTable")]
    [InlineData("csharp", "EmptyLoop")]
    [InlineData("vb", "EmptyLoop")]
    public void Unproven_loop_effects_and_escapes_discard_positive_claims(string language, string method)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Input.Role == "end-to-end" && b.Observation.ObjectBody?.Name == method);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(200_000));
        Assert.NotEmpty(result.Gaps); Assert.Empty(result.Reads); Assert.Empty(result.Executions); Assert.Null(result.Returned);
    }

    [Theory]
    [InlineData("csharp", false)]
    [InlineData("csharp", true)]
    [InlineData("vb", false)]
    [InlineData("vb", true)]
    public void Successful_symbolic_path_preserves_schema_but_tracks_overlay_value(string language, bool enabled)
    {
        var bodies = Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Score");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, enabled, new(200_000));
        Assert.Empty(result.Gaps);
        Assert.Equal(2, result.Executions.Count);
        var read = Assert.IsType<IlSqlLifecycleExtractor.Read>(result.Returned);
        Assert.Equal("execution:0", Assert.Single(read.SchemaExecutions));
        Assert.Equal(enabled ? "application" : "sql", read.Value.Kind);
        Assert.Equal(enabled ? null : "execution:0", read.Value.Execution);
        Assert.Equal(enabled, read.Value.Assignment is not null);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Framework_catalog_requires_signed_identity_and_admitted_generic_owner(string language)
    {
        var bodies = Bodies(language);
        var owners = bodies.Select(b => b.Observation.ObjectBody!.OwnerType).ToArray();
        var calls = bodies.SelectMany(b => b.Observation.Calls).Select(c => c.TargetIdentity).Distinct().ToArray();
        var constructor = Assert.Single(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "list-new");
        Assert.Null(IlSqlLifecycleApis.Resolve(constructor, []));
        Assert.Null(IlSqlLifecycleApis.Resolve(constructor.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal), owners));
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "row-write");
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "rows-add");
        Assert.Contains(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == "enumerator-next");
        foreach (var api in new[] { "arraylist-new", "arraylist-add", "arraylist-enumerator" })
        {
            var identity = Assert.Single(calls, c => IlSqlLifecycleApis.Resolve(c, owners) == api);
            foreach (var forged in new[] {
                identity.Replace("b77a5c561934e089", "0000000000000000", StringComparison.Ordinal),
                identity.Replace("4.0.0.0", "3.0.0.0", StringComparison.Ordinal),
                identity.Replace("ArrayList", "ArrayLisp", StringComparison.Ordinal),
                identity.Replace("hasThis:true", "hasThis:false", StringComparison.Ordinal) })
                Assert.Null(IlSqlLifecycleApis.Resolve(forged, owners));
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Lifecycle_bodies_have_dual_reader_agreed_control_flow(string language)
    {
        var bodies = Bodies(language);
        var load = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "Load");
        Assert.NotEmpty(load.Observation.ObjectBody!.ExceptionRegions!);
        foreach (var body in bodies.Where(b => b.Input.Role == "end-to-end"))
        {
            output.WriteLine(body.Observation.ObjectBody!.Name);
            foreach (var instruction in body.Observation.ObjectBody.Instructions) output.WriteLine(instruction);
            foreach (var call in body.Observation.Calls) output.WriteLine($"CALL {call.Offset:x}: {call.TargetIdentity}");
            foreach (var region in body.Observation.ObjectBody.ExceptionRegions!) output.WriteLine(region);
        }
    }

    internal static IReadOnlyList<IlSqlObjectPathExtractor.Body> Bodies(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var result = new List<IlSqlObjectPathExtractor.Body>();
        foreach (var role in new[] { "end-to-end", "wrapper" })
        {
            var path = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language,
                role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll");
            var bytes = File.ReadAllBytes(path);
            var left = IlBodyEvidenceExtractor.ReadCecilBodies(bytes, new(), new(200_000), CancellationToken.None);
            var right = IlBodyEvidenceExtractor.ReadSystemReflectionMetadataBodies(bytes, new(), new(200_000), CancellationToken.None);
            Assert.Empty(IlBodyEvidenceExtractor.CompareBodies(left, right));
            var agreed = IlBodyEvidenceExtractor.AgreeValueFlows(left.Bodies, right.Bodies);
            Assert.All(agreed, b => Assert.NotNull(b.ObjectBody));
            var hash = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            var input = new IlInputOutcome(Path.GetFileName(path), role, "admitted", "local", hash, hash,
                left.AssemblyIdentity, left.ModuleName, left.ModuleMvid, null, []);
            result.AddRange(agreed.Select(b => new IlSqlObjectPathExtractor.Body(b, input)));
        }
        return result;
    }
}
