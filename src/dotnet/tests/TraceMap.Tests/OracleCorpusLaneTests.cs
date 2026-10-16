using TraceMap.Core;

namespace TraceMap.Tests;

// Stage 2a of the dotnetperf oracle wiring (#856): the corpus commits its own
// compiled VS-era test fixtures; this lane scans them with the compiled/IL
// evidence lanes and pins admission, determinism, runtime floor, and census
// behavior on era-real binaries. No dotnetperf code executes here.
public sealed class OracleCorpusLaneTests
{
    private static string CorpusRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (Directory.Exists(Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter")))
                return Path.Combine(directory.FullName, "samples", "dotnetperf-rewriter");
        throw new DirectoryNotFoundException("dotnetperf corpus unavailable");
    }

    private static string TestAssembly(string name) => Path.Combine(
        CorpusRoot(), "src", "NetPerf.Tests.Integration", "TestAssemblies", name);

    public static TheoryData<string> EraAssemblies => new()
    {
        "CallGraphTestClasses.dll",
        "CallGraphTestClassesDuplicates.dll",
    };

    [Theory]
    [MemberData(nameof(EraAssemblies))]
    public void Era_binaries_admit_deterministically_with_runtime_floor_and_census(string assemblyName)
    {
        var assembly = TestAssembly(assemblyName);
        Assert.True(File.Exists(assembly), assembly);
        using var temp = new TempDirectory();
        ScanResult First() => ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: [assembly], IlBodyEvidence: true));
        var first = First();
        var second = First();
        // Determinism: identical scans produce identical facts, byte for byte.
        Assert.Equal(
            first.Facts.Select(fact => fact.FactId).Order(StringComparer.Ordinal),
            second.Facts.Select(fact => fact.FactId).Order(StringComparer.Ordinal));

        var declaration = Assert.Single(first.Facts, fact => fact.FactType == FactTypes.ManagedAssemblyDeclared);
        // The committed fixtures are immutable corpus bytes rebuilt with 4.x-era
        // tooling (.NETFramework,Version=v4.0 verbatim in the attribute blob), so
        // the observed basis pins EXACTLY — a regression in attribute extraction
        // that falls back to mscorlib must fail here, not pass conditionally.
        Assert.Equal("target-framework-attribute", declaration.Properties["runtimeFloorBasis"]);
        Assert.Equal(".NETFramework,Version=v4.0", declaration.Properties["runtimeFloor"]);
        Assert.False(declaration.Properties.ContainsKey("runtimeFloorSourceVersion"));

        Assert.Contains(first.Facts, fact => fact.FactType == FactTypes.ManagedLateBindingCensus);
        Assert.Contains(first.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
    }

    [Theory]
    [InlineData("NormalPublicClass.netmodule", "ManagedNetmoduleInputUnsupported")]
    [InlineData("ConflictingNameSpace.netmodule", "ManagedNetmoduleInputUnsupported")]
    // MultiModule.dll is a multi-module assembly manifest referencing the
    // .netmodules — its own typed boundary, distinct from a bare netmodule.
    [InlineData("MultiModule.dll", "MultiModuleManagedAssemblyUnsupported")]
    public void Era_unsupported_input_shapes_stay_typed_boundaries(string inputName, string expectedGap)
    {
        var input = TestAssembly(inputName);
        Assert.True(File.Exists(input), input);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: [input], IlBodyEvidence: true));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.ManagedAssemblyDeclared);
        // Exact gap-name pins: a differently named gap must fail the boundary claim.
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && (fact.ContractElement == expectedGap
                || fact.Properties.GetValueOrDefault("gapKind") == expectedGap));
    }
}
