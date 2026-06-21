# Site TraceMap Tools Evidence Glossary Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-evidence-glossary`
Implementation branch: `codex/impl-site-evidence-glossary`
Base: `origin/dev`
Target PR base: `dev`

## Scope

This implementation creates the public-safe evidence glossary/reference page as
a standalone concept-level site route.

## Claim Boundary

The future page is concept-level vocabulary guidance. It must not claim runtime
behavior, production traffic, endpoint performance, outage cause, release
safety, operational safety, AI or LLM impact analysis, or complete product
coverage. It must not publish raw facts, raw SQLite indexes, analyzer logs, raw
source snippets, raw SQL, config values, secrets, local absolute paths, raw
remotes, generated scan directories, private sample names, or hidden validation
details.

## Route Decision Status

Selected placement: `/glossary/`.

Rejected alternatives:

- `/docs/evidence-glossary/`: rejected because the site already uses short
  first-level public proof and guidance routes for concept references, and a
  nested docs route would make the glossary feel like repository
  documentation instead of public vocabulary guidance.
- Folded placement on `/evidence/`, `/proof-paths/`, `/proof-source-catalog/`,
  or `/limitations/`: rejected because those routes already have demo-level or
  boundary-specific jobs. Folding the glossary into one of them would blur the
  concept-level claim signal and create a competing vocabulary surface without
  standalone discovery metadata.

Route rationale: `/glossary/` is short, human-readable, public-safe, and can be
listed in sitemap and discovery metadata with `publicClaimLevel: concept`.

Canonical-source decisions for overlapping terms:

- `rule ID`: canonical public vocabulary remains `/evidence/`; glossary gives a
  concept-level definition and links back.
- `evidence tier`: canonical tier list remains `/evidence/`; glossary repeats
  the four tier names conservatively.
- `proof path`: canonical public route mapping remains `/proof-paths/`, with
  source-family cross-checks in `/proof-source-catalog/`.
- `coverage label`: canonical examples remain `/proof-paths/` and public demo
  result surfaces; glossary defines the boundary only.
- `limitation` and `analysis gap`: canonical boundary wording remains
  `/limitations/` and `/evidence/`; glossary treats each as part of the claim.
- `commit/source context`, `extractor version`, `supporting IDs`,
  `public claim level`, and `local-only artifact family`: glossary is the
  concept-level vocabulary surface, while generated artifacts, rule catalog
  entries, route metadata, and documented limitations remain source material.

Discovery metadata decision: `concept` is accepted by the discovery validator,
and `hintCategory: evidence` is selected because the route defines evidence
vocabulary rather than a use-case, roadmap, demo, or limitations-only surface.

Validation plan: enforce `/glossary/` in sitemap and discovery outputs; require
links to `/evidence/`, `/proof-paths/`, `/proof-source-catalog/`, and
`/limitations/`; verify required terms, stable anchors, non-claims, metadata,
and word count. Standalone route word-count bounds: 900 to 2200 rendered words.

Accepted `hintCategory` values from `site/scripts/discovery.mjs` at spec-review
time: `start`, `evidence`, `limitations`, `demo`, `repo-doc`, `roadmap`, and
`use-case`. Future implementation must choose from this list or update the
discovery validator with rationale.

## Review Status

- `claude-opus-4.8` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-glossary --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T202620-702Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` initial Kiro review command:
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-glossary --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  exited 1 with reduced coverage because Kiro reported denied tool access.
  Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T202620-783Z-spec-claude-sonnet-4.6.clean.md`.
- Initial Medium findings patched: live route grounding, standalone/folded word
  count bounds, affirmative AI/LLM validation, discovery metadata shape,
  canonical vocabulary reconciliation, stable non-claims anchors, hint category
  selection, and validator anchors for word-count tests.
- `claude-opus-4.8` re-review command completed with full coverage. Clean
  artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203052-334Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` re-review command exited 1 with reduced coverage because
  Kiro reported denied tool access. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203052-284Z-spec-claude-sonnet-4.6.clean.md`.
