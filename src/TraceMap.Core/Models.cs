namespace TraceMap.Core;

public sealed record ScanManifest(
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    DateTimeOffset ScannedAt,
    string AnalysisLevel,
    string BuildStatus,
    IReadOnlyList<string> Solutions,
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> TargetFrameworks,
    IReadOnlyList<string> KnownGaps);

public sealed record EvidenceSpan(
    string FilePath,
    int StartLine,
    int EndLine,
    string? SnippetHash,
    string ExtractorId,
    string ExtractorVersion);

public sealed record CodeFact(
    string FactId,
    string ScanId,
    string Repo,
    string CommitSha,
    string? ProjectPath,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    EvidenceSpan Evidence,
    IReadOnlyDictionary<string, string> Properties);

public sealed record ScanOptions(string RepoPath, string OutputPath);

public sealed record FileInventoryItem(
    string RelativePath,
    string Kind,
    long SizeBytes);

public sealed record GitMetadata(
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    IReadOnlyList<string> KnownGaps);

public sealed record ScanResult(
    ScanManifest Manifest,
    IReadOnlyList<CodeFact> Facts,
    IReadOnlyList<FileInventoryItem> Inventory);

public static class EvidenceTiers
{
    public const string Tier2Structural = nameof(Tier2Structural);
    public const string Tier3SyntaxOrTextual = nameof(Tier3SyntaxOrTextual);
    public const string Tier4Unknown = nameof(Tier4Unknown);
}

public static class FactTypes
{
    public const string RepoScanned = nameof(RepoScanned);
    public const string BuildStatus = nameof(BuildStatus);
    public const string AnalysisGap = nameof(AnalysisGap);
    public const string FileInventoried = nameof(FileInventoried);
    public const string SolutionDeclared = nameof(SolutionDeclared);
    public const string ProjectDeclared = nameof(ProjectDeclared);
    public const string PackageReferenced = nameof(PackageReferenced);
    public const string TargetFrameworkDeclared = nameof(TargetFrameworkDeclared);
    public const string ConfigFileDeclared = nameof(ConfigFileDeclared);
    public const string SqlFileDeclared = nameof(SqlFileDeclared);
}

public static class RuleIds
{
    public const string RepoManifest = "repo.manifest.v1";
    public const string FileInventory = "file.inventory.v1";
    public const string ProjectFile = "project.file.v1";
}

public static class ScannerVersions
{
    public const string TraceMap = "tracemap-milestone1";
    public const string RepoManifestExtractor = "repo-manifest/0.1.0";
    public const string FileInventoryExtractor = "file-inventory/0.1.0";
    public const string ProjectFileExtractor = "project-file/0.1.0";
}
