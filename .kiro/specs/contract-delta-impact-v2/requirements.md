# Contract Delta Impact V2 Requirements

## Introduction

TraceMap already has a basic `tracemap reduce --index <index.sqlite> --contract-delta <contract-delta.json> --out <report.md>` command. The current reducer is useful for deterministic name matching against one index, but it does not yet model richer contract references, combined-index evidence, path/reverse context, SQL/package surfaces, or machine-readable output.

Contract Delta Impact V2 extends the reducer into an evidence-backed impact workflow for both single indexes and combined indexes. It answers: given a structured contract delta, which indexed facts provide static evidence that code, endpoints, dependency surfaces, or downstream repositories may need review?

This remains static analysis. Contract impact means evidence-backed review context, not proof of runtime production impact, customer usage, branch feasibility, runtime dependency injection, generated serializer mappings, SQL execution, schema existence, or dynamic dispatch.

## Scope

In scope:

- Define a v2 contract-delta input model for type, property, method, endpoint, package, schema, SQL/table/column, and dependency-surface references where indexed evidence exists.
- Support single-language indexes and combined indexes with source provenance.
- Match deltas against facts, symbols, endpoints, DTO/member facts, SQL surfaces, package surfaces, dependency edges, paths, reverse dependency evidence, and coverage gaps.
- Emit deterministic Markdown and JSON impact reports.
- Preserve rule IDs, evidence tiers, file spans, commit SHA, extractor versions, supporting fact/edge/path IDs, source labels, reduced coverage caveats, source identity caveats, and limitations.
- Reuse existing `reduce`, `report`, `paths`, `reverse`, `diff`, and `impact` semantics where practical.

Out of scope:

- No runtime traffic inference.
- No production telemetry or tracing.
- No runtime dependency-injection binding certainty.
- No reflection, dynamic dispatch, serializer alias, collection-content, mutation, or branch feasibility inference.
- No SQL execution, database connection, schema validation, query-plan inference, or migration execution.
- No vulnerability scanning or package compatibility scoring.
- No source-code text diffing.
- No LLM calls, embeddings, vector databases, or prompt-based classification in the reducer.
- No hidden risk scores or business criticality inference.

## Requirements

### Requirement 1: V2 Contract Delta Input

**User Story:** As an automation author, I want a structured v2 contract-delta format so TraceMap can match multiple contract surface kinds without guessing from one string field.

#### Acceptance Criteria

1. WHEN a v2 contract delta is provided THEN TraceMap SHALL accept `version`, `contract`, `source`, `changes`, and optional `metadata` fields.
2. WHEN a change is provided THEN it SHALL include `id`, `kind`, `changeType`, `reference`, and optional `old`, `new`, `severity`, `sourceHint`, and `metadata` fields.
3. WHEN `kind` is provided THEN allowed values SHALL include `type`, `property`, `method`, `endpoint`, `package`, `schema`, `sql-table`, `sql-column`, `sql-query`, and `dependency-surface`.
4. WHEN `reference` is provided THEN it SHALL be structured by kind rather than parsed only from display text.
5. WHEN legacy Milestone 7 deltas are provided THEN TraceMap SHALL continue to support them through a compatibility adapter and SHALL label the input as `LegacyContractDeltaV1` in the report.
6. WHEN the legacy compatibility adapter parses a flat `element` value THEN it SHALL follow documented v1 mapping rules and SHALL NOT infer more specificity than the legacy syntax supports.
7. WHEN a v2 change lacks enough structured identity to match safely THEN TraceMap SHALL emit an input validation gap and SHALL NOT invent a match from free text.
8. WHEN an unknown `kind` or `changeType` appears THEN TraceMap SHALL fail clearly by default with a sanitized error that does not echo unsafe raw input; a future compatibility mode MAY preserve unknown changes as `UnknownAnalysisGap` but must label them.
9. WHEN JSON is parsed THEN parsing SHALL be deterministic, case-sensitive for enum values unless documented otherwise, and independent of property order.

### Requirement 2: Single-Index Operation

**User Story:** As a repository maintainer, I want v2 contract deltas to work against one language index so the existing reducer workflow gets richer without requiring combined indexes.

#### Acceptance Criteria

1. WHEN `tracemap reduce --index <index.sqlite> --contract-delta <delta.json> --out <path>` receives a v2 delta and the index is a single-language index THEN TraceMap SHALL emit a v2 impact report.
2. WHEN a single index lacks combined tables THEN TraceMap SHALL match against `scan_manifest`, `facts`, symbols, and language adapter tables available in that index.
3. WHEN the scan reports full semantic coverage, known commit SHA, and no relevant gaps THEN no-match findings MAY use `NoEvidenceFullCoverage`.
4. WHEN the scan has reduced coverage, syntax-only coverage, failed/partial build, missing commit SHA, or relevant `AnalysisGap` facts THEN no-match findings SHALL NOT use `NoEvidenceFullCoverage`.
5. WHEN semantic facts and syntax fallback facts both match a change THEN stronger evidence SHALL be reported first, but weaker evidence MAY be retained as supporting evidence.
6. WHEN the same evidence matches through multiple aliases THEN TraceMap SHALL deduplicate by stable fact/symbol identity and preserve all matched reference forms.

