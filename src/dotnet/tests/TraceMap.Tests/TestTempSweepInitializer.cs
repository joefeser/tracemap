namespace TraceMap.Tests;

/// <summary> Sweeps stale test temp directories once per test-assembly load, before
/// any test runs — covering every tracemap-* temp creator in the suite (scan outputs,
/// WebForms review roots, PDB extractors, source-metadata fixtures), not just the ones
/// that route through TempOutput(). Age-gated to one hour so a concurrently running
/// test process's live directories are never touched.</summary>
internal static class TestTempSweepInitializer
{
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void Sweep() => IlBodyEvidenceExtractorShared.SweepStaleTempDirectories();
}
