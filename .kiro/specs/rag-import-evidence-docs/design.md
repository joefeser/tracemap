# RAG-Import Evidence Docs Design

## Overview

RAG-import evidence docs is a downstream export over existing TraceMap evidence.
It reads indexes and compatible report artifacts, projects evidence into stable
claim-bearing chunks, and writes Markdown plus JSONL for external ingestion.

The export does not analyze code, reduce impact, rank relevance, embed text,
call models, write vector stores, or answer questions. It only repackages
existing deterministic evidence with citations, gaps, limitations, and safety
metadata.

## Goals

- Provide a first-class command for chunked evidence documentation.
- Preserve TraceMap's evidence contract in every claim-bearing chunk.
- Make downstream ingestion incremental through stable chunk IDs and ordering.
- Emit human-inspectable Markdown and machine-ingestible JSONL from the same
  chunk model.
- Reuse vault export and evidence-pack claim-level safety concepts.
- Compose route-flow, property/value-flow, release-review, evidence pack, vault,
  and combined-report outputs without duplicating analysis logic.
- Keep public/demo outputs safe by default.

## Non-Goals

- No scanner or reducer changes in the docs-export implementation slice.
- No new route-flow, value-origin, impact, diff, or release-review analysis.
- No site work.
- No raw source snippets by default.
- No runtime behavior, production usage, deployment, release approval,
  vulnerability, license, ownership, or business-impact claims.
- No LLM calls, embeddings, vector databases, prompt-based classification,
  natural-language Q&A, or RAG-stack integration in TraceMap core.

## Command Shape

Preferred command:

```bash
tracemap docs-export \
  --index <index-or-combined.sqlite> \
  --out <output-dir> \
  --route-flow-report <route-flow.json> \
  --paths-report <paths-report.json> \
  --reverse-report <reverse-report.json> \
  --combined-report <dependency-report.json> \
  --release-review-report <release-review.json> \
  --vault-graph <graph.json> \
  --evidence-pack <evidence-pack.json> \
  --source-claim-catalog <source-claims.json> \
  --minimum-claim-level hidden|demo-safe|public-safe \
  --families source-overview,endpoint,route-flow,property-flow,dependency-surface,data-surface,package-config,query-sql-shape,legacy,release-review,impact-summary,gap,limitation \
  --format markdown,jsonl \
  --date YYYY-MM \
  --dry-run \
  --force
```

Why `tracemap docs-export`:

- It is visibly an export workflow, not an analysis command.
- It can support both single and combined indexes while allowing optional report
  inputs.
- It avoids overloading `tracemap report`, whose job is human reporting over a
  current evidence view.
- It avoids the term `rag` in the CLI, keeping TraceMap's command language about
  deterministic artifacts rather than downstream AI infrastructure.

Rejected alternatives:

- `tracemap rag export`: too easy to read as RAG integration or AI behavior.
- `tracemap evidence-docs export`: accurate but introduces a nested command
  namespace before the existing CLI has a strong reason for it.
- `tracemap vault export --format rag`: conflates navigable vault notes with
  ingestion chunks and would make safety/collision semantics harder to reason
  about.

`--index` is required for v1 so every export has a scan or combined index root.
Report inputs can enrich chunks but do not replace the indexed source identity
anchor. If a future locked evidence-pack-only workflow is needed, it should be a
separate spec update.

`--format` accepts one comma-separated argument from this closed set:
`markdown`, `jsonl`, or `markdown,jsonl`. Repeating `--format` is invalid in v1.
Out-of-set values and repeated flags are CLI argument errors with sanitized
diagnostics. `manifest.json` is always written for successful exports;
`--format` selects only the Markdown and/or JSONL content artifacts.

`--families` accepts one comma-separated argument using exact lowercase values
from this closed set:

| Family value | Chunk family |
| --- | --- |
| `source-overview` | Source overview |
| `endpoint` | Endpoint and endpoint alignment |
| `route-flow` | Route-centered static flow |
| `property-flow` | Property/value-origin flow |
| `dependency-surface` | Dependency surfaces |
| `data-surface` | Data surfaces |
| `package-config` | Package and config evidence |
| `query-sql-shape` | Query and SQL-shape evidence |
| `legacy` | Legacy evidence families |
| `release-review` | Release-review packet evidence |
| `impact-summary` | Static impact summary evidence |
| `gap` | Gap chunks |
| `limitation` | Limitation chunks |

