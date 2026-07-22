# SQL Validation PostgreSQL Integration State

Branch: `codex/sql-validation-postgres-integration`

Scope: issue #518, disposable PostgreSQL validation for the already-shipped
SQL validation harness. This adds no probe vocabulary and no production reader.

Decision: use a manual opt-in integration smoke rather than a routine CI job.
The smoke pins the official PostgreSQL 16.8 Alpine image by digest, binds only
to a random loopback port, uses no volume, retains no connection material, and
cleans the container and scratch directory through a trap.

Validation:

- Disposable PostgreSQL 16.8 integration smoke: passed twice on the final
  script, including byte-identical repeated output, categorical assertions,
  public-safe projection checks, and post-run container cleanup.
- SQL harness and summary focused tests: 29/29 passed.
- `dotnet build src/dotnet/TraceMap.sln --nologo`: passed with the repository's
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory only.
- `dotnet test src/dotnet/TraceMap.sln --no-build --nologo`: 861/861 passed.
- Bash syntax, private-path guard, and `git diff --check`: passed.

PR state: [#519](https://github.com/joefeser/tracemap/pull/519) is open at
`f8a9e0cf`. ACK stopped before GitHub review processing with
`environment_blocked / LOCAL_BUILD_STALE`: installed ACK `0.3.0-rc.1`
reported stale compiled output, while the trusted TraceMap lane requires a
stable build. No review bot was requested and no merge authority was inferred.

Deferred: reusable archive/source plan templates and operator packet; public
static-versus-observed story; PostgreSQL schema/migration adapter work; live
RDS, `pg_cron`, `dblink`, execution, migration, rollback, and richer Access
extraction.
