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
        // These fixtures were rebuilt with 4.x-era tooling, so the attribute basis
        // fires; both bases are acceptable with basis-consistent floor values, and
        // the floor never guesses outside its documented shapes.
        var basis = declaration.Properties["runtimeFloorBasis"];
        var floor = declaration.Properties["runtimeFloor"];
        if (basis == "target-framework-attribute")
            Assert.StartsWith(".NETFramework,Version=v", floor, StringComparison.Ordinal);
        else
        {
            Assert.Equal("mscorlib-assembly-reference", basis);
            Assert.True(floor is "clr-2.x" or "clr-4.x", $"unexpected runtime floor {floor}");
            Assert.Matches(@"^\d+\.\d+\.\d+\.\d+$", declaration.Properties["runtimeFloorSourceVersion"]);
        }

        Assert.Contains(first.Facts, fact => fact.FactType == FactTypes.ManagedLateBindingCensus);
        Assert.Contains(first.Facts, fact => fact.FactType == FactTypes.ManagedIlCallObserved);
    }

    [Theory]
    [InlineData("NormalPublicClass.netmodule", "etmodule")]
    [InlineData("ConflictingNameSpace.netmodule", "etmodule")]
    // MultiModule.dll is a multi-module assembly manifest referencing the
    // .netmodules — its own typed boundary, distinct from a bare netmodule.
    [InlineData("MultiModule.dll", "MultiModuleManagedAssemblyUnsupported")]
    public void Era_unsupported_input_shapes_stay_typed_boundaries(string inputName, string marker)
    {
        var input = TestAssembly(inputName);
        Assert.True(File.Exists(input), input);
        using var temp = new TempDirectory();
        var scan = ScanEngine.Scan(new ScanOptions(temp.Path, Path.Combine(temp.Path, "out"),
            CompiledInputPaths: [input], IlBodyEvidence: true));
        Assert.DoesNotContain(scan.Facts, fact => fact.FactType == FactTypes.ManagedAssemblyDeclared);
        Assert.Contains(scan.Facts, fact => fact.FactType == FactTypes.AnalysisGap
            && (fact.ContractElement?.Contains(marker, StringComparison.Ordinal) == true
                || fact.Properties.GetValueOrDefault("gapKind")?.Contains(marker, StringComparison.Ordinal) == true));
    }
}
