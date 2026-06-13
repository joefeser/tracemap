# Combined Dependency Diff Requirements

## Introduction

TraceMap can scan individual repositories, combine multiple language indexes, report cross-app dependency evidence, and query bounded dependency paths. The next layer is a deterministic diff command over two combined indexes.

The goal is to answer review questions like:

- Which endpoints, dependency surfaces, and evidence paths appeared or disappeared between two snapshots?
- Did a client-to-server-to-database dependency trail change across commits?
- Did a route stop reaching a SQL/config/package/outbound HTTP surface according to static evidence?
- Did scan coverage get weaker, making an apparent removal impossible to trust?
- Which source repositories, languages, commit SHAs, rule IDs, evidence tiers, file spans, and path signatures support the diff?

This is still static evidence diffing. It is not runtime tracing, semantic source diffing, deployment diffing, database schema diffing, or AI impact analysis.

## Current State

- `tracemap combine` writes combined source, fact, symbol, relationship, call, object creation, argument flow, alias, parameter-forwarding, endpoint, and dependency-edge data.
- `tracemap report` summarizes endpoint alignment, dependency surfaces, dependency edges, known gaps, and limitations.
- `tracemap paths` queries bounded evidence trails through a combined index.
- TraceMap does not yet compare two combined indexes to explain what static dependency evidence changed.
- Commit SHA and scan metadata are available through source manifests and should be used when describing each snapshot.

## MVP Scope Decisions

- Add a new command: `tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path>`.
- MVP input is two combined SQLite databases produced by `tracemap combine`.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP is read-only. It must not mutate either combined index or write derived rows back to either database.
- MVP compares source inventory, coverage, endpoint evidence, dependency surfaces, dependency edges, and optionally dependency paths.
- MVP path comparison is opt-in with `--include-paths` because path search can be expensive on large combined indexes.
- MVP default comparison includes source, coverage, endpoint, surface, and dependency-edge summaries.
- MVP reuses existing report/path readers and endpoint matching behavior where practical. It must not add another endpoint alignment implementation.
- MVP treats absence under reduced coverage as an analysis gap, not as proof of removal.
- MVP does not compare raw source text, ASTs, compiled binaries, runtime configuration, deployments, database schemas, migrations as executed, or package lockfile resolution beyond existing facts.
- MVP does not use LLMs, embeddings, vector databases, or prompt-based classification.

## Example Workflow

```bash
dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index /tmp/before-client/index.sqlite --label client \
  --index /tmp/before-api/index.sqlite --label api \
  --out /tmp/before-combined.sqlite

dotnet run --project src/dotnet/TraceMap.Cli -- combine \
  --index /tmp/after-client/index.sqlite --label client \
  --index /tmp/after-api/index.sqlite --label api \
  --out /tmp/after-combined.sqlite

dotnet run --project src/dotnet/TraceMap.Cli -- diff \
  --before /tmp/before-combined.sqlite \
  --after /tmp/after-combined.sqlite \
  --include-paths \
  --out /tmp/tracemap-diff
```

Expected directory artifacts:

```text
diff-report.md
diff-report.json
```

## Requirements

### Requirement 1: Diff Command

**User Story:** As a reviewer, I want a `tracemap diff` command so that I can compare two combined indexes without writing custom SQL.

#### Acceptance Criteria

1. WHEN the user runs `tracemap diff --before <combined.sqlite> --after <combined.sqlite> --out <path>` THEN TraceMap SHALL read both indexes and emit a deterministic diff report.
2. WHEN `--format json` is provided with a file output THEN TraceMap SHALL emit a machine-readable JSON diff report.
3. WHEN `--out` is an existing directory or a path without an extension THEN TraceMap SHALL emit both `diff-report.md` and `diff-report.json`.
4. WHEN `--out` is a file path THEN TraceMap SHALL emit only the requested format, defaulting to Markdown.
5. WHEN either input is not a combined index THEN the command SHALL fail with a clear message and SHALL NOT silently compare single-language indexes.
6. WHEN required combined tables or views are missing THEN the command SHALL fail with a schema error naming the missing table or view and input side.
7. WHEN the command completes THEN the CLI SHALL print output path, compared source count, endpoint diff count, surface diff count, edge diff count, path diff count, gap count, and report coverage.
8. WHEN the command runs THEN it SHALL open both SQLite databases read-only and SHALL NOT mutate either input.

### Requirement 2: Comparison Scope and Selectors

**User Story:** As an investigator, I want scoped diffs so that I can compare only the endpoints or dependency surfaces I care about.

