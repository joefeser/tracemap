# Release Review Report Requirements

## Introduction

TraceMap can scan repositories, combine indexes, compare dependency evidence, query paths and reverse dependencies, and run several focused impact/diff workflows. A release reviewer still needs one deterministic packet that brings those pieces together for a before/after release candidate.

Release Review Report answers: between a before snapshot and an after snapshot, what static evidence changed, what contract or dependency surfaces need human review, which paths or reverse dependencies support that review, and where does TraceMap stop because coverage, identity, or evidence is incomplete?

This is a static evidence review packet. It is not a CI gate policy, merge decision, runtime incident predictor, deployment verifier, production traffic report, OpenAPI completeness proof, database schema validation, or AI-generated release conclusion.

## Scope

In scope:

- Add a release-oriented report workflow over before/after indexes or combined indexes.
- Compose existing and planned outputs from snapshot diff, combined change impact, contract delta impact v2, API/DTO contract diff, SQL/schema change impact, package surface evidence, dependency paths, reverse queries, and coverage analysis where evidence exists.
- Emit deterministic Markdown and JSON release review reports.
- Include source identity, commit SHA, coverage, extractor versions, changed surfaces, impacted paths, gaps, caveats, limitations, and a reviewer checklist.
- Preserve all underlying rule IDs, evidence tiers, file spans, source labels, supporting fact IDs, edge IDs, path IDs, and commit SHAs.
- Keep report conclusions explicitly tied to evidence and limitations.

Out of scope:

- No CI gate policy in v1.
- No auto-approval, merge recommendation, or release decision.
- No runtime traffic, deployment, production usage, customer impact, branch feasibility, runtime dependency injection, dynamic dispatch, serializer runtime mapping, SQL execution, schema existence, package compatibility, or vulnerability claims.
- No source-code text diffing beyond indexed fact evidence.
- No LLM calls, embeddings, vector databases, prompt-based classification, or generated prose conclusions in core.
- No raw SQL, source snippets, config values, connection strings, raw URLs, local absolute paths, or private paths in public reports.

## Requirements

### Requirement 1: Command and Inputs

**User Story:** As a release reviewer, I want one command that builds a release review packet from before/after TraceMap evidence.

#### Acceptance Criteria

1. WHEN the user runs `tracemap release-review --before <index.sqlite> --after <index.sqlite> --out <path>` THEN TraceMap SHALL emit a deterministic release review report.
2. WHEN both inputs are single-language indexes THEN the report SHALL run in `ReleaseReviewSingleV1` mode.
3. WHEN both inputs are combined indexes THEN the report SHALL run in `ReleaseReviewCombinedV1` mode.
4. WHEN one input is combined and the other is single-language THEN the command SHALL emit a sanitized error to stderr naming only which side is combined and which side is single-language, SHALL NOT echo raw file paths, and SHALL exit non-zero; mixed-mode release review is deferred.
5. WHEN either input is not a valid TraceMap index THEN the command SHALL fail with a sanitized schema error naming the side and missing object.
6. WHEN `--contract-delta <path>` is provided THEN the command SHALL verify the file is readable and include contract delta impact context using the v2 contract-delta workflow where available; if the workflow is unavailable THEN the Contract Delta Impact section SHALL be `deferred` or `unavailable` and SHALL emit a gap rather than silently ignore the input.
7. WHEN `--sql-schema-delta <path>` is provided THEN the command SHALL verify the file is readable and include SQL/schema impact context using the SQL/schema change impact workflow where available; if the workflow is unavailable THEN the SQL and Schema Impact section SHALL be `deferred` or `unavailable` and SHALL emit a gap rather than silently ignore the input.
8. WHEN `--package-delta <path>` is provided THEN the command SHALL verify the file is readable and include package impact context where indexed package-surface evidence is available; if the package-upgrade workflow is not implemented THEN the Package Impact section SHALL be `deferred` or `unavailable`, SHALL emit a gap stating the file was received but not analyzed by a package-upgrade workflow, and SHALL NOT silently ignore the input or imply package compatibility analysis.
9. WHEN no delta file is provided THEN the report SHALL still include before/after evidence diffs, coverage, gaps, and reviewer checklist sections.
10. WHEN the command runs THEN it SHALL open input indexes read-only and SHALL NOT mutate either input.
11. WHEN output path is a directory or has no extension THEN TraceMap SHALL write `release-review.md` and `release-review.json`.
12. WHEN output path is a file and `--format json` is provided THEN TraceMap SHALL write deterministic JSON to that file.
13. WHEN output path is a file and `--format markdown` or no format is provided THEN TraceMap SHALL write Markdown to that file.
14. WHEN output path is a directory or has no extension THEN `--format` SHALL NOT suppress the paired `release-review.md` and `release-review.json` outputs in v1.

