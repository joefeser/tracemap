# Site TraceMap Tools LLM Discovery Pack Design

Public claim level: demo
Status: completed
Readiness: implemented

## Overview

The LLM discovery pack is a static site discovery layer for `tracemap.tools`.
It gives bots, crawlers, documentation agents, and LLM-based assistants a small
set of public-safe entry points that point back to TraceMap's evidence pages,
limitations, generated demo summaries, and source-of-truth repository docs.

This design does not add AI analysis to TraceMap. It does not add product LLM
calls, embeddings, vector databases, prompt-based classification, runtime
services, scanner changes, reducer changes, or report generation changes.

## Source Model

The future implementation should derive discovery entries from reviewed site
source metadata and public-safe repository document references.

Recommended source inputs:

- `site/src/_site/pages.json` for public route metadata.
- `site/src/_blog/articles.json` for public blog entries.
- A new checked-in source file such as `site/src/_site/discovery.json` as the
  authoritative source for discovery-only fields that do not exist in
  `pages.json`.
- Existing site pages that already state public claim level, proof paths,
  limitations, or non-claims.

`pages.json` should remain responsible for sitemap route metadata unless a
separate implementation intentionally extends its schema. Discovery-only fields
such as title, summary, public claim level, preferred proof path, limitations,
and non-claims should live in `discovery.json` or an equivalent reviewed source
file so the build does not scrape HTML for claim metadata.

Suggested `discovery.json` entry shape:

```json
{
  "path": "/demo/result/",
  "title": "Public Demo Result",
  "summary": "Public-safe static evidence summary for the checked-in demo.",
  "publicClaimLevel": "demo",
  "sourceType": "site-page",
  "hintCategory": "evidence",
  "preferredProofPath": "/demo/proof-upgrades/",
  "limitations": ["Static evidence only", "No runtime proof"],
  "nonClaims": ["No release approval", "No production traffic proof"]
}
```

Entries should sort by `path` using ordinal comparison before writing generated
JSON. Repository document links should use stable public refs such as `main` or
a release tag, not the feature implementation branch.

Limitations describe what available evidence cannot prove. Non-claims describe
what TraceMap does not do or does not assert.

Generated outputs remain build artifacts under `site/dist` and are not edited
by hand.

`site/src/_site/discovery.json` is a private build-time input only. It must not
be copied directly to `site/dist`. The build should read that source and write
the public outputs `/llms.txt`, `/docs-index.json`, and `/routes-index.json`.
An empty discovery source should still produce all three public outputs with
valid empty JSON arrays where applicable and a valid `llms.txt` containing the
required non-claims section.

## Output Shape

The pack should publish three static outputs:

- `/llms.txt`: concise human-readable route guidance for bots and assistants.
- `/docs-index.json`: source-of-truth repository docs and public-safe reference
  metadata.
- `/routes-index.json`: public route metadata, claim level, summary, preferred
  proof links, and limitations.

Each generated entry should keep fields small, deterministic, and bounded.
Suggested fields include `path`, `title`, `summary`, `publicClaimLevel`,
`sourceType`, `preferredProofPath`, `limitations`, and `nonClaims`.

Required discovery source fields: `path` or `url`, `title`, `summary`,
`sourceType`, `publicClaimLevel`, `limitations`, and `nonClaims`.
`sourceType` must be one of `site-page` or `repo-doc`.
`hintCategory` must be one of `start`, `evidence`, `limitations`, `demo`,
`repo-doc`, `roadmap`, or `use-case`.
`preferredProofPath` is optional, but if present and internal it must resolve to
a built site artifact.
Empty `preferredProofPath` means `null`, an empty string, or a whitespace-only
string. Empty values are invalid when the field is present. Unresolved internal
`preferredProofPath` values must fail the build with an explicit message naming
only the unresolved public path, not local filesystem paths.

`llms.txt` should use a pinned Markdown-like shape:

- H1 title.
- Short blockquote summary.
- H2 sections for "Start Here", "Evidence And Proof", "Limitations", "Demo",
  "Repository Docs", and "Non-Claims".
- Bullet links with stable public URLs and one short bounded summary each.

The H2 sections should appear in the listed order. Sections with no entries
should be omitted, not emitted as empty sections.

