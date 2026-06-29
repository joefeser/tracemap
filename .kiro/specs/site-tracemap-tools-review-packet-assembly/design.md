# Site TraceMap Tools Review Packet Assembly Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design defines a future public-site concept surface for a human review
packet assembly checklist. The page or section teaches a person how to assemble
public-safe review handoff material from existing TraceMap evidence surfaces
without claiming that TraceMap generates packet-builder output, proves runtime
behavior, or conducts autonomous review.

The design center is a packet assembly checklist: a person chooses the review
question, gathers public-safe evidence, attaches limitations, names next
owners, runs the claim checklist, checks stop conditions, and writes bounded
handoff notes.

## Information Architecture

Preferred placement remains undecided until implementation. Candidate
placements are:

- `/packets/assembly/`
- `/review-packet/`
- a section on `/packets/`
- a section on `/review-room/`

The implementation should choose the placement that creates the least overlap
with adjacent pages. A standalone route is appropriate if the workflow needs
metadata, sitemap inclusion, direct linking, and route-level validation. A
section is appropriate if the workflow reads as a companion to an existing
packet or review-room page and the existing page can support the added word
count and validation without losing focus.

The future surface should use this structure:

1. Claim-level header with `Public claim level: concept`.
2. Shared principle: `No public conclusion without evidence`.
3. Short explanation that the surface is a human assembly workflow for
   existing evidence, not a generated feature.
4. Required packet ingredients.
5. Assembly workflow sections.
6. Stop conditions.
7. Handoff notes template.
8. Relationship to neighboring pages.
9. Boundary and non-claim section.

## Content Model

The required packet ingredient list is the contract for future copy and
validation:

- claim being reviewed
- audience
- proof path
- public claim level
- rule ID or rule family
- evidence tier
- coverage label
- commit SHA
- extractor version
- public-safe file path and line span
- limitations
- non-claims
- next owner
- validation evidence
- unresolved gaps

The workflow sections are also validation targets:

- choose the question
- collect public-safe evidence
- attach limitations
- name next owners
- run claim checklist
- stop conditions
- handoff notes

The required stop conditions are:

- missing proof path
- private-only support
- raw artifact leakage
- unknown or reduced coverage without label
- unsupported runtime, release, or safety wording
- no next owner
- no validation evidence

## Handoff Notes Template

The future page may include a compact template, but the template must stay
human-authored and public-safe. Recommended fields:

| Field | Purpose |
| --- | --- |
| Review question | Names the claim or decision being reviewed. |
| Audience | Names who will receive the packet. |
| Bounded claim | States only what public-safe static evidence supports. |
| Proof path | Links to the public-safe source trail or private review location. |
| Evidence metadata | Keeps rule ID/family, tier, coverage label, commit SHA, extractor version, and file span together. |
| Limitations and non-claims | Prevents the packet from becoming a stronger claim. |
| Validation evidence | Records what was checked without exposing raw logs. |
| Unresolved gaps | Keeps reduced, unknown, private-only, or pending evidence visible. |
| Next owner | Names who handles the remaining review or non-static question. |

No example should contain raw source, raw SQL, configuration values, secrets,
local paths, raw remotes, generated scan directories, private sample names,
hidden capability names, real internal reviewer names, private owner names, or
hidden validation details.

## Differentiation From Neighboring Pages

The page should include a concise comparison block:

- Use `/packets/` for the general evidence packet artifact model.
- Use `/manager-packet/` for manager-facing summary framing.
- Use `/team-evidence-handoff/` for receiver-specific handoff language.
- Use `/incident-evidence-handoff/` for incident-adjacent static evidence
  transfer.
- Use `/review-room/` for the meeting agenda around known, partial, and
  missing evidence.
- Use `/review-claim-checklist/` to decide whether a sentence may be repeated.
- Use `/proof-source-catalog/` to map public routes and claims to source
  material.
- Use `/use-cases/change-review/` for change-review framing.
- Use `/questions/` for stakeholder question orientation.
- Use the claim-ledger concept for claim vocabulary and upgrade or downgrade
  rules.
- Use review packet assembly to gather the ingredients and stop-condition
  checks before handoff.

The comparison must not imply any neighboring page proves runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, AI or LLM analysis, autonomous review, or complete
coverage.

## Metadata and Discovery

If standalone, metadata should describe the route as a concept guide for
assembling public-safe review packets from existing deterministic static
evidence. Titles and descriptions should avoid runtime, production,
endpoint-performance, outage-cause, release-safety, operational-safety,
AI/LLM, autonomous-review, and complete-coverage claims.

Discovery metadata should use:

- `publicClaimLevel: concept`
- `sourceType`: site page or the closest existing value
- `hintCategory`: `use-case`, matching the existing discovery vocabulary for
  adjacent concept and workflow surfaces unless the discovery schema has a
  more specific existing value at implementation time
- `preferredProofPath`: `/proof-paths/` when that route exists
- `limitations`: static-evidence and public-safety boundaries
- `nonClaims`: runtime, production, endpoint-performance, outage-cause,
  release-safety, operational-safety, AI/LLM, autonomous-review, generated
  packet-builder, and complete-coverage claims

If section placement is chosen, discovery metadata should follow the existing
page pattern and add a stable anchor when the site supports section-level
discovery.

## Validation Design

The future implementation should add focused validation comparable to adjacent
concept-page validators. Validation should check:

- rendered route or section exists
- `Public claim level: concept`
- `No public conclusion without evidence`
- required ingredient labels
- required workflow section labels
- required stop-condition labels
- required adjacent links when those routes exist
- metadata and discovery claim level
- sitemap metadata when standalone
- internal link resolution in generated site output
- rendered word count between 400 and 1500 words unless section placement
  records a stricter constraint
- word-count risk from the large required ingredient, workflow, stop-condition,
  and non-claim set; implementation should prefer compact tables and short
  bullets over duplicating neighboring explanations
- forbidden generated packet-builder feature claims
- forbidden runtime, production, release-safety, operational-safety, AI/LLM,
  autonomous-review, and complete-coverage positioning
- scoped forbidden-claim checks that permit required non-claims only in
  explicit negated wording or sanctioned boundary regions
- forbidden private/raw material in rendered text, decoded HTML, raw HTML
  attributes, and metadata

Implementation validation should include:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- desktop and mobile browser sanity checks when layout or interaction changes
  are made

## Accessibility and Layout

The future implementation should reuse existing static site components and
semantic HTML patterns. Headings should be hierarchical, tables or checklists
should remain readable on mobile, and link text should identify the destination
or decision clearly.

No interactive form, upload flow, client-side state, runtime service, raw
artifact viewer, packet-builder generator, agent integration, or autonomous
review flow is part of this spec.
