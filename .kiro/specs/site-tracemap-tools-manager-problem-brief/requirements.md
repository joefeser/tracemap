# Site TraceMap Tools Manager Problem Brief Requirements

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Summary

Add a public-safe manager/problem brief page on `tracemap.tools`. The page
explains the recurring
coordination problem TraceMap is meant to reduce: teams often need to answer
manual dependency questions during review pressure, but static evidence can be
scattered, partial, and hard to inspect consistently.

This phase implements a concept-level public page that helps managers and
technical leads understand why deterministic evidence
packets can reduce manual dependency-indexing and review burden while keeping
uncertainty, gaps, and public claim boundaries visible.

## Shared Site Principle

No public conclusion without evidence.

## Requirements

### Requirement 1: Publish a manager/problem brief route or article

The site shall publish a concept-level route or article for managers and leads
that explains the problem TraceMap addresses without overstating what the tool
can prove.

Acceptance criteria:

- The page says `Public claim level: concept`.
- The page states the shared site principle.
- The page answers: what problem does TraceMap solve?
- The page answers: why do deterministic evidence packets reduce manual
  dependency-indexing and review burden?
- The page answers: how can teams inspect change risk without treating static
  evidence as certainty?
- The page uses professional origin-story framing around recurring coordination
  friction, manual dependency questions, and cross-team review pressure.
- The page does not blame consultants, vendors, coworkers, teams, or specific
  organizations.
- The page does not disparage named tools, products, vendor categories, or
  existing CI/CD practices; positioning stays problem-first, not
  competitor-first.
- The page fits an existing public site long-form pattern, such as a page hero,
  short manager-facing sections, evidence packet examples, proof path links,
  and a visible boundaries section.
- The page should stay manager-readable, roughly 500 to 1000 words unless the
  existing site pattern requires a different length, and shall not exceed 1500
  words without a spec amendment.
- The page follows the existing site accessibility baseline for heading
  hierarchy, color contrast, and alt text for non-text content.
- Alt text and image metadata must follow the same public claim boundaries as
  visible page copy.

### Requirement 2: Keep claims concept-level and evidence-bounded

The page shall describe TraceMap's deterministic static evidence model without
claiming stronger product coverage, runtime certainty, or release safety.

Acceptance criteria:

- The page explains that TraceMap produces deterministic static evidence packets
  with rule IDs, evidence tiers, coverage labels, limitations, and generated
  artifacts.
- The page explains that evidence packets can help reviewers find and discuss
  relevant dependency surfaces faster than rebuilding a dependency index by
  hand.
- The page describes change-risk inspection as an aid to review, not a final
  decision, approval, or guarantee.
- The page does not claim runtime behavior, production traffic, endpoint
  performance, outage cause, release safety, operational safety, AI impact
  analysis, LLM analysis, or complete product coverage.
- The page avoids saying TraceMap proves impact unless a linked demo or proof
  path provides evidence with rule IDs, evidence tiers, and limitations.

### Requirement 3: Use public-safe generated summaries and demo evidence

The page shall use public-safe generated summaries and demo evidence rather
than publishing private or raw scanner artifacts.

Acceptance criteria:

- Future implementation uses public-safe generated summaries, public demo
  evidence, or manually written concept copy grounded in existing public proof
  paths.
- Future implementation does not publish raw `facts.ndjson`, `index.sqlite`,
  analyzer logs, raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, or private
  sample identities.
- Example evidence packets use sanitized labels, relative public paths, hashes,
  counts, rule IDs, evidence tiers, and coverage labels where applicable.
- The page makes clear that public summaries are a presentation layer over
  deterministic evidence, not a substitute for facts, reports, limitations, or
  coverage labels.

### Requirement 4: Link to proof paths and boundaries

The page shall connect the manager-friendly explanation to existing public proof
and limitation surfaces.

Acceptance criteria:

- The page links to `/proof-paths/`.
- The page links to `/validation/`.
- The page links to `/limitations/`.
- The page links to `/demo/` or the most relevant public demo route.
- The page links to `/docs/` when it references generated artifacts or evidence
  packet terminology.
- Links use public routes and do not expose local filesystem paths or private
  remotes.
- If one of these public routes is renamed or unavailable at implementation
  time, the implementation shall either use the current equivalent public route
  or block the page until a safe public target exists.

### Requirement 5: Add discovery metadata and public navigation

The route or article shall be discoverable without making it look like a
production proof page.

Acceptance criteria:

- The route appears in the site's page metadata with claim level `concept`.
- The route appears in generated sitemap output if the existing site metadata
  model includes sitemap generation for comparable public pages.
- The route appears in discovery metadata such as `routes-index.json` or
  `docs-index.json` if those indexes include comparable public pages.
- Existing relevant public pages may link to the brief using manager-facing
  anchor text, but the link copy must not imply release approval, operational
  safety, production coverage, or runtime proof.
- Page title, description, and social/discovery metadata should describe a
  manager/problem brief, not an impact-analysis claim.
- Social metadata should keep titles at 70 characters or less and descriptions
  at 160 characters or less unless the existing site metadata pattern requires
  a different limit.

### Requirement 6: Validate the implementation phase

The future implementation shall run the normal site validation for a public page
change and record the result in this spec's implementation state.

Acceptance criteria:

- Run `git diff --check`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- For layout or interaction changes, run a desktop and mobile browser sanity
  check.
- Validate rendered copy does not contain forbidden AI/LLM positioning such as
  `AI impact analysis`, `LLM analysis`, `machine learning impact analysis`, or
  `artificial intelligence impact analysis`.
- A suitable starting forbidden-positioning pattern is
  `/\\b(AI[- ]?powered|AI impact analysis|LLM[- ]?powered|LLM analysis|machine learning impact analysis|artificial intelligence impact analysis|intelligent analysis|smart impact)\\b/i`.
- The implementation may extend this pattern but must not reduce it without a
  spec amendment.
- Confirm rendered page word count is between 400 and 1500 words.
- Record validation commands, results, scope decisions, and follow-up items in
  `implementation-state.md`.
