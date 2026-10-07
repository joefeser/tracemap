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
    private static string NarrowingMethod(string name) => EventMethod(ReceiverAssembly(LateAssembly), "NarrowingMatrix", name,
        LateStatic + "(" + (name == "FromLong" ? "type(namespace:6:System|names:5:Int64)" : OverloadInt) + ")->" + OverloadInt);

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(false, false)]
    public void Narrowing_matrix_strictness_preserves_compiler_acceptance_and_checked_IL(bool strict, bool explicitCast)
    {
        using var temp = new TempDirectory();
        var fixture = Fixture("vb", LateAssembly);
        var source = NarrowingSource(temp.Path, fixture.Source, strict, explicitCast);
        var text = File.ReadAllText(Path.Combine(source, "NarrowingMatrix.vb"));
        var tree = VisualBasicSyntaxTree.ParseText(text);
        var attributes = VisualBasicSyntaxTree.ParseText("""
            <Assembly: System.Reflection.AssemblyVersion("1.0.0.0")>
            <Assembly: System.Runtime.Versioning.TargetFramework(".NETCoreApp,Version=v10.0")>
            """);
        var references = ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!).Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path));
        var compilation = VisualBasicCompilation.Create(LateAssembly, [tree, attributes], references,
            new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary, optionStrict: strict ? OptionStrict.On : OptionStrict.Off,
                deterministic: true, optimizationLevel: OptimizationLevel.Debug, checkOverflow: true));
        var model = compilation.GetSemanticModel(tree);
        var invocation = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>().First();
        var argument = Assert.IsType<SimpleArgumentSyntax>(Assert.Single(invocation.ArgumentList.Arguments)).Expression;
        var conversion = model.ClassifyConversion(explicitCast ? Assert.IsType<PredefinedCastExpressionSyntax>(argument).Expression : argument,
            compilation.GetSpecialType(SpecialType.System_Int32));
        Assert.True(conversion.Exists && conversion.IsNarrowing && conversion.IsNumeric);
        var rejected = strict && !explicitCast;
        using var bytes = new MemoryStream();
        var emit = compilation.Emit(bytes);
        Assert.Equal(!rejected, emit.Success);
        if (rejected)
        {
            var diagnostic = Assert.Single(emit.Diagnostics, d => d.Id == "BC30512" && d.Severity == DiagnosticSeverity.Error);
            Assert.Equal(6, diagnostic.Location.GetLineSpan().StartLinePosition.Line);
            Assert.Equal(6, diagnostic.Location.GetLineSpan().EndLinePosition.Line);
        }
        else Assert.DoesNotContain(emit.Diagnostics, d => d.Severity == DiagnosticSeverity.Error);
        var symbol = model.GetSymbolInfo(invocation);
        if (rejected)
        {
            Assert.Null(symbol.Symbol);
            Assert.Equal(CandidateReason.OverloadResolutionFailure, symbol.CandidateReason);
            var candidate = Assert.IsAssignableFrom<IMethodSymbol>(Assert.Single(symbol.CandidateSymbols));
            Assert.Equal("AcceptInteger", candidate.Name);
            Assert.Equal(SpecialType.System_Int32, Assert.Single(candidate.Parameters).Type.SpecialType);
        }
        else
        {
            Assert.Empty(symbol.CandidateSymbols);
            var selected = Assert.IsAssignableFrom<IMethodSymbol>(symbol.Symbol);
            Assert.Equal("AcceptInteger", selected.Name);
            Assert.Equal(SpecialType.System_Int32, Assert.Single(selected.Parameters).Type.SpecialType);
        }

        var assembly = fixture.Assembly;
        if (!rejected)
        {
            assembly = Path.Combine(temp.Path, LateAssembly + ".dll");
            File.WriteAllBytes(assembly, bytes.ToArray()); // Inspect emitted bytes only; never execute.
        }
        var options = new ScanOptions(source, Path.Combine(temp.Path, "out"), CompiledInputPaths: [assembly], IlBodyEvidence: true);
        var scan = Scan(options);
        Assert.Equal(rejected ? "FailedOrPartial" : "Succeeded", scan.Manifest.BuildStatus);
        if (rejected)
        {
            Assert.EndsWith("Reduced", scan.Manifest.AnalysisLevel, StringComparison.Ordinal);
            var diagnostic = Assert.Single(scan.Facts, f => f.Evidence.FilePath == "NarrowingMatrix.vb" && f.Properties.GetValueOrDefault("diagnosticId") == "BC30512");
            Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, diagnostic.RuleId);
            Assert.Equal(EvidenceTiers.Tier4Unknown, diagnostic.EvidenceTier);
            CheckNarrowingSource(scan, diagnostic, 7);
        }
        foreach (var (name, line, parameter) in new[] { ("FromLong", 7, "Long"), ("FromInteger", 10, "Integer") })
        {
            var call = Assert.Single(scan.Facts, f => f.FactType == FactTypes.CallEdge && f.Evidence.FilePath == "NarrowingMatrix.vb" && f.Evidence.StartLine == line);
            var unresolved = rejected && name == "FromLong";
            Assert.Equal(unresolved ? RuleIds.VisualBasicSyntaxCallGraph : RuleIds.VisualBasicSemanticCallGraph, call.RuleId);
            Assert.Equal(unresolved ? EvidenceTiers.Tier3SyntaxOrTextual : EvidenceTiers.Tier1Semantic, call.EvidenceTier);
            Assert.Equal("Global." + ReceiverNamespace + ".NarrowingMatrix." + name + "(value As " + parameter + ")", call.SourceSymbol);
            Assert.Equal(unresolved ? "AcceptInteger" : "Global." + ReceiverNamespace + ".NarrowingMatrix.AcceptInteger(value As Integer)", call.TargetSymbol);
            CheckNarrowingSource(scan, call, line, semantic: !unresolved);
            var gaps = scan.Facts.Where(f => f.Evidence.FilePath == "NarrowingMatrix.vb" && f.Evidence.StartLine == line
                && f.Properties.GetValueOrDefault("gapKind") == "CallSiteSemanticResolutionUnavailable").ToArray();
            if (unresolved)
            {
                var gap = Assert.Single(gaps);
                Assert.Equal(RuleIds.VisualBasicSemanticWorkspace, gap.RuleId);
                Assert.Equal(EvidenceTiers.Tier4Unknown, gap.EvidenceTier);
                Assert.Null(gap.SourceSymbol);
                Assert.Null(gap.TargetSymbol);
                Assert.Equal(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("AcceptInteger"))).ToLowerInvariant()[..32], gap.Properties["siteHash"]);
                CheckNarrowingSource(scan, gap, line);
                Assert.DoesNotContain(scan.Facts, f => f.Evidence.FilePath == "NarrowingMatrix.vb" && f.Evidence.StartLine == line
                    && f.FactType is FactTypes.CallEdge or FactTypes.MethodInvoked && f.EvidenceTier == EvidenceTiers.Tier1Semantic);
            }
            else Assert.Empty(gaps);
        }
        CheckNarrowingBinary(scan, assembly);
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.SourceMetadataIdentityReconciled);
        var repeat = Scan(options with { OutputPath = Path.Combine(temp.Path, "repeat") });
        Assert.Equal(JsonSerializer.Serialize(scan.Facts), JsonSerializer.Serialize(repeat.Facts));
        Assert.Equal(JsonSerializer.Serialize(scan.Manifest.IlBodyProvenance), JsonSerializer.Serialize(repeat.Manifest.IlBodyProvenance));
    }

    [Theory]
    [InlineData("duplicate")]
    [InlineData("malformed")]
    [InlineData("bounded")]
    public void Narrowing_matrix_input_ambiguity_malformed_and_limit_remain_explicit(string mode)
    {
        using var temp = new TempDirectory();
        var fixture = Fixture("vb", LateAssembly);
        var input = fixture.Assembly;
        if (mode != "bounded")
        {
            input = Path.Combine(temp.Path, "copy.dll");
            File.WriteAllBytes(input, mode == "malformed" ? File.ReadAllBytes(fixture.Assembly)[..64] : File.ReadAllBytes(fixture.Assembly));
        }
        var scan = Scan(new ScanOptions(fixture.Source, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: mode == "duplicate" ? [fixture.Assembly, input] : [input], IlBodyEvidence: true,
            IlBodyLimits: mode == "bounded" ? new IlBodyLimits(MaxTotalWorkUnits: 8) : null));
        var kind = mode switch { "duplicate" => "AmbiguousDuplicateManagedAssembly", "malformed" => "MalformedManagedInput", _ => "IlTotalWorkLimitExceeded" };
        var gaps = scan.Facts.Where(f => f.Properties.GetValueOrDefault("gapKind") == kind).ToArray();
        Assert.NotEmpty(gaps);
        foreach (var gap in gaps) CheckEventGap(scan, gap, input, il: mode == "bounded");
        Assert.DoesNotContain(scan.Facts, f => f.FactType == FactTypes.SourceMetadataIdentityReconciled);
        if (mode != "duplicate") Assert.DoesNotContain(scan.Facts, f => f.FactType is FactTypes.ManagedIlBodyDeclared or FactTypes.ManagedIlCallObserved);
        else Assert.Equal(2, scan.Facts.Count(f => f.FactType == FactTypes.ManagedMethodDeclared && f.TargetSymbol == NarrowingMethod("FromLong")));
    }

    private static void CheckNarrowingBinary(ScanResult scan, string assembly)
    {
        using var pe = new PEReader(File.OpenRead(assembly));
        var reader = pe.GetMetadataReader();
        using var cecil = Mono.Cecil.ModuleDefinition.ReadModule(assembly);
        var owner = Assert.Single(cecil.Types, t => t.FullName == ReceiverNamespace + ".NarrowingMatrix");
        var targetHandle = ReceiverHandle(reader, "NarrowingMatrix", "AcceptInteger");
        Assert.Equal(new byte[] { 0, 1, 8, 8 }, reader.GetBlobBytes(reader.GetMethodDefinition(targetHandle).Signature));
        var target = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared && f.TargetSymbol == NarrowingMethod("AcceptInteger"));
        Assert.Equal($"0x{MetadataTokens.GetToken(targetHandle):x8}", target.Properties["metadataToken"]);
        AssertReceiverEvidence(scan, target, assembly, RuleIds.DotNetCompiledMember);
        foreach (var name in new[] { "FromLong", "FromInteger" })
        {
            var handle = ReceiverHandle(reader, "NarrowingMatrix", name);
            var method = reader.GetMethodDefinition(handle);
            Assert.Equal(new byte[] { 0, 1, 8, (byte)(name == "FromLong" ? 10 : 8) }, reader.GetBlobBytes(method.Signature));
            var decoded = DecodeEventBody(pe.GetMethodBody(method.RelativeVirtualAddress).GetILBytes()!);
            var independent = Assert.Single(owner.Methods, m => m.MetadataToken.ToInt32() == MetadataTokens.GetToken(handle));
            Assert.Equal(name, independent.Name);
            Assert.True(independent.IsStatic && independent.IsPublic);
            Assert.Equal("System.Int32", independent.ReturnType.FullName);
            Assert.Equal(name == "FromLong" ? "System.Int64" : "System.Int32", Assert.Single(independent.Parameters).ParameterType.FullName);
            Assert.Equal(decoded.Select(i => (i.Offset, i.OpCode.Value)), independent.Body.Instructions.Select(i => (i.Offset, i.OpCode.Value)));
            if (name == "FromLong") Assert.Single(decoded, i => i.OpCode == OpCodes.Conv_Ovf_I4);
            else Assert.DoesNotContain(decoded, i => i.OpCode.Name!.StartsWith("conv", StringComparison.Ordinal));
            var encoded = Assert.Single(decoded, i => i.OpCode == OpCodes.Call);
            Assert.Equal(MetadataTokens.GetToken(targetHandle), encoded.Token);
            Assert.Equal(encoded.Token, Assert.IsType<Mono.Cecil.MethodDefinition>(Assert.Single(independent.Body.Instructions, i => i.OpCode == Mono.Cecil.Cil.OpCodes.Call).Operand).MetadataToken.ToInt32());
            var member = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedMethodDeclared && f.TargetSymbol == NarrowingMethod(name));
            Assert.Equal($"0x{MetadataTokens.GetToken(handle):x8}", member.Properties["metadataToken"]);
            var body = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedIlBodyDeclared && f.Properties["compiledFactId"] == member.FactId);
            Assert.Equal(member.TargetSymbol + "|il-body:instructions:" + decoded.Length.ToString(CultureInfo.InvariantCulture) + ":sha256:" + body.Properties["ilBodySha256"], body.TargetSymbol);
            var call = Assert.Single(scan.Facts, f => f.FactType == FactTypes.ManagedIlCallObserved && f.Properties["ilBodyFactId"] == body.FactId);
            Assert.Equal(target.TargetSymbol, call.Properties["targetIdentity"]);
            Assert.Equal($"0x{encoded.Token:x8}", call.Properties["referenceToken"]);
            Assert.Equal("methoddef", call.Properties["referenceKind"]);
            Assert.Equal("call", call.Properties["opcode"]);
            Assert.Equal(member.Properties["metadataToken"], call.Properties["metadataToken"]);
            Assert.Equal(encoded.Offset.ToString(CultureInfo.InvariantCulture), call.Properties["ilOffset"]);
            Assert.Equal(body.TargetSymbol + "|call:call:" + encoded.Offset.ToString(CultureInfo.InvariantCulture) + ":" + target.TargetSymbol, call.TargetSymbol);
            Assert.Null(call.SourceSymbol);
            AssertReceiverEvidence(scan, member, assembly, RuleIds.DotNetCompiledMember);
            AssertReceiverEvidence(scan, body, assembly, RuleIds.DotNetIlBody);
            AssertReceiverEvidence(scan, call, assembly, RuleIds.DotNetIlCall);
        }
    }

    private static void CheckNarrowingSource(ScanResult scan, CodeFact fact, int line, bool semantic = true)
    {
        Assert.Equal("NarrowingMatrix.vb", fact.Evidence.FilePath);
        Assert.Equal(line, fact.Evidence.StartLine);
        Assert.Equal(line, fact.Evidence.EndLine);
        Assert.Equal("CompiledEvidence.VisualBasic.vbproj", fact.ProjectPath);
        Assert.Equal(scan.Manifest.CommitSha, fact.CommitSha);
        Assert.Matches("^[0-9a-f]{40}$", fact.CommitSha);
        Assert.Equal(scan.Manifest.RepoName, fact.Repo);
        Assert.Equal(semantic ? nameof(VisualBasicSemanticExtractor) : nameof(VisualBasicSyntaxExtractor), fact.Evidence.ExtractorId);
        Assert.Equal(semantic ? ScannerVersions.VisualBasicSemanticExtractor : ScannerVersions.VisualBasicSyntaxExtractor, fact.Evidence.ExtractorVersion);
    }

    private static string NarrowingSource(string temp, string fixture, bool strict, bool explicitCast)
    {
        var source = Path.Combine(temp, "source");
        Directory.CreateDirectory(source);
        var text = File.ReadAllText(Path.Combine(fixture, "NarrowingMatrix.vb"));
        File.WriteAllText(Path.Combine(source, "NarrowingMatrix.vb"), explicitCast ? text : text.Replace("CInt(value)", "value", StringComparison.Ordinal));
        File.WriteAllText(Path.Combine(source, "CompiledEvidence.VisualBasic.vbproj"),
            "<Project Sdk=\"Microsoft.NET.Sdk\"><PropertyGroup><TargetFramework>net10.0</TargetFramework><RootNamespace></RootNamespace><OptionStrict>" + (strict ? "On" : "Off") + "</OptionStrict></PropertyGroup></Project>");
        foreach (var args in new[] { new[] { "init" }, new[] { "add", "-A" }, new[] { "-c", "user.name=TraceMap", "-c", "user.email=tests@example.invalid", "commit", "-m", "public narrowing fixture" } })
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