Unselected supported families are recorded as `not_requested` in the manifest
and do not emit gaps. Requested families that the input cannot support emit
schema or unsupported-family gaps. Empty, whitespace-only, or out-of-vocabulary
family tokens are CLI argument errors, not unsupported-family gaps.

## Input Sources

Supported source classes:

| Input | Initial role | Notes |
| --- | --- | --- |
| Single-source SQLite index | Source and fact evidence | Read-only. Emits source, fact, gap, and limitation chunks for supported tables. |
| Combined SQLite index | Primary cross-source evidence | Read-only. Preserves source labels, commit SHAs, combined IDs, fact IDs, edge IDs, rule IDs, evidence tiers, and coverage. |
| Combined dependency report JSON | Report-derived dependency chunks | Consumes documented JSON only. Unknown sections become schema gaps. |
| Route-flow report JSON | Route-flow chunks | Preserves classifications, rows, supporting IDs, gaps, and limitations. |
| Paths/reverse report JSON | Path and upstream/downstream context | Preserves path/reverse classifications, selectors, supporting IDs, and gaps. |
| Release-review report JSON | Release-review and impact-summary chunks | Preserves release rollups as static report findings, not approvals. |
| Vault graph JSON | Optional identity/link metadata | Deferred until `evidence-graph-vault-export.v1` is confirmed locked; when available, it can supply graph node/edge IDs and claim-level metadata where schema-compatible. Until then, supplied vault graph inputs emit schema gaps. |
| Evidence pack JSON | Curated public/demo proof input | Can promote only fields covered by locked evidence-pack schema and claim metadata. |
| Source claim catalog JSON | Claim-level promotion | Maps stable source identities to `demo-safe` or `public-safe`; display names never promote evidence. |

Compatibility gates:

| Input | Version signal | Minimum fields | Failure behavior |
| --- | --- | --- | --- |
| Index | table/schema metadata | sources or equivalent scan identity, facts, rule/evidence fields where supported | Fail if not a TraceMap index; emit schema gaps for optional family tables. |
| Combined index | `index_sources`, `combined_facts`, combined edge/report tables | source labels, source IDs, commit SHAs, facts, edges, coverage | Fail if core combined schema is missing; optional tables become gaps. |
| Report JSON | `reportType`, `version`, or schema marker | stable row IDs, rule IDs, evidence tiers, source provenance, gaps/limitations | Emit schema gaps for unknown optional sections; fail if no documented fields are usable. |
| Vault graph | `evidence-graph-vault-export.v1` | nodes, edges, gaps, limitations, claim metadata | Reject incompatible versions for promotion; consume safe IDs as supplemental links. |
| Evidence pack | locked pack schema | claim level, source labels, evidence sections, gaps, limitations, safety metadata | Map `local-only` to `hidden`; reject for public/demo promotion if schema or claim metadata is insufficient. |
| Source claim catalog | `source-claim-catalog.v1` | stable source identity, claim level, proof metadata | Unmatched entries emit gaps and do not promote evidence. |

Vault graph integration depends on `evidence-graph-vault-export.v1` being a
locked schema. If the implementation slice finds that schema is not available
or compatible, vault graph claim promotion must be deferred and schema gaps must
be emitted for supplied vault inputs rather than blocking index-based export.

### Source Claim Catalog

`source-claim-catalog.v1` is owned by the vault export workflow and docs export
consumes the same schema. Suggested shape:

