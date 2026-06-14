# Snapshot Diff By Commit SHA Requirements

## Introduction

TraceMap indexes record repository identity, commit SHA, coverage, extractor versions, facts, symbols, dependency surfaces, and analysis gaps. Existing `tracemap diff` compares two combined indexes, but users need a first-class snapshot workflow that asks a stricter question:

> Given two TraceMap indexes for the same repository identity at two commit SHAs, what static evidence changed?

This spec defines a deterministic snapshot-diff command over already-produced TraceMap indexes. It does not clone repositories, checkout commits, scan source, diff source text, execute code, or infer runtime behavior. It compares indexed evidence only.

## Scope

In scope:

- Compare two existing TraceMap index artifacts from the same repository identity at different commit SHAs.
- Support single-language indexes and combined indexes.
- Validate source identity, source labels, commit SHAs, coverage, scan manifests, extractor versions, and schema compatibility before diffing.
- Summarize changed endpoints, DTO/type/member evidence, dependency surfaces, package surfaces, SQL/query surfaces, event/message surfaces when present, call edges, object creation, argument/value-flow facts, symbol identities, symbol relationships, analysis gaps, and extractor-version changes.
- Reuse `tracemap diff` and combined projection semantics where possible.
- Emit deterministic Markdown and JSON reports.
- Preserve rule IDs, evidence tiers, file spans, commit SHAs, extractor versions, source labels, supporting fact IDs, supporting edge IDs, and coverage caveats.

Out of scope:

- No Git checkout orchestration in v1.
- No repository scanning or rescanning.
- No semantic source-code diff.
- No runtime traffic, deployment, database, queue, package resolver, or telemetry comparison.
- No branch feasibility, runtime dependency injection, dynamic dispatch, reflection, serializer mapping, taint, or mutation inference.
- No generated OpenAPI, schema, migration, or package compatibility claims beyond indexed evidence.
- No LLM calls, embeddings, vector databases, or prompt-based classification.

## Requirements

### Requirement 1: Snapshot Diff Command

**User Story:** As a release reviewer, I want a command that compares two TraceMap snapshots by commit SHA so I can review indexed evidence changes without writing custom SQL.

#### Acceptance Criteria

1. WHEN the user runs `tracemap snapshot-diff --before <index.sqlite> --after <index.sqlite> --out <path>` THEN TraceMap SHALL read both indexes read-only and emit a deterministic snapshot diff report.
2. WHEN both inputs are single-language indexes THEN TraceMap SHALL compare them as one-source snapshots.
3. WHEN both inputs are combined indexes THEN TraceMap SHALL compare sources by combined source label and repository identity.
4. WHEN one input is single-language and the other is combined THEN TraceMap SHALL validate both input shapes, fail with a non-zero exit code before comparison, and write no output files unless a future explicit source selection mode is defined.
5. WHEN either input is missing required manifest/source identity data THEN TraceMap SHALL fail closed for source-history conclusions and emit a sanitized schema or identity gap where a report can still be produced.
6. WHEN `--format json` is provided with file output THEN TraceMap SHALL emit machine-readable JSON.
7. WHEN `--out` is a directory or a path without an extension THEN TraceMap SHALL emit `snapshot-diff-report.md` and `snapshot-diff-report.json`.
8. WHEN `--out` is a file path THEN TraceMap SHALL emit only the requested format, defaulting to Markdown.
9. WHEN the command completes THEN CLI output SHALL include output path, before SHA, after SHA, source count, diff row count, gap count, coverage state, and whether path comparison was requested.
10. WHEN `--exit-code` is provided THEN TraceMap SHALL return `1` when actionable diff rows are present and `0` when only no-diff or gap-only rows are present; invalid input remains non-zero.

### Requirement 2: Source Identity And Commit Validation

**User Story:** As a maintainer, I want strict identity validation so TraceMap does not compare unrelated repositories by accident.

#### Acceptance Criteria

