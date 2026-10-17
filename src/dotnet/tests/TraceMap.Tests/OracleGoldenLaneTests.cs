using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;

namespace TraceMap.Tests;

// #856 stage 2c: the corpus's goldens as static expectations.
//   - the machine-generated validator suites (SwitchInstrTest,
//     SingleTargetBranchesTest, ProtectedBlocksTest; ~33.9k era verdicts) parsed
//     into expectation data and replayed through the adapted era construction
//     helpers and the adapted era validator (certifies the stage-2b adaptation
//     against the era's own goldens);
//   - the recorded instrumentation-data golden (CallGraphTestClasses.xml)
//     cross-checked against tracemap's Cecil decode of the committed
//     era-instrumented fixture DLL (414-method id map, both directions).
// LeaveInstrTest is deferred: its bodies are hand-authored ilasm text with no
// construction helper (documented on the issue).
public sealed class OracleGoldenLaneTests
{
    private static string CorpusRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter")))
                return Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter");
        throw new DirectoryNotFoundException("dotnetperf corpus unavailable");
    }

    private static string TestAssembliesPath(string fileName) =>
        Path.Combine(CorpusRoot(), "src", "NetPerf.Tests.Integration", "TestAssemblies", fileName);

    private static NetPerf.Goldens.GoldenSuites Suites => NetPerf.Goldens.GoldenSuites.Parse(CorpusRoot());


    // PINS (stable because the corpus is byte-pinned): per-suite expectation
    // totals, protected-blocks total (including the two hand-authored inline
    // constructions), the era's five intentionally-empty documentation tests,
    // and the deterministic sample size of the default replay.
    private static readonly string[] PINSUITCOUNTS = ["SingleTargetBranchesTest.cs=16536", "SwitchInstrTest.cs=16674"];
    private const int PINBLOCKSCOUNT = 663;
    private static readonly string[] PINIRREGULAR = [];
    private static readonly string[] PINNOOP =
    [
        "ProtectedBlocksTest.cs|LastInstructionInFilterIsEndFilter_Passes",
        "ProtectedBlocksTest.cs|ProtectedBlockAndFilterOverlap_AtStartOfProtectedBlock_Fails",
        "ProtectedBlocksTest.cs|ProtectedBlockAndFilterHandlerOverlap_AtEndOfProtectedBlock_Fails",
        "ProtectedBlocksTest.cs|ProtectedBlockAndFilterHandlerOverlap_ContainedWithinProtectedBlock_Fails",
        "ProtectedBlocksTest.cs|ProtectedBlockAndFilterHandlerAdjacent_ProtectedBlockPreceeds_Passes",
    ];
    private const int PINSAMPLEREPLAYED = 2427;

    // PIN: total expectations per suite (fail/pass split) and the irregular list.
    // The corpus is byte-pinned, so any drift here is a parser regression.
    [Fact]
    public void Golden_suites_parse_to_the_pinned_record_counts()
    {
        var suites = Suites;
        var bySuite = suites.Branches
            .GroupBy(golden => golden.Suite)
            .OrderBy(group => group.Key)
            .Select(group => $"{group.Key}={group.Count()}")
            .ToArray();
        Assert.Equal(PINSUITCOUNTS, bySuite);
        Assert.Equal(PINBLOCKSCOUNT, suites.ProtectedBlocks.Count);
        Assert.Equal(PINIRREGULAR, suites.Irregular.Select(trio => $"{trio.Item1}|{trio.Item2}|{trio.Item3}").ToArray());
        Assert.Equal(PINNOOP, suites.EraNoOpTests);
    }

    [Fact]
    public void Generated_goldens_replay_against_the_adapted_era_validator()
    {
        var suites = Suites;
        var replayed = 0;

        // Every protected-blocks golden (668 era cases) replays in the default suite.
        foreach (var golden in suites.ProtectedBlocks)
        {
            ReplayAndAssert(golden);
            replayed++;
        }

        // Branches: a deterministic stratified sample — first two and last one of
        // every (suite x verdict x opcode x handler-kind-trio) category — plus a
        // pinned total. The full ~33k sweep is the env-gated endurance test.
        foreach (var group in suites.Branches.GroupBy(golden => golden.Category).OrderBy(group => group.Key))
        {
            foreach (var golden in group.Take(2).Concat(group.TakeLast(1)))
            {
                ReplayAndAssert(golden);
                replayed++;
            }
        }

        Assert.Equal(PINSAMPLEREPLAYED, replayed);
    }

    private static void ReplayAndAssert(NetPerf.Goldens.ProtectedBlocksGolden golden)
    {
        var result = NetPerf.Goldens.GoldenReplay.Replay(golden);
        if (golden.ExpectedValid)
        {
            Assert.True(result.IsValid,
                $"{golden.TestName}: era golden passes but adapted validator rejects: {string.Join("; ", result.ErrorMessages)}");
            return;
        }
        Assert.False(result.IsValid, $"{golden.TestName}: era golden fails but adapted validator passes");
        if (golden.ErrorStartsWith != null)
            Assert.StartsWith(golden.ErrorStartsWith, result.ErrorMessages[0], StringComparison.Ordinal);
        if (golden.ErrorEndsWith != null)
            Assert.EndsWith(golden.ErrorEndsWith, result.ErrorMessages[0], StringComparison.Ordinal);
    }

    private static void ReplayAndAssert(NetPerf.Goldens.BranchesGolden golden)
    {
        var result = NetPerf.Goldens.GoldenReplay.Replay(golden);
        if (golden.ExpectedValid)
        {
            Assert.True(result.IsValid,
                $"{golden.Suite}|{golden.TestName}: era golden passes but adapted validator rejects: {string.Join("; ", result.ErrorMessages)}");
            return;
        }
        Assert.False(result.IsValid, $"{golden.Suite}|{golden.TestName}: era golden fails but adapted validator passes");
        if (golden.ExpectedErrorSuffix != null)
            Assert.EndsWith(golden.ExpectedErrorSuffix, result.ErrorMessages[0], StringComparison.Ordinal);
    }

    // Era-validated exception-handler/branch shapes must decode identically
    // through tracemap's dual SRM + Cecil readers. Each sampled golden body
    // becomes a method (with one trailing call so body admission is observable
    // as a ManagedIlCallObserved fact — the nop grids contain no calls), the
    // assembly is scanned with the IL body lane, and the scan must be gap-free.
    [Fact]
    public void Golden_constructed_bodies_decode_identically_through_both_tracemap_readers()
    {
        var suites = Suites;
        var sample = suites.Branches
            .GroupBy(golden => golden.Category).OrderBy(group => group.Key)
            .SelectMany(group => group.Take(1))
            .Take(24)
            .ToList();

        using var temp = new TempDirectory();
        var assemblyPath = Path.Combine(temp.Path, "GoldenShapes.dll");
        WriteGoldenBodiesAssembly(assemblyPath, sample);

        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: [assemblyPath], IlBodyEvidence: true));
        // A standalone DLL legitimately carries manifest/binding/value-model gaps;
        // the property under test is that both readers agree on every golden body
        // (IlReaderDisagreement is the body lane's dual-reader failure gap) and
        // that each body was admitted far enough to produce call facts.
        Assert.DoesNotContain(scan.Facts, fact =>
            fact.FactType == FactTypes.AnalysisGap
            && (fact.ContractElement == "IlReaderDisagreement" || fact.ContractElement == "IlValueReaderDisagreementOrLimit"));
        var callFacts = scan.Facts.Count(fact => fact.FactType == FactTypes.ManagedIlCallObserved);
        Assert.True(callFacts >= sample.Count, $"expected a call fact per golden body, got {callFacts}/{sample.Count}");
    }

    private static void WriteGoldenBodiesAssembly(string path, List<NetPerf.Goldens.BranchesGolden> goldens)
    {
        using var module = ModuleDefinition.CreateModule("GoldenShapes", new ModuleParameters { Kind = ModuleKind.Dll });
        var type = new TypeDefinition("GoldenShapes", "GoldenMethods",
            TypeAttributes.Public | TypeAttributes.Abstract | TypeAttributes.Sealed, module.TypeSystem.Object);
        module.Types.Add(type);

        var writeLine = module.ImportReference(typeof(Console).GetMethod(nameof(Console.WriteLine), Type.EmptyTypes));
        for (var i = 0; i < goldens.Count; i++)
        {
            var golden = goldens[i];
            var method = new MethodDefinition($"Case{i}_{golden.SourceOpcode}_{golden.SourceKind}_{golden.TargetKind}_{golden.Target2Kind}",
                MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            var body = NetPerf.Goldens.GoldenReplay.BuildBody(golden, method);
            // One trailing call makes body admission observable; inserted before
            // the final ret, outside every exception region.
            var ret = body.Instructions[^1];
            body.GetILProcessor().InsertBefore(ret, Instruction.Create(OpCodes.Call, writeLine));
            // The era construction leaves catch handlers without a catch type (the
            // era validated in memory and never wrote these bodies); Cecil refuses
            // to serialize a null catch-type token, so pin it to object.
            foreach (var handler in body.ExceptionHandlers)
                if (handler.HandlerType == Mono.Cecil.Cil.ExceptionHandlerType.Catch && handler.CatchType == null)
                    handler.CatchType = module.TypeSystem.Object;
            method.Body = body;
            type.Methods.Add(method);
        }

        module.Write(path);
    }

    [Fact]
    public void Era_instrumented_fixture_id_map_matches_the_recorded_xml_golden()
    {
        var xml = NetPerf.Goldens.EraInstrumentationData.ParseXml(TestAssembliesPath("CallGraphTestClasses.xml"));
        var literal = NetPerf.Goldens.EraInstrumentationData.ReadLiteral(TestAssembliesPath("CallGraphTestClasses.dll"));

        // The committed XML is the recorded expected value of the DLL's baked literal.
        Assert.Equal(xml.InstanceHash, literal.InstanceHash);
        Assert.Equal(xml.ModuleId, literal.ModuleId);
        Assert.Equal(xml.AssemblyIdentity, literal.AssemblyIdentity);
        Assert.Equal(xml.PreInstrumentedHash, literal.PreInstrumentedHash);
        Assert.Equal(xml.MethodsById, literal.MethodsById);

        // tracemap's Cecil decode of the instrumented output reproduces the
        // recorded id map exactly, in both directions. The era signature format
        // omits generic arity, so distinct methods can share a signature — the
        // comparison is on (id, signature) multisets, not a signature-keyed map.
        var decoded = NetPerf.Goldens.EraInstrumentationDecoder.DecodedMethodIdPairs(TestAssembliesPath("CallGraphTestClasses.dll"));
        Assert.Equal(
            literal.MethodsById.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}|{pair.Value}"),
            decoded.OrderBy(pair => pair.Id).Select(pair => $"{pair.Id}|{pair.Signature}"));
    }

    [Fact]
    public void Duplicates_fixture_literal_is_internally_consistent_under_decode()
    {
        var literal = NetPerf.Goldens.EraInstrumentationData.ReadLiteral(TestAssembliesPath("CallGraphTestClassesDuplicates.dll"));
        var decoded = NetPerf.Goldens.EraInstrumentationDecoder.DecodedMethodIdPairs(TestAssembliesPath("CallGraphTestClassesDuplicates.dll"));
        Assert.Equal(
            literal.MethodsById.OrderBy(pair => pair.Key).Select(pair => $"{pair.Key}|{pair.Value}"),
            decoded.OrderBy(pair => pair.Id).Select(pair => $"{pair.Id}|{pair.Signature}"));
    }

    // The full ~33.9k-expectation sweep. Visible skip unless explicitly enabled
    // (the compiled-dotnet-evidence-foundation spec keeps the full corpus out of
    // the default suite): set TRACEMAP_ORACLE_FULL_GOLDENS=1 to run it.
    [OracleGoldenSweepFact]
    public void Full_generated_golden_sweep()
    {
        var suites = Suites;
        var failures = new List<string>();
        foreach (var golden in suites.Branches)
        {
            var result = NetPerf.Goldens.GoldenReplay.Replay(golden);
            if (result.IsValid != golden.ExpectedValid
                || (!golden.ExpectedValid && golden.ExpectedErrorSuffix != null && !result.ErrorMessages[0].EndsWith(golden.ExpectedErrorSuffix, StringComparison.Ordinal)))
                failures.Add($"{golden.Suite}|{golden.TestName}: expected valid={golden.ExpectedValid}, got valid={result.IsValid}: {string.Join("; ", result.ErrorMessages)}");
        }
        foreach (var golden in suites.ProtectedBlocks)
        {
            var result = NetPerf.Goldens.GoldenReplay.Replay(golden);
            if (result.IsValid != golden.ExpectedValid)
                failures.Add($"ProtectedBlocksTest|{golden.TestName}: expected valid={golden.ExpectedValid}, got valid={result.IsValid}");
        }
        Assert.True(failures.Count == 0, $"{failures.Count} golden mismatches:\n{string.Join("\n", failures.Take(20))}");
    }
}

public sealed class OracleGoldenSweepFactAttribute : FactAttribute
{
    public OracleGoldenSweepFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("TRACEMAP_ORACLE_FULL_GOLDENS") != "1")
            Skip = "Set TRACEMAP_ORACLE_FULL_GOLDENS=1 to replay every generated golden expectation.";
    }
}
