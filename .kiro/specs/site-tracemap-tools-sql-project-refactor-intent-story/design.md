# Site SQL Project Refactor Intent Story Design

## Placement

- Article: `/blog/sql-project-refactor-intent-evidence/`
- Compact proof: `/sql/project-refactor-intent/`
- Asset: `/assets/sql-project-refactor-intent-proof-packet.json`

The article explains the schema-diff ambiguity and reviewer narrative. The
compact page exposes rule, tier, provenance, row, gap, downstream-review, and
owner-question fields. The JSON asset is the deterministic public projection.

## Evidence decisions

- Fixture-backed rows are limited to the project/log link, table rename, and
  schema move produced by the checked-in sample.
- Column rename is shown as an `illustrative-supported-category`, never as a
  sample fact.
- Gap rows are labeled `illustrative-gap-shape` and grounded in the gap rule
  catalog rather than misrepresented as gaps emitted by the clean sample.
- Raw operation keys and their hashes are omitted from the public projection.

## Safety

The focused validator scans rendered, decoded, tag-collapsed, metadata, and
asset text for private paths, credentials, connection material, raw operation
keys, raw SQL/XML, copyable deployment commands, private infrastructure
sentinels, and positive deployment/approval/safety claims. Teaching non-claims
remain inside explicit boundary markers.

Primary navigation remains unchanged.
