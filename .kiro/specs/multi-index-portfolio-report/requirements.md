# Multi-Index Portfolio Dependency Report Requirements

## Introduction

TraceMap can scan individual repositories, combine indexes, summarize combined dependency evidence, diff combined snapshots, compute static impact context, query bounded paths, and run reverse dependency queries. The next portfolio layer is a deterministic report over many indexes or combined indexes that answers:

- What dependency surfaces exist across this portfolio of apps, packages, services, and libraries?
- Which sources call, expose, depend on, or can statically reach the same endpoints, SQL surfaces, packages, symbols, and configuration surfaces?
- Which portfolio surfaces changed between two portfolio snapshots?
- Which dependencies need human review because evidence is dynamic, ambiguous, coverage-reduced, or blocked by an analysis gap?
- Where can future release-review output reuse portfolio evidence without inventing a stronger conclusion?

This is still static evidence reporting. It is not runtime topology discovery, production traffic analysis, deployment mapping, service catalog ownership inference, package compatibility analysis, vulnerability analysis, release approval, or AI-generated classification.

## Scope

In scope:

- Add a portfolio report workflow over N single-language indexes, N combined indexes, or a manifest that references them.
- Preserve source labels, repo identity, commit SHA, scanner/extractor versions, scan IDs, analysis level, build status, rule IDs, evidence tiers, file paths, line spans, fact IDs, edge IDs, and limitations.
- Reuse combined report, dependency surfaces, endpoint alignment, paths, diff, impact, and reverse query concepts where they are available.
- Emit deterministic Markdown and JSON portfolio reports.
- Include MVP source inventory, dependency surface inventory, cross-source endpoint/dependency summary, portfolio gaps, limitations, and optional before/after portfolio diff context.
- Keep report output safe for public review by redacting raw snippets, raw SQL, raw URLs, secrets, private paths, and local absolute paths.

Out of scope:

- No source scanning in the portfolio command.
- No mutation of input indexes.
- No generated service ownership, runtime topology, production traffic, SLO, deployment, auth, CORS, proxy, DI binding, reflection, dynamic dispatch, branch-feasibility, SQL execution, database schema existence, or package compatibility claims.
- No LLM calls, embeddings, vector databases, prompt-based classification, generated risk scoring, or AI summaries in core.
- No raw source snippets, raw SQL, literal values, config values, connection strings, raw URLs, raw secrets, private checkout paths, or local absolute paths in public outputs.
- No replacement for `tracemap combine`, `report`, `diff`, `impact`, `paths`, `reverse`, or any future release-review workflow.

## MVP Scope Decisions

- Add a new command: `tracemap portfolio --out <path>`.
- MVP accepts either repeated `--index <path> --label <label>` pairs or `--manifest <portfolio.json>`.
- MVP may accept combined indexes directly and must expand their `index_sources` metadata without losing provenance.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory or extensionless output paths.
- MVP opens all input SQLite databases read-only and does not persist derived rows.
- MVP derives cross-source summaries in memory from existing facts and edges. Combined indexes use combined tables; single-language indexes require a first-class reader over `facts`, symbol, and edge tables rather than silently pretending combined-only readers support them.
- MVP can optionally compare two portfolio manifests with `--before-manifest` and `--after-manifest`; direct before/after repeated indexes are deferred unless specified in a follow-up.
- MVP does not perform path or reverse traversal by default. Optional path/reverse context is requested explicitly and bounded.
- MVP does not include `--release-review` or release-review packet import because no shipping release-review report workflow exists yet.
- MVP does not include `--exit-code`; deterministic CI policy is deferred to a follow-up spec.
- MVP does not require a separate portfolio database, graph database, or web UI.

## Example Workflows

Single portfolio snapshot:

```bash
tracemap portfolio \
  --index /tmp/client/index.sqlite --label web-client \
  --index /tmp/api/index.sqlite --label orders-api \
  --index /tmp/jobs/index.sqlite --label billing-jobs \
  --out /tmp/portfolio-report
```

