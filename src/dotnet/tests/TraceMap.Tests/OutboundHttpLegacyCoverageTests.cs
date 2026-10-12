using System.Security.Cryptography;
using TraceMap.Core;
using TraceMap.Reporting;
using TraceMap.Storage;
using Mono.Cecil;

namespace TraceMap.Tests;

public sealed class OutboundHttpLegacyCoverageTests
{
    [Fact]
    public async Task Projectless_vb_website_reports_candidates_and_partial_coverage()
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var sample = Path.Combine(repo.FullName, "samples/outbound-http-coverage/legacy/net472/website-projectless");
        using var temp = new TempDirectory();
        var result = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "out")));
        var candidate = Assert.Single(result.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier);
        Assert.Equal("unresolved", candidate.Properties["methodFamily"]);
        var gap = Assert.Single(result.Facts, fact => fact.RuleId == RuleIds.VisualBasicHttpCoverageGap);
        Assert.Equal("App_Code/RemoteExample.vb", gap.Evidence.FilePath);
        Assert.Equal("file-level-coverage-not-callsite", gap.Properties["spanKind"]);
        Assert.Equal("reduces-http-coverage", gap.Properties["coverageEffect"]);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        var report = Path.Combine(temp.Path, "report.md");
        await MarkdownReportWriter.WriteAsync(report, result);
        Assert.Contains("VisualBasicHttpAnalysisPartialWithoutSemantics", await File.ReadAllTextAsync(report));
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, result.Manifest, result.Facts);
        var paths = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path,
            FromSymbol: "RemoteExample.Run", ToSurface: "http-client"));
        var handoff = GroupedCompiledPathHandoffBuilder.Create(paths,
            Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index))));
        Assert.Contains(handoff.Nodes.Values, node => node.HttpCallEvidence is
            { CoverageLabel: "reduced-explicit-receiver-candidate", MethodFamily: "unresolved" });
        Assert.NotNull(handoff.BackendExtraction);
        Assert.Contains(handoff.BackendExtraction.Operations, operation => operation.Family == "outbound-http"
            && operation.ConnectionState == "candidate-query-path");
        var outputs = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, "grouped"));
        Assert.Contains("reduced-explicit-receiver-candidate", await File.ReadAllTextAsync(outputs.HtmlPath));
        // A coverage warning applies even when no HTTP-looking names exist: it
        // describes unsupported analysis, never claims a hidden HTTP call.
        var empty = Path.Combine(temp.Path, "plain"); Directory.CreateDirectory(empty);
        await File.WriteAllTextAsync(Path.Combine(empty, "Plain.vb"), "Public Class Plain\nEnd Class");
        var plain = ScanEngine.Scan(new ScanOptions(empty, Path.Combine(temp.Path, "plain-out")));
        Assert.Single(plain.Facts, fact => fact.RuleId == RuleIds.VisualBasicHttpCoverageGap);
        Assert.DoesNotContain(plain.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
    }

    [Theory]
    [InlineData("net20", "csharp", 57, "2.0.0.0")]
    [InlineData("net20", "vb", 57, "2.0.0.0")]
    [InlineData("net35", "csharp", 57, "2.0.0.0")]
    [InlineData("net35", "vb", 57, "2.0.0.0")]
    [InlineData("net40", "csharp", 57, "4.0.0.0")]
    [InlineData("net40", "vb", 57, "4.0.0.0")]
    [InlineData("net45", "csharp", 116, "4.0.0.0")]
    [InlineData("net45", "vb", 116, "4.0.0.0")]
    [InlineData("net48", "csharp", 116, "4.0.0.0")]
    [InlineData("net48", "vb", 116, "4.0.0.0")]
    [InlineData("net452", "csharp", 116, "4.0.0.0")]
    [InlineData("net452", "vb", 116, "4.0.0.0")]
    [InlineData("net472", "csharp", 116, "4.0.0.0")]
    [InlineData("net472", "vb", 116, "4.0.0.0")]
    public async Task Legacy_reference_overloads_preserve_identity_signatures_and_mode_limits(string tfm, string language, int count, string systemVersion)
    {
        var repo = new DirectoryInfo(AppContext.BaseDirectory);
        while (repo is not null && !File.Exists(Path.Combine(repo.FullName, "AGENTS.md"))) repo = repo.Parent;
        Assert.NotNull(repo);
        var sample = Path.Combine(repo.FullName, "samples", "outbound-http-coverage", "legacy", tfm, language);
        var configuration = new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;
        var binary = Path.Combine(sample, "bin", configuration, tfm, $"HttpLegacy.{tfm}.{language}.dll");
        using var module = ModuleDefinition.ReadModule(binary);
        var system = Assert.Single(module.AssemblyReferences, reference => reference.Name == "System");
        Assert.Equal(systemVersion, system.Version.ToString());
        Assert.Equal("b77a5c561934e089", Convert.ToHexStringLower(system.PublicKeyToken));
        Assert.Equal(tfm is "net45" or "net452" or "net472" or "net48", module.AssemblyReferences.Any(reference => reference.Name == "System.Net.Http"));
        // The same System 2.0 identity occurs under both net20 and net35. It is not a TFM.
        var frameworkAttribute = module.Assembly.CustomAttributes.FirstOrDefault(attribute => attribute.AttributeType.FullName == "System.Runtime.Versioning.TargetFrameworkAttribute");
        var expectedFramework = tfm is "net20" or "net35" ? null : ".NETFramework,Version=v" + string.Join(".", tfm[3..].ToCharArray());
        Assert.Equal(expectedFramework, frameworkAttribute?.ConstructorArguments[0].Value);
        using var temp = new TempDirectory();
        var semantic = ScanEngine.Scan(new ScanOptions(sample, Path.Combine(temp.Path, "semantic")));
        Assert.Equal("Succeeded", semantic.Manifest.BuildStatus);
        var calls = semantic.Facts.Where(fact => fact.FactType == FactTypes.HttpCallDetected && fact.EvidenceTier == EvidenceTiers.Tier1Semantic).ToArray();
        Assert.Equal(count, calls.Length);
        Assert.DoesNotContain(semantic.Facts, fact => fact.RuleId == RuleIds.VisualBasicHttpCoverageGap);
        var index = Path.Combine(temp.Path, "index.sqlite");
        SqliteIndexWriter.Write(index, semantic.Manifest, semantic.Facts);
        var hash = Convert.ToHexStringLower(SHA256.HashData(await File.ReadAllBytesAsync(index)));
        var empty = Path.Combine(temp.Path, "empty"); Directory.CreateDirectory(empty);
        var compiled = ScanEngine.Scan(new ScanOptions(empty, Path.Combine(temp.Path, "compiled"), CompiledInputPaths: [binary], IlBodyEvidence: true));
        Assert.DoesNotContain(compiled.Facts, fact => fact.FactType == FactTypes.HttpCallDetected);
        Assert.DoesNotContain(compiled.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
        var assemblyFact = Assert.Single(compiled.Facts, fact => fact.FactType == FactTypes.ManagedAssemblyDeclared);
        Assert.Equal(expectedFramework, assemblyFact.Properties.GetValueOrDefault("targetFramework"));
        foreach (var method in module.Types.Single(type => type.Name == "LegacyMatrix").Methods.Where(method => method.Name.StartsWith("Case", StringComparison.Ordinal)))
        {
            var emitted = Assert.Single(method.Body.Instructions.Select(instruction => instruction.Operand).OfType<MethodReference>(),
                reference => reference.DeclaringType.FullName is "System.Net.WebClient" or "System.Net.WebRequest" or "System.Net.HttpWebRequest" or "System.Net.Http.HttpClient" or "System.Net.Http.HttpMessageInvoker");
            var referenceAssembly = Assert.IsType<AssemblyNameReference>(emitted.DeclaringType.Scope);
            var semanticCall = Assert.Single(calls, fact => fact.SourceSymbol?.Contains($".{method.Name}(", StringComparison.Ordinal) == true);
            Assert.Equal(emitted.Name, semanticCall.Properties["methodName"]);
            Assert.Equal(referenceAssembly.Name, semanticCall.Properties["targetAssemblyName"]);
            Assert.Equal(Convert.ToHexStringLower(referenceAssembly.PublicKeyToken), semanticCall.Properties["targetAssemblyPublicKeyToken"]);
            Assert.Equal(RuleIds.HttpClientInvocation, semanticCall.RuleId);
            var il = Assert.Single(compiled.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved
                && fact.TargetSymbol?.Contains($"method:7:{method.Name}|", StringComparison.Ordinal) == true
                && fact.Properties["targetIdentity"].Contains($"|member:{emitted.Name.Length}:{emitted.Name}|", StringComparison.Ordinal));
            // Independent Cecil decoding checks the entire encoded parameter/return/calling signature.
            var signature = ManagedMetadataExtractor.MethodSignature(ManagedMetadataExtractor.FormatType(emitted.ReturnType),
                emitted.Parameters.Select(parameter => ManagedMetadataExtractor.FormatType(parameter.ParameterType)), emitted.GenericParameters.Count, "default", emitted.HasThis, emitted.ExplicitThis);
            Assert.EndsWith("|" + signature, il.Properties["targetIdentity"], StringComparison.Ordinal);
            Assert.Contains($"version:{referenceAssembly.Version.ToString().Length}:{referenceAssembly.Version}", il.Properties["targetIdentity"], StringComparison.Ordinal);
            Assert.Matches("^[0-9a-f]{64}$", il.Properties["ilGeneratorSha256"]);
            Assert.Matches("^[0-9a-f]{64}$", il.Properties["ilBoundedInputSha256"]);
            var report = await CombinedDependencyPathReporter.BuildReportAsync(new(index, temp.Path, FromSymbol: semanticCall.SourceSymbol, ToSurface: "http-client"));
            var handoff = GroupedCompiledPathHandoffBuilder.Create(report, hash);
            Assert.Contains(handoff.Nodes.Values, node => node.HttpCallEvidence?.MethodName == emitted.Name && node.RuleId == RuleIds.HttpClientInvocation);
            var output = await GroupedCompiledPathReportWriter.WriteAsync(handoff, Path.Combine(temp.Path, method.Name));
            Assert.Contains(emitted.Name, await File.ReadAllTextAsync(output.HtmlPath));
        }
        await MarkdownReportWriter.WriteAsync(Path.Combine(temp.Path, "compiled.md"), compiled);
        Assert.Contains("Compiled-only inputs retain IL call evidence", await File.ReadAllTextAsync(Path.Combine(temp.Path, "compiled.md")));
        var projectlessRoot = Path.Combine(temp.Path, "projectless"); Directory.CreateDirectory(projectlessRoot);
        File.Copy(Path.Combine(sample, language == "vb" ? "Matrix.vb" : "Matrix.cs"), Path.Combine(projectlessRoot, language == "vb" ? "Matrix.vb" : "Matrix.cs"));
        var projectless = ScanEngine.Scan(new ScanOptions(projectlessRoot, Path.Combine(temp.Path, "fallback")));
        var candidates = projectless.Facts.Where(fact => fact.FactType == FactTypes.HttpCallDetected).ToArray();
        if (language == "vb")
        {
            Assert.Equal(count, candidates.Length);
            Assert.All(candidates, candidate => Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier));
            Assert.Contains(projectless.Facts, fact => fact.RuleId == RuleIds.VisualBasicHttpCoverageGap
                && fact.EvidenceTier == EvidenceTiers.Tier4Unknown);
        }
        else
        {
            Assert.Equal(count, candidates.Length);
            Assert.All(candidates, candidate => { Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, candidate.EvidenceTier); Assert.Equal("unresolved", candidate.Properties["methodFamily"]); });
        }
    }
}
