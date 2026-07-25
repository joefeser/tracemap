# PostgreSQL Enum/Routine Evidence State

Status: implemented-and-merged

Branch: `codex/postgres-schema-migration-routines-enums`

PR: [#528](https://github.com/joefeser/tracemap/pull/528)

Merge commit: `fc9f3a4fd016f7133d82a38605a8bbcc82b90983`

Base: reconciled `origin/dev` through PR #527 / merge
`dda039325b802c43466c846f06a6bbe308237d4d`

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

After the main-to-dev reconciliation, release review safely allowlists enum and
routine identity/omission metadata while preserving the rule-specific
limitations and exact scan commit SHA.

Final validation:

- focused extractor and release-review tests: 14/14 passed;
- `dotnet build src/dotnet/TraceMap.sln --no-restore`: passed with existing
  NU1903 advisories;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 883/883 passed;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.

Deferred: enum labels; routine signatures/bodies; quoted identifiers; aggregate,
window, trigger, and operator declarations; drop/rename operations; snapshots;
EF Core/Npgsql migration APIs; execution graphs; live introspection; and
database-centered reporting.
