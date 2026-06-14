# SQL Dependency Surfaces Tasks

## Implementation Tasks

- [x] 1. Confirm current SQL evidence behavior. Requirements: 1, 3, 4, 5, 6, 7.
  - [ ] Inspect .NET SQL text, Dapper, ADO.NET, EF raw SQL, and `.sql` file facts.
  - [ ] Inspect TypeScript Prisma/Base44 query-builder facts and direct SQL coverage gaps.
  - [ ] Inspect JVM SQL resource, JDBC, and JPA facts.
  - [ ] Inspect Python SQL-shape behavior and tests.
  - [ ] Inspect combined `sql-query` and `sql-persistence` surface projection, path, reverse, diff, and impact behavior.

- [x] 2. Add shared SQL-shape extraction contract. Requirements: 1, 2, 8, 9.
  - [ ] Define supported operations and unsupported constructs.
  - [ ] Define Python-compatible normalized masked SQL text as the v1 `queryShapeHash` input.
  - [ ] Define safe table/column identifier extraction rules.
  - [ ] Add shared golden fixtures for text hash, shape hash, operation, safe identifiers, shape-hash-only `WITH`/CTE behavior, and unsupported/dynamic no-shape cases.
  - [ ] Add normalization edge-case fixtures for escaped quotes, multiline comments, CRLF/tabs, trailing semicolons, `--` inside literals, and double-quoted identifiers.
  - [ ] Add unit tests for supported shapes.
  - [ ] Add unit tests for unsupported shapes that produce no table/column overclaim.

- [x] 3. Implement .NET SQL-shape backfill. Requirements: 1, 2, 3, 8, 9.
  - [ ] Add `database.sql.shape.v1` to the rule catalog with normalization and static-evidence limitations.
  - [ ] Add or reuse a .NET SQL shape helper.
  - [ ] Emit SQL-shape `QueryPatternDetected` for simple `.sql` files.
  - [ ] Emit SQL-shape `QueryPatternDetected` for supported Dapper/ADO.NET/EF raw SQL literals.
  - [ ] Set SQL code facts' `targetSymbol` to containing method/class/file symbols when available.
  - [ ] Preserve existing SQL facts.
  - [ ] Add EF raw SQL literal shape tests.
  - [ ] Add raw SQL suppression tests.
  - [ ] Add dynamic/unsupported SQL no-overclaim tests.

- [x] 4. Implement TypeScript direct SQL surfaces. Requirements: 1, 2, 4, 8, 9.
  - [ ] Add `typescript.integration.sql.v1` to the rule catalog with limitations before emitting new direct SQL evidence.
  - [ ] Preserve Prisma/Base44 query-builder facts without `sqlSourceKind`.
  - [ ] Detect simple direct SQL literals in supported client calls.
  - [ ] Detect supported static tagged SQL literals.
  - [ ] Emit `SqlTextUsed` and SQL-shape `QueryPatternDetected` where safe.
  - [ ] Emit dynamic boundary evidence or gaps without raw SQL.
  - [ ] Add query-builder regression tests proving Prisma/Base44 facts do not gain `sqlSourceKind`.

- [x] 5. Implement JVM SQL-shape backfill. Requirements: 1, 2, 5, 8, 9.
  - [ ] Emit SQL-shape `QueryPatternDetected` for `.sql` resources where safe.
  - [ ] Emit SQL-shape `QueryPatternDetected` for JDBC literals where safe.
  - [ ] Emit SQL-shape `QueryPatternDetected` for JPA literals where safe, or document the deferral if current extractor behavior is not credible.
  - [ ] Replace legacy SQL resource `operationName` values such as `SqlResource` with visible SQL verbs or omit operation names.
  - [ ] Omit routine names from `CALL`/`EXEC` table fields in v1.
  - [ ] Add unsupported/dynamic no-overclaim tests.

