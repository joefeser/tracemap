# PostgreSQL Destructive Migration Evidence State

Status: ready-for-review

Branch: `codex/postgres-schema-migration-destructive-changes`

Base: `origin/dev` through PR #528 / merge
`fc9f3a4fd016f7133d82a38605a8bbcc82b90983`

Scope: fourth bounded issue #435 raw-DDL slice for single-object table drops,
single-subcommand column drops, and table/column renames. The implementation
reuses `PostgresMigrationOperation`; declaration fact types are not used to
represent removal.

No live database access, SQL execution, dependency-effect inference, data-loss
claim, rollback claim, runtime reachability, schema introspection, permission
claim, production-state claim, or release approval.

Deferred: quoted identifiers; multi-object and multi-subcommand changes;
`DROP INDEX`, `DROP TYPE`, routine drops, constraint drops/renames; snapshots;
EF Core/Npgsql migration APIs; execution graphs; live introspection; and
database-centered reporting.

Validation:

- focused PostgreSQL schema-migration tests: 17/17 passed;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with the existing
  NU1903 SQLite advisory;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 886/886 passed;
- checked-in sample CLI scan emitted four destructive-operation facts with
  rule, tier, repository-relative span, commit, extractor version, bounded
  coverage, safe old/new identity, categorical drop behavior, and limitations;
- facts and report retained no raw `DROP TABLE` or `ALTER TABLE` text;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.