Manifest input:

```bash
tracemap portfolio \
  --manifest portfolio.json \
  --out /tmp/portfolio-report \
  --format json
```

Before/after portfolio comparison:

```bash
tracemap portfolio \
  --before-manifest before-portfolio.json \
  --after-manifest after-portfolio.json \
  --out /tmp/portfolio-review \
  --include-impact
```

Expected directory artifacts:

```text
portfolio-report.md
portfolio-report.json
```

## Requirements

### Requirement 1: Portfolio Command and Inputs

**User Story:** As a platform reviewer, I want one command that summarizes dependency evidence across many TraceMap indexes.

#### Acceptance Criteria

1. WHEN the user runs `tracemap portfolio --index <index.sqlite> --label <label> --out <path>` THEN TraceMap SHALL emit a deterministic portfolio dependency report.
2. WHEN multiple `--index` arguments are provided THEN each input SHALL have exactly one user-provided label or a manifest-provided label.
3. WHEN `--manifest <portfolio.json>` is provided THEN TraceMap SHALL read index entries from the manifest and SHALL reject malformed entries with sanitized errors.
4. WHEN `--manifest` is combined with direct `--index` arguments THEN the command SHALL fail unless a future explicit merge flag named `--allow-mixed-inputs` is specified by a follow-up spec.
5. WHEN any input is a combined index THEN TraceMap SHALL expand its `index_sources` rows into portfolio source records while preserving the combined index label as container provenance.
6. WHEN any input is a single-language index THEN TraceMap SHALL include it as one portfolio source and SHALL read identity, coverage, surface, and edge evidence through a single-language index reader over the source index schema.
7. WHEN an input is not a valid TraceMap index THEN TraceMap SHALL fail with a sanitized schema error naming the input label and missing object, without echoing raw local paths.
8. WHEN an index lacks repo or commit SHA identity THEN TraceMap SHALL emit `UnknownCommitSha` or source identity gaps and SHALL mark coverage partial.
9. WHEN the command runs THEN it SHALL open all input indexes read-only and SHALL NOT mutate them.
10. WHEN output path is a directory or has no extension THEN TraceMap SHALL write `portfolio-report.md` and `portfolio-report.json`.
11. WHEN output path is a file and `--format json` is provided THEN TraceMap SHALL write deterministic JSON to that file.
12. WHEN output path is a file and `--format markdown` or no format is provided THEN TraceMap SHALL write Markdown to that file.
13. WHEN output path is a directory or has no extension THEN `--format` SHALL NOT suppress paired Markdown and JSON outputs in v1.
14. WHEN an extensionless output path already exists as a file THEN the command SHALL fail with a sanitized output-path error rather than overwriting it or treating it as a report file.
15. WHEN the command completes THEN the CLI SHALL print sanitized output location, portfolio source count, input index count, surface count, edge count, gap count, truncation state, and report coverage.

### Requirement 2: Portfolio Manifest

**User Story:** As an automation author, I want a manifest so portfolio reports can be reproduced without long command lines.

#### Acceptance Criteria

1. WHEN a manifest is used THEN it SHALL have a stable schema version.
2. WHEN manifest `version` is unsupported THEN the command SHALL fail with a sanitized version error.
3. WHEN manifest entries are read THEN each entry SHALL include label, index path, optional expected repo identity, optional expected commit SHA, optional source group, and optional role tags.
4. WHEN expected repo identity or expected commit SHA is provided THEN TraceMap SHALL compare it to index metadata and emit identity gaps on mismatch.
5. WHEN role tags are provided THEN the report MAY group sources by safe role tags, but SHALL NOT infer runtime ownership or service topology from tags.
6. WHEN manifest values include absolute paths THEN those paths MAY be used for local input resolution but SHALL NOT be emitted in Markdown or JSON.
7. WHEN manifest values include relative paths THEN those paths SHALL be resolved relative to the manifest file's directory location.
8. WHEN duplicate labels are present THEN the command SHALL fail unless a future explicit duplicate-label policy is specified.
9. WHEN a manifest references an unreadable file THEN the command SHALL fail with the entry label and a sanitized reason.