#### Acceptance Criteria

1. WHEN no scope is provided THEN TraceMap SHALL compare sources, coverage, endpoints, dependency surfaces, and dependency edges.
2. WHEN `--include-paths` is provided THEN TraceMap SHALL also compare bounded dependency path signatures using the same path search semantics as `tracemap paths`.
3. WHEN `--scope <value>` is provided THEN valid values SHALL be `all`, `sources`, `endpoints`, `surfaces`, `edges`, and `paths`.
4. WHEN `--scope all` is provided THEN TraceMap SHALL include path comparison only if `--include-paths` is also provided or SHALL emit a clear message explaining that path diffing is opt-in.
5. WHEN `--source <label>` is provided THEN TraceMap SHALL constrain comparison to matching source labels.
6. WHEN `--endpoint "<METHOD> <PATH_KEY>"` is provided THEN TraceMap SHALL constrain endpoint and path comparison to matching normalized endpoint evidence.
7. WHEN `--surface <kind>` is provided THEN TraceMap SHALL constrain surface and path comparison to that terminal surface kind.
8. WHEN `--surface-name <text>` is provided with `--surface` THEN TraceMap SHALL filter surface display names, package names, config keys, table names, or normalized path keys with case-insensitive exact matching by default; `*` MAY be used as a leading/trailing wildcard for prefix, suffix, or contains matching.
9. WHEN `--max-depth`, `--max-paths`, or `--max-frontier` are provided with `--include-paths` THEN TraceMap SHALL pass those limits to the path query engine for both snapshots.
10. WHEN selectors match neither snapshot THEN TraceMap SHALL emit a valid report with zero diffs and a `SelectorNoMatch` gap.

### Requirement 3: Snapshot and Source Pairing

**User Story:** As a maintainer, I want source pairing rules so that diff results compare the right repositories and do not merge unrelated scans.

#### Acceptance Criteria

1. WHEN sources have the same combined source label in both snapshots THEN TraceMap SHALL pair them by source label.
2. WHEN a source label exists only in the before snapshot THEN TraceMap SHALL emit a `SourceRemoved` diff.
3. WHEN a source label exists only in the after snapshot THEN TraceMap SHALL emit a `SourceAdded` diff.
4. WHEN paired sources have different repo names, languages, root path hashes, or repository identities where available THEN TraceMap SHALL emit a `SourceIdentityChanged` warning.
5. WHEN paired sources have different commit SHAs THEN TraceMap SHALL include before and after SHAs in source metadata.
6. WHEN either paired source has an unknown commit SHA THEN TraceMap SHALL emit `UnknownAnalysisGap` for conclusions that depend on complete source history.
7. WHEN scan coverage differs between snapshots THEN TraceMap SHALL emit `CoverageChanged` with before and after coverage values.
8. WHEN after coverage is reduced and evidence is missing after the change THEN TraceMap SHALL classify the missing evidence as coverage-relative rather than definite removal.
9. WHEN before coverage is reduced and evidence appears after the change THEN TraceMap SHALL classify the new evidence as `AddedWithBeforeGap` rather than proving it was newly introduced.
10. WHEN unpaired sources exist THEN path and endpoint diffs SHALL remain source-scoped and SHALL NOT compare unrelated labels by name similarity.

### Requirement 4: Stable Identity Keys

**User Story:** As a reviewer, I want stable identity keys so that diffs reflect meaningful evidence changes instead of row ID churn.

#### Acceptance Criteria

1. WHEN endpoint evidence is compared THEN TraceMap SHALL key endpoints by source label, endpoint kind, HTTP method, normalized path key, and stable symbol or handler identity where available.
2. WHEN dependency surfaces are compared THEN TraceMap SHALL key surfaces by source label, surface kind, normalized display identity, and structured metadata such as package name, config key, SQL table names, operation, query shape hash, or HTTP method/path key where available.
3. WHEN dependency edges are compared THEN TraceMap SHALL key edges by source label, edge kind, normalized source identity, normalized target identity, and rule-backed evidence shape rather than volatile database row IDs.
4. WHEN paths are compared THEN TraceMap SHALL key paths by a stable path signature built from the ordered sequence of normalized node and edge descriptors.
5. WHEN a stable identity cannot be constructed without raw source snippets or unsafe values THEN TraceMap SHALL use a deterministic hash and SHALL mark the row `NeedsReviewDiff`.
6. WHEN two rows share the same stable identity but differ in evidence tier, rule ID, file span, source symbol, target symbol, or metadata hash THEN TraceMap SHALL classify the row as `ChangedEvidence`.
7. WHEN only volatile row IDs differ THEN TraceMap SHALL NOT classify the row as changed.
8. WHEN duplicate stable identities exist within one snapshot THEN TraceMap SHALL preserve all provenance, emit a `DuplicateIdentity` gap, and classify affected diffs no stronger than `NeedsReviewDiff`.

