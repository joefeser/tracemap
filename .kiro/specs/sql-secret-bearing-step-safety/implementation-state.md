# SQL Secret-Bearing Step Safety Implementation State

Status: implemented-pending-pr-review
Implementation branch: `codex/sql-secret-bearing-step-safety-impl`
Target base: `dev`
Public claim level: category-only-static-evidence

## Implemented Scope

- Added deterministic PostgreSQL-first classification for user mappings, FDW
  server options, dblink inputs, subscription connections, credential-like
  scheduled commands, and active credential-assignment comments.
- Added `secret-bearing`, `secret-reference`, `possible-secret`, and
  `not-established` classifications with closed category codes.
- Added fail-closed Tier 4 gaps for dynamic and malformed high-risk boundaries.
- Enforced a category-only fact boundary with span-only identity, no raw SQL,
  no values, and no protected-material hashes.
- Suppressed SQL text, shape, and context hashes at protected-material
  boundaries in SQL files, C# literals, and typed-dataset commands.
- Added a human-safe report section with owner-review stops and explicit
  limitations/non-claims.

## Related Issues

- [#455 Secret-bearing SQL step detection](https://github.com/joefeser/tracemap/issues/455)
- [#453 SQL execution-context contracts](https://github.com/joefeser/tracemap/issues/453)
- [#454 PostgreSQL RDS archive-link evidence](https://github.com/joefeser/tracemap/issues/454)
- [#438 Database surface and operation evidence reports](https://github.com/joefeser/tracemap/issues/438)

## Decisions

- Raw values are omitted rather than hashed; protected statement identity is a
  repo-relative span plus deterministic statement ordinal.
- Generic serializers remain safe because classifier output is constructed
  from a closed allowlist before it becomes a `CodeFact`.
- External references are categorized without retaining placeholder names.
- Detection does not claim that a script is safe or that no other secrets exist.

## Validation

- Focused SQL context and secret-safety tests: passed (19 tests).
- Full .NET suite: passed (716 tests).
- Synthetic CLI leak scan and combined-index assertions: covered by tests.
- Checked-in placeholder-only smoke fixture: added.
- Final build, CLI smoke, private-path guard, and diff check are required before
  publication.

## Follow-Up Boundaries

- Live database, environment, vault, and secret-store access remain out of scope.
- No runnable remediation or credential-handling templates are generated.
- Future dialect adapters must add their own rules and false-negative limits;
  PostgreSQL-first behavior is not generalized by inference.
