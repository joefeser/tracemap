using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string DefaultOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:12:DefaultShape|";
    private const string DefaultDecimal = RuntimeScope + "type(namespace:6:System|names:7:Decimal)";

    [Theory]
    [InlineData("Required", null, null, ClrInt)]
    [InlineData("IntSeven", ConstantTypeCode.Int32, "07000000", ClrInt)]
    [InlineData("IntNine", ConstantTypeCode.Int32, "09000000", ClrInt)]
    [InlineData("Text", ConstantTypeCode.String, "73006500760065006e00", ClrString)]
    [InlineData("NullText", ConstantTypeCode.NullReference, "00000000", ClrString)]
    [InlineData("DecimalSeven", null, null, DefaultDecimal)]
    public void Default_value_matrix_uses_constant_rows_and_attribute_oracles_separately_from_signatures(
        string name, ConstantTypeCode? code, string? blob, string parameterType)
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var matches = DefaultMethods(facts).Where(fact => fact.Properties["metadataName"] == name).ToArray();
        Assert.Equal(3, matches.Length);
        Assert.Equal(3, matches.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.FactId).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        Assert.All(matches, fact => AssertAccessorEvidence(fact, evaluation.Provenance!, commit));
        foreach (var language in new[] { "csharp", "vb", "fsharp" })
        {
            var assembly = ClrAssembly(language);
            using var stream = File.OpenRead(assembly);
            using var pe = new PEReader(stream);
            var reader = pe.GetMetadataReader();
            var type = reader.GetTypeDefinition(Assert.Single(reader.TypeDefinitions, handle =>
                reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
                && reader.GetString(reader.GetTypeDefinition(handle).Name) == "DefaultShape"));
            var methodHandle = Assert.Single(type.GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == name);
            var parameterHandle = Assert.Single(reader.GetMethodDefinition(methodHandle).GetParameters(), handle => reader.GetParameter(handle).SequenceNumber == 1);
            var parameter = reader.GetParameter(parameterHandle);
            Assert.Equal(name != "Required", (parameter.Attributes & ParameterAttributes.Optional) != 0);
            Assert.Equal(code.HasValue, (parameter.Attributes & ParameterAttributes.HasDefault) != 0);
            var constantHandle = parameter.GetDefaultValue();
            Assert.Equal(!code.HasValue, constantHandle.IsNil);
            if (code.HasValue)
            {
                var constant = reader.GetConstant(constantHandle);
                Assert.Equal(parameterHandle, constant.Parent);
                Assert.Equal(code.Value, constant.TypeCode);
                Assert.Equal(blob, Convert.ToHexString(reader.GetBlobBytes(constant.Value)).ToLowerInvariant());
            }
            using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
            var cecilMethod = Assert.Single(Assert.Single(module.Types, item => item.FullName == "TraceMap.CompiledFixtures.Equivalence.DefaultShape").Methods,
                method => method.MetadataToken.ToInt32() == System.Reflection.Metadata.Ecma335.MetadataTokens.GetToken(methodHandle));
            var cecilParameter = Assert.Single(cecilMethod.Parameters);
            Assert.Equal(name != "Required", cecilParameter.IsOptional);
            Assert.Equal(code.HasValue, cecilParameter.HasConstant);
            if (code.HasValue)
            {
                object? expected = name switch { "IntSeven" => 7, "IntNine" => 9, "Text" => "seven", _ => null };
                Assert.Equal(expected, cecilParameter.Constant);
            }
            var decimalAttributes = cecilParameter.CustomAttributes.Where(attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.DecimalConstantAttribute").ToArray();
            if (name == "DecimalSeven")
            {
                var attribute = Assert.Single(decimalAttributes);
                Assert.Equal(new object[] { (byte)0, (byte)0, 0u, 0u, 7u }, attribute.ConstructorArguments.Select(argument => argument.Value).ToArray());
                var raw = reader.GetCustomAttribute(Assert.Single(parameter.GetCustomAttributes(), handle =>
                {
                    var candidate = reader.GetCustomAttribute(handle);
                    if (candidate.Constructor.Kind != HandleKind.MemberReference) return false;
                    var constructor = reader.GetMemberReference((MemberReferenceHandle)candidate.Constructor);
                    if (constructor.Parent.Kind != HandleKind.TypeReference) return false;
                    var owner = reader.GetTypeReference((TypeReferenceHandle)constructor.Parent);
                    return reader.GetString(owner.Namespace) == "System.Runtime.CompilerServices"
                        && reader.GetString(owner.Name) == "DecimalConstantAttribute";
                }));
                Assert.Equal(parameterHandle, raw.Parent);
                var ctor = reader.GetMemberReference((MemberReferenceHandle)raw.Constructor);
                Assert.Equal(".ctor", reader.GetString(ctor.Name));
                var attributeType = reader.GetTypeReference((TypeReferenceHandle)ctor.Parent);
                Assert.Equal(HandleKind.AssemblyReference, attributeType.ResolutionScope.Kind);
                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)attributeType.ResolutionScope);
                Assert.Equal("System.Runtime", reader.GetString(scope.Name));
                Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
                Assert.Equal("", reader.GetString(scope.Culture));
                Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
                Assert.Equal("2005010505090909", Convert.ToHexString(reader.GetBlobBytes(ctor.Signature)).ToLowerInvariant());
                Assert.Equal("010000000000000000000000070000000000", Convert.ToHexString(reader.GetBlobBytes(raw.Value)).ToLowerInvariant());
            }
            else Assert.Empty(decimalAttributes);

            var (singleEvaluation, singleFacts, singleCommit) = EvaluateClrMatrix(inputs: [assembly]);
            var fact = Assert.Single(DefaultMethods(singleFacts), item => item.Properties["metadataToken"] == Token(methodHandle));
            Assert.Contains(matches, item => item.TargetSymbol == fact.TargetSymbol);
            Assert.Equal("arity:0" + ClrStatic + "(" + parameterType + ")->" + parameterType, fact.Properties["signature"]);
            Assert.Equal(name == "Required" ? "" : "0", fact.Properties["optionalParameterOrdinals"]);
            AssertAccessorEvidence(fact, singleEvaluation.Provenance!, singleCommit);
        }
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Fact]
    public void Default_value_matrix_equal_markers_do_not_prove_equal_defaults_and_reversal_is_deterministic()
    {
        var (first, facts, _) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        Assert.Equal(18, DefaultMethods(facts).Length);
        var integers = DefaultMethods(facts).Where(fact => fact.Properties["metadataName"] is "IntSeven" or "IntNine").ToArray();
        Assert.Equal(6, integers.Length);
        Assert.Single(integers.Select(fact => fact.Properties["signature"]).Distinct());
        Assert.All(integers, fact => Assert.Equal("0", fact.Properties["optionalParameterOrdinals"]));
        Assert.Equal(6, integers.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Default_value_matrix_duplicate_malformed_and_member_limited_inputs_remain_explicit_gaps(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        Assert.NotEmpty(DefaultMethods(facts));
        Assert.All(DefaultMethods(facts), fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(DefaultMethods(rejectedFacts));
            AssertClrGap(Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind), rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    private static CodeFact[] DefaultMethods(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.FactType == FactTypes.ManagedMethodDeclared
        && fact.TargetSymbol?.Contains(DefaultOwner, StringComparison.Ordinal) == true
        && fact.Properties["metadataName"] != ".ctor").ToArray();
}
