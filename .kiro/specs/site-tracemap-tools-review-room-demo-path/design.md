# Site TraceMap Tools Review Room Demo Path Design

Status: implemented
Readiness: implemented
Public claim level: concept

## Overview

The future guided path is a concept-level site surface that connects the
existing review-room, agenda, proof-path, claim-checklist, packet, demo,
limitations, validation, and owner-follow-up surfaces into one bounded
visitor flow. It teaches a reader how to move from a static review question to
proof inspection, limitation checking, owner routing, and an explicit stop
when evidence is insufficient.

This design describes the future public-site shape only. It does not implement
site code or claim that a live guided product flow exists.

## Placement Decision Model

Candidate placements:

- `/review-room/demo-path/`: recommended starting point because the guided
  path belongs under the review room but should not crowd the base
  `/review-room/` concept page or the `/review-room/agenda/` meeting script.
- Section on `/review-room/`: allowed if implementation finds the path is short
  enough to support the base room without turning it into a tutorial page.
- Section on `/review-room/agenda/`: allowed if the path is primarily a
  meeting script extension rather than a visitor orientation route.
- Section on `/demo/start-here/`: allowed only if the implementation frames
  the path as demo interpretation and reconciles the section's concept claim
  level with the host page metadata.

The implementation records the final placement and rejected alternatives in
`implementation-state.md`. If standalone, route metadata, sitemap metadata,
discovery metadata, and generated-link validation are required. If sectioned,
stable anchors, host-page crowding checks, metadata reconciliation, and anchor
validation are required.

## Information Architecture

The path starts from review-room intent and branches only to public-safe
surfaces that exist in generated output.

Primary route families:

- Review start: `/review-room/` and `/review-room/agenda/`
- Proof inspection: `/proof-paths/` and `/proof-paths/tour/`
- Packet inspection: `/packets/`, `/packets/assembly/`, and
  `/packets/examples/` when present
- Claim decision: `/review-claim-checklist/`
- Demo context: `/demo/`, `/demo/start-here/`, `/demo/evidence-trail/`,
  `/demo/proof-assets/`, `/demo/result/`, `/demo/runbook/`, and
  `/demo/troubleshooting/` when present
- Boundaries: `/limitations/` and `/validation/`
- Owner routing: `/owners/follow-up/` when present, with role-label fallback

Related route families may be linked after the core path, such as
`/questions/`, `/questions/objections/`, `/reviewer-quickstart/`,
`/manager-brief/`, `/manager-packet/`, `/team-evidence-handoff/`,
`/decisions/evidence-record/`, and `/handoff/template/`.

## Page Structure

Recommended standalone route structure:

1. Hero: name the guided path, show `Public claim level: concept`, state
   `No public conclusion without evidence`, and reject live product
   completeness.
2. Static question chooser: offer one or more authored static questions and
   forbid runtime, release, outage, production, and safety questions as path
   outcomes.
3. Guided path table or card list: one row per required step with action,
   required evidence field or route, allowed outcome, limitation, next owner
   or next route, and stop condition.
4. Proof-path and packet requirements: list proof fields that must travel with
   any conclusion.
5. Limitations and non-claims: make unsupported wording visible and route it
   to limitations or owner follow-up.
6. Owner routing: map unresolved questions to role labels and public-safe
   routes.
7. Stop conditions: stable section that tells the visitor when to stop,
   downgrade, keep internal, or ask an owner.
8. Link trail: existing public-safe routes only, with generated-link
   validation.

## Step Contract

Each step must have the same field shape so validators and reviewers can tell
whether the future page is complete.

Required fields:

- `step label`
- `visitor action`
- `required evidence field or route`
- `allowed outcome`
- `limitation`
- `next owner or next route`
- `stop condition`

Required steps:

- `choose a static question`
- `open the review room`
- `inspect the agenda`
- `inspect proof paths`
- `inspect an evidence packet` (requires at least one packet route or a
  recorded substitute limitation; if no packet route exists in generated
  output, the step is retained with a visible limitation note, the omission is
  recorded in `implementation-state.md`, and the step does not claim
  packet-builder behavior)
- `run the claim checklist`
- `check limitations and non-claims`
- `route unresolved questions to owners`
- `stop when evidence is insufficient`

Allowed outcomes:

- `continue`
- `downgrade`
- `owner follow-up`
- `internal only`
- `stop`

Disallowed outcomes include unqualified `impacted`, `safe`, `approved`,
`root cause`, `complete`, and `production proven`.