1. WHEN comparing single-language indexes THEN repository identity SHALL be derived from manifest metadata as `repo-hash:{Hash(RemoteUrl ?? RepoName)}` when either value exists, and the raw remote URL or repository name SHALL NOT be rendered.
2. WHEN `ScanRootPathHash` or `GitRootHash` is available THEN TraceMap SHALL treat `ScanRootPathHash ?? GitRootHash` as a root identity signal and caveat, not as a replacement for repository identity.
3. WHEN single-language indexes lack first-class language metadata THEN language SHALL be treated as optional metadata and SHALL NOT by itself block comparison.
4. WHEN comparing combined indexes THEN every paired source label SHALL match by repository identity and language unless `--allow-identity-mismatch` is provided.
5. WHEN source labels differ in combined indexes THEN unmatched labels SHALL be reported as source added/removed; fuzzy pairing by name similarity SHALL NOT occur.
6. WHEN repo identity conflicts are detected THEN the command SHALL fail by default and SHALL NOT compare evidence for the conflicting source.
7. WHEN `--allow-identity-mismatch` is provided THEN conflicting sources MAY be compared, but all rows for that source SHALL be no stronger than review-tier, confidence SHALL be capped at `review`, and both snapshot metadata and affected rows SHALL carry identity caveats using hashed identity values only.
8. WHEN commit SHA is missing or unknown for either snapshot THEN history-dependent conclusions SHALL be `UnknownAnalysisGap`.
9. WHEN before and after commit SHA are identical and comparable evidence is unchanged THEN TraceMap SHALL emit `NoSnapshotDiffEvidence`.
10. WHEN before and after commit SHA are identical but evidence changed THEN TraceMap SHALL emit `ChangedEvidence` rows and add a `SameCommitShaDivergentEvidence` note with rule ID `snapshot.diff.identity.v1` to each affected row, because the index artifacts disagree for the same commit.
11. WHEN commit SHA is the only changed source metadata THEN TraceMap SHALL emit a source-level `ChangedEvidence` row and SHALL NOT create endpoint/surface/edge churn.
12. WHEN extractor versions differ THEN TraceMap SHALL emit `ExtractorVersionChanged` rows or gaps and downgrade evidence kinds whose extraction behavior changed.

### Requirement 3: Coverage And Schema Validation

**User Story:** As a reviewer, I want coverage changes and schema gaps to shape the diff classifications so absence of evidence is not overclaimed.

#### Acceptance Criteria

1. WHEN analysis level, build status, or known gap counts differ THEN TraceMap SHALL emit coverage diff rows.
2. WHEN after coverage is reduced and evidence disappears THEN TraceMap SHALL classify the disappearance as `RemovedWithAfterGap` or `UnknownAnalysisGap`, not definite removal.
3. WHEN before coverage is reduced and evidence appears THEN TraceMap SHALL classify the appearance as `AddedWithBeforeGap` or `UnknownAnalysisGap`, not definite addition.
4. WHEN both sides have reduced coverage for the evidence kind THEN TraceMap SHALL classify absence/presence conclusions no stronger than `UnknownAnalysisGap` unless rule-specific coverage says otherwise.
5. WHEN optional precision tables are missing from one snapshot THEN TraceMap SHALL emit `SchemaPrecisionGap` and use safe fallback evidence only where documented.
6. WHEN malformed JSON properties or manifest fields are present THEN TraceMap SHALL emit `MalformedMetadataGap`, omit unsafe metadata, and continue where possible.
7. WHEN coverage is partial THEN Markdown and JSON SHALL visibly mark the report partial.

### Requirement 4: Evidence Kinds Compared

**User Story:** As a user, I want snapshot diff to summarize the important indexed evidence changes, not just raw source metadata.

#### Acceptance Criteria

