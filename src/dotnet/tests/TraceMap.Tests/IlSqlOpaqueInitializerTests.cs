using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlOpaqueInitializerTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Opaque_initializers_require_opt_in_and_preserve_conditional_typed_origins(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "OpaqueProduce");
        var refused = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.NotEmpty(refused.Gaps);
        Assert.Null(refused.Entry);
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true);
        Assert.Empty(result.Gaps);
        Assert.NotNull(result.Entry);
        Assert.Equal(2, result.OpaqueInitializers!.Count);
        Assert.All(result.OpaqueInitializers, receipt =>
        {
            Assert.Equal(IlSqlLifecycleExtractor.OpaquePolicy, receipt.Policy);
            Assert.Equal("type(namespace:6:System|names:6:String)", receipt.DeclaredType);
            Assert.InRange(receipt.Calls.Count, 1, 8);
            Assert.All(receipt.Calls, call => { Assert.True(call.MayThrow); Assert.Equal("unmodeled", call.Effects); });
            Assert.Equal(64, receipt.FieldSha256.Length);
        });
        Assert.Contains("external-effects-unmodeled", result.Claim, StringComparison.Ordinal);
        var parameters = Assert.Single(result.Entry.Commands).Parameters;
        Assert.StartsWith("opaque-field:", parameters[2].ValueOrigin, StringComparison.Ordinal);
        Assert.Contains(result.OpaqueInitializers, receipt => parameters[2].ValueOrigin == "opaque-field:" + receipt.FieldSha256);
        Assert.Equal(4, parameters.Select(parameter => parameter.Allocation).Distinct().Count());
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Opaque_SQL_names_control_mutation_reference_escape_handlers_and_limits_refuse(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "OpaqueCommand", "OpaqueControl", "OpaqueMutation", "OpaqueEscapeProduce",
            "OpaqueReferenceProduce", "OpaqueCatchProduce", "OpaqueLongProduce", "OpaqueRead" })
        {
            var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == name);
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000), opaqueRootInitializers: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Missing_private_field_metadata_and_byref_calls_cannot_be_abstracted(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "OpaqueProduce");
        var constructor = Assert.Single(bodies, body => body.Observation.ObjectBody is { Name: ".ctor" } meta
            && meta.OwnerType == root.Observation.ObjectBody!.OwnerType);
        foreach (var changed in new[] {
            constructor with { Observation = constructor.Observation with { ObjectBody = constructor.Observation.ObjectBody! with { PrivateInstanceFields = [] } } },
            constructor with { Observation = constructor.Observation with { Calls = constructor.Observation.Calls.Select(call => call.StackShape is { ReturnsValue: true } shape
                ? call with { StackShape = shape with { ByReferenceParameters = "1" } } : call).ToArray() } } })
        {
            var variant = bodies.Select(body => body == constructor ? changed : body).ToArray();
            var result = IlSqlLifecycleExtractor.Extract(root, variant, false, new(400_000), opaqueRootInitializers: true);
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Empty(result.Executions);
        }
    }

    [Fact]
    public void Opt_in_requires_an_explicit_selected_root_scope()
    {
        var options = new ScanOptions(".", ".", IlBodyEvidence: true) { IlOpaqueRootInitializers = true };
        Assert.Throws<ArgumentException>(() => IlBodyEvidenceExtractor.Evaluate(options, new(null, [], [], [])));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_opt_in_uses_a_separate_schema_and_bound_initializer_receipts(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var root = Assert.Single(IlSqlLifecycleExtractorTests.Bodies(language), body => body.Observation.ObjectBody?.Name == "OpaqueProduce");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-opaque-root-initializers", "--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken };
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var candidate = Assert.Single(facts, fact => fact.TargetSymbol == root.Observation.BodyIdentity
            && fact.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
        Assert.Equal(IlSqlLifecycleExtractor.OpaqueEntrySchema, candidate.Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.OpaquePolicy, candidate.Properties["opaqueInitializerPolicy"]);
        Assert.Equal(64, candidate.Properties["ilGeneratorSha256"].Length);
        Assert.Equal(64, candidate.Properties["ilBoundedInputSha256"].Length);
        using var lifecycle = JsonDocument.Parse(candidate.Properties["lifecycle"]);
        Assert.Equal(2, lifecycle.RootElement.GetProperty("opaqueInitializers").GetArrayLength());
        Assert.DoesNotContain(facts, fact => fact.TargetSymbol == root.Observation.BodyIdentity
            && fact.FactType == FactTypes.ManagedIlSqlLifecycleCandidate);
    }
}