### Requirement 3: Combined-Index Operation

**User Story:** As a platform reviewer, I want contract deltas to run against combined indexes so I can see cross-repo and cross-language evidence.

#### Acceptance Criteria

1. WHEN the index is a combined index THEN TraceMap SHALL preserve source label, source index ID, scan ID, repo identity hash, commit SHA, language, analysis level, build status, and coverage warnings for each finding.
2. WHEN combined source identity is missing, unverified, duplicated, or conflicting THEN findings for that source SHALL be downgraded to review-tier or `UnknownAnalysisGap` according to existing combined diff/impact identity rules.
3. WHEN a delta matches facts from multiple sources THEN the report SHALL group findings by change and source label deterministically.
4. WHEN path or reverse context is requested THEN TraceMap SHALL use existing combined path/reverse graph behavior rather than adding a second graph traversal implementation.
5. WHEN combined optional precision tables are missing THEN TraceMap SHALL emit schema/coverage gaps and SHALL NOT claim complete cross-source reachability.
6. WHEN combined and single-index output differ because one has path/reverse evidence and the other does not THEN the report SHALL state which evidence layer was unavailable.

### Requirement 4: Matching Semantics

**User Story:** As a reviewer, I want every match to explain exactly which indexed evidence matched the contract reference.

#### Acceptance Criteria

1. WHEN `kind=type` THEN TraceMap SHALL match compiler-resolved type facts, symbols, DTO declarations, serializer contract types, endpoint request/response types, and syntax fallback type names where available.
2. WHEN `kind=property` THEN TraceMap SHALL match semantic property/member access, DTO properties, serializer contract members, database column mappings, request/response fields, and syntax fallback member names where available.
3. WHEN `kind=method` THEN TraceMap SHALL match method declarations, invocations, endpoint handlers, call edges, parameter-forward edges, and method-compatible syntax facts where available.
4. WHEN `kind=endpoint` THEN TraceMap SHALL match normalized HTTP method/path keys, route facts, client endpoint facts, endpoint alignment evidence, and path/reverse endpoint roots where available.
5. WHEN `kind=package` THEN TraceMap SHALL match package/dependency facts, manifest evidence, lockfile evidence, import/usage evidence where available, and combined package surfaces when present.
6. WHEN `kind=schema`, `sql-table`, `sql-column`, or `sql-query` THEN TraceMap SHALL match SQL-shape facts, database column mappings, ORM mappings, SQL dependency surfaces, and safe table/column/query-shape metadata where available.
7. WHEN `kind=dependency-surface` THEN TraceMap SHALL match combined dependency surfaces by kind, safe surface identity, source kind, and supporting facts.
8. WHEN a match is name-only, syntax-only, ambiguous, generic, or high fan-out THEN classification SHALL be no stronger than `NeedsReview` or `NeedsReviewImpact`.
9. WHEN evidence contains unsafe values such as raw SQL, snippets, connection strings, raw URLs, or local absolute paths THEN reports SHALL omit or hash those values.
10. WHEN no evidence matches THEN TraceMap SHALL choose no-evidence classifications based on coverage and analysis gaps, not optimism.

### Requirement 5: Path And Reverse Context

**User Story:** As an investigator, I want optional path and reverse context around contract matches so I can understand static reachability.

#### Acceptance Criteria

1. WHEN neither `--include-paths` nor `--include-reverse` is provided THEN TraceMap SHALL NOT run path or reverse traversal and SHALL state that reachability context was not requested.
2. WHEN `--include-paths` is provided with a combined index THEN TraceMap SHALL gather bounded path context for matched endpoints, symbols, and dependency surfaces where selectors can be derived.
3. WHEN `--include-reverse` is provided with a combined index THEN TraceMap SHALL gather bounded reverse context from matched surfaces to endpoint/symbol/source roots where supported.
4. WHEN `--include-paths` or `--include-reverse` is provided with a single-language index THEN TraceMap SHALL reject the option with a clear message in v2 unless a future single-index traversal layer is specified.
5. WHEN path/reverse selectors are derived THEN they SHALL come only from symbol-identity-backed matches or stable-key dependency surface matches.
6. WHEN a match is name-only, syntax-only, high fan-out, or otherwise lacks stable symbol/surface identity THEN TraceMap SHALL emit `PathContextUnavailable` or `ReverseContextUnavailable` rather than seed traversal from that match.
7. WHEN path/reverse context is unavailable for a change THEN TraceMap SHALL emit `PathContextUnavailable` or `ReverseContextUnavailable` with rule ID and limitation.
8. WHEN no path/reverse evidence is found and contributing sources have reduced coverage THEN TraceMap SHALL emit `UnknownAnalysisGap` rather than proving absence of callers.
9. WHEN traversal hits depth, frontier, row, per-change, or total query caps THEN TraceMap SHALL emit `TruncatedByLimit` and mark report coverage partial.
10. WHEN path/reverse context supports a match THEN the report SHALL include supporting path IDs, edge IDs, fact IDs, classifications, and coverage caveats sorted deterministically.

