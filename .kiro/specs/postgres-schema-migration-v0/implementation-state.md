# PostgreSQL Schema/Migration v0 State

Branch: `codex/postgres-schema-migration-v0`

Scope: first bounded #435 extraction slice: raw `.sql` `CREATE TABLE` and
`ALTER TABLE ... ADD COLUMN` only, with migration-file/operation/table/column
facts and supported-family gaps.

Validation:

- focused PostgreSQL schema/migration tests: 6/6 passed
- `dotnet build src/dotnet/TraceMap.sln`: passed (known NU1903 warnings)
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 867/867 passed
- `./scripts/check-private-paths.sh`: passed
- `git diff --check`: passed

PR/ACK: PR #524 targets `dev`. ACK stopped before GitHub review processing with
`environment_blocked / LOCAL_BUILD_STALE / owner_decision_required`; the
resolved CLI reports 0.2.0 while its installed package metadata is
0.3.0-rc.1, and the lane requires a non-prerelease stable build. No reviewer
request or merge authority was produced. Rerun ACK on the final pushed head
after the local stable build is repaired.

Review follow-up: current Qodo findings were addressed by rejecting
multi-subcommand `ALTER TABLE` statements with a categorical gap, retaining
masked structural hashes for readable statement gaps plus a file-level
hash-of-hashes, and avoiding statement lexing when a conservative raw-text
prefilter cannot contain either supported DDL family. Shared SQL read/parse
caching remains a broader cross-extractor follow-up.

Exact-head Codex follow-up: mixed supported/deferred top-level `CREATE TABLE`
clauses now retain supported table/column evidence while emitting
`CreateTableClauseUnsupported` with reduced coverage. A quoted-column
regression proves the unsupported identity is not rendered; focused and full
solution validation pass.

Deferred: quoted identifiers; indexes; constraints; enums; routines;
checked-in snapshots; EF Core/Npgsql migration APIs; execution/order graphs;
live introspection; shared cross-extractor SQL read/statement caching; and all
runtime/production claims.
