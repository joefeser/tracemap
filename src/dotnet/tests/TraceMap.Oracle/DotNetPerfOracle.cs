namespace NetPerf;

// Internal facade over the era instrumenter/validator for the test project
// (InternalsVisibleTo). #856 stage 2b.
internal static class DotNetPerfOracle
{
    internal static bool IsInstrumented(string assembly) =>
        ILInstrumenter.IsAssemblyInstrumented(new Common.FilePath(assembly), null);

    internal static void Instrument(string assembly, Common.Instrumentation.IInstrumentFactory factory) =>
        ILInstrumenter.InstrumentAssembly(new Common.FilePath(assembly), null, null, factory);
}