### Requirement 2: Source Identity and Coverage

**User Story:** As a reviewer, I want the report to prove which snapshots and sources are comparable before summarizing change evidence.

#### Acceptance Criteria

1. WHEN comparing snapshots THEN TraceMap SHALL include before and after commit SHAs, scan IDs, repo identity hashes where available, languages, source labels, build status, analysis level, coverage, and extractor versions.
2. WHEN commit SHA is missing on either side THEN the report SHALL emit `UnknownCommitSha` and avoid history-completeness claims.
3. WHEN source identity is missing, unverified, duplicated, or conflicting THEN the report SHALL emit identity gaps and downgrade affected sections.
4. WHEN paired sources have different repository identity, language, or source label ownership THEN the report SHALL not compare their evidence as the same source unless a future explicit override is specified.
5. WHEN coverage is reduced on the before side THEN newly observed after evidence SHALL be labeled coverage-relative, not definite new release risk.
6. WHEN coverage is reduced on the after side THEN missing after evidence SHALL be labeled coverage-relative, not definite removal.
7. WHEN optional precision tables are absent THEN the report SHALL emit schema gaps for sections that depend on those tables.
8. WHEN a section depends on a workflow that was not run, not requested, unavailable, deferred, or truncated THEN the report SHALL use the closed status vocabulary `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`, not silently omit the section.
9. WHEN analysis gaps affect source comparability THEN the Summary and Reviewer Checklist SHALL include the limiting gaps.

### Requirement 3: Composed Evidence Sources

**User Story:** As a maintainer, I want release review sections to reuse existing TraceMap evidence instead of inventing another analysis layer.

#### Acceptance Criteria

1. WHEN snapshot diff behavior is available THEN release review SHALL reuse `tracemap diff` semantics for source, coverage, endpoint, surface, edge, and optional path evidence changes.
2. WHEN combined change impact behavior is available THEN release review SHALL wrap or reuse that workflow for impact item conversion and downgrade semantics instead of creating a competing impact classifier.
3. WHEN contract delta impact v2 behavior is available THEN release review SHALL include its findings and gaps without reclassifying them into stronger meanings.
4. WHEN API/DTO contract diff behavior is available THEN release review SHALL include endpoint, route, handler, DTO type, DTO property, method signature, and request/response attachment changes where evidence exists.
5. WHEN SQL/schema change impact behavior is available THEN release review SHALL include SQL query-shape, SQL text/hash, SQL resource, mapping, and `sql-persistence` evidence separately.
6. WHEN package upgrade impact behavior is unavailable THEN release review SHALL still be allowed to report indexed package declaration, lockfile, import, usage, or package-surface diffs from snapshot evidence, but SHALL label package-upgrade impact as deferred and SHALL NOT claim compatibility or vulnerability impact.
7. WHEN path or reverse context is requested THEN release review SHALL reuse existing bounded path/reverse workflows and SHALL NOT add a competing graph traversal implementation.
8. WHEN path or reverse context is requested in `ReleaseReviewSingleV1` mode THEN release review SHALL render those subsections as `unavailable` or `deferred` with a gap because v1 path/reverse context depends on combined indexes.
9. WHEN value-origin evidence is available in future indexes THEN release review MAY include it as a separate context subsection; absence of value-origin support SHALL be a gap or deferred section, not proof of no value path.
10. WHEN an underlying workflow emits a rule ID, evidence tier, limitation, caveat, confidence, or gap THEN release review SHALL preserve it.
11. WHEN an underlying workflow is unavailable in the current implementation slice THEN release review spec and output SHALL label the dependency as future or unavailable rather than block the whole report.

### Requirement 4: Report Sections

