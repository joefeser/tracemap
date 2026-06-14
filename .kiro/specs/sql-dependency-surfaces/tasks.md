# SQL Dependency Surfaces Tasks

## Implementation Tasks

- [x] 1. Confirm current SQL evidence behavior. Requirements: 1, 3, 4, 5, 6, 7.
  - [x] Inspect .NET SQL text, Dapper, ADO.NET, EF raw SQL, and `.sql` file facts.
  - [x] Inspect TypeScript Prisma/Base44 query-builder facts and direct SQL coverage gaps.
  - [x] Inspect JVM SQL resource, JDBC, and JPA facts.
  - [x] Inspect Python SQL-shape behavior and tests.
  - [x] Inspect combined `sql-query` and `sql-persistence` surface projection, path, reverse, diff, and impact behavior.

- [x] 2. Add shared SQL-shape extraction contract. Requirements: 1, 2, 8, 9.
  - [x] Define supported operations and unsupported constructs.
  - [x] Define Python-compatible normalized masked SQL text as the v1 `queryShapeHash` input.
  - [x] Define safe table/column identifier extraction rules.
  - [x] Add shared golden fixtures for text hash, shape hash, operation, safe identifiers, shape-hash-only `WITH`/CTE behavior, and unsupported/dynamic no-shape cases.
  - [x] Add normalization edge-case fixtures for escaped quotes, multiline comments, CRLF/tabs, trailing semicolons, `--` inside literals, and double-quoted identifiers.
  - [x] Add unit tests for supported shapes.
  - [x] Add unit tests for unsupported shapes that produce no table/column overclaim.

- [x] 3. Implement .NET SQL-shape backfill. Requirements: 1, 2, 3, 8, 9.
  - [x] Add `database.sql.shape.v1` to the rule catalog with normalization and static-evidence limitations.
  - [x] Add or reuse a .NET SQL shape helper.
  - [x] Emit SQL-shape `QueryPatternDetected` for simple `.sql` files.
  - [x] Emit SQL-shape `QueryPatternDetected` for supported Dapper/ADO.NET/EF raw SQL literals.
  - [x] Set SQL code facts' `targetSymbol` to containing method/class/file symbols when available.
  - [x] Preserve existing SQL facts.
  - [x] Add EF raw SQL literal shape tests.
  - [x] Add raw SQL suppression tests.
  - [x] Add dynamic/unsupported SQL no-overclaim tests.

- [x] 4. Implement TypeScript direct SQL surfaces. Requirements: 1, 2, 4, 8, 9.
  - [x] Add `typescript.integration.sql.v1` to the rule catalog with limitations before emitting new direct SQL evidence.
  - [x] Preserve Prisma/Base44 query-builder facts without `sqlSourceKind`.
  - [x] Detect simple direct SQL literals in supported client calls.
  - [x] Detect supported static tagged SQL literals.
  - [x] Emit `SqlTextUsed` and SQL-shape `QueryPatternDetected` where safe.
  - [x] Emit dynamic boundary evidence or gaps without raw SQL.
  - [x] Add query-builder regression tests proving Prisma/Base44 facts do not gain `sqlSourceKind`.

- [x] 5. Implement JVM SQL-shape backfill. Requirements: 1, 2, 5, 8, 9.
  - [x] Emit SQL-shape `QueryPatternDetected` for `.sql` resources where safe.
  - [x] Emit SQL-shape `QueryPatternDetected` for JDBC literals where safe.
  - [x] Emit SQL-shape `QueryPatternDetected` for JPA literals where safe, or document the deferral if current extractor behavior is not credible.
  - [x] Replace legacy SQL resource `operationName` values such as `SqlResource` with visible SQL verbs or omit operation names.
  - [x] Omit routine names from `CALL`/`EXEC` table fields in v1.
  - [x] Add unsupported/dynamic no-overclaim tests.

- [x] 6. Align Python only where needed. Requirements: 1, 2, 6, 8, 9.
  - [x] Preserve existing Python SQL tests.
  - [x] Align source-kind values if the shared contract changes.
  - [x] Keep Python's normalized masked SQL text `queryShapeHash` behavior as the v1 reference.
  - [x] Add or update Python fixture tests to export expected golden SQL shape values.
  - [x] Keep raw SQL suppression tests green.

- [x] 7. Harden combined SQL surface projection. Requirements: 7, 8, 9.
  - [x] Verify `CombinedDependencySurfaceRow` contains all SQL surface fields.
  - [x] Apply display/grouping label precedence for SQL surfaces.
  - [x] Preserve full-metadata diff/reverse identity and prove different `sqlSourceKind` values do not collapse.
  - [x] Keep hash-only SQL surfaces review-tier where needed.
  - [x] Emit `HashOnlyEvidence` and `VolatileIdentity` or equivalent caveats where stable SQL identity is weak.
  - [x] Ensure Markdown/JSON omit raw SQL and unsafe paths.
  - [x] Re-validate table/column identifiers before combined report rendering.
  - [x] Add tests for shape-hash, table/column, text-hash, and fact-ID fallback surface keys.
  - [x] Add grouping/identity collision tests for same table / different shape hash.
  - [x] Add combined SQL surface byte-stability tests.

- [x] 8. Harden paths and reverse SQL behavior. Requirements: 7, 8, 10.
  - [x] Verify reachable SQL surfaces become terminal path nodes.
  - [x] Verify unlinked SQL surfaces remain gaps, not successful paths.
  - [x] Verify reverse SQL-surface queries find endpoints only with path evidence.
  - [x] Add reduced-coverage caveat tests where appropriate.

- [x] 9. Update diff and impact SQL behavior if needed. Requirements: 7, 8, 9.
  - [x] Verify SQL surface diffs use stable metadata, not volatile IDs.
  - [x] Verify hash-only evidence does not overclaim semantic changes.
  - [x] Verify reduced coverage downgrades remain intact.

- [x] 10. Update docs and rule catalog. Requirements: 1, 8, 9, 10.
  - [x] Update `docs/LANGUAGE_ADAPTER_CONTRACT.md`.
  - [x] Update `docs/VALIDATION.md`.
  - [x] Update `docs/ACCEPTANCE.md`.
  - [x] Update `rules/rule-catalog.yml` limitations and emitted properties.
  - [x] Add `database.sql.shape.v1` for .NET/shared SQL-shape evidence.
  - [x] Add `typescript.integration.sql.v1` for TypeScript direct SQL evidence if implemented in this slice.
  - [x] Document Python v1 normalization caveats on every SQL-shape rule.
  - [x] Document `HashOnlyEvidence` and `VolatileIdentity` under the relevant combined rules.
  - [x] Document dynamic SQL boundary behavior and migration-file production deferral.
  - [x] Add any additional new rule IDs only when genuinely required.

- [x] 11. Validate. Requirements: 10.
  - [x] `dotnet build src/dotnet/TraceMap.sln`
  - [x] `dotnet test src/dotnet/TraceMap.sln`
  - [x] `npm run check --prefix src/typescript`
  - [x] `JAVA_HOME=/opt/homebrew/opt/openjdk@21/libexec/openjdk.jdk/Contents/Home gradle -p src/jvm test`
  - [x] Python adapter tests if Python changes.
  - [x] `./scripts/smoke-combined-paths.sh` if combined/path/reverse behavior changes.
  - [x] `./scripts/check-private-paths.sh`
  - [x] `git diff --check`

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