```json
{
  "schemaVersion": "source-claim-catalog.v1",
  "entries": [
    {
      "sourceIdentity": {
        "kind": "combined-source",
        "sourceIndexId": "source:...",
        "commitSha": "0123456789abcdef0123456789abcdef01234567",
        "scanId": "scan:..."
      },
      "claimLevel": "demo-safe",
      "proofId": "proof:...",
      "proofPathCategory": "reviewed-public-fixture",
      "reviewer": "reviewer-id",
      "reviewedAt": "2026-06",
      "limitations": []
    },
    {
      "sourceIdentity": {
        "kind": "single-source",
        "scanId": "scan:...",
        "commitSha": "0123456789abcdef0123456789abcdef01234567"
      },
      "claimLevel": "public-safe",
      "proofId": "proof:...",
      "proofPathCategory": "reviewed-public-fixture",
      "reviewer": "reviewer-id",
      "reviewedAt": "2026-06",
      "limitations": []
    }
  ]
}
```

Required fields are `schemaVersion`, `entries`, `sourceIdentity.kind`,
`claimLevel`, `proofId`, `proofPathCategory`, reviewer metadata, and
`reviewedAt`. Valid `sourceIdentity.kind` values for this spec are
`combined-source` and `single-source`. At least one stable identity field such
as `sourceIndexId`, `scanId`, or an approved public `commitSha` is required per
entry. Display labels are optional safe display metadata and never promote
evidence by themselves. If the canonical vault source-claim catalog adds
stricter fields, docs export must follow that canonical schema rather than
forking a second `source-claim-catalog.v1`.

## Output Layout

Directory output:

```text
evidence-docs/
  manifest.json
  chunks.jsonl
  README.md
  index.md
  chunks/
    source-overview/
      <chunk-id>.md
    endpoint/
      <chunk-id>.md
    route-flow/
      <chunk-id>.md
    property-flow/
      <chunk-id>.md
    dependency-surface/
      <chunk-id>.md
    data-surface/
      <chunk-id>.md
    package-config/
      <chunk-id>.md
    query-sql-shape/
      <chunk-id>.md
    legacy/
      <chunk-id>.md
    release-review/
      <chunk-id>.md
    impact-summary/
      <chunk-id>.md
    gap/
      <chunk-id>.md
    limitation/
      <chunk-id>.md
```

`README.md` and `index.md` are generated files. They summarize schema version,
claim level, inputs, generation settings, chunk counts, omitted counts, safety
status, and limitations. They must not provide usage instructions for a specific
RAG stack.

The `chunks/` layout is a convenience for humans. `chunks.jsonl` is the primary
machine-ingestion artifact.

## Schema

### Manifest

Suggested `manifest.json`:

```json
{
  "schemaVersion": "tracemap-evidence-docs.v1",
  "tracemapGenerated": true,
  "contentHash": "",
  "generator": {
    "name": "tracemap-docs-export",
    "version": "tracemap-evidence-docs.v1",
    "generatedAt": "2026-06"
  },
  "claimLevel": "demo-safe",
  "formats": ["markdown", "jsonl"],
  "generationSettings": {
    "families": ["source-overview", "endpoint"],
    "minimumClaimLevel": "demo-safe",
    "includeRawSnippets": false
  },
  "inputs": [],
  "outputs": [],
  "chunkCounts": [],
  "omittedCounts": [],
  "gaps": [],
  "limitations": []
}
```

`contentHash` is computed over canonical JSON with `contentHash` set to the
empty string. Demo/public outputs require explicit `--date YYYY-MM`, and that
value is included in the hash. Hidden outputs without `--date` use the fixed
`generator.generatedAt` sentinel `local-only`, include that sentinel in the
content hash, and remain byte-stable across reruns. Docs export never falls
back to wall-clock time.

### JSONL Chunk

Each `chunks.jsonl` line is one object:

```json
{
  "schemaVersion": "tracemap-evidence-docs.v1",
  "chunkId": "chunk:endpoint:5f2c4d1a91f0b4d8e2c3a011",
  "chunkFamily": "endpoint",
  "chunkType": "claim",
  "claimLevel": "demo-safe",
  "title": "Endpoint evidence",
  "sortKey": "endpoint|source|rule|chunk",
  "summary": "Static endpoint evidence with citations.",
  "bodyMarkdown": "Generated bounded Markdown body.",
  "citations": [],
  "sourceRefs": [],
  "supportingIds": [],
  "ruleIds": [],
  "evidenceTiers": [],
  "coverageLabels": [],
  "gaps": [],
  "limitations": [],
  "redactions": [],
  "links": []
}
```

