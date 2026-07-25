# EF Core Mapping v0 Requirements

## Goal

Provide deterministic static evidence that connects EF/EF Core entity models to
bounded database table and column identities, then compose that evidence into
the existing database design-review packet.

## Requirements

1. The semantic C# extractor shall preserve existing `DbContextDeclared`,
   `DbSetDeclared`, and annotation mapping behavior.
2. `DbSetDeclared` facts shall identify the bounded generic entity type when
   Roslyn resolves it.
3. `[Table]` and `[Column]` evidence shall include bounded entity/member
   identity and shall preserve a constant table schema when supplied.
4. Constant fluent `Entity<T>().ToTable(...)` and
   `Entity<T>().Property(...).HasColumnName(...)` mappings shall emit
   `DatabaseColumnMapping` facts under `database.ef.v1`.
5. Dynamic table/column arguments, recognizable fluent chains without
   semantic binding, and assembly/reflection-driven model configuration shall
   emit rule-backed `AnalysisGap` facts rather than a guessed mapping.
6. The database design-review packet shall compose compatible EF table and
   column mappings into same-source PostgreSQL table groups using exact,
   case-insensitive bounded identifiers. When EF omits schema identity, a table
   name may match only when it is unique within that source.
7. EF mappings that cannot be linked to a declared PostgreSQL table, or columns
   that cannot be linked through a bounded entity-to-table mapping, shall remain
   explicit packet gaps.
8. Facts and reports shall preserve file spans, commit SHA, extractor identity,
   evidence tier, supporting fact IDs, rule IDs, coverage labels, and
   limitations where available.
9. Outputs shall be deterministic and shall not render source snippets,
   generated SQL, connection material, credentials, local paths, or arbitrary
   expressions.

## Non-goals

- Executing application startup, migrations, `OnModelCreating`, or a database.
- Reconstructing the runtime EF model or generated SQL.
- Proving provider selection, connection identity, migration application,
  database existence, or production state.
- Inferring convention-only table/column names.
- Extracting keys, indexes, relationships, owned types, value converters, or
  application database-operation call patterns in this v0 slice.