### Requirement 6: Classifications

**User Story:** As a reviewer, I want classifications that distinguish strong static evidence from review-only and unknown outcomes.

#### Acceptance Criteria

1. WHEN the target index is a single-language index THEN report type `ContractDeltaImpactSingleV2` SHALL use the closed v1-compatible classification set: `DefiniteImpact`, `ProbableImpact`, `NeedsReview`, `NoEvidenceFullCoverage`, `NoEvidenceReducedCoverage`, `TruncatedByLimit`, and `UnknownAnalysisGap`.
2. WHEN the target index is a combined index THEN report type `ContractDeltaImpactCombinedV2` SHALL use the closed combined-impact classification set: `StaticImpactEvidence`, `ProbableStaticImpact`, `NeedsReviewImpact`, `NoImpactEvidence`, `SelectorNoMatch`, `TruncatedByLimit`, `PathContextUnavailable`, `ReverseContextUnavailable`, and `UnknownAnalysisGap`.
3. WHEN a changed contract element matches Tier1 semantic usage/declaration evidence directly in single-index mode THEN TraceMap SHALL classify it as `DefiniteImpact` unless coverage or identity caveats require a downgrade.
4. WHEN a changed contract element matches Tier1/Tier2 static evidence in combined mode with credible source identity and optional requested path/reverse context THEN TraceMap MAY classify it as `StaticImpactEvidence`; otherwise strong structural evidence SHALL be `ProbableStaticImpact`.
5. WHEN a changed contract element matches strong Tier2 structural evidence such as DTO shape, endpoint request/response type, SQL/package surface, framework integration, or database mapping in single-index mode THEN TraceMap SHALL classify it as `ProbableImpact` unless stronger semantic evidence exists.
6. WHEN a changed contract element matches Tier3 syntax/textual/name-only evidence THEN TraceMap SHALL classify it as `NeedsReview` in single-index mode or `NeedsReviewImpact` in combined mode.
7. WHEN v2-native matching encounters generic, ambiguous, or high-fan-out evidence THEN TraceMap SHALL downgrade to review-tier; legacy v1 compatibility output SHALL preserve existing v1 classification behavior and attach warnings unless a deliberate v1 behavior change is specified and tested.
8. WHEN analysis gaps prevent credible conclusion THEN TraceMap SHALL classify it as `UnknownAnalysisGap`.
9. WHEN no match exists and full semantic coverage is credible in single-index mode THEN TraceMap SHALL classify it as `NoEvidenceFullCoverage`.
10. WHEN no match exists and coverage is reduced or partial in single-index mode THEN TraceMap SHALL classify it as `NoEvidenceReducedCoverage` or `UnknownAnalysisGap` when gaps are specifically relevant to the changed element.
11. WHEN no comparable evidence is found in combined mode under credible full coverage THEN TraceMap SHALL emit `NoImpactEvidence` as a gap/report classification, not as a single-index reducer classification.
12. WHEN source identity, duplicate stable identity, hash-only surface identity, or schema gaps reduce confidence THEN TraceMap SHALL downgrade strong classifications and include a caveat.
13. WHEN confidence is emitted THEN it SHALL be derived from classification with a fixed documented mapping.
14. WHEN multiple evidence rows support different classifications THEN the finding SHALL expose the strongest allowed classification and preserve lower-tier supporting evidence.

### Requirement 7: CLI And Output

**User Story:** As a user, I want the v2 reducer to produce safe human and machine-readable reports using existing TraceMap command conventions.

#### Acceptance Criteria

