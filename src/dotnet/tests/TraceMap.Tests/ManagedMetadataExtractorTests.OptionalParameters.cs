using System.Reflection.PortableExecutable;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    [Theory]
    [InlineData("method", FactTypes.ManagedMethodDeclared)]
    [InlineData("property", FactTypes.ManagedPropertyDeclared)]
    public void Optional_parameter_reader_disagreement_with_identical_identity_is_not_admitted(string kind, string factType)
    {
        var left = new ManagedMetadataExtractor.MetadataObservation(
            kind + ":0x06000001", "same-identity", factType, RuleIds.DotNetCompiledMember,
            kind, "0x06000001", new Dictionary<string, string> { ["optionalParameterOrdinals"] = "2,10" });
        foreach (var rightProperties in new Dictionary<string, string>[]
                 {
                     new() { ["optionalParameterOrdinals"] = "2" },
                     new() { ["optionalParameterOrdinals"] = "10,2" },
                     new()
                 })
        {
            var right = left with { Properties = rightProperties };
            var gap = Assert.Single(ManagedMetadataExtractor.CrossCheck([left], [right]));
            Assert.Equal(left.Key, gap.Key);
            Assert.Equal(left.Identity, gap.CecilIdentity);
            Assert.Equal(right.Identity, gap.SystemReflectionMetadataIdentity);
            Assert.Equal(["MetadataReaderDisagreement"], ManagedMetadataExtractor.ReaderDisagreementGapKinds([gap]));
            Assert.Equal(gap, Assert.Single(ManagedMetadataExtractor.CrossCheck([right], [left])));
        }
        Assert.Empty(ManagedMetadataExtractor.CrossCheck([left], [left]));
    }

    [Fact]
    public void Optional_parameter_cross_language_markers_preserve_their_limited_contract()
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var methods = facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared
            && fact.TargetSymbol!.Contains("|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:OptionalShape|", StringComparison.Ordinal)
            && fact.Properties["metadataName"] != ".ctor").ToArray();
        Assert.Equal(12, methods.Length);
        foreach (var name in new[] { "Required", "OptionalSeven", "OptionalNine", "Wide" })
        {
            var matches = methods.Where(fact => fact.Properties["metadataName"] == name).ToArray();
            Assert.Equal(3, matches.Length);
            Assert.Equal(3, matches.Select(fact => fact.TargetSymbol).Distinct().Count());
            var ordinals = name == "Required" ? "" : name == "Wide" ? "0,1,2,3,4,5,6,7,8,9,10" : "0";
            Assert.All(matches, fact =>
            {
                Assert.Equal(ordinals, fact.Properties["optionalParameterOrdinals"]);
                Assert.Equal(RuleIds.DotNetCompiledMember, fact.RuleId);
                Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
                AssertClrProvenance(fact, evaluation.Provenance!, commit);
            });
        }
        foreach (var assembly in methods.GroupBy(fact => fact.Properties["assemblyIdentity"]))
        {
            var simple = assembly.Where(fact => fact.Properties["metadataName"] != "Wide").ToArray();
            Assert.Equal(3, simple.Length);
            Assert.All(simple, fact => Assert.Equal("arity:0" + ClrStatic + "(" + ClrInt + ")->" + ClrInt, fact.Properties["signature"]));
            // Different default constants do not change signature/optional-marker evidence.
            // No default-value equality or complete API equivalence may be inferred.
            Assert.Equal(2, simple.Select(fact => fact.Properties["optionalParameterOrdinals"]).Distinct().Count());
        }
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Optional_parameter_sparse_setter_rows_do_not_promote_value_to_index_parameter(bool optionalIndex)
    {
        using var temp = new TempDirectory();
        var path = Path.Combine(temp.Path, "setter.dll");
        using (var assembly = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("OptionalSetter", new Version(1, 0)), "OptionalSetter", ModuleKind.Dll))
        {
            var module = assembly.MainModule;
            var type = new TypeDefinition("Fixture", "Container", TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(type);
            var setter = new MethodDefinition("set_Item", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.HideBySig, module.TypeSystem.Void);
            // Without name or flags the index parameter has no Param row; the
            // setter value's sequence is 2 and must never stand in for index 0.
            setter.Parameters.Add(new ParameterDefinition(null, optionalIndex ? ParameterAttributes.Optional : ParameterAttributes.None, module.TypeSystem.Int32));
            setter.Parameters.Add(new ParameterDefinition("value", ParameterAttributes.Optional, module.TypeSystem.Int32));
            setter.Body.Instructions.Add(Instruction.Create(OpCodes.Ret));
            type.Methods.Add(setter);
            type.Properties.Add(new PropertyDefinition("Item", PropertyAttributes.None, module.TypeSystem.Int32) { SetMethod = setter });
            assembly.Write(path);
        }
        using (var pe = new PEReader(File.OpenRead(path)))
        {
            var reader = System.Reflection.Metadata.PEReaderExtensions.GetMetadataReader(pe);
            var definition = reader.GetMethodDefinition(Assert.Single(reader.MethodDefinitions));
            var sequences = definition.GetParameters().Select(handle => reader.GetParameter(handle).SequenceNumber).ToArray();
            Assert.Equal(optionalIndex ? new[] { 1, 2 } : new[] { 2 }, sequences);
        }
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [path]);
        var property = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedPropertyDeclared);
        var setterFact = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared);
        Assert.Equal(optionalIndex ? "0" : "", property.Properties["optionalParameterOrdinals"]);
        Assert.Equal(optionalIndex ? "0,1" : "1", setterFact.Properties["optionalParameterOrdinals"]);
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
        AssertClrProvenance(property, evaluation.Provenance!, commit);
    }
}