`bodyMarkdown` is required in v1 for every JSONL chunk. It contains bounded
generated Markdown text for importers and must pass the same Markdown escaping,
prohibited-claim wording, and unsafe-value gates as the corresponding generated
`.md` file.

`chunkType` is a closed vocabulary:

| Value | Meaning |
| --- | --- |
| `claim` | Claim-bearing evidence chunk with citations. |
| `gap` | Gap chunk that documents unavailable, reduced, hidden, or incompatible evidence. |
| `limitation` | Limitation chunk that documents a rule or analysis boundary. |
| `summary` | Non-claim aggregate or index chunk that points to cited child chunks. |

JSONL chunk integrity is maintained through the manifest output hash for
`chunks.jsonl`, not by per-chunk content hashes.

### Gap

Gap shape:

```json
{
  "gapId": "gap:unknown-analysis:...",
  "ruleId": "docs-export.gap.unknown-analysis.v1",
  "evidenceTier": "Tier4Unknown",
  "reason": "reduced-coverage",
  "chunkFamily": "route-flow",
  "sourceRefs": [],
  "supportingIds": [],
  "limitations": []
}
```

`reason` is a closed vocabulary. Initial values include
`reduced-coverage`, `unknown-commit-sha`, `missing-provenance`,
`schema-incompatible`, `unsupported-family`, `hidden-evidence-omitted`,
`claim-level-hidden`, `claim-level-unmatched`, `duplicate-stable-identity`,
`selector-no-match`, `truncated`, and `extractor-unavailable`.

Reason-to-rule mapping:

| Gap reason | Rule ID |
| --- | --- |
| `reduced-coverage` | `docs-export.gap.unknown-analysis.v1` |
| `unknown-commit-sha` | `docs-export.gap.unknown-analysis.v1` |
| `missing-provenance` | `docs-export.gap.missing-provenance.v1` |
| `schema-incompatible` | `docs-export.gap.schema-incompatible.v1` |
| `unsupported-family` | `docs-export.gap.unsupported-family.v1` |
| `hidden-evidence-omitted` | `docs-export.gap.hidden-evidence-omitted.v1` |
| `claim-level-hidden` | `docs-export.gap.claim-level-hidden.v1` |
| `claim-level-unmatched` | `docs-export.gap.claim-level-unmatched.v1` |
| `duplicate-stable-identity` | `docs-export.gap.duplicate-stable-identity.v1` |
| `selector-no-match` | `docs-export.gap.unknown-analysis.v1` |
| `truncated` | `docs-export.gap.unknown-analysis.v1` |
| `extractor-unavailable` | `docs-export.gap.unknown-analysis.v1` |

### Citation

Citation shape:

```json
{
  "citationId": "citation:...",
  "sourceLabel": "api",
  "sourceScope": "combined-source",
  "scanId": "scan:...",
  "commitSha": "0123456789abcdef0123456789abcdef01234567",
  "coverageLabel": "Level1SemanticAnalysisReduced",
  "filePath": "src/App.cs",
  "startLine": 12,
  "endLine": 18,
  "ruleIds": ["csharp.semantic.route.v1"],
  "evidenceTier": "Tier1Semantic",
  "extractorName": "csharp.semantic",
  "extractorVersion": "x.y.z",
  "supportingFactIds": ["fact:..."],
  "supportingEdgeIds": [],
  "supportingReportIds": []
}
```

For unsafe or unavailable fields, use `null`, `unknown`, or a redaction object
with a rule-backed gap. Do not omit required citation keys silently.

## Stable IDs

Stable IDs use SHA-256 over UTF-8 records with explicit context prefixes. ID
input records are length-prefixed UTF-8 fields in this canonical form:
`<field-name-length>:<field-name>=<value-length>:<value>\n`, with fields in the
schema-defined order for the entity. Empty values use `0:` and missing optional
fields are omitted only when the schema explicitly allows omission. Hash outputs
are lowercase hex and truncated to 24 hex characters for v1 IDs. The context
string, field order, length-prefix delimiter format, and truncation length are
schema contract.

