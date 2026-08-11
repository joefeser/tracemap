# Framework Migration Evidence v0 Requirements

## Goal

Extend issue #531 with deterministic static evidence for compiler-resolved EF
Core migration declarations and migration operations while keeping generic EF
migration evidence distinct from explicitly PostgreSQL-bound evidence.

## Requirements

1. The semantic C# extractor shall recognize a migration declaration only when
   Roslyn proves that its type derives from
   `Microsoft.EntityFrameworkCore.Migrations.Migration` from an allowlisted
   framework metadata assembly whose simple name and public-key token match the
   supported EF Core identity. A source-declared or unsigned metadata type with
   the same namespace and name shall not qualify.
2. The extractor shall recognize migration operations only when Roslyn binds
   the invoked method to EF Core `MigrationBuilder` from an allowlisted
   framework metadata assembly and the enclosing type independently satisfies
   the migration-declaration admission rule. Source lookalikes, application
   extension methods, and calls from ordinary helper types shall not qualify.
3. Each operation shall preserve its containing migration type, containing
   method, `up`, `down`, or `unknown` direction, closed operation kind,
   provider-scope classification, repository-relative span, commit SHA,
   extractor version, evidence tier, rule ID, coverage label, and limitations.
4. Bounded object identity may be retained only from compiler-bound constant
   arguments for the closed per-operation parameter map in the design.
   Dynamic, missing, ambiguous, array, lambda, builder, or unsafe identity
   shapes outside that map shall produce a rule-backed gap without a guessed
   target.
5. Generic EF Core `MigrationBuilder` calls shall use provider scope `unknown`.
   They shall not become PostgreSQL declarations or attach to PostgreSQL schema
   groups merely because the project references Npgsql.
6. The v0 producer shall emit provider scope `unknown` only. A later
   `postgresql-explicit` scope shall require a pinned Npgsql package version,
   exact metadata assembly identity, fully qualified supported signatures, and
   an independent fixture. String resemblance, package presence, namespaces in
   application code, annotations, or provider-looking literals shall never
   establish this scope.
7. `MigrationBuilder.Sql`, seed/data operations, custom operations, provider
   annotations chained from an admitted operation, and unsupported argument
   shapes shall emit categorical gaps.
   Raw SQL, seed values, annotation values, argument text, and their digests
   shall not be retained.
8. Recognizable migration syntax without usable semantic binding may emit only
   `Tier4Unknown` coverage gaps. It shall not emit a migration declaration,
   operation, provider, or object identity claim.
9. The first producer slice shall emit versioned framework-migration rule
   evidence and persist it through existing NDJSON and SQLite fact contracts
   without changing existing PostgreSQL raw-DDL facts.
10. The producer slice shall audit existing generic consumers. The reducer
    shall reject both framework-migration fact types and the framework-migration
    gap rule before generic matching until a later evidence-preserving impact
    contract explicitly admits them. Protected values shall remain absent from
    every generic report, log, and index surface.
11. A later consumer slice may display generic framework migration evidence as
    application-side evidence. It may correlate an operation to a PostgreSQL
    schema group only after a separately specified provider-explicit contract
    and exact bounded object identity are both sufficient; otherwise the
    unlinked state remains an explicit gap.
12. Output ordering and fact identity inputs shall be deterministic across
    repeated scans and file enumeration order. Identity inputs shall include
    canonical migration owner, operation kind, direction, and source span
    rather than display name alone; the repository-wide scan ID remains part
    of the existing fact-ID contract.
13. No output shall claim migration execution, ordering, application, rollback,
    reversibility, generated SQL, provider selection at runtime, database
    existence, live-schema correspondence, compatibility, safety, or release
    approval.
14. Gaps shall be aggregated deterministically by migration type and categorical
    `gapKind`, with an occurrence count. Operation identity shall include a
    deterministic invocation ordinal so identical operation shapes on one line
    do not collapse.

## Non-goals

- Running `dotnet ef`, application startup, migration code, or a database.
- Reconstructing the runtime EF model, generated SQL, operation graph, or
  migration history table.
- Inferring PostgreSQL from an installed package, project name, connection
  string, `ActiveProvider` branch, or configuration convention.
- Retaining raw SQL, seed data, annotation values, default expressions,
  computed expressions, connection material, or local absolute paths.
- Replacing the EF model-mapping contract in #436, application operation-call
  contract in #437, database composition in #438, or raw PostgreSQL DDL
  extraction already shipped under `database.postgres.schema-migration.v1`.
- Snapshot comparison semantics, incremental migration ordering, or shared SQL
  read/statement caching in this slice.
- Provider-explicit Npgsql operations until a real package signature and
  metadata-assembly fixture are pinned independently.
