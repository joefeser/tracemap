using System.Text.Json;
using TraceMap.Cli;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ScanDiagnosticProgressTests
{
    private static ScanProgressCheckpoint ReadCheckpoint(string json) => JsonSerializer.Deserialize<ScanProgressCheckpoint>(json,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

    [Fact]
    public void Live_nested_phase_survives_failure_without_claiming_completion_or_exposing_private_values()
    {
        var root = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(root);
        var repo = Path.Combine(root, "repo"); Directory.CreateDirectory(repo);
        var output = Path.Combine(root, "output"); var checkpoint = Path.Combine(root, "progress.json");
        using (ScanDiagnosticProgress.Create(checkpoint, repo, output, new(repo, output)))
        {
            Assert.Throws<InvalidOperationException>(() => ScanProgressAmbient.Observe<int>(ScanProgressStages.IlLifecycle, () =>
                ScanProgressAmbient.Observe<int>(ScanProgressStages.IlCallbackInspection, () =>
                {
                    var live = ReadCheckpoint(File.ReadAllText(checkpoint));
                    Assert.Equal(ScanProgressStages.IlCallbackInspection, live.Latest.Stage);
                    Assert.Equal("started", live.Latest.State); Assert.False(Directory.Exists(output));
                    throw new InvalidOperationException("PRIVATE-ERROR-CANARY");
                })));
        }
        Assert.Null(ScanProgressAmbient.Current);
        var text = File.ReadAllText(checkpoint);
        var result = ReadCheckpoint(text);
        Assert.Equal("failed", result.Latest.State);
        Assert.Contains(result.History, item => item.Stage == ScanProgressStages.IlCallbackInspection && item.State == "failed");
        Assert.DoesNotContain("PRIVATE-ERROR-CANARY", text, StringComparison.Ordinal);
        using var binding = JsonDocument.Parse(File.ReadAllText(checkpoint + ".binding.json"));
        Assert.Equal(64, binding.RootElement.GetProperty("generatorSha256").GetString()!.Length);
        Assert.Equal(64, binding.RootElement.GetProperty("boundedInputSha256").GetString()!.Length);
        Assert.DoesNotContain(repo, binding.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Normal_scan_progress_is_fresh_outside_source_and_output_and_does_not_change_facts()
    {
        var root = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(root);
        var repo = Path.Combine(root, "repo"); Directory.CreateDirectory(repo);
        File.WriteAllText(Path.Combine(repo, "Public.cs"), "public class PublicSample { public int Count() => 1; }");
        var first = Path.Combine(root, "first"); var second = Path.Combine(root, "second"); var checkpoint = Path.Combine(root, "progress.json");
        using var stdout = new StringWriter(); using var stderr = new StringWriter();
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", first], stdout, stderr));
        Assert.Equal(0, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", second, "--diagnostic-progress", checkpoint], stdout, stderr));
        Assert.Equal(File.ReadAllBytes(Path.Combine(first, "facts.ndjson")), File.ReadAllBytes(Path.Combine(second, "facts.ndjson")));
        var result = ReadCheckpoint(File.ReadAllText(checkpoint));
        Assert.Equal("completed", result.Latest.State); Assert.Equal(ScanProgressStages.ScanPublication, result.Latest.Stage);
        Assert.Contains(result.History, item => item.Stage == ScanProgressStages.SourceSemanticAnalysis);
        Assert.Contains(result.History, item => item.Stage == ScanProgressStages.CompiledMetadata);
        Assert.Contains(result.History, item => item.Stage == ScanProgressStages.IlDecode);
        var prior = File.ReadAllBytes(checkpoint);
        Assert.Equal(1, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", Path.Combine(root,"third"), "--diagnostic-progress", checkpoint], stdout, stderr));
        Assert.Equal(prior, File.ReadAllBytes(checkpoint));
        Assert.Null(ScanProgressAmbient.Current);
        foreach (var path in new[] { Path.Combine(repo,"bad.json"), Path.Combine(root,"fourth","bad.json") })
        {
            Assert.Equal(1, await TraceMapCommand.RunAsync(["scan", "--repo", repo, "--out", Path.Combine(root,"fourth"), "--diagnostic-progress", path], stdout, stderr));
            Assert.False(File.Exists(path));
        }
    }

    [Fact]
    public void Compiled_only_refuses_missing_inputs_and_conflicting_source_scope()
    {
        var root = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(root);
        var options = new ScanOptions(root, Path.Combine(root, "out")) { CompiledOnly = true };
        Assert.Throws<ArgumentException>(() => ScanEngine.Scan(options));
        options = options with { CompiledInputPaths = ["missing.dll"], Restore = true };
        Assert.Throws<ArgumentException>(() => ScanEngine.Scan(options));
        options = options with { Restore = false, ProjectPaths = ["Public.csproj"] };
        Assert.Throws<ArgumentException>(() => ScanEngine.Scan(options));
    }

    [Fact]
    public void Compiled_only_never_loads_even_malformed_source_projects()
    {
        var root = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "NeverLoad.csproj"), "<INVALID");
        File.WriteAllText(Path.Combine(root, "NeverLoad.sln"), "INVALID");
        File.WriteAllText(Path.Combine(root, "NeverParse.vb"), "INVALID");
        var checkpoint = Path.Combine(Path.GetDirectoryName(root)!, Guid.NewGuid() + ".json");
        var options = new ScanOptions(root, Path.Combine(root, "out"), CompiledInputPaths: ["missing.dll"])
            { CompiledOnly = true };
        using (ScanDiagnosticProgress.Create(checkpoint, root, options.OutputPath, options))
        {
            var result = ScanEngine.Scan(options, null, default, ScanProgressAmbient.Current);
            Assert.Empty(result.Inventory);
            Assert.Empty(result.Manifest.Projects); Assert.Empty(result.Manifest.Solutions);
            Assert.Equal("NotRun", result.Manifest.BuildStatus);
            Assert.Contains(result.Manifest.KnownGaps, gap => gap.StartsWith("SourceAnalysisExcludedByCompiledOnly", StringComparison.Ordinal));
        }
        var progress = ReadCheckpoint(File.ReadAllText(checkpoint));
        Assert.DoesNotContain(progress.History, item => item.Stage is "solution-load" or "compilation" or "msbuild-registration");
        Assert.Contains(progress.History, item => item.Stage == ScanProgressStages.CompiledMetadata);
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public void IL_decode_callback_and_naming_have_distinct_operational_stages(string language)
    {
        var root = IlBodyEvidenceExtractorShared.TempOutput(); Directory.CreateDirectory(root);
        var repo = Path.Combine(root,"repo"); Directory.CreateDirectory(repo);
        var checkpoint = Path.Combine(root,"progress.json");
        var bodies = IlSqlLifecycleExtractorTests.Bodies(language);
        var selected = Assert.Single(bodies, body => body.Observation.ObjectBody?.Name == "EventNamingProduce");
        using (ScanDiagnosticProgress.Create(checkpoint, repo, Path.Combine(root,"output"), new(repo, root)))
        {
            var result = ScanProgressAmbient.Observe(ScanProgressStages.IlLifecycle, () =>
                IlSqlLifecycleExtractor.Extract(selected,bodies,false,new(400_000),infoMessageBoundary:true));
            Assert.Empty(result.Gaps); Assert.NotNull(result.Entry);
        }
        var progress = ReadCheckpoint(File.ReadAllText(checkpoint));
        foreach (var stage in new[] { ScanProgressStages.IlLifecycle, ScanProgressStages.IlCallbackInspection, ScanProgressStages.IlResultNaming })
            Assert.Contains(progress.History, item => item.Stage == stage && item.State == "completed");
    }
}
