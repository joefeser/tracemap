# Deterministic Review Priority Scoring Requirements

## Introduction

TraceMap reports already preserve rule IDs, evidence tiers, classifications, analysis gaps, coverage labels, source identity, file spans, commit SHAs, extractor versions, and limitations. Large reports can still be hard to triage because the highest-attention rows are spread across diff, impact, path, reverse, portfolio, and release-review sections.

Deterministic review priority scoring adds an explainable ordering and severity-hint layer over existing TraceMap evidence. It helps reviewers decide what to inspect first, while preserving the underlying evidence and downgrade rules.

This feature is a static evidence prioritization layer. It is not runtime risk prediction, incident probability, release approval, vulnerability scanning, compliance certification, business criticality inference, or AI judgment.

## Scope

In scope:

- Define a deterministic review priority model based only on existing TraceMap evidence: evidence tiers, rule IDs, classifications, coverage labels, analysis gaps, changed facts, paths, surfaces, fan-out, source identity, schema availability, caps, and documented limitations.
- Emit score components and severity hints that each cite source evidence, rule IDs, evidence tiers, supporting IDs, and limitations.
- Integrate conceptually with release-review, combined diff, combined impact, route-flow/path, reverse, portfolio, and Markdown/JSON report outputs.
- Define strict downgrade and unknown behavior for reduced coverage, missing commit SHA, missing or conflicting source identity, missing optional schema, ambiguous evidence, hash-only identity, truncation, and unavailable workflows.
- Prefer terminology such as `reviewPriority`, `severityHint`, and `attentionLevel` over runtime or probability language.
- Keep score rules deterministic, documented, byte-stable, and testable.

Out of scope:

- No machine learning, LLM calls, embeddings, vector databases, prompt-based classification, or generated prose conclusions in core.
- No production incident probability, outage likelihood, exploitability, business criticality, customer impact, traffic, telemetry, deployment, runtime reachability, auth, proxy, serializer-runtime, SQL execution, schema-existence, package compatibility, vulnerability, license, compliance, or certification claims.
- No hidden weights or environment-specific tuning.
- No automatic merge, release approval, CI gate, or policy enforcement in v1.
- No raw source snippets, raw SQL/config values, literal values, connection strings, raw URLs, hostnames, raw remotes, local absolute paths, private paths, or secrets in reports.
- No new scanner facts are required in the first implementation slice.

## Requirements

### Requirement 1: Command and Report Integration

**User Story:** As a reviewer, I want TraceMap reports to include deterministic priority hints so I can sort evidence without losing the underlying facts.

#### Acceptance Criteria

1. WHEN a report workflow opts into review priority scoring THEN TraceMap SHALL calculate score components from already loaded report evidence and SHALL NOT rescan source code.
2. WHEN scoring is enabled for release-review THEN release-review JSON SHALL include a `reviewPriority` section and row-level priority metadata where applicable.
3. WHEN scoring is enabled for combined diff, combined impact, route-flow/path, reverse, or portfolio reports THEN each workflow SHALL preserve its existing report schema and add scoring through explicit versioned fields or an optional companion section, not by replacing existing classifications.
4. WHEN a workflow does not support scoring in the current slice THEN it SHALL report scoring as `deferred` using that workflow's existing closed status vocabulary and SHALL NOT silently omit requested scoring; `not_supported` is deferred until a workflow has an explicit unsupported-scoring code path.
5. WHEN scoring is omitted by default in a first implementation slice THEN the CLI option or configuration SHALL be explicit, documented, and deterministic.
6. WHEN output path is a directory and a scored report is produced THEN paired Markdown and JSON outputs SHALL remain deterministic and follow existing report output conventions.
7. WHEN report scoring runs THEN input indexes SHALL be opened read-only and SHALL NOT be mutated.
8. WHEN identical inputs, options, and TraceMap version are used twice THEN scoring output SHALL be byte-stable.
9. WHEN scoring status is `unavailable`, `deferred`, or `unknown` THEN that status SHALL NOT change the underlying workflow's existing exit-code behavior unless a future explicit scoring exit-code option is added.

### Requirement 2: Evidence Inputs and Provenance

**User Story:** As a maintainer, I want every priority component to explain exactly which evidence caused it.

#### Acceptance Criteria

