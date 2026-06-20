using TraceMap.Core;

namespace TraceMap.Reporting.ReviewPriority;

public sealed record ReviewPrioritySummary(
    string Status,
    string ModelVersion,
    string AttentionLevel,
    int? PriorityScore,
    bool Complete,
    IReadOnlyList<string> ContributingSections,
    IReadOnlyList<string> LimitedSections,
    IReadOnlyList<ReviewPriorityComponent> Components,
    IReadOnlyList<string> Limitations);

public sealed record ReviewPriorityRow(
    string RowId,
    string RowKind,
    string Section,
    string? SourceLabel,
    string Classification,
    string RuleId,
    string EvidenceTier,
    string SeverityHint,
    int? PriorityScore,
    bool Complete,
    IReadOnlyList<ReviewPriorityComponent> Components,
    IReadOnlyList<string> SourceEvidenceIds,
    IReadOnlyList<string> Limitations);

public sealed record ReviewPriorityComponent(
    string ComponentId,
    string ComponentKind,
    int? ComponentValue,
    string Direction,
    string RuleId,
    string EvidenceTier,
    IReadOnlyList<ReviewPriorityEvidenceRef> SourceEvidence,
    IReadOnlyList<string> Limitations,
    IReadOnlyList<KeyValuePair<string, string>> Metadata);

public sealed record ReviewPriorityEvidenceRef(
    string EvidenceId,
    string EvidenceKind,
    string RuleId,
    string? EvidenceTier,
    string? SourceLabel,
    string? CommitSha,
    string? FilePath,
    int? StartLine,
    int? EndLine);

public static class ReviewPriorityStatuses
{
    public const string Available = "available";
    public const string NotRequested = "not_requested";
    public const string Unavailable = "unavailable";
    public const string Deferred = "deferred";
    public const string Truncated = "truncated";
}

public static class ReviewPrioritySeverityHints
{
    public const string CriticalReview = "critical_review";
    public const string HighReview = "high_review";
    public const string MediumReview = "medium_review";
    public const string LowReview = "low_review";
    public const string Info = "info";
    public const string Unknown = "unknown";
}

public static class ReviewPriorityAttentionLevels
{
    public const string HighestAttention = "highest_attention";
    public const string HighAttention = "high_attention";
    public const string ModerateAttention = "moderate_attention";
    public const string LowAttention = "low_attention";
    public const string Informational = "informational";
    public const string Unknown = "unknown";
}

public static class ReviewPriorityRules
{
    public const string ModelVersion = "review-priority.v1";
    public const string Component = "review.priority.component.v1";
    public const string Aggregate = "review.priority.aggregate.v1";
    public const string Downgrade = "review.priority.downgrade.v1";
    public const string Coverage = "review.priority.coverage.v1";
    public const string Identity = "review.priority.identity.v1";
    public const string Commit = "review.priority.commit.v1";
    public const string Schema = "review.priority.schema.v1";
    public const string Truncation = "review.priority.truncation.v1";
    public const string Workflow = "review.priority.workflow.v1";
    public const string Selector = "review.priority.selector.v1";

    public static readonly IReadOnlyList<string> DefaultLimitations =
    [
        "Review priority is deterministic static review prioritization over existing TraceMap evidence.",
        "Review priority is not runtime risk prediction, production impact, release approval, vulnerability scanning, compliance, business criticality, or AI analysis.",
        "V1 review priority is ordinal-only; priorityScore is null and no numeric weights or bands are emitted."
    ];

    public static int EvidenceTierRank(string? tier)
    {
        return tier switch
        {
            EvidenceTiers.Tier1Semantic => 1,
            EvidenceTiers.Tier2Structural => 2,
            EvidenceTiers.Tier3SyntaxOrTextual => 3,
            EvidenceTiers.Tier4Unknown => 4,
            _ => 5
        };
    }

    public static int SeverityRank(string? severity)
    {
        return severity switch
        {
            ReviewPrioritySeverityHints.Unknown => 0,
            ReviewPrioritySeverityHints.CriticalReview => 1,
            ReviewPrioritySeverityHints.HighReview => 2,
            ReviewPrioritySeverityHints.MediumReview => 3,
            ReviewPrioritySeverityHints.LowReview => 4,
            ReviewPrioritySeverityHints.Info => 5,
            _ => 6
        };
    }
}
