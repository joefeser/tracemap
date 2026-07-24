# PostgreSQL Enum/Routine Evidence State

Status: implementation-validated-on-pre-sync-base

Branch: `codex/postgres-schema-migration-routines-enums`

Base: `origin/dev` at `11f25dae054fde47aea95dc23d373e9c7561b404`

Scope: third bounded issue #435 raw-DDL slice for enum, function, and procedure
declaration identity. Enum labels, routine signatures, parameters, return
declarations, languages, and bodies are omitted.

No live database access, SQL execution, runtime reachability, schema
introspection, permission claim, production-state claim, or release approval.

Validation:

- focused PostgreSQL schema-migration tests: 12/12 passed;
- `dotnet build src/dotnet/TraceMap.sln`: passed with existing NU1903
  advisories;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 876/876 passed;
- checked-in sample CLI scan emitted enum/routine facts with rule, tier, span,
  commit, extractor, coverage, and omission evidence while retaining no labels,
  signatures, or bodies;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.

Final validation remains pending after the main-to-dev reconciliation merge.

Deferred: enum labels; routine signatures/bodies; quoted identifiers; aggregate,
window, trigger, and operator declarations; drop/rename operations; snapshots;
EF Core/Npgsql migration APIs; execution graphs; live introspection; and
database-centered reporting.
