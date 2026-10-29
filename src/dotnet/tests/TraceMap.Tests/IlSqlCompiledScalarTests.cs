using System.Text.Json;
using TraceMap.Core;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class IlSqlCompiledScalarTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Retained_scalar_transform_control_and_handlers_require_a_distinct_opt_in_and_unknown_result(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "CompiledScalarTraceProduce");
        var strict = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true, externalTraceBoundary: true);
        Assert.NotEmpty(strict.Gaps);
        Assert.Null(strict.Entry);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000),
            opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        var initializer = Assert.Single(result.OpaqueInitializers!);
        var scalar = Assert.Single(initializer.CompiledCallees!);
        Assert.Equal(IlSqlLifecycleExtractor.CompiledScalarPolicy, scalar.Policy);
        Assert.InRange(scalar.Instructions, 1, 128);
        Assert.Equal(4, scalar.Handlers);
        Assert.True(scalar.HasBranches);
        Assert.False(scalar.DirectFieldWrites);
        Assert.Equal("unbound-declared-string;not-passthrough", scalar.ResultOrigin);
        Assert.True(scalar.Call.MayThrow);
        Assert.Contains("internal-control-and-handlers-unmodeled", scalar.Boundary, StringComparison.Ordinal);
        Assert.Contains("compiled-scalar-internal-control-unmodeled", result.Claim, StringComparison.Ordinal);
        Assert.Single(result.ConstructorOwnerViews!);
        Assert.Single(result.ExternalTraces!);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(Assert.Single(result.Executions).Id, command.Execution);
        Assert.Equal("opaque-field:" + initializer.FieldSha256, command.Parameters[2].ValueOrigin);
        Assert.Equal(4, command.Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Direct_writes_throws_opaque_SQL_identifiers_and_control_remain_refusals(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "CompiledScalarWriteProduce", "CompiledScalarThrowProduce", "CompiledScalarCommand", "CompiledScalarControl" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000),
                opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Missing_agreement_instance_generic_and_over_bound_helpers_never_become_summaries(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "CompiledScalarTraceProduce");
        var helper = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "PublicScalarTransform");
        var observation = helper.Observation;
        foreach (var changed in new[] {
            observation with { ObjectBodyAgreement = null },
            observation with { ObjectBody = observation.ObjectBody! with { HasThis = true } },
            observation with { ObjectBody = observation.ObjectBody! with { IsGeneric = true } },
            observation with { InstructionCount = 129 },
            observation with { ExceptionRegionCount = 5 },
            observation with { LocalCount = 33 },
            observation with { MaxStack = "17" },
            observation with { Calls = Enumerable.Repeat(observation.Calls[0], 33).ToArray() } })
        {
            var variant = bodies.Select(body => body == helper ? helper with { Observation = changed } : body).ToArray();
            var result = IlSqlLifecycleExtractor.Extract(root, variant, false, new(400_000),
                opaqueRootInitializers: true, externalTraceBoundary: true, compiledScalarBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
            Assert.NotNull(result.OpaqueInitializerRejection);
        }
    }

    [Fact]
    public void Compiled_boundary_requires_opaque_initializers_and_selected_roots()
    {
        Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(
            new ScanOptions(".", ".", IlBodyEvidence: true) { IlCompiledScalarBoundary = true }, new(null, [], [], [])));
        Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(
            new ScanOptions(".", ".", IlBodyEvidence: true) { IlCompiledScalarBoundary = true,
                IlLifecycleRoots = [new string('a', 64) + ":0x06000001"] }, new(null, [], [], [])));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_conditional_entries_bind_compiled_body_summary_and_three_explicit_policies(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var root = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), body => body.Observation.ObjectBody?.Name == "CompiledScalarTraceProduce");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-opaque-root-initializers", "--il-external-trace-boundary", "--il-compiled-scalar-boundary",
            "--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken };
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var fact = Assert.Single(facts, fact => fact.TargetSymbol == root.Observation.BodyIdentity && fact.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
        Assert.Equal(IlSqlLifecycleExtractor.OpaqueEntrySchema, fact.Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.CompiledScalarPolicy, fact.Properties["compiledScalarPolicy"]);
        Assert.Equal(64, fact.Properties["ilGeneratorSha256"].Length);
        Assert.Equal(64, fact.Properties["ilBoundedInputSha256"].Length);
        using var lifecycle = JsonDocument.Parse(fact.Properties["lifecycle"]);
        var summary = Assert.Single(lifecycle.RootElement.GetProperty("opaqueInitializers").EnumerateArray())
            .GetProperty("compiledCallees")[0];
        Assert.Equal(4, summary.GetProperty("handlers").GetInt32());
        Assert.Equal("unbound-declared-string;not-passthrough", summary.GetProperty("resultOrigin").GetString());
    }
}
