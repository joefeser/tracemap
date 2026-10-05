using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class ManagedMetadataExtractorTests
{
    private const string AccessorType = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:AccessorShape|";
    private const string InstanceMethod = "arity:0|call:default|hasThis:true|explicitThis:false|";
    private const string InstanceProperty = "call:default|hasThis:true|()->" + ClrInt;
    private const string ClrVoid = "type(namespace:6:System|names:4:Void)";
    private const string RuntimeScope = "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)";
    private const string EventHandlerType = RuntimeScope + "type(namespace:6:System|names:12:EventHandler)";
    private const string EventArgsType = RuntimeScope + "type(namespace:6:System|names:9:EventArgs)";

    [Theory]
    [InlineData("Value", FactTypes.ManagedPropertyDeclared, InstanceProperty)]
    [InlineData("Snapshot", FactTypes.ManagedPropertyDeclared, InstanceProperty)]
    [InlineData("Changed", FactTypes.ManagedEventDeclared, EventHandlerType)]
    public void Property_event_matrix_compares_shapes_but_preserves_three_member_endpoints(string name, string kind, string signature)
    {
        var (evaluation, facts, commit) = EvaluateClrMatrix();
        var matches = AccessorMembers(facts).Where(fact => fact.FactType == kind && fact.Properties["metadataName"] == name).ToArray();
        Assert.Equal(3, matches.Length);
        Assert.Equal(3, matches.Select(fact => fact.TargetSymbol).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.FactId).Distinct().Count());
        Assert.Equal(3, matches.Select(fact => fact.Properties["assemblyIdentity"]).Distinct().Count());
        Assert.All(matches, fact =>
        {
            Assert.Equal(signature, fact.Properties["signature"]);
            AssertAccessorEvidence(fact, evaluation.Provenance!, commit);
        });
        Assert.DoesNotContain(facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "MetadataReaderDisagreement");
    }

    [Theory]
    [InlineData("csharp", false)]
    [InlineData("vb", true)]
    [InlineData("fsharp", false)]
    public void Property_event_matrix_uses_metadata_semantics_handles_not_accessor_looking_names(string language, bool hasRaiser)
    {
        var assembly = ClrAssembly(language);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly]);
        var members = AccessorMembers(facts);
        var byToken = members.ToDictionary(fact => fact.Properties["metadataToken"], StringComparer.Ordinal);
        using var stream = File.OpenRead(assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        var typeHandle = Assert.Single(reader.TypeDefinitions, handle =>
            reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "AccessorShape");
        var type = reader.GetTypeDefinition(typeHandle);
        var properties = type.GetProperties().ToDictionary(handle => reader.GetString(reader.GetPropertyDefinition(handle).Name));
        Assert.Equal(2, properties.Count);
        var value = reader.GetPropertyDefinition(properties["Value"]).GetAccessors();
        var snapshot = reader.GetPropertyDefinition(properties["Snapshot"]).GetAccessors();
        Assert.Empty(value.Others);
        Assert.Empty(snapshot.Others);
        Assert.True(snapshot.Setter.IsNil);
        var valueFact = byToken[Token(properties["Value"])];
        var snapshotFact = byToken[Token(properties["Snapshot"])];
        Assert.Equal(valueFact.Properties["signature"], snapshotFact.Properties["signature"]);
        Assert.NotEqual(valueFact.TargetSymbol, snapshotFact.TargetSymbol);

        var eventHandle = Assert.Single(type.GetEvents());
        var eventDefinition = reader.GetEventDefinition(eventHandle);
        Assert.Equal("Changed", reader.GetString(eventDefinition.Name));
        var accessors = eventDefinition.GetAccessors();
        Assert.Empty(accessors.Others);
        Assert.Equal(!hasRaiser, accessors.Raiser.IsNil);
        var handles = new HashSet<MethodDefinitionHandle>();
        Check(value.Getter, valueFact, InstanceMethod + "()->" + ClrInt);
        Check(value.Setter, valueFact, InstanceMethod + "(" + ClrInt + ")->" + ClrVoid);
        Check(snapshot.Getter, snapshotFact, InstanceMethod + "()->" + ClrInt);
        var eventFact = byToken[Token(eventHandle)];
        Check(accessors.Adder, eventFact, InstanceMethod + "(" + EventHandlerType + ")->" + ClrVoid);
        Check(accessors.Remover, eventFact, InstanceMethod + "(" + EventHandlerType + ")->" + ClrVoid);
        if (hasRaiser)
            Check(accessors.Raiser, eventFact, InstanceMethod + "(type(namespace:6:System|names:6:Object)," + EventArgsType + ")->" + ClrVoid);
        Assert.Equal(hasRaiser ? 6 : 5, handles.Count);

        var decoy = Assert.Single(type.GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == "get_Unbound");
        Assert.DoesNotContain(decoy, handles);
        Assert.Equal(0, (int)(reader.GetMethodDefinition(decoy).Attributes & MethodAttributes.SpecialName));
        var decoyFact = byToken[Token(decoy)];
        Assert.Equal(InstanceMethod + "()->" + ClrInt, decoyFact.Properties["signature"]);
        Assert.DoesNotContain(properties.Keys, name => name == "Unbound");
        Assert.All(members, fact => AssertAccessorEvidence(fact, evaluation.Provenance!, commit));

        void Check(MethodDefinitionHandle handle, CodeFact owner, string signature)
        {
            Assert.False(handle.IsNil);
            Assert.True(handles.Add(handle));
            var method = byToken[Token(handle)];
            Assert.Equal(FactTypes.ManagedMethodDeclared, method.FactType);
            Assert.Equal(signature, method.Properties["signature"]);
            Assert.NotEqual(owner.TargetSymbol, method.TargetSymbol);
            Assert.NotEqual(owner.FactId, method.FactId);
            Assert.Equal(owner.Properties["assemblyIdentity"], method.Properties["assemblyIdentity"]);
            Assert.NotEqual(0, (int)(reader.GetMethodDefinition(handle).Attributes & MethodAttributes.SpecialName));
        }
    }

    [Fact]
    public void Property_event_matrix_reversed_inputs_preserve_all_facts_and_provenance()
    {
        var (first, facts, _) = EvaluateClrMatrix();
        var (second, repeated, _) = EvaluateClrMatrix(reverse: true);
        Assert.Equal(JsonSerializer.Serialize(first.Provenance), JsonSerializer.Serialize(second.Provenance));
        Assert.Equal(JsonSerializer.Serialize(facts), JsonSerializer.Serialize(repeated));
    }

    [Theory]
    [InlineData("csharp")]
    [InlineData("vb")]
    [InlineData("fsharp")]
    public void Property_event_matrix_duplicate_malformed_and_member_limited_inputs_fail_closed(string language)
    {
        using var temp = new TempDirectory();
        var assembly = ClrAssembly(language);
        var copy = Path.Combine(temp.Path, "copy.dll");
        File.Copy(assembly, copy);
        var (evaluation, facts, commit) = EvaluateClrMatrix(inputs: [assembly, copy]);
        var members = AccessorMembers(facts);
        Assert.NotEmpty(members);
        Assert.All(members, fact => Assert.NotEqual("eligible", fact.Properties["sourceReconciliationEligibility"]));
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
            Assert.Empty(AccessorMembers(rejectedFacts));
            var gap = Assert.Single(rejectedFacts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind);
            AssertClrGap(gap, rejected.Provenance!, rejectedCommit);
            Assert.Equal("compiled-metadata-partial", rejected.Provenance!.CoverageState);
        }
    }

    private static string Token(EntityHandle handle) => "0x" + MetadataTokens.GetToken(handle).ToString("x8", System.Globalization.CultureInfo.InvariantCulture);

    private static CodeFact[] AccessorMembers(IReadOnlyList<CodeFact> facts) => facts.Where(fact =>
        fact.TargetSymbol?.Contains(AccessorType, StringComparison.Ordinal) == true
        && fact.FactType is FactTypes.ManagedMethodDeclared or FactTypes.ManagedPropertyDeclared or FactTypes.ManagedEventDeclared).ToArray();

    private static void AssertAccessorEvidence(CodeFact fact, CompiledInputProvenance provenance, string commit)
    {
        AssertClrProvenance(fact, provenance, commit);
        Assert.Equal("dotnet.compiled.member.v1", fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
        var prefix = fact.FactType switch { FactTypes.ManagedMethodDeclared => "06", FactTypes.ManagedPropertyDeclared => "17", _ => "14" };
        Assert.Matches("^0x" + prefix + "[0-9a-f]{6}$", fact.Properties["metadataToken"]);
        Assert.StartsWith(fact.Properties["assemblyIdentity"], fact.TargetSymbol);
    }
}
