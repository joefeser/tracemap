using System.Text.Json;
using TraceMap.Core;
using TraceMap.Cli;
namespace TraceMap.Tests;

public sealed class IlSqlOpcodeDiagnosticTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void Rejected_callee_opcode_retains_root_and_actual_invocation_chain(string language)
    {
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var root = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "ReferenceCtorProduce");
        var getter = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "get_BaseMarker");
        var meta = getter.Observation.ObjectBody!;
        var changed = getter with { Observation = getter.Observation with { ObjectBody = meta with {
            Instructions = meta.Instructions.Select(line => {
                var parts = line.Split(':', 4);
                return parts[2] == "ldfld" ? string.Join(':', parts[0], parts[1], "ldftn", parts[3]) : line;
            }).ToArray() } } };
        var result = IlSqlLifecycleExtractor.Extract(root, bodies.Select(body => body == getter ? changed : body).ToArray(),
            false, new(400_000), opaqueRootInitializers: true);
        Assert.Equal("lifecycle-opcode:ldftn", Assert.Single(result.Gaps));
        var diagnostic = Assert.IsType<IlSqlLifecycleExtractor.OpcodeRejection>(result.OpcodeFailure);
        Assert.Equal(root.Observation.BodyIdentity, diagnostic.SelectedRoot.Body);
        Assert.Equal(getter.Observation.MetadataToken, diagnostic.MetadataToken);
        Assert.Equal(getter.Observation.BodyIdentity, diagnostic.Site.Body);
        Assert.True(diagnostic.InvocationPath.Count > 1);
        Assert.NotNull(diagnostic.InvocationPath[^1].Caller);
        Assert.Equal(diagnostic.Site.Body, diagnostic.InvocationPath[^1].Entry.Body);
        Assert.Null(result.Entry);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_gap_facts_emit_diagnostics_without_contaminating_an_unrelated_selected_root(string language)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var sample = Path.Combine(directory.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var good = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceProducer");
        var opcode = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceDelegateOpcodeRefusal");
        var trace = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "TraceUnknownBuilderRefusal");
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(sample, "end-to-end"), "--out", output,
            "--il-body-evidence", "--il-external-trace-boundary" };
        foreach (var root in new[] { good, opcode, trace })
            args.AddRange(["--il-lifecycle-root", root.Input.RawFileSha256 + ":" + root.Observation.MetadataToken]);
        foreach (var role in new[] { "end-to-end", "wrapper" })
            args.AddRange(["--compiled-input", Path.Combine(sample, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        Assert.Contains(facts, fact => fact.TargetSymbol == good.Observation.BodyIdentity && fact.FactType == FactTypes.ManagedIlSqlLifecycleEntryCandidate);
        Assert.DoesNotContain(facts, fact => fact.TargetSymbol == good.Observation.BodyIdentity
            && fact.RuleId == RuleIds.DotNetIlSqlLifecycle && fact.FactType == FactTypes.AnalysisGap);
        var opcodeGap = Assert.Single(facts, fact => fact.TargetSymbol == opcode.Observation.BodyIdentity
            && fact.RuleId == RuleIds.DotNetIlSqlLifecycle && fact.FactType == FactTypes.AnalysisGap);
        using var opcodeJson = JsonDocument.Parse(opcodeGap.Properties["opcodeFailure"]);
        Assert.Equal(opcode.Observation.MetadataToken, opcodeJson.RootElement.GetProperty("metadataToken").GetString());
        Assert.NotEmpty(opcodeJson.RootElement.GetProperty("invocationPath").EnumerateArray());
        var traceGap = Assert.Single(facts, fact => fact.TargetSymbol == trace.Observation.BodyIdentity
            && fact.RuleId == RuleIds.DotNetIlSqlLifecycle && fact.FactType == FactTypes.AnalysisGap);
        using var traceJson = JsonDocument.Parse(traceGap.Properties["externalTraceFailure"]);
        Assert.Equal(trace.Observation.MetadataToken, traceJson.RootElement.GetProperty("metadataToken").GetString());
    }
}