### Requirement 3: Source Identity and Coverage

**User Story:** As a reviewer, I want to know exactly which sources and commits support the portfolio conclusions.

#### Acceptance Criteria

1. WHEN a portfolio report is generated THEN it SHALL list every portfolio source with label, container label when applicable, language, repo name or repo identity hash, commit SHA, scan ID, scanner version, extractor version when available, analysis level, build status, and coverage status.
2. WHEN source data comes from a combined index THEN the source record SHALL include both the combined input label and the original `index_sources` label.
3. WHEN a source has failed build status, reduced semantic coverage, unknown commit SHA, missing scanner version, missing language, or analysis gaps THEN report coverage SHALL be reduced.
4. WHEN duplicate repo/commit/source labels create ambiguous identity THEN the report SHALL emit an identity gap and SHALL NOT merge their evidence.
5. WHEN the same underlying scan identity, or the same repo identity plus commit SHA, appears through more than one portfolio input THEN the report SHALL emit a `DuplicateSourceIdentity` gap and SHALL exclude duplicate copies from cross-source endpoint alignment and shared-surface grouping.
6. WHEN a source has known gaps in manifest JSON THEN the report SHALL summarize gap categories deterministically.
7. WHEN evidence is compared across sources with different commits THEN the report SHALL state that it is cross-snapshot portfolio evidence, not a single coherent release state, unless the manifest declares an expected portfolio snapshot.
8. WHEN a section depends on unavailable schema objects or optional precision tables THEN the section SHALL emit schema gaps rather than silently omitting evidence.
9. WHEN report coverage is reduced THEN the Summary and JSON `coverageWarnings` SHALL say conclusions are partial and coverage-relative.
10. WHEN coverage status is rendered THEN it SHALL use only `FullEvidenceAvailable`, `ReducedCoverage`, or `UnknownAnalysisGap`.
11. WHEN file paths are rendered THEN they SHALL be repository-relative or safe normalized paths only, never local absolute paths.

### Requirement 4: Dependency Surface Inventory

**User Story:** As a platform engineer, I want a portfolio-wide inventory of dependency surfaces without writing SQL.

#### Acceptance Criteria

1. WHEN HTTP client call facts exist THEN the report SHALL summarize them by source label, method, normalized path key when available, URL kind, dynamic reason code, evidence tier, rule ID, and file span.
2. WHEN HTTP route binding facts exist THEN the report SHALL summarize them by source label, method, normalized path key when available, route metadata, evidence tier, rule ID, and file span.
3. WHEN SQL or query-pattern facts exist THEN the report SHALL summarize operation, table names, column names, source kind, shape hash, source label, evidence tier, rule ID, and file span where available.
4. WHEN SQL evidence is only `SqlTextUsed`, `DapperCallDetected`, or `SqlCommandDetected` THEN the report SHALL display hashes, lengths, source kind, operation metadata when available, and `n/a` for table/column fields rather than inventing parsed SQL structure.
5. WHEN database column or persistence mapping facts such as `DatabaseColumnMapping` exist THEN the report SHALL summarize them as `sql-persistence` surface rows with table/column/mapping metadata where available.
6. WHEN package, project reference, import, module, config, environment variable, connection string name, resource identifier, or dependency facts exist THEN the report SHALL summarize them as `package-config` surface rows with safe package/module/key identity, ecosystem when available, dependency kind, source label, evidence tier, rule ID, and file span.
7. WHEN config, environment variable, connection string name, or resource identifier facts exist THEN the report SHALL summarize only stable key/name/hash metadata and SHALL NOT display raw values.
8. WHEN dependency edges exist THEN the report SHALL summarize calls, object creations, symbol relationships, argument flows, and parameter-forwarding edges by source label, edge kind, source symbol, target symbol, evidence tier, rule ID, and file span.
9. WHEN optional fields are missing THEN Markdown SHALL render `unknown` or `n/a`, and JSON SHALL use `null` or empty arrays consistently.
10. WHEN the same surface appears in multiple sources THEN the report SHALL preserve separate evidence rows and MAY add a grouped portfolio view without removing provenance.
11. WHEN a section exceeds configured row caps THEN Markdown SHALL show deterministic truncation notices, JSON SHALL expose omitted counts, and report coverage SHALL include `TruncatedByLimit`.

