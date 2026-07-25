# Database Design-Review Packet Requirements

## Purpose

Compose already-shipped PostgreSQL schema, migration, snapshot, query-shape,
and proven static route-path evidence into a deterministic review packet. The
packet helps a reviewer understand the database design visible in a combined
TraceMap index without adding extraction, connecting to a database, or
executing SQL.

## Requirements

### 1. Dedicated command and artifacts

1. The CLI SHALL expose `database-design-review --index <combined.sqlite>
   --out <path>`.
2. A directory output SHALL contain `database-design-review.md` and
   `database-design-review.json`.
3. An explicit file output SHALL honor `--format markdown|json` using the
   repository's established report-output behavior.
4. Version 1 SHALL require a combined index so source labels, commit identities,
   dependency surfaces, and cross-source route paths share one provenance
   boundary.

### 2. Reuse existing evidence only

1. The packet SHALL read existing PostgreSQL schema/migration facts,
   PostgreSQL schema gaps, SQL/query dependency surfaces, and existing bounded
   path-report evidence.
2. The implementation SHALL add no SQL parsing or extraction behavior.
3. The implementation SHALL add no database connection, catalog
   introspection, SQL execution, migration execution, application startup,
   runtime probing, or Windows work.
4. Missing or incompatible input SHALL become a rule-backed packet gap rather
   than an inferred conclusion.

### 3. Database design groups

1. The packet SHALL group bounded PostgreSQL evidence by normalized
   schema/table identity when both are statically available.
2. Each table group MAY include table declarations, columns, named
   constraints, indexes, migration operations, query-shape references, and
   route-to-query references.
3. Enum, routine, migration-file, and checked-in snapshot evidence SHALL remain
   reviewable even when it cannot be assigned to a table.
4. Destructive or rename operations SHALL remain operations; they SHALL NOT be
   rendered as current declarations.
5. Unsupported, incomplete, or reduced snapshot coverage SHALL remain
   structured gaps.

### 4. Query and route composition

1. Query evidence SHALL come from the existing combined SQL dependency-surface
   projection and SHALL retain only its allowlisted safe metadata.
2. A query/table association SHALL require an exact normalized static table
   identity match and SHALL be labeled `static-name-match`.
3. Route evidence SHALL be included only when the existing bounded path
   reporter produces a path from an endpoint or approved legacy entry root to
   a SQL/query terminal.
4. Route/table composition SHALL preserve the upstream path classification,
   rule IDs, tiers, supporting fact IDs, supporting edge IDs, and path
   limitations.
5. The packet SHALL NOT infer a route/table relationship from shared source,
   file proximity, naming convention, or an unlinked query fact.
6. Unlinked queries, missing table identity, absent route paths, and traversal
   truncation SHALL be explicit packet gaps or coverage summaries.

### 5. Provenance, safety, and non-claims

1. Every design item, operation, query reference, route reference, and gap
   SHALL carry a rule ID.
2. Upstream evidence SHALL preserve source label, commit SHA, evidence tier,
   repository-relative file span, extractor ID/version where available,
   supporting fact IDs, supporting edge IDs, coverage label, and limitations.
3. Missing commit SHA or extractor provenance for PostgreSQL schema facts SHALL
   produce an incompatible-provenance gap instead of a supported design item.
4. Output SHALL NOT contain raw SQL, snippets, credentials, connection strings,
   scheduled command bodies, local absolute paths, private server names,
   validation output, ticket details, query text hashes, or generic property
   bags.
5. The packet SHALL state that it does not prove SQL execution, migration
   ordering or application, runtime reachability, database existence, schema
   freshness or completeness, production state, data correctness, effective
   permissions, compatibility, rollback, release approval, or that anything is
   safe to run.

### 6. Determinism and bounds

1. JSON property names, collection ordering, identities, and Markdown sections
   SHALL be deterministic for identical combined-index bytes.
2. The command SHALL expose positive caps for design objects, evidence rows,
   route references, and gaps.
3. Truncation SHALL set reduced coverage and emit a rule-backed
   `TruncatedByLimit` gap with omitted counts.
4. Empty compatible input SHALL produce a valid partial packet with explicit
   evidence-unavailable gaps.

### 7. Validation

1. Tests SHALL cover grouped schema evidence, destructive-operation separation,
   snapshots, exact query/table matching, unlinked queries, proven route paths,
   incompatible provenance, truncation, deterministic output, and protected
   value suppression.
2. CLI tests SHALL verify help, argument validation, directory/file outputs,
   and combined-index enforcement.
3. Focused tests, the full solution build and test suite, the private-path
   guard, and `git diff --check` SHALL pass before PR handoff.
