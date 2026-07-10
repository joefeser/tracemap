# PostgreSQL Archive-Link Evidence Tasks

## Implementation Plan

### Phase 1: Rules and Shared Contracts

- [x] 1.1 Confirm the execution-context contract implementation is available or explicitly scope a minimal dependency slice.
- [x] 1.2 Catalog archive-link surface, prerequisite, edge, and gap vocabulary with limitations.
- [x] 1.3 Define closed mechanism, direction, context, prerequisite, and missing-evidence codes.
- [x] 1.4 Define safe identity behavior that never depends on secrets or private infrastructure values.

### Phase 2: PostgreSQL Extraction

- [x] 2.1 Extract `postgres_fdw` extension, server, mapping, import, foreign-table, and grant surfaces.
- [x] 2.2 Extract dblink extension/call surfaces while omitting connection and query arguments.
- [x] 2.3 Extract publication, subscription, and explicit replication membership surfaces while omitting connection data.
- [x] 2.4 Extract `pg_cron` scheduling surfaces without scheduled SQL bodies.
- [x] 2.5 Emit reduced-coverage gaps for dynamic, procedural, malformed, unsupported, or ambiguous shapes.

### Phase 3: Linking and Prerequisites

- [x] 3.1 Link same-mechanism objects using safe stable identities and context.
- [x] 3.2 Derive source/archive direction only from declarations or deterministic context relationships.
- [x] 3.3 Emit checked-in prerequisite candidates and `missing-evidence` gaps.
- [x] 3.4 Preserve supporting fact IDs, spans, tiers, coverage, and limitations through storage and reports.

### Phase 4: Synthetic Fixtures and Tests

- [x] 4.1 Add separate public-safe FDW, dblink, logical replication, and `pg_cron` fixtures.
- [x] 4.2 Add mixed-context, conflicting-direction, missing-prerequisite, dynamic-option, and malformed fixtures.
- [x] 4.3 Prove user mapping, dblink, subscription, and scheduled-command values do not leak in any output.
- [x] 4.4 Test deterministic links and IDs across repeated scans and file enumeration order changes.
- [x] 4.5 Add report assertions that all runtime connectivity, applied state, replication, and scheduling claims remain unknown.

### Phase 5: Validation

- [x] 5.1 Update rule and SQL evidence documentation plus `docs/VALIDATION.md`.
- [x] 5.2 Run focused extractor/reducer/storage/report tests.
- [x] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [x] 5.4 Run a CLI scan against the synthetic archive fixture and inspect all required artifacts.
- [x] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Live PostgreSQL/RDS connectivity or capability checks.
- Replication lag, job history, schema equivalence, or archive correctness.
- SQL Server, Oracle, or MySQL cross-database mechanisms.
- Automatic SQL execution or configuration remediation.