1. WHEN a score component is emitted THEN it SHALL cite a scoring rule ID, source finding or gap IDs, source rule IDs, evidence tiers, source labels, commit SHAs where available, file paths and line spans where available, and limitations.
2. WHEN a score component is derived from an underlying report row THEN it SHALL preserve the row's original classification and SHALL NOT relabel the row into a stronger conclusion.
3. WHEN a score component is derived from an analysis gap THEN it SHALL cite the gap rule ID and evidence tier, and SHALL treat the component as uncertainty or review-attention evidence, not product impact proof.
4. WHEN fan-out is used THEN it SHALL be calculated from deterministic static counts such as affected sources, changed surfaces, distinct endpoint selectors, distinct path roots, path count caps, reverse root counts, or supporting edge counts.
5. WHEN path or reverse evidence contributes THEN the score component SHALL cite path or reverse rule IDs, evidence tiers, bounded query limits, and truncation state.
6. WHEN coverage labels or build status contribute THEN the component SHALL cite coverage source evidence and SHALL distinguish reduced analysis from high-confidence change evidence.
7. WHEN source identity, commit SHA, or schema metadata is missing or conflicting THEN the scoring output SHALL cite the gap and downgrade or mark affected components unknown.
8. WHEN a component depends on optional data that was not requested or not available THEN the component SHALL record that limitation and SHALL NOT infer absence of risk.
9. WHEN arbitrary metadata is included in scoring JSON THEN it SHALL use canonical sorted key/value arrays, not unordered dictionaries.

### Requirement 3: Closed Priority Vocabulary

**User Story:** As an automation author, I want a stable vocabulary for sorting and filtering without interpreting prose.

#### Acceptance Criteria

1. WHEN row-level priority is emitted THEN `severityHint` SHALL use a closed vocabulary: `critical_review`, `high_review`, `medium_review`, `low_review`, `info`, and `unknown`.
2. WHEN report-level priority is emitted THEN `attentionLevel` SHALL use a closed vocabulary: `highest_attention`, `high_attention`, `moderate_attention`, `low_attention`, `informational`, and `unknown`.
3. WHEN a numeric score is emitted THEN it SHALL be named `priorityScore`, SHALL be an integer in a fixed range, and SHALL be accompanied by component rows whose sum or deterministic aggregation explains it.
4. WHEN a numeric score is not necessary for v1 THEN TraceMap MAY emit ordinal priority only, but the choice SHALL be documented in design and JSON versioning.
5. WHEN `unknown` is emitted THEN the output SHALL include at least one limiting gap or unavailable component explaining why no stronger or lower priority could be stated.
6. WHEN a report contains only no-evidence results under verified identity and full requested coverage THEN priority SHALL be no stronger than `informational` or `low_review`, unless checklist or gap rules produce a higher review-attention hint.
7. WHEN evidence is syntax-only, textual, hash-only, ambiguous, duplicate, name-only, coverage-relative, or high fan-out from noisy names THEN row priority SHALL be capped at `medium_review` unless an explicit scoring rule documents a stronger static evidence condition.
8. WHEN truncation, reduced coverage, missing schema, missing identity, unknown commit SHA, or unavailable requested workflows affect conclusions THEN the affected report priority SHALL be `unknown` or at least `moderate_attention` depending on the documented rule and scope.
9. WHEN existing workflow classifications use terms like `DefiniteImpact` or `ProbableImpact` THEN scoring SHALL treat them as static-evidence classifications only and SHALL NOT convert them into runtime probability language.

### Requirement 4: Component Model and Deterministic Aggregation

**User Story:** As a reviewer, I want to see why an item received a priority hint and which parts I can trust.

#### Acceptance Criteria

1. WHEN a score is emitted THEN TraceMap SHALL emit component records with `componentKind`, `componentValue`, `direction`, `ruleId`, `evidenceTier`, `sourceEvidenceIds`, and `limitations`.
2. WHEN multiple components apply to the same row THEN aggregation SHALL be deterministic and documented.
3. WHEN component weights are used THEN every weight SHALL be named, fixed in code, documented in design, covered by tests, and visible in JSON output.
4. WHEN component weights, ordinal precedence, cap behavior, or row/report aggregation rules are changed in a future version THEN JSON version or scoring model version SHALL change.
5. WHEN a downgrade applies THEN the output SHALL include both the positive evidence component and the downgrade component rather than hiding the original evidence.
6. WHEN caps are hit before scoring can inspect all rows THEN scoring SHALL emit a truncation component and SHALL NOT present the report-level score as complete.
7. WHEN row sorting uses score output THEN ties SHALL be broken deterministically by attention level, severity hint, underlying classification order, evidence tier, source label, stable key, file path, line span, and stable ID.
8. WHEN a score component refers to a limitation THEN the limitation SHALL be included in the report limitations or the row-level limitation list.
9. WHEN scoring is performed across sections THEN report-level aggregation SHALL state which sections contributed, which were not requested, which were unavailable, and which were truncated.

### Requirement 5: Downgrade and Unknown Behavior

**User Story:** As an evidence consumer, I want incomplete analysis to be visible instead of being scored as clean.

#### Acceptance Criteria