1. WHEN endpoint facts change THEN TraceMap SHALL report endpoint diffs with method, normalized path key, endpoint kind, source label, evidence tier, rule IDs, file spans, and commit SHAs.
2. WHEN DTO/type/member facts change THEN TraceMap SHALL report contract-shape diffs using stable symbol/member identities where available and syntax fallback identities where necessary.
3. WHEN dependency surfaces change THEN TraceMap SHALL report surface diffs for HTTP, SQL, package, config, file/storage, and event/message surfaces where indexed evidence exists.
4. WHEN SQL surfaces change THEN TraceMap SHALL compare safe operation/table/column/query-shape metadata and SHALL NOT render raw SQL.
5. WHEN package surfaces change THEN TraceMap SHALL compare ecosystem, package name, version/range, manifest/lockfile evidence, and usage evidence where available without vulnerability or compatibility claims.
6. WHEN event/message surfaces are present THEN TraceMap SHALL compare safe topic/queue/event identifiers and publisher/consumer evidence where available without runtime broker claims.
7. WHEN call edges, object creation, symbol relationships, argument flows, or parameter-forward edges change THEN TraceMap SHALL report graph evidence diffs with supporting IDs and rule IDs.
8. WHEN analysis gaps change THEN TraceMap SHALL report gap diffs so users can distinguish improved/worsened coverage from product behavior.
9. WHEN extractor versions change THEN TraceMap SHALL report extractor-version diffs and attach limitations to evidence produced by changed extractors.
10. WHEN evidence kind is unsupported by a given language adapter THEN TraceMap SHALL emit a coverage/gap caveat rather than a missing-evidence conclusion.
11. WHEN combined-index delegation cannot produce snapshot-specific arrays such as `contractShapeDiffs`, `gapDiffs`, or `extractorVersionDiffs` from available evidence THEN TraceMap SHALL mark those sections unavailable with a gap or limitation instead of implying no changes.
12. WHEN comparing combined indexes in v1 THEN `contractShapeDiffs` SHALL remain empty with an availability gap unless a combined contract-shape projector is explicitly implemented.
13. WHEN comparing single-language indexes THEN `contractShapeDiffs` SHALL be populated from declared type/member facts such as `TypeDeclared`, `PropertyDeclared`, and `MethodDeclared` when those facts are present.
14. WHEN comparing single-language indexes THEN `gapDiffs` SHALL be populated from `AnalysisGap` facts when those facts are present, using safe gap codes, file spans, rule IDs, and path hashes or safe relative paths.

### Requirement 5: Stable Identity And Churn Control

**User Story:** As a reviewer, I want changes to reflect meaningful indexed evidence differences rather than row ID churn.

#### Acceptance Criteria

1. WHEN comparing evidence rows THEN TraceMap SHALL use stable keys derived from source label, evidence kind, normalized symbol/surface identity, rule family, safe metadata hash, and file span where necessary.
2. WHEN volatile database row IDs differ but stable evidence identity and metadata are unchanged THEN TraceMap SHALL NOT emit a diff row.
3. WHEN stable identity is unchanged but rule ID, evidence tier, span, extractor version, or safe metadata changes THEN TraceMap SHALL emit `ChangedEvidence`.
4. WHEN stable identity cannot be built without volatile row IDs or unsafe values THEN TraceMap SHALL use a deterministic hash fallback and classify the row no stronger than `NeedsReviewDiff`.
5. WHEN duplicate stable identities exist within one snapshot THEN TraceMap SHALL preserve all duplicate provenance in JSON, emit `DuplicateIdentity`, and downgrade affected rows.
6. WHEN duplicate count changes between snapshots THEN TraceMap SHALL emit `ChangedEvidence` with duplicate-count metadata.
7. WHEN unsafe values appear in fact property bags THEN they SHALL NOT be used as cleartext stable-key inputs or rendered output.

### Requirement 6: Relationship To Existing Commands

**User Story:** As a maintainer, I want snapshot diff to reuse existing diff/report/path logic instead of creating parallel semantics.

#### Acceptance Criteria

