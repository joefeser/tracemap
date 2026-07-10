# SQL Permission Prerequisite Evidence Implementation State

Status: ready-for-implementation
Spec branch: `codex/sql-evidence-runway-specs`
Target base: `dev`
Public claim level: hidden

## Scope State

This folder is specification-only. It defines PostgreSQL permission statement
evidence and a prerequisite candidate reducer; no implementation is complete.

## Related Issues

- [#456 SQL permission prerequisite evidence](https://github.com/joefeser/tracemap/issues/456)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#455 Secret-bearing SQL step detection](https://github.com/joefeser/tracemap/issues/455)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Ordering Recommendation

Implement fourth. It depends on the shared context contract, archive-link
operation/object inventory, and secret-safe property boundary. Its normalized
prerequisite rows are a direct input to the final runbook packet.

Recommended runway:

1. `sql-execution-context-contracts`
2. `postgres-archive-link-evidence`
3. `sql-secret-bearing-step-safety`
4. `sql-permission-prerequisite-evidence`
5. `sql-operator-runbook-packet`

## Decisions

- `present-in-scripts` never means effective runtime privilege.
- `missing-evidence` never means a live grant is absent.
- The reducer does not simulate PostgreSQL privilege inheritance or state.
- Unknown order/context/identity reduces coverage and produces owner review.
- No grant SQL is generated or executed.

## Validation Expected During Implementation

- Synthetic ordered and unordered permission/archive fixtures.
- Rule registry, extraction, reducer, safety, storage, report, and determinism tests.
- Full .NET tests and one CLI fixture scan.
- `./scripts/check-private-paths.sh` and `git diff --check`.

## Open Design Questions

- Choose the smallest initial PostgreSQL capability-code registry that is useful
  without implying authoritative privilege semantics.
- Decide how opaque role/object identities preserve cross-file linking without
  exposing low-entropy private identifiers.
