using System.Security.Cryptography;
using TraceMap.Cli;
using TraceMap.Core;
namespace TraceMap.Tests;

public sealed class IlSqlStateControlNativeTests
{
    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Native_scan_emits_state_alternatives_as_one_conditional_candidate(string language)
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "AGENTS.md"))) root = root.Parent;
        Assert.NotNull(root);
        var config = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var fixture = Path.Combine(root.FullName, "samples", "sql-compiled-wrapper-evidence", language);
        var inputs = new[] { "end-to-end", "wrapper", "whole-protocol" }
            .Select(role => Path.Combine(fixture, role, "bin", config, "net48", $"PublicSql.{language}.{role}.dll")).ToArray();
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var rootBody = bodies.Single(body => body.Observation.ObjectBody?.Name == "WholeProtocolProduce");
        var rootSha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(inputs[2])));
        var output = IlBodyEvidenceExtractorShared.TempOutput();
        var args = new List<string> { "scan", "--repo", Path.Combine(fixture, "whole-protocol"), "--out", output,
            "--il-body-evidence", "--il-info-message-boundary", "--il-lifecycle-root",
            rootSha + ":" + rootBody.Observation.MetadataToken, "--compiled-only" };
        foreach (var input in inputs) args.AddRange(["--compiled-input", input]);
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(args.ToArray(), stdout, stderr) == 0, stderr.ToString());
        var facts = File.ReadLines(Path.Combine(output, "facts.ndjson")).Select(SqlServerSchemaEvidence.Read<CodeFact>).ToArray();
        Assert.Contains(facts, fact => fact.FactType == FactTypes.AnalysisGap
            && fact.Properties.Values.Any(value => value.Contains("SourceAnalysisExcludedByCompiledOnly", StringComparison.Ordinal)));
        var candidate = Assert.Single(facts, fact => fact.TargetSymbol == rootBody.Observation.BodyIdentity
            && fact.FactType == FactTypes.ManagedIlSqlLifecycleCandidate);
        Assert.Equal(IlSqlLifecycleExtractor.StateAlternativesSchema, candidate.Properties["sqlLifecycleSchema"]);
        Assert.Equal(IlSqlLifecycleExtractor.StateControlPolicy, candidate.Properties["stateControlPolicy"]);
        var result = SqlServerSchemaEvidence.Read<IlSqlLifecycleExtractor.Result>(candidate.Properties["lifecycle"]);
        Assert.Equal(2, result.StateAlternatives!.Count);
        Assert.Contains("exceptional-termination", result.StateAlternatives.Select(alternative => alternative.Outcome));
        Assert.Contains("state-conditional-alternatives", result.Claim, StringComparison.Ordinal);
        Assert.DoesNotContain("public.whole_protocol", candidate.Properties["lifecycle"], StringComparison.Ordinal);
        foreach (var required in new[] { "scan-manifest.json", "facts.ndjson", "index.sqlite", "report.md", "logs/analyzer.log" })
            Assert.True(File.Exists(Path.Combine(output, required)));
        // A rerun of the same inputs is byte-identical: alternatives are deterministic.
        var replay = IlBodyEvidenceExtractorShared.TempOutput();
        var replayArgs = args.Select((argument, index) => argument == output ? replay : argument).ToArray();
        using var replayStdout = new StringWriter(); using var replayStderr = new StringWriter();
        Assert.True(await TraceMapCommand.RunAsync(replayArgs, replayStdout, replayStderr) == 0, replayStderr.ToString());
        Assert.Equal(File.ReadAllBytes(Path.Combine(output, "facts.ndjson")),
            File.ReadAllBytes(Path.Combine(replay, "facts.ndjson")));
    }
}
