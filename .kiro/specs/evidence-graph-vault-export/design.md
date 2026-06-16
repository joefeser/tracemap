# Evidence Graph Vault Export Design

## Overview

The evidence graph/vault export is a local navigation layer over existing
TraceMap evidence. It takes one or more compatible TraceMap artifacts, projects
safe evidence into a graph model, and renders a deterministic Markdown vault
plus optional `graph.json`.

The export should be useful in Obsidian, but must remain plain Markdown and JSON
so it is reviewable in Git and usable without any proprietary or hosted tool.

This feature deliberately does not run analysis, infer runtime behavior, or
classify impact. It only reshapes existing rule-backed static evidence.

## Proposed Command

Preferred first-class command:

```bash
tracemap vault export \
  --combined-index <combined.sqlite> \
  --portfolio-report <portfolio-report.json> \
  --paths-report <paths-report.json> \
  --reverse-report <reverse-report.json> \
  --release-review-report <release-review.json> \
  --evidence-pack <evidence-pack.json> \
  --source-claim-catalog <source-claims.json> \
  --out <vault-output> \
  --minimum-claim-level demo-safe|public-safe \
  --date YYYY-MM \
  --format markdown,json \
  --dry-run \
  --force
```

This command shape intentionally avoids overloading the existing `tracemap
export --index <path> --format <format>` command. There is no `--claim-level`
option because claim levels are computed from input identity, source claim
catalogs, evidence-pack metadata, and the selected minimum output filter.

If CLI integration is too large for the first implementation, a temporary script
such as `scripts/evidence-graph-vault-export.mjs` is acceptable when
`implementation-state.md` records the migration path. The script must follow
the same schema and safety rules as the future CLI.

## Input Sources

Supported source classes:

| Input | Role | Notes |
| --- | --- | --- |
| Combined SQLite index | Primary evidence graph source | Read-only. Provides sources, facts, edges, coverage, commit SHAs, rule IDs, evidence tiers. |
| Portfolio report JSON | Portfolio/source/surface grouping | Consumes documented JSON only. Unknown sections become schema gaps. |
| Paths report JSON | Forward path evidence | Preserves path classifications, supporting IDs, gaps, and limitations. |
| Reverse report JSON | Reverse query evidence | Preserves reverse classifications, selectors, supporting IDs, gaps, and limitations. |
| Diff report JSON | Snapshot diff evidence | Deferred until a compatible diff report schema is confirmed; preserves diff classifications, gaps, rule IDs, and limitations when implemented. |
| Impact report JSON | Static impact context | Deferred until a compatible impact report schema is confirmed; preserves item/path-context classifications, gaps, rule IDs, and limitations when implemented. |
| Release-review report JSON | Review packet relationships | Preserves static evidence sections and partial/unavailable statuses. |
| Evidence pack JSON | Curated proof packet | Deferred until a locked v1 evidence-pack schema includes claim-level metadata and stable source identity. Raw packet internals are not copied. |
| Source claim catalog JSON | Claim-level promotion input | Optional reviewed catalog that maps stable source identities to `demo-safe` or `public-safe`; display names never promote evidence by themselves. |

The implementation should start with combined index plus existing report JSON
inputs. Evidence packs should be deferred until their schema is locked enough to
carry explicit claim-level metadata.

Compatibility gates:

| Input | Version signal | Minimum required fields | Failure behavior |
| --- | --- | --- | --- |
| Combined SQLite index | `metadata.schemaVersion` or compatible table set | sources, facts or equivalent evidence rows, source IDs, rule IDs, evidence tiers, coverage | Sanitized `InputSchemaUnsupported` error if not combined or missing required tables. |
| Portfolio report JSON | `schemaVersion` or report kind marker | source/surface group summaries and limitations | Emit schema gaps for unknown optional sections; fail if no documented fields are usable. |
| Paths report JSON | `schemaVersion` or path report kind marker | path rows, classifications, rule IDs, supporting IDs, gaps | Emit schema gaps for unknown optional sections; fail if required path rows are missing. |
| Reverse report JSON | `schemaVersion` or reverse report kind marker | reverse rows, selectors, classifications, rule IDs, supporting IDs, gaps | Emit schema gaps for unknown optional sections; fail if required reverse rows are missing. |
| Diff report JSON | Locked diff report schema version | diff rows, classifications, rule IDs, supporting IDs, gaps | Deferred until compatible; unsupported versions emit schema gaps. |
| Impact report JSON | Locked impact report schema version | impact items, classifications, rule IDs, path context, gaps | Deferred until compatible; unsupported versions emit schema gaps. |
| Release-review report JSON | Locked report schema version | evidence sections, classifications, rule IDs, limitations | Deferred until compatible; unsupported versions emit schema gaps. |
| Evidence pack JSON | Locked `evidence-pack.v1` schema | source identity, claim level, proof metadata, safe summaries | Deferred until available; unsupported versions are rejected for public/demo promotion. |
| Source claim catalog JSON | `source-claim-catalog.v1` | stable source identity, claim level, proof ID, proof path category, reviewer metadata | Unmatched catalog entries emit claim-level gaps and do not promote evidence. |

## Output Layout

Suggested output:

```text
vault/
  graph.json
  README.md
  index.md
  sources/
    <source-id>.md
  endpoints/
    <endpoint-id>.md
  surfaces/
    <surface-id>.md
  packages/
    <package-id>.md
  symbols/
    <symbol-id>.md
  rules/
    <rule-id>.md
  gaps/
    <gap-id>.md
  limitations/
    <limitation-id>.md
```

`README.md` and `index.md` are generated files with sentinels. They explain
claim level, coverage, source inputs, visible counts, omitted hidden counts, and
limitations.

The exporter must not overwrite non-generated files. A generated file is
recognized only when its sentinel matches the export schema and generator ID.

## Generated Sentinels

Each generated Markdown file must start with YAML frontmatter that includes the
generated-file metadata:

```yaml
---
tracemap_generated: true
tracemap_export_schema: evidence-graph-vault-export.v1
tracemap_generator: tracemap-vault-export
tracemap_content_sha256: <64 lowercase hex chars>
tracemap_kind: source
claim_level: demo-safe
---
```

The Markdown content hash is computed over the entire canonical note content:
the complete frontmatter block plus body after UTF-8, LF, and final newline
normalization. The `tracemap_content_sha256` value is replaced by an empty
string for hashing; every other frontmatter field is included as-is in canonical
serialization order. The frontmatter must remain parseable by a standard YAML
frontmatter reader and must be the first content in the file so Obsidian-style
tools can read it.

Canonical frontmatter serialization is deliberately narrow: one YAML document,
plain scalar strings, booleans, and block arrays only; no anchors, aliases,
folded scalars, comments, timestamps, or implicit type coercion. Keys use the
documented order below, arrays use schema-defined ordinal ordering, strings are
escaped consistently, and the closing `---` is followed by exactly one blank
line before the Markdown body.

`graph.json` should include:

```json
{
  "schemaVersion": "evidence-graph-vault-export.v1",
  "contentHash": "<64 lowercase hex chars>",
  "generator": {
    "name": "tracemap-vault-export",
    "version": "evidence-graph-vault-export.v1",
    "generatedAt": "2026-06"
  },
  "classification": "demo-safe",
  "inputs": [],
  "nodes": [],
  "edges": [],
  "gaps": [],
  "limitations": []
}
```

The graph content hash is computed over the full canonical JSON document with
`contentHash` set to an empty string. Explicit input-driven fields such as
`generator.generatedAt` are included in the hash, so changing `--date` changes
the hash deterministically.

The `generatedAt` value is an explicit reviewed `YYYY-MM` input for demo/public
outputs, not wall-clock time.

Generated file collision rules:

1. Missing output file: create it.
2. Existing valid generated file with matching schema, generator, and
   self-consistent content hash: replace during a normal re-export only after
   all safety checks for the newly generated content pass.
3. Existing generated file with invalid, missing, or stale content hash: fail
   with `GeneratedFileStale` unless `--force` is supplied and all safety checks
   for the newly generated content pass.
4. Existing non-generated file or user-authored note: fail with
   `UserFileCollision`; never overwrite it.
