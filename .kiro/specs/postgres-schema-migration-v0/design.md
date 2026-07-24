# PostgreSQL Schema/Migration v0 Design

Reuse the shipped SQL statement lexer so comments, strings, dollar-quoted
bodies, semicolons, lexical completeness, ordinals, and line spans follow the
same boundary as execution-context and secret-safety evidence.

A dedicated extractor recognizes two anchored structural shapes. It emits one
file fact when at least one supported-family statement or gap exists, operation
facts for create/add intent, table facts for create intent, and column facts for
explicit column declarations. Constraint clauses are excluded from columns.

This regex-backed v0 is intentionally not a PostgreSQL parser. Quoted
identifiers and every other schema family are deferred and documented.