### Requirement 5: Diff Classifications

**User Story:** As a reviewer, I want diff classifications that separate strong static changes from coverage gaps and review items.

#### Acceptance Criteria

1. WHEN evidence exists only in the after snapshot and before coverage was full enough for that evidence kind THEN TraceMap SHALL classify it as `Added`.
2. WHEN evidence exists only in the before snapshot and after coverage was full enough for that evidence kind THEN TraceMap SHALL classify it as `Removed`.
3. WHEN evidence exists in both snapshots with the same stable identity but changed metadata THEN TraceMap SHALL classify it as `ChangedEvidence`.
4. WHEN evidence appears only after but before coverage was reduced or had relevant gaps THEN TraceMap SHALL classify it as `AddedWithBeforeGap`.
5. WHEN evidence appears only before but after coverage was reduced or had relevant gaps THEN TraceMap SHALL classify it as `RemovedWithAfterGap`.
6. WHEN evidence cannot be compared credibly because source identity, commit SHA, schema, duplicate identities, or analysis gaps prevent a conclusion THEN TraceMap SHALL classify it as `UnknownAnalysisGap`.
7. WHEN selectors match no evidence in either snapshot THEN TraceMap SHALL classify the result as `SelectorNoMatch`.
8. WHEN path comparison is requested and no path exists in either snapshot under credible full coverage THEN TraceMap SHALL classify it as `NoPathEvidence`.
9. WHEN path comparison is not requested THEN TraceMap SHALL NOT imply that paths were unchanged.
10. WHEN confidence is emitted THEN it SHALL be derived from classification using a fixed mapping documented in design.

### Requirement 6: Evidence and Rule IDs

**User Story:** As a maintainer, I want every diff claim backed by source evidence and rule IDs.

#### Acceptance Criteria

1. WHEN a diff row is emitted THEN it SHALL include a diff rule ID.
2. WHEN a diff row is emitted THEN it SHALL include before and after evidence rule IDs where available.
3. WHEN a diff row is emitted THEN it SHALL include before and after evidence tiers where available.
4. WHEN a diff row is emitted THEN it SHALL include source label, language, scan ID, commit SHA, file path, and line span where available for both snapshots.
5. WHEN a path diff is emitted THEN it SHALL include before and after path signatures, path classifications, source transitions, supporting fact IDs, supporting edge IDs, and terminal surface metadata where available.
6. WHEN an endpoint diff is emitted THEN it SHALL include endpoint kind, method, normalized path key, static match quality where available, and matched source labels where applicable.
7. WHEN a surface diff is emitted THEN it SHALL include surface kind and safe structured metadata, not raw SQL, config values, raw URLs, or source snippets.
8. WHEN a diff conclusion is limited by coverage THEN the limiting source labels and gap rule IDs SHALL be included.

### Requirement 7: Markdown Report

**User Story:** As a human reviewer, I want a readable diff report so that I can quickly understand what changed.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN it SHALL include sections in this order: Summary, Compared Snapshots, Sources, Coverage Changes, Endpoint Diffs, Surface Diffs, Edge Diffs, Path Diffs, Gaps, Limitations.
2. WHEN path diffing was not requested THEN the Path Diffs section SHALL say that path comparison was not run.
3. WHEN a row is rendered THEN it SHALL show classification, source label, evidence kind, stable identity, before evidence, after evidence, rule IDs, evidence tiers, and safe file spans where available.
4. WHEN a diff is coverage-relative THEN Markdown SHALL visibly mark the coverage caveat near the row.
5. WHEN many rows exist THEN Markdown SHALL cap rows deterministically and emit truncation notices.
6. WHEN SQL, config, HTTP, or package surfaces are rendered THEN Markdown SHALL show safe metadata only and SHALL NOT show raw SQL, raw config values, raw snippets, raw URLs, connection strings, or local absolute paths.
7. WHEN no diffs exist THEN Markdown SHALL state whether the result means `NoDiffEvidence`, `SelectorNoMatch`, or `UnknownAnalysisGap`.

### Requirement 8: JSON Report Contract