- Re-review Medium findings patched: named word-count validator assertion,
  existing `hintCategory` vocabulary source, stable `#non-claims` anchor,
  specific affirmative AI primitive phrases, minimum required link set,
  private/raw-material sanctioned-section guard, and explicit folded-section
  numeric bounds.
- Final re-review command with `claude-opus-4.8` exited 1 with reduced coverage.
  Kiro reported denied tool access and then hit tool-approval/rate-limit
  handling while trying to inspect discovery metadata. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203546-115Z-spec-claude-opus-4.8.clean.md`.
- Final re-review command with `claude-sonnet-4.6` exited 1 with reduced
  coverage because Kiro reported denied tool access. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-glossary/2026-06-20T203546-180Z-spec-claude-sonnet-4.6.clean.md`.
- Final Medium findings patched after the last Sonnet review: folded-section
  word-count floor/ceiling and required literal private/raw token list.
  Last Sonnet review stated re-review was not required for those changes unless
  the patches introduced new ambiguity.
- Current Medium or higher findings: none known after patches. Review coverage
  remains partially reduced where noted above because Kiro denied tool access in
  some review runs.
- PR-loop actionable review findings patched after PR creation: stale
  `hintCategory` allowlist replaced with the validator enum, non-existent
  aggregate `/use-cases/` route replaced with specific use-case routes, and
  `concept` page-level metadata scoped to standalone glossary routes.
- PR #242 review loop returned one actionable Qodo/Codex-thread finding for
  order-sensitive metadata regexes in the glossary validator. Patched by using
  parsed HTML attributes for canonical, Open Graph title, and page-level claim
  metadata checks, with a focused regression test for reversed attribute order.
- PR #242 review loop then surfaced a Qodo top-level finding that required
  actual `href` and `id` attributes rather than `data-href` or `data-id`
  lookalikes for required links, anchors, and sanctioned-section stripping.
  Patched the glossary helper regexes to require whitespace-delimited
  attributes and added regression tests for `data-href`, `data-id`, and
  sanctioned-section lookalikes.
- PR #242 review loop then surfaced a fresh Codex thread requiring hard
  private-value checks across the whole page instead of stripping sanctioned
  sections first. Patched the validator to scan local paths, remotes,
  connection strings, and credential-like tokens against the full decoded page
  while keeping raw-artifact family vocabulary scoped to sanctioned boundary
  sections.

## Implementation Summary

- Added standalone route `/glossary/` with visible
  `Public claim level: concept` and
  `No public conclusion without evidence`.
- Added required glossary terms with stable anchors, definitions, public use,
  and limitations.
- Added concept-level sitemap and discovery metadata for `/glossary/`.
- Added focused glossary validation for required copy, terms, anchors, links,
  metadata, sitemap/discovery coverage, word count, forbidden affirmative
  positioning, and sanctioned private/raw-material boundary wording.
- Added focused positive and negative glossary validator tests.
- Added minimal inbound links from `/evidence/`, `/proof-paths/`, and
  `/proof-source-catalog/`.

## Validation

Implementation validation passed on 2026-06-20:

- `npm test` from `site/`: passed, 249 tests after the metadata-order,
  attribute-boundary, and hard-private review fixes.
- `npm run validate` from `site/`: passed, generated 48 HTML files, checked
  1543 internal references, 47 sitemap URLs, 1 legacy story safety target, and
  13 legacy modernization evidence-map rows.
- `npm run build` from `site/`: passed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Browser sanity for `/glossary/`: passed on desktop 1440x1000 and mobile
  390x844. The route rendered the expected title and H1, visible concept claim
  level, non-claims section, required proof links, and no horizontal overflow.

Minimum required link validation result: `/evidence/`, `/proof-paths/`,
`/proof-source-catalog/`, and `/limitations/` exist in generated output and
are enforced by the glossary validator. Additional linked routes present at
implementation time are `/validation/`, `/roadmap/`, `/capabilities/`,
`/manager-brief/`, `/review-claim-checklist/`, and `/docs/`.

## Follow-Ups

- None.
