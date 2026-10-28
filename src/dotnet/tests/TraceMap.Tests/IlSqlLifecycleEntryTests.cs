using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlSqlLifecycleEntryTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Four_argument_dataset_producer_keeps_unknown_inputs_and_parameter_execution_identity(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "ProduceInputs");
        Assert.True(IlSqlLifecycleExtractor.IsRoot(root.Observation));
        var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
        Assert.Empty(result.Gaps);
        Assert.Null(result.Returned);
        var entry = Assert.IsType<IlSqlLifecycleExtractor.EntryOutput>(result.Entry);
        Assert.Equal("dataset-return", entry.Kind);
        Assert.Equal(4, entry.Arguments.Count);
        Assert.All(entry.Arguments, input => Assert.Equal("unbound-root-argument", input.Origin));
        Assert.All(entry.Arguments, input => Assert.NotNull(input.DeclaredType));
        Assert.Equal(entry.ReturnedDataSet, Assert.Single(entry.DataSets).Allocation);
        var command = Assert.Single(entry.Commands);
        Assert.Equal(Assert.Single(result.Executions).Id, command.Execution);
        Assert.Equal(4, command.Parameters.Count);
        Assert.Equal(new[] { "argument:0", "argument:1", "argument:2", "argument:3" }, command.Parameters.Select(p => p.ValueOrigin));
        Assert.Equal(4, command.Parameters.Select(p => p.Allocation).Distinct().Count());
        Assert.All(entry.Tables, table => Assert.Equal(command.Execution, Assert.Single(table.SchemaExecutions)));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Void_loader_retains_stored_dataset_and_legacy_column_consumer_stays_separate(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var loader = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "StoreInputs");
        var loaded = IlSqlLifecycleExtractor.Extract(loader, bodies, false, new(400_000));
        Assert.Empty(loaded.Gaps);
        var entry = Assert.IsType<IlSqlLifecycleExtractor.EntryOutput>(loaded.Entry);
        Assert.Equal("void-completion", entry.Kind);
        Assert.Null(loaded.Returned);
        Assert.Null(entry.ReturnedDataSet);
        Assert.Equal(Assert.Single(entry.StoredDataSets).DataSetAllocation, Assert.Single(entry.DataSets).Allocation);
        var consumer = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "StoredInputRead");
        var read = IlSqlLifecycleExtractor.Extract(consumer, bodies, false, new(400_000));
        Assert.Empty(read.Gaps);
        Assert.NotNull(read.Returned);
        Assert.Null(read.Entry);
        Assert.Equal("execution:0", read.Returned.Value.Execution);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Unknown_control_command_or_unobservable_outputs_withhold_entry_claims(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var method in new[] { "UnknownGuard", "UnknownCommand", "EmptyProducer", "DiscardInputs" })
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == method);
            Assert.True(IlSqlLifecycleExtractor.IsRoot(root.Observation));
            var result = IlSqlLifecycleExtractor.Extract(root, bodies, false, new(400_000));
            Assert.NotEmpty(result.Gaps);
            Assert.Null(result.Entry);
            Assert.Null(result.Returned);
            Assert.Empty(result.Executions);
        }
        var producer = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == "ProduceInputs");
        foreach (var invalid in new[] {
            producer.Observation.ObjectBody! with { IsVirtual = true },
            producer.Observation.ObjectBody! with { IsGeneric = true },
            producer.Observation.ObjectBody! with { ParameterCount = 17 } })
        {
            var changed = producer with { Observation = producer.Observation with { ObjectBody = invalid } };
            var result = IlSqlLifecycleExtractor.Extract(changed,
                bodies.Select(body => body == producer ? changed : body).ToArray(), false, new(400_000));
            Assert.Equal("lifecycle-root-admission-shape", Assert.Single(result.Gaps));
            Assert.Null(result.Entry);
        }
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_cli_emits_entry_contract_and_rejection_gaps_without_fabricating_a_column_read(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        using var stdout = new StringWriter();
        using var stderr = new StringWriter();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output, "--il-body-evidence" };
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        foreach (var name in new[] { "ProduceInputs", "StoreInputs" })
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == name);
            var fact = Assert.Single(facts, f => f.TargetSymbol == root.Observation.BodyIdentity
                && f.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
            Assert.Equal(IlSqlLifecycleExtractor.EntrySchema, fact.Properties["sqlLifecycleSchema"]);
            Assert.Equal(RuleIds.DotNetIlSqlLifecycle, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, fact.EvidenceTier);
            Assert.True(fact.Properties.ContainsKey("ilGeneratorSha256"));
            Assert.True(fact.Properties.ContainsKey("ilBoundedInputSha256"));
            Assert.NotEmpty(fact.Properties["bodyLinks"]);
            Assert.DoesNotContain(facts, f => f.TargetSymbol == root.Observation.BodyIdentity
                && f.FactType == FactTypes.ManagedIlSqlLifecycleCandidate);
        }
        foreach (var name in new[] { "UnknownGuard", "UnknownCommand", "EmptyProducer", "DiscardInputs" })
        {
            var root = Assert.Single(bodies, b => b.Observation.ObjectBody?.Name == name);
            Assert.Contains(facts, f => f.TargetSymbol == root.Observation.BodyIdentity && f.FactType == FactTypes.AnalysisGap
                && f.RuleId == RuleIds.DotNetIlSqlLifecycle);
        }
    }
}
