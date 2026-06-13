# Combined Change Impact Requirements

## Introduction

TraceMap can now scan language-specific repositories, combine indexes, report dependency evidence, query bounded dependency paths, and diff two combined snapshots.

The next layer is a deterministic change-impact report over two combined snapshots. The goal is to answer review questions like:

- Which changed endpoints, dependency surfaces, edges, or source coverage changes have static evidence behind them?
- Which upstream endpoint paths or downstream dependency surfaces are connected to changed evidence?
- Did static reachability appear, disappear, or change between two commits?
- Where does TraceMap stop because reduced coverage, source identity mismatch, dynamic behavior, or missing schema prevents a credible conclusion?

This is still static analysis. `impact` means evidence-backed static change context. It does not prove runtime traffic, production usage, customer impact, branch feasibility, dependency injection bindings, reflection targets, serializer mappings, SQL execution, database schema existence, auth behavior, deployment routing, or actual value flow.

## Current State

- `tracemap combine` preserves multi-index source provenance in a combined SQLite database.
- `tracemap report` summarizes endpoint alignment, dependency surfaces, dependency edges, and coverage.
- `tracemap paths` follows bounded static evidence trails from endpoints, symbols, or sources to terminal dependency surfaces.
- `tracemap diff` compares two combined snapshots and emits coverage-relative source, endpoint, surface, edge, and opt-in path diffs.
- There is not yet a command that correlates changed evidence with before/after reachability context.

## MVP Scope Decisions

- Add a new command: `tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path>`.
- MVP input is two combined SQLite databases produced by `tracemap combine`.
- MVP output is Markdown by default, JSON with `--format json`, and both files for directory output.
- MVP is read-only. It must not mutate either input index or persist derived rows.
- MVP reuses the combined diff engine and combined path query logic instead of adding a second comparison or traversal implementation.
- MVP computes impact items from changed endpoint, surface, edge, coverage, and optional path evidence.
- MVP supports bounded before/after path context around changed evidence.
- MVP does not scan repositories, diff source files, execute applications, call LLMs, use embeddings, or query vector databases.
- MVP does not infer runtime usage or business criticality.
- MVP does not require path expansion for every diff row by default; it uses strict caps and emits truncation gaps.

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

dotnet run --project src/dotnet/TraceMap.Cli -- impact \
  --before /tmp/before-combined.sqlite \
  --after /tmp/after-combined.sqlite \
  --out /tmp/change-impact \
  --max-impact-items 100 \
  --max-paths-per-item 5
