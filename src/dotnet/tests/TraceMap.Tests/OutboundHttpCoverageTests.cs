using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed class OutboundHttpCoverageTests
{
    private static string Sample(string language)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        Assert.NotNull(repo);
        return Path.Combine(repo.FullName, "samples", "outbound-http-coverage", language);
    }

    [Fact]
    public void Http_report_projection_rejects_private_arbitrary_property_values()
    {
        var evidence = OutboundHttpCallEvidence.FromProperties(new Dictionary<string, string>
        {
            ["methodFamily"] = "PRIVATE_VALUE", ["methodName"] = "PRIVATE_VALUE", ["urlKind"] = "PRIVATE_VALUE",
            ["protocolKind"] = "PRIVATE_VALUE", ["coverageLabel"] = "PRIVATE_VALUE", ["limitations"] = "PRIVATE_VALUE"
        });
        Assert.DoesNotContain("PRIVATE_VALUE", JsonSerializer.Serialize(evidence));
        Assert.Equal("unavailable", evidence.DestinationAvailability);
    }

    [Fact]
    public void Vb_unresolved_http_candidate_names_preserve_case_insensitivity()
    {
        Assert.True(OutboundHttpApi.IsCandidate("uploadvaluestaskasync", ignoreCase: true));
        Assert.False(OutboundHttpApi.IsCandidate("UploadSecret"));
        Assert.False(OutboundHttpApi.IsCandidate("getstringasync"));
    }

    [Theory]
    [InlineData("System.Net.Http", "System.Net.Http", "HttpClient", "GetStringAsync")]
    [InlineData("System.Net.WebClient", "System.Net", "WebClient", "UploadString")]
    [InlineData("System.Net.Requests", "System.Net", "WebRequest", "GetResponse")]
    [InlineData("System.Net.Http.Json", "System.Net.Http.Json", "HttpClientJsonExtensions", "GetFromJsonAsync")]
    public void Unsigned_exact_framework_type_and_assembly_lookalikes_are_rejected(string assembly, string ns, string type, string method)
    {
        var source = $"namespace {ns} {{ public class {type} {{ public void {method}(string address) {{ }} }} }}";
        var compilation = CSharpCompilation.Create(assembly, [CSharpSyntaxTree.ParseText(source)],
            [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)]);
        var symbol = Assert.Single(compilation.GetTypeByMetadataName(ns + "." + type)!.GetMembers(method).OfType<IMethodSymbol>());
        Assert.False(OutboundHttpApi.TryClassify(symbol, out _, out _));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    public async Task Every_compiler_built_overload_retains_distinct_semantic_evidence_and_report(string language)
    {
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(Sample(language), Path.Combine(temp.Path, "scan")));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.HttpCallDetected && f.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        for (var i = 0; i < 166; i++)
        {
            var fact = Assert.Single(calls, f => f.SourceSymbol?.Contains($".Case{i:D3}(", StringComparison.Ordinal) == true);
            Assert.Equal(EvidenceTiers.Tier1Semantic, fact.EvidenceTier);
            Assert.Equal(RuleIds.HttpClientInvocation, fact.RuleId);
            Assert.True(fact.Evidence.StartLine > 0);
            Assert.NotEmpty(fact.Properties["targetAssemblyPublicKeyToken"]);
            Assert.NotEmpty(fact.Properties["limitations"]);
        }
        Assert.Equal(166, calls.Count(f => f.SourceSymbol?.Contains("Matrix.Case", StringComparison.Ordinal) == true));
        Assert.DoesNotContain(calls, f => f.SourceSymbol?.Contains("FakeCalls", StringComparison.Ordinal) == true
            || f.SourceSymbol?.Contains("ContentOnly", StringComparison.Ordinal) == true
            || f.SourceSymbol?.Contains("Wrapper", StringComparison.Ordinal) == true);
        var dynamic = Assert.Single(calls, f => f.SourceSymbol?.Contains("DynamicDestination", StringComparison.Ordinal) == true);
        Assert.Equal("unavailable", dynamic.Properties["urlKind"]);
        Assert.Equal("GET", dynamic.Properties["httpMethod"]);
        var request = Assert.Single(calls, f => f.SourceSymbol?.Contains("RequestVariable", StringComparison.Ordinal) == true);
        Assert.Equal("UNKNOWN", request.Properties["httpMethod"]);
        var dynamicVerb = Assert.Single(calls, f => f.SourceSymbol?.Contains("DynamicVerb", StringComparison.Ordinal) == true);
        Assert.Equal("UNKNOWN", dynamicVerb.Properties["httpMethod"]);
        Assert.Contains(calls, f => f.SourceSymbol?.Contains("Inherited", StringComparison.Ordinal) == true);
        var file = Assert.Single(calls, f => f.SourceSymbol?.Contains("FileProtocol", StringComparison.Ordinal) == true);
        Assert.Equal("unproven-address-protocol", file.Properties["protocolKind"]);
        Assert.Contains(calls, f => f.Properties["methodFamily"] == "WebClient" && f.Properties["httpMethod"] == "PUT");
        Assert.DoesNotContain("https://example.invalid", JsonSerializer.Serialize(calls));
        Assert.All(calls.Where(f => f.Properties["methodFamily"] == "HttpClient" && f.Properties["methodName"] is "Send" or "SendAsync"
            && f.SourceSymbol?.Contains("Matrix.Case", StringComparison.Ordinal) == true), f =>
            { Assert.Equal("POST", f.Properties["httpMethod"]); Assert.Equal("compile-time-constant-hash", f.Properties["urlKind"]); });
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, scan.Manifest, scan.Facts);
        var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index)));
        // Every overload must survive retained-index query/report projection, not just extraction.
        foreach (var positive in calls.Where(f => f.SourceSymbol?.Contains("Matrix.Case", StringComparison.Ordinal) == true))
        {
            var family = positive.Properties["methodFamily"];
            var report = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
                FromSymbol: positive.SourceSymbol, ToSurface: "http-client", MaxDepth: 16));
            var handoff = GroupedCompiledPathHandoffBuilder.Create(report, hash);
            Assert.NotEmpty(handoff.BackendExtraction!.Operations);
            Assert.Contains(handoff.Nodes.Values, node => node.HttpCallEvidence is { } evidence
                && evidence.MethodFamily == family && evidence.MethodName == positive.Properties["methodName"]
                && evidence.DestinationAvailability == positive.Properties["urlKind"] && node.HttpMethod == positive.Properties["httpMethod"]);
            Assert.Contains("compiled-only", handoff.HttpCoverageLimitations);
            GroupedCompiledPathHandoffBuilder.Restore(handoff with { HttpCoverageLimitations = null });
            Assert.Throws<InvalidDataException>(() => GroupedCompiledPathHandoffBuilder.Restore(handoff with { HttpCoverageLimitations = "altered" }));
            Assert.Matches("^[0-9a-f]{64}$", handoff.GeneratorSha256);
            var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, positive.FactId));
            Assert.Contains("Backend extraction inventory", await File.ReadAllTextAsync(output.HtmlPath));
        }
        var markdown = Path.Combine(temp.Path, "report.md");
        await MarkdownReportWriter.WriteAsync(markdown, scan);
        Assert.Contains("Absence never means unused", await File.ReadAllTextAsync(markdown));
        var repeat = ScanEngine.Scan(new ScanOptions(Sample(language), Path.Combine(temp.Path, "repeat")));
        Assert.Equal(scan.Facts.Select(f => JsonSerializer.Serialize(f)), repeat.Facts.Select(f => JsonSerializer.Serialize(f)));
    }

    [Theory]
    [InlineData("csharp", ".cs")]
    [InlineData("vb", ".vb")]
    public async Task Projectless_candidates_never_claim_framework_support(string language, string extension)
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "source"); Directory.CreateDirectory(root);
        foreach (var file in Directory.GetFiles(Sample(language), "*" + extension)) File.Copy(file, Path.Combine(root, Path.GetFileName(file)));
        var scan = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "scan")));
        var calls = scan.Facts.Where(f => f.FactType == FactTypes.HttpCallDetected).ToArray();
        if (language == "vb") Assert.Empty(calls);
        else
        {
            Assert.True(calls.Length >= 166);
            Assert.All(calls, f => { Assert.Equal("unresolved", f.Properties["methodFamily"]); Assert.Equal("UNKNOWN", f.Properties["httpMethod"]); Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, f.EvidenceTier); });
        }
        await MarkdownReportWriter.WriteAsync(Path.Combine(temp.Path, "report.md"), scan);
        Assert.Contains("VB projectless fallback", await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.md")));
    }

    [Theory]
    [InlineData("csharp", "HttpMatrix.CSharp")]
    [InlineData("vb", "HttpMatrix.VB")]
    public async Task Compiled_only_retains_calls_without_inventing_http_surfaces(string language, string name)
    {
        using var temp = new TempDirectory();
        var root = Path.Combine(temp.Path, "empty"); Directory.CreateDirectory(root);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var assembly = Path.Combine(Sample(language), "bin", configuration, "net10.0", name + ".dll");
        Assert.True(File.Exists(assembly));
        var scan = ScanEngine.Scan(new ScanOptions(root, Path.Combine(temp.Path, "scan"), CompiledInputPaths: [assembly], IlBodyEvidence: true));
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.HttpCallDetected);
        Assert.True(scan.Facts.Count(f => f.FactType == FactTypes.ManagedIlCallObserved) >= 166);
        for (var i = 0; i < 166; i++)
            Assert.Contains(scan.Facts, f => f.FactType == FactTypes.ManagedIlCallObserved
                && f.TargetSymbol?.Contains($"method:7:Case{i:D3}|", StringComparison.Ordinal) == true);
        await MarkdownReportWriter.WriteAsync(Path.Combine(temp.Path, "report.md"), scan);
        Assert.Contains("Compiled-only inputs retain IL call evidence", await File.ReadAllTextAsync(Path.Combine(temp.Path, "report.md")));
    }
}
