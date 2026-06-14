# Snapshot Diff By Commit SHA Tasks

## Implementation Tasks

- [x] 1. Confirm current snapshot metadata and diff behavior. Requirements: 1, 2, 3, 6.
  - [x] Inspect single-index manifest/fact/schema metadata for repo identity, commit SHA, coverage, and extractor versions.
  - [x] Inspect combined `index_sources` metadata and existing combined diff source pairing.
  - [x] Confirm existing `tracemap diff` and `tracemap impact` output contracts.
  - [x] Identify reusable safe-path, Markdown, JSON, metadata sorting, and output writer helpers.

- [x] 2. Add command and option model. Requirements: 1, 7, 9, 10.
  - [x] Add `tracemap snapshot-diff --before <path> --after <path> --out <path>`.
  - [x] Add `--format`, `--scope`, selectors, caps, `--include-paths`, `--allow-identity-mismatch`, and `--exit-code`.
  - [x] Implement snapshot-to-combined scope mapping for `coverage` and `graph`.
  - [x] Validate and translate snapshot-only scopes before invoking combined diff helpers.
  - [x] Emit availability gaps for unavailable `contract-shapes`, `gaps`, and `extractors` scopes.
  - [x] Validate mixed single/combined inputs fail clearly in v1.
  - [x] Preserve read-only SQLite input handling.
  - [x] Add initial snapshot diff rule catalog entries before any implementation PR past command/input validation merges.

- [x] 3. Implement snapshot input detection and validation initial slice. Requirements: 1, 2, 3.
  - [x] Detect single-language indexes.
  - [x] Detect combined indexes.
  - [x] Parse single-index manifest JSON for `RemoteUrl`, `RepoName`, `ScanRootPathHash`, `GitRootHash`, commit SHA, and extractor metadata.
  - [x] Derive repository identity as `repo-hash:{Hash(RemoteUrl ?? RepoName)}` where available.
  - [x] Derive the single-index source label as the constant `single`.
  - [x] Treat single-index language as optional metadata when no first-class value exists.
  - [x] Validate combined source identity and language.
  - [x] Redact raw URLs, repository names, local roots, and private paths from identity errors and reports.
  - [x] Validate known commit SHAs for history-dependent conclusions.
  - [x] Emit identity, schema, and coverage gaps.

- [x] 4. Build single-index projector initial slice. Requirements: 4, 5, 8.
  - [x] Project source and coverage records.
  - [x] Project analysis-gap availability and extractor-version records.

- [x] 5. Reuse combined diff for combined indexes. Requirements: 3, 4, 5, 6.
  - [x] Expose or reuse no-write combined read/comparison helpers rather than copying comparison logic.
  - [x] Delegate endpoint/surface/edge/path comparison to existing combined diff where possible.
  - [x] Map combined `SourceDiffs`, `CoverageDiffs`, `EndpointDiffs`, `SurfaceDiffs`, `EdgeDiffs`, `PathDiffs`, and `Gaps` into snapshot sections.
  - [x] Reapply snapshot redaction rules to delegated metadata before rendering Markdown or JSON.
  - [x] Add snapshot-specific source/commit/extractor validation around delegated output.
  - [x] Preserve combined diff rule IDs as supporting rules.
  - [x] Ensure `--allow-identity-mismatch` downgrades affected rows.

- [x] 6. Implement stable keys and classifications. Requirements: 5, 8.
  - [x] Add stable key helpers for source, coverage, and extractor-version evidence.
  - [x] Ensure row ID churn alone does not emit diffs for source, coverage, and extractor-version evidence.
  - [x] Detect changed safe metadata under source identity.
  - [x] Add duplicate identity handling.
  - [x] Add coverage-aware classification downgrades.
  - [x] Add deterministic confidence mapping.

- [x] 7. Implement output writers. Requirements: 9, 10, 11.
  - [x] Emit deterministic Markdown with required section order.
  - [x] Emit deterministic JSON with required top-level fields.
  - [x] Sort all arrays and metadata.
  - [x] Use shared redaction and Markdown escaping helpers.
  - [x] Avoid timestamps and machine-local paths.

