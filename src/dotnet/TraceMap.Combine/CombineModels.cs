namespace TraceMap.Combine;

public sealed record CombineOptions(
    IReadOnlyList<string> IndexPaths,
    string OutputPath,
    IReadOnlyList<string>? Labels = null);

public sealed record CombineResult(
    string OutputPath,
    IReadOnlyList<CombinedIndexSource> Sources,
    int FactCount,
    int SymbolCount,
    int RelationshipCount,
    int CallEdgeCount);

public sealed record CombinedIndexSource(
    string SourceIndexId,
    string Label,
    string IndexPath,
    string IndexPathHash,
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    string? Language,
    string? ScanRootRelativePath,
    string? ScanRootPathHash,
    string? GitRootHash,
    string AnalysisLevel,
    string BuildStatus);
