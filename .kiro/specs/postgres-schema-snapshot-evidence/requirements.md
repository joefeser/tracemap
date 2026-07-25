# Requirements

## Goal

Extend issue #435 with bounded static evidence for explicit checked-in
PostgreSQL schema snapshots.

## Requirements

1. Recognize a schema snapshot only from an active standard
   `-- PostgreSQL database dump` header or exact versioned
   `-- tracemap-postgres-schema-snapshot: v1` directive.
2. Do not infer snapshot identity from filenames, directories, SQL literals, or
   inactive comment-like text.
3. Emit `PostgresSchemaSnapshotDeclared` under
   `database.postgres.schema-migration.v1` with snapshot format, recognized
   bounded-DDL count, unsupported-DDL count, coverage label, and explicit
   source-database-identity omission.
4. Continue emitting supported bounded table, column, constraint, index, enum,
   routine, and migration-operation facts from the snapshot.
5. Aggregate unsupported `CREATE`, `ALTER`, `DROP`, and `TRUNCATE` families into
   categorical `database.postgres.schema-migration.gap.v1` evidence and label
   the snapshot reduced.
6. Preserve repository-relative span, one-way evidence hash, commit SHA,
   evidence tier, extractor version, coverage, and limitations.
7. Do not retain snapshot comments, source database/server identity, raw SQL,
   literals, snippets, connection material, local paths, or unsupported object
   identities.
8. Do not claim dump generation, completeness, freshness, restoreability,
   execution, live-schema equivalence, production state, or release safety.
