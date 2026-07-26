# Requirements

## Goal

Extend issue #435 with bounded static PostgreSQL enum and routine declaration
evidence from checked-in SQL migrations.

## Requirements

1. Recognize unquoted `CREATE TYPE ... AS ENUM` declarations and emit the safe
   schema/type identity without retaining enum labels.
2. Recognize unquoted `CREATE [OR REPLACE] FUNCTION` and
   `CREATE [OR REPLACE] PROCEDURE` declarations and emit safe schema/routine
   identity plus the routine kind.
3. Emit a migration-operation fact and a specific enum/routine fact under
   `database.postgres.schema-migration.v1`.
4. Preserve repository-relative span, snippet hash, commit SHA, evidence tier,
   extractor version, coverage label, and limitations.
5. Unsupported quoted, incomplete, or otherwise deferred shapes must emit
   categorical `database.postgres.schema-migration.gap.v1` evidence.
6. Do not retain enum labels, routine signatures, parameter names/types,
   return declarations, language clauses, bodies, literals, dynamic SQL,
   connection material, or raw SQL.
7. Do not claim dialect validity, execution, reachability, live object state,
   permissions, transaction behavior, production use, or release safety.
