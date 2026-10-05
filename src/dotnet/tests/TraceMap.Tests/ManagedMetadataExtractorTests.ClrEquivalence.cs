using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;
using TraceMap.Storage;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string ClrInt = "type(namespace:6:System|names:5:Int32)";
    private const string ClrString = "type(namespace:6:System|names:6:String)";
    private const string ClrMatrix = ClrInt + "[rank=2;sizes=-;lowerBounds=0,0]";
    private const string ClrStatic = "|call:default|hasThis:false|explicitThis:false|";
    private const string SharedType = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:11:SharedShape|";

    // Golden signatures are hand-authored, never obtained from the reader under test.
    [Theory]
    [InlineData("CLR-SIG-001", "Select", "arity:0" + ClrStatic + "(" + ClrInt + ")->" + ClrInt)]
    [InlineData("CLR-SIG-002", "Select", "arity:0" + ClrStatic + "(" + ClrString + ")->" + ClrString)]
    [InlineData("CLR-SIG-003", "Reference", "arity:0" + ClrStatic + "(" + ClrInt + "&)->" + ClrInt)]
    [InlineData("CLR-SIG-004", "Echo", "arity:1" + ClrStatic + "(!!0)->!!0")]
    [InlineData("CLR-SIG-005", "Echo", "arity:2" + ClrStatic + "(!!0)->!!0")]
    [InlineData("CLR-SIG-006", "Rank", "arity:0" + ClrStatic + "(" + ClrInt + "[])->" + ClrInt + "[]")]
    [InlineData("CLR-SIG-007", "Rank", "arity:0" + ClrStatic + "(" + ClrMatrix + ")->" + ClrMatrix)]
    public void Clr_signature_matrix_preserves_equal_shapes_and_distinct_endpoints(string caseId, string name, string signature)
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var methods = ClrMethods(facts);
        Assert.True(methods.Length == 21, $"{caseId}: expected seven methods from each of three languages; got {methods.Length}");
        var matches = methods.Where(fact => fact.Properties["metadataName"] == name
            && fact.Properties["signature"] == signature).ToArray();
        Assert.Equal(3, matches.Length);
        Assert.Equal(3, matches.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.FactId).Distinct().Count());
        foreach (var fact in matches)
        {
            AssertClrProvenance(fact, evaluation.Provenance!, commit);
            Assert.Equal("dotnet.compiled.member.v1", fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
            Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
            Assert.Matches("^0x06[0-9a-f]{6}$", fact.Properties["metadataToken"]);
            Assert.Equal("unbound", fact.Properties["provenanceState"]);
            Assert.StartsWith(fact.Properties["assemblyIdentity"], fact.TargetSymbol);
            Assert.EndsWith("|" + signature, fact.TargetSymbol);
            Assert.Null(fact.Evidence.SnippetHash);
        }
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
        // Shape equality cannot mint a source identity, PDB occurrence, IL body or rewrite edge.
        Assert.All(facts, fact => Assert.StartsWith("dotnet.compiled.", fact.RuleId));
        Assert.DoesNotContain(facts, fact => fact.RuleId == "dotnet.compiled.source-identity.v1"
            || fact.RuleId.StartsWith("dotnet.compiled.pdb", StringComparison.Ordinal)
            || fact.RuleId.StartsWith("dotnet.compiled.il-", StringComparison.Ordinal));
    }

    [Fact]
    public void Clr_signature_counterexamples_and_repeat_output_are_exact()
    {
        var (first, facts, commit) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        Assert.Equal(JsonSerializer.Serialize(first.Provenance, JsonOptions.Stable), JsonSerializer.Serialize(second.Provenance, JsonOptions.Stable));
        Assert.Equal(JsonSerializer.Serialize(facts, JsonOptions.Stable), JsonSerializer.Serialize(repeated, JsonOptions.Stable));
        Assert.Equal(21, ClrMethods(facts).Length);
        foreach (var assembly in ClrMethods(facts).GroupBy(fact => fact.Properties["assemblyIdentity"]))
        {
            Assert.Equal(7, assembly.Select(fact => fact.Properties["signature"]).Distinct().Count());
            Assert.Equal(7, assembly.Select(fact => fact.TargetSymbol).Distinct().Count());
            foreach (var name in new[] { "Select", "Echo", "Rank" })
                Assert.Equal(2, assembly.Count(fact => fact.Properties["metadataName"] == name));
            Assert.All(assembly, fact => AssertClrProvenance(fact, first.Provenance!, commit));
        }
    }

    [Theory]
    [InlineData("csharp", "CLR-SIG-008")]
    [InlineData("vb", "CLR-SIG-009")]
    [InlineData("fsharp", "CLR-SIG-010")]
    public void Clr_signature_duplicate_inputs_remain_ambiguous(string language, string caseId)
    {
        using var temp = new TempDirectory();
        var source = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "duplicate.dll");
        File.Copy(source, copy);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [source, copy]);
        Assert.Equal(2, evaluation.Provenance!.Outcomes.Count);
        Assert.All(evaluation.Provenance.Outcomes, outcome =>
            Assert.Contains("AmbiguousDuplicateManagedAssembly", outcome.GapKinds));
        var gaps = facts.Where(fact => fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly").ToArray();
        Assert.True(gaps.Length == 2, caseId);
        Assert.All(gaps, gap => AssertClrGap(gap, evaluation.Provenance, commit));
        Assert.Equal("compiled-metadata-partial", evaluation.Provenance.CoverageState);
        Assert.DoesNotContain(facts, fact => fact.RuleId == "dotnet.compiled.source-identity.v1");
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Clr_signature_inputs_fail_closed_when_truncated_or_member_limited(string language)
    {
        using var temp = new TempDirectory();
        var source = ClrAssembly(language);
        var truncated = Path.Combine(temp.Path, "truncated.dll");
        // Bounded data-only header truncation; no binary is loaded or executed.
        File.WriteAllBytes(truncated, File.ReadAllBytes(source)[..64]);
        Check([truncated], null, "MalformedManagedInput");
        Check([source], new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");

        void Check(string[] inputs, CompiledInputLimits? limits, string expectedGap)
        {
            var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: inputs, limits: limits);
            AssertGap(evaluation, expectedGap);
            var gap = Assert.Single(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == expectedGap);
            AssertClrGap(gap, evaluation.Provenance!, commit);
            Assert.Empty(ClrMethods(facts));
            Assert.DoesNotContain(facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared);
        }
    }

    private static CodeFact[] ClrMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol?.Contains(SharedType, StringComparison.Ordinal) == true
        && fact.Properties.GetValueOrDefault("metadataName") != ".ctor").ToArray();

    private static string ClrAssembly(string language)
    {
        var assemblies = FixtureAssemblies(FindRepoRoot());
        return language switch { "csharp" => assemblies.CSharp, "vb" => assemblies.VisualBasic, "fsharp" => assemblies.FSharp, _ => throw new ArgumentException(language) };
    }

    private static (CompiledInputEvaluation Evaluation, IReadOnlyList<CodeFact> Facts, string Commit) EvaluateClrMatrix(
        bool reverse = false, string[]? inputs = null, CompiledInputLimits? limits = null)
    {
        var repo = FindRepoRoot();
        var commit = Git(repo, "rev-parse", "HEAD");
        inputs ??= [ClrAssembly("csharp"), ClrAssembly("vb"), ClrAssembly("fsharp")];
        if (reverse) Array.Reverse(inputs);
        var evaluation = ManagedMetadataExtractor.Evaluate(repo, commit,
            new ScanOptions(repo, "unused", CompiledInputPaths: inputs, CompiledInputLimits: limits));
        return (evaluation, ManagedMetadataExtractor.MaterializeFacts(Manifest(commit, evaluation.Provenance), evaluation), commit);
    }

    private static void AssertClrGap(CodeFact gap, CompiledInputProvenance provenance, string commit)
    {
        Assert.Equal("dotnet.compiled.gap.v1", gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        AssertClrProvenance(gap, provenance, commit);
    }

    private static void AssertClrProvenance(CodeFact fact, CompiledInputProvenance provenance, string commit)
    {
        Assert.Equal(commit, fact.CommitSha);
        Assert.Equal("compiled-fixture", fact.Repo);
        Assert.Equal(nameof(ManagedMetadataExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.ManagedMetadataExtractor, fact.Evidence.ExtractorVersion);
        Assert.Equal(1, fact.Evidence.StartLine);
        Assert.Equal(1, fact.Evidence.EndLine);
        Assert.False(string.IsNullOrWhiteSpace(fact.Evidence.FilePath));
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
        Assert.Equal(provenance.BoundedInputSha256, fact.Properties["boundedInputSha256"]);
        Assert.Matches("^[0-9a-f]{64}$", fact.Properties["boundedInputSha256"]);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ManagedMetadataExtractor).Assembly.Location))).ToLowerInvariant(),
            fact.Properties["generatorSha256"]);
    }
}