- [x] 8. Complete rule catalog documentation pass. Requirements: 11.
  - [x] Confirm PR 1 already added the initial rule catalog entries before feature behavior merged.
  - [x] Add `snapshot.diff.source.v1`.
  - [x] Add `snapshot.diff.coverage.v1`.
  - [x] Add `snapshot.diff.evidence.v1`.
  - [x] Add `snapshot.diff.identity.v1`.
  - [x] Add `snapshot.diff.schema.v1`.
  - [x] Document limitations for static evidence, coverage, identity, schema, and extractor-version caveats.
  - [x] Document availability gaps for evidence kinds not exposed by an input schema or delegated engine.

- [x] 9. Add focused tests for implemented slice. Requirements: 12.
  - [x] Single-index evidence change.
  - [x] Combined-index delegation.
  - [x] Single-index manifest JSON identity derivation.
  - [x] Single-index source label is the constant `single` and never derived from path or raw repo metadata.
  - [x] Single-index comparison with missing language metadata.
  - [x] Mixed single/combined rejection.
  - [x] Combined edge-to-graph output mapping.
  - [x] Delegated combined metadata is re-redacted before snapshot output.
  - [x] Unavailable snapshot-specific arrays emitting availability gaps.
  - [x] Source identity conflict and allowed mismatch downgrade.
  - [x] Redacted identity conflict errors and reports.
  - [x] Unknown commit SHA.
  - [x] Reduced coverage downgrade.
  - [x] Row ID churn avoidance.
  - [x] Extractor-version change.
  - [x] Unsafe value redaction.
  - [x] Path comparison opt-in.
  - [x] `--scope` mapping for `coverage`, `graph`, `contract-shapes`, `gaps`, and `extractors`.
  - [x] Deterministic confidence mapping.
  - [x] Byte-stable Markdown/JSON.
  - [x] Read-only input databases.

- [x] 10. Update user-facing docs only during implementation. Requirements: 1, 6, 9.
  - [x] Add README/acceptance/validation notes in implementation PR, not this spec PR.
  - [x] Update `docs/ACCEPTANCE.md` and `docs/VALIDATION.md` when implementation merges.
  - [x] Document relationship to `tracemap diff`, `impact`, and future release review.
  - [x] Include examples that use existing index artifacts rather than Git checkout orchestration.

- [x] 11. Validate implementation. Requirements: 12.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Recommended PR Slices

- [x] PR 1: Command shell, input detection, source/commit validation, rule catalog, and no-op report.
- [x] PR 2: Combined-index delegation, section mapping, scope mapping, and availability gaps.
- PR 3: Single-index projector and evidence diff output.
- PR 4: Graph/surface/contract-shape expansion, extractor rows, and redaction hardening.
- PR 5: Optional path comparison and release-review integration.

## Completed Follow-Up Slices

- [x] `codex/snapshot-diff-single-index-followups`: single-index endpoint projection for `HttpRouteBinding` and `HttpCallDetected` facts, single-index dependency-surface projection for safe surface facts already supported by combined surface readers, malformed manifest/properties metadata gaps, and same-SHA divergent evidence notes.
- [x] Added focused tests for single-index endpoint/surface projection, same-SHA changed endpoint notes, malformed metadata gaps, and updated endpoint availability expectations.
- [x] Kept graph and contract-shape projectors deferred to avoid widening this PR beyond the endpoint/surface follow-up slice.

## Deferred Follow-Ups

- Project contract-shape records for type/property/method/DTO evidence.
- Project graph records for call edges, object creations, symbol relationships, argument flows, and parameter forwarding where available.
- Expand dependency-surface projection if future adapters add storage or event/message facts beyond the current combined surface reader vocabulary.
- Project single-index analysis-gap diffs from `AnalysisGap` facts rather than only emitting coverage and malformed metadata gaps.
- Add duplicate-identity edge-case tests for single-index endpoint and surface records.
- Run relevant adapter tests if projector behavior touches language-specific outputs.
- Git checkout orchestration from commit SHAs.
- Source patch summarization.
- Runtime telemetry comparison.
- Release review policy gates.
- HTML graph/report viewer.
