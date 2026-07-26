# Design

The existing `PostgresSchemaMigrationExtractor` remains the single bounded
checked-in-DDL projector. This slice adds prefix recognition for:

- `CREATE TYPE [schema.]name AS ENUM (...)`;
- `CREATE [OR REPLACE] FUNCTION [schema.]name (...) ...`; and
- `CREATE [OR REPLACE] PROCEDURE [schema.]name (...) ...`.

Recognition is intentionally signature-light. Enum labels and every routine
surface after the safe unquoted object name are omitted. The enclosing
`EvidenceSpan` retains only the repository-relative span and a one-way
statement hash. The fact properties retain categorical object/operation kind,
safe schema/object identity, bounded coverage, and limitations.

The existing statement splitter already treats quoted strings, comments, and
PostgreSQL dollar-quoted bodies as lexical regions, so semicolons inside a
routine body do not create invented statements. Incomplete or unsupported
declarations fall through to the existing categorical gap path.

This is extraction only. Enum and routine facts are not treated as table
matches by the SQL schema-delta reducer; database-centered composition remains
a later #438 slice.
