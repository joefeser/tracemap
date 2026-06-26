# Design

## Route

`/legacy-modernization/review-handoff/` is a concept-level static page under `site/src/legacy-modernization/review-handoff/index.html`.

The page uses existing site patterns:

- Standard static HTML shell with the existing header and footer.
- `page-hero` for route orientation.
- `claim-ledger-wrap` and `claim-ledger-table` for a horizontally scrollable matrix.
- `split-section`, `detail-list`, `boundary-section`, and `link-grid` for explanatory sections and related links.

## Content Shape

The page has five public sections:

1. Hero with claim level, evidence boundary, and proof-oriented links.
2. Handoff boundary explaining that this route moves from static evidence to owner questions.
3. Handoff matrix with required columns and seven required question rows.
4. Boundary comparison distinguishing TraceMap static evidence from decisions, telemetry, tooling, ownership, and release approval.
5. Non-claims, stop conditions, and adjacent public-safe links.

## Metadata

`site/src/_site/pages.json` includes the route so the generated sitemap contains it.

`site/src/_site/discovery.json` includes:

- `publicClaimLevel: concept`
- `sourceType: site-page`
- `hintCategory: use-case`
- `preferredProofPath: /legacy-modernization/evidence-map/`
- limitations and non-claims that keep the page bounded to deterministic static evidence and owner follow-up

## Validation

`site/scripts/legacy-modernization-review-handoff.mjs` validates generated dist output and exported helpers are tested directly by `site/scripts/legacy-modernization-review-handoff.test.mjs`.

The validator checks:

- Required route file, sitemap route, and discovery metadata.
- Required visible phrases.
- Matrix marker, headers, and required rows.
- Adjacent links that exist in the site.
- Word count bounds.
- No primary navigation addition for the route.
- Forbidden runtime, modernization, approval, database, AI/LLM, and complete-coverage claims.
- Forbidden private or raw material.

Negative tests mutate fixtures to prove the guard catches missing copy, metadata regressions, forbidden claims, private material, and missing matrix content.
