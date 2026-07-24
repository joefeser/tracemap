# PostgreSQL Schema and Migration Evidence

TraceMap's bounded PostgreSQL schema/migration extractor recognizes explicit
`CREATE TABLE`, single-subcommand `ALTER TABLE ... ADD COLUMN`, supported named
constraint, and simple `CREATE [UNIQUE] INDEX` statements in checked-in `.sql`
files. It emits deterministic migration-file, migration-operation, table,
column, constraint, and index facts with rule IDs, Tier 2 evidence,
repository-relative statement spans, commit-bound scan provenance, extractor
version, bounded coverage labels, and limitations.

The current slice accepts unquoted PostgreSQL identifiers only. Constraint
coverage is limited to explicitly named primary-key, unique, and foreign-key
clauses at the top level of supported `CREATE TABLE` statements or in
single-subcommand `ALTER TABLE ... ADD CONSTRAINT` statements. Index coverage
is limited to simple column lists; sort/null ordering is accepted but not
modeled. It does not model column types, defaults, generated expressions,
inline column constraints, check/exclusion expressions, foreign-key actions,
expression/partial/include indexes, enums, routines, snapshots, EF Core/Npgsql
migration APIs, or execution graphs. Incomplete or unsupported shapes inside
the recognized DDL families emit
`database.postgres.schema-migration.gap.v1` rather than invented objects.
Multi-subcommand `ALTER TABLE` statements are therefore gaps instead of
partially reported first-column evidence. A `CREATE TABLE` statement containing
both supported and deferred top-level clauses retains its supported table and
column facts while also emitting an explicit reduced-coverage gap.

Raw SQL, snippets, literals, connection material, and unsupported identifiers
are not stored on these facts. The evidence is checked-in design intent only.
Table, column, constraint, and index facts may participate in
`--sql-schema-delta` matching as `sql-schema-metadata`; those findings remain
review-tier.

It does not prove that a migration ran, statements ran in order, a live object
exists, an index is selected, uniqueness or referential integrity holds,
schemas are compatible, data is correct, permissions are sufficient, rollback
works, production uses the object, or a release is safe or approved.