- [x] 6. Align Python only where needed. Requirements: 1, 2, 6, 8, 9.
  - [ ] Preserve existing Python SQL tests.
  - [ ] Align source-kind values if the shared contract changes.
  - [ ] Keep Python's normalized masked SQL text `queryShapeHash` behavior as the v1 reference.
  - [ ] Add or update Python fixture tests to export expected golden SQL shape values.
  - [ ] Keep raw SQL suppression tests green.

- [x] 7. Harden combined SQL surface projection. Requirements: 7, 8, 9.
  - [ ] Verify `CombinedDependencySurfaceRow` contains all SQL surface fields.
  - [ ] Apply display/grouping label precedence for SQL surfaces.
  - [ ] Preserve full-metadata diff/reverse identity and prove different `sqlSourceKind` values do not collapse.
  - [ ] Keep hash-only SQL surfaces review-tier where needed.
  - [ ] Emit `HashOnlyEvidence` and `VolatileIdentity` or equivalent caveats where stable SQL identity is weak.
  - [ ] Ensure Markdown/JSON omit raw SQL and unsafe paths.
  - [ ] Re-validate table/column identifiers before combined report rendering.
  - [ ] Add tests for shape-hash, table/column, text-hash, and fact-ID fallback surface keys.
  - [ ] Add grouping/identity collision tests for same table / different shape hash.
  - [ ] Add combined SQL surface byte-stability tests.

- [x] 8. Harden paths and reverse SQL behavior. Requirements: 7, 8, 10.
  - [ ] Verify reachable SQL surfaces become terminal path nodes.
  - [ ] Verify unlinked SQL surfaces remain gaps, not successful paths.
  - [ ] Verify reverse SQL-surface queries find endpoints only with path evidence.
  - [ ] Add reduced-coverage caveat tests where appropriate.

- [x] 9. Update diff and impact SQL behavior if needed. Requirements: 7, 8, 9.
  - [ ] Verify SQL surface diffs use stable metadata, not volatile IDs.
  - [ ] Verify hash-only evidence does not overclaim semantic changes.
  - [ ] Verify reduced coverage downgrades remain intact.

- [x] 10. Update docs and rule catalog. Requirements: 1, 8, 9, 10.
  - [ ] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
  - [ ] Update `docs/VALIDATION.md`.
  - [ ] Update `docs/ACCEPTANCE.md`.
  - [ ] Update `rules/rule-catalog.yml` limitations and emitted properties.
  - [ ] Add `database.sql.shape.v1` for .NET/shared SQL-shape evidence.
  - [ ] Add `typescript.integration.sql.v1` for TypeScript direct SQL evidence if implemented in this slice.
  - [ ] Document Python v1 normalization caveats on every SQL-shape rule.
  - [ ] Document `HashOnlyEvidence` and `VolatileIdentity` under the relevant combined rules.
  - [ ] Document dynamic SQL boundary behavior and migration-file production deferral.
  - [ ] Add any additional new rule IDs only when genuinely required.

- [x] 11. Validate. Requirements: 10.
  - [ ] `dotnet build src/dotnet/TraceMap.sln`
  - [ ] `dotnet test src/dotnet/TraceMap.sln`
  - [ ] `npm run check --prefix src/typescript`
  - [ ] `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test`
  - [ ] Python adapter tests if Python changes.
  - [ ] `./scripts/smoke-combined-paths.sh` if combined/path/reverse behavior changes.
  - [ ] `./scripts/check-private-paths.sh`
  - [ ] `git diff --check`

## Recommended PR Slices

- [x] PR 1: Shared SQL-shape contract + golden fixtures + Python reference expected values.
- [x] PR 2: .NET SQL backfill + combined surface projection/display tests.
- [x] PR 3: TypeScript direct SQL surfaces + query-builder regression tests.
- [x] PR 4: JVM SQL-shape backfill.
- [x] PR 5: Python alignment, only if contract documentation requires it.
- [x] PR 6: Combined path/reverse/diff/impact smoke hardening.

## Deferred Follow-Ups

- Full SQL parser with dialect modules.
- Migration graph and schema artifact indexing.
- Stored procedure/function dependency expansion.
- Runtime telemetry or query execution evidence.
- UI graph rendering for SQL surfaces.
