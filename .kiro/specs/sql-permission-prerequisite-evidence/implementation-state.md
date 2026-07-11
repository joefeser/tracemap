# SQL Permission Prerequisite Evidence Implementation State

Status: implemented-pending-pr-review
Implementation branch: `codex/sql-permission-prerequisite-evidence-impl`
Target base: `dev`
Public claim level: deterministic-static-evidence

## Implemented Scope

- Extracts supported PostgreSQL grants, revokes, ownership changes, default
  privileges, and role memberships into category-only evidence.
- Supports database, schema, table, sequence, routine, foreign-server,
  foreign-wrapper, role, and extension-capability categories.
- Adds the versioned `postgres-permission-prerequisites/v1` operation registry.
- Reduces candidates into `present-in-scripts`, `missing-evidence`,
  `conflicting-evidence`, `unknown`, or `needs-owner-review` with deterministic
  reason codes and supporting/contradicting fact IDs.
- Caps administrative capabilities, cross-file order, permission-after-operation,
  and reduced inputs at owner review or unknown instead of simulating state.
- Adds safe Markdown permission/prerequisite tables and explicit non-claims.

## Related Issues

- [#456 SQL permission prerequisite evidence](https://github.com/joefeser/tracemap/issues/456)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#455 Secret-bearing SQL step detection](https://github.com/joefeser/tracemap/issues/455)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Decisions

- Permission statement facts and prerequisite coverage facts remain distinct.
- Raw identities are omitted; exact foreign-server linking uses a one-way key
  from a non-secret SQL identifier, and coverage requires compatible opaque
  object and principal identities where the operation exposes them.
- `present-in-scripts` is never described as effective runtime access.
- Generic RDS/provider privileges are not inferred; administrative candidates
  always require owner validation.

## Validation

- Focused SQL context/safety/archive/permission suite passed (35 tests).
- Full .NET suite passed (732 tests).
- CLI smoke passed with five permission statements, six prerequisite coverage
  rows, `present-in-scripts` non-claims, and no planted values.
- Build passed with zero warnings/errors; private-path guard and diff check
  passed.

## Follow-Up Boundaries

- No live privilege lookup, inheritance expansion, RLS/policy evaluation,
  transaction/branch simulation, cloud IAM access, grant generation, or SQL
  execution is implemented.
- The final runbook story may consume these rows but cannot upgrade their static
  evidence status.