### Requirement 5: Cross-Source Dependency Summary

**User Story:** As a reviewer, I want to see relationships between portfolio sources while preserving evidence boundaries.

#### Acceptance Criteria

1. WHEN HTTP client calls and route bindings with normalized path evidence exist across sources THEN the report SHALL compute N-way endpoint alignment using the same semantics as combined dependency reporting.
2. WHEN endpoint alignment is computed THEN classifications SHALL use stable strings such as `MatchedEndpoint`, `OptionalSegmentMatch`, `MethodMismatch`, `AmbiguousMatch`, `ClientCallNoServerEndpoint`, `ServerEndpointNoClientMatch`, `DynamicClientUrlNeedsReview`, and `UnknownAnalysisGap`.
3. WHEN a client call matches endpoints in multiple sources THEN the report SHALL emit one finding per matched source and SHALL NOT collapse cross-source fan-out into ambiguity.
4. WHEN multiple endpoints inside the same source match the same client call at the same classification level THEN the report SHALL classify that source-local fan-in as `AmbiguousMatch`.
5. WHEN a source contains both client calls and routes THEN same-source matches SHALL be included and flagged with `sameSource = true`.
6. WHEN dynamic or unnormalized URLs prevent matching THEN the report SHALL emit review findings with closed-set reason codes and hashes, not raw URL fragments.
7. WHEN dependency surfaces have shared safe identities across sources, such as package names, SQL table names, or config keys, THEN the report MAY group them as portfolio shared surfaces with a grouping rule ID and limitations.
8. WHEN grouped shared surfaces are emitted THEN each group SHALL include supporting source evidence rows, SHALL expose whether all supporting rows came from the same source, and SHALL NOT claim runtime coupling or ownership.
9. WHEN no client or route evidence exists for a source THEN the report SHALL say endpoint alignment is not computable for that source rather than implying no dependencies.
10. WHEN analysis gaps affect cross-source matching THEN the report SHALL emit `UnknownAnalysisGap` findings with representative evidence where available.
11. WHEN duplicate source identity gaps affect candidate evidence THEN duplicate copies SHALL not create cross-source endpoint matches or shared-surface groups.

### Requirement 6: Before/After Portfolio Comparison

**User Story:** As a release reviewer, I want to compare two portfolio snapshots without losing source identity or coverage caveats.

#### Acceptance Criteria

1. WHEN the user provides `--before-manifest` and `--after-manifest` THEN TraceMap SHALL compare the two portfolio snapshots and include portfolio diff context.
2. WHEN before/after manifests have source label mismatches THEN the report SHALL emit added/removed/unpaired source evidence rather than matching arbitrary sources.
3. WHEN matching source labels have different repo identity than expected THEN the report SHALL emit identity gaps and downgrade affected comparisons.
4. WHEN matching source labels have different extracted repo identity and no manifest `expectedRepoIdentity` resolves the pair THEN the report SHALL emit an `IdentityAmbiguous` gap, downgrade affected comparisons to `ReviewRecommended`, and continue without treating the pair as a strong same-source comparison.
5. WHEN combined indexes are present in before or after manifests THEN the comparison SHALL pair expanded source records by declared source labels and identity, not by local file path.
6. WHEN `--include-impact` is provided THEN the report SHALL reuse existing combined change impact semantics where possible and SHALL NOT add a competing impact classifier.
7. WHEN impact context cannot run because inputs are not compatible combined snapshots THEN the section SHALL be `unavailable` or `deferred` with gaps.
8. WHEN coverage is reduced on either side THEN added, removed, and missing evidence SHALL be labeled coverage-relative.
9. WHEN no comparable changes exist under full requested coverage THEN the report MAY emit `NoPortfolioChangeEvidence`; under reduced coverage it SHALL emit a partial/gap result instead.
10. WHEN before/after comparison is not requested THEN diff and impact sections SHALL be present in JSON with `status: "not_requested"` and SHALL NOT be silently omitted.
11. WHEN comparison caps are hit THEN the report SHALL emit truncation gaps and SHALL NOT imply complete portfolio comparison.

