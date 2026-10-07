using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class PortablePdbExtractorTests
{
    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp", "StateMachineMatrix.cs")]
    [InlineData("vb", "CompiledEvidence.VisualBasic", "StateMachineMatrix.vb")]
    public void State_machine_matrix_pins_generated_method_and_pdb_occurrences(string language, string assemblyName, string sourceFile)
    {
        var fixture = Fixture(language, assemblyName);
        var scan = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);
        using var stream = File.OpenRead(fixture.Assembly);
        using var pe = new PEReader(stream);
        var metadata = pe.GetMetadataReader();
        using var pdbStream = File.OpenRead(fixture.Pdb);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var pdb = provider.GetMetadataReader();
        var content = new BlobContentId(pdb.DebugMetadataHeader!.Id);
        var contentId = $"{content.Guid:D}:{content.Stamp:x8}";
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(fixture.Assembly, new Mono.Cecil.ReaderParameters
        {
            ReadSymbols = true,
            SymbolReaderProvider = new Mono.Cecil.Cil.PortablePdbReaderProvider()
        });
        var outer = Assert.Single(metadata.TypeDefinitions, handle => metadata.GetString(metadata.GetTypeDefinition(handle).Name) == "StateMachineMatrix");
        Assert.Equal("TraceMap.CompiledFixtures.Equivalence", metadata.GetString(metadata.GetTypeDefinition(outer).Namespace));
        var cecilOuter = Assert.Single(module.Types, type => type.FullName == "TraceMap.CompiledFixtures.Equivalence.StateMachineMatrix");
        var assemblyIdentity = "assembly:name:" + assemblyName.Length + ":" + assemblyName
            + "|version:7:1.0.0.0|culture:7:neutral|publicKeyToken:4:null|module:" + (assemblyName.Length + 4)
            + ":" + assemblyName + ".dll|targetFramework:25:.NETCoreApp,Version=v10.0";
        var observedIdentities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var name in new[] { "AwaitOne", "Enumerate" })
        {
            var kickoff = Assert.Single(metadata.GetTypeDefinition(outer).GetMethods(), handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == name);
            // Select by the PDB's StateMachineMethod table, never by generated-name similarity.
            var debugHandle = Assert.Single(pdb.MethodDebugInformation, handle => pdb.GetMethodDebugInformation(handle).GetStateMachineKickoffMethod() == kickoff);
            var move = MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(debugHandle));
            var method = metadata.GetMethodDefinition(move);
            Assert.Equal("MoveNext", metadata.GetString(method.Name));
            Assert.Equal(name == "AwaitOne" ? "200001" : "200002", Convert.ToHexString(metadata.GetBlobBytes(method.Signature)).ToLowerInvariant());
            var generated = method.GetDeclaringType();
            Assert.Equal(outer, metadata.GetTypeDefinition(generated).GetDeclaringType());
            var generatedName = metadata.GetString(metadata.GetTypeDefinition(generated).Name);
            var cecilType = Assert.Single(cecilOuter.NestedTypes, type => type.MetadataToken.ToInt32() == MetadataTokens.GetToken(generated));
            Assert.Equal(generatedName, cecilType.Name);
            Assert.Contains(cecilType.CustomAttributes, attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
            var cecil = Assert.Single(cecilType.Methods, candidate => candidate.MetadataToken.ToInt32() == MetadataTokens.GetToken(move));
            Assert.Equal("MoveNext", cecil.Name);
            Assert.False(cecil.IsStatic);
            Assert.Empty(cecil.Parameters);
            Assert.Equal(name == "AwaitOne" ? "System.Void" : "System.Boolean", cecil.ReturnType.FullName);
            Assert.Equal(MetadataTokens.GetToken(kickoff), cecil.DebugInformation.StateMachineKickOffMethod.MetadataToken.ToInt32());
            // The compiler attribute's type argument independently identifies the same exact nested type.
            var cecilKickoff = Assert.Single(cecilOuter.Methods, candidate => candidate.MetadataToken.ToInt32() == MetadataTokens.GetToken(kickoff));
            var attributeName = name == "AwaitOne" ? "AsyncStateMachineAttribute" : "IteratorStateMachineAttribute";
            var attribute = Assert.Single(cecilKickoff.CustomAttributes, candidate => candidate.AttributeType.FullName == "System.Runtime.CompilerServices." + attributeName);
            var argument = Assert.IsAssignableFrom<Mono.Cecil.TypeReference>(Assert.Single(attribute.ConstructorArguments).Value);
            Assert.Equal(cecilType.FullName, argument.FullName);
            Assert.Equal(MetadataTokens.GetToken(generated), argument.Resolve().MetadataToken.ToInt32());

            var signature = "arity:0|call:default|hasThis:true|explicitThis:false|()->type(namespace:6:System|names:"
                + (name == "AwaitOne" ? "4:Void)" : "7:Boolean)");
            var identity = assemblyIdentity + "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:18:StateMachineMatrix"
                + generatedName.Length + ":" + generatedName + "|arity:0|method:8:MoveNext|" + signature;
            Assert.True(observedIdentities.Add(identity));
            var compiled = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == identity);
            Assert.Equal($"0x{MetadataTokens.GetToken(move):x8}", compiled.Properties["metadataToken"]);
            Assert.Equal(signature, compiled.Properties["signature"]);
            Assert.Equal(RuleIds.DotNetCompiledMember, compiled.RuleId);
            Assert.Equal(EvidenceTiers.Tier2Structural, compiled.EvidenceTier);
            Assert.Equal(scan.Manifest.CommitSha, compiled.CommitSha);
            Assert.Equal(scan.Manifest.RepoName, compiled.Repo);
            var compiledInput = Assert.Single(scan.Manifest.CompiledInputProvenance!.Outcomes, input => input.SafeLocator == compiled.Evidence.FilePath);
            Assert.Equal(compiledInput.ProvenanceBindingInputSha256, compiled.Properties["provenanceBindingInputSha256"]);
            Assert.False(string.IsNullOrWhiteSpace(compiled.Properties["limitation"]));
            Assert.Equal(nameof(ManagedMetadataExtractor), compiled.Evidence.ExtractorId);
            Assert.Equal(ScannerVersions.ManagedMetadataExtractor, compiled.Evidence.ExtractorVersion);
            Assert.Equal(ManagedMetadataExtractor.MetadataLocationKind, compiled.Properties["evidenceLocationKind"]);
            Assert.Equal(1, compiled.Evidence.StartLine);
            Assert.Equal(1, compiled.Evidence.EndLine);
            Assert.Equal(Hash(fixture.Assembly), compiled.Properties["rawFileSha256"]);
            Assert.Equal(Hash(typeof(ManagedMetadataExtractor).Assembly.Location), compiled.Properties["generatorSha256"]);
            Assert.Equal(scan.Manifest.CompiledInputProvenance!.BoundedInputSha256, compiled.Properties["boundedInputSha256"]);

            var pdbIdentity = $"pdb:format:portable|id:{contentId}|method:{MetadataTokens.GetRowNumber(move)}";
            var declared = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.PdbMethodDeclared && fact.TargetSymbol == pdbIdentity);
            var edge = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled && fact.Properties["compiledFactId"] == compiled.FactId);
            Assert.Equal(compiled.TargetSymbol, edge.SourceSymbol);
            Assert.Equal(pdbIdentity, edge.TargetSymbol);
            Assert.Equal(declared.FactId, edge.Properties["pdbMethodFactId"]);
            Assert.Equal(compiled.TargetSymbol, edge.Properties["metadataIdentity"]);
            Assert.Equal(pdbIdentity, edge.Properties["pdbMethodIdentity"]);
            Assert.Equal(compiled.Properties["metadataToken"], declared.Properties["metadataToken"]);
            CheckStatePdbEvidence(declared, scan, fixture.Pdb, RuleIds.DotNetPdbIdentity);
            CheckStatePdbEvidence(edge, scan, fixture.Pdb, RuleIds.DotNetPdbIdentity);
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
                && fact.Properties.GetValueOrDefault("compiledFactId") == compiled.FactId);
            var points = pdb.GetMethodDebugInformation(debugHandle).GetSequencePoints().ToArray();
            var emitted = scan.Facts.Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared && fact.Properties["pdbMethodFactId"] == declared.FactId)
                .OrderBy(fact => int.Parse(fact.Properties["ordinal"], System.Globalization.CultureInfo.InvariantCulture)).ToArray();
            Assert.NotEmpty(points);
            Assert.Contains(points, point => !point.IsHidden);
            Assert.Contains(points, point => point.IsHidden);
            Assert.Equal(points.Length, emitted.Length);
            Assert.Equal(points.Length, cecil.DebugInformation.SequencePoints.Count);
            for (var index = 0; index < points.Length; index++)
            {
                var raw = points[index];
                var independent = cecil.DebugInformation.SequencePoints[index];
                var point = emitted[index];
                Assert.Equal((raw.Offset, raw.IsHidden, raw.StartLine, raw.StartColumn, raw.EndLine, raw.EndColumn),
                    (independent.Offset, independent.IsHidden, independent.StartLine, independent.StartColumn, independent.EndLine, independent.EndColumn));
                Assert.Equal(pdbIdentity, point.SourceSymbol);
                Assert.Equal(edge.FactId, point.Properties["metadataPdbReconciliationFactId"]);
                Assert.Equal(index.ToString(System.Globalization.CultureInfo.InvariantCulture), point.Properties["ordinal"]);
                Assert.Equal(raw.Offset.ToString(System.Globalization.CultureInfo.InvariantCulture), point.Properties["ilOffset"]);
                Assert.Equal(raw.IsHidden.ToString().ToLowerInvariant(), point.Properties["hidden"]);
                foreach (var (key, value) in new[] { ("startLine", raw.StartLine), ("startColumn", raw.StartColumn), ("endLine", raw.EndLine), ("endColumn", raw.EndColumn) })
                    Assert.Equal(value.ToString(System.Globalization.CultureInfo.InvariantCulture), point.Properties[key]);
                var document = Assert.Single(scan.Facts, fact => fact.FactId == point.Properties["pdbDocumentFactId"]);
                Assert.Equal(document.TargetSymbol, point.TargetSymbol);
                CheckStatePdbEvidence(document, scan, fixture.Pdb, RuleIds.DotNetPdbIdentity);
                var rawDocument = pdb.GetDocument(raw.Document);
                Assert.Equal(Hash(Path.Combine(fixture.Source, sourceFile)), Convert.ToHexString(pdb.GetBlobBytes(rawDocument.Hash)).ToLowerInvariant());
                Assert.Equal(pdb.GetBlobBytes(rawDocument.Hash), independent.Document.Hash);
                var documentIdentity = $"pdb:format:portable|id:{contentId}|document:{MetadataTokens.GetRowNumber(raw.Document)}|hashAlgorithm:{pdb.GetGuid(rawDocument.HashAlgorithm):D}|checksum:{Hash(Path.Combine(fixture.Source, sourceFile))}|language:{pdb.GetGuid(rawDocument.Language):D}";
                Assert.Equal(documentIdentity, document.TargetSymbol);
                Assert.Equal($"{pdbIdentity}|sequence:{index}|offset:{raw.Offset}|document:{MetadataTokens.GetRowNumber(raw.Document)}|hidden:{raw.IsHidden.ToString().ToLowerInvariant()}|range:{raw.StartLine}:{raw.StartColumn}-{raw.EndLine}:{raw.EndColumn}", point.ContractElement);
                Assert.Equal(MetadataTokens.GetRowNumber(raw.Document).ToString(System.Globalization.CultureInfo.InvariantCulture), document.Properties["documentRowId"]);
                CheckStatePdbEvidence(point, scan, fixture.Pdb, RuleIds.DotNetPdbSequencePoint);
                Assert.Equal(raw.IsHidden ? 1 : raw.StartLine, point.Evidence.StartLine);
                Assert.Equal(raw.IsHidden ? 1 : raw.EndLine, point.Evidence.EndLine);
                if (!raw.IsHidden) Assert.Equal(sourceFile, point.Evidence.FilePath);
            }
        }
        // Ordinary MoveNext stays a separate endpoint; its spelling grants no state-machine role.
        var ordinary = Assert.Single(metadata.GetTypeDefinition(outer).GetMethods(), handle => metadata.GetString(metadata.GetMethodDefinition(handle).Name) == "MoveNext");
        Assert.True(pdb.GetMethodDebugInformation(ordinary).GetStateMachineKickoffMethod().IsNil);
        Assert.Equal("200002", Convert.ToHexString(metadata.GetBlobBytes(metadata.GetMethodDefinition(ordinary).Signature)).ToLowerInvariant());
        var ordinaryFact = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Properties["metadataToken"] == $"0x{MetadataTokens.GetToken(ordinary):x8}");
        Assert.Equal(assemblyIdentity + "|type:namespace:37:TraceMap.CompiledFixtures.Equivalence|names:18:StateMachineMatrix|arity:0|method:8:MoveNext|arity:0|call:default|hasThis:true|explicitThis:false|()->type(namespace:6:System|names:7:Boolean)", ordinaryFact.TargetSymbol);
        Assert.DoesNotContain(ordinaryFact.TargetSymbol!, observedIdentities);
        var repeat = ScanBound(fixture.Source, fixture.Assembly, fixture.Pdb);
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.PdbInputProvenance), JsonSerializer.Serialize(repeat.Manifest.PdbInputProvenance));
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    public void State_machine_matrix_missing_or_duplicate_metadata_cannot_be_repaired_by_name(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        using var temp = new TempDirectory();
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(fixture.Source, fixture.Assembly, receipt);
        var options = new ScanOptions(fixture.Source, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: [fixture.Assembly], CompiledBindingReceiptPaths: [receipt], PdbInputPaths: [fixture.Pdb]);
        var scan = Scan(options);
        var compiled = ManagedMetadataExtractor.Evaluate(fixture.Source, scan.Manifest.CommitSha, options);
        var pdb = PortablePdbExtractor.Evaluate(fixture.Source, options, compiled);
        var facts = ManagedMetadataExtractor.MaterializeFacts(scan.Manifest, compiled);
        using var stream = File.OpenRead(fixture.Assembly);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        using var pdbStream = File.OpenRead(fixture.Pdb);
        using var provider = MetadataReaderProvider.FromPortablePdbStream(pdbStream);
        var debug = provider.GetMetadataReader();
        var owner = Assert.Single(reader.TypeDefinitions, handle => reader.GetString(reader.GetTypeDefinition(handle).Name) == "StateMachineMatrix");
        foreach (var name in new[] { "AwaitOne", "Enumerate" })
        {
            var kickoff = Assert.Single(reader.GetTypeDefinition(owner).GetMethods(), handle => reader.GetString(reader.GetMethodDefinition(handle).Name) == name);
            var generated = Assert.Single(debug.MethodDebugInformation, handle => debug.GetMethodDebugInformation(handle).GetStateMachineKickoffMethod() == kickoff);
            var token = $"0x{MetadataTokens.GetToken(MetadataTokens.MethodDefinitionHandle(MetadataTokens.GetRowNumber(generated))):x8}";
            var member = Assert.Single(facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.Properties["metadataToken"] == token);
            var declared = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.PdbMethodDeclared && fact.Properties["metadataToken"] == token);
            Check(facts.Where(fact => fact.FactId != member.FactId).ToArray(), "PdbMetadataMethodZeroCandidate", "0");
            Check(facts.Append(member with { FactId = "fact-state-machine-duplicate" }).ToArray(), "PdbMetadataMethodMultipleCandidates", "2");

            void Check(IReadOnlyList<CodeFact> candidates, string kind, string count)
            {
                var emitted = PortablePdbExtractor.MaterializeFacts(fixture.Source, scan.Manifest, pdb, candidates, []);
                var gap = Assert.Single(emitted, fact => fact.Properties.GetValueOrDefault("gapKind") == kind && fact.Properties.GetValueOrDefault("metadataToken") == token);
                Assert.Equal(count, gap.Properties["candidateCount"]);
                Assert.Equal(declared.TargetSymbol, gap.Properties["pdbMethodIdentity"]);
                CheckStateGap(gap, scan);
                Assert.Equal(Hash(fixture.Pdb), gap.Properties["pdbRawFileSha256"]);
                Assert.DoesNotContain(emitted, fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled && fact.TargetSymbol == declared.TargetSymbol);
                Assert.DoesNotContain(emitted, fact => fact.FactType == FactTypes.PdbSequencePointDeclared && fact.SourceSymbol == declared.TargetSymbol);
                // Other exact rows remain available; refusal is local to the disputed endpoint.
                Assert.Equal(scan.Facts.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled) - 1,
                    emitted.Count(fact => fact.FactType == FactTypes.MetadataPdbMethodReconciled));
            }
        }
    }

    [Theory]
    [InlineData("csharp", "CompiledEvidence.CSharp")]
    [InlineData("vb", "CompiledEvidence.VisualBasic")]
    public void State_machine_matrix_malformed_and_bounded_pdb_inputs_fail_closed(string language, string assemblyName)
    {
        var fixture = Fixture(language, assemblyName);
        using var temp = new TempDirectory();
        var malformed = Path.Combine(temp.Path, "truncated.pdb");
        File.WriteAllBytes(malformed, File.ReadAllBytes(fixture.Pdb)[..64]);
        Check(malformed, null, "MalformedPortablePdb");
        Check(fixture.Pdb, new PdbInputLimits(MaxSequencePointCount: 1), "PdbSequencePointCountExceeded");
        void Check(string pdb, PdbInputLimits? limits, string kind)
        {
            var scan = ScanBound(fixture.Source, fixture.Assembly, pdb, limits);
            var gap = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == kind);
            CheckStateGap(gap, scan);
            Assert.Equal(Hash(pdb), gap.Properties["pdbRawFileSha256"]);
            Assert.Equal("pdb-partial", scan.Manifest.PdbInputProvenance!.CoverageState);
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType is FactTypes.PdbMethodDeclared
                or FactTypes.MetadataPdbMethodReconciled or FactTypes.PdbSequencePointDeclared);
        }
    }

    private static void CheckStateGap(CodeFact gap, ScanResult scan)
    {
        Assert.Equal(RuleIds.DotNetPdbGap, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Equal(scan.Manifest.CommitSha, gap.CommitSha);
        Assert.Equal(nameof(PortablePdbExtractor), gap.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.PortablePdbExtractor, gap.Evidence.ExtractorVersion);
        Assert.Equal(1, gap.Evidence.StartLine);
        Assert.Equal(1, gap.Evidence.EndLine);
        Assert.False(string.IsNullOrWhiteSpace(gap.Evidence.FilePath));
        Assert.Equal(Hash(typeof(PortablePdbExtractor).Assembly.Location), gap.Properties["pdbGeneratorSha256"]);
        Assert.Equal(scan.Manifest.PdbInputProvenance!.BoundedInputSha256, gap.Properties["pdbBoundedInputSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(gap.Properties["limitation"]));
    }

    private static void CheckStatePdbEvidence(CodeFact fact, ScanResult scan, string pdb, string rule)
    {
        Assert.Equal(rule, fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier2Structural, fact.EvidenceTier);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        var outcome = Assert.Single(scan.Manifest.PdbInputProvenance!.Outcomes);
        Assert.Equal(outcome.ProvenanceBindingInputSha256, fact.Properties["provenanceBindingInputSha256"]);
        Assert.Equal(outcome.MatchedAssemblyIdentity, fact.Properties["matchedAssemblyIdentity"]);
        Assert.Equal(outcome.PdbContentId, fact.Properties["pdbContentId"]);
        if (fact.FactType != FactTypes.PdbSequencePointDeclared || fact.Properties["hidden"] == "true")
        {
            Assert.Equal(outcome.SafeLocator, fact.Evidence.FilePath);
            Assert.Equal(1, fact.Evidence.StartLine);
            Assert.Equal(1, fact.Evidence.EndLine);
        }
        Assert.Equal(nameof(PortablePdbExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(ScannerVersions.PortablePdbExtractor, fact.Evidence.ExtractorVersion);
        Assert.Equal(PortablePdbExtractor.PdbLocationKind, fact.Properties["evidenceLocationKind"]);
        Assert.Equal(Hash(pdb), fact.Properties["pdbRawFileSha256"]);
        Assert.Equal(Hash(typeof(PortablePdbExtractor).Assembly.Location), fact.Properties["pdbGeneratorSha256"]);
        Assert.Equal(scan.Manifest.PdbInputProvenance!.BoundedInputSha256, fact.Properties["pdbBoundedInputSha256"]);
        Assert.Matches("^[0-9a-f]{64}$", fact.Properties["provenanceBindingInputSha256"]);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
    }

    private static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
}
