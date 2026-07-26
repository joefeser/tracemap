# Design

`PostgresSchemaMigrationExtractor` remains the single bounded checked-in DDL
projector. Before statement projection it inspects active line comments through
the existing SQL lexical scanner. Snapshot identity is established only by:

- the standard PostgreSQL dump header; or
- the exact TraceMap v1 snapshot directive.

This prevents filenames and marker-looking string literals from becoming
evidence. The snapshot fact stores only categorical format, statement counts,
coverage, source-identity omission, standard provenance, and limitations.

Existing supported DDL projection remains unchanged. Statements beginning with
`CREATE`, `ALTER`, `DROP`, or `TRUNCATE` that are outside the bounded projector
are counted and categorized without retaining their object identity. One
file-level Tier 4 gap summarizes unsupported families. A marked snapshot with
no recognized bounded DDL still emits snapshot identity plus an explicit
recognized-DDL-unavailable gap; it does not become a migration-file fact.

Release review allowlists only snapshot format, counts, coverage, and the
source-identity omission flag. It does not render comments or raw SQL.

No database connection, `pg_dump` invocation, restore, execution, live
introspection, dump comparison, completeness judgment, or runtime claim is
introduced.