1. WHEN both inputs are combined indexes THEN snapshot diff SHALL delegate comparable endpoint/surface/edge/path projection to the existing combined diff engine where possible.
2. WHEN both inputs are single-language indexes THEN snapshot diff SHALL use a single-source projector that mirrors combined diff stable-key and safety rules.
3. WHEN combined-index delegation is used THEN existing combined `SourceDiffs`, `CoverageDiffs`, `EndpointDiffs`, `SurfaceDiffs`, `EdgeDiffs`, `PathDiffs`, and `Gaps` SHALL be mapped into snapshot `sourceDiffs`, `coverageDiffs`, `endpointDiffs`, `surfaceDiffs`, `graphDiffs`, `pathDiffs`, and `gaps` without changing their evidence meaning.
4. WHEN `--include-paths` is provided for combined indexes THEN snapshot diff SHALL use the same bounded path comparison semantics as `tracemap diff --include-paths`.
5. WHEN `--include-paths` is provided for single-language indexes THEN the command SHALL fail clearly in v1 unless a future single-index path layer is defined.
6. WHEN users need general multi-repo comparison without same-repo commit validation THEN documentation SHALL point them to `tracemap diff`.
7. WHEN users need impact context around changed rows THEN documentation SHALL point them to `tracemap impact` or future release review workflows.
8. WHEN future release review report consumes snapshot diff output THEN it SHALL treat snapshot diff rows as static evidence deltas, not runtime release risk proof.

### Requirement 7: Selectors And Caps

**User Story:** As an investigator, I want scoped snapshot diffs so large indexes can be reviewed safely.

#### Acceptance Criteria

1. WHEN `--source <label>` is provided with combined indexes THEN TraceMap SHALL restrict comparison to that source label.
2. WHEN `--scope <scope>` is provided THEN TraceMap SHALL restrict diff rows to the requested scope vocabulary: `all`, `sources`, `coverage`, `endpoints`, `surfaces`, `graph`, `paths`, `gaps`, `extractors`, or `contract-shapes`.
3. WHEN `--endpoint "<METHOD> <PATH_KEY>"` is provided THEN TraceMap SHALL restrict endpoint and path comparison to matching normalized endpoint evidence.
4. WHEN `--surface <kind>` and optional `--surface-name <name>` are provided THEN TraceMap SHALL restrict surface and path comparison using safe surface identity matching.
5. WHEN `--max-diff-rows`, `--max-gaps`, `--max-depth`, `--max-paths`, or `--max-frontier` are provided THEN TraceMap SHALL apply deterministic caps and emit `TruncatedByLimit` when rows are omitted.
6. WHEN a selector matches neither snapshot THEN TraceMap SHALL emit `SelectorNoMatch` and SHALL NOT fabricate unchanged rows.
7. WHEN selectors apply to disabled scopes THEN TraceMap SHALL record ignored selector/scope combinations in query metadata.
8. WHEN `--scope coverage` is used with combined delegation THEN TraceMap SHALL delegate through the existing combined `sources` scope and filter to coverage rows afterward.
9. WHEN `--scope graph` is used with combined delegation THEN TraceMap SHALL map it to existing combined edge rows.
10. WHEN `--scope contract-shapes`, `gaps`, or `extractors` requests evidence unavailable in the delegated combined diff output THEN TraceMap SHALL emit an availability gap rather than an empty no-diff conclusion.
11. WHEN a snapshot-only scope token is used with combined indexes THEN TraceMap SHALL translate it before invoking shared combined diff helpers and SHALL NOT forward `coverage`, `graph`, `gaps`, `extractors`, or `contract-shapes` to existing combined scope validators.
12. WHEN an unknown scope token is provided THEN TraceMap SHALL fail closed with a clear selector error.

### Requirement 8: Classifications

**User Story:** As a reviewer, I want classifications that separate strong static evidence changes from analysis gaps.

#### Acceptance Criteria

