# Evidence Export Usability Polish Design

## Overview

This design adds a usability layer on top of the existing deterministic vault
and docs exporters. The goal is to make generated artifacts easier to browse,
import, and cite while preserving TraceMap's rule-backed evidence model.

The implementation should treat existing exporter safety behavior as the
baseline:

- generated Markdown, JSON, JSONL, and manifests keep sentinels and content
  hashes;
- public/demo output remains strict;
- hidden/local output may redact, hash, category-label, or omit values only
  through documented deterministic policy;
- no source snippets, raw SQL, config values, secrets, raw remotes, local
  absolute paths, exact private routes, private repo names, or analyzer logs are
  rendered in public/demo output;
- LLMs, embeddings, vector databases, and prompt-based classification remain
  outside TraceMap core.

## Current State

`tracemap vault export` already produces deterministic Markdown notes plus
optional `graph.json` over existing TraceMap evidence. It has generated-file
sentinels, content hashes, claim levels, strict public/demo validation, hidden
local safety handling, graph nodes/edges, gaps, limitations, and collision
protection.

`tracemap docs-export` already produces deterministic evidence documentation
with `manifest.json`, `chunks.jsonl`, Markdown chunk files, chunk families,
claim levels, redaction checks, and generated-file collision protection.

The polish needed here is mostly presentation and schema ergonomics:

- improve first-open navigation;
- split indexes by review question;
- make safe display text easier to scan;
- expose closed tags and aliases for Markdown tools;
- improve graph categories without adding conclusions;
- make chunk records claim/citation-first for downstream import.

## Non-Goals

- No new scanner or reducer conclusions.
- No runtime proof, telemetry, ownership, vulnerability, release-approval, or
  production behavior claims.
- No LLM calls, embedding generation, vector writes, semantic search service,
  prompt classification, or model-generated ranking in TraceMap core.
- No use of RAG or vector output as evidence.
- No publication of raw private, secret, source, route, SQL, config, remote, or
  machine-local values.
- No hosted UI or site work.

## Vault Navigation Model

### Top-Level Files

Suggested generated top-level layout:

```text
vault/
  Start Here.md
  index.md
  graph.json
  sources/
  endpoints/
  routes/
  symbols/
  surfaces/
  packages/
  reports/
  rules/
  gaps/
  limitations/
```

`Start Here.md` is the human entry point. Plain Markdown links SHALL use
percent encoding, for example `[Start Here](Start%20Here.md)`. Obsidian-style
wikilinks MAY be emitted as `[[Start Here]]` in supplemental text sections
where they do not replace canonical links. `README.md`, when currently emitted
by the exporter, must remain generated and cross-link to `Start Here.md` unless
an explicit schema/versioned contract change replaces it. `index.md` remains a
compact machine-friendly all-content index for users and tests that already
expect an index file. All entry notes are generated and protected by the
existing sentinel rules.

If changing the exact entry file name or percent-encoded link handling would
break existing users, implement an aliasing approach:

- keep `README.md` or `index.md` where currently required;
- add `Start Here.md` as a generated note;
- link all entry notes to each other;
- record the naming choice in docs and tests.

### Start Here Sections

The start page should use deterministic sections:

1. `Evidence Summary`
2. `Coverage And Claim Level`
3. `Start With`
4. `Review Queues`
5. `Indexes`
6. `Gaps And Limitations`
7. `Source Artifacts`

These headings are safe closed strings. Section content is generated from
counts, classifications, coverage labels, claim level, and safe links. The page
should not include raw source details.

`Start With` should link to high-value deterministic entry points:

- endpoints index when endpoint or route evidence exists;
- surfaces index when dependency surfaces exist;
- gaps index when any gap exists;
- limitations index when limitations exist;
- rules index when rule evidence exists.

`Review Queues` should group needs-review, weak evidence, reduced coverage, and
gaps using rule IDs and evidence tiers. The queue is not a priority ranking; it
is a deterministic grouping of already-labeled evidence.

### Folder Index Pages

Each generated folder with content should have an `index.md` page. Indexes
should be sorted by:

