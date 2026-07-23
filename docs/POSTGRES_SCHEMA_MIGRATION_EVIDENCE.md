# PostgreSQL Schema and Migration Evidence

TraceMap's first PostgreSQL schema/migration extraction slice recognizes
explicit `CREATE TABLE` and `ALTER TABLE ... ADD COLUMN` statements in checked-in
`.sql` files. It emits deterministic migration-file, migration-operation,
table, and column facts with rule IDs, Tier 2 evidence, repository-relative
statement spans, commit-bound scan provenance, extractor version, bounded
coverage labels, and limitations.

V0 accepts unquoted PostgreSQL identifiers only. It does not model column types,
defaults, generated expressions, indexes, constraints, enums, routines,
snapshots, EF Core/Npgsql migration APIs, or execution graphs. Incomplete or
unsupported shapes inside the two recognized DDL families emit
`database.postgres.schema-migration.gap.v1` rather than invented objects.
Multi-subcommand `ALTER TABLE` statements are therefore gaps in v0 instead of
partially reported first-column evidence.

Raw SQL, snippets, literals, connection material, and unsupported identifiers
are not stored on these facts. The evidence is checked-in design intent only.
It does not prove that a migration ran, statements ran in order, a live object
exists, schemas are compatible, data is correct, permissions are sufficient,
rollback works, production uses the object, or a release is safe or approved.
