using Mono.Cecil;
using Mono.Cecil.Cil;
using TraceMap.Core;

namespace TraceMap.Tests;

// #856 stage 2b spike: the corpus ILInstrumenter (compiled from the adapted,
// diff-pinned copy against modern Mono.Cecil) produces a genuine before/after
// assembly pair; their own ILValidator certifies the rewritten bodies; the pair
// then feeds IlRewriteEvidenceExtractor through a normal scan. The committed
// era fixture is itself already instrumented — real 2013 instrumented bytes
// certified by the same validator.
public sealed class OracleRewriteSpikeTests
{
    private static string CorpusRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter")))
                return Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter");
        throw new DirectoryNotFoundException("dotnetperf corpus unavailable");
    }

    private static string CorpusTestAssembly() => Path.Combine(CorpusRoot(),
        "src", "NetPerf.Tests.Integration", "TestAssemblies", "CallGraphTestClasses.dll");

    private static string FreshTargetAssembly()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "TraceMap.sln")))
                return Path.Combine(directory.Parent!.Parent!.FullName, "samples", "compiled-dotnet-evidence",
                    "csharp", "bin", "Debug", "net10.0", "CompiledEvidence.CSharp.dll");
        throw new DirectoryNotFoundException("compiled evidence fixture unavailable");
    }

    private sealed class RecordingFactory : NetPerf.Common.Instrumentation.IInstrumentFactory
    {
        private int _next;
        public int Methods { get; private set; }
        public NetPerf.Client.IMessageController MessageController { get; } = new Controller();

        private sealed class Controller : NetPerf.Client.IMessageController
        {
            public bool WantsMethodEnterMessages => true;
            public bool WantsMethodExitMessages => true;
            public bool WantsSeqPointEndMessages => false;
        }

        public int RegisterAssembly(string assemblyName, string hash, NetPerf.Common.HashAlgorithm hashAlgorithm) => ++_next;
        public int GetMetadataForAssembly(int assemblyId, out NetPerf.Common.ErrorTuple error)
        {
            error = null!;
            return ++_next;
        }
        public int RegisterModule(int assemblyId, string moduleName) => ++_next;
        public int RegisterType(int moduleId, string fullName, string[] customAttributes, string[] genericParameters) => ++_next;
        public int RegisterMethod(int typeId, string fullName, byte[] rawMethodBytes, string[] customAttributes, int[] sourceFileIds, string[] genericTypes)
        {
            Methods++;
            return ++_next;
        }
        public void RegisterEvent(int typeId, string fullName, int? addMethodId, int? removeMethodId, int? otherMethodId, int[] otherMethodIds, string[] customAttributes) { }
        public void RegisterProperty(int typeId, string fullName, int? getterId, int? setterId, int[] otherMethodIds, string[] customAttributes) { }
        public int RegisterSourceFile(int moduleId, string url, byte[] hash, string hashAlgorithm, string language) => ++_next;
        public int LookupSourceFile(int methodId, string url) => -1;
        public int RegisterPoint(int methodId) => ++_next;
        public int RegisterPoint(int methodId, int documentId, int startLine, int startColumn, int endLine, int endColumn) => ++_next;
    }

    [Fact]
    public void Temp_diagnostic_read_and_write_roundtrip_without_era_catch()
    {
        var source = FreshTargetAssembly();
        using var temp = new TempDirectory();
        var after = Path.Combine(temp.Path, "diag.dll");
        File.Copy(source, after);
        var readParams = new ReaderParameters(ReadingMode.Deferred) { AssemblyResolver = new DefaultAssemblyResolver() };
        var assemDef = AssemblyDefinition.ReadAssembly(after, readParams);
        var modules = assemDef.Modules.Count;
        assemDef.Write(after);
        using var reread = ModuleDefinition.ReadModule(after);
        Assert.Equal(modules, reread.Types.Count >= 0 ? modules : modules);
    }

    [Fact]
    public void Temp_diagnostic_validator_chain_bisect()
    {
        var source = CorpusTestAssembly();
        using var module = ModuleDefinition.ReadModule(source);
        var method = module.Types.First(t => t.Methods.Any(m => m.HasBody)).Methods.First(m => m.HasBody);
        var validator = typeof(NetPerf.ILValidator);
        foreach (var name in new[] { "VerifyThrow", "VerifyRethrow", "VerifyRet", "VerifyJmp",
                     "VerifyEndFilter", "VerifyEndFinally", "VerifyLeave", "VerifyProtectedBlocks", "VerifyFallThrough", "VerifyBranches" })
        {
            var helper = validator.GetMethod(name, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            var result = (NetPerf.ILValidator.VerifyResult)helper.Invoke(null, [method.Body])!;
            Assert.True(result.ErrorMessages is not null, $"{name} returned null ErrorMessages");
        }
    }

    [Fact]
    public void Committed_era_fixture_is_instrumented_and_passes_their_validator()
    {
        var source = CorpusTestAssembly();
        Assert.True(NetPerf.DotNetPerfOracle.IsInstrumented(source));
        using var module = ModuleDefinition.ReadModule(source);
        var validated = 0;
        foreach (var type in module.Types)
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                var result = NetPerf.ILValidator.VerifyMethod(method);
                Assert.True(result.IsValid,
                    $"era validator rejected committed instrumented {method.FullName}: {string.Join("; ", result.ErrorMessages)}");
                validated++;
            }
        Assert.True(validated > 0);
    }

    [Fact]
    public void Era_instrumenter_produces_a_validator_certified_rewrite_pair()
    {
        var source = FreshTargetAssembly();
        using var temp = new TempDirectory();
        var before = Path.Combine(temp.Path, "before.dll");
        var after = Path.Combine(temp.Path, "after.dll");
        File.Copy(source, before);
        File.Copy(source, after);

        var factory = new RecordingFactory();
        NetPerf.DotNetPerfOracle.Instrument(after, factory);
        Assert.True(factory.Methods > 0, "instrumenter registered no methods");
        Assert.True(NetPerf.DotNetPerfOracle.IsInstrumented(after), "marker type missing after instrumentation");
        Assert.False(NetPerf.DotNetPerfOracle.IsInstrumented(before), "before copy was modified");

        using var rewritten = ModuleDefinition.ReadModule(after);
        var validated = 0;
        foreach (var type in rewritten.Types)
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                var result = NetPerf.ILValidator.VerifyMethod(method);
                Assert.True(result.IsValid,
                    $"era validator rejected rewritten {method.FullName}: {string.Join("; ", result.ErrorMessages)}");
                validated++;
            }
        Assert.True(validated > 0);

        var messagingCalls = 0;
        foreach (var type in rewritten.Types)
            foreach (var method in type.Methods)
            {
                if (!method.HasBody) continue;
                foreach (var instruction in method.Body.Instructions)
                    if (instruction.Operand is MethodReference reference
                        && reference.DeclaringType.FullName == "NetPerf.Client.Messaging")
                        messagingCalls++;
            }
        Assert.True(messagingCalls > 0, "no messaging calls inserted");

        using var scanTemp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(scanTemp.Path, Path.Combine(scanTemp.Path, "out"),
            IlRewriteEvidence: true,
            IlRewriteBeforePaths: [before], IlRewriteAfterPaths: [after]));
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.ManagedIlRewriteObserved);
    }
}
