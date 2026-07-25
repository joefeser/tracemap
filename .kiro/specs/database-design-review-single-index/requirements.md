# Database Design-Review Single-Index Requirements

## Purpose

Allow the existing database design-review packet to consume a scanner-produced
`index.sqlite` directly. Preserve the combined-index packet behavior while
making the useful PostgreSQL, EF mapping, application operation, and query
evidence available before repositories are combined.

## Requirements

1. `database-design-review --index` SHALL accept either `index.sqlite` or
   `combined.sqlite` and reject other SQLite shapes with a bounded error.
2. The implementation SHALL reuse stored facts and manifests only. It SHALL add
   no extraction, source reopening, database connection, SQL execution, runtime
   probing, or Windows behavior.
3. Single-index packets SHALL preserve rule ID, evidence tier, commit SHA,
   repository-relative span, extractor ID/version, supporting fact IDs,
   coverage, and limitations where available.
4. PostgreSQL declarations and operations, EF mappings, application operation
   candidates, and query surfaces SHALL use the existing packet projection and
   safety allowlist.
5. Single-index input SHALL NOT claim route coverage. It SHALL emit zero route
   references and one rule-backed `SingleIndexRoutePathUnavailable` gap that
   explains combined graph evidence is required.
6. The single-index gap SHALL replace per-query and per-operation route-absence
   gaps; absence of a combined graph is not evidence that a route is absent.
7. Combined-index output and route traversal behavior SHALL remain unchanged.
8. Single-index output SHALL remain deterministic, bounded, partial, and free
   of raw SQL, snippets, hashes, connection material, credentials, local paths,
   private infrastructure names, scheduled bodies, or validation output.
9. Focused tests SHALL cover valid single input, reduced route coverage,
   provenance, determinism, protected-value suppression, invalid input, and
   preservation of combined behavior.

