# SQL Schema Change Impact Tasks

## Implementation Tasks

- [x] 1. Confirm current SQL impact inputs and evidence seams. Requirements: 1, 2, 3, 4.
  - [x] Inspect combined `sql-query` and `sql-persistence` surface readers.
  - [x] Inspect existing contract-delta v2 and combined impact readers for reusable input/report models.
  - [x] Record seam gaps in the first implementation PR body under a "Seams checked" section before adding new behavior.

- [x] 2. Add SQL/schema delta input model and validator initial slice. Requirements: 1, 7, 8, 9.
  - [x] Require explicit change `id` values in v1.
  - [x] Validate allowed `kind` and `changeType` values.
  - [x] Validate unknown version, duplicate IDs, missing IDs, and empty changes.
  - [x] Reject or gap unsafe raw SQL/free-text references without echoing unsafe values.
  - [x] Normalize valid SQL/schema changes into contract-delta impact selectors.
  - [x] Reject `--contract-delta` and `--sql-schema-delta` together with a clear mutual-exclusion error.

- [x] 3. Implement single-index SQL/schema matching initial slice. Requirements: 2, 4, 6, 8.
  - [x] Match table references against SQL-shape and mapping facts.
  - [x] Match column references against column/field/mapped/member metadata.
  - [x] Match query-shape references against `queryShapeHash` and `textHash`.
  - [x] Match sql-file references against `SqlFileDeclared`, `SqlTextUsed`, and SQL-shape source-kind metadata.
  - [x] Separate query evidence, text-hash evidence, SQL resource evidence, and mapping evidence.
  - [x] Keep schema-only, table-only, mappedName-only, and hash-only matches review-tier.

- [x] 4. Implement combined-index SQL/schema matching initial slice. Requirements: 3, 4, 6, 8.
  - [x] Include `sql-persistence` by default for table, column, mapping, and persistence-surface changes.
  - [x] Preserve source labels, commit SHAs, scan IDs, and coverage warnings.
  - [x] Verify `--sql-schema-delta` accepts combined indexes through `--index`.

- [x] 5. Add optional path and reverse context boundaries. Requirements: 5, 6, 8.
  - [x] Reject path/reverse flags against single-language indexes.
  - [x] Emit `PathContextUnavailable` or `ReverseContextUnavailable` for unstable matches.

- [x] 6. Add classifications and confidence mapping initial slice. Requirements: 4, 6, 9.
  - [x] Implement single-index classifications.
  - [x] Implement combined-index classifications.
  - [x] Add fixed confidence mapping from classification.

- [x] 7. Add Markdown and JSON reports. Requirements: 7, 8, 9.
  - [x] Emit deterministic Markdown sections.
  - [x] Emit deterministic JSON schema.
  - [x] Use `sql-impact-report.md` and `sql-impact-report.json` for SQL/schema directory output while preserving existing contract-delta file names.
  - [x] Introduce `SqlSchemaChangeImpactSingleV1` and `SqlSchemaChangeImpactCombinedV1` as new JSON models rather than changing the current Markdown-only impact record.
  - [x] Include rule IDs, evidence tiers, file spans, commit SHAs, extractor versions, supporting IDs, and limitations.
  - [x] Omit or hash unsafe metadata.
  - [x] Add byte-stability tests, including volatile identity caveats.

- [x] 8. Update rules and documentation during implementation. Requirements: 9.
  - [x] Add `contract.delta.input.v2`, `contract.delta.impact.v2`, and `contract.delta.context.v2` to `rules/rule-catalog.yml` if they are not already present.
  - [x] Reuse `contract.delta.input.v2`, `contract.delta.impact.v2`, and `contract.delta.context.v2` by default.
  - [x] Add `sql.schema.*` rules only if implementation proves dedicated rules are needed.
  - [x] Document limitations for any new rule before code merges.
  - [x] Ensure supporting adapter and combined rule IDs are preserved.
  - [x] Document static SQL/schema impact limitations in the command help or report output.

- [x] 9. Validate implementation. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

## Spec PR Tasks

- [x] Create `.kiro/specs/sql-schema-change-impact/requirements.md`.
- [x] Create `.kiro/specs/sql-schema-change-impact/design.md`.
- [x] Create `.kiro/specs/sql-schema-change-impact/tasks.md`.
- [x] Create `.kiro/specs/sql-schema-change-impact/review-prompts.md`.
- [x] Run Opus spec review.
- [x] Run Sonnet spec review.
- [x] Patch Medium+ review findings.
- [x] Validate spec-only file scope.
- [x] Push ready PR and post `@codex review`.

## Recommended PR Slices

- [x] PR 1: Input model, validator, SQL-to-contract selector normalization, report skeleton, and reused rule IDs.
- [x] PR 2: Single-index SQL/schema matching.

## Deferred Follow-Ups

- Inspect SQL-shape fact properties across .NET, TypeScript, JVM, and Python.
- Inspect `DatabaseColumnMapping`, mapped-name properties, and ORM mapping facts across adapters.
- Project safe `old`, `new`, and metadata contracts into reports where useful.
- Add SQL-to-contract selector normalization tests for every SQL/schema kind.
- Add tests for valid schema, table, column, query-shape, mapping, sql-file, persistence-surface, missing-ID, and invalid input.
- Match schema references against safe schema metadata where available.
- Cover `added` with no evidence and `removed` with multiple evidence rows.
- Deduplicate matched facts deterministically.
- Add reduced/full coverage no-evidence tests.
- Reuse combined surface projection for `sql-query`.
- Reuse combined surface projection for `sql-persistence`.
- Preserve hash-only, volatile identity, duplicate identity, and schema caveats.
- Add tests for schema/table/column/query-shape matches across multiple source labels.
- Derive stable path/reverse selectors only from safe SQL surface identity.
- Reuse existing combined paths/reverse readers.
- Emit per-finding context gaps and summary partial coverage when only some changes have stable selectors.
- Emit `UnknownAnalysisGap` when no path exists under reduced coverage.
- Add cap/truncation tests.
- Downgrade schema-only, hash-only, table-only, mappedName-only, unlinked, duplicate, volatile, and reduced-coverage evidence.
- Add tests for `sql-schema-metadata` evidence labels and mapping identity fallback when any key component is unsafe.
- Add tests proving no overclaiming from mapping-only evidence.
- Add tests proving languages without Tier1 SQL mapping evidence do not emit `DefiniteImpact`.
- Add path resolution tests for file output, default directory output, and directory `--format json` precedence.
- Fail tests if any emitted finding or gap cites a missing rule ID.
- Run affected TypeScript/JVM/Python tests if adapter facts or fixtures change.
- Run relevant combined path/reverse smoke checks if path/reverse behavior changes.
- PR 3: Combined `sql-query` and `sql-persistence` matching.
- PR 4: Optional path/reverse context.
- PR 5: Report polish, byte-stability, public sample validation.
- Dialect-specific SQL parser modules.
- Migration file execution or migration graph modeling.
- Database introspection.
- Schema ownership/team metadata.
- Runtime telemetry correlation.
- Query-plan or performance impact.
