using System.Text.Json;
using TraceMap.Core;

namespace TraceMap.Reporting;

public static partial class StaticHtmlEvidenceExplorer
{
    private const long MaxReducerImpactBytes = 33_554_432;
    private const int MaxReducerImpactSources = 1_000;
    private const int MaxReducerImpactResults = 1_000;
    private const int MaxReducerImpactEvidenceRows = 10_000;
    private const int MaxReducerImpactGaps = 1_000;

    private static readonly HashSet<string> SupportedReducerReportTypes = new(StringComparer.Ordinal)
    {
        "contract-delta-impact-single",
        "contract-delta-impact-combined"
    };

    private static readonly HashSet<string> SupportedReducerClassifications = new(StringComparer.Ordinal)
    {
        "DefiniteImpact",
        "ProbableImpact",
        "NeedsReview",
        "NoEvidenceFullCoverage",
        "NoEvidenceReducedCoverage",
        "UnknownAnalysisGap",
        "StaticImpactEvidence",
        "ProbableStaticImpact",
        "NeedsReviewImpact",
        "NoImpactEvidence",
        "SelectorNoMatch",
        "TruncatedByLimit",
        "PathContextUnavailable",
        "ReverseContextUnavailable"
    };

    private static readonly HashSet<string> SupportedReducerConfidence = new(StringComparer.Ordinal)
    {
        "high",
        "medium",
        "review",
        "coverage-relative-none",
        "unknown"
    };

    private static async Task AddReducerImpactArtifactAsync(
        string inputDirectory,
        string safetyProfile,
        string? authoritativeCommitSha,
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerSource> reportSources,
        List<ExplorerReducerResult> reducerResults,
        List<ExplorerEvidenceRow> evidenceRows,
        List<ExplorerGap> gaps,
        List<ExplorerLimitation> limitations,
        SortedSet<string> overallCoverageLabels,
        Dictionary<(string RuleId, string Category, string Location, string Action), int> redactions,
        CancellationToken cancellationToken)
    {
        const string artifactId = "artifact:reducer-impact-report";
        const string limitationId = "limitation:reducer-impact-static-coverage";
        var path = Path.Combine(inputDirectory, "impact-report.json");
        if (!File.Exists(path))
        {
            return;
        }

        var snapshot = await ReadBoundedArtifactAsync(path, MaxReducerImpactBytes, cancellationToken);
        if (snapshot.Content is null)
        {
            AddUnsupportedReducerImpactArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "artifact-too-large",
                "The reducer report exceeded the bounded reader size and was not parsed.");
            return;
        }

        try
        {
            using var document = JsonDocument.Parse(
                snapshot.Content,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 96
                });
            var root = RequireObject(document.RootElement, "root");
            RejectDuplicateJsonProperties(root);
            foreach (var required in new[] { "reportType", "version", "reportCoverage", "query", "index", "summary", "findings", "gaps", "limitations" })
            {
                _ = RequireProperty(root, required);
            }

            var report = JsonSerializer.Deserialize<ReducerImpactReportInput>(snapshot.Content, JsonOptions)
                ?? throw new InvalidDataException("reducer report unavailable");
            ValidateReducerImpactReport(report);

            var orderedSources = report.Index.Sources
                .OrderBy(source => source.SourceIndexId ?? string.Empty, StringComparer.Ordinal)
                .ThenBy(source => source.CommitSha, StringComparer.Ordinal)
                .ToArray();
            var authoritativeMatches = authoritativeCommitSha is null
                ? 0
                : orderedSources.Count(source => source.CommitSha!.Equals(authoritativeCommitSha, StringComparison.OrdinalIgnoreCase));
            var sourceByKey = new Dictionary<string, (string SourceId, ReducerImpactSourceInput Source)>(StringComparer.Ordinal);
            for (var index = 0; index < orderedSources.Length; index++)
            {
                var source = orderedSources[index];
                var key = ReducerSourceKey(source.SourceIndexId);
                var sourceId = authoritativeMatches == 1
                    && source.CommitSha!.Equals(authoritativeCommitSha, StringComparison.OrdinalIgnoreCase)
                        ? SourceId
                        : $"source:reducer:{Hash($"{key}:{source.CommitSha}", 24)}";
                sourceByKey.Add(key, (sourceId, source));
                if (sourceId != SourceId)
                {
                    reportSources.Add(new ExplorerSource(
                        sourceId,
                        $"Reducer report source {index + 1:D2}",
                        "reducer-impact-source",
                        ClaimLevelForSafetyProfile(safetyProfile),
                        report.ReportCoverage == "Full" && !report.Summary.Truncated ? "available" : "reduced",
                        source.CommitSha,
                        [SafeClosedText(source.ScannerVersion, "reducer-report.scanner-version", redactions)],
                        [artifactId],
                        report.Gaps.Count,
                        1,
                        0,
                        0));
                }
            }

