using TraceMap.Core;

namespace TraceMap.Reporting.ReviewPriority;

public sealed record ReleaseReviewPriorityResult(
    ReviewPrioritySummary Summary,
    IReadOnlyList<ReviewPriorityRow> Rows);

public static class ReleaseReviewPriorityScorer
{
    private static readonly HashSet<string> StrongStaticClassifications = new(StringComparer.Ordinal)
    {
        ReleaseReviewClassifications.ActionableStaticEvidence,
        "DefiniteImpact",
        "ProbableImpact",
        CombinedImpactClassifications.StaticImpactEvidence,
        CombinedImpactClassifications.ProbableStaticImpact,
        CombinedDependencyDiffClassifications.Added,
        CombinedDependencyDiffClassifications.Removed,
        CombinedDependencyDiffClassifications.ChangedEvidence
    };

    public static ReleaseReviewPriorityResult Score(ReleaseReviewDocument report)
    {
        var sectionStatuses = SectionStatuses(report);
        var findings = Findings(report).ToArray();
        var gaps = report.Gaps.OrderBy(gap => gap.GapId, StringComparer.Ordinal).ToArray();
        var rows = new List<ReviewPriorityRow>();

        foreach (var finding in findings)
        {
            rows.Add(ScoreFinding(report, finding, sectionStatuses.GetValueOrDefault(finding.Section, ReviewPriorityStatuses.Available)));
        }

        foreach (var gap in gaps)
        {
            rows.Add(ScoreGap(report, gap, sectionStatuses.GetValueOrDefault(gap.Section, ReviewPriorityStatuses.Available)));
        }

        foreach (var item in report.ReviewerChecklist.OrderBy(item => item.ChecklistId, StringComparer.Ordinal))
        {
            rows.Add(ScoreChecklistItem(report, item, findings, gaps));
        }

        var sortedRows = rows
            .OrderBy(row => row.Complete)
            .ThenBy(row => ReviewPriorityRules.SeverityRank(row.SeverityHint))
            .ThenBy(row => ClassificationRank(row.Classification))
            .ThenBy(row => ReviewPriorityRules.EvidenceTierRank(row.EvidenceTier))
            .ThenBy(row => row.SourceLabel ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(row => row.Section, StringComparer.Ordinal)
            .ThenBy(row => FirstEvidencePath(row), StringComparer.Ordinal)
            .ThenBy(row => FirstEvidenceStartLine(row))
            .ThenBy(row => row.RowId, StringComparer.Ordinal)
            .ToArray();

        var limitedSections = LimitedSections(report, sortedRows, sectionStatuses);
        var contributingSections = sortedRows
            .Select(row => row.Section)
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(section => section, StringComparer.Ordinal)
            .ToArray();

        var status = report.Summary.Truncated
            || sectionStatuses.Values.Any(sectionStatus => sectionStatus == ReleaseReviewStatuses.Truncated)
            || sortedRows.Any(row => row.Components.Any(component => component.RuleId == ReviewPriorityRules.Truncation))
            ? ReviewPriorityStatuses.Truncated
            : ReviewPriorityStatuses.Available;
        var complete = status == ReviewPriorityStatuses.Available
            && sortedRows.All(row => row.Complete)
            && limitedSections.Count == 0;
        var attentionLevel = AttentionLevel(sortedRows, complete);
        var summaryComponents = SummaryComponents(report, sortedRows, sectionStatuses, attentionLevel, complete);
        var limitations = ReviewPriorityRules.DefaultLimitations
            .Concat(sortedRows.SelectMany(row => row.Limitations))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var summary = new ReviewPrioritySummary(
            status,
            ReviewPriorityRules.ModelVersion,
            attentionLevel,
            null,
            complete,
            contributingSections,
            limitedSections,
            summaryComponents,
            limitations);
        return new ReleaseReviewPriorityResult(summary, sortedRows);
    }

    private static ReviewPriorityRow ScoreFinding(ReleaseReviewDocument report, ReleaseReviewFinding finding, string sectionStatus)
    {
        var evidence = FindingEvidence(finding);
        var components = new List<ReviewPriorityComponent>
        {
            Component(
                "static_change_evidence",
                "increase",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                $"Preserves release-review classification `{finding.Classification}`; this is static review priority, not runtime impact.",
                [Pair("classification", finding.Classification), Pair("section", finding.Section)])
        };

        if (finding.Classification == ReleaseReviewClassifications.NoActionableEvidence)
        {
            components.Add(Component(
                "no_actionable_evidence",
                "decrease",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                "No-actionable-evidence rows are informational only under credible requested coverage and do not prove runtime absence.",
                [Pair("classification", finding.Classification), Pair("section", finding.Section)]));
        }

        if (StrongStaticClassifications.Contains(finding.Classification)
            && finding.EvidenceTier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural)
        {
            components.Add(Component(
                "evidence_tier_strength",
                "increase",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                "Tier1/Tier2 static evidence can raise review priority only when coverage and identity gaps do not cap it.",
                [Pair("candidateSeverity", ReviewPrioritySeverityHints.HighReview)]));
        }

        if (PublicSurfaceSection(finding.Section))
        {
            components.Add(Component(
                "public_surface",
                "increase",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                "Public-surface priority comes only from existing release-review section evidence and does not prove runtime exposure.",
                [Pair("section", finding.Section)]));
        }

        if (HasCrossSourceEvidence(finding))
        {
            components.Add(Component(
                "cross_repo_reach",
                "increase",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                "Cross-source reach uses explicit release-review row metadata only and does not infer ownership, deployment, topology, or business reach.",
                [Pair("sourceCount", CrossSourceCount(finding).ToString())]));
        }

        if (finding.Section is "pathContext" or "reverseContext")
        {
            components.Add(Component(
                finding.Section == "pathContext" ? "path_context" : "reverse_context",
                "increase",
                ReviewPriorityRules.Component,
                finding.EvidenceTier,
                evidence,
                "Path and reverse context are bounded static evidence and not complete runtime reachability or usage.",
                [Pair("section", finding.Section)]));
        }

        AddDowngradeComponents(components, finding, evidence, sectionStatus);

        var severity = SeverityFromComponents(finding.Classification, finding.EvidenceTier, components);
        var complete = !components.Any(component => component.Direction == "unknown");
        return new ReviewPriorityRow(
            finding.FindingId,
            "finding",
            finding.Section,
            finding.SourceLabel,
            finding.Classification,
            finding.RuleId,
            finding.EvidenceTier,
            severity,
            null,
            complete,
            SortComponents(components),
            [finding.FindingId],
            RowLimitations(components, finding.Limitations));
    }

    private static ReviewPriorityRow ScoreGap(ReleaseReviewDocument report, ReleaseReviewGap gap, string sectionStatus)
    {
        var evidence = GapEvidence(gap);
        var ruleId = GapRuleId(gap, sectionStatus);
        var selectorNoMatchCredible = gap.Classification == ReleaseReviewClassifications.SelectorNoMatch
            && report.Summary.RollupClassification == ReleaseReviewClassifications.SelectorNoMatch
            && sectionStatus == ReleaseReviewStatuses.Available;
        var kind = ruleId == ReviewPriorityRules.Selector
            ? selectorNoMatchCredible ? "selector_no_match_credible" : "selector_no_match_uncertain"
            : GapComponentKind(gap, sectionStatus);
        var direction = selectorNoMatchCredible
            ? "decrease"
            : "unknown";
        var limitation = gap.Classification == ReleaseReviewClassifications.SelectorNoMatch
            ? "Selector no-match is informational only when coverage and source identity are credible; otherwise gaps keep priority uncertain."
            : "Analysis gaps preserve uncertainty and must not be read as evidence absence or release approval.";
        var components = new List<ReviewPriorityComponent>
        {
            Component(
                kind,
                direction,
                ruleId,
                gap.EvidenceTier,
                evidence,
                limitation,
                [Pair("classification", gap.Classification), Pair("gapKind", gap.GapKind), Pair("sectionStatus", sectionStatus)])
        };

        if (sectionStatus is ReleaseReviewStatuses.Unavailable or ReleaseReviewStatuses.Deferred)
        {
            components.Add(Component(
                "unavailable_workflow",
                "unknown",
                ReviewPriorityRules.Workflow,
                EvidenceTiers.Tier4Unknown,
                evidence,
                "Requested or represented workflow evidence was unavailable or deferred, so scoring records uncertainty instead of treating the section as low priority.",
                [Pair("sectionStatus", sectionStatus)]));
        }

        var severity = direction == "decrease"
            ? ReviewPrioritySeverityHints.Info
            : ReviewPrioritySeverityHints.Unknown;
        return new ReviewPriorityRow(
            gap.GapId,
            "gap",
            gap.Section,
            gap.SourceLabel,
            gap.Classification,
            gap.RuleId,
            gap.EvidenceTier,
            severity,
            null,
            direction == "decrease",
            SortComponents(components),
            [gap.GapId],
            RowLimitations(components, [gap.Message]));
    }

    private static ReviewPriorityRow ScoreChecklistItem(
        ReleaseReviewDocument report,
        ReleaseReviewChecklistItem item,
        IReadOnlyList<ReleaseReviewFinding> findings,
        IReadOnlyList<ReleaseReviewGap> gaps)
    {
        var sourceFindings = item.FindingIds
            .Select(id => findings.FirstOrDefault(finding => finding.FindingId == id))
            .Where(finding => finding is not null)
            .Select(finding => finding!)
            .ToArray();
        var sourceGaps = item.GapIds
            .Select(id => gaps.FirstOrDefault(gap => gap.GapId == id))
            .Where(gap => gap is not null)
            .Select(gap => gap!)
            .ToArray();
        var evidence = sourceFindings.Select(FindingEvidence)
            .Concat(sourceGaps.Select(GapEvidence))
            .SelectMany(value => value)
            .OrderBy(value => value.EvidenceId, StringComparer.Ordinal)
            .ToArray();
        if (evidence.Length == 0)
        {
            evidence =
            [
                new ReviewPriorityEvidenceRef(
                    item.ChecklistId,
                    "releaseReviewChecklistItem",
                    item.RuleId,
                    EvidenceTiers.Tier4Unknown,
                    null,
                    null,
                    null,
                    null,
                    null)
            ];
        }

        var tier = evidence
            .OrderByDescending(value => ReviewPriorityRules.EvidenceTierRank(value.EvidenceTier))
            .FirstOrDefault()?.EvidenceTier ?? EvidenceTiers.Tier4Unknown;
        var direction = item.Severity == "informational" ? "decrease" : "increase";
        var components = new List<ReviewPriorityComponent>
        {
            Component(
                "checklist_attention",
                direction,
                ReviewPriorityRules.Component,
                tier,
                evidence,
                "Checklist priority is derived from referenced release-review findings or gaps; it does not add a stronger conclusion.",
                [Pair("checklistSeverity", item.Severity), Pair("section", item.Section)])
        };

        var severity = item.Severity switch
        {
            "must_review" => ReviewPrioritySeverityHints.HighReview,
            "should_review" => ReviewPrioritySeverityHints.MediumReview,
            "informational" => ReviewPrioritySeverityHints.Info,
            _ => ReviewPrioritySeverityHints.Info
        };
        if (tier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown && severity is ReviewPrioritySeverityHints.HighReview)
        {
            components.Add(Component(
                "review_tier_evidence",
                "cap",
                ReviewPriorityRules.Downgrade,
                tier,
                evidence,
                "Tier3 or unknown checklist evidence is capped at review-tier priority.",
                [Pair("capSeverity", ReviewPrioritySeverityHints.MediumReview)]));
            severity = ReviewPrioritySeverityHints.MediumReview;
        }

        var complete = !components.Any(component => component.Direction == "unknown")
            && severity != ReviewPrioritySeverityHints.Unknown;
        return new ReviewPriorityRow(
            item.ChecklistId,
            "checklist",
            item.Section,
            null,
            item.Severity,
            item.RuleId,
            tier,
            severity,
            null,
            complete,
            SortComponents(components),
            item.FindingIds.Concat(item.GapIds).OrderBy(value => value, StringComparer.Ordinal).ToArray(),
            RowLimitations(components, [item.Text]));
    }

    private static void AddDowngradeComponents(
        List<ReviewPriorityComponent> components,
        ReleaseReviewFinding finding,
        IReadOnlyList<ReviewPriorityEvidenceRef> evidence,
        string sectionStatus)
    {
        if (finding.EvidenceTier is EvidenceTiers.Tier3SyntaxOrTextual or EvidenceTiers.Tier4Unknown)
        {
            components.Add(Component(
                "review_tier_evidence",
                "cap",
                ReviewPriorityRules.Downgrade,
                finding.EvidenceTier,
                evidence,
                "Tier3, syntax/textual, fallback, hash-only, or unknown evidence is capped at medium_review in v1.",
                [Pair("capSeverity", ReviewPrioritySeverityHints.MediumReview)]));
        }

        if (finding.Metadata.Any(pair => pair.Key == "coverageRelative" && pair.Value == "true"))
        {
            components.Add(Component(
                "coverage_gap",
                "cap",
                ReviewPriorityRules.Coverage,
                EvidenceTiers.Tier4Unknown,
                evidence,
                "Coverage-relative evidence remains review attention and is capped at medium_review.",
                [Pair("capSeverity", ReviewPrioritySeverityHints.MediumReview)]));
        }

        if (string.IsNullOrWhiteSpace(finding.CommitSha) || finding.CommitSha.Equals("unknown", StringComparison.OrdinalIgnoreCase))
        {
            components.Add(Component(
                "commit_gap",
                "unknown",
                ReviewPriorityRules.Commit,
                EvidenceTiers.Tier4Unknown,
                evidence,
                "Missing commit SHA prevents history-completeness claims for this row.",
                []));
        }

        if (sectionStatus == ReleaseReviewStatuses.Truncated)
        {
            components.Add(Component(
                "truncation",
                "unknown",
                ReviewPriorityRules.Truncation,
                EvidenceTiers.Tier4Unknown,
                evidence,
                "Section truncation means scoring may not have inspected all relevant rows.",
                [Pair("sectionStatus", sectionStatus)]));
        }
    }

    private static string SeverityFromComponents(string classification, string evidenceTier, IReadOnlyList<ReviewPriorityComponent> components)
    {
        if (components.Any(component => component.Direction == "unknown"))
        {
            return ReviewPrioritySeverityHints.Unknown;
        }

        if (classification == ReleaseReviewClassifications.NoActionableEvidence)
        {
            return ReviewPrioritySeverityHints.Info;
        }

        var severity = StrongStaticClassifications.Contains(classification)
            && evidenceTier is EvidenceTiers.Tier1Semantic or EvidenceTiers.Tier2Structural
            ? ReviewPrioritySeverityHints.HighReview
            : classification switch
            {
                ReleaseReviewClassifications.ReviewRecommended => ReviewPrioritySeverityHints.MediumReview,
                ReleaseReviewClassifications.PartialAnalysis => ReviewPrioritySeverityHints.MediumReview,
                ReleaseReviewClassifications.SelectorNoMatch => ReviewPrioritySeverityHints.Info,
                ReleaseReviewClassifications.NoActionableEvidence => ReviewPrioritySeverityHints.Info,
                _ => ReviewPrioritySeverityHints.MediumReview
            };

        if (components.Any(component => component.Direction == "cap")
            && ReviewPriorityRules.SeverityRank(severity) < ReviewPriorityRules.SeverityRank(ReviewPrioritySeverityHints.MediumReview))
        {
            // Caps are ceilings: they can lower stronger static evidence to review-tier,
            // but they never raise already-low or informational evidence.
            return ReviewPrioritySeverityHints.MediumReview;
        }

        return severity;
    }

    private static ReviewPriorityComponent[] SummaryComponents(
        ReleaseReviewDocument report,
        IReadOnlyList<ReviewPriorityRow> rows,
        IReadOnlyDictionary<string, string> sectionStatuses,
        string attentionLevel,
        bool complete)
    {
        ReviewPriorityEvidenceRef[] evidence =
        [
            new ReviewPriorityEvidenceRef(
                "release-review:summary",
                "releaseReviewSummary",
                report.Summary.RuleId,
                EvidenceTiers.Tier2Structural,
                null,
                null,
                null,
                null,
                null)
        ];
        var components = new List<ReviewPriorityComponent>
        {
            Component(
                "report_aggregation",
                complete ? "increase" : "unknown",
                ReviewPriorityRules.Aggregate,
                complete ? EvidenceTiers.Tier2Structural : EvidenceTiers.Tier4Unknown,
                evidence,
                "Report attention level is derived deterministically from row severity, completeness, section status, and truncation.",
                [
                    Pair("attentionLevel", attentionLevel),
                    Pair("rowCount", rows.Count.ToString()),
                    Pair("rollupClassification", report.Summary.RollupClassification)
                ])
        };

        foreach (var sectionStatus in sectionStatuses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (sectionStatus.Value is ReleaseReviewStatuses.Unavailable or ReleaseReviewStatuses.Deferred)
            {
                components.Add(Component(
                    "unavailable_workflow",
                    "unknown",
                    ReviewPriorityRules.Workflow,
                    EvidenceTiers.Tier4Unknown,
                    evidence,
                    "Unavailable or deferred release-review sections contribute report-level review uncertainty.",
                    [Pair("section", sectionStatus.Key), Pair("sectionStatus", sectionStatus.Value)]));
            }
            else if (sectionStatus.Value == ReleaseReviewStatuses.Truncated)
            {
                components.Add(Component(
                    "truncation",
                    "unknown",
                    ReviewPriorityRules.Truncation,
                    EvidenceTiers.Tier4Unknown,
                    evidence,
                    "Truncated release-review sections make report-level priority incomplete.",
                    [Pair("section", sectionStatus.Key), Pair("sectionStatus", sectionStatus.Value)]));
            }
        }

        return SortComponents(components);
    }

    private static string AttentionLevel(IReadOnlyList<ReviewPriorityRow> rows, bool complete)
    {
        if (!complete)
        {
            return ReviewPriorityAttentionLevels.Unknown;
        }

        if (rows.Any(row => row.SeverityHint == ReviewPrioritySeverityHints.CriticalReview))
        {
            return ReviewPriorityAttentionLevels.HighestAttention;
        }

        if (rows.Any(row => row.SeverityHint == ReviewPrioritySeverityHints.HighReview))
        {
            return ReviewPriorityAttentionLevels.HighAttention;
        }

        if (rows.Any(row => row.SeverityHint == ReviewPrioritySeverityHints.MediumReview))
        {
            return ReviewPriorityAttentionLevels.ModerateAttention;
        }

        if (rows.Any(row => row.SeverityHint == ReviewPrioritySeverityHints.LowReview))
        {
            return ReviewPriorityAttentionLevels.LowAttention;
        }

        return ReviewPriorityAttentionLevels.Informational;
    }

    private static IReadOnlyList<string> LimitedSections(
        ReleaseReviewDocument report,
        IReadOnlyList<ReviewPriorityRow> rows,
        IReadOnlyDictionary<string, string> sectionStatuses)
    {
        return rows
            .Where(row => !row.Complete)
            .Select(row => row.Section)
            .Concat(sectionStatuses.Where(pair => pair.Value is ReleaseReviewStatuses.Unavailable or ReleaseReviewStatuses.Deferred or ReleaseReviewStatuses.Truncated).Select(pair => pair.Key))
            .Concat(report.SourceCoverage.Where(source => source.GapIds.Count > 0).Select(_ => "sourceCoverage"))
            .Where(section => !string.IsNullOrWhiteSpace(section))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(section => section, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, string> SectionStatuses(ReleaseReviewDocument report)
    {
        return new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["sourceCoverage"] = ReleaseReviewStatuses.Available,
            ["topChangedSurfaces"] = report.TopChangedSurfaces.Status,
            ["contractImpact"] = report.ContractImpact.Status,
            ["apiDtoChanges"] = report.ApiDtoChanges.Status,
            ["sqlSchemaImpact"] = report.SqlSchemaImpact.Status,
            ["sqlEvidence"] = report.SqlEvidence.Status,
            ["accessEvidence"] = report.AccessEvidence.Status,
            ["packageImpact"] = report.PackageImpact.Status,
            ["pathContext"] = report.PathContext.Status,
            ["reverseContext"] = report.ReverseContext.Status
        };
    }

    private static IEnumerable<ReleaseReviewFinding> Findings(ReleaseReviewDocument report)
    {
        return new[]
            {
                report.TopChangedSurfaces,
                report.ContractImpact,
                report.ApiDtoChanges,
                report.SqlSchemaImpact,
                report.SqlEvidence,
                report.AccessEvidence,
                report.PackageImpact,
                report.PathContext,
                report.ReverseContext
            }
            .SelectMany(section => section.Findings)
            .OrderBy(finding => finding.FindingId, StringComparer.Ordinal);
    }

    private static IReadOnlyList<ReviewPriorityEvidenceRef> FindingEvidence(ReleaseReviewFinding finding)
    {
        return
        [
            new ReviewPriorityEvidenceRef(
                finding.FindingId,
                "releaseReviewFinding",
                finding.RuleId,
                finding.EvidenceTier,
                finding.SourceLabel,
                finding.CommitSha,
                finding.FilePath is null ? null : CombinedReportHelpers.SafePath(finding.FilePath),
                finding.StartLine,
                finding.EndLine)
        ];
    }

    private static IReadOnlyList<ReviewPriorityEvidenceRef> GapEvidence(ReleaseReviewGap gap)
    {
        return
        [
            new ReviewPriorityEvidenceRef(
                gap.GapId,
                "releaseReviewGap",
                gap.RuleId,
                gap.EvidenceTier,
                gap.SourceLabel,
                null,
                null,
                null,
                null)
        ];
    }

    private static ReviewPriorityComponent Component(
        string kind,
        string direction,
        string ruleId,
        string tier,
        IReadOnlyList<ReviewPriorityEvidenceRef> evidence,
        string limitation,
        IEnumerable<KeyValuePair<string, string?>> metadata)
    {
        var safeMetadata = CombinedReportHelpers.SortedMetadata(metadata);
        var componentInput = string.Join(
            "|",
            kind,
            direction,
            ruleId,
            tier,
            string.Join(",", evidence.Select(item => item.EvidenceId).OrderBy(value => value, StringComparer.Ordinal)),
            string.Join(",", safeMetadata.Select(pair => $"{pair.Key}={pair.Value}")));
        return new ReviewPriorityComponent(
            $"review-priority:{CombinedReportHelpers.Hash(componentInput, 16)}",
            kind,
            null,
            direction,
            ruleId,
            tier,
            evidence
                .OrderBy(value => value.EvidenceId, StringComparer.Ordinal)
                .ThenBy(value => value.RuleId, StringComparer.Ordinal)
                .ToArray(),
            [limitation],
            safeMetadata);
    }

    private static ReviewPriorityComponent[] SortComponents(IEnumerable<ReviewPriorityComponent> components)
    {
        return components
            .OrderBy(component => ComponentKindRank(component.ComponentKind))
            .ThenBy(component => component.Direction, StringComparer.Ordinal)
            .ThenBy(component => component.RuleId, StringComparer.Ordinal)
            .ThenBy(component => ReviewPriorityRules.EvidenceTierRank(component.EvidenceTier))
            .ThenBy(component => component.SourceEvidence.FirstOrDefault()?.EvidenceId ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(component => component.Metadata.FirstOrDefault().Key ?? string.Empty, StringComparer.Ordinal)
            .ThenBy(component => component.ComponentId, StringComparer.Ordinal)
            .ToArray();
    }

    private static IReadOnlyList<string> RowLimitations(
        IReadOnlyList<ReviewPriorityComponent> components,
        IEnumerable<string> sourceLimitations)
    {
        return ReviewPriorityRules.DefaultLimitations
            .Concat(sourceLimitations)
            .Concat(components.SelectMany(component => component.Limitations))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static string GapRuleId(ReleaseReviewGap gap, string sectionStatus)
    {
        if (gap.GapKind.Contains("Coverage", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewPriorityRules.Coverage;
        }

        if (gap.GapKind.Contains("Identity", StringComparison.OrdinalIgnoreCase) || gap.GapKind.Contains("SourceOnly", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewPriorityRules.Identity;
        }

        if (gap.GapKind.Contains("Commit", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewPriorityRules.Commit;
        }

        if (gap.GapKind.Contains("Schema", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewPriorityRules.Schema;
        }

        if (gap.GapKind.Contains("Truncated", StringComparison.OrdinalIgnoreCase))
        {
            return ReviewPriorityRules.Truncation;
        }

        if (gap.GapKind.Contains("Selector", StringComparison.OrdinalIgnoreCase) || gap.Classification == ReleaseReviewClassifications.SelectorNoMatch)
        {
            return ReviewPriorityRules.Selector;
        }

        if (sectionStatus is ReleaseReviewStatuses.Unavailable or ReleaseReviewStatuses.Deferred)
        {
            return ReviewPriorityRules.Workflow;
        }

        return ReviewPriorityRules.Downgrade;
    }

    private static string GapComponentKind(ReleaseReviewGap gap, string sectionStatus)
    {
        var ruleId = GapRuleId(gap, sectionStatus);
        return ruleId switch
        {
            ReviewPriorityRules.Coverage => "coverage_gap",
            ReviewPriorityRules.Identity => "identity_gap",
            ReviewPriorityRules.Commit => "commit_gap",
            ReviewPriorityRules.Schema => "schema_gap",
            ReviewPriorityRules.Truncation => "truncation",
            ReviewPriorityRules.Selector => gap.Classification == ReleaseReviewClassifications.SelectorNoMatch ? "selector_no_match_credible" : "selector_no_match_uncertain",
            ReviewPriorityRules.Workflow => "unavailable_workflow",
            _ => "analysis_gap"
        };
    }

    private static bool PublicSurfaceSection(string section)
    {
        return section is "topChangedSurfaces"
            or "contractImpact"
            or "apiDtoChanges"
            or "sqlSchemaImpact"
            or "packageImpact";
    }

    private static int ClassificationRank(string classification)
    {
        return classification switch
        {
            ReleaseReviewClassifications.UnknownAnalysisGap => 0,
            ReleaseReviewClassifications.TruncatedByLimit => 1,
            ReleaseReviewClassifications.ActionableStaticEvidence => 2,
            "DefiniteImpact" => 2,
            "ProbableImpact" => 3,
            CombinedImpactClassifications.StaticImpactEvidence => 2,
            CombinedImpactClassifications.ProbableStaticImpact => 3,
            CombinedDependencyDiffClassifications.Added => 3,
            CombinedDependencyDiffClassifications.Removed => 3,
            CombinedDependencyDiffClassifications.ChangedEvidence => 3,
            ReleaseReviewClassifications.ReviewRecommended => 4,
            ReleaseReviewClassifications.PartialAnalysis => 5,
            ReleaseReviewClassifications.SelectorNoMatch => 6,
            ReleaseReviewClassifications.NoActionableEvidence => 7,
            _ => 8
        };
    }

    private static int ComponentKindRank(string kind)
    {
        return kind switch
        {
            "report_aggregation" => 0,
            "static_change_evidence" => 1,
            "evidence_tier_strength" => 2,
            "public_surface" => 3,
            "cross_repo_reach" => 4,
            "fan_out" => 5,
            "path_context" => 6,
            "reverse_context" => 7,
            "review_tier_evidence" => 8,
            "coverage_gap" => 9,
            "identity_gap" => 10,
            "commit_gap" => 11,
            "schema_gap" => 12,
            "unavailable_workflow" => 13,
            "truncation" => 14,
            "selector_no_match_credible" => 15,
            "selector_no_match_uncertain" => 16,
            "checklist_attention" => 17,
            "no_actionable_evidence" => 18,
            "analysis_gap" => 19,
            _ => 99
        };
    }

    private static string FirstEvidencePath(ReviewPriorityRow row)
    {
        return row.Components
            .SelectMany(component => component.SourceEvidence)
            .Select(evidence => evidence.FilePath ?? string.Empty)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }

    private static int FirstEvidenceStartLine(ReviewPriorityRow row)
    {
        return row.Components
            .SelectMany(component => component.SourceEvidence)
            .Select(evidence => evidence.StartLine)
            .FirstOrDefault(value => value.HasValue) ?? 0;
    }

    private static bool HasCrossSourceEvidence(ReleaseReviewFinding finding)
    {
        return CrossSourceCount(finding) > 1;
    }

    private static int CrossSourceCount(ReleaseReviewFinding finding)
    {
        foreach (var key in new[] { "sourceCount", "affectedSourceCount", "pathSourceCount", "beforeSourceCount", "afterSourceCount" })
        {
            var value = finding.Metadata.FirstOrDefault(pair => pair.Key == key).Value;
            if (int.TryParse(value, out var count) && count > 1)
            {
                return count;
            }
        }

        return 0;
    }

    private static KeyValuePair<string, string?> Pair(string key, string? value)
    {
        return new KeyValuePair<string, string?>(key, value);
    }
}
