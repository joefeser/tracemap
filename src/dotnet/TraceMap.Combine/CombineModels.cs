namespace TraceMap.Combine;

public sealed record CombineOptions(
    IReadOnlyList<string> IndexPaths,
    string OutputPath,
    IReadOnlyList<string>? Labels = null)
{
    // Explicit local contracts only. Ordinary combines never infer attachment
    // authority from a shared commit, source label or nearby manifest filename.
    public IReadOnlyList<CompiledAttachmentCombineInput> CompiledAttachments { get; init; } = [];
    public long MaxAttachmentIndexBytes { get; init; } = 16L * 1024 * 1024 * 1024;
    public long MaxAttachmentHashBytes { get; init; } = 32L * 1024 * 1024 * 1024;
}

public sealed record CompiledAttachmentCombineInput(
    string ParentIndexPath, string ParentManifestPath,
    string AttachmentIndexPath, string AttachmentManifestPath);

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
