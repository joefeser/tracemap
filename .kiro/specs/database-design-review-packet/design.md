# Database Design-Review Packet Design

## Decision

Add a read-side `database-design-review` report over a combined TraceMap index.
It is a deterministic composition layer, not an extractor. The combined index
is the v1 boundary because it already binds source labels, scan manifests,
commits, facts, dependency surfaces, and graph edges.

## Inputs

The reporter reuses three existing read models:

1. `CombinedDependencyReporter.ReadAsync` for manifests, source coverage, and
   existing fact rows.
2. `CombinedDependencyReporter.BuildSurfaces` for allowlisted SQL/query
   dependency surfaces.
3. `CombinedDependencyPathReporter.BuildReportAsync` once with approved legacy
   and endpoint roots and `sql-query` terminals for bounded proven static paths.

No source files are reopened and no SQL text is reparsed.

## Packet model

`DatabaseDesignReviewDocument` contains:

- a version and `static-evidence` claim level;
- source/commit coverage;
- a deterministic summary;
- table design groups;
- global enum, routine, migration-file, and snapshot items;
- unlinked query references;
- structured gaps; and
- shared non-claims.

Each evidence row uses a common provenance DTO containing:

- source label and commit SHA;
- upstream rule ID and evidence tier;
- repository-relative span;
- extractor ID/version where available;
- supporting fact and edge IDs;
- coverage label; and
- limitations.

Packet-level composition and gap rows use cataloged
`database.design-review.packet.v1` and `database.design-review.gap.v1` rules.

## Table grouping

The PostgreSQL extractor's unquoted identifiers are normalized to lower case.
Schema-less facts use an explicit `default-schema` group and remain visibly
schema-unresolved. Table groups are keyed by source plus normalized
schema/table identity so identical names from different repository sources are
not collapsed.

Declarations and operations remain separate collections. Rename/drop evidence
never mutates an inferred current schema model.

## Query correlation

Existing `sql-query` surfaces may expose a table name. The composer normalizes
only bounded unquoted `schema.table` or `table` identities and correlates them
to a table group in the same source. A match is classified
`static-name-match`. Ambiguous or missing matches become gaps or unlinked query
rows; the composer does not select a database, schema search path, provider, or
runtime connection.

Shape/text hashes are used only as stable identity inputs and are not rendered.

## Route correlation

The path reporter remains the authority for graph traversal and path
classification. The composer consumes only returned paths whose terminal node
is an SQL/query surface. It correlates that terminal's bounded table identity
to a design group using the same source-scoped exact match.

The route row preserves the path classification and all supporting fact/edge
IDs. It does not convert a static path into runtime reachability.

## Status and coverage

The document uses `available`, `partial`, and `unavailable` packet coverage
labels, separate from finding classifications.

- `available`: compatible bounded PostgreSQL design evidence exists and no
  blocking/truncation gaps are present.
- `partial`: useful design evidence exists with explicit gaps, unmatched query
  references, missing route evidence, reduced source coverage, or truncation.
- `unavailable`: no compatible PostgreSQL design evidence can be projected.

Gaps are records, never status values.

## Safety

Projection is allowlist-only. The packet may render bounded PostgreSQL
schema/table/column/constraint/index/enum/routine identifiers and normalized
route templates because they are the local review subject. It never renders
raw SQL, source snippets, SQL hashes, connection material, credentials,
private infrastructure identities, scheduled bodies, validation output, local
paths, or arbitrary fact properties.

## Limitations

The packet describes static repository evidence only. It does not prove
database existence, current schema state, migration application or order,
query execution, route feasibility, runtime provider/connection selection,
permissions, compatibility, data correctness, rollback, release approval, or
safe execution.
