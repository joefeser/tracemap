# PostgreSQL Archive-Link Evidence Implementation State

Status: ready-for-implementation
Spec branch: `codex/sql-evidence-runway-specs`
Target base: `dev`
Public claim level: hidden

## Scope State

This folder is specification-only. It defines PostgreSQL archive-link evidence
for FDW, dblink, logical replication, and `pg_cron`; no extractor or reducer is
implemented by this branch.

## Related Issues

- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#437 Database operation call-pattern evidence](https://github.com/joefeser/tracemap/issues/437)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Ordering Recommendation

Implement second, after `sql-execution-context-contracts`. Archive-link
extraction supplies the objects and dependency graph consumed by secret safety,
permission prerequisite reduction, and the final runbook packet.

The full recommended order is:

1. `sql-execution-context-contracts`
2. `postgres-archive-link-evidence`
3. `sql-secret-bearing-step-safety`
4. `sql-permission-prerequisite-evidence`
5. `sql-operator-runbook-packet`

## Decisions

- Mechanism and categorical context are useful evidence even when endpoints are
  redacted or unknown.
- Missing checked-in setup evidence is not proof of missing live state.
- `pg_cron` is modeled as scheduled execution context, not runtime job history.
- RDS caveats require explicit checked-in evidence and owner validation.
- No raw connection or scheduled-command material enters default artifacts.

## Validation Expected During Implementation

- Synthetic fixtures for each supported PostgreSQL mechanism.
- Rule, parser, linker, prerequisite, safety, storage, report, and determinism tests.
- Full .NET tests and a CLI fixture scan.
- `./scripts/check-private-paths.sh` and `git diff --check`.

## Open Design Questions

- Determine whether existing SQL fact types can express link objects without a
  new fact family while preserving stable schemas.
- Define the narrow safe-identifier policy for public fixture object labels
  versus opaque identities in ordinary scans.
