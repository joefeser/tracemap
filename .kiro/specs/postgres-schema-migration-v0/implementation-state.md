# PostgreSQL Schema/Migration v0 State

Branch: `codex/postgres-schema-migration-v0`

Scope: first bounded #435 extraction slice: raw `.sql` `CREATE TABLE` and
`ALTER TABLE ... ADD COLUMN` only, with migration-file/operation/table/column
facts and supported-family gaps.

Validation:

- focused PostgreSQL schema/migration tests: 4/4 passed
- `dotnet build src/dotnet/TraceMap.sln`: passed (known NU1903 warnings)
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 865/865 passed
- `./scripts/check-private-paths.sh`: passed
- `git diff --check`: passed

PR/ACK: PR #524 targets `dev`. ACK is pending its final-head run; no review or
merge authority is inferred until it returns an authorized decision.

Deferred: quoted identifiers; indexes; constraints; enums; routines;
checked-in snapshots; EF Core/Npgsql migration APIs; execution/order graphs;
live introspection; and all runtime/production claims.
