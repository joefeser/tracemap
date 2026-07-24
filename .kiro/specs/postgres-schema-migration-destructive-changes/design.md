# Design

The existing `PostgresSchemaMigrationExtractor` remains the single bounded
checked-in-DDL projector. This slice adds anchored recognition for:

- single-object `DROP TABLE [IF EXISTS] [schema.]table [CASCADE|RESTRICT]`;
- single-subcommand `ALTER TABLE ... DROP [COLUMN] [IF EXISTS] column`;
- single-subcommand `ALTER TABLE ... RENAME [COLUMN] old TO new`; and
- single-subcommand `ALTER TABLE ... RENAME TO new_table`.

Each supported statement emits one `PostgresMigrationOperation`. Existing table
and column declaration fact types remain declaration evidence and are not reused
for removals or renames. Operation properties retain only safe unquoted
identities, categorical operation kind, categorical drop behavior when
applicable, and standard provenance/coverage/limitation fields.

The statement span stores a one-way hash of masked structural text. Raw SQL and
unsupported identities are not projected. Existing top-level comma detection
keeps multi-subcommand `ALTER TABLE` statements as gaps; the anchored
single-object drop grammar keeps multi-object drops as gaps.

Release review adds only `newTableName`, `newColumnName`, and `dropBehavior` to
the schema-migration metadata allowlist. These are static checked-in operation
descriptors, not proof that a destructive action ran or affected data.

This slice does not add live database access, SQL execution, dependency-effect
inference, rollback analysis, quoted identifier support, `DROP INDEX`,
`DROP TYPE`, routine drops, or database-centered reporting.
