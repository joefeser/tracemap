# Reverse Impact Query Requirements

## Introduction

TraceMap can combine multi-language indexes, report dependency surfaces, follow forward dependency paths, diff combined snapshots, and explain changed evidence with `tracemap impact`.

The next layer is a deterministic reverse query over one combined snapshot. The goal is to answer investigation questions like:

- Which endpoints, symbols, or source projects have static evidence that can reach this SQL table, HTTP client call, package/config surface, or API route?
- Which code paths connect a dependency surface back to a caller?
- Which findings are strong static evidence versus syntax-only or analysis-gap results?
- Where does TraceMap stop because coverage is reduced, graph evidence is missing, traversal caps are hit, or selectors are ambiguous?

This is still static analysis. Reverse query results are evidence-backed static reachability context. They do not prove runtime traffic, production usage, database execution, branch feasibility, dependency injection bindings, reflection targets, serializer mappings, collection contents, mutation semantics, or actual value flow.

## Current State

- `tracemap combine` preserves multi-index source provenance in a combined SQLite database.
- `tracemap report` summarizes endpoint alignment, dependency surfaces, dependency edges, and coverage.
- `tracemap paths` follows bounded forward static evidence trails from endpoints, symbols, or sources to terminal dependency surfaces.
- `tracemap diff` compares two combined snapshots.
- `tracemap impact` explains changed evidence and optional before/after path context.
- There is not yet a command that starts from a dependency surface and finds upstream static callers or endpoint roots.

## MVP Scope Decisions

- Add a new command: `tracemap reverse --index <combined.sqlite> --out <path>`.
- MVP input is one combined SQLite database produced by `tracemap combine`.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP is read-only. It must not mutate the input index or persist derived rows.
- MVP reuses the combined path graph evidence and endpoint matching semantics instead of adding a second graph model.
- MVP starts from selected terminal dependency surfaces and traverses reverse static edges toward endpoints, symbols, or source summaries.
- MVP may ship endpoint roots first; valid but unimplemented `--to` targets must fail clearly until their implementation slice lands.
- MVP supports SQL/query, HTTP route, HTTP client, and package/config surfaces that already exist in combined evidence.
- MVP does not scan repositories, execute applications, call LLMs, use embeddings, query vector databases, or infer runtime usage.
- MVP uses strict caps and emits truncation gaps rather than implying complete graph coverage.

## Example Workflow

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- reverse \
  --index /tmp/combined.sqlite \
  --surface sql-query \
  --surface-name runners \
  --to endpoints \
  --out /tmp/reverse-sql-runners \
  --max-roots 100 \
  --max-paths-per-root 5
