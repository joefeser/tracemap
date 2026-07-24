# PostgreSQL Constraint/Index Evidence Design

## Boundary

Extend the existing deterministic `PostgresSchemaMigrationExtractor`; do not
add a second SQL reader or a dialect parser.

Supported v1 additions:

- named top-level `CONSTRAINT ... PRIMARY KEY (...)`,
  `CONSTRAINT ... UNIQUE (...)`, and
  `CONSTRAINT ... FOREIGN KEY (...) REFERENCES ... (...)` clauses inside a
  supported `CREATE TABLE`;
- the same named constraint families in a single-subcommand
  `ALTER TABLE ... ADD CONSTRAINT`; and
- simple unquoted `CREATE [UNIQUE] INDEX` statements over an unquoted
  schema/table and a list of simple unquoted columns.

The projection emits `PostgresSchemaConstraintDeclared` and
`PostgresSchemaIndexDeclared` facts beside the existing migration operation,
table, column, and file facts. Safe properties are closed structural fields;
the statement remains represented only by its evidence span and structural
hash.

The existing SQL schema-change impact reducer recognizes table, column,
constraint, and index facts as `sql-schema-metadata`. These matches remain
review-tier because checked-in DDL identity is not live-schema or execution
proof.

## Conservative parsing

The slice accepts only shapes that can be projected without retaining
expressions or prose. Quoted identifiers, check/exclusion constraints,
expression or partial indexes, include/storage/tablespace clauses, foreign-key
actions, unnamed constraints, and other suffixes remain categorical gaps.
Mixed `CREATE TABLE` bodies retain supported columns/constraints and emit a
reduced-coverage gap for deferred clauses.

## Non-claims

These are checked-in static design facts. They do not prove DDL execution,
statement order, live objects, index selection, uniqueness, referential
integrity, data compatibility, permissions, rollback, production use, or
release safety.