### Requirement 7: Optional Path and Reverse Context

**User Story:** As an investigator, I want deeper context only when I ask for it, with clear bounds and caveats.

#### Acceptance Criteria

1. WHEN `--include-paths` is omitted THEN path traversal SHALL NOT run.
2. WHEN `--include-reverse` is omitted THEN reverse traversal SHALL NOT run.
3. WHEN `--include-paths` is provided THEN the report SHALL reuse existing bounded path query logic and SHALL preserve path IDs, rule IDs, evidence tiers, file spans, supporting facts, edge IDs, and path limitations.
4. WHEN `--include-reverse` is provided THEN the report SHALL reuse existing bounded reverse query logic and SHALL preserve reverse classifications, rule IDs, evidence tiers, supporting facts, edge IDs, and limitations.
5. WHEN path or reverse context is requested for inputs that cannot provide a compatible combined graph THEN the section SHALL be `unavailable` or `deferred` with a gap.
6. WHEN path or reverse traversal hits depth, frontier, source, path, root, or gap caps THEN the report SHALL emit `TruncatedByLimit` and mark the affected section as truncated.
7. WHEN release-review context is represented in v1 output THEN it SHALL be `not_requested` or `deferred` because release-review import is a follow-up after a release-review report workflow exists.
8. WHEN optional context is not requested THEN JSON SHALL include the section with `status: "not_requested"`.

### Requirement 8: Classifications, Rules, and Limitations

**User Story:** As a maintainer, I want every portfolio conclusion tied to documented deterministic rules.

#### Acceptance Criteria

1. WHEN a portfolio finding, group, gap, summary rollup, or checklist item is emitted THEN it SHALL include a rule ID.
2. WHEN a finding uses underlying evidence from scan, report, diff, impact, path, reverse, or release-review workflows THEN it SHALL preserve underlying rule IDs, evidence tiers, file spans, and limitations.
3. WHEN portfolio-specific grouping is introduced THEN implementation SHALL document rule IDs such as `portfolio.surface.group.v1`, `portfolio.endpoint.alignment.v1`, `portfolio.identity.v1`, `portfolio.coverage.v1`, `portfolio.truncation.v1`, `portfolio.schema.v1`, and `portfolio.redaction.v1` before emitting them.
4. WHEN portfolio report emits rollup classifications THEN the closed vocabulary SHALL include only `ActionableStaticEvidence`, `ReviewRecommended`, `NoActionableEvidence`, `PartialAnalysis`, `SelectorNoMatch`, `UnknownAnalysisGap`, and `TruncatedByLimit`.
5. WHEN rollup classification is selected THEN it SHALL use fixed precedence: `UnknownAnalysisGap`, `TruncatedByLimit`, `ActionableStaticEvidence`, `ReviewRecommended`, `PartialAnalysis`, `SelectorNoMatch`, `NoActionableEvidence`.
6. WHEN evidence is Tier3, dynamic, ambiguous, hash-only, coverage-relative, identity-unverified, or truncated THEN portfolio rollup SHALL be no stronger than `ReviewRecommended` unless a higher-precedence gap applies.
7. WHEN requested evidence is missing because schema or coverage prevents credible analysis THEN rollup SHALL be `UnknownAnalysisGap` or `PartialAnalysis`, not `NoActionableEvidence`.
8. WHEN a section contains multiple row classifications THEN the section rollup SHALL be a summary label only; reviewers must inspect detail rows because a single higher-precedence row can dominate the rollup.
9. WHEN limitations are rendered THEN they SHALL document static-analysis boundaries, grouping limitations, endpoint matching limitations, SQL limitations, package limitations, path/reverse bounds, and redaction policy.
10. WHEN rules are added or changed THEN the rule catalog SHALL include documented limitations before implementation is considered complete.

