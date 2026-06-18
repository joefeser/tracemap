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
`chunkFamily`, `chunkType`, `claimLevel`, `sortKey`, `bodyMarkdown`,
citations, source refs, supporting IDs, rule IDs, evidence tiers, coverage
labels, gaps, limitations, redactions, and links.

Markdown files start with generated frontmatter containing the schema,
generator, content hash, chunk or summary identity, claim level, and source
labels. The manifest content hash is computed with its own `contentHash` field
blanked.

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
