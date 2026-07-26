# Requirements

## Purpose

Publish a manager-facing, public-safe demonstration of the shipped
`database-design-review` command without adding extraction or strengthening its
static evidence claims.

## Requirements

1. The site SHALL expose `/database/design-review/` as a manager-first answer to
   the question: “What database design, mappings, operations, and query
   relationships are visible in this repository?”
2. The site SHALL expose `/database/design-review/proof-packet/` and a checked-in
   synthetic JSON asset shaped from `database-design-review/1.0`.
3. The public projection SHALL identify its claim level as `demo`, preserve
   rule IDs, evidence tiers, coverage, repo-relative spans, commit and extractor
   provenance, supporting IDs, limitations, caps, and explicit gaps.
4. The story SHALL distinguish single-index and combined-index behavior. A
   single index has zero route references and
   `SingleIndexRoutePathUnavailable`; a combined index may retain bounded route
   references supported by existing graph evidence.
5. The pages and asset SHALL publish no raw SQL, source snippets or snippet
   hashes, credentials, connection strings, scheduled bodies, machine-local
   paths, private infrastructure identities, command output, raw SQLite, or
   arbitrary fact properties.
6. The pages SHALL prominently state that the packet does not prove live
   database state, execution, applied migrations, runtime reachability,
   provider selection, production schema, compatibility, rollback, release
   approval, DBA approval, or safety to run.
7. The new routes SHALL be discoverable from the existing manager packet,
   manager proof paths, capabilities, outputs, examples, packet examples,
   sitemap, and discovery metadata.
8. Focused validation SHALL cover route structure, public projection shape,
   discovery, inbound links, claim level, limitations, and protected-output
   safety.
