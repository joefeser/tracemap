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

Optional inputs include `--route-flow-report`, `--paths-report`,
`--reverse-report`, `--combined-report`, `--release-review-report`,
`--vault-graph`, `--evidence-pack`, and `--source-claim-catalog`.

`--format` accepts one comma-separated value from `markdown`, `jsonl`, or
`markdown,jsonl`. `manifest.json` is always the generated-file integrity anchor
for successful exports.

`--families` accepts a comma-separated subset of:

```text
source-overview,endpoint,route-flow,property-flow,dependency-surface,data-surface,package-config,query-sql-shape,legacy,release-review,impact-summary,gap,limitation
```

Unselected families are recorded as `not_requested`. Requested families that
the input cannot support emit rule-backed gaps.

## Outputs

Directory output uses schema `tracemap-evidence-docs.v1`:

```text
manifest.json
chunks.jsonl
README.md
index.md
chunks/<family>/<chunk-id>.md
```

Each JSONL line is one chunk object with `schemaVersion`, `chunkId`,
`chunkFamily`, `questionFamilies`, `chunkType`, `claimLevel`, `title`,
`sectionTitle`, `sortKey`, structured `claim`, `bodyMarkdown`, citations,
source refs, supporting IDs, rule IDs, evidence tiers, coverage labels, gaps,
limitations, redactions, and links. `bodyMarkdown` is rendered from the same
structured evidence fields and does not become source evidence by itself.

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

## Stable IDs

Docs-export IDs use context-separated SHA-256 inputs with length-prefixed
fields:

```text
<field-name-length>:<field-name>=<value-length>:<value>
```

Displayed IDs are truncated to 24 lowercase hex characters. If distinct full
identity records collide, docs export emits
`docs-export.gap.duplicate-stable-identity.v1` rather than choosing a winner.

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
