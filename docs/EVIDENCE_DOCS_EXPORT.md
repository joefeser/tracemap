# Evidence Docs Export

`tracemap docs-export` generates deterministic evidence documentation from an
existing TraceMap scan or combined index. The output is intended for external
documentation search, knowledge-base, or retrieval systems.

TraceMap does not implement retrieval, embeddings, ranking, answer generation,
prompt classification, vector writes, access controls, or data retention for
those external systems.

## Usage

```bash
tracemap docs-export \
  --index <index-or-combined.sqlite> \
  --out <docs-output> \
  --format markdown,jsonl
```

Optional inputs include `--route-flow-report`, `--property-flow-report`,
`--paths-report`, `--reverse-report`, `--combined-report`,
`--release-review-report`, `--vault-graph`, `--evidence-pack`, and
`--webforms-packet`, and `--source-claim-catalog`.

To add the bounded Web Forms modernization inventory to the same corpus:

```bash
tracemap docs-export \
  --index <index.sqlite> \
  --webforms-packet <webforms-modernization.json> \
  --out <docs-output> \
  --format markdown,jsonl
```

The packet source must uniquely match the supplied index by scan and commit
identity. The `webforms-modernization` family emits separate retrieval units for
the packet overview, surfaces, event chains, downstream boundaries,
identity/state declarations, batch/data-movement declarations, and structural
slice candidates. Packet gaps remain first-class gap chunks. Owner questions
and limitations remain scoped static metadata rather than inferred findings.

This export is deliberately a documentation corpus, not a BRD generator or
modernization prompt. A downstream private workflow may retrieve and interpret
the documents under its own access controls, but TraceMap does not infer
business intent, recommend a target design, or generate Angular, .NET, SQL, or
migration code as part of docs export.

`--format` accepts one comma-separated value from `markdown`, `jsonl`, or
`markdown,jsonl`. `manifest.json` is always the generated-file integrity anchor
for successful exports.

`--families` accepts a comma-separated subset of:

```text
source-overview,endpoint,route-flow,property-flow,dependency-surface,data-surface,package-config,query-sql-shape,legacy,release-review,impact-summary,webforms-modernization,gap,limitation
```

Unselected families are recorded as `not_requested`. Requested families that
the input cannot support emit rule-backed gaps.

## Outputs

Directory output uses schema `tracemap-evidence-docs.v1`:

```text
manifest.json
query-recipes.json
chunks.jsonl
README.md
index.md
QUERY_RECIPES.md
chunks/<family>/index.md
chunks/<family>/<chunk-id>.md
```

Each JSONL line is one chunk object with `schemaVersion`, `chunkId`,
`chunkFamily`, `questionFamilies`, `chunkType`, `claimLevel`, `title`,
`sectionTitle`, `sortKey`, structured `claim`, `bodyMarkdown`, citations,
source refs, supporting IDs, rule IDs, evidence tiers, coverage labels, gaps,
limitations, redactions, and links. `bodyMarkdown` is rendered from the same
structured evidence fields and does not become source evidence by itself.

Chunks also carry additive `retrievalHints`. Each hint names a recipe from
`query-recipes.json`, supplies bounded parameter values already present in the
chunk's citations or packet identity, and identifies the supporting evidence.
Hints do not add a finding, raise an evidence tier, or close a gap.
Web Forms handler hints bind the retained display symbol used by fact and call
tables. Boundary fact lookups are emitted only when the terminal identity is a
retained fact; projection-only path nodes remain cited without an inapplicable
fact lookup.

Gap records retain their own source references, commit identity, safe
repository-relative file path and structured line span when available, plus
extractor identity/version and supporting IDs. Metadata that is unavailable
remains null on the gap rather than being inferred from an unrelated citation.
Web Forms packet identity includes the retained packet contents and requested
surface selection, so distinct bounded views of the same scan remain separate
retrieval inputs instead of colliding.

Each chunk includes deterministic navigation links to its Markdown file, the
family index, and the top-level docs index. Markdown output renders the same
links near the top of each chunk and writes one family index per emitted chunk
family. These navigation pages are generated presentation metadata only; they do
not add evidence, promote claim levels, or prove runtime behavior.

Markdown files start with generated frontmatter containing the schema,
generator, content hash, chunk or summary identity, claim level, and source
labels. The manifest content hash is computed with its own `contentHash` field
blanked.

## Question Families

`questionFamilies` is an additive, deterministic view over canonical
`chunkFamily` records. A chunk can belong to more than one question family when
a primary evidence question and a cross-cutting review view both apply. Current
question-family values are:

- `endpoint-question`
- `data-surface-question`
- `package-question`
- `snapshot-change-question`
- `modernization-evidence-question`
- `weak-evidence-question`
- `gap-question`
- `limitation-question`

Snapshot-change question membership is emitted only for compatible
release-review evidence. Requested canonical CLI families that are unavailable
continue to use `docs-export.gap.unsupported-family.v1`. Additive
question-family views that cannot be supported by an input schema use
`docs-export.gap.unsupported-question-family.v1`.

## Claims And Citations

Each chunk carries a structured `claim` before narrative Markdown. Claim kinds
are deterministic labels such as `static-evidence`, `weak-static-evidence`,
`gap-statement`, and `limitation-statement`. Claims include claim level, rule
IDs, evidence tiers, coverage labels, supporting IDs, and limitation
references. Lower-tier, reduced-coverage, gap, or review-only evidence remains
labeled for review and is not promoted by docs-export.

