using System.Diagnostics;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using TraceMap.Core;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;

namespace TraceMap.Tests;

public sealed partial class SourceMetadataReconciliationTests
{
    [Theory]
    [InlineData("ambiguous", "ViaProject", 15, "BC30561", "ImportToken")]
    [InlineData("missing", "ViaProject", 15, "BC30002", "ImportToken")]
    [InlineData("malformed-alias", "ViaAlias", 18, "BC30203", "")]
    public void Import_conflict_matrix_preserves_failed_source_and_separate_metadata(
        string mode, string name, int line, string diagnosticId, string errorType)
    {
        using var temp = new TempDirectory();
        var source = CreateImportConflictSource(temp.Path, mode);
        var assembly = FixtureAssemblyPath(ImportSource(), "CompiledEvidence.VisualBasic.dll");
        // Deliberately attest the valid public binary against invalid source: a receipt
        // cannot repair an unresolved compiler identity or prove build authenticity.
        var scan = ScanBound(source, [assembly]);
        Assert.Equal("FailedOrPartial", scan.Manifest.BuildStatus);
        Assert.EndsWith("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
        var diagnostics = scan.Facts.Where(fact => fact.Properties.GetValueOrDefault("diagnosticId") == diagnosticId).ToArray();
        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, fact =>
        {
            Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, fact.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
            Assert.Equal("ImportsMatrix.vb", fact.Evidence.FilePath);
            Assert.Equal(mode == "malformed-alias" ? 1 : line, fact.Evidence.StartLine);
            Assert.Equal(fact.Evidence.StartLine, fact.Evidence.EndLine);
            Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
            Assert.Equal(scan.Manifest.RepoName, fact.Repo);
            Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
            Assert.Equal(nameof(VisualBasicSemanticExtractor), fact.Evidence.ExtractorId);
            Assert.Equal(ScannerVersions.VisualBasicSemanticExtractor, fact.Evidence.ExtractorVersion);
            Assert.Null(fact.TargetSymbol);
            Assert.Equal("CompilationDiagnostic", fact.Properties["gapKind"]);
        });
        CheckImportCompilerError(source, mode, name, diagnosticId, errorType);
        var gap = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataIdentityIncomplete"
            && fact.Evidence.FilePath == "ImportsMatrix.vb" && fact.Evidence.StartLine == line);
        var owner = "visualbasic type CompiledEvidence.VisualBasic%401.0.0.0 TraceMap.CompiledFixtures.Equivalence.ImportsMatrix";
        var type = Uri.EscapeDataString("unknown:" + errorType);
        var expected = "visualbasic method " + Uri.EscapeDataString(owner) + " " + name + "(" + type + ")->" + type;
        Assert.Equal(expected, gap.SourceSymbol);
        Assert.Null(gap.TargetSymbol);
        Assert.Equal(expected, gap.Properties["sourceDeclarationIdentity"]);
        Assert.Equal("SourceErrorTypeIdentityUnavailable", gap.Properties["details"]);
        Assert.Equal("0", gap.Properties["candidateCount"]);
        Assert.Equal("", gap.Properties["expectedMetadataIdentity"]);
        Assert.Equal("unjoined", gap.Properties["reconciliationState"]);
        CheckImportConflictEvidence(scan, gap, line, line + 2, RuleIds.DotNetCompiledSourceIdentity,
            nameof(SourceMetadataReconciler), ScannerVersions.SourceMetadataReconciliationExtractor);
        var observation = Assert.Single(scan.Facts, fact => fact.FactId == gap.Properties["sourceFactId"]);
        Assert.Equal(FactTypes.SourceMetadataIdentityObserved, observation.FactType);
        Assert.Equal(expected, observation.SourceSymbol);
        Assert.Equal(RuleIds.DotNetCompiledSourceIdentity, observation.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, observation.EvidenceTier);
        Assert.Equal(gap.Evidence, observation.Evidence);
        Assert.Equal(gap.CommitSha, observation.CommitSha);
        Assert.Equal(gap.Repo, observation.Repo);
        Assert.Equal(gap.ProjectPath, observation.ProjectPath);
        Assert.Equal(expected, observation.Properties["sourceDeclarationIdentity"]);
        Assert.Null(observation.TargetSymbol);
        Assert.Equal("", observation.Properties["metadataIdentity"]);
        Assert.Equal(ScannerVersions.VisualBasicSemanticExtractor, observation.Properties["sourceExtractorVersion"]);
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
            && fact.Evidence.FilePath == "ImportsMatrix.vb" && fact.Evidence.StartLine == line);
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity(name));
        CheckConflictBinary(scan, assembly, name);
        // Partial source coverage does not erase independently resolved declarations.
        var control = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled
            && fact.TargetSymbol == ImportIdentity("ViaDefault"));
        CheckImportSource(scan, control, "ViaDefault", 21, EvidenceTiers.Tier1Semantic);
        Assert.Equal("bound", control.Properties["compiledProvenanceState"]);
        var repeat = ScanBound(source, [assembly]);
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.CompiledInputProvenance), JsonSerializer.Serialize(repeat.Manifest.CompiledInputProvenance));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Import_conflict_matrix_qualified_control_resolves_but_member_limit_refuses_join(bool bounded)
    {
        using var temp = new TempDirectory();
        var source = CreateImportConflictSource(temp.Path, "qualified");
        var assembly = FixtureAssemblyPath(ImportSource(), "CompiledEvidence.VisualBasic.dll");
        var receipt = Path.Combine(temp.Path, "binding.json");
        WriteBoundReceipt(source, [assembly], receipt);
        var scan = ScanEngine.Scan(new ScanOptions(source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [assembly],
            CompiledBindingReceiptPaths: [receipt], CompiledInputLimits: bounded ? new CompiledInputLimits(MaxMemberCount: 1) : null));
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        if (bounded)
        {
            Assert.Equal("compiled-metadata-partial", scan.Manifest.CompiledInputProvenance!.CoverageState);
            var limit = Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "ManagedInputMemberCountLimitExceeded");
            CheckImportCompiled(scan, limit, assembly, gap: true);
            Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity("ViaProject"));
            var gap = Assert.Single(scan.Facts, fact => fact.TargetSymbol == ImportIdentity("ViaProject")
                && fact.Properties.GetValueOrDefault("gapKind") == "SourceMetadataReconciliationZeroCandidate");
            CheckImportSource(scan, gap, "ViaProject", 15, EvidenceTiers.Tier4Unknown);
        }
        else
        {
            var edge = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled && fact.TargetSymbol == ImportIdentity("ViaProject"));
            CheckImportSource(scan, edge, "ViaProject", 15, EvidenceTiers.Tier1Semantic);
            Assert.Equal("bound", edge.Properties["compiledProvenanceState"]);
            var member = CheckConflictBinary(scan, assembly, "ViaProject");
            Assert.Equal(member.FactId, edge.Properties["compiledFactId"]);
            Assert.Equal(member.Properties["generatorSha256"], edge.Properties["compiledGeneratorSha256"]);
            foreach (var key in new[] { "boundedInputSha256", "provenanceBindingInputSha256" }) Assert.Equal(member.Properties[key], edge.Properties[key]);
        }
    }

    private static void CheckImportCompilerError(string source, string mode, string name, string diagnosticId, string errorType)
    {
        // Independent compiler invocation over the public declaration file only;
        // no scanner identity provider, MSBuild workspace or fixture execution.
        var tree = VisualBasicSyntaxTree.ParseText(File.ReadAllText(Path.Combine(source, "ImportsMatrix.vb")));
        var imports = new List<string> { "System.Collections.Generic" };
        if (mode != "missing") imports.Add("TraceMap.CompiledFixtures.ProjectImported");
        if (mode == "ambiguous") imports.Add("TraceMap.CompiledFixtures.FileImported");
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path));
        var compilation = VisualBasicCompilation.Create("ImportConflictOracle", [tree], references,
            new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, globalImports: imports.Select(GlobalImport.Parse)));
        using var bytes = new MemoryStream();
        var emit = compilation.Emit(bytes);
        Assert.False(emit.Success);
        Assert.Contains(emit.Diagnostics, diagnostic => diagnostic.Id == diagnosticId && diagnostic.Severity == DiagnosticSeverity.Error);
        var method = Assert.Single(compilation.GetTypeByMetadataName("TraceMap.CompiledFixtures.Equivalence.ImportsMatrix")!.GetMembers(name).OfType<IMethodSymbol>());
        var error = Assert.IsAssignableFrom<IErrorTypeSymbol>(method.ReturnType);
        Assert.Equal(errorType, error.Name);
        Assert.True(SymbolEqualityComparer.Default.Equals(error, Assert.Single(method.Parameters).Type));
        if (mode == "ambiguous")
            Assert.Equal(new[] { "TraceMap.CompiledFixtures.FileImported.ImportToken", "TraceMap.CompiledFixtures.ProjectImported.ImportToken" },
                error.CandidateSymbols.Select(symbol => symbol.ToDisplayString()).OrderBy(value => value, StringComparer.Ordinal));
        else Assert.Empty(error.CandidateSymbols);
    }

    private static CodeFact CheckConflictBinary(ScanResult scan, string assembly, string name)
    {
        using var pe = new PEReader(File.OpenRead(assembly));
        var reader = pe.GetMetadataReader();
        var owner = Assert.Single(reader.TypeDefinitions, h => reader.GetString(reader.GetTypeDefinition(h).Namespace) == "TraceMap.CompiledFixtures.Equivalence"
            && reader.GetString(reader.GetTypeDefinition(h).Name) == "ImportsMatrix");
        var method = Assert.Single(reader.GetTypeDefinition(owner).GetMethods(), h => reader.GetString(reader.GetMethodDefinition(h).Name) == name);
        var blob = reader.GetBlobReader(reader.GetMethodDefinition(method).Signature);
        Assert.Equal(new byte[] { 0, 1, 0x12 }, blob.ReadBytes(3));
        var type = blob.ReadTypeHandle();
        Assert.Equal(HandleKind.TypeDefinition, type.Kind);
        Assert.Equal(0x12, blob.ReadByte());
        Assert.Equal(type, blob.ReadTypeHandle());
        Assert.Equal(0, blob.RemainingBytes);
        Assert.Equal(ImportNamespace(name), reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)type).Namespace));
        Assert.Equal("ImportToken", reader.GetString(reader.GetTypeDefinition((TypeDefinitionHandle)type).Name));
        var member = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared && f.TargetSymbol == ImportIdentity(name));
        Assert.Equal($"0x{MetadataTokens.GetToken(method):x8}", member.Properties["metadataToken"]);
        Assert.Equal(ImportSignature(name), member.Properties["signature"]);
        CheckImportCompiled(scan, member, assembly);
        return member;
    }

    private static void CheckImportConflictEvidence(ScanResult scan, CodeFact fact, int start, int end, string rule, string extractor, string version)
    {
        Assert.Equal(rule, fact.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, fact.EvidenceTier);
        Assert.Equal("ImportsMatrix.vb", fact.Evidence.FilePath);
        Assert.Equal(start, fact.Evidence.StartLine);
        Assert.Equal(end, fact.Evidence.EndLine);
        Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Matches("^[0-9a-f]{40}$", fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        Assert.Equal(extractor, fact.Evidence.ExtractorId);
        Assert.Equal(version, fact.Evidence.ExtractorVersion);
        Assert.False(string.IsNullOrWhiteSpace(fact.Properties["limitation"]));
    }

    private static string CreateImportConflictSource(string temp, string mode)
    {
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);
        foreach (var file in Directory.GetFiles(ImportSource(), "*.vb")) File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        var project = File.ReadAllText(Path.Combine(ImportSource(), "CompiledEvidence.VisualBasic.vbproj"));
        if (mode is "ambiguous" or "qualified") project = project.Replace("</ItemGroup>", "<Import Include=\"TraceMap.CompiledFixtures.FileImported\" /></ItemGroup>", StringComparison.Ordinal);
        if (mode == "missing") project = project.Replace("<Import Include=\"TraceMap.CompiledFixtures.ProjectImported\" />", "", StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(source, "CompiledEvidence.VisualBasic.vbproj"), project);
        var path = Path.Combine(source, "ImportsMatrix.vb");
        var text = File.ReadAllText(path);
        if (mode == "qualified") text = text.Replace("As ImportToken", "As Global.TraceMap.CompiledFixtures.ProjectImported.ImportToken", StringComparison.Ordinal);
        if (mode == "malformed-alias") text = text.Replace("Imports FileToken = TraceMap.CompiledFixtures.FileImported.ImportToken", "Imports FileToken =", StringComparison.Ordinal);
        File.WriteAllText(path, text);
        foreach (var args in new[] { new[] { "init" }, new[] { "add", "-A" }, new[] { "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "public import conflict fixture" } })
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = source, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync();
            var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Public fixture git setup timed out."); }
            Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        }
        return source;
    }
}
