# Site TraceMap Tools Public Claim Review Drill Tasks

Status: implemented
Readiness: implemented
Public claim level: concept

These tasks track the implemented public claim review drill route.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] If both `claude-opus-4.8` and `claude-sonnet-4.6` are unavailable,
  record the exact errors, run the review with the best available model, and
  record the substitution and rationale in `implementation-state.md`. Do not
  skip the review entirely; the readiness gate requires at least one completed
  review unless the review harness itself is unavailable.
  Note: not applicable for this packet; both requested models ran and their
  reduced-coverage outcomes are recorded in `implementation-state.md`.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
  Note: targeted and final confirmation re-reviews ran on 2026-06-22 with
  reduced coverage because Kiro reported denied write-tool access; their clean
  artifacts were inspected and Medium findings were patched or dispositioned.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned in
  `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.

## Implementation Tasks

- [x] Confirm or update this spec-local `implementation-state.md` with branch,
  route choice, scope decisions, review results, validation plan, and initial
  implementation status before changing site code.
- [x] Choose the final route or placement:
  `/claims/review-drill/`, `/review-claim-checklist/drill/`, section on
  `/review-claim-checklist/`, section on `/proof-paths/tour/`, or a recorded
  equivalent.
- [x] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [x] Explain why the selected placement is a drill surface rather than the
  canonical claim checklist, proof-path tour, proof-path FAQ, objections page,
  packet examples page, or change-risk language guide.
- [x] Add the concept-level drill page or section using existing static-site
  layout, typography, accessibility, metadata, and validation patterns.
- [x] Include visible `Public claim level: concept` copy.
- [x] Include visible `No public conclusion without evidence` copy.
- [x] Keep the page or section out of primary navigation unless
  implementation-state records a matching site information-architecture
  decision.
- [x] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [x] If implemented as a section, record the host route's claim-level
  reconciliation and add stable unique anchors for the drill sections.
- [x] Include required sections: drill setup, sample public-safe claims,
  evidence checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims.
- [x] Include all seven required drill rows: supported demo-level claim,
  concept-only claim, reduced-coverage claim, unsafe runtime claim, unsafe
  release claim, private-evidence-only claim, and missing-proof claim.
- [x] For every row, include claim text, expected claim level, proof path
  needed, evidence fields to check, limitation or non-claim, correct outcome,
  and next action.
- [x] Keep row expected claim levels limited to `shipped`, `demo`, `concept`,
  and `hidden`.
- [x] Keep answer-key outcomes limited to `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- [x] Ensure the supported demo-level row requires public-safe demo proof,
  rule ID or rule family, evidence tier, coverage label, limitation, and
  non-claim before it can be repeated.
- [x] Ensure the concept-only row remains concept-level and cannot be presented
  as shipped or demo-backed unless separately proved.
- [x] Ensure the reduced-coverage row keeps reduced or partial coverage visible
  and forces downgrade or owner follow-up unless the claim is narrowed.
- [x] Ensure the unsafe runtime row rejects runtime behavior, production
  traffic, endpoint performance, and outage-cause conclusions.
- [x] Ensure the unsafe release row rejects release approval, release safety,
  operational safety, and replacement of release controls.
- [x] Ensure the private-evidence-only row stays internal or routes to owner
  follow-up until public-safe summary evidence exists.
- [x] Ensure the missing-proof row does not allow confidence, seniority,
  repetition, or pressure to fill missing proof.
- [x] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the canonical real-claim decision ritual.
- [x] Link to `/proof-paths/tour/` when it exists and explain that it remains
  the guided proof-path reading flow.
- [x] Link to `/proof-paths/faq/` when it exists and explain that it remains
  proof-path question-and-answer context.
- [x] Link to `/questions/objections/` when it exists and explain that it
  remains stakeholder concern handling.
- [x] Link to `/packets/examples/` when it exists and explain that it remains
  concrete packet reading examples.
- [x] Link to `/language/change-risk/` when it exists and explain that it
  remains change-risk wording guidance.
- [x] For any adjacent route that is absent, renamed, or replaced, record the
  route status and substitution, deferral, or omission in
  `implementation-state.md`.
- [x] Do not publish raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  or credential-like values.
- [x] Do not reveal internal reviewer identities, private sample identities,
  hidden capability names, in-flight sequencing, confidential customer
  context, private repository details, or local-only evidence.
- [x] Avoid blame language in page copy, metadata, examples, validation
  messages, and review-packet references.
- [x] Add focused validation for visible concept claim level and shared
  principle.
- [x] Add focused validation for required sections and row scenarios.
- [x] Add focused validation for row field completeness.
- [x] Add focused validation that `evidence fields to check` enumerates proof
  path, rule ID or rule family, evidence tier, coverage label, limitation,
  non-claim, source context, and public/private status rather than one vague
  value.
- [x] Add focused validation that rows with `repeat with proof` expose
  discrete rule ID or rule family, evidence tier, and coverage label.
- [x] Add focused validation that expected claim levels use only `shipped`,
  `demo`, `concept`, and `hidden`.
- [x] Add focused validation for answer-key outcomes.
- [x] Add focused validation that each required row scenario maps only to its
  allowed answer-key outcome set.
- [x] Add focused validation for adjacent-route link resolution and recorded
  substitutions.
- [x] Add focused validation for standalone metadata, sitemap metadata, and
  discovery `publicClaimLevel: concept` if a standalone route is chosen.
- [x] Add focused validation for forbidden automated-grading, runtime-proof,
  release-safety, operational-safety, absence-of-impact, complete-coverage,
  AI/LLM, and replacement-of-human-review claims.
- [x] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML attributes, raw HTML, metadata, sitemap or
  discovery output, fixtures, tests, and bot-oriented discovery surfaces.
- [x] Add focused validation for no blame language using a recorded advisory
  phrase set.
- [x] Add focused validation that rendered body word count stays between 500
  and 1800 words while required sections and row fields remain present.
- [x] Wire focused validation into the existing aggregate site validation
  workflow.
- [x] Run `npm test` from `site/` after site source is added.
- [x] Run `npm run validate` from `site/` after site source is added.
- [x] Run `npm run build` from `site/` after site source is added.
- [x] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.
- [x] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
