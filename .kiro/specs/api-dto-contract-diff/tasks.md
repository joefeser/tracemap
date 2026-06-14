# API and DTO Contract Diff Tasks

## Implementation Tasks

- [x] 1. Confirm current API/DTO evidence behavior. Requirements: 1, 2, 3, 4, 5.
  - [x] Inventory endpoint fact types and route metadata in .NET, TypeScript, JVM, and Python indexes.
  - [x] Inventory DTO/type/property facts and serializer/schema metadata currently emitted by adapters.
  - [x] Inventory request/response endpoint attachments currently available or missing.
  - [x] Gate request/response attachment implementation on credible attachment evidence; otherwise implement gap-only attachment reporting.
  - [x] Record per-language DTO metadata availability for declared type, nullability, requiredness, and aliases.
  - [x] Inventory combined-index projections that already expose endpoint and DTO evidence.
  - [x] Record gaps that affect implementation scope.
  - [x] Record inventory findings in the implementation PR description or a committed implementation note before row emitters are added.

- [x] 2. Define report models and rule IDs. Requirements: 6, 7, 9, 10.
  - [x] Add API/DTO contract diff report models.
  - [x] Define closed classification set and confidence mapping.
  - [x] Add rule catalog entries for endpoint, DTO, attachment, identity, coverage, schema, selector, and truncation rules.
  - [x] Treat rule catalog entries as a merge gate for the first PR that emits any `api.dto.contract.diff.*` row or gap.
  - [x] Document limitations for OpenAPI, runtime traffic, serializer mapping, binary compatibility, deployment, and auth.

- [x] 3. Add CLI and output behavior. Requirements: 1, 8, 9.
  - [x] Add `tracemap contract-diff --before --after --out`.
  - [x] Add `--format`, `--scope`, `--source`, `--endpoint`, `--type`, `--property`, `--change-kind`, caps, and `--exit-code`.
  - [x] Define comma-separated `--scope` defaults and closed `--change-kind` values.
  - [x] Open input SQLite files read-only.
  - [x] Implement file vs directory output behavior.
  - [x] Emit deterministic Markdown and JSON skeletons.

- [x] 4. Implement snapshot validation. Requirements: 2, 6, 7.
  - [x] Detect single-language vs combined input mode.
  - [x] Implement a single-language scan-index reader for `facts` plus scan manifest metadata; do not assume combined-index tables exist.
  - [x] Detect combined mode by `index_sources` and single-language mode by `facts` plus scan manifest metadata.
  - [x] Reject mixed single/combined mode.
  - [x] Validate source identity, language, commit SHA, coverage, and extractor versions.
  - [x] Emit source identity and coverage gaps.
  - [x] Handle missing optional precision tables without overclaiming.

- [x] 5. Implement endpoint and route-shape projection. Requirements: 3, 6, 7, 9.
  - [x] Project endpoint contract rows from facts.
  - [x] Build stable endpoint identities.
  - [x] Compare method/path/handler/route parameter metadata.
  - [x] Add route-shape rows and tests.
  - [x] Downgrade path-only or syntax-only endpoints.

- [x] 6. Implement DTO type/property projection. Requirements: 4, 6, 7, 9.
  - [x] Project DTO type rows.
  - [x] Project DTO property/member rows.
  - [x] Compare declared type, required/nullability metadata, and explicit aliases when indexed.
  - [x] Downgrade generic property-only and syntax-only matches.
  - [x] Avoid inferring serializer aliases from naming conventions.

- [x] 7. Implement request/response attachment comparison. Requirements: 5, 6, 7.
  - [x] Project endpoint-to-request DTO attachments where evidence exists.
  - [x] Project endpoint-to-response DTO attachments where evidence exists.
  - [x] Preserve status-code/response-kind metadata when indexed.
  - [x] Emit gaps when attachment evidence is unavailable due to coverage or missing facts.
  - [x] If no adapter emits attachment facts in v1, keep this as gap/report behavior and defer changed-attachment rows.

- [x] 8. Implement combined-index support. Requirements: 1, 2, 3, 4, 5, 6.
  - [x] Pair combined sources by label.
  - [x] Reuse existing combined readers and safe rendering helpers where practical.
  - [x] Keep same display names from different source labels distinct.
  - [x] Preserve source provenance and supporting fact IDs.
  - [x] Add source identity conflict tests.

- [x] 9. Implement selectors, caps, and exit-code behavior. Requirements: 1, 8, 9.
  - [x] Parse endpoint selectors with method and path key.
  - [x] Implement source/type/property/scope filters.
  - [x] Implement closed `--change-kind` parsing and invalid-value errors.
  - [x] Parse endpoint selectors as `METHOD<whitespace>NORMALIZED_PATH_KEY`; normalize method casing and reject whitespace inside normalized path keys.
  - [x] Emit selector metadata and `SelectorNoMatch` gaps.
  - [x] Emit `TruncatedByLimit` when caps are hit.
  - [x] Implement `--exit-code` semantics for `Added`, `Removed`, and `ChangedEvidence` only.

- [x] 10. Add safety and determinism tests. Requirements: 9, 11.
  - [x] Test raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths do not render.
  - [x] Test Markdown escaping of user-controlled display fields.
  - [x] Test JSON and Markdown byte-stability.
  - [x] Test input indexes are not mutated.
  - [x] Test duplicate identity downgrade, route parameter changes, handler-symbol changes, cap truncation, and attachment-unavailable gaps.
  - [x] Defer changed request/response attachment tests until at least one adapter emits stable endpoint-to-DTO attachment evidence.

- [x] 11. Validate. Requirements: 11.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`
  - [x] Run language adapter smoke checks only if extraction behavior changes.

## Recommended PR Slices

- [x] PR 1: Evidence inventory, rule catalog entries, models, CLI skeleton, single-index endpoint/DTO projection, Markdown/JSON skeleton.
- [x] PR 2: Stable identity, classification engine, and single-index tests.
- [x] PR 3: Selectors, caps, exit-code behavior, and deterministic output tests.
- [x] PR 4: Combined-index source pairing and combined endpoint/DTO projections.
- [x] PR 5: Route-shape comparison and request/response attachment gap rendering; changed attachment rows only where indexed evidence exists.
- [x] PR 6: Safety, byte-stability, and release-review integration hooks.

## Deferred Follow-Ups

- OpenAPI document generation or comparison.
- Binary compatibility analysis.
- Runtime route/traffic/deployment comparison.
- Serializer runtime alias discovery.
- UI visualization of API/DTO contract diffs.