Duplicate-identity detection compares the full pre-truncation, context-separated
ID input record and full SHA-256 digest, not only the 24-hex displayed ID. A
truncation collision from different full input records emits
`docs-export.gap.duplicate-stable-identity.v1`; the colliding claim chunks are
omitted from claim output and represented by the gap so the exporter never
chooses a winner.

Suggested contexts:

| Entity | Context |
| --- | --- |
| Chunk | `docs-export/chunk/v1` |
| Citation | `docs-export/citation/v1` |
| Manifest output entry | `docs-export/output/v1` |
| Redaction record | `docs-export/redaction/v1` |
| Gap record | `docs-export/gap/v1` |
| Limitation record | `docs-export/limitation/v1` |

Chunk ID input:

```text
docs-export/chunk/v1
schemaVersion
chunkFamily
chunkType
source stable identity
claim level
primary rule IDs
primary evidence tiers
normalized safe selector or family key
sorted supporting fact IDs
sorted supporting edge IDs
sorted supporting report IDs
safe file path plus line span when part of identity
```

Supporting IDs are evidence, not display text. If supporting IDs are absent for
a family, the design for that family must name a stable substitute or emit an
identity gap.

Per-family file path and line-span identity rules:

| Family | File path/span in chunk ID? | Rationale |
| --- | --- | --- |
| `source-overview` | No | Source identity, scan ID, commit identity, and language define the chunk. |
| `endpoint` | Yes when no stable fact/report ID exists; otherwise supporting ID wins | Distinguishes duplicate routes backed by different source spans without depending on display names. |
| `route-flow` | No when route-flow report ID is available; yes for index-only fallback rows with no stable report ID | Route-flow report IDs should be primary; span is fallback evidence identity. |
| `property-flow` | Yes when stable flow/fact/edge ID is unavailable | Value-origin rows can share symbols but differ by source span. |
| `dependency-surface` | Yes when stable fact/surface ID is unavailable | Distinguishes same safe surface key observed in multiple files. |
| `data-surface` | Yes when stable fact/surface ID is unavailable | Distinguishes repeated data-shape evidence under one source. |
| `package-config` | No when package/config stable metadata and fact ID exist; yes for duplicate metadata without IDs | Avoids file-move churn unless needed to prevent collision. |
| `query-sql-shape` | Yes when stable query/shape fact ID is unavailable | Same query shape can appear at multiple spans. |
| `legacy` | No for evidence-pack/report sections with stable IDs; yes for raw index facts without stable section IDs | Legacy summaries should use section IDs; raw facts need fallback identity. |
| `release-review` | No | Release-review finding/checklist/report IDs define the chunk. |
| `impact-summary` | No when impact/report finding IDs exist; yes only for raw index fallback | Preserves report identity first. |
| `gap` | No | Gap ID is derived from reason, affected family, supporting IDs, and source/report identity. |
| `limitation` | No | Limitation ID is derived from limitation code/rule and affected source/report identity. |

If the required supporting report ID, fact ID, edge ID, or fallback span is not
available for a family, emit `docs-export.gap.missing-provenance.v1` or
`docs-export.gap.unknown-analysis.v1` rather than creating an unstable ID.

## Chunk Families

### Source Overview

Source overview chunks summarize the evidence source:

- source label;
- source index ID or scan ID;
- language;
- commit SHA or approved commit identity;
- build status and analysis level;
- coverage label;
- extractor versions;
- artifact availability;
- source-level gaps and limitations.

They do not summarize every fact in the source unless a summary rule supports
the aggregate.

### Endpoint

Endpoint chunks cover route bindings, HTTP client calls, and endpoint alignment.
They render normalized method/path keys, safe route templates where permitted,
file spans, match classification, dynamic URL reason codes, rule IDs, evidence
tiers, and limitations. They do not render raw URLs, hosts, query strings,
tokens, or endpoint values.

### Route Flow

Route-flow chunks consume route-flow report JSON or compatible path evidence.
They preserve:

- query selector metadata;
- entry evidence;
- route-flow classification;
- path rows;
- business/data logic rows;
- dependency surfaces;
- gap and limitation records.

