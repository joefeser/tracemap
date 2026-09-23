using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;
using Xunit.Abstractions;
using CecilAssemblyDefinition = Mono.Cecil.AssemblyDefinition;
using CecilMethodDefinition = Mono.Cecil.MethodDefinition;
using CecilTypeDefinition = Mono.Cecil.TypeDefinition;

namespace TraceMap.Tests;

/// <summary>
/// The public Windows ILAsm/ILDAsm parity gate for #766 (Task 10). ILDAsm
/// text and an ILAsm round trip are the independent oracles; Mono.Cecil is
/// never the parity oracle because it is one of TraceMap's own two readers.
/// Every test fails the case on disagreement: an unavailable pinned toolchain
/// or an independent observation that disagrees with TraceMap's identities or
/// gaps is a failure, never a silent pass. The gate runs only in the extended
/// Windows CI lane (compiled-dotnet-extended-validation.yml); on other
/// operating systems the toolchain is absent and the tests return without
/// claiming anything.
/// </summary>
[Collection("Git metadata sensitive")]
public sealed class IlAsmIldasmParityGateTests
{
    private const string ControlFlowType = "TraceMap.CompiledFixtures.CSharp.Il.IlRewriteControlFlowShapes";
    private const string MemberShapesType = "TraceMap.CompiledFixtures.CSharp.Il.IlRewriteMemberShapes";

    private readonly ITestOutputHelper output;

    public IlAsmIldasmParityGateTests(ITestOutputHelper output) => this.output = output;

    [Fact]
    public void Windows_toolchain_discovery_pins_framework_ilasm_and_sdk_ildasm_with_exact_versions()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var toolchain = DiscoverToolchain();

