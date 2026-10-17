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
        public NetPerf.Common.Instrumentation.IMessagingController MessageController { get; } = new Controller();

        private sealed class Controller : NetPerf.Common.Instrumentation.IMessagingController
        {
            public bool WantsMethodEnterMessages => true;
            public bool WantsMethodExitMessages => true;
            public bool WantsSeqPointEndMessages => false;
        }

        public int RegisterAssembly(string assemblyIdentity, string preInstrumentedHash, NetPerf.Common.HashAlgorithm preInstrumentedHashAlgorithm) => ++_next;
        public string GetMetadataForAssembly(int assemblyId, out NetPerf.Common.ErrorTuple error)
        {
            error = new NetPerf.Common.ErrorTuple();
            return "netperf-oracle-metadata";
        }
        public int RegisterModule(int assemblyId, string moduleName) => ++_next;
        public int RegisterType(int moduleId, string signature, string[] customAttributes, string[] genericArguments) => ++_next;
        public int RegisterMethod(int typeId, string signature, byte[] rawMethodBytes, string[] customAttributes, int[] sourceFileIds, string[] genericArguments)
        {
            Methods++;
            return ++_next;
        }
        public int RegisterEvent(int typeId, string signature, int? addMethodId, int? removeMethodId, int? fireMethodId, int[] otherMethodIds, string[] customAttributes) => ++_next;
        public int RegisterProperty(int typeId, string signature, int? getMethodId, int? setMethodId, int[] otherMethodIds, string[] customAttributes) => ++_next;
        public int RegisterSourceFile(int moduleId, string uri, byte[] hash, string hashAlgorithm, string sourceLanguage) => ++_next;
        public int LookupSourceFile(int methodId, string uri) => -1;
        public int RegisterPoint(int methodId, int? sourceFileId = null, int? startLine = null, int? startColumn = null, int? endLine = null, int? endColumn = null) => ++_next;
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