### Requirement 9: Markdown Output

**User Story:** As a human reviewer, I want a readable portfolio report organized around review workflow.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL appear in this order: Summary, Portfolio Inputs, Source Identity and Coverage, Cross-Source Endpoint Alignment, Dependency Surfaces, Dependency Edges, Shared Portfolio Surfaces, Optional Path and Reverse Context, Portfolio Diff and Impact, Release Review Context, Gaps, Limitations.
2. WHEN a section has no evidence because it was not requested THEN it SHALL say `not_requested`.
3. WHEN a section has no evidence because the workflow or index support is unavailable THEN it SHALL say `unavailable` or `deferred` and include a gap.
4. WHEN a section has evidence but only review-tier findings or gaps THEN it SHALL not be summarized as safe, clean, approved, or low risk.
5. WHEN findings are rendered THEN Markdown SHALL show safe display name, classification, source label, rule ID, evidence tier, commit SHA, file span, supporting IDs, and coverage caveats.
6. WHEN table cells contain user-controlled strings, including manifest `portfolioId`, `snapshotId`, `label`, `group`, and `roleTags`, THEN Markdown SHALL escape or omit pipe characters, line endings, brackets, parentheses, backticks, angle brackets, and link-like syntax.
7. WHEN no evidence is found for a section under full coverage THEN the section SHALL say `No evidence found under requested scope`; under reduced coverage it SHALL say the result is coverage-relative.
8. WHEN output is partial or truncated THEN Markdown SHALL show that status near the Summary and affected sections.

### Requirement 10: JSON Output Contract

**User Story:** As an automation author, I want deterministic JSON output for dashboards and CI comparisons.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include top-level `reportType`, `version`, `mode`, `query`, `portfolioSnapshot`, `beforeSnapshot`, `afterSnapshot`, `summary`, `inputs`, `sources`, `sourceCoverage`, `endpointAlignment`, `dependencySurfaces`, `dependencyEdges`, `sharedSurfaces`, `pathContext`, `reverseContext`, `portfolioDiff`, `portfolioImpact`, `releaseReviewContext`, `gaps`, and `limitations`.
2. WHEN before/after comparison is requested THEN JSON SHALL include `beforeSnapshot` and `afterSnapshot`; otherwise those fields SHALL be `null`.
3. WHEN optional sections are unavailable, deferred, not requested, or truncated THEN each section SHALL include a `status` field using the closed vocabulary `available`, `not_requested`, `unavailable`, `deferred`, and `truncated`.
4. WHEN findings are included THEN each finding SHALL preserve stable ID, source label, source identity, classification, rule ID, evidence tier, file span, commit SHA, supporting facts, supporting edges, safe display metadata, and limitations.
5. WHEN arbitrary metadata is emitted THEN JSON SHALL encode it as `metadata: [{ "key": "...", "value": "..." }]`, sorted by key and value using ordinal comparison.
6. WHEN reused path, reverse, impact, or diff evidence carries dictionary metadata THEN portfolio JSON SHALL normalize it to sorted `metadata` key/value arrays rather than embedding conflicting metadata shapes.
7. WHEN arrays are emitted THEN they SHALL be sorted deterministically.
8. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.
9. WHEN identical inputs and options are run twice THEN Markdown and JSON SHALL be byte-stable.
10. WHEN the JSON schema changes in a future version THEN top-level `version` SHALL change.
11. WHEN `PortfolioSnapshot` or nested objects are serialized THEN they SHALL NOT include generated timestamps, wall-clock dates, process IDs, imported timestamps, scanned timestamps, or other run-specific values.

