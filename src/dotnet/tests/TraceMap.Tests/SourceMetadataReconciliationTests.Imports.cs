using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class SourceMetadataReconciliationTests
{
    private const string ImportAssembly = "assembly:name:28:CompiledEvidence.VisualBasic|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:32:CompiledEvidence.VisualBasic.dll|targetFramework:25:.NETCoreApp,Version=v10.0";
    private const string ImportOwner = "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:13:ImportsMatrix|arity:0";
    private static readonly (string Name, int Line)[] ImportCases = [("ViaProject", 15), ("ViaAlias", 18), ("ViaDefault", 21)];
    private static string ImportNamespace(string name) => "TraceMap.CompiledFixtures." + (name == "ViaProject" ? "ProjectImported" : "FileImported");
    private static string ImportType(string name) => name == "ViaDefault"
        ? "scope(assembly:name:18:System.Collections|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:26:System.Collections.Generic|names:6:List`1)<type(namespace:6:System|names:5:Int32)>"
        : "scope(assembly:name:28:CompiledEvidence.VisualBasic|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null)type(namespace:" + ImportNamespace(name).Length + ":" + ImportNamespace(name) + "|names:11:ImportToken)";
    private static string ImportSignature(string name) => "arity:0|call:default|hasThis:false|explicitThis:false|(" + ImportType(name) + ")->" + ImportType(name);
    private static string ImportIdentity(string name) => ImportAssembly + ImportOwner + "|method:" + name.Length + ":" + name + "|" + ImportSignature(name);
    private static string ImportDeclaration(string name)
    {
        var owner = "visualbasic type CompiledEvidence.VisualBasic%401.0.0.0 TraceMap.CompiledFixtures.Equivalence.ImportsMatrix";
        var type = name == "ViaDefault"
            ? "System.Collections@10.0.0.0:System.Collections.Generic.List(Of T)<System.Runtime@10.0.0.0:Integer>"
            : "CompiledEvidence.VisualBasic@1.0.0.0:" + ImportNamespace(name) + ".ImportToken";
        return "visualbasic method " + Uri.EscapeDataString(owner) + " " + name + "(" + Uri.EscapeDataString(type) + ")->" + Uri.EscapeDataString(type);
    }
    private static string ImportSource() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "vb");

    [Fact]
    public void Import_matrix_joins_project_alias_and_default_imports_by_complete_identity()
    {
        var source = ImportSource();
        var assembly = FixtureAssemblyPath(source, "CompiledEvidence.VisualBasic.dll");
        var scan = ScanBound(source, [assembly]);
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        using var pe = new PEReader(File.OpenRead(assembly));
        var reader = pe.GetMetadataReader();
        var owner = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(handle).Name) == "ImportsMatrix");
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var cecilOwner = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.ImportsMatrix");
        foreach (var (name, line) in ImportCases)
        {
            var handle = Assert.Single(reader.GetTypeDefinition(owner).GetMethods(), candidate => reader.GetString(reader.GetMethodDefinition(candidate).Name) == name);
            var method = reader.GetMethodDefinition(handle);
            Assert.True(method.Attributes.HasFlag(MethodAttributes.Static));
            Assert.Equal(MethodAttributes.Public, method.Attributes & MethodAttributes.MemberAccessMask);
            var blob = reader.GetBlobReader(method.Signature);
            Assert.Equal(new byte[] { 0, 1 }, blob.ReadBytes(2));
            var returnHandle = ReadType(ref blob);
            Assert.Equal(returnHandle, ReadType(ref blob));
            Assert.Equal(0, blob.RemainingBytes);
            var cecil = Assert.Single(cecilOwner.Methods, item => item.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
            Assert.Equal(name, cecil.Name);
            Assert.True(cecil.IsStatic && cecil.IsPublic);
            foreach (var type in new[] { cecil.ReturnType, Assert.Single(cecil.Parameters).ParameterType })
            {
                if (name == "ViaDefault")
                {
                    var constructed = Assert.IsType<Mono.Cecil.GenericInstanceType>(type);
                    Assert.Equal("System.Int32", Assert.Single(constructed.GenericArguments).FullName);
                    Assert.Equal("System.Collections.Generic.List`1", constructed.ElementType.FullName);
                    Assert.Equal("System.Collections, Version=10.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a", constructed.ElementType.Scope.ToString());
                }
                else
                {
                    Assert.Equal(ImportNamespace(name) + ".ImportToken", type.FullName);
                    Assert.Equal(MetadataTokens.GetToken(returnHandle), type.MetadataToken.ToInt32());
                    Assert.Equal("CompiledEvidence.VisualBasic.dll", type.Scope.Name);
                }
            }
            var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == ImportIdentity(name));
            Assert.Equal($"0x{MetadataTokens.GetToken(handle):x8}", member.Properties["metadataToken"]);
            Assert.Equal(ImportSignature(name), member.Properties["signature"]);
            CheckImportCompiled(scan, member, assembly);
            var edge = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity(name));
            var observation = Assert.Single(scan.Facts, fact => fact.FactId == edge.Properties["sourceFactId"]);
            CheckImportSource(scan, edge, name, line, EvidenceTiers.Tier1Semantic);
            CheckImportSource(scan, observation, name, line, EvidenceTiers.Tier1Semantic);
            Assert.Equal(FactTypes.SourceMetadataIdentityObserved, observation.FactType);
            Assert.Equal(member.FactId, edge.Properties["compiledFactId"]);
            Assert.Equal("bound", edge.Properties["compiledProvenanceState"]);
            Assert.Equal("exact-one-candidate", edge.Properties["reconciliationState"]);
            Assert.Equal(ScannerVersions.ManagedMetadataExtractor, edge.Properties["compiledExtractorVersion"]);
            Assert.Equal(ScannerVersions.VisualBasicSemanticExtractor, edge.Properties["sourceExtractorVersion"]);
            Assert.Equal("Visual Basic", edge.Properties["sourceLanguage"]);
            Assert.Equal(member.Properties["generatorSha256"], edge.Properties["compiledGeneratorSha256"]);
            foreach (var key in new[] { "boundedInputSha256", "provenanceBindingInputSha256" }) Assert.Equal(member.Properties[key], edge.Properties[key]);
            Assert.Contains("does not prove runtime", edge.Properties["limitation"]);
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap && fact.TargetSymbol == member.TargetSymbol);

            EntityHandle ReadType(ref BlobReader signature)
            {
                if (name == "ViaDefault") Assert.Equal(0x15, signature.ReadByte()); // GENERICINST
                Assert.Equal(0x12, signature.ReadByte()); // CLASS
                var typeHandle = signature.ReadTypeHandle();
                if (name == "ViaDefault")
                {
                    Assert.Equal(HandleKind.TypeReference, typeHandle.Kind);
                    var type = reader.GetTypeReference((TypeReferenceHandle)typeHandle);
                    Assert.Equal("System.Collections.Generic", reader.GetString(type.Namespace));
                    Assert.Equal("List`1", reader.GetString(type.Name));
                    Assert.Equal(HandleKind.AssemblyReference, type.ResolutionScope.Kind);
                    var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
                    Assert.Equal("System.Collections", reader.GetString(scope.Name));
                    Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
                    Assert.Equal("", reader.GetString(scope.Culture));
                    Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
                    Assert.Equal(new byte[] { 1, 8 }, signature.ReadBytes(2));
                }
                else
                {
                    Assert.Equal(HandleKind.TypeDefinition, typeHandle.Kind);
                    var type = reader.GetTypeDefinition((TypeDefinitionHandle)typeHandle);
                    Assert.Equal(ImportNamespace(name), reader.GetString(type.Namespace));
                    Assert.Equal("ImportToken", reader.GetString(type.Name));
                }
                return typeHandle;
            }
        }
        Assert.Equal(3, ImportCases.Select(item => ImportIdentity(item.Name)).Distinct().Count());
        Assert.Equal(3, ImportCases.Select(item => ImportDeclaration(item.Name)).Distinct().Count());
        var repeat = ScanBound(source, [assembly]);
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.CompiledInputProvenance), JsonSerializer.Serialize(repeat.Manifest.CompiledInputProvenance));
    }

    [Theory]
    [InlineData("unbound", "SourceMetadataReconciliationCompiledEvidenceUnacceptable", "1")]
    [InlineData("duplicate", "SourceMetadataReconciliationMultipleCandidates", "2")]
    [InlineData("malformed", "SourceMetadataReconciliationZeroCandidate", "0")]
    [InlineData("limit", "SourceMetadataReconciliationZeroCandidate", "0")]
    public void Import_matrix_unbound_ambiguous_malformed_and_bounded_inputs_refuse_joins(string mode, string kind, string count)
    {
        var source = ImportSource();
        var assembly = FixtureAssemblyPath(source, "CompiledEvidence.VisualBasic.dll");
        using var temp = new TempDirectory();
        ScanResult scan;
        if (mode == "duplicate")
        {
            var copy = Path.Combine(temp.Path, "copy.dll");
            File.Copy(assembly, copy);
            scan = ScanBound(source, [assembly, copy]);
            Assert.Equal(2, scan.Facts.Count(fact => fact.Properties.GetValueOrDefault("gapKind") == "AmbiguousDuplicateManagedAssembly"));
            foreach (var name in ImportCases.Select(item => item.Name))
            {
                var members = scan.Facts.Where(fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == ImportIdentity(name)).ToArray();
                Assert.Equal(2, members.Length);
                Assert.Equal(2, members.Select(fact => fact.Evidence.FilePath).Distinct().Count());
                foreach (var member in members) CheckImportCompiled(scan, member, copy);
            }
        }
        else if (mode == "unbound") scan = ScanUnbound(source, [assembly]);
        else
        {
            var input = assembly;
            if (mode == "malformed")
            {
                input = Path.Combine(temp.Path, "malformed.dll");
                File.WriteAllBytes(input, File.ReadAllBytes(assembly)[..64]); // Read as data only.
            }
            scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [input],
                CompiledInputLimits: mode == "limit" ? new CompiledInputLimits(MaxMemberCount: 1) : null));
            var inputGap = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == (mode == "limit" ? "ManagedInputMemberCountLimitExceeded" : "MalformedManagedInput"));
            Assert.Equal(RuleIds.DotNetCompiledGap, inputGap.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, inputGap.EvidenceTier);
            CheckImportCompiled(scan, inputGap, input, gap: true);
            Assert.Equal("compiled-metadata-partial", scan.Manifest.CompiledInputProvenance!.CoverageState);
        }
        foreach (var (name, line) in ImportCases)
        {
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity(name));
            var gap = Assert.Single(scan.Facts, fact => fact.TargetSymbol == ImportIdentity(name) && fact.Properties.GetValueOrDefault("gapKind") == kind);
            CheckImportSource(scan, gap, name, line, EvidenceTiers.Tier4Unknown);
            Assert.Equal(count, gap.Properties["candidateCount"]);
            Assert.False(string.IsNullOrWhiteSpace(gap.Properties["limitation"]));
            var observation = Assert.Single(scan.Facts, fact => fact.FactId == gap.Properties["sourceFactId"]);
            CheckImportSource(scan, observation, name, line, EvidenceTiers.Tier1Semantic);
        }
    }

    [Fact]
    public void Import_matrix_source_oracle_rejects_alias_target_and_span_substitution()
    {
        var source = ImportSource();
        var scan = ScanBound(source, [FixtureAssemblyPath(source, "CompiledEvidence.VisualBasic.dll")]);
        var edge = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity("ViaProject"));
        CheckImportSource(scan, edge, "ViaProject", 15, EvidenceTiers.Tier1Semantic);
        var wrong = edge with { SourceSymbol = "source:Visual Basic|" + ImportIdentity("ViaAlias"), TargetSymbol = ImportIdentity("ViaAlias") };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => CheckImportSource(scan, wrong, "ViaProject", 15, EvidenceTiers.Tier1Semantic));
        wrong = edge with { Properties = new SortedDictionary<string, string>(edge.Properties.ToDictionary(pair => pair.Key, pair => pair.Value), StringComparer.Ordinal)
            { ["sourceDeclarationIdentity"] = ImportDeclaration("ViaAlias") } };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => CheckImportSource(scan, wrong, "ViaProject", 15, EvidenceTiers.Tier1Semantic));
        wrong = edge with { Evidence = edge.Evidence with { StartLine = 18, EndLine = 20 } };
        Assert.ThrowsAny<Xunit.Sdk.XunitException>(() => CheckImportSource(scan, wrong, "ViaProject", 15, EvidenceTiers.Tier1Semantic));
    }

    private static void CheckImportSource(ScanResult scan, CodeFact fact, string name, int line, string tier)
    {
        Assert.Equal(RuleIds.DotNetCompiledSourceIdentity, fact.RuleId);
        Assert.Equal(tier, fact.EvidenceTier);
        Assert.Equal(ImportIdentity(name), fact.TargetSymbol);
        Assert.Equal("source:Visual Basic|" + ImportIdentity(name), fact.SourceSymbol);
        Assert.Equal(ImportDeclaration(name), fact.Properties["sourceDeclarationIdentity"]);
        Assert.Equal("ImportsMatrix.vb", fact.Evidence.FilePath);
        Assert.Equal(line, fact.Evidence.StartLine);
        Assert.Equal(line + 2, fact.Evidence.EndLine);
        Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        Assert.Equal(nameof(SourceMetadataReconciler), fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.SourceMetadataReconciliationExtractor, fact.Evidence.ExtractorVersion);
    }
    private static void CheckImportCompiled(ScanResult scan, CodeFact fact, string input, bool gap = false)
    {
        Assert.Equal(gap ? RuleIds.DotNetCompiledGap : RuleIds.DotNetCompiledMember, fact.RuleId);
        Assert.Equal(gap ? EvidenceTiers.Tier4Unknown : EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        Assert.Equal(1, fact.Evidence.StartLine);
        Assert.Equal(1, fact.Evidence.EndLine);
        Assert.Equal(nameof(ManagedMetadataExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.ManagedMetadataExtractor, fact.Evidence.ExtractorVersion);
        var provenance = scan.Manifest.CompiledInputProvenance!;
        var outcome = Assert.Single(provenance.Outcomes, item => item.SafeLocator == fact.Evidence.FilePath);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(input))).ToLowerInvariant(), fact.Properties["rawFileSha256"]);
        Assert.Equal(outcome.ProvenanceBindingInputSha256, fact.Properties["provenanceBindingInputSha256"]);
        Assert.Equal(provenance.BoundedInputSha256, fact.Properties["boundedInputSha256"]);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(typeof(ManagedMetadataExtractor).Assembly.Location))).ToLowerInvariant(), fact.Properties["generatorSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
        if (!gap)
        {
            Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, fact.Properties["evidenceLocationKind"]);
            Assert.Equal(ImportAssembly, fact.Properties["assemblyIdentity"]);
            Assert.Matches("^0x06[0-9a-f]{6}$", fact.Properties["metadataToken"]);
        }
    }
}
