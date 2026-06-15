# Contract Delta Impact V2 Tasks

## Implementation Tasks

- [x] 1. Confirm current reducer and combined evidence behavior. Requirements: 1, 2, 3, 4.
  - [x] Inspect `ContractDeltaReducer` legacy input parsing, v1 classification behavior, and current Markdown-only output.
  - [x] Inspect reducer-compatible fact keys across .NET, TypeScript, JVM, and Python for type, property, method, endpoint, package, and SQL evidence.
  - [x] Inspect combined report/path/reverse/diff/impact readers that can be reused without adding a second graph traversal or second dependency-surface projection.
  - [x] Identify which matchers are already possible from existing facts and which must emit schema/coverage gaps until future adapter work exists.

- [x] 2. Add rule catalog skeleton before implementation code. Requirements: 6, 9.
  - [x] Add `contract.delta.impact.v2` with emitted finding/gap classifications and documented limitations.
  - [x] Add `contract.delta.input.v2` only if input validation or compatibility gaps use a separate rule boundary.
  - [x] Add `contract.delta.context.v2` only if path/reverse context rows or unavailable gaps use a separate rule boundary.
  - [x] Update `contract.delta.reduce.v1` limitations only if legacy compatibility behavior intentionally changes.
  - [x] Ensure every new finding, gap, or context row has a documented rule ID before code emits it.

- [x] 3. Add v2 input model and legacy compatibility adapter. Requirements: 1, 7, 8, 10.
  - [x] Define `ContractDeltaV2`, change, reference, old/new, source, and metadata models.
  - [x] Detect legacy v1 deltas and adapt them using the documented `Type.member` and name-only mapping rules.
  - [x] Preserve v1 generic/high-fan-out classification behavior unless the implementation deliberately changes it with explicit regression tests and rule limitations.
  - [x] Validate unknown kinds/change types with sanitized errors that do not echo unsafe raw input.
  - [x] Add parser tests for valid v2, legacy v1, malformed JSON, unknown kind, missing reference fields, and ambiguous legacy `Type.member` behavior.

- [x] 4. Add report model and deterministic output. Requirements: 7, 8, 10.
  - [x] Add report models for `ContractDeltaImpactSingleV2` and `ContractDeltaImpactCombinedV2`.
  - [x] Enforce the closed classification set for each report type and never expose both vocabularies as peer classifications in one finding.
  - [x] Add deterministic finding IDs, evidence row IDs, gap IDs, and context row IDs from documented stable inputs.
  - [x] Add `--format`, `--scope`, `--source`, `--change-id`, `--kind`, `--surface`, `--endpoint`, cap flags, and `--exit-code` behavior.
  - [x] Add directory/file output behavior matching existing report conventions, including directory output writing both Markdown and JSON.
  - [x] Add byte-stability tests for Markdown and JSON, including array ordering, metadata ordering, empty arrays, and `null` scalar handling.
  - [x] Add unsafe-value redaction tests for raw SQL, snippets, config values, connection strings, raw URLs, and local absolute paths.

- [x] 5. Implement single-index matchers. Requirements: 2, 4, 6, 10.
  - [x] Match type references against semantic/syntax type evidence and symbols.
  - [x] Match property references against property/member/DTO/serializer/database column evidence.
  - [x] Match method references against declarations, invocations, call edges, and parameter-forward evidence.
  - [x] Match endpoint references against route/client endpoint facts using exact `normalizedPathKey` plus method for strong matches.
  - [x] Match package references against manifest/package/config/import/usage facts where present.
  - [x] Match SQL/schema references against SQL-shape, table, column, ORM, and database mapping facts where present without rendering raw SQL.
  - [x] Add tests for classification ordering, generic-name downgrade, high-fan-out downgrade, endpoint method mismatch, and SQL field matching.