1. WHEN evidence exists only after and coverage is credible on both sides THEN classify `Added`.
2. WHEN evidence exists only before and coverage is credible on both sides THEN classify `Removed`.
3. WHEN evidence exists on both sides with changed safe metadata THEN classify `ChangedEvidence`.
4. WHEN evidence appears only after but before coverage was reduced THEN classify `AddedWithBeforeGap`.
5. WHEN evidence disappears after but after coverage was reduced THEN classify `RemovedWithAfterGap`.
6. WHEN source identity, commit SHA, schema, duplicate identity, extractor version, or analysis gaps prevent a credible conclusion THEN classify `UnknownAnalysisGap`.
7. WHEN evidence is syntax-only, name-only, hash-only, or identity-fallback-based THEN classify no stronger than `NeedsReviewDiff`.
8. WHEN no comparable changes exist under credible coverage THEN emit `NoSnapshotDiffEvidence`.
9. WHEN selector matches nothing THEN emit `SelectorNoMatch`.
10. WHEN confidence is emitted THEN it SHALL derive from the fixed mapping below.

| Classification | Confidence |
| --- | --- |
| `Added`, `Removed`, `ChangedEvidence` with Tier1/Tier2 evidence and credible coverage | `high` |
| `Added`, `Removed`, `ChangedEvidence` with Tier3 evidence | `review` |
| `AddedWithBeforeGap`, `RemovedWithAfterGap` | `medium` |
| `NeedsReviewDiff` | `review` |
| `NoSnapshotDiffEvidence`, `SelectorNoMatch`, `TruncatedByLimit`, `UnknownAnalysisGap` | `unknown` |

### Requirement 9: Markdown Report

**User Story:** As a human reviewer, I want a readable snapshot diff report that is safe to share.

#### Acceptance Criteria

1. WHEN Markdown is emitted THEN sections SHALL be: Summary, Query, Snapshot Identity, Source And Coverage Changes, Endpoint Changes, Contract Shape Changes, Surface Changes, Graph Changes, Analysis Gap Changes, Extractor Version Changes, Path Changes, Gaps, Limitations.
2. WHEN a row is rendered THEN it SHALL show classification, evidence kind, source label, stable identity, before evidence, after evidence, rule IDs, evidence tiers, commit SHAs, and safe file spans where available.
3. WHEN path comparison was not requested THEN the Path Changes section SHALL state that path comparison was not run.
4. WHEN coverage is reduced or identity is unverified THEN affected rows SHALL show caveats near the row.
5. WHEN no diffs exist THEN Markdown SHALL distinguish `NoSnapshotDiffEvidence`, `SelectorNoMatch`, and `UnknownAnalysisGap`.
6. WHEN unsafe values are present THEN Markdown SHALL omit or hash raw SQL, snippets, config values, connection strings, raw URLs, local absolute paths, and private repo names.

### Requirement 10: JSON Report Contract

**User Story:** As an automation author, I want stable JSON so CI and release tooling can consume snapshot diffs.

#### Acceptance Criteria

1. WHEN JSON is emitted THEN top-level fields SHALL include `reportType`, `version`, `reportCoverage`, `query`, `beforeSnapshot`, `afterSnapshot`, `summary`, `sourceDiffs`, `coverageDiffs`, `endpointDiffs`, `contractShapeDiffs`, `surfaceDiffs`, `graphDiffs`, `gapDiffs`, `extractorVersionDiffs`, `pathDiffs`, `gaps`, and `limitations`.
2. WHEN snapshot metadata is emitted THEN it SHALL include source labels, languages, scan IDs, commit SHAs, repo identity hashes where available, coverage, build status, analysis level, extractor versions, and source identity caveats.
3. WHEN a diff row is emitted THEN it SHALL include `diffId`, `stableKey`, `changeType`, `classification`, `confidence`, `evidenceKind`, `sourceLabel`, `before`, `after`, `ruleIds`, `evidenceTiers`, `supportingFactIds`, `supportingEdgeIds`, `supportingPathIds`, `coverageCaveats`, and `notes`.
4. WHEN before or after evidence is absent THEN JSON SHALL use `null` consistently.
5. WHEN arrays are empty THEN JSON SHALL emit empty arrays rather than omitting fields.
6. WHEN metadata maps are emitted THEN keys SHALL be sorted deterministically.
7. WHEN identical inputs and options are used THEN JSON and Markdown SHALL be byte-stable.
8. WHEN the JSON contract changes in a future version THEN the top-level `version` SHALL change.
9. WHEN a top-level array is unavailable because the input schema or delegated engine does not expose that evidence kind THEN the array SHALL remain empty and the report SHALL include an explicit availability gap or limitation.
10. WHEN commit SHAs are emitted or included in stable metadata THEN they SHALL be lowercased deterministically.