1. closed category ordinal;
2. safe display title sort key;
3. stable ID.

Folder indexes should show:

- count summaries;
- coverage labels;
- claim level;
- primary rule IDs;
- evidence tier distribution;
- needs-review and gap counts;
- deterministic links to notes.

Indexes should avoid table cells with long unsafe source values. Use short
safe titles and metadata blocks or lists when public/demo safe.

## Safe Display Titles

### Title Record

Renderers should create a title record before Markdown, graph, or chunk
serialization:

```json
{
  "displayTitle": "Endpoint evidence: GET route",
  "titleKind": "safe-derived",
  "stableId": "node:endpoint:...",
  "aliases": ["endpoint-evidence", "tier2-structural"],
  "redactions": [],
  "limitations": []
}
```

`titleKind` is a closed vocabulary:

- `safe-source-label`
- `safe-rule-id`
- `safe-category`
- `safe-relative-path`
- `safe-symbol`
- `safe-route-template`
- `safe-composite`
- `safe-evidence-summary`
- `redacted-category`
- `hash-fallback`
- `stable-id-fallback`

Public/demo output may use only records that pass strict validation. Hidden
output may use `redacted-category`, `hash-fallback`, or safe hidden/local
contexts from the existing safety policy.

When a title combines multiple safe sources, such as category plus evidence
tier plus safe route template, use `titleKind: safe-composite` and record the
contributing closed or validated sources in `titleSources` for test
validation. Summary titles such as count-only folder headings should use
`titleKind: safe-evidence-summary`.

### Title Sources

Allowed title sources:

- rule IDs;
- evidence tiers;
- coverage labels;
- claim levels;
- node kinds and edge kinds;
- surface kinds;
- classifications;
- gap and limitation kinds;
- public-safe source labels;
- safe repo-relative paths and spans;
- safe symbols or route templates after context validation;
- stable TraceMap IDs.

Disallowed title sources:

- local absolute paths;
- raw remotes;
- raw URLs or endpoint addresses;
- exact private routes;
- raw SQL;
- config values;
- connection strings;
- source snippets;
- analyzer diagnostics;
- secrets or secret-like raw values in public/demo output;
- private repo or sample names.

### Aliases

Aliases should be bounded and deterministic. Suggested alias categories:

- safe title;
- stable ID short form;
- rule ID;
- evidence tier tag text;
- coverage label text;
- surface kind text;
- classification text.

The title record is the single source for generated aliases. Frontmatter,
Markdown body sections, graph display metadata, and docs-export chunks should
consume that same alias array rather than recomputing aliases independently.
Aliases SHALL be sorted by category ordinal in this order: safe title, stable
ID short form, rule ID, evidence tier, coverage label, surface kind,
classification. Values within each category sort lexicographically by their
rendered safe form.

Aliases are not evidence identities. Stable IDs and supporting IDs remain the
canonical join keys in frontmatter, graph JSON, and chunks.

## Tags

Tags should use a closed vocabulary with prefixes:

```text
tracemap/claim/hidden
tracemap/claim/demo-safe
tracemap/claim/public-safe
tracemap/tier/tier1semantic
tracemap/tier/tier2structural
tracemap/tier/tier3syntaxortextual
tracemap/tier/tier4unknown
tracemap/coverage/reduced
tracemap/coverage/partial
tracemap/review/needs-review
tracemap/gap/analysis-gap
tracemap/kind/endpoint
tracemap/kind/surface
tracemap/surface/sql-query
```

The exact strings should be finalized during implementation and documented. All
tag values must pass public/demo scalar validation before they are accepted as
closed vocabulary.

Tags SHALL be sorted lexicographically by the full `tracemap/`-prefixed string
after generation and before serialization.

Tags are navigation aids only. They must not replace rule IDs, evidence tiers,
coverage labels, limitations, or supporting IDs.

## Graph Category Model

### Node Categories

Recommended v1 polish categories:

| Category | Use |
| --- | --- |
| `source` | Scan or combined source identity. |
| `endpoint` | Static endpoint evidence. |
| `route` | Route template, route-flow, or path route evidence. |
| `symbol` | Safe symbol identity. |
| `surface` | Dependency or data surface evidence. |
| `package` | Package/dependency metadata. |
| `report` | Report-derived evidence section. |
| `rule` | Rule catalog entry. |
| `gap` | Analysis, schema, safety, or omission gap. |
| `limitation` | Documented limitation. |
| `chunk` | Optional docs-export chunk reference when docs and vault outputs are linked. |

The existing node kinds may remain canonical if changing them would break
schema compatibility. In that case, add `displayCategory` or
`navigationCategory` as an additive field.

### Edge Categories

Recommended v1 polish categories:

| Category | Meaning |
| --- | --- |
| `describes` | A note, report, or chunk describes an evidence entity. |
| `supports` | Evidence supports a claim or gap statement. |
| `links-to-rule` | Evidence references a rule. |
| `has-limitation` | Evidence carries a limitation. |
| `has-gap` | Evidence carries a gap. |
| `static-path-evidence` | Existing path evidence, preserving original classification. |
| `route-flow-evidence` | Existing route-flow evidence. |
| `surface-evidence` | Existing dependency/data surface evidence. |
| `symbol-evidence` | Existing symbol evidence. |
| `report-evidence` | Report-derived relationship. |

These categories are static documentation relationships. They do not imply
runtime execution, reachability, ownership, deployment, vulnerability,
production traffic, or release approval.

## Optional Review-Friendly Graph Mode

An optional mode can be useful if implemented as deterministic filtering:

```bash
tracemap vault export \
  --graph-mode full|review
```

`full` keeps current behavior. `review` SHALL include a node or edge when any
documented predicate is true:

- evidence tier is `Tier3SyntaxOrTextual` or `Tier4Unknown`;
- classification contains `NeedsReview` or known weak/static review labels;
- coverage label is reduced, partial, unsupported, or unknown;
- node kind is `gap` or `limitation`;
- a supported report input says the item is unstable or unresolved;
- rule ID belongs to an exporter safety, schema, omission, or gap rule family.

Tier1/Tier2 evidence may still appear in review mode when another predicate is
true, such as a needs-review classification or reduced coverage label. If the
implementation cannot keep this boolean logic crisp, defer the mode and record
the deferral as a rule-backed limitation or gap.

The mode must record:

- `graphMode`;
- selection predicates;
- selected counts;
- omitted counts by category;
- partial status;
- limitations.

If no crisp deterministic predicates are accepted during implementation, defer
this feature. Do not implement subjective importance.

## Docs-Export Chunk Model

### Chunk Families

Existing families should remain compatible. Question-oriented families are
presentation views over canonical `chunkFamily` records unless implementation
documents a new physical chunk family. Additive or renamed presentation
families should map to canonical family IDs:

| Family | Review Question | Availability |
| --- | --- | --- |
| `endpoint-question` | What static evidence describes this endpoint or route? | Compatible endpoint, route-flow, path, reverse, combined, vault, or report input. |
| `data-surface-question` | What code has static evidence of touching this data surface? | Compatible surface evidence input. |
| `package-question` | What package/dependency metadata is present? | Compatible package or dependency metadata input. |
| `snapshot-change-question` | What changed between evidence snapshots? | Only when docs-export receives explicit release-review input or a future supported diff/snapshot report; otherwise emit `docs-export.gap.unsupported-question-family.v1` with `Tier4Unknown`. |
| `weak-evidence-question` | What evidence is lower-tier, ambiguous, reduced, or needs review? | Compatible evidence with lower-tier, reduced, weak, or needs-review labels. |
| `gap-question` | What could TraceMap not prove or parse? | Any compatible input with gaps or unsupported requested families. |
| `limitation-question` | What limitations constrain the claim? | Any compatible input with limitations. |

If implementation prefers not to add new family IDs, keep existing IDs and add
`questionFamilies` as an additive array field. This reduces schema churn while
making RAG import easier.

