using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Cli;

/// <summary>Normal scan progress survives external termination; values/paths never enter the binding projection.</summary>
internal sealed class ScanDiagnosticProgress : IDisposable
{
    private readonly ScanProgressReporter reporter;
    private readonly ScanProgressReporter? previous;
    private ScanDiagnosticProgress(ScanProgressReporter reporter)
    {
        this.reporter = reporter;
        previous = ScanProgressAmbient.Current;
        ScanProgressAmbient.Current = reporter;
    }
    internal static ScanDiagnosticProgress? Create(string? requested, string repo, string output, ScanOptions options)
    {
        if (requested is null) return null;
        if (ScanProgressAmbient.Current is not null) throw new ArgumentException("nested-progress-not-supported");
        string path;
        try { path = LocalReviewCommand.ValidateDiagnosticProgressPath(repo, output, requested); }
        catch (Exception) { throw new ArgumentException("progress-path-invalid"); }
        var paths = new[] { path, path + ".performance.json", path + ".binding.json" };
        foreach (var item in paths)
            if (File.Exists(item) || Directory.Exists(item)) throw new ArgumentException("progress-path-exists");
        var projection = new
        {
            compiledInputs = options.CompiledInputPaths?.Count ?? 0,
            lifecycleRoots = options.IlLifecycleRoots?.Count ?? 0,
            options.Restore, options.ExactSourceScope, options.IlBodyEvidence,
            options.IlOpaqueRootInitializers, options.IlExternalTraceBoundary,
            options.IlCompiledScalarBoundary, options.IlInfoMessageBoundary,
            projects = options.ProjectPaths?.Count ?? 0, solutions = options.SolutionPaths?.Count ?? 0,
            includes = options.IncludeGlobs?.Count ?? 0, excludes = options.ExcludeGlobs?.Count ?? 0
        };
        var generator = Hash(string.Join("\n", new[] { typeof(ScanDiagnosticProgress).Assembly.Location,
            typeof(ScanProgressReporter).Assembly.Location }.Select(file => Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(file))))));
        var input = Hash(JsonSerializer.Serialize(projection));
        using (var binding = new FileStream(paths[2], FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            JsonSerializer.Serialize(binding, new { schema = "scan-progress-binding/1", generatorSha256 = generator,
                boundedInputSha256 = input, inputProjection = projection,
                limitation = "mutable-operational-observation;not-scan-success;values-and-paths-not-bound;not-evidence" });
        var reporter = new ScanProgressReporter(Console.Error, path, heartbeatInterval: TimeSpan.FromSeconds(2));
        if (reporter.ReadCheckpoint() is null) { reporter.Dispose(); throw new IOException("progress-checkpoint-unavailable"); }
        reporter.Emit(ScanProgressReporter.ScanOperation, ScanProgressStages.ArgumentsValidated, "completed");
        return new(reporter);
    }
    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    public void Dispose()
    {
        ScanProgressAmbient.Current = previous;
        reporter.Dispose();
    }
}