```

Expected directory artifacts:

```text
reverse-report.md
reverse-report.json
```

## Requirements

### Requirement 1: Reverse Command

**User Story:** As an investigator, I want a `tracemap reverse` command so that I can start from a dependency surface and see upstream static callers without writing graph SQL.

#### Acceptance Criteria

1. WHEN the user runs `tracemap reverse --index <combined.sqlite> --out <path>` THEN TraceMap SHALL read the combined index and emit a reverse report.
2. WHEN the input is not a combined index THEN TraceMap SHALL fail with a clear message and SHALL NOT silently run over a single-language index.
3. WHEN the output path is an existing directory or has no extension THEN TraceMap SHALL write `reverse-report.md` and `reverse-report.json`.
4. WHEN `--format json` is provided with file output THEN TraceMap SHALL write a machine-readable JSON report.
5. WHEN the command runs THEN it SHALL open the database read-only.
6. WHEN the command completes THEN the CLI SHALL print output path, source count, selected surface count, root count, path count, gap count, truncation state, and report coverage.
7. WHEN no matching surfaces exist THEN TraceMap SHALL emit a valid report with `SelectorNoMatch` rather than failing.
8. WHEN matching surfaces exist but no credible reverse path is found THEN TraceMap SHALL emit `NoReversePathEvidence` only under full credible coverage.

### Requirement 2: Surface Selection

**User Story:** As an investigator, I want to target a dependency surface safely so that reverse results are scoped to the thing I care about.

#### Acceptance Criteria

1. WHEN `--surface <kind>` is provided THEN TraceMap SHALL select dependency surfaces of that kind.
2. WHEN `--surface-name <text>` is provided THEN TraceMap SHALL use the same MVP-safe surface-name matching rules as `tracemap diff` and `tracemap impact`: case-insensitive exact match across safe display names, package names, config keys, table names, or normalized path keys. Wildcard matching remains a `tracemap paths` feature and is deferred for reverse unless diff/impact adopt the same wildcard semantics.
3. WHEN `--source <label>` is provided THEN TraceMap SHALL limit selected surfaces and requested roots to matching source labels where applicable, but SHALL NOT prune traversed mid-path evidence solely because it crosses another source label.
4. WHEN no surface selector is provided THEN TraceMap SHALL use terminal dependency surfaces as the start set and SHALL cap output deterministically.
5. WHEN multiple surfaces share the same stable identity THEN TraceMap SHALL emit a duplicate-identity gap and SHALL NOT select an arbitrary winner for strong classification.
6. WHEN a selector is ambiguous or unsupported for a surface kind THEN TraceMap SHALL emit `NeedsReviewSurfaceEvidence` or `UnknownAnalysisGap` rather than strengthening the result.
7. WHEN selected surface metadata contains raw SQL, raw URL, config value, source snippet, connection string, or local absolute path THEN TraceMap SHALL omit or hash unsafe values before output.

### Requirement 3: Reverse Roots and Paths

**User Story:** As a reviewer, I want reverse results grouped by upstream root so that I can see which endpoint, symbol, or source can reach a dependency.

#### Acceptance Criteria

1. WHEN `--to endpoints` is selected THEN TraceMap SHALL stop at matched endpoint evidence and group paths by endpoint root.
2. WHEN `--to symbols` is selected THEN TraceMap SHALL stop at the nearest symbol root that can reach the selected surface.
3. WHEN `--to sources` is selected THEN TraceMap SHALL summarize contributing source labels and SHALL include representative bounded paths when available.
4. WHEN `--to all` is selected THEN TraceMap SHALL include endpoint roots, symbol roots, and source summaries without duplicating identical path evidence.
5. WHEN reverse traversal crosses source labels THEN the path SHALL preserve source transitions and combined source provenance.
6. WHEN a path is emitted THEN it SHALL include stable path ID, classification, root identity, terminal surface identity, source labels, scan IDs, commit SHAs, rule IDs, evidence tiers, file spans, node IDs, edge IDs, supporting fact IDs, and supporting edge IDs where available.
7. WHEN a root has more matching paths than `--max-paths-per-root` THEN TraceMap SHALL cap deterministically and emit `TruncatedByLimit`.

### Requirement 4: Reverse Classification

**User Story:** As a reviewer, I want reverse classifications that separate strong static reachability from review-only or unknown results.

#### Acceptance Criteria

1. WHEN a reverse path is supported by Tier1 semantic call/create/relationship evidence and full credible coverage THEN TraceMap MAY classify it as `StrongStaticReversePath`.
2. WHEN a reverse path is supported by strong structural endpoint/surface/edge evidence but lacks full semantic call-chain evidence THEN TraceMap SHALL classify it as `ProbableStaticReversePath`.
3. WHEN a reverse path depends on Tier3 syntax/textual evidence, name-only linking, ambiguous identity, or duplicate stable identity THEN TraceMap SHALL classify it as `NeedsReviewReversePath`.
4. WHEN analysis gaps prevent a credible conclusion THEN TraceMap SHALL classify the row or gap as `UnknownAnalysisGap`.
5. WHEN comparable surface evidence exists but no reverse path is found under full credible coverage THEN TraceMap SHALL emit `NoReversePathEvidence`.
6. WHEN a selector matches nothing THEN TraceMap SHALL emit `SelectorNoMatch`.
7. WHEN traversal hits depth, frontier, root, path, or gap caps THEN TraceMap SHALL emit `TruncatedByLimit` and mark report coverage partial.
8. WHEN confidence is emitted THEN it SHALL be derived from classification with a fixed documented mapping.

### Requirement 5: Coverage and Gaps

**User Story:** As a maintainer, I want reverse reports to label partial analysis so that users do not overread missing paths.

#### Acceptance Criteria

1. WHEN any contributing source has reduced analysis coverage THEN reverse no-path conclusions SHALL be downgraded to `UnknownAnalysisGap`.
2. WHEN build status, semantic analysis level, or known scan gaps prevent credible traversal THEN the report SHALL include coverage warnings.
3. WHEN precise edge tables are missing but fallback dependency edges exist THEN TraceMap SHALL emit review-tier results and preserve the schema gap.
4. WHEN source identity or commit SHA is missing from a combined source THEN TraceMap SHALL mark the source as identity-unverified in the report.
5. WHEN duplicate stable identities are detected in selected surfaces, roots, or path nodes THEN TraceMap SHALL include a duplicate identity gap and SHALL NOT produce strong static classifications for affected rows.
6. WHEN no surfaces, roots, or paths are emitted because of caps THEN TraceMap SHALL explain that the report is partial, not empty evidence.
7. WHEN both traversal caps and reduced-coverage gaps apply THEN TraceMap SHALL emit both `TruncatedByLimit` and `UnknownAnalysisGap`; truncation SHALL NOT suppress coverage gaps.
8. WHEN determining whether a no-path conclusion is credible THEN "contributing sources" SHALL mean the sources of selected surfaces plus all sources traversed while walking reverse adjacency toward roots within `--max-depth` and `--max-frontier`.

### Requirement 6: Selectors and Caps

**User Story:** As an investigator, I want narrow, bounded reverse queries that stay deterministic on large combined indexes.

#### Acceptance Criteria

1. WHEN `--source <label>` is provided THEN matching SHALL be deterministic case-insensitive exact matching equivalent to `StringComparison.OrdinalIgnoreCase`, consistent with `tracemap paths`, `tracemap diff`, and `tracemap impact`.
2. WHEN `--surface <kind>` is provided THEN allowed values SHALL be `sql-query`, `http-route`, `http-client`, and `package-config`.
3. WHEN `--surface-name <text>` is provided without `--surface` THEN TraceMap SHALL apply the name filter across all supported surface kinds and record the broad selector in query metadata.
4. WHEN `--to <target>` is provided THEN allowed values SHALL be `endpoints`, `symbols`, `sources`, and `all`; the default SHALL be `endpoints`.
5. WHEN `--max-depth <n>` is omitted THEN the default SHALL be `8`.
6. WHEN `--max-frontier <n>` is omitted THEN the default SHALL be `10000`.
7. WHEN `--max-roots <n>` is omitted THEN the default SHALL be `100`.
8. WHEN `--max-paths-per-root <n>` is omitted THEN the default SHALL be `5`.
9. WHEN `--max-surfaces <n>` is omitted THEN the default SHALL be `200`.
10. WHEN `--max-gaps <n>` is omitted THEN the default SHALL be `1000`.
11. WHEN a cap is hit THEN output SHALL be capped deterministically with a truncation gap.
12. WHEN `--to <target>` is valid but not implemented in the current release THEN TraceMap SHALL fail with a clear unsupported-selector message rather than silently returning empty results.
13. WHEN `--exit-code` is provided THEN TraceMap SHALL return `1` only when reverse roots or paths are present, and `0` when only gaps/no-evidence rows are present.
14. WHEN validation, argument parsing, file access, schema validation, database connection, or system errors occur THEN standard non-zero error exit codes SHALL take precedence over the result-based `--exit-code` behavior.

### Requirement 7: Markdown Report

**User Story:** As a human reviewer, I want a readable reverse report that is safe to share.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL be: Summary, Query, Snapshot Sources, Selected Surfaces, Reverse Roots, Paths, Gaps, Limitations.
2. WHEN a selected surface is rendered THEN it SHALL show kind, safe display name, source label, rule ID, evidence tier, file span, and coverage caveats.
3. WHEN a reverse root is rendered THEN it SHALL show root kind, safe display name, classification, confidence, source labels, path count, and rule IDs.
4. WHEN a path is rendered THEN it SHALL show ordered reverse evidence from root to terminal surface, even though traversal was computed backward.
5. WHEN output is partial THEN Markdown SHALL show partial coverage near the summary and affected sections.
6. WHEN safe metadata is rendered THEN it SHALL exclude raw SQL, raw snippets, config values, connection strings, raw URLs, and local absolute paths.

### Requirement 8: JSON Report Contract

**User Story:** As an automation author, I want deterministic JSON output so reverse reports can be compared in CI.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `reportCoverage`, `coverageWarnings`, `query`, `snapshot`, `summary`, `selectedSurfaces`, `reverseRoots`, `paths`, `gaps`, and `limitations`.
2. WHEN arrays are emitted THEN they SHALL be sorted deterministically.
3. WHEN metadata is emitted THEN keys SHALL be sorted deterministically.
4. WHEN the same input and options are run twice THEN JSON and Markdown SHALL be byte-stable.
5. WHEN raw source property bags contain unsafe values THEN JSON SHALL omit or hash them.
6. WHEN paths are included THEN supporting path IDs, fact IDs, and edge IDs SHALL be sorted.
7. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.

### Requirement 9: Rules and Limitations

**User Story:** As a maintainer, I want every reverse conclusion tied to a documented rule.

#### Acceptance Criteria

1. WHEN selected surfaces are emitted THEN every row SHALL include a rule ID.
2. WHEN reverse roots or paths are emitted THEN every row SHALL include a rule ID and evidence tier.
3. WHEN gaps are emitted THEN every gap SHALL include a rule ID and evidence tier.
4. WHEN new `combined.reverse.*.v1` rule IDs are introduced THEN `rules/rule-catalog.yml` SHALL document their limitations.
5. WHEN reverse traversal reuses path evidence THEN the report SHALL preserve supporting `combined.paths.*.v1` rule IDs rather than hiding the underlying evidence source.
6. WHEN limitations are emitted THEN they SHALL explicitly state that static reverse reachability is not runtime usage proof.

### Requirement 10: Tests and Validation

**User Story:** As a maintainer, I want enough tests to keep reverse queries honest as adapters evolve.

#### Acceptance Criteria

1. WHEN a combined index contains an endpoint-to-SQL path THEN tests SHALL prove `tracemap reverse --surface sql-query --to endpoints` returns the endpoint root.
2. WHEN a combined index contains an HTTP client surface THEN tests SHALL prove reverse query can target the HTTP client surface without leaking raw URLs.
3. WHEN a combined index contains package/config evidence THEN tests SHALL prove reverse query can target package/config surfaces without leaking config values.
4. WHEN selectors match nothing THEN tests SHALL prove `SelectorNoMatch`.
5. WHEN coverage is reduced and no path is found THEN tests SHALL prove `UnknownAnalysisGap`, not `NoReversePathEvidence`.
6. WHEN full credible coverage has no reverse path THEN tests SHALL prove `NoReversePathEvidence`.
7. WHEN traversal hits depth, frontier, root, path, or gap caps THEN tests SHALL prove `TruncatedByLimit`.
8. WHEN duplicate stable identities exist THEN tests SHALL prove the duplicate identity gap is cited and strong classification is downgraded.
9. WHEN source identity or commit SHA is missing THEN tests SHALL prove identity warnings appear.
10. WHEN `--to endpoints`, `--to symbols`, `--to sources`, and `--to all` are used THEN tests SHALL prove target-specific grouping semantics.
11. WHEN `--exit-code` is used THEN tests SHALL prove exit code `1` with reverse roots or paths and `0` without roots or paths.
12. WHEN output is generated twice from identical inputs THEN tests SHALL prove byte-stable Markdown and JSON.
13. WHEN the command runs THEN tests SHALL prove the input database is not mutated.
14. WHEN source snapshot metadata contains a repository remote URL THEN tests SHALL prove the remote is omitted or hashed in Markdown and JSON output.
15. WHEN `--surface-name` is used without `--surface` and matches surfaces of multiple kinds THEN tests SHALL prove the broad selector is recorded in query metadata and all matching kinds are included.
16. WHEN a forward `tracemap paths` endpoint-to-surface result exists over the same combined index THEN tests SHALL prove the matching reverse surface-to-endpoint query can return the endpoint root under the same coverage assumptions.
17. WHEN `--to all` is used THEN tests SHALL prove identical path evidence is not duplicated across endpoint, symbol, and source groups.
18. WHEN validation runs locally THEN `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass.