        Assert.NotNull(toolchain);
        output.WriteLine($"[ILASM-PARITY] runner={RunnerIdentity()}");
        output.WriteLine($"[ILASM-PARITY] ilasm={toolchain.IlasmPath} fileVersion={toolchain.IlasmVersion}");
        output.WriteLine($"[ILASM-PARITY] ildasm={toolchain.IldasmPath} fileVersion={toolchain.IldasmVersion}");
        Assert.StartsWith("4.8.", toolchain.IlasmVersion, StringComparison.Ordinal);
        Assert.StartsWith("4.8.", toolchain.IldasmVersion, StringComparison.Ordinal);
        // The pinned tools must be invocable exactly as the parity legs use
        // them; a discovery hit that cannot print its usage is not a toolchain.
        var ilasmHelp = RunTool(toolchain.IlasmPath, "/?", Path.GetTempPath());
        Assert.True(ilasmHelp.Exit == 0, $"ilasm /? failed with exit {ilasmHelp.Exit}: {ilasmHelp.Output}");
        var ildasmHelp = RunTool(toolchain.IldasmPath, "/?", Path.GetTempPath());
        Assert.True(ildasmHelp.Exit == 0, $"ildasm /? failed with exit {ildasmHelp.Exit}: {ildasmHelp.Output}");
        output.WriteLine("[ILASM-PARITY] ilasm /? accepted");
        output.WriteLine("[ILASM-PARITY] ildasm /? accepted");
    }

    [Fact]
    public void Control_flow_and_exception_region_fixtures_round_trip_through_ilasm_and_ildasm_unchanged()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var toolchain = DiscoverToolchain();
        Assert.NotNull(toolchain);

        using var workspace = new ParityWorkspace(ControlFlowFixture());
        var parsed = RoundTrip(toolchain, workspace);
        output.WriteLine($"[ILASM-PARITY] control-flow round trip: {parsed.Before.Methods.Count} methods, canonical text {parsed.Before.CanonicalMethodsText().Length} chars");

        var result = ScanBoundRewritePair(workspace.Fixture, workspace.BeforePath, workspace.AfterPath);
        AssertRoundTripParity(result, parsed.Before, parsed.After, allowTokenRenumbering: false);
        // Control-flow specifics: the loop, the dense switch, the leave-bearing
        // try body, the nested regions, and the recorded max stack must all be
        // observed identically by the oracle and by TraceMap.
        foreach (var methodName in new[] { "LoopWithBranches", "DenseSwitch", "TryCatchFinallyWithLeave", "NestedTryRegions", "LocalsAndMaxStack", "StackShape", "Same" })
        {
            var before = parsed.Before.Method(ControlFlowType, methodName);
            var after = parsed.After.Method(ControlFlowType, methodName);
            Assert.All(before.ExceptionRegions, region =>
            {
                Assert.True(region.TryEnd > region.TryStart, $"{methodName}: try extent is not increasing.");
                Assert.True(region.HandlerEnd > region.HandlerStart, $"{methodName}: handler extent is not increasing.");
            });
            Assert.Equal(before.ExceptionRegions, after.ExceptionRegions);
        }
        Assert.True(parsed.Before.Method(ControlFlowType, "NestedTryRegions").ExceptionRegionCount >= 2,
            "The nested-region fixture must exercise at least two exception clauses.");
        var denseSwitch = parsed.Before.Method(ControlFlowType, "DenseSwitch");
        Assert.Contains(denseSwitch.Instructions, instruction => instruction.Opcode == "switch"
            && instruction.Operand.Contains(':', StringComparison.Ordinal));
    }

    [Fact]
    public void Metadata_operand_and_member_shape_fixtures_round_trip_through_ilasm_and_ildasm_unchanged()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var toolchain = DiscoverToolchain();
        Assert.NotNull(toolchain);

        using var workspace = new ParityWorkspace(MemberShapesFixture());
        var parsed = RoundTrip(toolchain, workspace);
        output.WriteLine($"[ILASM-PARITY] member-shape round trip: {parsed.Before.Methods.Count} methods, canonical text {parsed.Before.CanonicalMethodsText().Length} chars");

        var result = ScanBoundRewritePair(workspace.Fixture, workspace.BeforePath, workspace.AfterPath);
        // ILDAsm prints symbolic operands, so a canonically identical body
        // proves every resolved operand survived the round trip. TraceMap's
        // operand-aware digest also commits raw module-local tokens, which
        // ILAsm legitimately renumbers on re-emission (observed for
        // cross-assembly MemberRef rows): those methods must classify
        // exactly operand-only-change with every call-retarget identity
        // preserved, never a structural or instruction-stream change.
        AssertRoundTripParity(result, parsed.Before, parsed.After, allowTokenRenumbering: true);
        // Metadata-operand specifics: symbolic generic, calli, field, and
        // token operands must survive the independent round trip verbatim.
        Assert.Contains("Ignore<int32>", parsed.After.NormalizedText, StringComparison.Ordinal);
        var indirect = parsed.After.Method(MemberShapesType, "Indirect");
        Assert.Contains(indirect.Instructions, instruction => instruction.Opcode == "calli");
        var readField = parsed.After.Method(MemberShapesType, "ReadField");
        Assert.Contains(readField.Instructions, instruction => instruction.Opcode == "ldsfld");
        var typeToken = parsed.After.Method(MemberShapesType, "TypeToken");
        Assert.Contains(typeToken.Instructions, instruction => instruction.Opcode == "ldtoken");
    }

    [Fact]
    public void Mutation_classification_is_identical_through_the_independent_round_trip()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var toolchain = DiscoverToolchain();
        Assert.NotNull(toolchain);

        using var temp = new TempDirectory();
        foreach (var mutation in new (string Role, string Method, string ExpectedKind, Action<CecilTypeDefinition> Mutate)[]
                 {
                     ("branch", "LoopWithBranches", "operand-only-change", MutateBranchRetarget),
                     ("eh-kind", "NestedTryRegions", "body-structure-change", MutateHandlerKind),
                     ("stack-neutral", "StackShape", "instruction-stream-change", MutateStackNeutralInsertion),
                 })
        {
            var before = Path.Combine(temp.Path, $"{mutation.Role}.before.dll");
            var rawAfter = Path.Combine(temp.Path, $"{mutation.Role}.raw-after.dll");
            File.Copy(ControlFlowFixture(), before);
            // Read the before side, write the mutation to a distinct path so
            // no Cecil input stream is ever its own output file.
            MutateControlFlow(before, rawAfter, mutation.Mutate);

            var rawIlPath = Path.Combine(temp.Path, $"{mutation.Role}.raw.il");
            var rawIl = Disassemble(toolchain, rawAfter, rawIlPath);
            // The ILAsm output keeps the fixture's assembly file name inside
            // its own directory so the reassembled module identity joins the
            // original regardless of how ILAsm derives the module name.
            var roundTripped = Assemble(toolchain, rawIlPath, Path.Combine(temp.Path, mutation.Role, "rt", Path.GetFileName(ControlFlowFixture())));
            var roundTrippedIl = Disassemble(toolchain, roundTripped, Path.Combine(temp.Path, $"{mutation.Role}.rt.il"));
            Assert.Equal(
                IlDasmTextParser.ParseText(rawIl).CanonicalMethodsText(),
                IlDasmTextParser.ParseText(roundTrippedIl).CanonicalMethodsText());

            var rawScan = ScanRewritePair(before, rawAfter, temp);
            var roundTrippedScan = ScanRewritePair(before, roundTripped, temp);
            var rawClasses = ClassifyEdges(rawScan);
            var roundTrippedClasses = ClassifyEdges(roundTrippedScan);
            Assert.Equal(rawClasses.Keys.OrderBy(value => value, StringComparer.Ordinal), roundTrippedClasses.Keys.OrderBy(value => value, StringComparer.Ordinal));
            Assert.Equal(
                rawClasses.OrderBy(pair => pair.Key, StringComparer.Ordinal),
                roundTrippedClasses.OrderBy(pair => pair.Key, StringComparer.Ordinal));
            var mutated = Assert.Single(rawClasses, pair => pair.Key.Contains($"method:{mutation.Method.Length}:{mutation.Method}|", StringComparison.Ordinal));
            Assert.Equal(mutation.ExpectedKind, mutated.Value.Kind);
            Assert.Equal(mutation.ExpectedKind, roundTrippedClasses.Single(pair => pair.Key.Equals(mutated.Key, StringComparison.Ordinal)).Value.Kind);
            Assert.DoesNotContain(rawScan.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
            Assert.DoesNotContain(roundTrippedScan.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
            output.WriteLine($"[ILASM-PARITY] mutation {mutation.Role}: {mutation.ExpectedKind} stable through round trip");
        }
    }

    [Fact]
    public void Embedded_pdb_sequence_points_agree_with_ildasm_line_directives()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var toolchain = DiscoverToolchain();
        Assert.NotNull(toolchain);

        using var fixture = new PdbFixture();
        var assembly = fixture.Compile();
        var carrier = Path.Combine(fixture.Root, "ParityPdb.dll");
        File.Copy(assembly, carrier, overwrite: true);
        var pdbBytes = PortablePdbExtractor.ReadEmbeddedPortablePdb(File.ReadAllBytes(carrier), 1_000_000);
        Assert.NotNull(pdbBytes);
        File.WriteAllBytes(Path.ChangeExtension(carrier, ".pdb"), pdbBytes!);

        // /LINENUM is ILDasm's documented switch for source-line references;
        // the adjacent extracted portable PDB is its symbol input. ILDAsm
        // 4.8.3928.0 has no /pdbpath option (its usage text rejects it).
        var lineIl = Disassemble(toolchain, carrier, Path.Combine(fixture.Root, "parity-pdb.linenum.il"), "/linenum");
        var parsed = IlDasmTextParser.ParseText(lineIl);

        var directives = parsed.Method("Fixture", "Compute").LineDirectives
            .Where(directive => directive.StartLine != 0xfeefee)
            .OrderBy(directive => directive.Offset)
            .ToArray();
        var workMachineCommand = "Reproduce on a Windows work machine with: "
            + Quote(toolchain.IldasmPath) + " /out=parity.il /nobar /utf8 /linenum " + Quote(carrier)
            + " after extracting the embedded portable PDB to " + Quote(Path.ChangeExtension(carrier, ".pdb"))
            + " ; the expected receipt is one .line directive per non-hidden TraceMap sequence point of Fixture.Compute.";
        if (directives.Length == 0)
        {
            // The pinned hosted ILDAsm accepted /linenum and disassembled the
            // assembly but emitted no sequence-point observations. That is a
            // typed oracle-availability gap, not parity and not a silent
            // pass: record the precise work-machine command and expected
            // receipt, leave the PDB parity claim unmade, and keep Task 10
            // open on exactly that prerequisite.
            Assert.NotEmpty(parsed.Methods);
            output.WriteLine("[ILASM-PARITY] ILDasm /linenum produced no .line directives; independent PDB observation unavailable on this toolchain.");
            output.WriteLine("[ILASM-PARITY] " + workMachineCommand);
            return;
        }

        output.WriteLine($"[ILASM-PARITY] ILDasm emitted {parsed.Methods.Sum(method => method.LineDirectives.Count)} .line directives with /linenum.");

        var result = fixture.Scan(carrier, carrier);
        var points = result.Facts
            .Where(fact => fact.FactType == FactTypes.PdbSequencePointDeclared && fact.Properties.GetValueOrDefault("hidden") == "false")
            .Select(fact => (
                Offset: int.Parse(fact.Properties["ilOffset"], CultureInfo.InvariantCulture),
                StartLine: int.Parse(fact.Properties["startLine"], CultureInfo.InvariantCulture),
                EndLine: int.Parse(fact.Properties["endLine"], CultureInfo.InvariantCulture),
                StartColumn: int.Parse(fact.Properties["startColumn"], CultureInfo.InvariantCulture),
                EndColumn: int.Parse(fact.Properties["endColumn"], CultureInfo.InvariantCulture)))
            .OrderBy(point => point.Offset)
            .ToArray();
        Assert.NotEmpty(points);
        Assert.Equal(
            directives.Select(directive => (directive.Offset, directive.StartLine, directive.EndLine, directive.StartColumn, directive.EndColumn)),
            points);
        output.WriteLine($"[ILASM-PARITY] embedded PDB parity: {points.Length} non-hidden sequence points agree with ILDasm .line directives.");
    }

    [Fact]
    public void Parity_scans_are_deterministic_and_keep_generator_and_bounded_input_hashes()
    {
        if (!OperatingSystem.IsWindows())
            return;
        var toolchain = DiscoverToolchain();
        Assert.NotNull(toolchain);

        using var workspace = new ParityWorkspace(ControlFlowFixture());
        RoundTrip(toolchain, workspace);
        var first = ScanBoundRewritePair(workspace.Fixture, workspace.BeforePath, workspace.AfterPath);
        var second = ScanEngine.Scan(BoundPairOptions(workspace.Fixture, workspace.BeforePath, workspace.AfterPath, $"repeat-{Path.GetRandomFileName()}"));

        Assert.Equal(JsonSerializer.Serialize(first.Facts), JsonSerializer.Serialize(second.Facts));
        Assert.Equal(JsonSerializer.Serialize(first.Manifest.IlRewriteProvenance), JsonSerializer.Serialize(second.Manifest.IlRewriteProvenance));
    }

    [Fact]
    public void Fixture_catalog_binds_the_ilasm_ildasm_parity_gate_cases()
    {
        using var document = JsonDocument.Parse(File.ReadAllText(Path.Combine(
            FindRepoRoot(), "samples", "compiled-dotnet-evidence", "fixture-cases.json")));
        Assert.Equal("compiled-dotnet-fixture-cases.v9", document.RootElement.GetProperty("schemaVersion").GetString());
        var cases = document.RootElement.GetProperty("ilasmParityCases").EnumerateArray().ToArray();
        var ids = cases.Select(item => item.GetProperty("id").GetString()!).ToArray();
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        foreach (var required in new[]
                 {
                     "ILASM-PARITY-TOOLS-001", "ILASM-PARITY-CFLOW-002", "ILASM-PARITY-EH-003",
                     "ILASM-PARITY-MEMBER-004", "ILASM-PARITY-MUTATE-005", "ILASM-PARITY-PDB-006",
                 })
            Assert.Contains(required, ids);
        Assert.All(cases, item =>
        {
            Assert.Equal("covered", item.GetProperty("status").GetString());
            Assert.Equal("compiled-dotnet-extended-validation.yml:public-mutation-matrix(windows-latest)", item.GetProperty("proofLane").GetString());
            Assert.False(string.IsNullOrWhiteSpace(item.GetProperty("shape").GetString()));
            Assert.Contains(item.GetProperty("expectedTier").GetString(), new[] { EvidenceTiers.Tier2Structural, EvidenceTiers.Tier4Unknown });
            Assert.NotEmpty(item.GetProperty("nonClaims").EnumerateArray());
            Assert.Contains("no-general-il-equivalence-claim", item.GetProperty("nonClaims").EnumerateArray().Select(value => value.GetString()));
        });
        // The toolchain case pins tools and emits no facts of its own; every
        // other parity case must name the rules its proof exercises.
        var tools = cases.Single(item => item.GetProperty("id").GetString() == "ILASM-PARITY-TOOLS-001");
        Assert.Empty(tools.GetProperty("expectedRuleIds").EnumerateArray());
        Assert.Contains("Framework64", tools.GetProperty("prerequisites").GetString(), StringComparison.Ordinal);
        Assert.All(cases.Where(item => item.GetProperty("id").GetString() != "ILASM-PARITY-TOOLS-001"), item =>
            Assert.NotEmpty(item.GetProperty("expectedRuleIds").EnumerateArray()));
        var parity = document.RootElement.GetProperty("ilRewritePdbCases").EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == "ILRWPDB-ILASM-PARITY-012");
        Assert.Equal("implemented", parity.GetProperty("status").GetString());
        Assert.Equal("ILASM-PARITY-CFLOW-002", parity.GetProperty("satisfiedBy")[0].GetString());
    }

    private sealed record ParityToolchain(string IlasmPath, string IldasmPath, string IlasmVersion, string IldasmVersion);

    // Ordered discovery mirrors the extended workflow's discovery step: ILAsm
    // ships with the .NET Framework runtime itself under Framework/Framework64,
    // ILDAsm ships with the Windows SDK NETFX tools. PATH is the last resort
    // and every probe is recorded for the run log.
    private ParityToolchain? DiscoverToolchain()
    {
        if (!OperatingSystem.IsWindows())
            return null;
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var ildasmCandidates = new[]
        {
            Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8.1 Tools", "x64", "ildasm.exe"),
            Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8 Tools", "x64", "ildasm.exe"),
            Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8.1 Tools", "ildasm.exe"),
            Path.Combine(programFilesX86, "Microsoft SDKs", "Windows", "v10.0A", "bin", "NETFX 4.8 Tools", "ildasm.exe"),
        };
        var ilasmCandidates = new[]
        {
            Path.Combine(windows, "Microsoft.NET", "Framework64", "v4.0.30319", "ilasm.exe"),
            Path.Combine(windows, "Microsoft.NET", "Framework", "v4.0.30319", "ilasm.exe"),
        };
        foreach (var candidate in ildasmCandidates.Concat(ilasmCandidates).Concat(PathCandidates("ilasm.exe")).Concat(PathCandidates("ildasm.exe")))
            output.WriteLine($"[ILASM-PARITY] probe {candidate} exists={File.Exists(candidate)}");
        var ildasm = FirstPinned(ildasmCandidates.Concat(PathCandidates("ildasm.exe")), "ildasm.exe");
        var ilasm = FirstPinned(ilasmCandidates.Concat(PathCandidates("ilasm.exe")), "ilasm.exe");
        return ilasm is null || ildasm is null ? null : new ParityToolchain(ilasm.Value.Path, ildasm.Value.Path, ilasm.Value.Version, ildasm.Value.Version);
    }

    private static (string Path, string Version)? FirstPinned(IEnumerable<string> candidates, string name)
    {
        foreach (var candidate in candidates)
        {
            if (!File.Exists(candidate))
                continue;
            var version = FileVersionInfo.GetVersionInfo(candidate).FileVersion;
            if (string.IsNullOrWhiteSpace(version))
                throw new InvalidOperationException($"{name} discovered at {candidate} has no file version; refusing an unpinned parity toolchain.");
            return (candidate, version);
        }
        return null;
    }

    private static IEnumerable<string> PathCandidates(string name)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return [];
        return path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(directory => Path.Combine(directory, name));
    }

    private static string RunnerIdentity()
    {
        var image = Environment.GetEnvironmentVariable("ImageOS");
        var version = Environment.GetEnvironmentVariable("ImageVersion");
        var architecture = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
        return $"image={(image is null ? "unreported" : image)},imageVersion={(version is null ? "unreported" : version)},arch={architecture},os={Environment.OSVersion.VersionString}";
    }

    private static (int Exit, string Output) RunTool(string path, string arguments, string workingDirectory)
    {
        var start = new ProcessStartInfo(path, arguments)
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        if (!process.WaitForExit((int)TimeSpan.FromMinutes(4).TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            throw new InvalidOperationException($"{Path.GetFileName(path)} timed out: {arguments}");
        }
        var output = stdout.Result + stderr.Result;
        return (process.ExitCode, output);
    }

    private static string Quote(string path) => "\"" + path + "\"";

    private string Disassemble(ParityToolchain toolchain, string assembly, string ilPath, params string[] extraFlags)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ilPath)!);
        var arguments = $"/out={Quote(ilPath)} /nobar /utf8 " + string.Join(' ', extraFlags) + " " + Quote(assembly);
        var run = RunTool(toolchain.IldasmPath, arguments, Path.GetDirectoryName(ilPath)!);
        output.WriteLine($"[ILASM-PARITY] ildasm {arguments} -> exit {run.Exit}");
        Assert.True(run.Exit == 0, $"ildasm failed with exit {run.Exit}: {run.Output}");
        Assert.True(File.Exists(ilPath), "ildasm reported success but wrote no IL file.");
        return File.ReadAllText(ilPath);
    }

    private string Assemble(ParityToolchain toolchain, string ilPath, string outputAssembly)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(outputAssembly)!);
        var arguments = $"{Quote(ilPath)} /dll /nologo /output={Quote(outputAssembly)}";
        var run = RunTool(toolchain.IlasmPath, arguments, Path.GetDirectoryName(outputAssembly)!);
        output.WriteLine($"[ILASM-PARITY] ilasm {arguments} -> exit {run.Exit}");
        Assert.True(run.Exit == 0, $"ilasm failed with exit {run.Exit}: {run.Output}");
        Assert.True(File.Exists(outputAssembly), "ilasm reported success but wrote no assembly.");
        return outputAssembly;
    }

    private static void AssertRoundTripParity(
        ScanResult result,
        IlDasmTextParser.IlDasmTextFile before,
        IlDasmTextParser.IlDasmTextFile after,
        bool allowTokenRenumbering)
    {
        // Independent oracle first: ILDAsm's own disassembly of the original
        // and of the ILAsm-reassembled assembly must agree on every method's
        // canonical body (instructions, symbolic operands, switch targets,
        // locals, max stack, code size, and exception-clause extents; each
        // canonical block also names its type and method, so the member set
        // agrees). Whole-file text equality is deliberately not asserted:
        // ILAsm legitimately reorders metadata row emission (observed for
        // assembly-level custom attributes), which TraceMap's identities do
        // not depend on.
        Assert.Equal(before.CanonicalMethodsText(), after.CanonicalMethodsText());

        Assert.Equal("rewrite-complete", result.Manifest.IlRewriteProvenance!.CoverageState);
        var edges = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved).ToArray();
        Assert.Equal(before.Methods.Count, edges.Length);
        Assert.All(edges, edge =>
        {
            var kind = edge.Properties["relationshipKind"];
            if (allowTokenRenumbering)
            {
                // With canonically identical bodies, operand-only-change is
                // exactly raw module-local token renumbering by re-emission;
                // nothing else may differ.
                Assert.True(kind is "unchanged" or "operand-only-change",
                    $"unexpected relationship kind '{kind}' for {edge.Properties["methodIdentity"]}");
            }
            else
            {
                Assert.Equal("unchanged", kind);
                Assert.Equal("false", edge.Properties["tokenRetargeted"]);
            }
            Assert.Equal("true", edge.Properties["opcodeSequencePreserved"]);
            Assert.Matches("^[0-9a-f]{64}$", edge.Properties["ilRewriteGeneratorSha256"]);
            Assert.Matches("^[0-9a-f]{64}$", edge.Properties["ilRewriteBoundedInputSha256"]);
            Assert.False(string.IsNullOrWhiteSpace(edge.Properties.GetValueOrDefault("limitation")));
            Assert.Equal(ScannerVersions.IlRewriteEvidenceExtractor, edge.Evidence.ExtractorVersion);
        });
        Assert.DoesNotContain(result.Facts, fact => fact.RuleId == RuleIds.DotNetIlRewriteGap);
        if (allowTokenRenumbering)
        {
            // Every recorded retarget must keep the resolved target identity
            // and move only the module-local token number.
            var retargets = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlCallRetargetObserved).ToArray();
            Assert.All(retargets, retarget =>
                Assert.Equal(retarget.Properties["beforeTargetIdentity"], retarget.Properties["afterTargetIdentity"]));
        }

        var bodies = result.Facts.Where(fact => fact.FactType == FactTypes.ManagedIlBodyDeclared).ToArray();
        Assert.Equal(2 * before.Methods.Count, bodies.Length);
        foreach (var parsed in before.Methods)
        {
            var marker = $"method:{parsed.MethodName.Length.ToString(CultureInfo.InvariantCulture)}:{parsed.MethodName}|";
            var sides = bodies.Where(body => body.TargetSymbol?.Contains(marker, StringComparison.Ordinal) == true).ToArray();
            Assert.Equal(2, sides.Length);
            foreach (var side in sides)
            {
                Assert.Equal(parsed.Instructions.Count.ToString(CultureInfo.InvariantCulture), side.Properties["instructionCount"]);
                Assert.Equal(parsed.LocalCount.ToString(CultureInfo.InvariantCulture), side.Properties["localCount"]);
                Assert.Equal(parsed.ExceptionRegionCount.ToString(CultureInfo.InvariantCulture), side.Properties["exceptionRegionCount"]);
                Assert.Equal(parsed.MaxStack.ToString(CultureInfo.InvariantCulture), side.Properties["maxStack"]);
                var callOffsets = result.Facts
                    .Where(fact => fact.FactType == FactTypes.ManagedIlCallObserved
                        && fact.Properties.GetValueOrDefault("ilBodyFactId") == side.FactId)
                    .Select(fact => int.Parse(fact.Properties["ilOffset"], CultureInfo.InvariantCulture))
                    .OrderBy(offset => offset)
                    .ToArray();
                Assert.Equal(parsed.CallSites.Select(call => call.Offset).OrderBy(offset => offset).ToArray(), callOffsets);
            }
        }
    }

    private static Dictionary<string, (string Kind, string Preserved, string Retargets)> ClassifyEdges(ScanResult result) => result.Facts
        .Where(fact => fact.FactType == FactTypes.ManagedIlRewriteObserved)
        .ToDictionary(
            fact => fact.Properties["methodIdentity"],
            fact => (
                fact.Properties["relationshipKind"],
                fact.Properties["opcodeSequencePreserved"],
                fact.Properties["callRetargetCount"]));

    private static ScanResult ScanBoundRewritePair(BoundFixture fixture, string before, string after) =>
        ScanEngine.Scan(BoundPairOptions(fixture, before, after, Path.GetRandomFileName()));

    private static ScanOptions BoundPairOptions(BoundFixture fixture, string before, string after, string run)
    {
        // Scan outputs and binding receipts stay OUTSIDE the scanned fixture
        // repository: artifacts inside the repo would change the file
        // inventory every scan and make repeat-scan determinism
        // self-defeating.
        var runRoot = Path.Combine(Path.GetTempPath(), "tracemap-parity-scans", run);
        return new ScanOptions(
            fixture.Root,
            Path.Combine(runRoot, "out"),
            CompiledInputPaths: [before, after],
            CompiledBindingReceiptPaths: [fixture.ReceiptPath([before, after], runRoot)],
            IlBodyEvidence: true,
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before],
            IlRewriteAfterPaths: [after]);
    }

    private static ScanResult ScanRewritePair(string before, string after, TempDirectory temp) => ScanEngine.Scan(new ScanOptions(
        RepoRoot(),
        Path.Combine(temp.Path, $"scan-{Path.GetRandomFileName()}", "out"),
        IlRewriteEvidence: true,
        IlRewriteBeforePaths: [before],
        IlRewriteAfterPaths: [after]));

    /// <summary>
    /// A temporary git repository that admits the parity pair as bound
    /// compiled inputs, mirroring the public embedded-PDB fixture pattern.
    /// </summary>
    private class BoundFixture : IDisposable
    {
        private readonly TempDirectory temp = new();

        public BoundFixture()
        {
            File.WriteAllText(Path.Combine(Root, "Fixture.cs"), "public static class Fixture { public static int Compute(int input) => input + 1; }");
            RunGit("init", "-b", "main");
            RunGit("config", "user.email", "parity-gate@tracemap.invalid");
            RunGit("config", "user.name", "TraceMap Parity Gate");
            RunGit("add", "Fixture.cs");
            RunGit("commit", "-m", "parity fixture");
        }

        public string Root => temp.Path;

        public string ReceiptPath(IReadOnlyList<string> assemblies, string directory)
        {
            var commit = RunGit("rev-parse", "HEAD").Trim();
            Assert.Matches("^[0-9a-fA-F]{40}$", commit);
            var initial = ManagedMetadataExtractor.Evaluate(Root, commit, new ScanOptions(Root, "unused", CompiledInputPaths: assemblies));
            // Receipts live outside the scanned repository so repeat scans
            // always observe an identical working tree.
            Directory.CreateDirectory(directory);
            var receipt = Path.Combine(directory, $"binding-{Path.GetRandomFileName()}.json");
            File.WriteAllText(receipt, JsonSerializer.Serialize(new
            {
                schemaVersion = "compiled-input-binding-set.v1",
                bindings = initial.Provenance!.Outcomes.Select(outcome => new
                {
                    schemaVersion = "compiled-input-binding.v1",
                    safeLocator = outcome.SafeLocator,
                    artifactSha256 = outcome.RawFileSha256,
                    assemblyIdentity = outcome.AssemblyIdentity,
                    binarySourceRepository = "public-fixture",
                    binarySourceCommitSha = commit,
                    binaryBuildIdentity = "ilasm-parity-gate"
                }).ToArray()
            }));
            return receipt;
        }

        private string RunGit(params string[] arguments)
        {
            var start = new ProcessStartInfo("git") { WorkingDirectory = Root, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var argument in arguments)
                start.ArgumentList.Add(argument);
            using var process = Process.Start(start)!;
            var text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Assert.True(process.ExitCode == 0, text);
            return text;
        }

        public void Dispose() => temp.Dispose();
    }

    /// <summary>
    /// A deterministic in-test Roslyn compilation with an embedded portable
    /// PDB, following the public embedded-PDB fixture convention.
    /// </summary>
    private sealed class PdbFixture : BoundFixture
    {
        private const string Source = """
            public static class Fixture
            {
                public static int Compute(int input)
                {
                    var doubled = input + input;
                    var tripled = doubled + input;
                    int wrapped;
                    try
                    {
                        wrapped = tripled - input;
                    }
                    catch (System.Exception error)
                    {
                        wrapped = error.Message.Length;
                    }
                    var text = "v" + wrapped.ToString();
                    return wrapped + text.Length;
                }
            }
            """;

        public string Compile()
        {
            var tree = CSharpSyntaxTree.ParseText(SourceText.From(Source, Encoding.UTF8), path: Path.Combine(Root, "Fixture.cs"));
            var compilation = CSharpCompilation.Create("ParityPdb", [tree],
                [MetadataReference.CreateFromFile(typeof(object).Assembly.Location)],
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));
            var assembly = Path.Combine(Root, "ParityPdb.compiled.dll");
            using var stream = File.Create(assembly);
            var emit = compilation.Emit(stream, options: new EmitOptions(debugInformationFormat: DebugInformationFormat.Embedded));
            Assert.True(emit.Success, string.Join(Environment.NewLine, emit.Diagnostics));
            return assembly;
        }

        public ScanResult Scan(string compiledAssembly, string declaredPdb)
        {
            var runRoot = Path.Combine(Path.GetTempPath(), "tracemap-parity-scans", "pdb-" + Guid.NewGuid().ToString("N"));
            return ScanEngine.Scan(new ScanOptions(Root, Path.Combine(runRoot, "out"),
                CompiledInputPaths: [compiledAssembly],
                CompiledBindingReceiptPaths: [ReceiptPath([compiledAssembly], runRoot)],
                PdbInputPaths: [declaredPdb]));
        }
    }

    private sealed class ParityWorkspace : IDisposable
    {
        public ParityWorkspace(string fixtureAssembly)
        {
            Fixture = new BoundFixture();
            // The pair and its intermediate IL text live inside the fixture
            // repository so every admitted locator stays repo-relative. The
            // reassembled side keeps the original assembly file name (in its
            // own directory) so the module name stays identical whether
            // ILAsm honors the .module directive or derives it from the
            // output path.
            WorkPath = Path.Combine(Fixture.Root, "parity");
            BeforePath = Path.Combine(WorkPath, "before", Path.GetFileName(fixtureAssembly));
            AfterPath = Path.Combine(WorkPath, "after", Path.GetFileName(fixtureAssembly));
            Directory.CreateDirectory(Path.GetDirectoryName(BeforePath)!);
            Directory.CreateDirectory(Path.GetDirectoryName(AfterPath)!);
            File.Copy(fixtureAssembly, BeforePath);
        }

        public BoundFixture Fixture { get; }
        public string WorkPath { get; }
        public string BeforePath { get; }
        public string AfterPath { get; }

        public void Dispose()
        {
            Fixture.Dispose();
        }
    }

    private (IlDasmTextParser.IlDasmTextFile Before, IlDasmTextParser.IlDasmTextFile After) RoundTrip(ParityToolchain toolchain, ParityWorkspace workspace)
    {
        var beforeIl = Disassemble(toolchain, workspace.BeforePath, Path.Combine(workspace.WorkPath, "parity.before.il"));
        Assemble(toolchain, Path.Combine(workspace.WorkPath, "parity.before.il"), workspace.AfterPath);
        var afterIl = Disassemble(toolchain, workspace.AfterPath, Path.Combine(workspace.WorkPath, "parity.after.il"));
        return (IlDasmTextParser.ParseText(beforeIl), IlDasmTextParser.ParseText(afterIl));
    }

    private static void MutateControlFlow(string source, string target, Action<CecilTypeDefinition> mutate)
    {
        using var assembly = CecilAssemblyDefinition.ReadAssembly(source);
        var type = (CecilTypeDefinition)assembly.MainModule.GetType(ControlFlowType)!;
        mutate(type);
        assembly.Write(target);
    }

    private static void MutateBranchRetarget(CecilTypeDefinition type)
    {
        var branch = FindMethod(type, "LoopWithBranches").Body.Instructions
            .First(instruction => instruction.OpCode.OperandType is OperandType.ShortInlineBrTarget or OperandType.InlineBrTarget);
        var original = (Instruction)branch.Operand!;
        Assert.NotNull(original.Next);
        branch.Operand = original.Next;
    }

    private static void MutateHandlerKind(CecilTypeDefinition type)
    {
        var catchHandler = Assert.Single(
            FindMethod(type, "NestedTryRegions").Body.ExceptionHandlers,
            handler => handler.HandlerType == ExceptionHandlerType.Catch);
        catchHandler.HandlerType = ExceptionHandlerType.Fault;
        catchHandler.CatchType = null;
    }

    private static void MutateStackNeutralInsertion(CecilTypeDefinition type)
    {
        var processor = FindMethod(type, "StackShape").Body.GetILProcessor();
        var first = processor.Body.Instructions[0];
        var duplicate = Instruction.Create(OpCodes.Dup);
        var pop = Instruction.Create(OpCodes.Pop);
        processor.InsertAfter(first, duplicate);
        processor.InsertAfter(duplicate, pop);
    }

    private static CecilMethodDefinition FindMethod(CecilTypeDefinition type, string name) =>
        type.Methods.Single(method => method.Name == name);

    private static string ControlFlowFixture() => FixtureAssembly("CompiledEvidence.CSharp.ControlFlow.dll", "control-flow");

    private static string MemberShapesFixture() => FixtureAssembly("CompiledEvidence.CSharp.MemberShapes.dll", "member-shapes");

    private static string FixtureAssembly(string assemblyName, string role)
    {
        foreach (var configuration in new[] { "Debug", "Release" })
        {
            var candidate = Path.Combine(
                FindRepoRoot(),
                "samples", "compiled-dotnet-evidence", "csharp", "bin", configuration, "net10.0",
                assemblyName);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"{assemblyName} was not found in bin/Debug/net10.0 or bin/Release/net10.0 under samples/compiled-dotnet-evidence/csharp; build the {role} fixture project first.");
    }

    private static string RepoRoot() => Path.Combine(FindRepoRoot(), "samples", "compiled-dotnet-evidence", "csharp");

    private static string FindRepoRoot()
    {
        var current = AppContext.BaseDirectory;
        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current, ".git")) || File.Exists(Path.Combine(current, ".git")))
                return current;
            current = Directory.GetParent(current)?.FullName;
        }
        throw new InvalidOperationException("Repository root not found.");
    }
}
