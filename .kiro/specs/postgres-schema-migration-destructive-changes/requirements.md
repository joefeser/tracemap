# Requirements

## Goal

Extend issue #435 with bounded static PostgreSQL destructive migration evidence
from checked-in SQL.

## Requirements

1. Recognize unquoted, single-object `DROP TABLE` statements and emit the safe
   schema/table identity plus categorical `cascade`, `restrict`, or
   `unspecified` behavior.
2. Recognize single-subcommand unquoted `ALTER TABLE ... DROP COLUMN`,
   `RENAME COLUMN`, and `RENAME TO` statements.
3. Emit `PostgresMigrationOperation` evidence under
   `database.postgres.schema-migration.v1` with safe source identity and safe
   new identity for rename operations.
4. Preserve repository-relative span, snippet hash, commit SHA, evidence tier,
   extractor version, coverage label, statement ordinal, and limitations.
5. Quoted identifiers, multi-object drops, multi-subcommand alterations,
   incomplete statements, and broader destructive DDL must emit categorical
   `database.postgres.schema-migration.gap.v1` evidence.
6. Do not retain raw SQL, literals, source snippets, unsafe identifiers,
   connection material, or local paths.
7. Do not claim execution, execution order, live schema state, dependency
   effects, data loss, rollback behavior, production use, or release safety.
