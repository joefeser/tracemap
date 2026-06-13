using TraceMap.Core;

namespace TraceMap.EndpointAlignment;

public sealed record EndpointAlignmentOptions(
    string ClientIndexPath,
    string ServerIndexPath,
    string OutputPath,
    string Format = "markdown",
    string? ClientLabel = null,
    string? ServerLabel = null);

public sealed record EndpointAlignmentWriteResult(
    EndpointAlignmentReport Report,
    string? MarkdownPath,
    string? JsonPath);

public sealed record EndpointAlignmentReport(
    string RuleId,
    string ScannerVersion,
    string ReportCoverage,
    IReadOnlyList<string> CoverageWarnings,
    EndpointSource ClientSource,
    EndpointSource ServerSource,
    IReadOnlyList<EndpointFinding> Findings);

public sealed record EndpointSource(
    string Label,
    string IndexPathHash,
    string ScanId,
    string RepoName,
    string? RemoteUrl,
    string? Branch,
    string CommitSha,
    string ScannerVersion,
    string AnalysisLevel,
    string BuildStatus,
    string? ScanRootRelativePath,
    string? ScanRootPathHash,
    string? GitRootHash);

public sealed record EndpointCandidate(
    string FactId,
    string ScanId,
    string CommitSha,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    string? SourceSymbol,
    string? TargetSymbol,
    string? ContractElement,
    string Method,
    string? NormalizedPathTemplate,
    string? NormalizedPathKey,
    IReadOnlyList<string> ExpandedPathKeys,
    IReadOnlyList<string> ParameterNames,
    IReadOnlyList<string> OptionalParameterNames,
    IReadOnlyList<string> QueryParameterNames,
    bool HasQueryParameters,
    bool IsDynamic,
    string? DynamicReason,
    IReadOnlyDictionary<string, string> Properties);

public sealed record EndpointFinding(
    string Classification,
    string Method,
    string? NormalizedPathKey,
    string? ClientPathTemplate,
    string? ServerPathTemplate,
    string? ClientFactId,
    string? ServerFactId,
    string ClientScanId,
    string ServerScanId,
    string ClientCommitSha,
    string ServerCommitSha,
    string RuleId,
    string StaticMatchQuality,
    string? ClientEvidenceTier,
    string? ServerEvidenceTier,
    EndpointEvidence? ClientEvidence,
    EndpointEvidence? ServerEvidence,
    IReadOnlyList<string> Notes);

public sealed record EndpointEvidence(
    string FactId,
    string FactType,
    string RuleId,
    string EvidenceTier,
    string FilePath,
    int StartLine,
    int EndLine,
    string CommitSha,
    string ExtractorVersion);

public static class EndpointClassifications
{
    public const string MatchedEndpoint = nameof(MatchedEndpoint);
    public const string OptionalSegmentMatch = nameof(OptionalSegmentMatch);
    public const string MethodMismatch = nameof(MethodMismatch);
    public const string ClientCallNoServerEndpoint = nameof(ClientCallNoServerEndpoint);
    public const string ServerEndpointNoClientMatch = nameof(ServerEndpointNoClientMatch);
    public const string AmbiguousMatch = nameof(AmbiguousMatch);
    public const string DynamicClientUrlNeedsReview = nameof(DynamicClientUrlNeedsReview);
    public const string UnknownAnalysisGap = nameof(UnknownAnalysisGap);
}