5. `--force` cannot bypass claim-level, redaction, private-path, or schema
   validation.

## Graph Model

### Stable ID Construction

Stable IDs use SHA-256 over UTF-8 strings with explicit context prefixes. Hash
outputs are lowercase hex and truncated to 24 hex characters for IDs. The full
input category, context string, and truncation length are part of the schema so
future changes require a schema version bump.

| Entity | Stable ID input |
| --- | --- |
| Source node | `node/source/v1` plus intrinsic source evidence: source index ID where available, commit SHA presence/value when public-safe, scanner/extractor version, and language. Claim-catalog promotion is applied after stable ID assignment. |
| Endpoint node | `node/endpoint/v1` plus source stable ID, normalized method, normalized path key, and rule ID; only public/demo-safe endpoint keys may be displayed. |
| Surface node | `node/surface/v1` plus source stable ID, surface kind, normalized safe metadata key, and rule ID. Supporting fact IDs stay on the node as evidence, not in the stable ID input. |
| Package node | `node/package/v1` plus source stable ID, package manager, normalized package name, version when safe, and rule ID. |
| Symbol node | `node/symbol/v1` plus source stable ID, safe symbol identity, and rule ID; supporting fact IDs stay on the node as evidence, not in the stable ID input. Symbols are omitted when identity is unsafe. |
| Rule/gap/limitation/report node | `node/<kind>/v1` plus rule ID, limitation code, gap classification, or report stable identity. |

Edge IDs use `edge/<kind>/v1` plus source node ID, target node ID, rule ID,
evidence tier, classification, and sorted supporting IDs. Missing or duplicate
stable identity emits a gap rather than falling back to display-name merging.

### Node

```json
{
  "id": "node:source:<hash>",
  "kind": "source",
  "claimLevel": "demo-safe",
  "displayName": "public-dotnet-server",
  "sourceId": "source:abc123",
  "sourceLabel": "public-dotnet-server",
  "commitSha": "012345...",
  "language": "csharp",
  "coverage": "Level1SemanticAnalysisReduced",
  "ruleIds": ["combined.report.source.v1"],
  "evidenceTiers": ["Tier2Structural"],
  "supportingFactIds": [],
  "supportingEdgeIds": [],
  "limitations": []
}
```

Node kinds are closed for v1 but additive:

- `source`
- `endpoint`
- `surface`
- `package`
- `symbol`
- `rule`
- `gap`
- `limitation`
- `report`

`gap` and `limitation` nodes are graph navigation nodes. The top-level
`graph.json` `gaps` and `limitations` arrays are the canonical record
collections. Gap/limitation nodes reference those canonical records by stable ID
and do not define a second schema for the same content.

SQL/query, WCF, Remoting, WebForms, HTTP, package config, and legacy data
families are represented as `kind: surface` with `surfaceKind` metadata. This
keeps the graph schema stable as extractor families grow.

Symbol nodes are off by default for demo/public exports unless a safe source
claim catalog or compatible evidence pack proves both the symbol identity and
display name are safe. Private namespaces, internal business terms, and
source-derived identifiers can leak context, so unsafe symbols are omitted with
a symbol-safety gap.

Endpoint nodes follow the same safety rule: normalized endpoint keys can still
contain business terms, so public/demo display requires public/demo source proof
or synthetic checked-in evidence. Otherwise endpoint nodes remain hidden or use
hashed IDs without raw display values.

### Edge

```json
{
  "id": "edge:path:<hash>",
  "kind": "path-evidence",
  "from": "node:endpoint:<hash>",
  "to": "node:surface:<hash>",
  "claimLevel": "demo-safe",
  "classification": "NeedsReviewPath",
  "ruleId": "combined.paths.static-traversal.v1",
  "evidenceTier": "Tier3SyntaxOrTextual",
  "supportingFactIds": [],
  "supportingEdgeIds": [],
  "limitations": []
}
```

Edge kinds include:

- graph primitives: `calls`, `creates`, `argument-passed`,
  `parameter-forward`, `value-origin`, `fact-attached-to-symbol`
