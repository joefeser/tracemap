using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string ConstraintOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:15:ConstraintShape|";
    private const string ConstraintSignature = "arity:1" + ClrStatic + "(!!0)->!!0";

    [Theory]
    [InlineData("Free", 0, null)]
    [InlineData("Reference", 4, null)]
    [InlineData("Value", 24, "ValueType")]
    [InlineData("Construct", 16, null)]
    [InlineData("Disposable", 0, "IDisposable")]
    public void Generic_constraint_matrix_keeps_signature_equality_separate_from_constraint_evidence(string name, int attributes, string? constraintName)
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        Assert.All(ConstraintMethods(facts), fact => AssertAccessorEvidence(fact, evaluation.Provenance!, commit));
        var selected = ConstraintMethods(facts).Where(fact => fact.Properties["metadataName"] == name).ToArray();
        Assert.Equal(3, selected.Length);
        Assert.Equal(3, selected.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(3, selected.Select(fact => fact.FactId).Distinct().Count());
        Assert.Equal(3, selected.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        foreach (var language in new[] { "csharp", "vb", "fsharp" })
        {
            var assembly = ClrAssembly(language);
            using var stream = File.OpenRead(assembly);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            var type = reader.GetTypeDefinition(Assert.Single(reader.TypeDefinitions, handle =>
                reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
                && reader.GetString(reader.GetTypeDefinition(handle).Name) == "ConstraintShape"));
            var methodHandle = Assert.Single(type.GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == name);
            var parameterHandle = Assert.Single(reader.GetMethodDefinition(methodHandle).GetGenericParameters());
            var parameter = reader.GetGenericParameter(parameterHandle);
            Assert.Equal(methodHandle, parameter.Parent);
            Assert.Equal(0, parameter.Index);
            // F# struct does not add the CLI default-constructor flag emitted by C#/VB.
            var expectedAttributes = language == "fsharp" && name == "Value" ? 8 : attributes;
            Assert.Equal(expectedAttributes, (int)parameter.Attributes);
            var expectedConstraintName = language == "fsharp" && name == "Value" ? null : constraintName;
            var constraints = parameter.GetConstraints().ToArray();
            if (expectedConstraintName is null) Assert.Empty(constraints);
            else
            {
                var constraint = reader.GetGenericParameterConstraint(Assert.Single(constraints));
                Assert.Equal(parameterHandle, constraint.Parameter);
                Assert.Equal(HandleKind.TypeReference, constraint.Type.Kind);
                var reference = reader.GetTypeReference((TypeReferenceHandle)constraint.Type);
                Assert.Equal("System", reader.GetString(reference.Namespace));
                Assert.Equal(expectedConstraintName, reader.GetString(reference.Name));
                Assert.Equal(HandleKind.AssemblyReference, reference.ResolutionScope.Kind);
                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)reference.ResolutionScope);
                Assert.Equal("System.Runtime", reader.GetString(scope.Name));
                Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
                Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            }
            // Cecil is an independent constraint reader here, never the sole oracle.
            // No assembly loading, dependency resolution, or runtime execution.
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
            var cecilType = Assert.Single(module.Types, item => item.FullName == "TraceMap.CompiledFixtures.Equivalence.ConstraintShape");
            var cecilMethod = Assert.Single(cecilType.Methods, method => method.MetadataToken.ToInt32() == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(methodHandle));
            var cecilParameter = Assert.Single(cecilMethod.GenericParameters);
            Assert.Equal(expectedAttributes, (int)cecilParameter.Attributes);
            Assert.Equal(0, cecilParameter.Position);
            Assert.Equal(expectedConstraintName is null ? Array.Empty<string>() : ["System." + expectedConstraintName],
                cecilParameter.Constraints.Select(constraint => constraint.ConstraintType.FullName).ToArray());
            var (singleEvaluation, singleFacts, singleCommit) = EvaluateClrMatrix(inputs: [assembly]);
            var fact = Assert.Single(ConstraintMethods(singleFacts), item => item.Properties["metadataToken"] == Token(methodHandle));
            var combined = Assert.Single(selected, item => item.TargetSymbol == fact.TargetSymbol);
            Assert.Equal(Token(methodHandle), combined.Properties["metadataToken"]);
            Assert.Equal(fact.Properties["signature"], combined.Properties["signature"]);
            Assert.Equal(fact.Properties["optionalParameterOrdinals"], combined.Properties["optionalParameterOrdinals"]);
            AssertAccessorEvidence(fact, singleEvaluation.Provenance!, singleCommit);
            Assert.Equal(Token(methodHandle), fact.Properties["metadataToken"]);
            Assert.Equal(ConstraintSignature, fact.Properties["signature"]);

        }
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Fact]
    public void Generic_constraint_matrix_does_not_treat_equal_signatures_as_equal_contracts_and_is_deterministic()
    {
        var (first, facts, _) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        var methods = ConstraintMethods(facts);
        Assert.Equal(15, methods.Length);
        Assert.Single(methods.Select(fact => fact.Properties["signature"]).Distinct());
        Assert.Equal(15, methods.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Generic_constraint_matrix_duplicate_malformed_and_member_limited_inputs_remain_explicit_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        var methods = ConstraintMethods(facts);
        Assert.NotEmpty(methods);
        Assert.All(methods, fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(ConstraintMethods(rejectedFacts));
            AssertClrGap(Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] ConstraintMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol?.Contains(ConstraintOwner, StringComparison.Ordinal) == true
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
