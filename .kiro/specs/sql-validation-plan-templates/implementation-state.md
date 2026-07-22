# SQL Validation Plan Template State

Branch: `codex/sql-validation-plan-templates`

Scope: issue #520. Add public-safe parser-valid source/archive plan examples,
focused contract tests, and an operator handoff packet. No new probes or live
connection behavior.

Validation:

- Both published templates completed CLI dry runs with all ten assertions
  explicitly `not-run`.
- Focused SQL harness tests: 18/18 passed.
- `dotnet build src/dotnet/TraceMap.sln --nologo`: passed with the repository's
  existing `SQLitePCLRaw.lib.e_sqlite3` NU1903 advisory only.
- `dotnet test src/dotnet/TraceMap.sln --no-build --nologo`: 863/863 passed.
- Private-path guard and `git diff --check`: passed.

PR state: implementation complete locally; commit, push, PR, and ACK pending.

Deferred: disposable PostgreSQL validation remains in #519; public site story,
target-specific private plans, live RDS validation, execution checks, and the
PostgreSQL schema/migration adapter remain separate.
