# Site TraceMap Tools Evidence Glossary Design

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Design Summary

Keep the future glossary as a conventional public reference page, not a
marketing page and not a raw artifact browser. The page should prioritize
scannable definitions, conservative claim boundaries, and durable anchors that
humans and bots can cite without upgrading TraceMap's public claims.

Design is included as a short file instead of folding it fully into
requirements because this phase has route, metadata, and validation choices
that future implementers should be able to review separately.

## Recommended Information Architecture

Non-binding preferred route for implementation evaluation: `/glossary/`.

Alternative route to evaluate: `/docs/evidence-glossary/`.

Possible folded placement to evaluate: an anchored section on an existing
documentation, proof-path, or limitations route if the site already has a
canonical vocabulary surface at implementation time.

The page should use these sections:

- Header with `Public claim level: concept` and
  `No public conclusion without evidence`.
- Short audience statement for engineers, reviewers, managers, architects, and
  agents.
- Glossary entries grouped under deterministic evidence, claim boundaries, and
  local-only artifacts.
- Non-claims section with stable anchor `#non-claims` that rejects runtime,
  operational, complete-coverage, and AI/LLM impact-analysis positioning.
- Related public-safe routes.

Before writing final page copy, implementation should reconcile overlapping
definitions with `/evidence/`, `/proof-paths/`, and `/proof-source-catalog/`.
The glossary should summarize and link to canonical public-safe surfaces rather
than creating competing definitions for evidence tiers, proof paths, coverage
labels, or rule families.

## Glossary Entry Shape

Each term entry should use a stable anchor and a compact structure:

- Term name.
- Plain-language definition.
- Public use: what the term can support when evidence is available.
- Limitation: what the term does not prove.
- Optional related routes or supporting IDs when public-safe.

Stable anchors should be predictable and present for every required term, for
example `#rule-id`, `#evidence-tier`, `#commit-source-context`,
`#extractor-version`, and `#local-only-artifact-family`.

## Public Copy Tone

The page should read like a reference surface for cautious readers. It should
avoid hype, capability inflation, and implementation promises. Words such as
`may`, `can`, `when available`, `public-safe`, `bounded`, `partial`, and
`gap-labeled` are appropriate when they prevent overclaiming.

## Validation Shape

Future validation should be implemented with the existing site validator
patterns. It should inspect rendered text, decoded HTML, and raw HTML
attributes for required phrases, required term anchors, required public-safe
links, metadata, forbidden private material, forbidden overclaims, and bounded
word count. Required phrases include at minimum
`Public claim level: concept` and
`No public conclusion without evidence`.

The AI/LLM guard should follow existing affirmative-positioning or
sanctioned-section patterns so required non-claims do not fail their own
validator.

If a standalone route is selected, validation should also cover sitemap and
discovery metadata, including the existing `publicClaimLevel`,
`sourceType`, `hintCategory`, `preferredProofPath`, `limitations`, and
`nonClaims` shape. If a folded placement is selected, validation should cover
the containing page, section anchors, and the documented reason standalone
route metadata was not added.
