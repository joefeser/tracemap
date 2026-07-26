# PostgreSQL Schema and Migration Evidence

TraceMap's bounded PostgreSQL schema/migration extractor recognizes explicit
`CREATE TABLE`, single-subcommand `ALTER TABLE ... ADD/DROP/RENAME COLUMN`,
`ALTER TABLE ... RENAME TO`, single-object `DROP TABLE`, supported named
constraint, simple `CREATE [UNIQUE] INDEX`, `CREATE TYPE ... AS ENUM`, and
`CREATE [OR REPLACE] FUNCTION/PROCEDURE` statements in checked-in `.sql`
files. It emits deterministic migration-file, migration-operation, table,
column, constraint, index, enum, and routine facts with rule IDs, Tier 2 evidence,
repository-relative statement spans, commit-bound scan provenance, extractor
version, bounded coverage labels, and limitations.

Checked-in schema snapshots are recognized only when an active line comment
contains the standard `-- PostgreSQL database dump` header or the explicit
`-- tracemap-postgres-schema-snapshot: v1` directive. A filename such as
`schema.sql` is not enough. Snapshot facts retain format, recognized bounded-DDL
count, aggregate unsupported-DDL count, reduced/full bounded coverage, and an
explicit source-database-identity omission. Comments, database names, server
names, and raw dump text are not projected.

Unsupported `CREATE`, `ALTER`, `DROP`, and `TRUNCATE` families inside an
explicit snapshot are summarized by categorical family and count. Snapshot
coverage becomes reduced, but supported table/column/constraint/index/enum/
routine and migration-operation evidence remains useful. This does not verify
how the dump was generated, whether it is complete or current, whether it can
be restored, or whether it matches a live database.

The current slice accepts unquoted PostgreSQL identifiers only. Constraint
coverage is limited to explicitly named primary-key, unique, and foreign-key
clauses at the top level of supported `CREATE TABLE` statements or in
single-subcommand `ALTER TABLE ... ADD CONSTRAINT` statements. Index coverage
is limited to simple column lists; sort/null ordering is accepted but not
modeled. It does not model column types, defaults, generated expressions,
inline column constraints, check/exclusion expressions, foreign-key actions,
expression/partial/include indexes, enum labels, routine signatures,
parameters, return declarations, languages, bodies, snapshots, EF Core/Npgsql
migration APIs, or execution graphs. Incomplete or unsupported shapes inside
the recognized DDL families emit
`database.postgres.schema-migration.gap.v1` rather than invented objects.
Multi-subcommand `ALTER TABLE` statements are therefore gaps instead of
partially reported first-column evidence. A `CREATE TABLE` statement containing
both supported and deferred top-level clauses retains its supported table and
column facts while also emitting an explicit reduced-coverage gap.

Drop and rename coverage retains only safe unquoted source identity, safe new
identity for renames, and categorical `cascade`, `restrict`, or `unspecified`
drop behavior. Multi-object drops, quoted identifiers, and broader destructive
DDL forms emit gaps. These facts do not establish dependency effects, data loss,
rollback behavior, or that any operation ran.

Raw SQL, snippets, literals, connection material, and unsupported identifiers
are not stored on these facts. Enum values and routine bodies are always
omitted. The evidence is checked-in design intent only.
Table, column, constraint, and index facts may participate in
`--sql-schema-delta` matching as `sql-schema-metadata`; those findings remain
review-tier.

It does not prove that a migration ran, statements ran in order, a live object
exists, an index is selected, uniqueness or referential integrity holds,
schemas are compatible, data is correct, permissions are sufficient, rollback
works, production uses the object, or a release is safe or approved.