**User Story:** As an automation author, I want a stable JSON diff report so that scripts and CI checks can consume it.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include top-level `version`, `reportCoverage`, `coverageWarnings`, `query`, `beforeSnapshot`, `afterSnapshot`, `summary`, `sourceDiffs`, `coverageDiffs`, `endpointDiffs`, `surfaceDiffs`, `edgeDiffs`, `pathDiffs`, `gaps`, and `limitations`.
2. WHEN query metadata is emitted THEN it SHALL include normalized selectors, scopes, include-paths flag, max depth, max paths, max frontier, and algorithm/version identifiers.
3. WHEN snapshot metadata is emitted THEN it SHALL include source labels, languages, scan IDs, commit SHAs, repo identity where available, coverage, and extractor versions where available.
4. WHEN a diff row is emitted THEN it SHALL include `diffId`, `changeType`, `classification`, `confidence`, `stableKey`, `diffRuleId`, `before`, `after`, `coverageCaveats`, and `notes`.
5. WHEN before or after evidence is missing THEN JSON SHALL use `null` consistently rather than omitting required fields.
6. WHEN data collections are empty THEN JSON SHALL emit empty arrays rather than omitting required fields.
7. WHEN Markdown caps rows THEN JSON SHALL still include all rows produced by the configured caps unless a JSON cap is explicitly added.
8. WHEN the JSON shape changes in a future version THEN the top-level `version` SHALL change.
9. WHEN outputs are generated from identical inputs and options THEN JSON SHALL be byte-stable.

### Requirement 9: Safety and Boundaries

**User Story:** As a maintainer, I want diff reports to be safe for public review and honest about what TraceMap can prove.

#### Acceptance Criteria

1. WHEN diff reports are emitted THEN they SHALL state that the report compares static evidence, not runtime behavior.
2. WHEN endpoint changes are reported THEN the report SHALL say endpoint evidence does not prove runtime traffic, auth behavior, proxies, deployment base paths, or reachability.
3. WHEN path changes are reported THEN the report SHALL say path evidence is not full taint analysis, branch feasibility analysis, runtime DI resolution, dynamic dispatch resolution, reflection resolution, or serializer contract mapping.
4. WHEN SQL/query changes are reported THEN the report SHALL say SQL evidence does not prove runtime execution, schema existence, generated SQL equivalence, dialect validity, or branch feasibility.
5. WHEN raw source snippets, raw SQL, raw URLs, config values, connection strings, or local absolute paths are present in input properties THEN the command SHALL not render them by default.
6. WHEN file paths are rendered THEN the command SHALL use the shared safe path helper that rejects or hashes local absolute paths.
7. WHEN Markdown cells are rendered THEN the command SHALL escape pipe characters, line endings, and Markdown link delimiters that could create accidental links.
8. WHEN reduced coverage exists THEN the report SHALL avoid claiming complete dependency coverage.

### Requirement 10: Tests and Validation

**User Story:** As a maintainer, I want focused tests so that diff behavior stays deterministic across language adapters.

#### Acceptance Criteria

1. WHEN two combined indexes differ by one endpoint THEN tests SHALL prove an endpoint diff appears.
2. WHEN two combined indexes differ by one SQL/config/package/HTTP surface THEN tests SHALL prove a surface diff appears.
3. WHEN evidence row IDs change but stable identities do not THEN tests SHALL prove no false diff appears.
4. WHEN evidence metadata changes under the same stable identity THEN tests SHALL prove `ChangedEvidence`.
5. WHEN after coverage is reduced and before evidence is missing after THEN tests SHALL prove `RemovedWithAfterGap` or `UnknownAnalysisGap`, not `Removed`.
6. WHEN before coverage is reduced and after evidence appears THEN tests SHALL prove `AddedWithBeforeGap`, not `Added`.
7. WHEN duplicate identities exist THEN tests SHALL prove affected diffs are down-ranked and a gap appears.
8. WHEN `--include-paths` is provided THEN tests SHALL prove added and removed path signatures are reported.
9. WHEN `--include-paths` is omitted THEN tests SHALL prove path diffing is not implied.
10. WHEN selectors match nothing THEN tests SHALL prove `SelectorNoMatch`.
11. WHEN both inputs are opened read-only THEN tests SHALL prove database files are unchanged after the command.
12. WHEN reports are emitted repeatedly for identical inputs THEN tests SHALL prove byte-stable Markdown and JSON output.
13. WHEN unsafe values are present in properties THEN tests SHALL prove reports do not contain raw SQL, raw URLs, config values, source snippets, connection strings, local absolute paths, or private repository names.