### Requirement 11: Safety and Redaction

**User Story:** As an open-source maintainer, I want portfolio reports to be safe to share.

#### Acceptance Criteria

1. WHEN Markdown or JSON is emitted THEN it SHALL NOT display raw SQL, source snippets, literal values, config values, connection strings, raw URLs, raw secrets, tokens, credentials, local absolute paths, private paths, or unchecked source property bags.
2. WHEN file paths are rendered THEN shared safe path helpers SHALL be used.
3. WHEN unsafe values appear in fact properties THEN portfolio report SHALL hash, omit, or replace them with closed-set reason codes.
4. WHEN HTTP evidence is rendered THEN it SHALL display normalized route/path keys and method metadata, not raw URLs.
5. WHEN SQL evidence is rendered THEN it SHALL display operation, table/column metadata, source kind, shape hash, text hash, or length only.
6. WHEN config evidence is rendered THEN it SHALL display key names or hashes only, not values.
7. WHEN package evidence is rendered THEN it SHALL display safe ecosystem/package/version metadata only and SHALL NOT claim vulnerability, license, or compatibility impact.
8. WHEN an error is emitted THEN it SHALL be sanitized and SHALL NOT include raw local absolute paths or secret-looking values.
9. WHEN tests inject unsafe values into fact properties or manifest string fields THEN Markdown, JSON, and stderr SHALL not leak those values.

### Requirement 12: Tests and Validation

**User Story:** As a maintainer, I want tests that prove portfolio reports are deterministic, safe, and honest.

#### Acceptance Criteria

1. Tests SHALL cover repeated `--index --label` input.
2. Tests SHALL cover manifest input.
3. Tests SHALL cover mixed single-language and combined inputs.
4. Tests SHALL cover combined input expansion into portfolio sources.
5. Tests SHALL cover duplicate label rejection.
6. Tests SHALL cover expected repo/commit mismatch gaps.
7. Tests SHALL cover unknown commit SHA producing partial coverage.
8. Tests SHALL cover duplicate source identity across combined and single-language inputs and SHALL prove duplicates do not create cross-source matches or shared-surface groups.
9. Tests SHALL cover HTTP endpoint alignment classifications across at least three sources.
10. Tests SHALL cover same-source endpoint findings and cross-source fan-out as one finding per source.
11. Tests SHALL cover dynamic URL findings without raw URL leakage.
12. Tests SHALL cover SQL, package, config, call edge, object creation, and parameter-forwarding surface rendering without unsafe values.
13. Tests SHALL cover shared portfolio surface grouping preserving all supporting source evidence and `allSourcesSame`.
14. Tests SHALL cover before/after manifest comparison with added, removed, changed, unpaired, and identity-ambiguous sources.
15. Tests SHALL cover `--include-impact` unavailable/deferred behavior when compatible combined snapshots are not available.
16. Tests SHALL cover path and reverse sections as `not_requested` by default and unavailable/deferred when requested against incompatible inputs.
17. Tests SHALL cover truncation caps, omitted counts, and `TruncatedByLimit`.
18. Tests SHALL cover rollup precedence.
19. Tests SHALL cover relative manifest paths resolving from the manifest location.
20. Tests SHALL prove Markdown and JSON are byte-stable for identical inputs and do not include generated or stored scan/import timestamps.
21. Tests SHALL prove input databases are not mutated.
22. Tests SHALL prove rule IDs and evidence tiers appear on portfolio findings, groups, gaps, and rollups.
23. Tests SHALL prove no raw SQL, snippets, config values, connection strings, raw URLs, raw secrets, manifest-injected Markdown, or local absolute paths render in Markdown, JSON, or stderr.
24. Validation SHALL include `dotnet build src/dotnet/TraceMap.sln`, `dotnet test src/dotnet/TraceMap.sln`, `./scripts/check-private-paths.sh`, `git diff --check`, and relevant pinned smoke checks from `docs/VALIDATION.md` when implementation touches language adapters or combined/path/reverse/report behavior.