**User Story:** As a human reviewer, I want a release packet organized around the way I review a change.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL appear in this order: Summary, Compared Snapshots, Source Identity and Coverage, Top Changed Surfaces, Contract Delta Impact, API and DTO Changes, SQL and Schema Impact, Package Impact, Path and Reverse Context, Analysis Gaps, Reviewer Checklist, Limitations.
2. WHEN a section has no evidence because it was not requested THEN it SHALL say `not_requested`.
3. WHEN a section has no evidence because the workflow or index support is unavailable THEN it SHALL say `unavailable` or `deferred` and include a gap.
4. WHEN a section has evidence but all findings are review-tier or gaps THEN it SHALL not be summarized as safe, clean, approved, or low risk.
5. WHEN top changed surfaces are rendered THEN ordering SHALL be deterministic by classification severity, evidence tier, source label, surface kind, stable identity, and finding ID.
6. WHEN impacted paths are rendered THEN the report SHALL show path classification, source labels, terminal surface kind, rule IDs, evidence tiers, supporting IDs, and coverage caveats.
7. WHEN reviewer checklist is rendered THEN every checklist item SHALL be derived from report evidence or limitations, not generated advice.
8. WHEN a release packet has blocking analysis gaps THEN the checklist SHALL call out the exact gap categories.
9. WHEN no actionable findings are present but gaps exist THEN the Summary SHALL say `No actionable static findings under requested scope; gaps remain`, not `safe to release`.

### Requirement 5: Classifications and Severity

**User Story:** As a reviewer, I want release summaries to rank evidence without hiding the underlying classifications.

#### Acceptance Criteria

1. WHEN release review summarizes findings THEN it SHALL preserve underlying classification names from diff, impact, path, reverse, API/DTO, SQL/schema, and package workflows.
2. WHEN a release-level rollup classification is emitted THEN it SHALL use a closed vocabulary: `ActionableStaticEvidence`, `ReviewRecommended`, `NoActionableEvidence`, `PartialAnalysis`, `SelectorNoMatch`, `UnknownAnalysisGap`, and `TruncatedByLimit`.
3. WHEN the primary release-level rollup is selected THEN it SHALL use this fixed precedence order: `UnknownAnalysisGap`, `TruncatedByLimit`, `ActionableStaticEvidence`, `ReviewRecommended`, `PartialAnalysis`, `SelectorNoMatch`, `NoActionableEvidence`.
4. WHEN any underlying finding is strong static evidence such as `DefiniteImpact`, `ProbableImpact`, `StaticImpactEvidence`, `ProbableStaticImpact`, `Added`, `Removed`, or `ChangedEvidence` under verified source identity and full or comparable coverage THEN release rollup SHALL be `ActionableStaticEvidence` unless a higher-precedence rollup applies.
5. WHEN findings are review-tier, syntax-only, hash-only, identity-unverified, emitted with high-fan-out caveats by an underlying workflow, coverage-relative, or ambiguous THEN release rollup SHALL be no stronger than `ReviewRecommended`.
6. WHEN the report contains only no-evidence results under verified identity and full requested coverage THEN release rollup SHALL be `NoActionableEvidence` unless a higher-precedence rollup applies.
7. WHEN reduced coverage, unknown commit SHA, identity gaps, missing precision tables, or unavailable workflows affect requested sections but do not block all comparison THEN release rollup SHALL be `PartialAnalysis` only when no higher-precedence rollup applies.
8. WHEN identity, schema, coverage, or missing-table gaps prevent a credible conclusion for a requested section THEN the primary release rollup SHALL be `UnknownAnalysisGap`.
9. WHEN selectors match nothing THEN release rollup SHALL be `SelectorNoMatch` unless a higher-precedence rollup applies, and SHALL include selector metadata.
10. WHEN caps truncate findings or gaps THEN the primary release rollup SHALL be `TruncatedByLimit` unless `UnknownAnalysisGap` also applies.
11. WHEN reviewer checklist severity is assigned THEN it SHALL use a deterministic mapping from rollup classification, underlying classification, and gap category; severity SHALL NOT be inferred from prose or row order.
12. WHEN a numeric or ordinal risk score is emitted in a future slice THEN it SHALL be deterministic, documented, and explainable; v1 SHALL NOT emit hidden risk scores.

### Requirement 6: Selectors, Scope, and Caps

