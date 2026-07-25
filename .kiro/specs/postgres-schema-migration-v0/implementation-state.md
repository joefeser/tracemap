# PostgreSQL Schema/Migration v0 State

Status: implemented-and-merged

Branch: `codex/postgres-schema-migration-v0`

PR: [#524](https://github.com/joefeser/tracemap/pull/524)

Merge commit: `974ea1ed2902941bd7816494f4157794dfeaad9e`

Scope: first bounded #435 extraction slice: raw `.sql` `CREATE TABLE` and
`ALTER TABLE ... ADD COLUMN` only, with migration-file/operation/table/column
facts and supported-family gaps.

Validation:

- focused PostgreSQL schema/migration tests: 6/6 passed
- `dotnet build src/dotnet/TraceMap.sln`: passed (known NU1903 warnings)
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 867/867 passed
- `./scripts/check-private-paths.sh`: passed
- `git diff --check`: passed

PR/ACK: PR #524 merged into `dev` after the ACK stable-build blocker was
resolved and the current review findings were patched and validated.

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

Main-promotion review follow-up: release-review now includes the schema
migration rule and gap rule in its SQL evidence projection, renders
schema/migration facts with safe upstream provenance and allowlisted structural
metadata, and preserves categorical schema-migration gaps. A schema-only index
regression proves the SQL evidence section remains available instead of
reporting `CompatibleEvidenceUnavailable`.

Later bounded slices delivered indexes, constraints, enums, routines,
destructive changes, and checked-in snapshots. Still deferred: quoted
identifiers; broader PostgreSQL DDL; EF Core/Npgsql migration APIs;
execution/order graphs; live introspection; shared cross-extractor SQL
read/statement caching; and all runtime/production claims.