1. WHEN before coverage is reduced THEN newly observed after evidence SHALL be scored as coverage-relative review attention, not definite new risk.
2. WHEN after coverage is reduced THEN missing after evidence SHALL be scored as coverage-relative review attention, not definite removal.
3. WHEN both sides have reduced coverage for a section THEN no-evidence or low-priority conclusions SHALL be replaced by `unknown` or limited review attention for that section.
4. WHEN commit SHA is missing on either side THEN scoring SHALL emit `UnknownCommitSha` or an equivalent scoring gap and SHALL avoid history-completeness language.
5. WHEN source identity is missing, duplicated, conflicting, or unverified THEN scoring SHALL downgrade affected row priority and SHALL raise report-level attention when the scope depends on that identity.
6. WHEN required schema is missing THEN scoring SHALL fail or mark the requested section unavailable using sanitized errors or gaps.
7. WHEN optional precision schema is missing THEN scoring SHALL continue, cite the optional-schema gap, and cap affected priority at review-tier.
8. WHEN evidence is ambiguous, duplicate, hash-only, syntax-only, text-only, or from fallback extraction THEN scoring SHALL preserve the evidence but cap confidence and priority according to documented rules.
9. WHEN a name is high fan-out, generic, or otherwise noisy according to existing reducer/report caveats THEN scoring SHALL downgrade to review attention and SHALL NOT force a high static impact conclusion.
10. WHEN row or gap caps truncate evidence THEN scoring SHALL emit `TruncatedByLimit`, expose omitted counts, and mark report-level scoring incomplete.
11. WHEN an underlying workflow was requested but unavailable THEN scoring SHALL emit an unavailable-workflow component rather than treating the missing section as low priority.
12. WHEN selectors match nothing under full credible coverage THEN scoring MAY emit informational priority; when selectors match nothing under reduced or unknown coverage THEN scoring SHALL emit unknown or partial priority.
13. WHEN the first release-review scoring slice checks schema availability THEN required schema SHALL mean the schema already required for release-review to build the requested sections; optional precision schema SHALL mean section-specific tables that improve confidence but are already handled as gaps by release-review.

### Requirement 6: Workflow-Specific Semantics

**User Story:** As a TraceMap user, I want priority scoring to reuse each workflow's meaning instead of inventing a competing impact model.

#### Acceptance Criteria

1. WHEN release-review scoring runs THEN it SHALL score checklist items, changed surfaces, contract/API/SQL/package sections, path/reverse context, gaps, and limitations without issuing release approval or readiness claims.
2. WHEN combined diff scoring runs THEN it SHALL score source, coverage, endpoint, surface, edge, and optional path diff rows using diff classifications and caveats.
3. WHEN combined impact scoring runs THEN it SHALL score impact items while preserving impact classifications and path-context classifications.
4. WHEN route-flow or path reports are scored THEN scoring SHALL account for path strength, evidence tier, bounded traversal, selector breadth, root/surface count, and truncation without claiming runtime reachability.
5. WHEN reverse reports are scored THEN scoring SHALL account for selected surfaces, reverse root count, path evidence, coverage caveats, target family, and truncation without claiming runtime usage.
6. WHEN portfolio scoring runs THEN scoring SHALL account for source count, cross-source shared surfaces, endpoint alignment findings, source coverage, optional before/after diff, optional impact/path/reverse context, and manifest identity gaps.
7. WHEN contract-delta reducer outputs are scored THEN scoring SHALL preserve reducer classifications and SHALL treat `NeedsReview` from fan-out or ambiguous names as review attention, not forced high impact.
8. WHEN package evidence contributes THEN scoring SHALL NOT claim compatibility, vulnerability, license, or deployment risk.
9. WHEN SQL evidence contributes THEN scoring SHALL NOT claim SQL execution, schema existence, data contents, tenant behavior, permissions, or query-plan risk.
10. WHEN HTTP route or client evidence contributes THEN scoring SHALL NOT claim runtime traffic, auth behavior, deployment base path, reverse proxy behavior, CORS, or handler execution.

### Requirement 7: Markdown and JSON Output Safety

**User Story:** As an open-source maintainer, I want scored reports to remain safe to publish.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN scoring sections SHALL NOT display raw SQL, raw source snippets, literal values, config values, connection strings, raw URLs, hostnames, local absolute paths, private paths, raw remotes, or unredacted secret-looking values.
2. WHEN JSON is emitted THEN scoring fields SHALL preserve only safe IDs, hashes, file paths already allowed by TraceMap safe rendering, line spans, rule IDs, evidence tiers, commit SHAs, and sanitized metadata.
3. WHEN unsafe values appear in source evidence properties THEN scoring SHALL reuse shared safe metadata and hashing helpers rather than rendering raw values.
4. WHEN Markdown tables include scoring data THEN table cells SHALL escape pipes, line endings, brackets, parentheses, backticks, angle brackets, and other user-controlled Markdown syntax.
5. WHEN report limitations mention scoring THEN they SHALL state scoring is deterministic static review prioritization, not runtime risk prediction, production impact, vulnerability scanning, compliance, release approval, or AI analysis.
6. WHEN PRs or generated artifacts describe validation THEN they SHALL avoid private local paths, private repo names, raw remotes, URLs, hostnames, snippets, secrets, and unsafe values.
7. WHEN scoring renders safe paths, Markdown table cells, or sorted metadata THEN it SHALL reuse shared report helpers such as `CombinedReportHelpers.Cell`, `CombinedReportHelpers.SafePath`, and `CombinedReportHelpers.SortedMetadata`, or a refactored equivalent shared helper, rather than adding another ad hoc escaping implementation.