```

Expected directory artifacts:

```text
impact-report.md
impact-report.json
```

## Requirements

### Requirement 1: Impact Command

**User Story:** As a reviewer, I want a `tracemap impact` command so that I can understand static dependency context around changed evidence without writing diff and path queries by hand.

#### Acceptance Criteria

1. WHEN the user runs `tracemap impact --before <combined.sqlite> --after <combined.sqlite> --out <path>` THEN TraceMap SHALL read both combined indexes and emit an impact report.
2. WHEN either input is not a combined index THEN TraceMap SHALL fail with a clear message and SHALL NOT silently compare single-language indexes.
3. WHEN the output path is an existing directory or has no extension THEN TraceMap SHALL write `impact-report.md` and `impact-report.json`.
4. WHEN `--format json` is provided with file output THEN TraceMap SHALL write a machine-readable JSON report.
5. WHEN the command runs THEN it SHALL open both databases read-only.
6. WHEN the command completes THEN the CLI SHALL print output path, source count, diff count, impact item count, gap count, truncation state, and report coverage.
7. WHEN no comparable changes exist THEN TraceMap SHALL emit a valid report with `NoImpactEvidence` rather than failing.

### Requirement 2: Diff Reuse

**User Story:** As a maintainer, I want impact analysis to reuse `tracemap diff` semantics so that classifications do not drift.

#### Acceptance Criteria

1. WHEN impact analysis identifies changed evidence THEN it SHALL consume the same projected diff rows and gap classifications as `tracemap diff`.
2. WHEN `tracemap diff` would downgrade added or removed evidence because coverage is reduced THEN `tracemap impact` SHALL preserve that downgrade.
3. WHEN source identity conflicts or unverified source identity prevent strong claims THEN impact rows SHALL be `NeedsReviewImpact` or `UnknownAnalysisGap`.
4. WHEN duplicate stable identities exist THEN impact rows SHALL cite the duplicate identity gap and SHALL NOT select an arbitrary winner.
5. WHEN selectors are provided THEN impact analysis SHALL pass compatible selectors to the diff layer and record ignored selectors.
6. WHEN diff output is truncated THEN impact analysis SHALL include a truncation gap and SHALL NOT claim complete impact coverage.

### Requirement 3: Impact Item Model

**User Story:** As an automation author, I want each impact item to explain what changed and which evidence supports it.

#### Acceptance Criteria

1. WHEN a diff row becomes an impact item THEN the item SHALL include stable ID, change type, classification, confidence, source label, evidence kind, before evidence, after evidence, rule ID, evidence tier, file span, supporting fact IDs, and supporting edge IDs where available.
2. WHEN a source coverage diff changes build status, analysis level, commit SHA, or known gaps THEN TraceMap SHALL emit a coverage impact item.
3. WHEN endpoint evidence changes THEN TraceMap SHALL emit an endpoint impact item with method and normalized path key metadata when available.
4. WHEN dependency surface evidence changes THEN TraceMap SHALL emit a surface impact item with safe structured metadata only.
5. WHEN dependency edge evidence changes THEN TraceMap SHALL emit an edge impact item with source/target symbol metadata when available.
6. WHEN path signatures change and path comparison is enabled THEN TraceMap SHALL emit path impact items.
7. WHEN a diff row only has syntax/textual or ambiguous evidence THEN impact classification SHALL be no stronger than `NeedsReviewImpact`.
8. WHEN a diff row has no credible before/after evidence because of gaps THEN impact classification SHALL be `UnknownAnalysisGap`.

### Requirement 4: Path Context

**User Story:** As a reviewer, I want changed evidence connected to before/after paths so that I can see static reachability context.

#### Acceptance Criteria

1. WHEN path context is enabled THEN TraceMap SHALL run bounded path queries against the before and after snapshots for selected impact items.
2. WHEN a changed endpoint is the focus THEN path context SHALL start from the endpoint selector and search to terminal dependency surfaces.
3. WHEN a changed surface is the focus THEN path context SHALL search for paths that terminate at the same surface kind and safe surface identity when supported by the path query engine.
4. WHEN a changed edge is the focus THEN path context SHALL search from the edge source symbol and, when useful, to terminal dependency surfaces.
5. WHEN path context cannot be queried for an impact item THEN TraceMap SHALL emit `PathContextUnavailable` with a rule ID and limitation.
6. WHEN before paths exist and after paths do not, or vice versa, THEN TraceMap SHALL classify the context as `ReachabilityChanged` only if source identity and coverage allow the claim.
7. WHEN both sides have comparable paths but path evidence changed THEN TraceMap SHALL classify the context as `ReachabilityEvidenceChanged`.
8. WHEN no path is found and coverage is reduced or gaps exist THEN TraceMap SHALL emit `UnknownAnalysisGap` rather than `NoPathEvidence`.
9. WHEN path search hits depth, frontier, row, or per-item caps THEN TraceMap SHALL emit `TruncatedByLimit` and mark the report partial.
10. WHEN `--max-paths-per-item` is omitted THEN the default SHALL be small enough for review readability, such as 5.

### Requirement 5: Selectors and Caps

**User Story:** As an investigator, I want to narrow impact reports to the source, endpoint, or dependency area I care about.

#### Acceptance Criteria

1. WHEN `--source <label>` is provided THEN TraceMap SHALL filter diff and path context to matching source labels where applicable.
2. WHEN `--endpoint "<METHOD> <PATH_KEY>"` is provided THEN TraceMap SHALL focus endpoint impact and path context on matching normalized endpoint evidence.
3. WHEN `--surface <kind>` is provided THEN TraceMap SHALL focus surface impact and path context on matching surface kinds.
4. WHEN `--surface-name <name>` is provided THEN TraceMap SHALL use the same safe surface-name matching rules as `tracemap paths` and `tracemap diff`.
5. WHEN `--scope <list>` is provided THEN allowed values SHALL be `all`, `sources`, `coverage`, `endpoints`, `surfaces`, `edges`, and `paths`.
6. WHEN `--scope all` is used THEN path context SHALL remain bounded by `--include-paths` or the command's explicit path-context setting.
7. WHEN `--max-impact-items <n>` is provided THEN output SHALL be capped deterministically with a truncation gap.
8. WHEN `--max-gaps <n>` is provided THEN gap output SHALL be capped deterministically with a truncation gap.
9. WHEN a selector matches nothing THEN TraceMap SHALL emit `SelectorNoMatch` and SHALL NOT emit fake impact items.

### Requirement 6: Classifications

**User Story:** As a reviewer, I want impact classifications that separate strong static evidence from review-only or unknown results.

#### Acceptance Criteria

1. WHEN changed evidence has full credible coverage and Tier1/Tier2 static support plus path context on at least one side THEN TraceMap MAY classify it as `StaticImpactEvidence`.
2. WHEN changed evidence is strongly structural but lacks enough path context THEN TraceMap SHALL classify it as `ProbableStaticImpact`.
3. WHEN changed evidence depends on Tier3 syntax/textual evidence, ambiguous matches, duplicate identities, or name-only linking THEN TraceMap SHALL classify it as `NeedsReviewImpact`.
4. WHEN analysis gaps prevent a credible conclusion THEN TraceMap SHALL classify it as `UnknownAnalysisGap`.
5. WHEN comparable evidence exists but no static impact context is found under full coverage THEN TraceMap SHALL classify it as `NoImpactEvidence`.
6. WHEN no selector matches evidence THEN TraceMap SHALL classify the gap as `SelectorNoMatch`.
7. WHEN output is capped or path search is truncated THEN TraceMap SHALL include `TruncatedByLimit` and SHALL NOT imply complete coverage.
8. WHEN confidence is emitted THEN it SHALL be derived from classification with a fixed documented mapping.

### Requirement 7: Markdown Report

**User Story:** As a human reviewer, I want a readable impact report that is safe to share.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL be: Summary, Query, Snapshot Sources, Impact Items, Path Context, Gaps, Limitations.
2. WHEN an impact item is rendered THEN it SHALL show change type, impact classification, evidence kind, source label, safe display name, rule ID, evidence tier, file span, and coverage caveats.
3. WHEN path context is rendered THEN before and after path evidence SHALL be clearly separated.
4. WHEN output is partial THEN Markdown SHALL show partial coverage near the summary and affected items.
5. WHEN safe metadata is rendered THEN it SHALL exclude raw SQL, raw snippets, config values, connection strings, raw URLs, and local absolute paths.
6. WHEN no impact items exist THEN Markdown SHALL explain whether the result is no changes, selector no match, or unknown due to analysis gaps.

### Requirement 8: JSON Report Contract

**User Story:** As an automation author, I want deterministic JSON output so that impact reports can be compared in CI.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN it SHALL include `reportType`, `version`, `reportCoverage`, `coverageWarnings`, `query`, `beforeSnapshot`, `afterSnapshot`, `summary`, `impactItems`, `gaps`, and `limitations`.
2. WHEN arrays are emitted THEN they SHALL be sorted deterministically.
3. WHEN metadata is emitted THEN keys SHALL be sorted deterministically.
4. WHEN the same inputs and options are run twice THEN JSON and Markdown SHALL be byte-stable.
5. WHEN raw source property bags contain unsafe values THEN JSON SHALL omit or hash them.
6. WHEN impact path context is included THEN supporting path IDs, fact IDs, and edge IDs SHALL be sorted.
7. WHEN a field has no values THEN JSON SHALL use empty arrays or `null` consistently.

### Requirement 9: Rules and Limitations

**User Story:** As a maintainer, I want every impact conclusion tied to a documented rule.

#### Acceptance Criteria

1. WHEN impact rows are emitted THEN every row SHALL include a rule ID.
2. WHEN path context rows are emitted THEN every row SHALL cite path and diff rule IDs that support it.
3. WHEN gaps are emitted THEN every gap SHALL include a rule ID and evidence tier.
4. WHEN new `combined.impact.*.v1` rule IDs are introduced THEN `rules/rule-catalog.yml` SHALL document their limitations.
5. WHEN limitations are emitted THEN they SHALL explicitly state that static impact evidence is not runtime impact proof.

### Requirement 10: Tests and Validation

**User Story:** As a maintainer, I want enough tests to keep impact reports honest as adapters evolve.

#### Acceptance Criteria

1. WHEN endpoint evidence changes THEN tests SHALL prove an endpoint impact item appears.
2. WHEN SQL/config/package surface evidence changes THEN tests SHALL prove a surface impact item appears without raw unsafe values.
3. WHEN evidence moves file/line span but stable metadata is unchanged THEN tests SHALL prove `ChangedEvidence` is preserved into impact output.
4. WHEN coverage is reduced on the opposite side of an added/removed item THEN tests SHALL prove the impact classification is downgraded.
5. WHEN source identity mismatches are allowed THEN tests SHALL prove strong impact classifications are downgraded.
6. WHEN path context is capped THEN tests SHALL prove truncation gaps appear.
7. WHEN selectors match nothing THEN tests SHALL prove `SelectorNoMatch`.
8. WHEN identical combined snapshots are compared THEN tests SHALL prove `NoImpactEvidence`.
9. WHEN the command runs THEN tests SHALL prove input databases are not mutated.
10. WHEN output is generated twice from identical inputs THEN tests SHALL prove byte-stable Markdown and JSON.
11. WHEN validation runs locally THEN `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check` SHALL pass.