### Requirement 11: Rules And Limitations

**User Story:** As a maintainer, I want every snapshot diff conclusion to cite documented rules and limitations.

#### Acceptance Criteria

1. WHEN a snapshot diff row is emitted THEN it SHALL include a snapshot-diff rule ID.
2. WHEN supporting evidence is emitted THEN source fact rule IDs and evidence tiers SHALL be preserved.
3. WHEN gaps are emitted THEN each gap SHALL include rule ID, evidence tier, and limitation text.
4. WHEN new rules are introduced THEN `rules/rule-catalog.yml` SHALL document `snapshot.diff.source.v1`, `snapshot.diff.coverage.v1`, `snapshot.diff.evidence.v1`, `snapshot.diff.identity.v1`, and `snapshot.diff.schema.v1` before implementation merges.
5. WHEN path comparison is delegated to combined diff THEN existing combined path/diff rule IDs SHALL be preserved rather than replaced.
6. WHEN limitations are rendered THEN they SHALL state that snapshot diff compares static indexed evidence only.

### Requirement 12: Tests And Validation

**User Story:** As a maintainer, I want focused tests that prevent snapshot diff from overclaiming.

#### Acceptance Criteria

1. Tests SHALL cover single-index snapshot diff with one changed endpoint or symbol fact.
2. Tests SHALL cover combined-index snapshot diff delegating to existing combined diff projection.
3. Tests SHALL prove single-index repository identity is derived from manifest JSON using the same hashed rendering rules as combined identity.
4. Tests SHALL prove single-index comparison remains safe when language metadata is absent.
5. Tests SHALL prove mixed single/combined input fails clearly.
6. Tests SHALL prove source identity conflicts fail by default.
7. Tests SHALL prove `--allow-identity-mismatch` downgrades classifications, caps confidence at `review`, attaches identity caveats, and redacts raw identity values in errors and reports.
8. Tests SHALL prove unknown commit SHA creates history-dependent gaps.
9. Tests SHALL prove same commit SHA with changed evidence emits `SameCommitShaDivergentEvidence` notes.
10. Tests SHALL prove reduced coverage downgrades added/removed classifications.
11. Tests SHALL prove row ID churn alone does not emit diff rows.
12. Tests SHALL prove changed extractor versions emit extractor-version rows or caveats.
13. Tests SHALL prove malformed metadata emits gaps without crashing.
14. Tests SHALL prove unsafe values do not render in Markdown or JSON.
15. Tests SHALL prove path comparison is opt-in and not implied.
16. Tests SHALL prove combined edge rows map to snapshot graph rows and unavailable snapshot-specific arrays emit availability gaps.
17. Tests SHALL prove `--scope` mapping and filtering behavior for `coverage`, `graph`, `contract-shapes`, `gaps`, and `extractors`, including that snapshot-only scopes are not forwarded to combined validators.
18. Tests SHALL prove confidence mapping is deterministic.
19. Tests SHALL prove byte-stable Markdown and JSON for identical inputs.
20. Tests SHALL prove input SQLite files are opened read-only and are not mutated.
21. Implementation validation SHALL include `dotnet build`, `dotnet test`, `./scripts/check-private-paths.sh`, and `git diff --check`.
