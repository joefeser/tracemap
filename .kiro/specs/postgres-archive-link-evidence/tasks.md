# PostgreSQL Archive-Link Evidence Tasks

## Implementation Plan

### Phase 1: Rules and Shared Contracts

- [ ] 1.1 Confirm the execution-context contract implementation is available or explicitly scope a minimal dependency slice.
- [ ] 1.2 Catalog archive-link surface, prerequisite, edge, and gap vocabulary with limitations.
- [ ] 1.3 Define closed mechanism, direction, context, prerequisite, and missing-evidence codes.
- [ ] 1.4 Define safe identity behavior that never depends on secrets or private infrastructure values.

### Phase 2: PostgreSQL Extraction

- [ ] 2.1 Extract `postgres_fdw` extension, server, mapping, import, foreign-table, and grant surfaces.
- [ ] 2.2 Extract dblink extension/call surfaces while omitting connection and query arguments.
- [ ] 2.3 Extract publication, subscription, and explicit replication membership surfaces while omitting connection data.
- [ ] 2.4 Extract `pg_cron` scheduling surfaces without scheduled SQL bodies.
- [ ] 2.5 Emit reduced-coverage gaps for dynamic, procedural, malformed, unsupported, or ambiguous shapes.

### Phase 3: Linking and Prerequisites

- [ ] 3.1 Link same-mechanism objects using safe stable identities and context.
- [ ] 3.2 Derive source/archive direction only from declarations or deterministic context relationships.
- [ ] 3.3 Emit checked-in prerequisite candidates and `missing-evidence` gaps.
- [ ] 3.4 Preserve supporting fact IDs, spans, tiers, coverage, and limitations through storage and reports.

### Phase 4: Synthetic Fixtures and Tests

- [ ] 4.1 Add separate public-safe FDW, dblink, logical replication, and `pg_cron` fixtures.
- [ ] 4.2 Add mixed-context, conflicting-direction, missing-prerequisite, dynamic-option, and malformed fixtures.
- [ ] 4.3 Prove user mapping, dblink, subscription, and scheduled-command values do not leak in any output.
- [ ] 4.4 Test deterministic links and IDs across repeated scans and file enumeration order changes.
- [ ] 4.5 Add report assertions that all runtime connectivity, applied state, replication, and scheduling claims remain unknown.

### Phase 5: Validation

- [ ] 5.1 Update rule and SQL evidence documentation plus `docs/VALIDATION.md`.
- [ ] 5.2 Run focused extractor/reducer/storage/report tests.
- [ ] 5.3 Run `dotnet test src/dotnet/TraceMap.sln`.
- [ ] 5.4 Run a CLI scan against the synthetic archive fixture and inspect all required artifacts.
- [ ] 5.5 Run `./scripts/check-private-paths.sh` and `git diff --check`.

## Follow-Ups Out of Scope

- Live PostgreSQL/RDS connectivity or capability checks.
- Replication lag, job history, schema equivalence, or archive correctness.
- SQL Server, Oracle, or MySQL cross-database mechanisms.
- Automatic SQL execution or configuration remediation.