They retain the route-flow boundary: static evidence only, no runtime request
execution, branch feasibility, DI target proof, database execution, or traffic
claims.

### Property Flow

Property-flow chunks consume value-origin evidence from indexes and reports.
The family name is user-oriented, but the schema should also use
`value-origin` metadata where that is the internal TraceMap term. Chunks cover:

- argument-to-parameter evidence;
- parameter-forwarding edges;
- local/member alias evidence;
- constructor/member origin evidence;
- callback and async boundaries;
- dependency-surface terminals;
- mutation, collection, branch, dynamic, DI, reflection, serializer, generated
  code, and reduced-coverage gaps.

They never claim full taint analysis, runtime values, object identity, or
collection contents.

### Dependency and Data Surfaces

These chunks summarize surfaces already known to TraceMap:

- HTTP routes and clients;
- SQL/query shape and persistence;
- package/config surfaces;
- WCF, remoting, ASMX, WebForms, WinForms, legacy data;
- storage, event/message, and other dependency surface evidence as available.

Rows preserve static evidence and avoid compatibility, vulnerability, runtime
reachability, service ownership, or production usage claims.

### Package, Config, Query, and SQL Shape

These chunks focus on ingestible review surfaces:

- package manager/ecosystem;
- safe package/module identifiers and version metadata;
- config key categories or safe hashes;
- query shape, operation, table/column metadata, source kind, and allowed
  hashes.

Raw SQL, config values, connection strings, secrets, raw URLs, and production
data are forbidden by default.

### Legacy

Legacy chunks compose existing legacy evidence families:

- build environment diagnostics;
- WebForms and WinForms event evidence;
- WCF/service-reference metadata;
- ASMX/SOAP;
- remoting endpoint/registration/channel/object/API;
- legacy data metadata;
- baseline regression summaries;
- legacy evidence-pack sections.

They are review packets over static evidence, not statements that a runtime UI
event fires, a service is reachable, or a legacy behavior occurred.

### Release Review and Impact Summary

Release-review chunks preserve the release-review report as a static packet:

- compared snapshots;
- source identity/coverage;
- changed surfaces;
- contract/API/SQL/package sections;
- path/reverse context;
- release rollup classification;
- checklist items;
- gaps and limitations.

The docs exporter may chunk a release-review checklist item as a retrieval unit,
but it must keep triggering finding IDs and gap IDs. It must not emit merge or
release approval language.

### Gaps and Limitations

Gap and limitation chunks are first-class. They let downstream systems retrieve
the uncertainty near a claim rather than only the positive evidence.

Gap and limitation chunks use docs-export rule IDs when the gap is introduced
by the exporter. They preserve underlying rule IDs when the gap comes from a
source report.

## Claim-Level Model

Claim levels:

| Level | Meaning |
| --- | --- |
| `hidden` | Local/private/unreviewed evidence. Default for raw indexes and reports. |
| `demo-safe` | Reviewed or synthetic evidence suitable for demos, not necessarily public claims. |
| `public-safe` | Reviewed evidence suitable for tracked public docs or demos. |

Promotion inputs:

- source claim catalog;
- vault graph metadata with compatible claim-level proof;
- evidence-pack metadata with locked safety schema.

Promotion rules:

1. Match by stable source identity, not display name.
2. Preserve all lower claim-level limitations in promoted chunks.
3. Split mixed-claim chunks when possible; otherwise cap at the lowest safe
   level.
4. Recompute output classification from included chunks.
5. Emit hidden-evidence omission gaps when filtering changes interpretation.
6. Fail public/demo output if only gaps or summary counts remain.

Evidence-pack `local-only` maps to docs-export `hidden`. Local-only packs may be
summarized only in hidden output and cannot promote demo/public chunks.

Full credible coverage for absence-like chunks means every contributing source
has known commit identity, no source identity conflict, no relevant
`AnalysisGap` facts for the requested family, no required optional table/schema
gaps, no truncation, and a coverage label that the owning report defines as
full or credible for no-evidence conclusions. If any of those conditions is not
met, emit `UnknownAnalysisGap`.

