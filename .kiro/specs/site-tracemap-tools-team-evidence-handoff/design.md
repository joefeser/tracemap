# Site TraceMap Tools Team Evidence Handoff Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This implementation defines a concept-level public site page at
`/team-evidence-handoff/`. The page teaches a bounded communication pattern for
sharing TraceMap evidence with a teammate, reviewer, manager, or agent while
keeping proof boundaries intact.

The design center is a receiver-specific handoff, not a new artifact catalog.
Every handoff keeps the same proof-bearing fields together:

- `summary`
- `proof path`
- `rule ID/rule family`
- `evidence tier`
- `coverage label`
- `limitations`
- `non-claims`
- `local-only artifacts`
- `next owner/action`

## Information Architecture

The page should be a public concept route with a concise structure:

1. Claim-level header with `Public claim level: concept` and the shared
   principle.
2. Short explanation of why handoff language matters.
3. Required handoff fields, presented as a compact checklist or table.
4. Receiver-specific patterns for teammate, reviewer, manager, and agent.
5. Boundary section for limitations, non-claims, and local-only artifacts.
6. Related public-safe links to proof paths, packets, manager packet,
   review-room, manager FAQ, proof-source catalog, limitations, and validation.

The page should not add a primary navigation item unless comparable concept
pages already use that pattern at implementation time. Discovery should happen
through metadata, sitemap/page index registration, and bounded cross-links.

## Route and Metadata

Preferred route: `/team-evidence-handoff/`

Metadata should describe the page as a concept guide for sharing deterministic
static evidence with another receiver. Titles and descriptions should avoid
runtime, production, endpoint-performance, outage-cause, release-safety,
operational-safety, AI/LLM, and complete-coverage claims.

Discovery metadata should use:

- `publicClaimLevel: concept`
- `hintCategory`: a communication, workflow, or use-case category consistent
  with the existing discovery schema
- `sourceType`: site page or the closest existing value
- `preferredProofPath`: `/proof-paths/`
- `limitations`: static-evidence and public-safety boundaries
- `nonClaims`: runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, AI/LLM, and complete-coverage non-claims

## Content Model

The required field list is the contract for the page and validator. The
page should include the exact deterministic sentence:

`A handoff is complete only when the summary, proof path, rule ID/rule family,
evidence tier, coverage label, limitations, non-claims, local-only artifacts,
and next owner/action travel together.`

The page may use short synthetic examples, such as:

- Teammate: "Here is the bounded summary, the proof path, and the follow-up
  code owner question."
- Reviewer: "Here is the rule family, tier, coverage label, limitation, and
  the review gap to check."
- Manager: "Here is what can be repeated to stakeholders without
  overclaiming, what remains a non-claim, and who owns the next action."
- Agent: "Preserve the proof path, do not publish local-only artifacts, and
  keep non-claims attached."

Examples must stay synthetic or public-demo-safe. They must not contain raw
source, raw SQL, configuration values, secrets, local paths, private remotes,
generated scan directories, private sample names, or raw scanner output.

## Boundary Design

The page should make the proof boundary visible in the copy rather than
burying it in a disclaimer. Recommended copy patterns:

- "The summary is only repeatable while attached to the proof path and
  limitations."
- "Partial evidence stays useful when the coverage label travels with it."
- "Local-only artifacts are working material; publish a public-safe summary,
  not raw scanner output."
- "The next owner/action is part of the handoff because TraceMap does not
  replace ownership, tests, telemetry, release review, or code review."

The page should not present local-only artifact names as downloadable public
assets. If artifact names are mentioned, they should be framed as private
working material or local generated outputs, never as raw public content.

## Differentiation From Neighboring Pages

The implementation should include a short "Use this when..." style
differentiation block:

- Use `/packets/` when explaining packet artifact families.
- Use `/manager-packet/` when preparing manager-facing summaries.
- Use `/review-room/` when structuring a shared review meeting.
- Use `/manager-faq/` when answering stakeholder questions.
- Use `/proof-source-catalog/` when explaining proof-source families.
- Use `/team-evidence-handoff/` when one receiver needs a bounded packet
  summary and next action from another.

This block exists to prevent route overlap. It must not frame any neighboring
page as proving runtime behavior, production behavior, release safety,
operational safety, AI/LLM analysis, or complete coverage.

## Validation Design

The implementation should add focused validation comparable to
neighboring concept-page validators. Validation should check:

- rendered route exists
- `Public claim level: concept`
- shared principle
- all required handoff fields
- exact deterministic sentence
- required public-safe links
- metadata and discovery claim level
- resolved internal links
- rendered word count between 400 and 1500 words
- forbidden AI/LLM positioning
- unsupported overclaim wording except sanctioned non-claim framing
- forbidden private/raw material
- forbidden-copy checks run against text with the sanctioned non-claims and
  boundary region removed, using word-boundary matching and a `public-safe`
  exemption consistent with neighboring concept-page validators

Site-level validation should include:

- `git diff --check`
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- `./scripts/check-private-paths.sh`
- desktop and mobile browser sanity checks for layout or interaction changes

## Accessibility and Layout

The future page should reuse existing static site components and semantic HTML
patterns. Headings should be hierarchical, link text should be descriptive, and
any checklist or table should remain readable on mobile.

No interactive form, client-side state, runtime service, raw artifact viewer,
agent integration, or upload flow is part of this spec.
