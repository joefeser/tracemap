# Snapshot Diff By Commit SHA Tasks

## Implementation Tasks

- [ ] 1. Confirm current snapshot metadata and diff behavior. Requirements: 1, 2, 3, 6.
  - [ ] Inspect single-index manifest/fact/schema metadata for repo identity, commit SHA, coverage, and extractor versions.
  - [ ] Inspect combined `index_sources` metadata and existing combined diff source pairing.
  - [ ] Confirm existing `tracemap diff` and `tracemap impact` output contracts.
  - [ ] Identify reusable safe-path, Markdown, JSON, metadata sorting, and output writer helpers.

- [ ] 2. Add command and option model. Requirements: 1, 7, 9, 10.
  - [ ] Add `tracemap snapshot-diff --before <path> --after <path> --out <path>`.
  - [ ] Add `--format`, `--scope`, selectors, caps, `--include-paths`, `--allow-identity-mismatch`, and `--exit-code`.
  - [ ] Implement snapshot-to-combined scope mapping for `coverage` and `graph`.
  - [ ] Validate and translate snapshot-only scopes before invoking combined diff helpers.
  - [ ] Emit availability gaps for unavailable `contract-shapes`, `gaps`, and `extractors` scopes.
  - [ ] Validate mixed single/combined inputs fail clearly in v1.
  - [ ] Preserve read-only SQLite input handling.
  - [ ] Add initial snapshot diff rule catalog entries before any implementation PR past command/input validation merges.

- [ ] 3. Implement snapshot input detection and validation. Requirements: 1, 2, 3.
  - [ ] Detect single-language indexes.
  - [ ] Detect combined indexes.
  - [ ] Parse single-index manifest JSON for `RemoteUrl`, `RepoName`, `ScanRootPathHash`, `GitRootHash`, commit SHA, and extractor metadata.
  - [ ] Derive repository identity as `repo-hash:{Hash(RemoteUrl ?? RepoName)}` where available.
  - [ ] Treat single-index language as optional metadata when no first-class value exists.
  - [ ] Validate combined source identity and language.
  - [ ] Redact raw URLs, repository names, local roots, and private paths from identity errors and reports.
  - [ ] Validate known commit SHAs for history-dependent conclusions.
  - [ ] Emit identity, schema, malformed metadata, and coverage gaps.

- [ ] 4. Build single-index projector. Requirements: 4, 5, 8.
  - [ ] Project source and coverage records.
  - [ ] Project endpoint records.
  - [ ] Project contract-shape records for type/property/method/DTO evidence.
  - [ ] Project dependency-surface records for SQL, package, HTTP, config, storage, and event/message evidence where available.
  - [ ] Project graph records for call edges, object creations, symbol relationships, argument flows, and parameter forwarding where available.
  - [ ] Project analysis-gap and extractor-version records.
  - [ ] Use single-index endpoint keys shaped as `endpoint:{sourceLabel}:{endpointKind}:{normalizedMethod}:{normalizedPathKey}:{handlerIdentityOrNone}`.

- [ ] 5. Reuse combined diff for combined indexes. Requirements: 3, 4, 5, 6.
  - [ ] Expose or reuse no-write combined read/comparison helpers rather than copying comparison logic.
  - [ ] Delegate endpoint/surface/edge/path comparison to existing combined diff where possible.
  - [ ] Map combined `SourceDiffs`, `CoverageDiffs`, `EndpointDiffs`, `SurfaceDiffs`, `EdgeDiffs`, `PathDiffs`, and `Gaps` into snapshot sections.
  - [ ] Add snapshot-specific source/commit/extractor validation around delegated output.
  - [ ] Preserve combined diff rule IDs as supporting rules.
  - [ ] Ensure `--allow-identity-mismatch` downgrades affected rows.

- [ ] 6. Implement stable keys and classifications. Requirements: 5, 8.
  - [ ] Add stable key helpers for each evidence kind.
  - [ ] Ensure row ID churn alone does not emit diffs.
  - [ ] Detect changed safe metadata under stable identity.
  - [ ] Add duplicate identity handling.
  - [ ] Add coverage-aware classification downgrades.
  - [ ] Add deterministic confidence mapping.

- [ ] 7. Implement output writers. Requirements: 9, 10, 11.
  - [ ] Emit deterministic Markdown with required section order.
  - [ ] Emit deterministic JSON with required top-level fields.
  - [ ] Sort all arrays and metadata.
  - [ ] Use shared redaction and Markdown escaping helpers.
  - [ ] Avoid timestamps and machine-local paths.

- [ ] 8. Complete rule catalog documentation pass. Requirements: 11.
  - [ ] Confirm PR 1 already added the initial rule catalog entries before feature behavior merged.
  - [ ] Add `snapshot.diff.source.v1`.
  - [ ] Add `snapshot.diff.coverage.v1`.
  - [ ] Add `snapshot.diff.evidence.v1`.
  - [ ] Add `snapshot.diff.identity.v1`.
  - [ ] Add `snapshot.diff.schema.v1`.
  - [ ] Document limitations for static evidence, coverage, identity, schema, and extractor-version caveats.
  - [ ] Document availability gaps for evidence kinds not exposed by an input schema or delegated engine.

- [ ] 9. Add focused tests. Requirements: 12.
  - [ ] Single-index evidence change.
  - [ ] Combined-index delegation.
  - [ ] Single-index manifest JSON identity derivation.
  - [ ] Single-index comparison with missing language metadata.
  - [ ] Mixed single/combined rejection.
  - [ ] Combined edge-to-graph output mapping.
  - [ ] Unavailable snapshot-specific arrays emitting availability gaps.
  - [ ] Source identity conflict and allowed mismatch downgrade.
  - [ ] Redacted identity conflict errors and reports.
  - [ ] Unknown commit SHA.
  - [ ] Same SHA with changed evidence warning.
  - [ ] Reduced coverage downgrade.
  - [ ] Row ID churn avoidance.
  - [ ] Extractor-version change.
  - [ ] Malformed metadata gap.
  - [ ] Unsafe value redaction.
  - [ ] Path comparison opt-in.
  - [ ] `--scope` mapping for `coverage`, `graph`, `contract-shapes`, `gaps`, and `extractors`.
  - [ ] Deterministic confidence mapping.
  - [ ] Byte-stable Markdown/JSON.
  - [ ] Read-only input databases.

- [ ] 10. Update user-facing docs only during implementation. Requirements: 1, 6, 9.
  - [ ] Add README/acceptance/validation notes in implementation PR, not this spec PR.
  - [ ] Update `docs/ACCEPTANCE.md` and `docs/VALIDATION.md` when implementation merges.
  - [ ] Document relationship to `tracemap diff`, `impact`, and future release review.
  - [ ] Include examples that use existing index artifacts rather than Git checkout orchestration.

- [ ] 11. Validate implementation. Requirements: 12.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] Relevant adapter tests if projector behavior touches language-specific outputs.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Recommended PR Slices

- [ ] PR 1: Command shell, input detection, source/commit validation, rule catalog, and no-op report.
- [ ] PR 2: Combined-index delegation, section mapping, scope mapping, and availability gaps.
- [ ] PR 3: Single-index projector and evidence diff output.
- [ ] PR 4: Graph/surface/contract-shape expansion, extractor rows, and redaction hardening.
- [ ] PR 5: Optional path comparison and release-review integration.

## Deferred Follow-Ups

- Git checkout orchestration from commit SHAs.
- Source patch summarization.
- Runtime telemetry comparison.
- Release review policy gates.
- HTML graph/report viewer.
