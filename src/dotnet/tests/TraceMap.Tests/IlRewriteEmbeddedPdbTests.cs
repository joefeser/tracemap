using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed class IlRewriteEmbeddedPdbTests
{
    [Fact]
    public void Embedded_portable_pdb_binds_to_exact_compiled_assembly_and_admits_identity()
    {
        using var fixture = new Fixture();
        var assembly = fixture.Compile("return input + 1;");
        var bytes = File.ReadAllBytes(assembly);
        var pdb = PortablePdbExtractor.ReadEmbeddedPortablePdb(bytes, 1_000_000);
        Assert.NotNull(pdb);
        Assert.True(PortablePdbExtractor.IsPortablePdb(pdb));
        Assert.False(File.Exists(Path.ChangeExtension(assembly, ".pdb")));

        var result = fixture.Scan(assembly, assembly);
        AssertPdbEnvelope(result);
        var recorder = new ScanReceiptRecorder(new ScanOptions("public-fixture", "unused"));
        recorder.Bind(result);
        Assert.Equal("embedded-portable", Assert.Single(recorder.CreateReceipt().PdbInputProvenance!.Outcomes).Format);
        var provenance = Assert.IsType<PdbInputProvenance>(result.Manifest.PdbInputProvenance);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("admitted", outcome.Outcome);
        Assert.Equal("embedded-portable", outcome.Format);
        Assert.NotNull(outcome.PdbContentId);
        Assert.Equal("pdb-complete", provenance.CoverageState);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted
            && fact.RuleId == RuleIds.DotNetPdbInput
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("pdbFormat") == "embedded-portable"
            && fact.Properties.GetValueOrDefault("pdbGeneratorSha256") == provenance.GeneratorSha256
            && fact.Properties.GetValueOrDefault("pdbBoundedInputSha256") == provenance.BoundedInputSha256);
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled
            && fact.RuleId == RuleIds.DotNetPdbIdentity
            && fact.Properties.GetValueOrDefault("limitation") is { Length: > 0 });
        Assert.Contains(result.Facts, fact => fact.FactType == FactTypes.PdbSequencePointDeclared
            && fact.RuleId == RuleIds.DotNetPdbSequencePoint);
    }

    [Fact]
    public void Assembly_without_embedded_pdb_emits_explicit_missing_gap()
    {
        using var fixture = new Fixture();
        var assembly = fixture.Compile("return input + 1;", embedded: false);
        Assert.Null(PortablePdbExtractor.ReadEmbeddedPortablePdb(File.ReadAllBytes(assembly), 1_000_000));
        var result = fixture.Scan(assembly, assembly);
        AssertPdbEnvelope(result);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties.GetValueOrDefault("gapKind") == "EmbeddedPortablePdbMissing");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
    }

    [Fact]
    public void Embedded_pdb_from_different_assembly_cannot_bind_by_shape()
    {
        using var fixture = new Fixture();
        var expected = fixture.Compile("return input + 1;", name: "Expected");
        var other = fixture.Compile("return input + 2;", name: "Other");
        var result = fixture.Scan(expected, other);
        AssertPdbEnvelope(result);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetPdbGap
            && fact.EvidenceTier == EvidenceTiers.Tier4Unknown
            && fact.Properties.GetValueOrDefault("gapKind") == "PdbAssemblyIdentityMismatch");
        Assert.DoesNotContain(result.Facts, fact => fact.FactType == FactTypes.PdbInputAdmitted);
    }

    [Fact]
    public void Embedded_portable_pdb_rewrite_pair_preserves_both_exact_side_identities()
    {
        using var fixture = new Fixture();
        var compiled = fixture.Compile("return input + 1;");
        var before = fixture.Copy(compiled, "before.dll");
        var after = fixture.Copy(compiled, "after.dll");
        var result = fixture.ScanRewrite(before, after, before, after);
        AssertPdbEnvelope(result);
        var provenance = Assert.IsType<IlRewritePdbProvenance>(result.Manifest.IlRewritePdbProvenance);
        var outcome = Assert.Single(provenance.Outcomes);
        Assert.Equal("admitted", outcome.Outcome);
        Assert.True(outcome.PdbRelationshipCount > 0);
        Assert.Empty(outcome.GapKinds);
        Assert.Contains(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewritePdb
            && fact.EvidenceTier == EvidenceTiers.Tier2Structural
            && fact.Properties.GetValueOrDefault("ilRewritePdbGeneratorSha256") is { Length: > 0 }
            && fact.Properties.GetValueOrDefault("ilRewritePdbBoundedInputSha256") is { Length: > 0 });
    }

    [Fact]
    public void Embedded_pdb_declared_for_other_rewrite_side_is_explicit_mismatch()
    {
        using var fixture = new Fixture();
        var first = fixture.Copy(fixture.Compile("return input + 1;"), "before.dll");
        var second = fixture.Copy(fixture.Compile("return input + 2;"), "after.dll");
        var result = fixture.ScanRewrite(first, second, first, first);
        AssertPdbEnvelope(result);
        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("mismatched", outcome.Outcome);
        Assert.Contains("IlRewritePdbAssemblyBindingMismatch", outcome.GapKinds);
        Assert.Equal("after:EmbeddedPdbAssemblyArtifactMismatch", outcome.Cause);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewritePdb);
    }

    [Fact]
    public void Rewrite_side_without_embedded_pdb_emits_missing_gap()
    {
        using var fixture = new Fixture();
        var before = fixture.Copy(fixture.Compile("return input + 1;"), "before.dll");
        var after = fixture.Copy(fixture.Compile("return input + 2;", embedded: false), "after.dll");
        var result = fixture.ScanRewrite(before, after, before, after);
        AssertPdbEnvelope(result);
        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("unavailable", outcome.Outcome);
        Assert.Equal("after:EmbeddedPortablePdbMissing", outcome.Cause);
        Assert.Contains("IlRewritePdbSideUnavailable", outcome.GapKinds);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewritePdb);
    }

    [Fact]
    public void Embedded_carrier_locator_obeys_the_stricter_rewrite_pdb_text_limit()
    {
        using var fixture = new Fixture();
        var compiled = fixture.Compile("return input + 1;");
        var carrier = Path.Combine(Path.GetDirectoryName(compiled)!, new string('x', 80) + ".dll");
        File.Copy(compiled, carrier);
        var result = fixture.ScanRewrite(carrier, carrier, carrier, carrier,
            new IlRewritePdbLimits(MaxTextLength: 71));
        AssertPdbEnvelope(result);
        var outcome = Assert.Single(result.Manifest.IlRewritePdbProvenance!.Outcomes);
        Assert.Equal("limit-exhausted", outcome.Outcome);
        Assert.Contains("IlRewritePdbTextLimitExceeded", outcome.GapKinds);
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewritePdb);
    }

    private static void AssertPdbEnvelope(ScanResult result)
    {
        var pdbRules = new HashSet<string>(StringComparer.Ordinal)
        {
            RuleIds.DotNetPdbInput, RuleIds.DotNetPdbIdentity,
            RuleIds.DotNetPdbSequencePoint, RuleIds.DotNetPdbGap,
            RuleIds.DotNetIlRewritePdb, RuleIds.DotNetIlRewritePdbGap
        };
        var facts = result.Facts.Where(fact => pdbRules.Contains(fact.RuleId)).ToArray();
        Assert.NotEmpty(facts);
        Assert.All(facts, fact =>
        {
            var rewrite = fact.RuleId is RuleIds.DotNetIlRewritePdb or RuleIds.DotNetIlRewritePdbGap;
            var generatorKey = rewrite ? "ilRewritePdbGeneratorSha256" : "pdbGeneratorSha256";
            var inputKey = rewrite ? "ilRewritePdbBoundedInputSha256" : "pdbBoundedInputSha256";
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties[generatorKey]);
            Assert.Matches("^[0-9a-f]{64}$", fact.Properties[inputKey]);
            Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
            Assert.False(string.IsNullOrWhiteSpace(fact.CommitSha));
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.FilePath));
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.ExtractorId));
            Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.ExtractorVersion));
            Assert.Contains(fact.EvidenceTier,
                new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
        });
    }

    private sealed class Fixture : IDisposable
    {
        private readonly TempDirectory temp = new();
        private string root => temp.Path;
        public Fixture()
        {
            File.WriteAllText(Path.Combine(root, "Fixture.cs"), "public static class Fixture { public static int Compute(int input) => input + 1; }");
            RunGit("init", "-b", "main");
            RunGit("config", "user.email", "fixtures@tracemap.invalid");
            RunGit("config", "user.name", "TraceMap Fixtures");
            RunGit("add", "Fixture.cs");
            RunGit("commit", "-m", "fixture");
        }

        public string Compile(string body, bool embedded = true, string name = "EmbeddedFixture")
        {
            var source = $"public static class Fixture {{ public static int Compute(int input) {{ {body} }} }}";
            var path = Path.Combine(root, "Fixture.cs");
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(source, Encoding.UTF8), path: path);
            var compilation = CSharpCompilation.Create(name, [tree],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
            var assembly = Path.Combine(root, name + ".dll");
            using var output = File.Create(assembly);
            var emit = compilation.Emit(output, options: new EmitOptions(
                debugInformationFormat: embedded ? DebugInformationFormat.Embedded : DebugInformationFormat.PortablePdb));
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            return assembly;
        }

        public ScanResult Scan(string compiledAssembly, string declaredPdb)
        {
            var commit = GitMetadataProvider.Detect(root).CommitSha;
            var initial = ManagedMetadataExtractor.Evaluate(root, commit, new ScanOptions(root, "unused", CompiledInputPaths: [compiledAssembly]));
            var receipt = Path.Combine(root, "binding.json");
            File.WriteAllText(receipt, JsonSerializer.Serialize(new
            {
                schemaVersion = "compiled-input-binding-set.v1",
                bindings = initial.Provenance!.Outcomes.Select(outcome => new
                {
                    schemaVersion = "compiled-input-binding.v1",
                    safeLocator = outcome.SafeLocator,
                    artifactSha256 = outcome.RawFileSha256,
                    assemblyIdentity = outcome.AssemblyIdentity,
                    binarySourceRepository = "public-fixture",
                    binarySourceCommitSha = commit,
                    binaryBuildIdentity = "embedded-pdb-test-build"
                }).ToArray()
            }));
            return ScanEngine.Scan(new ScanOptions(root, Path.Combine(root, "scan-" + Guid.NewGuid().ToString("N")),
                CompiledInputPaths: [compiledAssembly], CompiledBindingReceiptPaths: [receipt], PdbInputPaths: [declaredPdb]));
        }

        public string Copy(string source, string name)
        {
            var target = Path.Combine(root, name);
            File.Copy(source, target, overwrite: true);
            return target;
        }

        public ScanResult ScanRewrite(string before, string after, string beforePdb, string afterPdb,
            IlRewritePdbLimits? pdbLimits = null) =>
            ScanEngine.Scan(new ScanOptions(root, Path.Combine(root, "rewrite-" + Guid.NewGuid().ToString("N")),
                IlRewriteEvidence: true,
                IlRewriteBeforePaths: [before],
                IlRewriteAfterPaths: [after],
                IlRewritePdbEvidence: true,
                IlRewriteBeforePdbPaths: [beforePdb],
                IlRewriteAfterPdbPaths: [afterPdb],
                IlRewritePdbLimits: pdbLimits));

        private void RunGit(params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments) start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, output);
        }

        public void Dispose() => temp.Dispose();
    }
}
