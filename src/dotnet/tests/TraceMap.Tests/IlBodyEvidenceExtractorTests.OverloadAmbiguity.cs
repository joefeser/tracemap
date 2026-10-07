using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Text;
using System.Security.Cryptography;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using TraceMap.Core;

namespace TraceMap.Tests;

public sealed partial class IlBodyEvidenceExtractorTests
{
    private const string OverloadInt = "type(namespace:6:System|names:5:Int32)";
    private static string OverloadType(bool uri) => uri
        ? "scope(assembly:name:14:System.Runtime|version:8:10.0.0.0|culture:7:neutral|publicKeyToken:16:b03f5f7f11d50a3a)type(namespace:6:System|names:3:Uri)"
        : "type(namespace:6:System|names:6:String)";
    private static string OverloadMethod(string name, string parameter = "") => EventMethod(ReceiverAssembly(LateAssembly), "OverloadMatrix", name, LateStatic + "(" + parameter + ")->" + OverloadInt);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Overload_matrix_explicit_cast_selects_exact_source_and_il_target(bool strict)
    {
        using var temp = new TempDirectory();
        var fixture = Fixture("vb", LateAssembly);
        var source = OverloadSource(temp.Path, fixture.Source, strict, ambiguous: false);
        CheckOverloadCompiler(source, strict, ambiguous: false);
        var options = new ScanOptions(source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        Assert.Equal("Succeeded", scan.Manifest.BuildStatus);
        CheckOverloadBinary(scan, fixture.Assembly);
        foreach (var (name, uri, line) in new[] { ("FromString", false, 10), ("FromUri", true, 13) })
        {
            var edge = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.CallEdge && fact.Evidence.FilePath == "OverloadMatrix.vb" && fact.Evidence.StartLine == line);
            Assert.Equal(RuleIds.VisualBasicSemanticCallGraph, edge.RuleId);
            Assert.Equal(EvidenceTiers.Tier1Semantic, edge.EvidenceTier);
            Assert.Equal("Global." + ReceiverNamespace + ".OverloadMatrix." + name + "()", edge.SourceSymbol);
            Assert.Equal("Global." + ReceiverNamespace + ".OverloadMatrix.SelectValue(value As " + (uri ? "Global.System.Uri" : "String") + ")", edge.TargetSymbol);
            CheckOverloadSource(scan, edge, line, semantic: true);
            Assert.DoesNotContain(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == "CallSiteSemanticResolutionUnavailable"
                && fact.Evidence.FilePath == "OverloadMatrix.vb" && fact.Evidence.StartLine == line);
        }
        var repeat = Scan(options with { OutputPath = Path.Combine(temp.Path, "repeat") });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeat.Manifest.IlBodyProvenance));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Overload_matrix_nothing_ambiguity_is_not_a_semantic_call(bool strict)
    {
        using var temp = new TempDirectory();
        var fixture = Fixture("vb", LateAssembly);
        var source = OverloadSource(temp.Path, fixture.Source, strict, ambiguous: true);
        CheckOverloadCompiler(source, strict, ambiguous: true);
        var options = new ScanOptions(source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [fixture.Assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        Assert.Equal("FailedOrPartial", scan.Manifest.BuildStatus);
        Assert.EndsWith("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
        var site = scan.Facts.Where(fact => fact.Evidence.FilePath == "OverloadMatrix.vb" && fact.Evidence.StartLine == 10).ToArray();
        Assert.DoesNotContain(site, fact => fact.FactType is FactTypes.CallEdge or FactTypes.MethodInvoked && fact.EvidenceTier == EvidenceTiers.Tier1Semantic);
        var edge = Assert.Single(site, fact => fact.FactType == FactTypes.CallEdge);
        Assert.Equal(RuleIds.VisualBasicSyntaxCallGraph, edge.RuleId);
        Assert.Equal(EvidenceTiers.Tier3SyntaxOrTextual, edge.EvidenceTier);
        Assert.Equal("Global." + ReceiverNamespace + ".OverloadMatrix.FromString()", edge.SourceSymbol);
        Assert.Equal("SelectValue", edge.TargetSymbol);
        CheckOverloadSource(scan, edge, 10, semantic: false);
        var gap = Assert.Single(site, fact => fact.Properties.GetValueOrDefault("gapKind") == "CallSiteSemanticResolutionUnavailable");
        Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, gap.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
        Assert.Null(gap.SourceSymbol);
        Assert.Null(gap.TargetSymbol);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("SelectValue"))).ToLowerInvariant()[..32], gap.Properties["siteHash"]);
        CheckOverloadSource(scan, gap, 10, semantic: true);
        var diagnostic = Assert.Single(site, fact => fact.Properties.GetValueOrDefault("diagnosticId") == "BC30521");
        Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, diagnostic.RuleId);
        Assert.Equal(EvidenceTiers.Tier4Unknown, diagnostic.EvidenceTier);
        CheckOverloadSource(scan, diagnostic, 10, semantic: true);
        var unaffected = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.CallEdge
            && fact.Evidence.FilePath == "OverloadMatrix.vb" && fact.Evidence.StartLine == 13);
        Assert.Equal(RuleIds.VisualBasicSemanticCallGraph, unaffected.RuleId);
        Assert.Equal(EvidenceTiers.Tier1Semantic, unaffected.EvidenceTier);
        Assert.Equal("Global." + ReceiverNamespace + ".OverloadMatrix.FromUri()", unaffected.SourceSymbol);
        Assert.Equal("Global." + ReceiverNamespace + ".OverloadMatrix.SelectValue(value As Global.System.Uri)", unaffected.TargetSymbol);
        CheckOverloadSource(scan, unaffected, 13, semantic: true);
        CheckOverloadBinary(scan, fixture.Assembly); // Separate original bytes; never a resolution oracle for the invalid source call.
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.SourceMetadataIdentityReconciled);
        var repeat = Scan(options with { OutputPath = Path.Combine(temp.Path, "repeat") });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Overload_matrix_wrong_table_call_operand_and_work_limit_remain_gaps(bool bounded)
    {
        var fixture = Fixture("vb", LateAssembly);
        using var temp = new TempDirectory();
        var input = fixture.Assembly;
        if (!bounded)
        {
            using var pe = new PEReader(File.OpenRead(input));
            var reader = pe.GetMetadataReader();
            var method = reader.GetMethodDefinition(ReceiverHandle(reader, "OverloadMatrix", "FromString"));
            var call = Assert.Single(DecodeEventBody(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!), item => item.OpCode == OpCodes.Call);
            var bytes = File.ReadAllBytes(input);
            var start = FileOffset(pe, method.RelativeVirtualAddress);
            var header = (bytes[start] & 3) == 2 ? 1 : (BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(start, 2)) >> 12) * 4;
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(start + header + call.Offset + 1, 4), 0x02000001);
            input = Path.Combine(temp.Path, "wrong-table.dll");
            File.WriteAllBytes(input, bytes); // Data only; never load or execute.
        }
        var scan = Scan(new ScanOptions(fixture.Source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [input], IlBodyEvidence: true,
            IlBodyLimits: bounded ? new IlBodyLimits(MaxTotalWorkUnits: 8) : null));
        Assert.Equal("il-partial", scan.Manifest.IlBodyProvenance!.CoverageState);
        CheckEventGap(scan, Assert.Single(scan.Facts, fact => fact.Properties.GetValueOrDefault("gapKind") == (bounded ? "IlTotalWorkLimitExceeded" : "IlCallTargetIdentityUnavailable")), input, il: true);
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
    }

    private static void CheckOverloadBinary(ScanResult scan, string assembly)
    {
        using var pe = new PEReader(File.OpenRead(assembly));
        var reader = pe.GetMetadataReader();
        using var module = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var owner = Assert.Single(module.Types, type => type.FullName == ReceiverNamespace + ".OverloadMatrix");
        var hashes = new List<string>();
        var streams = new List<short[]>();
        foreach (var (name, uri) in new[] { ("FromString", false), ("FromUri", true) })
        {
            var handle = ReceiverHandle(reader, "OverloadMatrix", name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(new byte[] { 0, 0, 8 }, reader.GetBlobBytes(method.Signature));
            var decoded = DecodeEventBody(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!);
            var encoded = Assert.Single(decoded, item => item.OpCode == OpCodes.Call);
            var cecil = Assert.Single(owner.Methods, item => item.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
            Assert.Equal(decoded.Select(item => (item.Offset, item.OpCode.Value)), cecil.Body.Instructions.Select(item => (item.Offset, item.OpCode.Value)));
            var target = Assert.IsType<Mono.Cecil.MethodDefinition>(Assert.Single(cecil.Body.Instructions, item => item.OpCode == Mono.Cecil.Cil.OpCodes.Call).Operand);
            Assert.Equal(encoded.Token, target.MetadataToken.ToInt32());
            Assert.Equal("SelectValue", target.Name);
            Assert.True(target.IsStatic && target.IsPublic);
            Assert.Equal("System.Int32", target.ReturnType.FullName);
            Assert.Equal(uri ? "System.Uri" : "System.String", Assert.Single(target.Parameters).ParameterType.FullName);
            var targetHandle = MetadataTokens.EntityHandle(encoded.Token);
            Assert.Equal(HandleKind.MethodDefinition, targetHandle.Kind);
            var targetDef = reader.GetMethodDefinition((MethodDefinitionHandle)targetHandle);
            Assert.Equal(method.GetDeclaringType(), targetDef.GetDeclaringType());
            Assert.Equal("SelectValue", reader.GetString(targetDef.Name));
            var signature = reader.GetBlobReader(targetDef.Signature);
            Assert.Equal(new byte[] { 0, 1, 8 }, signature.ReadBytes(3));
            Assert.Equal(uri ? 0x12 : 0x0e, signature.ReadByte());
            if (uri)
            {
                var type = reader.GetTypeReference((TypeReferenceHandle)signature.ReadTypeHandle());
                Assert.Equal("System", reader.GetString(type.Namespace));
                Assert.Equal("Uri", reader.GetString(type.Name));
                var scope = reader.GetAssemblyReference((AssemblyReferenceHandle)type.ResolutionScope);
                Assert.Equal("System.Runtime", reader.GetString(scope.Name));
                Assert.Equal(new Version(10, 0, 0, 0), scope.Version);
                Assert.Equal("b03f5f7f11d50a3a", Convert.ToHexString(reader.GetBlobBytes(scope.PublicKeyOrToken)).ToLowerInvariant());
            }
            Assert.Equal(0, signature.RemainingBytes);
            var member = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == OverloadMethod(name));
            Assert.Equal($"0x{MetadataTokens.GetToken(handle):x8}", member.Properties["metadataToken"]);
            var selected = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedMethodDeclared && fact.TargetSymbol == OverloadMethod("SelectValue", OverloadType(uri)));
            Assert.Equal($"0x{encoded.Token:x8}", selected.Properties["metadataToken"]);
            var body = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlBodyDeclared && fact.Properties["compiledFactId"] == member.FactId);
            Assert.Equal(member.TargetSymbol + "|il-body:instructions:" + decoded.Length.ToString(CultureInfo.InvariantCulture) + ":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
            var call = Assert.Single(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved && fact.Properties["ilBodyFactId"] == body.FactId);
            Assert.Equal(selected.TargetSymbol, call.Properties["targetIdentity"]);
            Assert.Equal($"0x{encoded.Token:x8}", call.Properties["referenceToken"]);
            Assert.Equal("methoddef", call.Properties["referenceKind"]);
            Assert.Equal("call", call.Properties["opcode"]);
            Assert.Equal(member.Properties["metadataToken"], call.Properties["metadataToken"]);
            Assert.Null(call.SourceSymbol);
            Assert.Equal(encoded.Offset.ToString(CultureInfo.InvariantCulture), call.Properties["ilOffset"]);
            Assert.Equal(body.TargetSymbol + "|call:call:" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + selected.TargetSymbol, call.TargetSymbol);
            foreach (var f in new[] { member, selected }) AssertReceiverEvidence(scan, f, assembly, RuleIds.DotNetCompiledMember);
            AssertReceiverEvidence(scan, body, assembly, RuleIds.DotNetIlBody);
            AssertReceiverEvidence(scan, call, assembly, RuleIds.DotNetIlCall);
            hashes.Add(body.Properties["ilBodySha256"]);
            streams.Add(decoded.Select(item => item.OpCode.Value).ToArray());
        }
        Assert.Equal(streams[0], streams[1]);
        Assert.NotEqual(hashes[0], hashes[1]);
    }

    private static void CheckOverloadSource(ScanResult scan, CodeFact fact, int line, bool semantic)
    {
        Assert.Equal("OverloadMatrix.vb", fact.Evidence.FilePath);
        Assert.Equal(line, fact.Evidence.StartLine);
        Assert.Equal(line, fact.Evidence.EndLine);
        Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Matches("^[0-9a-f]{40}$", fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        Assert.Equal(semantic ? nameof(VisualBasicSemanticExtractor) : nameof(VisualBasicSyntaxExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(semantic ? ScannerVersions.VisualBasicSemanticExtractor : ScannerVersions.VisualBasicSyntaxExtractor, fact.Evidence.ExtractorVersion);
    }

    private static void CheckOverloadCompiler(string source, bool strict, bool ambiguous)
    {
        var tree = VisualBasicSyntaxTree.ParseText(File.ReadAllText(Path.Combine(source, "OverloadMatrix.vb")));
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = VisualBasicCompilation.Create("OverloadOracle", [tree], references,
            new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionStrict: strict ? OptionStrict.On : OptionStrict.Off));
        using var bytes = new MemoryStream();
        var emit = compilation.Emit(bytes);
        Assert.Equal(!ambiguous, emit.Success);
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var symbol = compilation.GetSemanticModel(tree).GetSymbolInfo(invocation);
        if (ambiguous)
        {
            Assert.Contains(emit.Diagnostics, d => d.Id == "BC30521" && d.Severity == DiagnosticSeverity.Error);
            Assert.Null(symbol.Symbol);
            Assert.Equal(CandidateReason.OverloadResolutionFailure, symbol.CandidateReason);
            Assert.Equal(new[] { "String", "System.Uri" }, symbol.CandidateSymbols.Cast<IMethodSymbol>().Select(m => Assert.Single(m.Parameters).Type.ToDisplayString()).OrderBy(s => s, StringComparer.Ordinal));
        }
        else
        {
            Assert.Empty(symbol.CandidateSymbols);
            Assert.Equal(SpecialType.System_String, Assert.Single(Assert.IsAssignableFrom<IMethodSymbol>(symbol.Symbol).Parameters).Type.SpecialType);
        }
    }

    private static string OverloadSource(string temp, string fixture, bool strict, bool ambiguous)
    {
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);
        foreach (var file in Directory.GetFiles(fixture, "*.vb")) File.Copy(file, Path.Combine(source, Path.GetFileName(file)));
        File.WriteAllText(Path.Combine(source, "CompiledEvidence.VisualBasic.vbproj"), File.ReadAllText(Path.Combine(fixture, "CompiledEvidence.VisualBasic.vbproj"))
            .Replace("<OptionStrict>On</OptionStrict>", "<OptionStrict>" + (strict ? "On" : "Off") + "</OptionStrict>", StringComparison.Ordinal));
        var path = Path.Combine(source, "OverloadMatrix.vb");
        if (ambiguous) File.WriteAllText(path, File.ReadAllText(path).Replace("SelectValue(DirectCast(Nothing, String))", "SelectValue(Nothing)", StringComparison.Ordinal));
        foreach (var args in new[] { new[] { "init" }, new[] { "add", "-A" }, new[] { "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "public overload fixture" } })
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git") { WorkingDirectory = source, UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
            foreach (var arg in args) process.StartInfo.ArgumentList.Add(arg);
            process.Start();
            var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(30_000)) { process.Kill(entireProcessTree: true); throw new TimeoutException("Public fixture git setup timed out."); }
            Assert.True(process.ExitCode == 0, stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult());
        }
        return source;
    }
}
