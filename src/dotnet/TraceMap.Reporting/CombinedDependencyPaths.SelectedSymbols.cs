using System.Text.Json.Serialization;

namespace TraceMap.Reporting;

public sealed record CombinedPathSymbolRoot(string SourceIndexId, string ScanId, string CommitSha, string SymbolId);
public sealed record CombinedPathAdmissionLimits(int MaxFacts = 250_000, int MaxEdges = 250_000,
    long MaxTextBytes = 128L * 1024 * 1024)
{
    public const long DefaultMaxGraphStorageBytes = 512L * 1024 * 1024;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? MaxGraphStorageBytes { get; init; }
}

public static partial class CombinedDependencyPathReporter
{
    /// <summary>One bounded graph admission for exact, caller-selected roots. No label or short-name authority.</summary>
    public static async Task<CombinedDependencyPathReport> BuildSelectedSymbolsAsync(
        CombinedDependencyPathOptions options, IReadOnlyList<CombinedPathSymbolRoot> roots,
        bool combinedIndex, CombinedPathAdmissionLimits? limits = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(roots);
        limits ??= new();
        if (limits.MaxGraphStorageBytes is { } storage && (storage < 64 * 1024 || storage > 16L * 1024 * 1024 * 1024))
            throw new ArgumentOutOfRangeException(nameof(limits), "Graph storage must be between 64 KiB and 16 GiB.");
        if (roots.Count > 10_000 || roots.Distinct().Count() != roots.Count ||
            roots.Any(root => root is null || string.IsNullOrWhiteSpace(root.SourceIndexId) || root.SourceIndexId.Length > 256 ||
                string.IsNullOrWhiteSpace(root.ScanId) || root.ScanId.Length > 256 || root.CommitSha is not { Length: 40 } ||
                root.CommitSha.Any(character => character is not (>= '0' and <= '9' or >= 'a' and <= 'f')) ||
                string.IsNullOrWhiteSpace(root.SymbolId) || root.SymbolId.Length > 65_536) ||
            options.FromEndpoint is not null || options.FromSymbol is not null || options.FromWebFormsEvent is not null ||
            options.FromSource is not null || options.SourcePair is not null)
            throw new InvalidDataException("COMPILED_SELECTED_SYMBOL_ROOTS_INVALID");
        var selected = options with { SymbolRoots = roots.ToArray(), StartingNodeLimit = Math.Max(1, roots.Count) };
        var budget = new ReportInputBudget(limits.MaxFacts, limits.MaxEdges, limits.MaxTextBytes)
            { MaxGraphStorageBytes = limits.MaxGraphStorageBytes ?? CombinedPathAdmissionLimits.DefaultMaxGraphStorageBytes };
        return combinedIndex
            ? (await BuildBoundedCombinedIndexReportWithTraversalAsync(selected, budget, cancellationToken)).Report
            : (await BuildBoundedSingleIndexReportWithTraversalAsync(selected, budget, cancellationToken)).Report;
    }
}