**User Story:** As an investigator, I want to scope release review without changing evidence meaning.

#### Acceptance Criteria

1. WHEN `--scope` is omitted THEN release review SHALL include all implemented sections.
2. WHEN `--scope` is provided THEN accepted values SHALL include `all`, `sources`, `coverage`, `surfaces`, `contracts`, `api-dto`, `sql-schema`, `packages`, `paths`, `reverse`, `gaps`, and `checklist`.
3. WHEN a scope maps to an unavailable workflow THEN the report SHALL emit an unavailable-workflow gap.
4. WHEN `--source <label>` is provided with combined indexes THEN the report SHALL filter all sections to the selected source.
5. WHEN `--scope coverage` is provided THEN release review SHALL map it to source/coverage summary and the diff layer's source scope, then filter to coverage rows where applicable; it SHALL NOT pass an unsupported `coverage` scope unchanged to workflows that do not accept it.
6. WHEN endpoint, surface, package, table, column, contract change ID, or source selectors are provided THEN they SHALL be passed to underlying workflows only when compatible.
7. WHEN selectors are ignored because the relevant scope is disabled or unavailable THEN query metadata SHALL record ignored selector/scope combinations.
8. WHEN `--include-paths` is omitted THEN path comparison SHALL not run and Path and Reverse Context SHALL state path context was `not_requested`.
9. WHEN `--include-reverse` is omitted THEN reverse query context SHALL not run and Path and Reverse Context SHALL state reverse context was `not_requested`.
10. WHEN `--include-paths` or `--include-reverse` is provided in `ReleaseReviewSingleV1` mode THEN the requested subsection status SHALL be `unavailable` or `deferred` with a gap; requested-but-unavailable mode SHALL take precedence over the default `not_requested` status.
11. WHEN caps such as `--max-findings`, `--max-paths`, `--max-gaps`, `--max-surface-rows`, and `--max-checklist-items` are hit THEN the report SHALL emit `TruncatedByLimit`.
12. WHEN caps are applied THEN ordering before truncation SHALL be deterministic and summary counts SHALL expose omitted counts.

### Requirement 7: Markdown Output Safety

**User Story:** As an open-source maintainer, I want release reports to be safe to publish.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN it SHALL NOT display raw SQL, source snippets, literal values, config values, connection strings, raw URLs, local absolute paths, private paths, or unredacted secret-looking values.
2. WHEN file paths are rendered THEN release review SHALL use shared safe path helpers.
3. WHEN Markdown table cells are rendered THEN pipe characters, line endings, brackets, parentheses, backticks, angle brackets, and other user-controlled Markdown syntax SHALL be escaped or omitted.
4. WHEN unsafe values appear in input fact properties THEN release review SHALL hash or omit them.
5. WHEN a section includes SQL evidence THEN it SHALL display safe query shape, table/column metadata, operation, source kind, and hashes only.
6. WHEN a section includes package evidence THEN it SHALL display ecosystem, safe package name, version/range metadata, and manifest/lockfile evidence only.
7. WHEN a section includes HTTP evidence THEN it SHALL display normalized safe route/path keys and method metadata, not raw URLs.
8. WHEN report limitations are rendered THEN they SHALL include the static-analysis boundary and redaction policy.

### Requirement 8: JSON Output Contract

**User Story:** As an automation author, I want deterministic JSON output for downstream tooling.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `mode`, `query`, `beforeSnapshot`, `afterSnapshot`, `summary`, `sourceCoverage`, `topChangedSurfaces`, `contractImpact`, `apiDtoChanges`, `sqlSchemaImpact`, `packageImpact`, `pathContext`, `reverseContext`, `gaps`, `reviewerChecklist`, and `limitations`.
2. WHEN an underlying workflow is unavailable THEN the corresponding section SHALL be present with `status: "unavailable"` and explanatory gaps.
3. WHEN a section was not requested THEN it SHALL be present with `status: "not_requested"`.
4. WHEN findings are included THEN each finding SHALL preserve stable ID, source label, classification, rule ID, evidence tier, file span, commit SHA, supporting IDs, safe display metadata, and limitations.
5. WHEN an underlying workflow already emits confidence or classification confidence THEN release review SHALL preserve it; release review SHALL NOT invent confidence values.
6. WHEN a section has evidence but caps were applied and rows were omitted THEN the section SHALL use `status: "truncated"` and include deterministic omitted counts.
7. WHEN checklist items are included THEN each item SHALL include stable ID, source section, triggering finding IDs or gap IDs, severity, and deterministic text.
8. WHEN arrays are emitted THEN they SHALL be sorted deterministically.
9. WHEN arbitrary metadata is emitted THEN release review SHALL use the canonical JSON encoding `metadata: [{ "key": "...", "value": "..." }]`, sorted by key and then value using ordinal comparison; emitted release-review JSON SHALL NOT use unordered metadata dictionaries for arbitrary fact metadata.
10. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.
11. WHEN identical inputs and options are run twice THEN Markdown and JSON SHALL be byte-stable.

