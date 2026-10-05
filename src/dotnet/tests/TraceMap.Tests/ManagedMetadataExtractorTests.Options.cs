using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string OptionType = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:11:OptionShape|";
    private const string NullableInt = "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:6:System|names:10:Nullable`1)<" + ClrInt + ">";
    private const string FSharpScope = "scope(assembly:name:11:FSharp.Core|version:8:10.1.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
    private const string OptionInt = FSharpScope + "type(namespace:21:Microsoft.FSharp.Core|names:14:FSharpOption`1)<" + ClrInt + ">";
    private const string ValueOptionInt = FSharpScope + "type(namespace:21:Microsoft.FSharp.Core|names:19:FSharpValueOption`1)<" + ClrInt + ">";

    [Fact]
    public void Nullable_option_matrix_matches_nullable_shapes_without_collapsing_assembly_endpoints()
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var methods = OptionMethods(facts);
        Assert.Equal(6, methods.Length);
        var nullable = methods.Where(fact => fact.Properties["metadataName"] == "NullableRoundtrip").ToArray();
        Assert.Equal(3, nullable.Length);
        Assert.Equal(3, nullable.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(3, nullable.Select(fact => fact.FactId).Distinct().Count());
        Assert.Equal(3, nullable.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        Assert.All(nullable, fact => Assert.Equal(Roundtrip(NullableInt), fact.Properties["signature"]));
        Assert.All(methods, fact => AssertOptionEvidence(fact, evaluation.Provenance!, commit));
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Theory]
    [InlineData("OptionRoundtrip", OptionInt)]
    [InlineData("ValueOptionRoundtrip", ValueOptionInt)]
    [InlineData("OptionalArgument", OptionInt)]
    public void Nullable_option_matrix_preserves_fsharp_wrappers_and_source_optional_nonclaim(string name, string wrapper)
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var method = Assert.Single(OptionMethods(facts), fact => fact.Properties["metadataName"] == name);
        Assert.Equal(Roundtrip(wrapper), method.Properties["signature"]);
        Assert.NotEqual(Roundtrip(NullableInt), method.Properties["signature"]);
        // F# ?arg is an option-valued signature, not a CLI Optional Param flag.
        Assert.Equal(string.Empty, method.Properties["optionalParameterOrdinals"]);
        AssertOptionEvidence(method, evaluation.Provenance!, commit);
        Assert.DoesNotContain(facts, fact => fact.RuleId == RuleIds.DotNetCompiledSourceIdentity);
    }

    [Fact]
    public void Nullable_option_matrix_preserves_distinct_wrappers_and_deterministic_facts()
    {
        var (evaluation, facts, _) = EvaluateClrMatrix();
        var (reversed, repeated, _) = EvaluateClrMatrix(reverse: true);
        Assert.Equal(JsonSerializer.Serialize(evaluation.Provenance), JsonSerializer.Serialize(reversed.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
        var methods = OptionMethods(facts);
        Assert.Equal(3, methods.Select(fact => fact.Properties["signature"]).Distinct().Count());
        var option = Assert.Single(methods, fact => fact.Properties["metadataName"] == "OptionRoundtrip");
        var optional = Assert.Single(methods, fact => fact.Properties["metadataName"] == "OptionalArgument");
        Assert.Equal(option.Properties["signature"], optional.Properties["signature"]);
        Assert.NotEqual(option.TargetSymbol, optional.TargetSymbol);
        Assert.NotEqual(option.FactId, optional.FactId);
    }

    private static string Roundtrip(string type) => "arity:0" + ClrStatic + "(" + type + ")->" + type;

    [Theory]
    [InlineData("csharp", 1)]
    [InlineData("vb", 1)]
    [InlineData("fsharp", 4)]
    public void Nullable_option_matrix_keeps_duplicate_malformed_and_member_limit_gaps(string language, int methodCount)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var duplicate = Path.Combine(temp.Path, "duplicate.dll");
        File.Copy(assembly, duplicate);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, duplicate]);
        var duplicates = OptionMethods(facts);
        Assert.Equal(methodCount * 2, duplicates.Length);
        Assert.All(duplicates, fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
        var gaps = facts.Where(fact => fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly").ToArray();
        Assert.Equal(2, gaps.Length);
        Assert.All(gaps, gap => AssertClrGap(gap, evaluation.Provenance!, commit));

        var truncated = Path.Combine(temp.Path, "truncated.dll");
        File.WriteAllBytes(truncated, File.ReadAllBytes(assembly)[..64]);
        Check(truncated, null, "MalformedManagedInput");
        Check(assembly, new CompiledInputLimits(MaxMemberCount: 1), "ManagedInputMemberCountLimitExceeded");

        void Check(string input, CompiledInputLimits? limits, string kind)
        {
            var (rejected, rejectedFacts, rejectedCommit) = EvaluateClrMatrix(inputs: [input], limits: limits);
            Assert.Empty(OptionMethods(rejectedFacts));
            Assert.DoesNotContain(rejectedFacts, fact => fact.FactType == FactTypes.ManagedMethodDeclared);
            var gap = Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind);
            AssertClrGap(gap, rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] OptionMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol?.Contains(OptionType, StringComparison.Ordinal) == true
        && fact.Properties["metadataName"] != ".ctor").ToArray();

    private static void AssertOptionEvidence(CodeFact fact, CompiledInputProvenance provenance, string commit)
    {
        AssertClrProvenance(fact, provenance, commit);
        Assert.Equal("dotnet.compiled.member.v1", fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
        Assert.Matches("^0x06[0-9a-f]{6}$", fact.Properties["metadataToken"]);
        Assert.StartsWith(fact.Properties["assemblyIdentity"], fact.TargetSymbol);
        Assert.EndsWith("|" + fact.Properties["signature"], fact.TargetSymbol);
        Assert.Equal("unbound", fact.Properties["provenanceState"]);
    }
}