## Redaction and Safety

Docs export should reuse or extract shared safety helpers from vault export and
evidence packs where possible.

Reject in every output string leaf and generated path:

- local absolute paths and home fragments;
- raw remotes;
- raw SQL and SQL text;
- raw config values and connection strings;
- raw URLs, hostnames, query strings, and endpoint addresses;
- captured credentials, tokens, keys, and secret-like strings;
- source snippets and raw expressions;
- analyzer diagnostics and stack traces;
- private sample identifiers and production data;
- unsafe Markdown and frontmatter values.

Allowed public/demo alternatives:

- closed vocabulary labels;
- safe relative file paths;
- source labels proven safe by claim metadata;
- rule IDs and evidence tiers;
- coverage labels;
- approved public commit SHAs;
- context-separated hashes for categories explicitly allowed by the policy;
- category-only redaction records.

Forbidden-to-hash categories include secret-like, credential-like, low-entropy
private, enumerable private, snippet-derived, and production-data values.

## Generated Sentinels

Chunk Markdown frontmatter:

```yaml
---
tracemap_generated: true
tracemap_export_schema: tracemap-evidence-docs.v1
tracemap_generator: tracemap-docs-export
tracemap_content_sha256: <64 lowercase hex chars>
chunk_id: chunk:endpoint:...
chunk_family: endpoint
claim_level: demo-safe
source_labels:
  - api
---
```

Summary Markdown files such as `README.md` and `index.md` use the same
frontmatter sentinel except they replace `chunk_id` and `chunk_family` with
`summary_kind`, such as `readme` or `index`, and may use `source_labels: []`
when the summary covers all included sources.

The Markdown content hash is computed over canonical frontmatter plus body after
UTF-8, LF, and final newline normalization, with the hash field replaced by the
empty string.

Canonical frontmatter serialization uses the fixed key order shown in the
sentinel example, followed by schema-defined optional keys in ordinal key order.
Fixed keys always appear first in the documented order even if optional keys
would sort earlier. Optional key names must not duplicate fixed key names. Only
plain scalars and block arrays are allowed. The hash is computed over that
serialized YAML text plus the Markdown body; generators must not hash an
unordered YAML map.

Fixed chunk frontmatter key order is:
`tracemap_generated`, `tracemap_export_schema`, `tracemap_generator`,
`tracemap_content_sha256`, `chunk_id`, `chunk_family`, `claim_level`,
`source_labels`. Summary frontmatter uses the same order but replaces
`chunk_id`, `chunk_family` with `summary_kind`.

`manifest.json` is recognized as generated only when it has
`schemaVersion: "tracemap-evidence-docs.v1"`, `tracemapGenerated: true`,
`generator.name: "tracemap-docs-export"`, and a self-consistent `contentHash`
computed with `contentHash` empty.

`chunks.jsonl` is recognized as generated only through a valid generated
`manifest.json` in the same output directory. The manifest must include an
output entry for `chunks.jsonl` with schema version, generator name, file size,
line count, and SHA-256 over the exact JSONL bytes. If `chunks.jsonl` exists
without a valid generated manifest or with a mismatched manifest hash, it is a
user-file collision or stale generated file according to the collision rules.

`manifest.json` uses the same generated-file replacement rules as vault export:

1. Create missing generated files.
2. Replace valid generated files after new output passes safety gates.
3. Fail stale or malformed generated files unless `--force` is supplied.
4. Refuse non-generated user files.
5. Never let `--force` bypass safety or claim-level gates.

## Rule IDs

Implementation should add docs-export rule catalog entries before emitting
derived chunks or gaps:

- `docs-export.chunk.source-overview.v1`
- `docs-export.chunk.endpoint.v1`
- `docs-export.chunk.route-flow.v1`
- `docs-export.chunk.property-flow.v1`
- `docs-export.chunk.dependency-surface.v1`
- `docs-export.chunk.data-surface.v1`
- `docs-export.chunk.package-config.v1`
- `docs-export.chunk.query-sql-shape.v1`
- `docs-export.chunk.legacy.v1`
- `docs-export.chunk.release-review.v1`
- `docs-export.chunk.impact-summary.v1`
- `docs-export.chunk.gap.v1`
- `docs-export.chunk.limitation.v1`
- `docs-export.validation.generated-file-stale.v1`
- `docs-export.validation.user-file-collision.v1`
- `docs-export.validation.unsafe-value-rejected.v1`
- `docs-export.validation.prohibited-claim-wording.v1`
- `docs-export.gap.schema-incompatible.v1`
- `docs-export.gap.claim-level-hidden.v1`
- `docs-export.gap.claim-level-unmatched.v1`
- `docs-export.gap.hidden-evidence-omitted.v1`
- `docs-export.gap.duplicate-stable-identity.v1`
- `docs-export.gap.unsupported-family.v1`
- `docs-export.gap.missing-provenance.v1`
- `docs-export.gap.unknown-analysis.v1`

Evidence tier semantics:

- Packaging chunk rules inherit the weakest supporting evidence tier when the
  chunk makes a claim from source/report evidence.
- Exporter-created validation and gap rules use `Tier4Unknown`.
- Summary chunks can use `Tier2Structural` only when they summarize documented
  artifact structure and cite limitations; otherwise they inherit or use
  `Tier4Unknown`.

Rule limitations must document static evidence boundaries, claim-level safety,
redaction, schema compatibility, duplicate identity behavior, and external
ingestion boundaries.

## Code Placement

Suggested implementation placement:

```text
src/dotnet/
  TraceMap.Reporting/
    EvidenceDocs/
      EvidenceDocsCommandOptions.cs
      EvidenceDocsModels.cs
      EvidenceDocsIndexReader.cs
      EvidenceDocsReportReaders.cs
      EvidenceDocsChunkProjector.cs
      EvidenceDocsMarkdownWriter.cs
      EvidenceDocsJsonlWriter.cs
      EvidenceDocsManifestWriter.cs
      EvidenceDocsSafetyValidator.cs
  TraceMap.Cli/
    Program.cs
  tests/TraceMap.Tests/
    EvidenceDocsExportTests.cs
```

Preferred reuse targets:

- source projection and safe rendering helpers from combined reporting;
- route-flow report readers/models where public;
- paths/reverse graph inventory and report schemas;
- vault export generated-file sentinel, stable hash, claim-level, link, and
  redaction helpers;
- evidence-pack safety validator concepts and claim-level proof metadata;
- release-review section status and checklist provenance models;
- shared Markdown escaping and canonical JSON writer utilities.

If reuse requires non-trivial refactoring, land behavior-preserving extraction
with focused tests before docs-export behavior.

## Validation Strategy

Focused test groups:

- schema and model shape;
- deterministic manifest, JSONL, and Markdown bytes;
- hidden output without `--date` using the stable `local-only` sentinel;
- stable IDs and ordering;
- source provenance completeness;
- rule/tier completeness;
- chunk-family projection;
- claim-level filtering and promotion;
- redaction and prohibited content;
- JSONL `bodyMarkdown` Markdown-safety validation;
- generated-file collision handling;
- no network/LLM/vector integration;
- old-schema and unsupported-family gaps.

Recommended commands for implementation PRs:

```bash
dotnet test src/dotnet/TraceMap.sln --filter EvidenceDocs
dotnet build src/dotnet/TraceMap.sln
dotnet test src/dotnet/TraceMap.sln
./scripts/check-private-paths.sh
git diff --check
```

Relevant pinned smoke checks from `docs/VALIDATION.md` should run when shared
combined report, route-flow, release-review, evidence-pack, vault, or adapter
behavior changes. Otherwise document an explicit deferral in the implementation
state.

## Deferred Follow-Ups

- Evidence-pack-only docs export without an index root.
- Vault graph claim promotion if `evidence-graph-vault-export.v1` is not locked
  when implementation begins.
- Optional raw snippet mode behind a separate safety spec.
- HTML browsing over generated chunks.
- Hosted docs or site consumption.
- Connectors for specific external knowledge-base products outside TraceMap
  core.
- Chunk size tuning based on downstream importer feedback, as long as chunk IDs
  and schema versioning remain stable.