Every required step supplies non-empty text for limitation, stop condition,
and next owner or next route. Placeholder values such as `-`, `n/a`,
`see above`, or `none` are not acceptable for those fields. If no stop
condition applies, the step says why stopping is not triggered.

## Proof And Packet Fields

The guided path requires proof and packet fields before any conclusion is
repeated:

- proof path
- rule ID or rule family
- evidence tier
- coverage label
- limitation
- non-claim
- public claim level
- validation evidence
- next owner
- public-safe source context when available

Public-safe source context may include commit SHA, extractor version, public
route, checked-in public demo path, report-family name, sanitized file path,
and line span only when an existing public-safe surface exposes those details.
Absent or private fields become visible limitations.

## Stop Model

The final guided-path step is a stop, not a conclusion. The page should make
stopping feel like a correct review outcome when evidence is insufficient.

Stop when:

- proof path is missing
- rule ID or rule family is missing
- evidence tier is missing
- coverage label is missing
- limitation is missing
- validation evidence is missing
- support is private-only without public-safe summary
- raw artifact material would leak
- runtime, production, release, safety, AI/LLM, or complete-coverage wording
  appears without support
- no next owner is assigned
- no public-safe packet route or substitute exists

## Owner Routing

Owner routing uses role labels rather than private names. The allowed owner
label vocabulary is: evidence owner, site owner, demo owner, source owner,
test owner, runtime owner, service owner, database owner, release reviewer,
validation owner, documentation owner, and manager/reviewer owner. No other
role labels may be used.

The page must explain that routing a question transfers responsibility for the
next review step. It does not prove, approve, diagnose, validate, or clear a
claim.

## Copy Boundaries

Required visible future copy:

- `Public claim level: concept`
- `No public conclusion without evidence`

Forbidden positioning:

- runtime proof
- production traffic knowledge
- endpoint performance measurement
- outage cause
- release approval or release safety
- operational safety
- production proof
- complete product coverage
- live workflow completeness
- AI impact analysis
- LLM analysis
- prompt-based proof or classification
- embeddings or vector database reasoning
- autonomous review

Forbidden public material:

- raw facts streams
- SQLite indexes
- analyzer logs
- source snippets
- raw SQL
- configuration values
- secrets or credential-like values
- local paths
- raw remotes
- generated directories
- private sample names
- hidden validation detail
- private owner names
- raw command output
- ignored output paths

## Metadata And Discovery

Standalone route metadata:

- canonical URL for the selected route
- title and description that say concept-level guided path
- Open Graph metadata following existing concept-page conventions
- sitemap entry in `site/src/_site/pages.json`
- discovery entry in `site/src/_site/discovery.json`
- `publicClaimLevel: concept`
- `hintCategory: use-case`
- `sourceType: site-page`
- `preferredProofPath: /proof-paths/`
- bounded limitations and non-claims

Section placement records why the host metadata remains sufficient and how the
section's visible concept claim level is reconciled with the host page.

## Validation Design

Future implementation validation should include focused route or section tests
plus generated-output validation.

Validation must check:

- required visible claim-level and principle copy
- required steps in order as one contiguous guided-path block
- required step fields
- required proof and packet fields
- required stop conditions
- required owner-routing role labels plus copy that routing does not prove,
  approve, diagnose, validate, or clear a claim
- link resolution for rendered links
- route metadata, sitemap, and discovery when standalone
- host anchors and metadata reconciliation when sectioned
- forbidden private/raw material
- forbidden runtime, production, release, safety, complete-coverage, and
  AI/LLM positioning

Negative tests should include:

- missing claim-level copy
- missing shared principle
- missing `inspect an evidence packet` step, including when all packet routes
  are absent
- missing `stop when evidence is insufficient` as the final step
- unresolved rendered link
- unsupported demo claim
- raw or private leakage
- unsupported runtime, release, safety, complete-coverage, or AI/LLM wording

Implementation validation commands:

- `git diff --check`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- `./scripts/check-private-paths.sh`
- desktop and mobile browser sanity checks for layout or interaction changes

## Non-Goals

- Implementing a site route or section in this phase
- Adding scanner, reducer, or generated artifact behavior
- Claiming live product completeness
- Claiming runtime behavior or production traffic proof
- Claiming endpoint performance, outage cause, release safety, or operational
  safety
- Adding AI, LLM, embedding, vector database, or prompt-based impact analysis
- Publishing raw or private material
- Adding top navigation
- Merging or squashing the branch