            var coverageLabels = new SortedSet<string>(StringComparer.Ordinal)
            {
                report.ReportCoverage switch
                {
                    "Full" => "ReducerFullCoverage",
                    "Partial" => "ReducerPartialCoverage",
                    _ => "ReducerReducedCoverage"
                },
                report.Summary.Truncated ? "ReducerTruncated" : "ReducerNotTruncated",
                report.Gaps.Count > 0 ? "ReducerGapsPresent" : "ReducerNoRecordedGaps"
            };
            foreach (var label in coverageLabels)
            {
                overallCoverageLabels.Add(label);
            }

            artifacts.Add(new ExplorerInputArtifact(
                artifactId,
                "reducer-impact-report",
                "Contract-delta impact report",
                snapshot.ContentHash,
                "contract-delta-impact/2.0",
                ClaimLevelForSafetyProfile(safetyProfile),
                coverageLabels.ToArray(),
                sourceByKey.Values.Select(value => value.SourceId).Distinct(StringComparer.Ordinal).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                [limitationId],
                [],
                "supported"));
            limitations.Add(CreateLimitation(
                "reducer-impact-static-coverage",
                ReducerImpactInputRuleId,
                "static-impact-coverage-relative",
                "reducer-results",
                "impact",
                "Reducer-backed impact classifications describe bounded static evidence under recorded coverage. They do not prove runtime reachability, production behavior, business impact, release safety, or complete dependency coverage.",
                [artifactId]));

            foreach (var finding in report.Findings.OrderBy(item => item.FindingId, StringComparer.Ordinal))
            {
                var resultId = $"result:{Hash(finding.FindingId!, 24)}";
                var resultEvidenceIds = new List<string>();
                var resultSupportIds = SafeSupportIds(finding.Evidence.Select(row => row.FactId!));
                for (var index = 0; index < finding.Evidence.Count; index++)
                {
                    var evidence = finding.Evidence[index];
                    var sourceEntry = sourceByKey[ReducerSourceKey(evidence.SourceIndexId)];
                    var evidenceId = $"evidence:{Hash($"{finding.FindingId}:{index}:{evidence.FactId}", 24)}";
                    resultEvidenceIds.Add(evidenceId);
                    evidenceRows.Add(new ExplorerEvidenceRow(
                        evidenceId,
                        evidence.RuleId!,
                        evidence.EvidenceTier!,
                        "reducer-impact-evidence",
                        $"support:{Hash(evidence.FactId!, 24)}",
                        artifactId,
                        sourceEntry.SourceId,
                        evidence.CommitSha,
                        SafeRepositoryPath(evidence.FilePath!, redactions),
                        evidence.StartLine,
                        evidence.EndLine,
                        null,
                        report.ReportCoverage,
                        SafeClosedText(sourceEntry.Source.ScannerVersion, "reducer-report.scanner-version", redactions),
                        [limitationId]));
                }

                reducerResults.Add(new ExplorerReducerResult(
                    resultId,
                    finding.Classification!,
                    finding.Confidence!,
                    finding.RuleId!,
                    finding.EvidenceTier!,
                    $"{report.Query.Algorithm}/{report.Query.AlgorithmVersion}",
                    report.ReportCoverage!,
                    artifactId,
                    resultEvidenceIds.OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    resultSupportIds.Count == 0 ? [artifactId] : resultSupportIds,
                    [limitationId]));
                AddRedaction(redactions, OmittedUnsafeValueRuleId, "reducer-free-text", "impact-report.findings", "omit");
            }

