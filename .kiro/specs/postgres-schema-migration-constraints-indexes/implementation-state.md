# PostgreSQL Constraint/Index Evidence State

Status: ready-for-review

Branch: `codex/postgres-schema-migration-constraints-indexes`

Base: `origin/dev` at `974ea1ed2902941bd7816494f4157794dfeaad9e`

PR: [#526](https://github.com/joefeser/tracemap/pull/526)

Scope: second bounded #435 raw-DDL slice for named primary-key, unique, and
foreign-key constraints plus simple `CREATE [UNIQUE] INDEX` evidence and
review-tier SQL schema-impact composition.

No live database access, SQL execution, schema introspection, quoted-identifier
projection, expression/predicate retention, migration ordering, or runtime
claims.

Validation:

- focused extractor and SQL schema-impact tests: 22/22 passed;
- `dotnet build src/dotnet/TraceMap.sln`: passed with the existing NU1903
  SQLite advisories;
- `dotnet test src/dotnet/TraceMap.sln --no-build`: 874/874 passed;
- checked-in sample CLI scan: passed with rule/tier/span/commit/extractor/
  coverage evidence and no retained raw DDL;
- `./scripts/check-private-paths.sh`: passed; and
- `git diff --check`: passed.

PR/ACK: ready PR #526 targets `dev`. The first exact-head CI run had an
unrelated JVM integration-test timeout; a failed-job rerun passed the JVM and
five-adapter combine jobs on the unchanged head. Review follow-up now marks
inline `NOT NULL` declarations as reduced coverage and requires PostgreSQL
schema facts to match the requested table (and schema when supplied). The
repository-wide `EvidenceSpan`/`CodeFact` rule-ID model remains unchanged:
every emitted fact carries its rule ID and its evidence span.

Deferred: check/exclusion and unnamed constraints; inline column constraints;
foreign-key actions; expression/partial/include indexes; quoted identifiers;
types/defaults/generated expressions; drop/rename operations; enums; routines;
snapshots; EF Core/Npgsql migration APIs; execution graphs; live introspection.
