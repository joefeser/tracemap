# PostgreSQL Archive-Link Evidence Implementation State

Status: implemented-and-merged
Implementation branch: `codex/postgres-archive-link-evidence-impl`
Target base: `dev`
Merged PR: [#462](https://github.com/joefeser/tracemap/pull/462)
Merge commit: `e7b494a1ae1c9d4fa62491d1a3cce8170e047e40`
Public claim level: deterministic-static-evidence

## Implemented Scope

- Classifies PostgreSQL FDW, dblink, logical publication/subscription, and
  `pg_cron` statement surfaces in the existing SQL/context pass.
- Emits span-derived opaque surface identities without retaining infrastructure,
  connection, subscription, remote-query, or scheduled-command values.
- Reduces extension, foreign-server, user-mapping, and publication prerequisite
  evidence across the analyzed script set.
- Emits deterministic prerequisite edges with supporting fact IDs and direction
  only when compatible explicit context declarations establish source/archive.
- Emits Tier 4 gaps for missing checked-in prerequisites, unknown/conflicting
  context or direction, malformed statements, and dynamic protected boundaries.
- Adds category-only Markdown reporting and PostgreSQL-first limitations.

## Related Issues

- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#435 PostgreSQL schema and migration surfaces](https://github.com/joefeser/tracemap/issues/435)
- [#437 Database operation call-pattern evidence](https://github.com/joefeser/tracemap/issues/437)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Decisions

- Archive extraction reuses already-loaded SQL statements and context facts; it
  does not add another SQL-file read.
- Surface identity is derived from repo-relative span, ordinal, mechanism, and
  surface kind. Exact prerequisite matching uses a separate one-way key from a
  non-secret SQL object identifier; raw names and all connection values remain
  omitted.
- Publication/subscription prerequisite evidence can link across their distinct
  mechanism categories, but cannot infer remote identity or connectivity.
- `missing-evidence` is intentionally distinct from runtime absence/failure.

## Validation

- Focused SQL context, secret-safety, and archive-link tests: passed (26 tests).
- Full .NET suite: passed (723 tests).
- Checked-in FDW, dblink, logical replication, and cron fixtures added.
- CLI fixture scan passed with 10 archive-link surfaces and no planted values.
- Build passed with zero warnings/errors; private-path guard and diff check
  passed.

## Follow-Up Boundaries

- No PostgreSQL/RDS connection, object lookup, permission check, replication
  status, scheduler history, SQL execution, or remediation is implemented.
- Permission-specific prerequisite semantics remain in the next SQL runway story.
- SQL Server, Oracle, and MySQL link mechanisms remain future adapters.
