# PostgreSQL Destructive Migration Evidence State

Status: implemented

Branch: `codex/postgres-schema-migration-destructive-changes`

PR: #529 (`https://github.com/joefeser/tracemap/pull/529`)

Merge commit: `28f2701c6b30a84a15d70f5eba4f2df70abe6be2`

Base: `origin/dev` through PR #528 / merge
`fc9f3a4fd016f7133d82a38605a8bbcc82b90983`

Scope: fourth bounded issue #435 raw-DDL slice for single-object table drops,
single-subcommand column drops, and table/column renames. The implementation
reuses `PostgresMigrationOperation`; declaration fact types are not used to
represent removal. Broader `DROP` and `TRUNCATE` shapes remain deferred and
now route to the existing Tier4 `UnsupportedSchemaDdlShape` gap path without
projecting identifiers.

No live database access, SQL execution, dependency-effect inference, data-loss
claim, rollback claim, runtime reachability, schema introspection, permission
claim, production-state claim, or release approval.

Deferred: quoted identifiers; multi-object and multi-subcommand changes;
`DROP INDEX`, `DROP TYPE`, routine drops, constraint drops/renames; snapshots;
EF Core/Npgsql migration APIs; execution graphs; live introspection; and
database-centered reporting.

Validation:

- focused PostgreSQL schema-migration tests: 18/18 passed;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with the existing
  NU1903 SQLite advisory;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 887/887 passed;
- exact-head checked-in sample CLI scan emitted four destructive-operation facts with
  rule, tier, repository-relative span, commit, extractor version, bounded
  coverage, safe old/new identity, categorical drop behavior, and limitations;
- facts and report retained no raw `DROP TABLE` or `ALTER TABLE` text;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.

Review-loop state: ACK recorded and dispositioned the Codex P2 finding that
unsupported destructive DDL had been filtered before gap emission. ACK then
returned `merge_ready` for the reviewed head with clean checks, no unresolved
threads, and no actionable or held findings. The configured trusted-review
quorum leaves the absent Qodo result as medium residual risk; it is not a
merge blocker under the repo-local lane policy. PR #529 then merged into
`dev`.
