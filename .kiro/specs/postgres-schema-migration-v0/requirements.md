# PostgreSQL Schema/Migration v0 Requirements

## Goal

Advance #435 with the first bounded extraction slice for explicit raw
PostgreSQL DDL.

## Requirements

1. Extract migration-file, create-table, table, and declared-column evidence
   from unquoted `CREATE TABLE` statements.
2. Extract add-column operation and column evidence from unquoted
   `ALTER TABLE ... ADD COLUMN` statements.
3. Preserve rule ID, evidence tier, repository-relative span, commit-bound scan
   provenance, extractor version, safe object identity, coverage, and
   limitations.
4. Emit categorical gaps for unreadable, incomplete, or unsupported shapes
   within the recognized statement families.
5. Never retain raw SQL, snippets, literal values, connection material, local
   paths, or unsupported identifiers in fact properties.
6. Make no execution, live-schema, compatibility, data, permission, rollback,
   production, safety, or approval claim.
