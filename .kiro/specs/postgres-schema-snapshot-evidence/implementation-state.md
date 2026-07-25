# PostgreSQL Schema Snapshot Evidence State

Status: ready-for-review

Branch: `codex/postgres-schema-snapshot-evidence`

PR: #530 (`https://github.com/joefeser/tracemap/pull/530`)

Base: `origin/dev` through PR #529 / merge
`28f2701c6b30a84a15d70f5eba4f2df70abe6be2`

Scope: fifth bounded issue #435 raw-DDL slice for explicit checked-in PostgreSQL
schema snapshots. Snapshot identity requires a standard pg_dump header or exact
TraceMap v1 directive. Supported DDL remains independently useful; unsupported
DDL is aggregated into categorical reduced-coverage gaps.

No database connection, `pg_dump` execution, restore, live introspection,
source-database identity, completeness/freshness claim, production-state claim,
or release approval.

Deferred: snapshot comparison/diff semantics; quoted identifiers; broader DDL
families; EF Core/Npgsql migration APIs; execution graphs; live introspection;
and database-centered reporting.

Validation:

- PR #530 review remediation: snapshot recognized-DDL accounting now records
  only successfully projected bounded DDL. Prefix-admitted but unprojectable
  statements are preserved as per-statement gaps and classified into safe,
  categorical snapshot coverage gaps; focused regression coverage includes
  mixed and unsupported-only snapshots.

- focused PostgreSQL schema-migration tests: 22/22 passed;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with the existing
  NU1903 SQLite advisory;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 891/891 passed;
- checked-in sample CLI scan emitted a rule-bound Tier 2 snapshot fact and
  categorical Tier 4 reduced-coverage gap with repository-relative span,
  commit, extractor version, counts, source-identity omission, and limitations;
- scan facts and report retained neither the snapshot marker nor the unsupported
  sequence identity;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.