- evidence relationships: `surface-evidence`, `endpoint-match`,
  `symbol-reconciliation`, `wcf-service-reference`, `remoting-evidence`,
  `webforms-event-flow`, `legacy-root-selection`
- report relationships: `path-evidence`, `reverse-evidence`,
  `release-review-evidence`, `portfolio-relationship`

Edges never prove runtime execution. Every edge is evidence of a static
relationship under the cited rule ID and evidence tier.

## Markdown Notes

Notes should include:

- safe title
- generated YAML frontmatter sentinel as the first content in the file
- summary section
- evidence table with rule IDs, evidence tiers, coverage, supporting IDs
- outgoing links
- incoming links or backlinks generated from the manifest
- gaps and limitations

Example frontmatter:

```yaml
---
tracemap_generated: true
tracemap_export_schema: evidence-graph-vault-export.v1
tracemap_generator: tracemap-vault-export
tracemap_content_sha256: <64 lowercase hex chars>
tracemap_kind: endpoint
claim_level: demo-safe
source_id: node-source-abc123
rule_ids:
  - combined.report.endpoint.v1
evidence_tiers:
  - Tier2Structural
coverage:
  - Level1SemanticAnalysisReduced
limitations: []
tags:
  - tracemap/endpoint
  - tracemap/tier2structural
---
```

Frontmatter values must be safe closed-vocabulary values or stable IDs. Raw
paths, raw URLs, SQL/config values, snippets, and private names are rejected.

Generated notes use one frontmatter block. The canonical key order is:
`tracemap_generated`, `tracemap_export_schema`, `tracemap_generator`,
`tracemap_content_sha256`, `tracemap_kind`, `claim_level`, then node-specific
metadata keys in schema order: `source_id`, `rule_ids`, `evidence_tiers`,
`coverage`, `limitations`, and `tags`. Unknown frontmatter keys are not emitted
in v1.

Links use standard relative Markdown syntax such as
`[safe label](../surfaces/node-surface-abcd1234.md)`. Wiki links are not part of
v1 because their parsing and alias behavior vary by tool.

## Claim-Level Filtering

The exporter computes claim level at node, edge, gap, and export level.

Recommended levels:

| Level | Meaning |
| --- | --- |
| `hidden` | Local/private/operator-only evidence, unsafe source identity, or unreviewed input. |
| `demo-safe` | Synthetic, checked-in, or reviewed public-safe evidence suitable for a local demo. |
| `public-safe` | Reviewed public evidence or synthetic fixtures suitable for public publishing. |

Filtering is explicit and produces a new output set. Hidden inputs are not
silently dropped. If filtering removes relationships that affect graph
interpretation, the output must include sanitized omission gap nodes. Summary
counts may supplement those gaps but do not replace them.

Raw combined indexes and report JSON inputs default to `hidden`. Promotion to
`demo-safe` or `public-safe` requires either a compatible evidence pack with
locked claim-level metadata or an explicit `--source-claim-catalog` entry.
Catalog entries match stable source identity, not display name. A stable source
identity may include source index ID, commit SHA category or value when safe,
scanner/extractor version, language, and a catalog-owned proof ID. Unmatched or
ambiguous catalog entries emit claim-level gaps and do not promote evidence.

`--minimum-claim-level demo-safe` and `--minimum-claim-level public-safe` fail
with
`NoVisibleEvidenceAfterFiltering` when no graph nodes remain visible after
filtering. Summary counts, omitted hidden counts, and top-level metadata do not
count as visible graph evidence.

## Redaction Strategy

Use existing TraceMap helpers where available:

- safe relative paths only
- source labels neutralized when unsafe
- raw remotes omitted
- SQL/config/endpoint values represented by stable shape or hash metadata
- snippets omitted
- secrets and low-entropy values omitted instead of hashed

Hashing policy:

| Input category | Public/demo handling |
| --- | --- |
| Stable internal IDs from TraceMap tables | May be context-hashed for IDs when they are not secret-like and are not displayed raw. |
| Commit SHA | May be displayed or hashed only when the source claim level permits it; otherwise record presence/category only. |
| Public package name/version | May be displayed as dependency metadata when source claim permits it. |
| Endpoint keys, table names, symbol names | Display only with explicit public/demo source proof; otherwise omit or use category-only gaps. |
| Raw local paths, remotes, private names, SQL, config, snippets, diagnostics | Reject, omit, or category-label; do not hash into tracked public/demo output. |
| Secrets, tokens, connection strings, low-entropy values | Reject or category-label; never hash for public/demo output. |

Validation scans every generated Markdown file and every JSON string leaf. It
reports category plus JSON pointer or Markdown section/line without echoing the
unsafe value.

## Vault Export Rule Namespace

The exporter preserves source rule IDs for evidence it projects. Projection-only
formatting does not add new evidence and does not need a new rule ID. Any gap,
limitation, omission, identity decision, or validation finding created by the
vault exporter itself uses the `vault-export.*.v1` namespace.

Initial rule IDs:

- `vault-export.gap.schema-incompatible.v1`
- `vault-export.gap.claim-level-hidden.v1`
- `vault-export.gap.claim-level-unmatched.v1`
- `vault-export.gap.hidden-evidence-omitted.v1`
- `vault-export.gap.duplicate-stable-identity.v1`
- `vault-export.gap.unsafe-symbol-omitted.v1`
- `vault-export.validation.generated-file-stale.v1`
- `vault-export.validation.user-file-collision.v1`
- `vault-export.validation.unsafe-value-rejected.v1`

Each implementation PR that introduces one of these rule IDs must document the
rule and limitation in the rule catalog before emitting it.

## Determinism

Stable ordering:

1. node kind
2. stable node ID
3. source stable ID
4. display name

Edges sort by:

1. edge kind
2. from node ID
3. to node ID
4. rule ID
5. stable ID

JSON is canonical: UTF-8 without BOM, LF endings, two-space indentation, final
newline, ordinal object keys, and schema-defined array ordering.

Markdown rendering uses the canonical graph model. A generated note with a
missing or stale sentinel fails validation.

## Implementation Slices

Recommended PR 1: implementation MVP

- script fallback or CLI command for `combined-index` plus paths/reverse report
  JSON inputs
- graph manifest
- Markdown notes for sources, endpoints, surfaces, gaps, rules, and limitations
- source claim catalog support for explicit demo/public promotion
- claim-level filtering
- redaction validation
- stale sentinel validation
- deterministic tests

Recommended PR 2:

- portfolio and release-review input support after their schema compatibility
  fields are confirmed
- evidence-pack support only after a locked v1 schema includes claim-level
  metadata
- richer symbol notes where safe
- richer index pages and backlink pages

Recommended PR 3:

- first-class CLI migration if PR 1 used script fallback
- public demo workflow integration
- optional site-safe summary generation for site specs to consume

## Error Handling

Errors are sanitized and category-based:

- `InputMissing`
- `InputSchemaUnsupported`
- `InputClaimLevelHidden`
- `UnsafeValueRejected`
- `GeneratedFileStale`
- `UserFileCollision`
- `DuplicateStableIdentity`
- `NoVisibleEvidenceAfterFiltering`
- `PrivatePathRejected`

Diagnostics must not echo unsafe values.

`InputClaimLevelHidden` is emitted only when the caller requests a
`--minimum-claim-level` filter that cannot be satisfied by the available claim
catalog or evidence-pack metadata. Inputs that simply default to `hidden`
continue as hidden exports until a filter requires promotion.

## Relationship To Site Work

The site may later consume public-safe generated summaries, screenshots, or
static examples from this export. This spec does not create site files, site
routes, public marketing copy, or hosted artifacts.

## Open Questions

- Whether the first implementation should live in .NET CLI or a Node script.
  Recommendation: prefer .NET if existing report readers are easy to reuse;
  otherwise ship a script MVP and document migration.
- Whether generated Markdown should include one note per symbol by default or
  only when requested. Recommendation: default symbols off unless safe symbol
  identity and display names are available.
- Whether public-safe exports should allow checked-in generated vault fixtures.
  Recommendation: not by default; use validation and optional demo artifacts
  only after output size and privacy rules are proven.
