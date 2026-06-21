# Implementation State

Status: implemented
Readiness: implemented
Branch: codex/site-next-phase-20260616
Public claim level: demo

## Summary

Implemented the static TraceMap discovery pack for `tracemap.tools`.

The site build now publishes:

- `/llms.txt`
- `/docs-index.json`
- `/routes-index.json`

The authoritative discovery source is checked in at
`site/src/_site/discovery.json`. It is a private build-time input and is not
copied to `site/dist`.

## Scope

Changed only site source/scripts/tests and the matching Kiro spec state/tasks:

- `site/src/_site/discovery.json`
- `site/src/robots.txt`
- `site/scripts/build.mjs`
- `site/scripts/discovery.mjs`
- `site/scripts/*.test.mjs`
- `site/scripts/validate.mjs`
- `.kiro/specs/site-tracemap-tools-llm-discovery-pack/tasks.md`
- `.kiro/specs/site-tracemap-tools-llm-discovery-pack/implementation-state.md`

No scanner, reducer, language adapter, report generation, or core product code
was edited.

## Scope Decisions

- Kept discovery static and build-time only.
- Kept repository documents distinct from site presentation routes through
  `sourceType: "repo-doc"` and `sourceType: "site-page"`.
- Used `hintCategory` to keep evidence and limitations ahead of roadmap and
  use-case hints in generated `llms.txt`.
- Sorted generated JSON entries deterministically by public path or URL using
  ordinal comparison.
- Pinned repository document URLs to stable `main` refs.
- Put detailed entry-level limitations and non-claims in the JSON indexes.
- Kept `llms.txt` concise with a short global non-claims section.
- Exposed `/llms.txt` through a plain `robots.txt` comment.
- Did not add `.txt` or `.json` discovery files to the sitemap.

## Main/Dev Wording Boundary

- `main` repository docs are labeled as source-of-truth docs only when linked to
  stable `main` URLs.
- Demo pages keep `Public claim level: demo`.
- Concept and roadmap routes use concept/future-facing wording and do not use
  shipped, released, deployed, or available positioning.
- Discovery metadata remains bounded to deterministic static evidence, rule
  IDs, evidence tiers, coverage labels, limitations, and generated artifacts.

## Validation

Passed on this branch:

```bash
git diff --check
cd site && npm test
cd site && npm run validate
cd site && npm run build
./scripts/check-private-paths.sh
```

Validation now checks:

- Required generated discovery files exist.
- `site/dist/discovery.json` does not exist.
- JSON index schema fields, `sourceType`, `hintCategory`, claim levels, stable
  repo-doc refs, deterministic ordering, and proof-path resolution.
- Empty discovery input still writes all public outputs.
- `preferredProofPath` absent/present-empty/present-valid/present-missing
  behavior.
- `llms.txt` H2 ordering and Non-Claims parsing.
- Denied-token exceptions only inside direct `nonClaims` strings or the
  `## Non-Claims` section of `llms.txt`.
- Evidence and limitation hints precede roadmap/use-case/demo hints.
- Discovery files are not listed in `sitemap.xml`.
- `robots.txt` includes the plain `/llms.txt` comment.

## Review Findings

- Manual generated-output inspection confirmed:
  - `llms.txt` contains the shared site principle and the required H2 order.
  - `docs-index.json` contains five stable `main` repository document refs.
  - `routes-index.json` contains fourteen public site routes.
  - `site/dist/discovery.json` is absent.
  - `robots.txt` exposes `/llms.txt` as a comment.
  - Sitemap output excludes the discovery `.txt` and `.json` files.

## Follow-Ups

- Future sitemap inclusion for discovery `.txt` or `.json` files should be a
  separate spec because current sitemap validation is intentionally route-only.
- Future public pages may add visible links to `/llms.txt`; the current baseline
  exposure is the robots comment required by this slice.