1. WHEN output path is a file and no format is provided THEN TraceMap SHALL write Markdown.
2. WHEN output path is a directory or has no extension THEN TraceMap SHALL write `impact-report.md` and `impact-report.json`.
3. WHEN `--format markdown` is provided with a directory output THEN TraceMap SHALL still write both Markdown and JSON, with Markdown as the console-reported primary artifact.
4. WHEN `--format json` is provided with a file output THEN TraceMap SHALL write deterministic JSON to that file even if the file extension is not `.json`.
5. WHEN the command completes THEN the CLI SHALL print output path, change count, finding count, gap count, source count, and report coverage.
6. WHEN `--exit-code` is provided THEN TraceMap SHALL return `1` only when findings above no-evidence/gap-only classifications exist; invalid input remains nonzero.
7. WHEN `--scope`, `--source`, `--change-id`, `--kind`, `--surface`, or `--endpoint` selectors are provided THEN TraceMap SHALL filter deterministically and emit `SelectorNoMatch` when nothing matches.
8. WHEN `--scope` is provided THEN it SHALL filter by change kind using the v2 `kind` vocabulary plus `all`, not by impact classification group.
9. WHEN caps such as `--max-findings`, `--max-evidence-rows`, `--max-paths-per-change`, and `--max-gaps` are hit THEN TraceMap SHALL emit `TruncatedByLimit`.

### Requirement 8: JSON Report Contract

**User Story:** As an automation author, I want deterministic JSON output for CI and long-term comparison.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `input`, `query`, `index`, `summary`, `findings`, `gaps`, `coverageWarnings`, and `limitations`.
2. WHEN a finding is emitted THEN it SHALL include stable finding ID, change ID, change kind, change type, classification, confidence, source label if available, rule ID, evidence tier, file span, commit SHA, extractor version, supporting fact IDs, supporting edge IDs, supporting path IDs, and safe display metadata.
3. WHEN findings are emitted THEN they SHALL be sorted by `changeId`, `sourceLabel`, classification ordinal, and finding ID.
4. WHEN evidence rows are emitted THEN they SHALL be sorted by evidence tier strength, file path, start line, and evidence ID.
5. WHEN path or reverse context rows are emitted THEN they SHALL be sorted by stable path/root ID.
6. WHEN nested evidence is emitted THEN arrays and metadata keys SHALL be sorted deterministically.
7. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.
8. WHEN the same inputs and options are run twice THEN JSON and Markdown SHALL be byte-stable.
9. WHEN unsafe values appear in fact property bags THEN JSON SHALL omit or hash them.

### Requirement 9: Rules And Limitations

**User Story:** As a maintainer, I want every conclusion to cite documented rules and limitations.

#### Acceptance Criteria

1. WHEN a finding is emitted THEN it SHALL include a reducer rule ID.
2. WHEN supporting evidence is emitted THEN each evidence row SHALL include the source fact rule ID and evidence tier.
3. WHEN path/reverse context is emitted THEN it SHALL cite the path/reverse/impact rule IDs supporting that context.
4. WHEN gaps are emitted THEN every gap SHALL include rule ID, evidence tier, and limitation.
5. WHEN existing `contract.delta.reduce.v1` behavior is reused THEN the report SHALL preserve that rule ID for compatibility findings.
6. WHEN v2 behavior is added THEN `rules/rule-catalog.yml` SHALL document `contract.delta.impact.v2` before implementation code merges.
7. WHEN input validation gaps are emitted under a separate rule THEN `contract.delta.input.v2` SHALL be documented before implementation code merges.
8. WHEN path/reverse context gaps are emitted under a separate rule THEN `contract.delta.context.v2` SHALL be documented before implementation code merges.
9. WHEN limitations are rendered THEN they SHALL state static contract impact evidence is not runtime impact proof.

### Requirement 10: Tests And Validation

**User Story:** As a maintainer, I want tests that prove v2 impact is deterministic, evidence-backed, and honest about coverage.

#### Acceptance Criteria

1. Tests SHALL cover legacy v1 input compatibility.
2. Tests SHALL cover v2 type, property, method, endpoint, package, SQL/table/column, and dependency-surface changes.
3. Tests SHALL cover single-index and combined-index operation.
4. Tests SHALL prove Tier1 semantic matches classify stronger than Tier2/Tier3 matches.
5. Tests SHALL prove generic/high-fan-out names downgrade to review-tier.
6. Tests SHALL prove reduced coverage prevents `NoEvidenceFullCoverage`.
7. Tests SHALL prove relevant `AnalysisGap` facts produce `UnknownAnalysisGap`.
8. Tests SHALL prove combined source identity conflicts downgrade or gap findings.
9. Tests SHALL prove path/reverse context is optional, bounded, and gap-labeled when unavailable.
10. Tests SHALL prove raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths do not render.
11. Tests SHALL prove Markdown and JSON are byte-stable for identical inputs.
12. Tests SHALL prove input indexes are opened read-only and are not mutated.
13. Tests SHALL prove name-only/Tier3 matches do not seed path or reverse traversal.
14. Tests SHALL prove SQL query/table/column references match real SQL surface fields without raw SQL output.
15. Tests SHALL prove legacy v1 generic/high-fan-out behavior remains compatible or intentionally changes with explicit regression coverage.
16. Tests SHALL prove `--exit-code` returns `0` for gap-only/no-evidence results and `1` for actionable findings when requested.
17. Validation SHALL include `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check` for implementation PRs.
