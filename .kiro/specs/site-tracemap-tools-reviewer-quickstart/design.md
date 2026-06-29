# Site TraceMap Tools Reviewer Quickstart Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

This design defines a future public-site quickstart for reviewers who need to
inspect a TraceMap evidence packet quickly. It is a short orientation layer
over existing public-safe proof, packet, review-room, checklist, validation,
and limitation surfaces.

The future page should answer one question: "What should a reviewer check
before repeating or acting on this evidence packet?"

It must remain concept-level guidance. It does not implement scanner behavior,
reducer behavior, runtime proof, release approval, autonomous approval, AI or
LLM analysis, embeddings, vector database analysis, prompt classification, or
replacement for tests, source review, code review, telemetry, or human
judgment.

## Information Architecture

Selected future route: `/reviewer-quickstart/`.

Rejected placements:

| Placement | Reason rejected |
| --- | --- |
| `/review-room/quickstart/` | Too tied to the meeting-room metaphor; this guide should serve any code reviewer inspecting a packet. |
| Section on `/review-room/` | Would mix first-visit orientation into the deeper known, partial, and missing evidence agenda. |
| Section on `/packets/assembly/` | Assembly is about preparing a packet; this guide is about inspecting one. |

The page should not be added to primary navigation unless implementation finds
an existing navigation pattern for concept-level reviewer entry pages and
records that decision. Discovery should come from sitemap metadata, discovery
metadata, and contextual links from adjacent surfaces.

## Required Page Structure

The future page should use these sections in this order:

1. `Start Here`
2. `Five-Minute Review`
3. `Evidence Fields`
4. `Stop Conditions`
5. `Safe Review Language`
6. `Escalation Owners`
7. `Non-Claims`

The header area must visibly include:

- `Public claim level: concept`
- `No public conclusion without evidence`

## Content Model

### Start Here

Start with the reviewer stance:

- Name the claim.
- Find the proof path.
- Keep claim level, rule, tier, coverage, context, limitations, and next owner
  attached.
- Stop when evidence is missing.

The copy should address code reviewers, reviewer agents, maintainers, and
engineering owners without implying that the page performs review or approval.

### Five-Minute Review

Required step labels:

1. `identify the claim`
2. `find the proof path`
3. `check public claim level`
4. `read rule ID/family`
5. `inspect evidence tier and coverage label`
6. `check commit/extractor context`
7. `read limitations/non-claims`
8. `name next owner`
9. `stop on missing evidence`

This section should be compact. It can be a numbered list or table. Each step
should have one sentence of guidance and a stop signal when evidence is absent.

### Evidence Fields

Expected field labels:

| Field | Meaning |
| --- | --- |
| Claim | The exact claim under review. |
| Proof path | Public-safe route, summary, documented trail, or named private review location. |
| Public claim level | The claim-strength boundary for the page or packet. |
| Rule ID or rule family | Deterministic rule basis and documented limitation. |
| Evidence tier | `Tier1Semantic`, `Tier2Structural`, `Tier3SyntaxOrTextual`, or `Tier4Unknown`. |
| Coverage label | The exact coverage state from the cited evidence. |
| Commit or source context | Source revision context when public-safe and available. |
| Extractor context | Extractor version or family when public-safe and available. |
| File path and line span | Public-safe checked-in or sanitized reference, never raw source. |
| Limitation | What the evidence cannot prove. |
| Non-claim | What the reviewer must not infer. |
| Validation evidence | Public-safe validation or review summary. |
| Unresolved gap | Missing, reduced, unknown, private-only, or pending evidence. |
| Next owner | Who handles the remaining review question. |

### Stop Conditions

Stop conditions should be rendered as direct blocker labels:

- missing proof path
- missing rule ID or rule family
- missing evidence tier
- missing coverage label
- missing limitation
- missing public claim level
- missing commit or extractor context without explicit limitation
- no validation evidence
- no next owner
- private-only support presented as public proof
- raw artifact leakage
- unsupported runtime, release, safety, production, AI/LLM, autonomous
  approval, or complete-coverage wording

### Safe Review Language

Safe wording should use bounded verbs:

- `inspect`
- `check`
- `follow`
- `review`
- `compare`
- `label`
- `record`
- `route`
- `escalate`
- `cannot conclude from this packet`

Unsafe wording categories include release approval, release safety, operational
safety, production proof, outage cause, endpoint performance, autonomous
approval, AI/LLM analysis, complete coverage, and unqualified impact
statements.

### Escalation Owners

The quickstart should name owner categories rather than individuals:

- reviewer owner
- source review owner
- code owner
- service owner
- database owner
- test owner
- validation owner
- telemetry or runtime owner
- release owner
- manager or decision owner

Owner categories should explain where the next question goes. They must not
assign blame or imply that missing evidence is a defect in a team or vendor.

### Non-Claims

The non-claims section must state that the quickstart and cited packets do not
prove runtime behavior, production traffic, endpoint performance, outage
cause, release approval, release safety, operational safety, complete
coverage, AI or LLM analysis, embeddings, vector database analysis, prompt
classification, autonomous approval, or replacement of tests, code review,
source review, runtime observability, or human judgment.

## Neighboring Surface Links

Required adjacent links when routes exist:

- `/review-room/`
- `/packets/assembly/`
- `/review-claim-checklist/`
- `/proof-paths/tour/`
- `/proof-paths/`
- `/questions/`
- `/demo/runbook/`
- `/limitations/`
- `/validation/`
- `/demo/manager-script/` or the live manager script route if different

If an adjacent route is missing, renamed, or not public at implementation time,
record the substitution or deferral in `implementation-state.md`.

## Metadata and Discovery

Standalone route implementation should include:

- route title and description
- canonical URL
- Open Graph title, description, URL, and type matching adjacent concept-page
  patterns
- sitemap entry for `/reviewer-quickstart/`
- discovery entry with `publicClaimLevel: concept`
- preferred proof path `/proof-paths/`
- limitations and non-claims matching this spec's boundaries

Use an existing discovery `hintCategory` value at implementation time. Do not
invent a new category unless the site discovery schema is explicitly extended
in the same implementation and validated.

## Validation Design

Future implementation should add focused validation comparable to adjacent
site concept validators. Validation should check:

- rendered route exists
- exact required phrases `Public claim level: concept` and
  `No public conclusion without evidence`
- required section headings
- required quickstart step labels
- required evidence field labels
- required stop-condition labels
- required adjacent links when routes exist
- route metadata, sitemap metadata, and discovery metadata
- public claim level remains concept
- word count between 500 and 1400 rendered words unless a tighter bound is
  recorded
- forbidden runtime, production, endpoint-performance, outage-cause,
  release-approval, release-safety, operational-safety, complete-coverage,
  AI/LLM, embeddings, vector database, prompt-classification, autonomous
  approval, and replacement claims
- forbidden raw or private material in rendered text, decoded HTML, raw HTML
  attributes, metadata, discovery fixtures, and tests
- no blame language around vendors, consultants, teams, or code quality

Required validation commands for the future implementation:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- focused text checks for this spec and the future route
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- desktop and mobile browser sanity checks

## Accessibility and Layout

The page should reuse existing static-site layout and semantic HTML patterns.
It should favor short sections, tables or checklists that remain readable on
mobile, clear link text, and no interactive state. If a table is used for
evidence fields, it must remain usable on mobile using existing table-wrap
patterns or an equivalent accessible layout.