Cross-cutting question families such as `weak-evidence-question`,
`gap-question`, and `limitation-question` should default to views over existing
canonical chunks. They should not duplicate chunk records unless a future schema
explicitly requires distinct records and proves stable identities, citations,
and byte ordering.

`questionFamilies` SHALL be an ordered array because one canonical chunk can
belong to both a primary view and one or more cross-cutting views. For example,
a lower-tier endpoint chunk can carry both `endpoint-question` and
`weak-evidence-question` while preserving the same `chunkId`. The array sorts
by family ordinal from the closed table above, then lexicographically for any
future additive values. Implementations MAY also emit separate view indexes
keyed by `chunkId`, but those indexes must be derived from the same
`questionFamilies` memberships and must not duplicate chunks.

### Claim/Citation-First Record

Recommended chunk shape:

```json
{
  "schemaVersion": "tracemap-evidence-docs.v1",
  "chunkId": "chunk:...",
  "chunkFamily": "endpoint",
  "questionFamilies": ["endpoint-question", "weak-evidence-question"],
  "title": "Endpoint evidence: GET route",
  "sectionTitle": "What static evidence describes this endpoint?",
  "claim": {
    "kind": "static-evidence",
    "text": "TraceMap has static endpoint evidence for this route.",
    "classification": "NeedsReview",
    "claimLevel": "demo-safe"
  },
  "citations": [
    {
      "ruleId": "route-flow.report.endpoint.v1",
      "evidenceTier": "Tier2Structural",
      "supportingFactIds": ["fact:..."],
      "supportingEdgeIds": [],
      "supportingReportIds": ["report:..."],
      "safeSourceSpans": [
        {
          "path": "safe/relative/path.cs",
          "startLine": 12,
          "endLine": 18
        }
      ],
      "coverageLabels": ["reduced"],
      "limitations": ["static-evidence-only"]
    }
  ],
  "redactions": [],
  "limitations": [],
  "bodyMarkdown": "..."
}
```

`bodyMarkdown` should be rendered from the structured fields so Markdown and
JSONL cannot drift.

### Chunk Body Sections

Recommended deterministic Markdown sections:

1. `Claim`
2. `Citations`
3. `Coverage`
4. `Limitations`
5. `Related Evidence`
6. `Redactions`

For gap chunks, replace `Claim` with `Gap Statement` or use a claim kind of
`gap`. Keep headings from a closed safe vocabulary.

## Hidden/Local Redaction Behavior

The hidden/local profile should share safety machinery between vault export and
docs-export where practical. The classifier should decide per value context:

- allow safe raw hidden/local context;
- render a deterministic hash;
- render a closed category label;
- omit with a rule-backed gap;
- reject before writing.

### Redactable Categories

Hidden/local output may use redact, hash, category-label, or omit behavior for
bounded values that are useful for local navigation but unsafe or too revealing
for publication. Examples:

- long or internal symbol display names: hash, abbreviate, or fall back to the
  stable ID;
- repo-relative paths with internal directory structure: use a category such as
  `internal-relative-path` or a context hash;
- local branch names in metadata: omit or hash;
- internal identifiers in titles or aliases: use `stable-id-fallback`;
- safe route templates that contain sensitive-looking words but no raw URL,
  hostname, query string, or exact private route: category-label or hash.

These examples are hidden/local only and must remain labeled when they affect
interpretation.

Hard-fail categories remain hard failures in every profile:

- credentials, API keys, access tokens, private keys, authorization headers,
  passwords, captured secret values;
- connection strings and unsafe config values;
- raw URLs, raw remotes, raw endpoint addresses, exact private routes;
- local absolute paths, home/temp/drive/UNC/file paths;
- raw SQL, SQL batches, stored procedure bodies;
- source snippets, analyzer diagnostics, stack traces, tool logs;
- private repo names, private sample identifiers, production data.

Public-safe and demo-safe output must continue to reject unsafe-looking
strings. Hidden/local redaction never promotes hidden content to public/demo.

## Generated File Safety

All polish rendering should happen in memory before any files are written:

1. read inputs;
2. project graph/chunks;
3. compute titles, aliases, tags, and categories;
4. apply claim-level filtering;
5. apply hidden/local safety transforms where allowed;
6. render Markdown/JSON/JSONL;
7. run final safety validation;
8. check generated-file sentinels and user-file collisions;
9. write files atomically or leave output unchanged on failure.

`--force` remains limited to stale generated-file replacement after new content
passes validation. It must not bypass safety, claim-level filtering, schema, or
collision rules.

## Rule IDs

Implementation should add exporter rule IDs only when behavior is new. Suggested
families:

```text
vault-export.navigation.start-here.v1
vault-export.navigation.index.v1
vault-export.navigation.title-redacted.v1
vault-export.navigation.tag-omitted.v1
vault-export.graph.review-filter.v1
docs-export.chunk.question-family.v1
docs-export.chunk.claim-citation.v1
docs-export.redaction.hidden-title.v1
docs-export.gap.unsupported-question-family.v1
```

Existing docs-export rule to preserve:

- `docs-export.gap.unsupported-family.v1`: already covers requested canonical
  CLI chunk families that are unavailable or unsupported for the supplied input
  schema.

Required for Requirement 8 question-family views:

- `docs-export.gap.unsupported-question-family.v1`: emitted when a requested
  additive `questionFamilies` view cannot be supported by the input schema, such as
  `snapshot-change-question` without release-review or compatible future
  diff/snapshot input. Evidence tier: `Tier4Unknown`. Limitation: static schema
  limitation or input-combination limitation, not runtime unavailability. It
  SHALL NOT be emitted for the same condition that already emitted
  `docs-export.gap.unsupported-family.v1`; implementation must choose the rule
  matching the requested layer.

Each rule needs documented emitted facts or records, evidence tier, limitations,
and safety behavior in `rules/rule-catalog.yml`. Exporter-created gaps that
represent unknown or omitted evidence should default to `Tier4Unknown` unless a
stronger tier is explicitly justified.

## Compatibility

Prefer additive schema changes:

- keep existing `schemaVersion` where fields are additive and backward
  compatible;
- add `navigationSchemaVersion`, `displayTitle`, `aliases`, `tags`,
  `navigationCategory`, `questionFamilies`, `claim`, or `citations` fields as
  needed;
- bump schema version only if existing field meaning changes.

Existing consumers should still be able to read stable IDs, supporting IDs,
rule IDs, evidence tiers, coverage labels, and limitations.

## Testing Strategy

Focused tests should cover:

- start page generation and deterministic links;
- folder indexes for endpoints, routes, symbols, surfaces, rules, gaps, and
  limitations;
- safe display titles and fallback titles;
- aliases and tags from closed vocabularies, including deterministic ordering
  across reruns;
- graph categories and optional review mode predicates;
- low-tier or needs-review graph edges retaining their original tier and
  classification after graph inclusion;
- full output remaining available by default when review-friendly graph mode is
  enabled, and filtered-only output being labeled partial when explicitly
  requested;
- chunk title/section rendering;
- question-oriented chunk families or `questionFamilies` fields, including
  multiple memberships for cross-cutting views;
- unsupported question families emitting rule-backed gaps rather than being
  silently omitted;
- canonical `docs-export.gap.unsupported-family.v1` and additive
  `docs-export.gap.unsupported-question-family.v1` not both firing for the same
  unsupported input condition;
- question-family metadata preserving existing canonical chunk IDs and fields
  as additive data while supporting multiple memberships;
- generated `README.md` or current entry-note contracts remaining present,
  cross-linked, or explicitly versioned;
- weak-evidence, gap, and limitation question views returning the same
  underlying chunk IDs, evidence tiers, and limitations as canonical chunks;
- claim/citation-first JSONL and Markdown parity;
- hidden/local redaction and public/demo strict rejection;
- no raw sensitive values in public/demo outputs;
- stale generated file and user-file collision behavior;
- byte-stable reruns.

Validation for implementation PRs should include focused exporter tests,
solution build/test, private-path guard, and whitespace diff check.