## Claim And Safety Rules

- Every public conclusion must route to evidence or limitations.
- Discovery metadata may summarize where to read, not infer what is true.
- `main` evidence may be described as available only when public pages or
  repository docs support it.
- `dev`-only work must be labeled planned, in progress, or future-facing.
- Roadmap and concept pages must not be summarized as shipped proof.
- Discovery text must preserve TraceMap's deterministic static evidence model:
  rule IDs, evidence tiers, coverage labels, limitations, and generated
  artifacts.

The pack must not publish private paths, raw source snippets, raw SQL, config
values, secrets, raw fact streams, SQLite databases, analyzer logs, generated
scan directories, local output roots, or private sample identities.

Validation should include a concrete deny-list or sentinel check for public
discovery outputs. At minimum it should cover local absolute paths such as
`/Users/` and Windows drive paths, raw SQL phrases such as `SELECT *`, obvious
connection string fragments, raw fact stream names, SQLite file names, analyzer
log references, and forbidden positioning phrases such as "AI impact analysis",
"embedding", and "vector database" unless they appear inside an explicit
non-claim.

## Build Integration

Implementation should extend the existing static site build rather than adding
new runtime behavior.

Expected approach:

1. Add source metadata under `site/src`.
2. Extend `site/scripts/build.mjs` or a helper module to write the discovery
   files into `site/dist`.
3. Extend site validation to check required fields, internal route references
   from discovery outputs, public claim levels, non-claims, deterministic sort
   order, and denied public-unsafe tokens.
4. Add focused tests for generation, route references, and forbidden wording or
   artifacts.

Discovery files should be exposed from stable direct URLs. Initial
implementation must not add `/llms.txt`, `/docs-index.json`, or
`/routes-index.json` to generated sitemap output because the current sitemap
validator accepts trailing-slash HTML routes only. Sitemap inclusion can be a
future enhancement only if sitemap validation is explicitly changed for those
exact file paths.

`robots.txt` is currently static source under `site/src`. If the implementation
mentions `/llms.txt` there, it should use a plain comment such as
`# LLM discovery: https://tracemap.tools/llms.txt`, not a non-standard
`Sitemap:` directive for a text file. The robots comment is the preferred
minimum exposure path; direct site links are additive.

## Validation

Spec review should use the repository Kiro review wrapper:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-opus-4.8 --fresh
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model claude-sonnet-4.6 --fresh
```

If a named model is unavailable, use the wrapper's model fallback:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-llm-discovery-pack --kind spec --model auto --fresh
```

Future implementation validation should include:

- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- `git diff --check`
- Manual copy review for static-evidence wording, non-claims, public-safe
  artifact boundaries, and main/dev labeling

Implementation tests should live under `site/scripts/*.test.mjs` so `npm test`
runs them. Test coverage should include generation of all three outputs,
required JSON field schemas, route/proof-path resolution against `site/dist`,
claim-level preservation, denied-token rejection, non-claims presence, main/dev
labeling, and deterministic ordering.

Denied-token exceptions are structural: a normally denied phrase is allowed only
when it appears as a value in a `nonClaims` array in discovery source/generated
JSON or inside the `## Non-Claims` section of `llms.txt`. The same phrase in a
title, summary, limitation, or route hint must fail validation.
For JSON, the exception applies only to direct string values in the `nonClaims`
array, not nested objects, nested arrays, or other fields.

The `llms.txt` validator should split content on H2 headings that start with
`## `. A line is inside the `## Non-Claims` section only after that heading and
before the next H2 heading or end of file.

`preferredProofPath` validation has three states: an absent field is allowed and
skips proof-path resolution; a present empty field is invalid; a present
non-empty internal path must resolve to a built `site/dist` artifact.

Schema validation failures, including missing required fields or invalid
`sourceType` or `hintCategory`, must fail validation and halt the build.

For non-shipped claim levels (`planned`, `concept`, `hidden`, `dev-only`),
generated copy may use "planned", "future", "in progress", "concept",
"hidden", or "dev-only" as appropriate, and must not describe the entry as
"available", "shipped", "released", or "deployed".

Sort order should use a stable ordinal comparison over normalized `path` or
`url` strings. Tests should include a fixture that proves repeated builds emit
the same JSON order.
