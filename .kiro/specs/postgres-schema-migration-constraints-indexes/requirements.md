# PostgreSQL Constraint/Index Evidence Requirements

## Goal

Advance #435 with a second bounded raw-DDL slice for explicit PostgreSQL
constraint and index design evidence.

## Requirements

1. Extract named primary-key, unique, and foreign-key constraint evidence from
   supported top-level `CREATE TABLE` clauses and single-subcommand
   `ALTER TABLE ... ADD CONSTRAINT` statements.
2. Extract simple `CREATE INDEX` and `CREATE UNIQUE INDEX` evidence, including
   safe table, index, column, uniqueness, and access-method identity.
3. Preserve rule ID, evidence tier, repository-relative span, commit-bound scan
   provenance, extractor version, safe object identity, coverage, and
   limitations.
4. Emit categorical gaps for recognized but incomplete, quoted, expression,
   predicate, action-clause, unnamed, or otherwise unsupported constraint/index
   shapes.
5. Never retain raw SQL, expressions, predicates, literals, quoted or
   unsupported identifiers, connection material, or local paths in fact
   properties.
6. Make no execution-order, applied-migration, live-schema, index-use,
   referential-integrity, compatibility, data, permission, rollback,
   production, safety, or approval claim.
7. Match the new constraint/index facts in SQL schema-change impact as
   `sql-schema-metadata` and no stronger than review tier.
