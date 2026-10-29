using System.Text.Json;
using TraceMap.Core;
using TraceMap.Cli;

namespace TraceMap.Tests;

public sealed class IlSqlExternalTraceTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Exact_void_trace_chains_require_opt_in_and_preserve_parameter_execution_identity(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceProducer");
        var strict = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.NotEmpty(strict.Gaps);
        Assert.Null(strict.Entry);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        Assert.Equal(2, result.ExternalTraces!.Count);
        Assert.All(result.ExternalTraces, trace =>
        {
            Assert.Equal(IlSqlLifecycleExtractor.ExternalTracePolicy, trace.Policy);
            Assert.Equal(3, trace.Calls.Count);
            Assert.All(trace.Calls, call => { Assert.True(call.MayThrow); Assert.Equal("unmodeled", call.Effects); });
            Assert.Contains("receiver-non-null-unproven", trace.Boundary, StringComparison.Ordinal);
        });
        Assert.Equal("argument:0", result.ExternalTraces[1].ArgumentOrigins[1]);
        Assert.Contains("external-trace-normal-return-assumed", result.Claim, StringComparison.Ordinal);
        var command = Assert.Single(result.Entry.Commands);
        Assert.Equal(Assert.Single(result.Executions).Id, command.Execution);
        Assert.Equal(4, command.Parameters.Select(parameter => parameter.Allocation).Distinct().Count());
        Assert.Equal("argument:0", command.Parameters[2].ValueOrigin);
        var separate = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceSeparateExecutions");
        var isolated = IlSqlLifecycleExtractor.Extract(separate, bodies, false, new(400_000), externalTraceBoundary: true);
        Assert.Empty(isolated.Gaps);
        Assert.Equal(2, isolated.Executions.Count);
        Assert.NotNull(isolated.Entry);
        Assert.Equal(2, isolated.Entry.Commands.Select(command => command.Execution).Distinct().Count());
        Assert.Equal(8, isolated.Entry.Commands.SelectMany(command => command.Parameters).Select(parameter => parameter.Allocation).Distinct().Count());
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Opaque_scalar_fields_and_local_strings_remain_data_not_context_references(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "OpaqueTraceProducer");
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true, externalTraceBoundary: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        var field = Assert.Single(result.OpaqueInitializers!).FieldSha256;
        Assert.Equal("opaque-field:" + field, Assert.Single(Assert.Single(result.ExternalTraces!).ArgumentOrigins));
        Assert.Equal("opaque-field:" + field, Assert.Single(result.Entry.Commands).Parameters[2].ValueOrigin);
        var local = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceLocalString");
        var loaded = IlSqlLifecycleExtractor.Extract(local, bodies, false, new(400_000), externalTraceBoundary: true);
        Assert.Empty(loaded.Gaps);
        Assert.Equal("argument:0", Assert.Single(Assert.Single(loaded.ExternalTraces!).ArgumentOrigins));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Context_branch_escape_tracked_argument_overload_limits_and_legacy_read_roots_refuse(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "TraceGuarded", "TraceEscape", "TraceWrongOverload", "TraceTrackedArgument", "TraceTooMany", "TraceRead" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), externalTraceBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Null(result.Returned);
            Assert.Empty(result.Executions);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Wrong_signed_framework_identity_or_incomplete_call_metadata_never_produces_an_entry(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceProducer");
        foreach (var transform in new Func<IlCallObservation, IlCallObservation>[] {
            call => call with { TargetIdentity = call.TargetIdentity.Replace("b03f5f7f11d50a3a", "0000000000000000", StringComparison.Ordinal) },
            call => call with { TargetIdentity = call.TargetIdentity.Replace("version:7:4.0.0.0", "version:7:2.0.0.0", StringComparison.Ordinal) },
            call => call with { TargetIdentity = call.TargetIdentity.Replace("System.Web", "Custom.Web", StringComparison.Ordinal) },
            call => call with { StackShape = null } })
        {
            var changed = root with { Observation = root.Observation with { Calls = root.Observation.Calls.Select(call =>
                call.TargetIdentity.Contains("b03f5f7f11d50a3a", StringComparison.Ordinal) ? transform(call) : call).ToArray() } };
            var variant = bodies.Select(body => body == root ? changed : body).ToArray();
            var result = IlSqlLifecycleExtractor.Extract(changed, variant, false, new(400_000), externalTraceBoundary: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
    }

    [Fact]
    public void Explicit_trace_boundary_requires_selected_roots()
        => Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(
            new ScanOptions(".", ".", IlBodyEvidence: true) { IlExternalTraceBoundary = true }, new(null, [], [], [])));

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_trace_boundary_entries_use_conditional_schema_and_generator_input_provenance(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var root = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), body => body.Observation.ObjectBody?.Name == "TraceProducer");
        var conditional = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), body => body.Observation.ObjectBody?.Name == "TraceConditional");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-external-trace-boundary", "--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken,
            "--il-lifecycle-root", conditional.Input.RawFileSha256 + ":" + conditional.Observation.MetadataToken };
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var candidate = Assert.Single(facts, fact => fact.TargetSymbol == root.Observation.BodyIdentity && fact.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
        Assert.Equal(IlSqlLifecycleExtractor.OpaqueEntrySchema, candidate.Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.ExternalTracePolicy, candidate.Properties["externalTracePolicy"]);
        Assert.Equal(64, candidate.Properties["ilGeneratorSha256"].Length);
        Assert.Equal(64, candidate.Properties["ilBoundedInputSha256"].Length);
        using var lifecycle = JsonDocument.Parse(candidate.Properties["lifecycle"]);
        Assert.Equal(2, lifecycle.RootElement.GetProperty("externalTraces").GetArrayLength());
        var alternatives = facts.Where(fact => fact.TargetSymbol == conditional.Observation.BodyIdentity
            && fact.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate).ToArray();
        Assert.Equal(2, alternatives.Length);
        Assert.Equal(IlSqlLifecycleExtractor.OpaqueEntrySchema, Assert.Single(alternatives,
            fact => fact.Properties["guard"] == "argument:0=false").Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.EntrySchema, Assert.Single(alternatives,
            fact => fact.Properties["guard"] == "argument:0=true").Properties["sqlLifecycleSchema"]);
    }
}
