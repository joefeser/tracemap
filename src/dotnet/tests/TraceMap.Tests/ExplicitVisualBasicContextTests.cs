using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class ExplicitVisualBasicContextTests
{
    [Theory]
    [InlineData("valid")]
    [InlineData("root-namespace")]
    [InlineData("imports")]
    [InlineData("partial")]
    [InlineData("source-hash")]
    [InlineData("reference-hash")]
    [InlineData("duplicate-reference")]
    [InlineData("missing-reference")]
    [InlineData("stale-commit")]
    [InlineData("unknown-field")]
    [InlineData("duplicate-field")]
    [InlineData("outside-source")]
    [InlineData("project-conflict")]
    [InlineData("duplicate-source")]
    public async Task Explicit_context_binds_only_verified_inputs(string scenario)
    {
        using var temp = new TempDirectory(OperatingSystem.IsMacOS() ? "/private/tmp" : null);
        var repo = Path.Combine(temp.Path, "repo");
        Directory.CreateDirectory(repo);
        var source = "Public Class Worker\n Public Function Run() As Integer\n Return Helper()\n End Function\n Public Function Helper() As Integer\n Return 42\n End Function\nEnd Class";
        if (scenario == "partial") source += "\nPublic Class Broken\n Inherits MissingBase\nEnd Class";
        if (scenario == "imports") source = source.Replace("As Integer", "As Int32", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(repo, "Worker.vb"), source);
        File.WriteAllText(Path.Combine(repo, "Orphan.vb"), "Public Class Orphan\nEnd Class");
        if (scenario == "project-conflict") File.WriteAllText(Path.Combine(repo, "Test.vbproj"), "<Project />");
        foreach (var arguments in new[] { new[] { "init" }, new[] { "add", "." }, new[] { "-c", "user.name=Synthetic", "-c", "user.email=test@example.invalid", "commit", "-m", "fixture" } })
        {
            using var process = Process.Start(new ProcessStartInfo("git", arguments) { WorkingDirectory = repo, RedirectStandardOutput = true, RedirectStandardError = true })!;
            Assert.True(process.WaitForExit(30_000));
            Assert.Equal(0, process.ExitCode);
        }
        var reference = Path.Combine(temp.Path, "core.dll");
        File.Copy(typeof(object).Assembly.Location, reference);
        static string Hash(string path) => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var sources = new List<object> { new { path = scenario == "outside-source" ? "../Worker.vb" : "Worker.vb", sha256 = scenario == "source-hash" ? new string('0', 64) : Hash(Path.Combine(repo, "Worker.vb")) } };
        var references = new List<object> { new { path = reference, sha256 = scenario == "reference-hash" ? new string('0', 64) : Hash(reference) } };
        if (scenario == "duplicate-source") sources.Add(sources[0]);
        if (scenario == "duplicate-reference") references.Add(references[0]);
        var context = JsonSerializer.Serialize(new
        {
            schemaVersion = "vb-compilation-context.v1",
            commitSha = scenario == "stale-commit" ? new string('0', 40) : GitMetadataProvider.Detect(repo).CommitSha,
            rootNamespace = scenario == "root-namespace" ? "SyntheticRoot" : "",
            optionStrict = true, optionInfer = true, optionExplicit = true,
            imports = scenario == "imports" ? new[] { "System" } : Array.Empty<string>(), sources, references
        });
        if (scenario == "unknown-field") context = context.Insert(1, "\"extra\":true,");
        if (scenario == "duplicate-field") context = context.Insert(1, "\"rootNamespace\":\"\",");
        var contextPath = Path.Combine(temp.Path, "context.json");
        File.WriteAllText(contextPath, context);
        if (scenario == "missing-reference") File.Delete(reference);
        var inventory = FileInventory.Collect(repo);
        var options = new ScanOptions(repo, Path.Combine(temp.Path, "out")) { VisualBasicCompilationContextPath = contextPath };
        var result = VisualBasicSemanticExtractor.Extract(repo, inventory, options);
        var accepted = scenario is "valid" or "root-namespace" or "imports" or "partial";
        Assert.True(result.Attempted);
        if (!accepted)
        {
            Assert.Empty(result.Facts);
            Assert.Empty(result.AnalyzedFiles!);
            Assert.Equal("ExplicitCompilationContextRefused", Assert.Single(result.GapFacts).Properties!["gapKind"]);
            return;
        }
        Assert.Contains("Worker.vb", result.AnalyzedFiles!);
        Assert.Contains(result.Facts, f => f.FactType == FactTypes.TypeDeclared && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.All(result.Facts, f => Assert.Equal("explicit-projectless-inputs", f.Properties!["compilationContextKind"]));
        Assert.Equal(scenario == "partial", result.ReducedCoverage);
        if (scenario == "root-namespace") Assert.Contains(result.Facts, f => f.SourceSymbol?.Contains("SyntheticRoot.Worker", StringComparison.Ordinal) == true || f.TargetSymbol?.Contains("SyntheticRoot.Worker", StringComparison.Ordinal) == true);
        if (scenario == "partial") Assert.Contains(result.GapFacts, f => f.Properties!["gapKind"] == "CompilationDiagnostic");
        var scan = ScanEngine.Scan(options);
        Assert.Contains(scan.Facts, f => f.EvidenceTier == EvidenceTiers.Tier1Semantic && f.Evidence.FilePath == "Worker.vb");
        Assert.DoesNotContain(scan.Facts, f => f.Properties.GetValueOrDefault("gapKind") == "NoVisualBasicProjectOrSolution");
        Assert.Contains(scan.Facts, f => f.Evidence.FilePath == "Orphan.vb" && f.Properties.GetValueOrDefault("gapKind") == "SemanticAnalysisUnavailable");
        Assert.DoesNotContain(scan.Facts, f => f.Evidence.FilePath == "Orphan.vb" && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        Assert.Contains(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
        if (scenario == "valid")
        {
            foreach (var name in new[] { "cli-one", "cli-two" })
            {
                using var output = new StringWriter();
                using var error = new StringWriter();
                Assert.Equal(0, await TraceMap.Cli.TraceMapCommand.RunAsync(
                    ["scan", "--repo", repo, "--out", Path.Combine(temp.Path, name), "--vb-compilation-context", contextPath], output, error));
                foreach (var artifact in new[] { "facts.ndjson", "index.sqlite", "report.md", "scan-manifest.json", "logs/analyzer.log" })
                    Assert.True(File.Exists(Path.Combine(temp.Path, name, artifact)));
            }
            Assert.Equal(File.ReadAllBytes(Path.Combine(temp.Path, "cli-one", "facts.ndjson")),
                File.ReadAllBytes(Path.Combine(temp.Path, "cli-two", "facts.ndjson")));
        }
    }
}