            foreach (var reportGap in report.Gaps.OrderBy(item => item.GapId, StringComparer.Ordinal))
            {
                gaps.Add(CreateGap(
                    $"reducer-impact-{Hash(reportGap.GapId!, 16)}",
                    reportGap.RuleId!,
                    "reducer-impact-gap",
                    artifactId,
                    "reducer-results",
                    report.ReportCoverage!,
                    "The reducer report preserved a rule-backed impact-analysis gap; the explorer does not reinterpret its free-text message.",
                    SafeSupportIds(reportGap.SupportingFactIds).Append(artifactId).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
                    reportGap.EvidenceTier!));
            }

            if (report.Limitations.Count > 0 || report.CoverageWarnings.Count > 0)
            {
                AddRedaction(redactions, OmittedUnsafeValueRuleId, "reducer-free-text", "impact-report.limitations", "omit");
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidDataException)
        {
            AddUnsupportedReducerImpactArtifact(
                artifacts,
                gaps,
                safetyProfile,
                snapshot.ContentHash,
                "unsupported-schema",
                "The reducer report did not match the supported contract-delta impact v2 contract and was not rendered.");
        }
    }

    private static void ValidateReducerImpactReport(ReducerImpactReportInput report)
    {
        if (!SupportedReducerReportTypes.Contains(report.ReportType ?? string.Empty)
            || report.Version != "2.0"
            || report.ReportCoverage is not ("Full" or "Reduced" or "Partial")
            || report.CoverageWarnings is null
            || report.Query is null
            || report.Query.Algorithm != "contract-delta-fact-match"
            || report.Query.AlgorithmVersion != "2.0"
            || report.Index is null
            || report.Index.Sources is null
            || report.Index.SourceCount != report.Index.Sources.Count
            || report.Index.SourceCount <= 0
            || report.Index.SourceCount > MaxReducerImpactSources
            || report.Summary is null
            || report.Findings is null
            || report.Gaps is null
            || report.Limitations is null
            || report.Findings.Count > MaxReducerImpactResults
            || report.Findings.Sum(finding => finding?.Evidence?.Count ?? 0) > MaxReducerImpactEvidenceRows
            || report.Gaps.Count > MaxReducerImpactGaps
            || report.Summary.FindingCount != report.Findings.Count
            || report.Summary.EvidenceRowCount != report.Findings.Sum(finding => finding?.Evidence?.Count ?? 0)
            || report.Summary.GapCount != report.Gaps.Count)
        {
            throw new InvalidDataException("reducer report contract mismatch");
        }

        var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var commitBySourceKey = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var source in report.Index.Sources)
        {
            var key = ReducerSourceKey(source?.SourceIndexId);
            if (source is null
                || !sourceKeys.Add(key)
                || string.IsNullOrWhiteSpace(source.ScanId)
                || !IsUsableCommitSha(source.CommitSha)
                || string.IsNullOrWhiteSpace(source.ScannerVersion))
            {
                throw new InvalidDataException("reducer report source identity unavailable");
            }

            commitBySourceKey.Add(key, source.CommitSha!);
        }

        var findingIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var finding in report.Findings)
        {
            if (finding is null
                || string.IsNullOrWhiteSpace(finding.FindingId)
                || !findingIds.Add(finding.FindingId)
                || !SupportedReducerClassifications.Contains(finding.Classification ?? string.Empty)
                || !SupportedReducerConfidence.Contains(finding.Confidence ?? string.Empty)
                || !IsSafeRuleId(finding.RuleId)
                || !IsSupportedEvidenceTier(finding.EvidenceTier)
                || finding.Evidence is null
                || finding.Limitations is null
                || IsActionableReducerClassification(finding.Classification!) && finding.Evidence.Count == 0)
            {
                throw new InvalidDataException("reducer report finding evidence unavailable");
            }

            foreach (var evidence in finding.Evidence)
            {
                var sourceKey = ReducerSourceKey(evidence?.SourceIndexId);
                if (evidence is null
                    || string.IsNullOrWhiteSpace(evidence.FactId)
                    || string.IsNullOrWhiteSpace(evidence.FactType)
                    || !IsSafeRuleId(evidence.RuleId)
                    || !IsSupportedEvidenceTier(evidence.EvidenceTier)
                    || string.IsNullOrWhiteSpace(evidence.FilePath)
                    || !IsValidSpan(evidence.StartLine, evidence.EndLine)
                    || !sourceKeys.Contains(sourceKey)
                    || !IsUsableCommitSha(evidence.CommitSha)
                    || !commitBySourceKey[sourceKey].Equals(evidence.CommitSha, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidDataException("reducer report evidence provenance unavailable");
                }
            }
        }

        var gapIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var gap in report.Gaps)
        {
            if (gap is null
                || string.IsNullOrWhiteSpace(gap.GapId)
                || !gapIds.Add(gap.GapId)
                || !IsSafeRuleId(gap.RuleId)
                || !IsSupportedEvidenceTier(gap.EvidenceTier)
                || gap.SupportingFactIds is null)
            {
                throw new InvalidDataException("reducer report gap evidence unavailable");
            }
        }
    }

    private static bool IsActionableReducerClassification(string classification)
    {
        return classification is "DefiniteImpact" or "ProbableImpact" or "NeedsReview" or "StaticImpactEvidence" or "ProbableStaticImpact" or "NeedsReviewImpact";
    }

    private static string ReducerSourceKey(string? sourceIndexId)
    {
        return string.IsNullOrWhiteSpace(sourceIndexId) ? "single-source" : sourceIndexId;
    }

    private static void AddUnsupportedReducerImpactArtifact(
        List<ExplorerInputArtifact> artifacts,
        List<ExplorerGap> gaps,
        string safetyProfile,
        string contentHash,
        string gapKind,
        string message)
    {
        const string artifactId = "artifact:reducer-impact-report";
        artifacts.Add(new ExplorerInputArtifact(
            artifactId,
            "reducer-impact-report",
            "Contract-delta impact report",
            contentHash,
            "unsupported",
            ClaimLevelForSafetyProfile(safetyProfile),
            ["UnknownCoverage"],
            [],
            [],
            [UnsupportedSchemaRuleId],
            "unsupported"));
        gaps.Add(CreateGap(
            "reducer-impact-unsupported",
            UnsupportedSchemaRuleId,
            gapKind,
            artifactId,
            "reducer-results",
            "PartialAnalysis",
            message,
            [artifactId]));
    }

    private sealed record ReducerImpactReportInput(
        string? ReportType,
        string? Version,
        string? ReportCoverage,
        IReadOnlyList<string> CoverageWarnings,
        ReducerImpactQueryInput Query,
        ReducerImpactIndexInput Index,
        ReducerImpactSummaryInput Summary,
        IReadOnlyList<ReducerImpactFindingInput> Findings,
        IReadOnlyList<ReducerImpactGapInput> Gaps,
        IReadOnlyList<string> Limitations);

    private sealed record ReducerImpactQueryInput(string? Algorithm, string? AlgorithmVersion);

    private sealed record ReducerImpactIndexInput(int SourceCount, IReadOnlyList<ReducerImpactSourceInput> Sources);

    private sealed record ReducerImpactSourceInput(
        string? SourceIndexId,
        string? ScanId,
        string? CommitSha,
        string? ScannerVersion);

    private sealed record ReducerImpactSummaryInput(int FindingCount, int EvidenceRowCount, int GapCount, bool Truncated);

    private sealed record ReducerImpactFindingInput(
        string? FindingId,
        string? Classification,
        string? RuleId,
        string? Confidence,
        string? EvidenceTier,
        IReadOnlyList<ReducerImpactEvidenceInput> Evidence,
        IReadOnlyList<string> Limitations);

    private sealed record ReducerImpactEvidenceInput(
        string? FactId,
        string? FactType,
        string? RuleId,
        string? EvidenceTier,
        string? FilePath,
        int StartLine,
        int EndLine,
        string? CommitSha,
        string? SourceIndexId);

    private sealed record ReducerImpactGapInput(
        string? GapId,
        string? RuleId,
        string? EvidenceTier,
        IReadOnlyList<string> SupportingFactIds);
}
