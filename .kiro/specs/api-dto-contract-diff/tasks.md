# API and DTO Contract Diff Tasks

## Implementation Tasks

- [ ] 1. Confirm current API/DTO evidence behavior. Requirements: 1, 2, 3, 4, 5.
  - [ ] Inventory endpoint fact types and route metadata in .NET, TypeScript, JVM, and Python indexes.
  - [ ] Inventory DTO/type/property facts and serializer/schema metadata currently emitted by adapters.
  - [ ] Inventory request/response endpoint attachments currently available or missing.
  - [ ] Gate request/response attachment implementation on credible attachment evidence; otherwise implement gap-only attachment reporting.
  - [ ] Record per-language DTO metadata availability for declared type, nullability, requiredness, and aliases.
  - [ ] Inventory combined-index projections that already expose endpoint and DTO evidence.
  - [ ] Record gaps that affect implementation scope.
  - [ ] Record inventory findings in the implementation PR description or a committed implementation note before row emitters are added.

- [ ] 2. Define report models and rule IDs. Requirements: 6, 7, 9, 10.
  - [ ] Add API/DTO contract diff report models.
  - [ ] Define closed classification set and confidence mapping.
  - [ ] Add rule catalog entries for endpoint, DTO, attachment, identity, coverage, schema, selector, and truncation rules.
  - [ ] Treat rule catalog entries as a merge gate for the first PR that emits any `api.dto.contract.diff.*` row or gap.
  - [ ] Document limitations for OpenAPI, runtime traffic, serializer mapping, binary compatibility, deployment, and auth.

- [ ] 3. Add CLI and output behavior. Requirements: 1, 8, 9.
  - [ ] Add `tracemap contract-diff --before --after --out`.
  - [ ] Add `--format`, `--scope`, `--source`, `--endpoint`, `--type`, `--property`, `--change-kind`, caps, and `--exit-code`.
  - [ ] Define comma-separated `--scope` defaults and closed `--change-kind` values.
  - [ ] Open input SQLite files read-only.
  - [ ] Implement file vs directory output behavior.
  - [ ] Emit deterministic Markdown and JSON skeletons.

- [ ] 4. Implement snapshot validation. Requirements: 2, 6, 7.
  - [ ] Detect single-language vs combined input mode.
  - [ ] Implement a single-language scan-index reader for `facts` plus scan manifest metadata; do not assume combined-index tables exist.
  - [ ] Detect combined mode by `index_sources` and single-language mode by `facts` plus scan manifest metadata.
  - [ ] Reject mixed single/combined mode.
  - [ ] Validate source identity, language, commit SHA, coverage, and extractor versions.
  - [ ] Emit source identity and coverage gaps.
  - [ ] Handle missing optional precision tables without overclaiming.

- [ ] 5. Implement endpoint and route-shape projection. Requirements: 3, 6, 7, 9.
  - [ ] Project endpoint contract rows from facts.
  - [ ] Build stable endpoint identities.
  - [ ] Compare method/path/handler/route parameter metadata.
  - [ ] Add route-shape rows and tests.
  - [ ] Downgrade path-only or syntax-only endpoints.

- [ ] 6. Implement DTO type/property projection. Requirements: 4, 6, 7, 9.
  - [ ] Project DTO type rows.
  - [ ] Project DTO property/member rows.
  - [ ] Compare declared type, required/nullability metadata, and explicit aliases when indexed.
  - [ ] Downgrade generic property-only and syntax-only matches.
  - [ ] Avoid inferring serializer aliases from naming conventions.

- [ ] 7. Implement request/response attachment comparison. Requirements: 5, 6, 7.
  - [ ] Project endpoint-to-request DTO attachments where evidence exists.
  - [ ] Project endpoint-to-response DTO attachments where evidence exists.
  - [ ] Preserve status-code/response-kind metadata when indexed.
  - [ ] Emit gaps when attachment evidence is unavailable due to coverage or missing facts.
  - [ ] If no adapter emits attachment facts in v1, keep this as gap/report behavior and defer changed-attachment rows.

- [ ] 8. Implement combined-index support. Requirements: 1, 2, 3, 4, 5, 6.
  - [ ] Pair combined sources by label.
  - [ ] Reuse existing combined readers and safe rendering helpers where practical.
  - [ ] Keep same display names from different source labels distinct.
  - [ ] Preserve source provenance and supporting fact IDs.
  - [ ] Add source identity conflict tests.

- [ ] 9. Implement selectors, caps, and exit-code behavior. Requirements: 1, 8, 9.
  - [ ] Parse endpoint selectors with method and path key.
  - [ ] Implement source/type/property/scope filters.
  - [ ] Implement closed `--change-kind` parsing and invalid-value errors.
  - [ ] Parse endpoint selectors as `METHOD<whitespace>NORMALIZED_PATH_KEY`; normalize method casing and reject whitespace inside normalized path keys.
  - [ ] Emit selector metadata and `SelectorNoMatch` gaps.
  - [ ] Emit `TruncatedByLimit` when caps are hit.
  - [ ] Implement `--exit-code` semantics for `Added`, `Removed`, and `ChangedEvidence` only.

- [ ] 10. Add safety and determinism tests. Requirements: 9, 11.
  - [ ] Test raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths do not render.
  - [ ] Test Markdown escaping of user-controlled display fields.
  - [ ] Test JSON and Markdown byte-stability.
  - [ ] Test input indexes are not mutated.
  - [ ] Test duplicate identity downgrade, route parameter changes, handler-symbol changes, cap truncation, and attachment-unavailable gaps.
  - [ ] Defer changed request/response attachment tests until at least one adapter emits stable endpoint-to-DTO attachment evidence.

- [ ] 11. Validate. Requirements: 11.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`
  - [ ] Run language adapter smoke checks only if extraction behavior changes.

## Recommended PR Slices

- [ ] PR 1: Evidence inventory, rule catalog entries, models, CLI skeleton, single-index endpoint/DTO projection, Markdown/JSON skeleton.
- [ ] PR 2: Stable identity, classification engine, and single-index tests.
- [ ] PR 3: Selectors, caps, exit-code behavior, and deterministic output tests.
- [ ] PR 4: Combined-index source pairing and combined endpoint/DTO projections.
- [ ] PR 5: Route-shape comparison and request/response attachment gap rendering; changed attachment rows only where indexed evidence exists.
- [ ] PR 6: Safety, byte-stability, and release-review integration hooks.

## Deferred Follow-Ups

- OpenAPI document generation or comparison.
- Binary compatibility analysis.
- Runtime route/traffic/deployment comparison.
- Serializer runtime alias discovery.
- UI visualization of API/DTO contract diffs.