Legacy data model descriptors are packaged as `data-surface` chunks when
terminal static descriptor facts are present. The chunk cites both the source
`legacy.data.*` rule and the projection rule
`legacy.data.model.surface.v1`, renders hash-only descriptor labels unless a
future reviewed claim context explicitly allows clear labels, and does not
claim SQL execution, runtime provider selection, live schema existence,
migration execution, or production database usage.

Property-flow chunks may include safe `terminalContextKind` metadata when that
key is already present on indexed static evidence or in a supplied
`property-flow-report.json` under
`lineagePaths[].nodes[].safeMetadata.terminalContextKind`. Docs export treats
this as retrieval metadata inside the existing `property-flow` chunk family. It
does not parse path-note prose, create a new finding, infer missing terminal
context, or prove runtime behavior, database execution, dependency execution,
impact, or complete coverage.

## Stable IDs

Docs-export IDs use context-separated SHA-256 inputs with length-prefixed
fields:

```text
<field-name-length>:<field-name>=<value-length>:<value>
```

Displayed IDs are truncated to 24 lowercase hex characters. If distinct full
identity records collide, docs export emits
`docs-export.gap.duplicate-stable-identity.v1` rather than choosing a winner.

## Evidence Query Recipes

`query-recipes.json` is a machine-readable closed catalog, and
`QUERY_RECIPES.md` is its human-readable rendering when Markdown output is
enabled. The initial catalog covers exact facts, overlapping file spans, exact
symbols, handler call edges, reverse callers, Web Forms surface facts,
database-shaped handler evidence, boundary target evidence, and evidence near a
gap span. A separate stored-procedure candidate-context recipe retrieves
command construction, command-type, invocation, and argument facts without
claiming that co-occurrence proves object identity or execution.

Each recipe declares required parameters, supported single/combined index
kinds, a shared result contract, evidence requirements, and limitations. SQL
variants are single-statement, parameterized, read-only, bounded by `$limit`,
and restricted to documented TraceMap-owned evidence tables. Catalog validation
rejects mutation or DDL tokens, multiple statements, missing parameters,
unbounded recipes, and unapproved tables.

Parameters declare the input kinds for which they are required. Every
combined-index variant requires `source_index_id` and applies it as a predicate,
so a file path, surface identity, or method symbol shared by multiple sources
cannot consume another source's result budget. Chunk retrieval hints record the
matching `inputKind` and include the owning combined-source ID when applicable.

This internal retrieval SQL is not application SQL. It does not query an
application database, expose captured SQL text, or prove runtime execution. A
downstream system can use the recipes to request more cited TraceMap evidence,
but remains responsible for access controls, orchestration, interpretation, and
any private planning or conversion workflow.

## Claim Levels

Raw indexes and reports default to `hidden`.

`--minimum-claim-level demo-safe` or `public-safe` filters chunks to reviewed
evidence at or above that level. Promotion requires stable source identity
proof from a compatible source-claim catalog, vault metadata, or evidence-pack
metadata. Display names alone do not promote evidence.

Demo/public output requires `--date YYYY-MM`. Hidden output without `--date`
uses the fixed `local-only` sentinel so bytes stay stable across reruns.

## Redaction

Generated Markdown, JSONL, manifest strings, frontmatter, file names, links,
tags, diagnostics, and logs are checked for unsafe content. The exporter rejects
or omits local absolute paths, home fragments, raw remotes, raw SQL, raw config
values, connection strings, raw URLs, endpoint addresses, credentials, tokens,
secret-like strings, source snippets, analyzer diagnostics, stack traces,
private identifiers, production data, and unsafe Markdown.

Diagnostics use category and output-relative location only. They do not echo
the unsafe value.

### Hidden/Local Examples

Hidden/local docs-export output may stay useful for local ingestion, but it
does not relax hard-fail safety categories and it does not make an output
public/demo safe.

| Category | Hidden/local outcome | Public/demo outcome |
| --- | --- | --- |
| Safe source label or repo-relative span such as `src/Api/Controller.cs:10-12` | Render as citation metadata when provenance is stable. | Render only after claim-level review permits the source. |
| Secret-like safe-context display component | Use a hash, category label, or omission record when supported by the exporter. | Reject or filter under strict validation. |
| Unsupported or missing citation provenance | Emit a rule-backed gap such as `docs-export.gap.missing-provenance.v1`. | Emit the same gap if the remaining output is claim-level safe. |
| Raw SQL, config value, credential, token, raw URL, raw remote, local absolute path, source snippet, or analyzer log | Hard fail or omit only when the value is not required for evidence identity; diagnostics stay sanitized. | Hard fail or omit under the same sanitized safety gate. |

## Collision Behavior

Generated files are replaced only after the new output passes safety checks.
Stale, hand-edited, malformed, or hash-invalid generated files fail with
`docs-export.validation.generated-file-stale.v1` unless `--force` is supplied.

`--force` does not bypass claim-level gates, redaction, schema checks, unsafe
value checks, non-generated user-file collisions, or private-path gates.
Non-generated files fail with `docs-export.validation.user-file-collision.v1`.

`--dry-run` validates inputs, projects chunks, applies filtering, reports
planned files and counts, and writes no files.

## Limitations

Docs export packages existing deterministic evidence. It does not scan source,
reduce impact, execute code, approve releases, prove vulnerabilities, prove
ownership, prove deployment, prove service reachability, prove production
traffic, or prove business impact.

Every emitted docs-export claim, gap, limitation, and validation finding is tied
to a documented `docs-export.*.v1` rule ID in `rules/rule-catalog.yml`.