### Requirement 8: Rule Catalog and Limitations

**User Story:** As a maintainer, I want scoring rules to be auditable like every other TraceMap conclusion.

#### Acceptance Criteria

1. WHEN scoring emits a component, row priority, report priority, downgrade, unknown, or truncation result THEN it SHALL cite a documented scoring rule ID.
2. WHEN the first implementation PR emits scoring output THEN it SHALL add or update rule catalog entries for scoring rules before enabling output.
3. WHEN a scoring rule is documented THEN it SHALL include inputs, deterministic behavior, evidence tier expectations, downgrade behavior, and limitations.
4. WHEN scoring reuses an underlying rule THEN it SHALL preserve that rule ID and SHALL NOT weaken its limitations.
5. WHEN scoring emits a limitation THEN it SHALL be tied to either the scoring rule or underlying source evidence rule.
6. WHEN scoring model version changes THEN release notes or docs SHALL identify the changed rule IDs and behavioral impact.
7. WHEN a score component has no evidence ID because it is derived from report-level metadata THEN it SHALL cite the metadata source, section, and rule ID.
8. WHEN a checklist-derived score component lacks its own evidence tier THEN it SHALL inherit evidence tiers from referenced findings or gaps where available, or use a documented metadata-component tier/limitation instead of inventing a source evidence tier.
9. WHEN a checklist-derived component references multiple source evidence tiers THEN it SHALL use the weakest contributing tier for component tier ordering and SHALL cite all contributing tier values in limitations or metadata.

### Requirement 9: Tests and Validation

**User Story:** As a maintainer, I want tests that prove scoring is deterministic, explainable, and honest about uncertainty.

#### Acceptance Criteria

1. Tests SHALL cover score output for at least one single-index workflow when implemented.
2. Tests SHALL cover score output for at least one combined-index workflow when implemented.
3. Tests SHALL prove every score component has a scoring rule ID, source evidence or metadata reference, evidence tier, and limitation.
4. Tests SHALL prove scoring preserves underlying classifications and rule IDs.
5. Tests SHALL cover reduced before coverage, reduced after coverage, missing commit SHA, identity conflict, missing optional schema, unavailable requested workflow, selector no-match, and truncation.
6. Tests SHALL cover syntax-only, hash-only, ambiguous, duplicate, and high-fan-out downgrade behavior.
7. Tests SHALL cover deterministic row ordering and tie-breaking.
8. Tests SHALL prove identical inputs and options produce byte-stable Markdown and JSON.
9. Tests SHALL prove raw SQL, snippets, config values, connection strings, raw URLs, hostnames, local absolute paths, private paths, raw remotes, and secrets are omitted or hashed.
10. Tests SHALL cover scoring status values reachable in the implemented slice, including `available`, `not_requested`, `unavailable`, `deferred`, and `truncated` for release-review; `not_supported` tests SHALL be deferred until a workflow can honestly emit that status.
11. Tests SHALL cover rule catalog entries for every new scoring rule ID.
12. Tests SHALL prove input indexes are opened read-only and are not mutated.
13. Validation for implementation PRs SHALL include `dotnet build src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, and `git diff --check`.
14. For language-adapter changes, validation SHALL also follow `docs/VALIDATION.md`; this spec does not require adapter changes in the first implementation slice.
15. Tests SHALL prove scoring does not upgrade an underlying source conclusion beyond documented static-evidence rules, for example a `NeedsReview` row cannot become `critical_review` without a separate non-noisy rule-backed component.
16. Tests SHALL prove `--include-priority` opt-out behavior preserves existing release-review Markdown and JSON byte-for-byte, unless the implementation explicitly chooses an always-present `not_requested` additive section and bumps or documents the JSON compatibility version.
17. Tests SHALL cover Markdown delimiter escaping in scoring output for allowed labels and limitations containing pipes, line endings, brackets, parentheses, backticks, angle brackets, and related Markdown syntax.
18. Tests SHALL assert every emitted `severityHint`, `attentionLevel`, and scoring section status belongs to the closed vocabulary for the implemented slice.
19. Tests SHALL assert release-review document version or scoring `modelVersion` is present and stable, and that future weight or schema changes require an intentional version update.