### Requirement 9: Rules and Limitations

**User Story:** As a maintainer, I want release-review conclusions tied to documented rules and limitations.

#### Acceptance Criteria

1. WHEN a release-level finding or rollup is emitted THEN it SHALL cite a release-review rule ID.
2. WHEN an underlying finding is included THEN its original rule ID and evidence tier SHALL be preserved.
3. WHEN release review introduces rollup findings THEN implementation PR 1 SHALL document `release.review.rollup.v1` or an equivalent rule in `rules/rule-catalog.yml` before emitting them.
4. WHEN release review introduces checklist items THEN implementation PR 1 SHALL document `release.review.checklist.v1` or an equivalent rule before emitting them.
5. WHEN unavailable workflow gaps are emitted THEN implementation PR 1 SHALL document and cite a release-review gap rule.
6. WHEN truncation, selector, source identity, coverage, schema, or unsupported-mode gaps are emitted THEN implementation PR 1 SHALL document the relevant release-review rules and each gap SHALL include rule ID, evidence tier, and limitation.
7. WHEN limitations are rendered THEN they SHALL state release review is static evidence context and not approval, CI policy, runtime risk prediction, or release readiness.
8. WHEN implementation reuses existing diff/impact/path/reverse rules THEN it SHALL not weaken their limitations.

### Requirement 10: Tests and Validation

**User Story:** As a maintainer, I want tests that prove release review is deterministic, safe, and honest about gaps.

#### Acceptance Criteria

1. Tests SHALL cover before/after single-index release review output.
2. Tests SHALL cover before/after combined-index release review output.
3. Tests SHALL cover report behavior when no delta inputs are provided.
4. Tests SHALL cover contract delta, API/DTO, SQL/schema, and package sections as unavailable when their workflows are not implemented.
5. Tests SHALL cover path/reverse context as not requested unless flags are present.
6. Tests SHALL cover reduced coverage producing `PartialAnalysis` or `UnknownAnalysisGap`, not clean release language.
7. Tests SHALL cover source identity conflict producing gaps and downgrade.
8. Tests SHALL prove no raw SQL, snippets, config values, connection strings, raw URLs, or local absolute paths render in Markdown or JSON.
9. Tests SHALL prove Markdown and JSON are byte-stable for identical inputs.
10. Tests SHALL cover selector ignored metadata and selector-no-match behavior.
11. Tests SHALL cover checklist items deriving only from finding/gap evidence.
12. Tests SHALL prove input indexes are opened read-only and are not mutated.
13. Validation SHALL include `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check` for implementation PRs.
14. Tests SHALL cover mixed single/combined input rejection.
15. Tests SHALL cover invalid or non-index schema errors without raw paths, URLs, or unsafe values.
16. Tests SHALL cover the output path and `--format` matrix for directory, extensionless path, Markdown file, and JSON file.
17. Tests SHALL cover deterministic rollup precedence, including coverage-relative added/removed findings not being promoted to `ActionableStaticEvidence`.
18. Tests SHALL cover checklist severity mapping determinism.
19. Tests SHALL cover truncation omitted-count exposure and deterministic ordering.
20. Tests SHALL cover single-index mode with requested path/reverse context rendering unavailable/deferred section status and gaps.
21. Tests SHALL cover section statuses `available`, `not_requested`, `unavailable`, `deferred`, and `truncated` in JSON.
22. Tests SHALL cover rule IDs on release rollups, checklist items, gaps, and preservation of underlying rule IDs and evidence tiers.
