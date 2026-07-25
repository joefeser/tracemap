# EF Core Mapping v0 Design

## Boundary

This slice extends the existing Roslyn semantic extractor and reuses
`DatabaseColumnMapping`. It does not introduce a second ORM fact model.
`database.ef.v1` is the authority for compiler-resolved EF fluent mappings and
gaps. `csharp.semantic.contractmapping.v1` remains the authority for resolved
`Table`/`Column` annotations because those attributes alone do not prove EF
usage.

## Extraction

- `DbSet<TEntity>` adds `entityType` and `entityTypeId`.
- `[Table("name", Schema = "schema")]` emits a table mapping with
  `entityType`, `mappedName`, and optional `schemaName`.
- `[Column("name")]` emits a column mapping with `entityType`, `memberName`,
  and `mappedName`.
- `modelBuilder.Entity<TEntity>().ToTable("name", "schema")` emits a table
  mapping when the entity type and constant name are bounded.
- `modelBuilder.Entity<TEntity>().Property(x => x.Member)
  .HasColumnName("name")` emits a column mapping when the entity type, member,
  and constant name are bounded.

Only method symbols bound to `Microsoft.EntityFrameworkCore` types are accepted
for fluent mapping evidence. Syntax resemblance alone does not produce a
Tier1 mapping.

Dynamic names and `ApplyConfigurationsFromAssembly` produce Tier4
`AnalysisGap` facts with safe classifications and hashes/count-free metadata;
expressions are not rendered.

## Composition

The design-review reader consumes compatible `database.ef.v1`
`DatabaseColumnMapping` and EF `AnalysisGap` facts from existing source
indexes.

1. Table mappings are matched to PostgreSQL table groups in the same source by
   exact normalized schema/table identity. A schema-unspecified EF mapping may
   use a unique same-source table-name match; zero or multiple candidates are
   gaps.
2. Column mappings are linked through their bounded entity identity to a
   matched table mapping.
3. Matching mappings are appended as declaration evidence.
4. Unmatched mappings and upstream EF gaps become packet gaps.

No convention-derived identity is invented. A `DbSet` entity without an
explicit table mapping remains evidence about the application model, not a
database-table match.

## Limitations

- Static fluent-chain recognition does not evaluate helper methods, loops,
  branches, variables, or custom conventions.
- Assembly scanning, reflection, provider conventions, compiled models, and
  runtime configuration remain unavailable.
- Exact repository identity matches do not prove a runtime EF model or live
  database correspondence.
- Table and column mappings do not prove reads, writes, generated SQL,
  reachability, migration state, or release safety.