- [x] 6. Implement coverage and gap handling. Requirements: 2, 3, 6, 9, 10.
  - [x] Preserve scan coverage, commit SHA, extractor versions, build status, and known gaps in findings.
  - [x] Enforce all `NoEvidenceFullCoverage` preconditions.
  - [x] Emit `NoEvidenceReducedCoverage` or `UnknownAnalysisGap` when reduced coverage or relevant gaps prevent a credible absence conclusion.
  - [x] Treat missing commit SHA as a coverage/identity caveat, not a clean no-evidence result.
  - [x] Add tests for reduced coverage, relevant `AnalysisGap` facts, failed/partial builds, missing commit SHA, and gap-only `--exit-code` behavior.

- [x] 7. Implement combined-index support. Requirements: 3, 4, 6, 8, 10.
  - [x] Detect combined indexes and load source provenance from existing combined source tables.
  - [x] Match contract references against `combined_facts`, `combined_symbols`, `combined_fact_symbols`, and existing computed dependency surfaces.
  - [x] Map SQL query references to existing combined surface fields: `queryShapeHash` to `ShapeHash`, `textHash` to `TextHash`, `tableName` to `TableName`, and `columnNames` to `ColumnNames`.
  - [x] Preserve source labels, source index IDs, repo identity hash, scan ID, commit SHA, language, analysis level, and build status.
  - [x] Downgrade findings for identity conflicts, duplicate identities, missing optional precision schema, hash-only surfaces, and source coverage caveats.
  - [x] Add tests for multiple-source grouping, source identity caveats, duplicate stable identities, hash-only surfaces, and selector no-match behavior.

- [x] 8. Add optional combined-only path/reverse context. Requirements: 5, 6, 9, 10.
  - [x] Reject `--include-paths` and `--include-reverse` for single-language indexes with clear errors in v2.
  - [x] Derive path selectors only from matched endpoints, symbol-identity-backed matches, and stable-key dependency surfaces.
  - [x] Derive reverse selectors only from matched dependency surfaces with stable identity.
  - [x] Reuse existing combined path/reverse query services without mutating indexes.
  - [x] Enforce caps for paths, reverse roots, evidence rows, gaps, and total context queries.
  - [x] Emit `PathContextUnavailable`, `ReverseContextUnavailable`, `TruncatedByLimit`, or `UnknownAnalysisGap` where context cannot be safely gathered.
  - [x] Add tests proving name-only, syntax-only, generic, high-fan-out, and ambiguous matches do not seed traversal.

- [x] 9. Add samples and validation docs. Requirements: 1, 10.
  - [x] Add v2 sample deltas for type, property, method, endpoint, package, SQL/table/column/query, and dependency-surface references where useful.
  - [x] Update validation docs with reduce v2 commands and expected classifications.
  - [x] Document single-index vs combined-index behavior, path/reverse availability, source identity caveats, and coverage caveats.
  - [x] Add acceptance notes for the first supported matcher set and the intentionally deferred matcher set.

- [x] 10. Validate implementation. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] Language adapter validation if facts or adapter behavior change.
  - [x] Combined smoke checks if path/reverse/combined behavior changes.
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Recommended PR Slices

- [x] PR 1: Rule catalog skeleton, v2 input model, legacy adapter, report shell, closed classification vocabularies, deterministic JSON/Markdown, and v1 byte-stability guard.
- [x] PR 2: Single-index type/property/method matchers, coverage/gap handling, and no-evidence preconditions.
- [x] PR 3: Single-index endpoint/package/SQL/schema/dependency-surface matchers.
- [x] PR 4: Combined-index matching, source provenance, dependency-surface reuse, and identity/coverage caveats.
- [x] PR 5: Optional combined-only path/reverse context with stable selector derivation and caps.
- [x] PR 6: Samples, validation docs, acceptance docs, and cross-language hardening.

## Deferred Follow-Ups

- Runtime telemetry correlation.
- Business criticality inputs.
- Vulnerability/package advisory enrichment.
- Full taint/dataflow engine.
- Single-index path/reverse traversal.
- Hosted dashboard or HTML explorer.
