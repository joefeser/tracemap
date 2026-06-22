# Site TraceMap Tools Public Claim Review Drill Tasks

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

These tasks track a spec-only public-site phase. Keep future implementation
tasks unchecked in this packet. Do not edit `site/src`, generated output,
scanner code, reducer code, validation scripts, or existing specs in this
phase.

`Status` remains `not-started` for this spec-only packet. A future
implementation may move it only when implementation work actually starts and
the change is recorded in `implementation-state.md`.

## Spec Review Tasks

- [x] Run Kiro spec review with `claude-opus-4.8` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [x] Run Kiro spec review with `claude-sonnet-4.6` or record the exact
  unavailable-tool/model error in `implementation-state.md`.
- [ ] If both `claude-opus-4.8` and `claude-sonnet-4.6` are unavailable,
  record the exact errors, run the review with the best available model, and
  record the substitution and rationale in `implementation-state.md`. Do not
  skip the review entirely; the readiness gate requires at least one completed
  review unless the review harness itself is unavailable.
- [x] Patch Medium or higher spec findings and rerun re-review where feasible.
  Note: targeted and final confirmation re-reviews ran on 2026-06-22 with
  reduced coverage because Kiro reported denied write-tool access; their clean
  artifacts were inspected and Medium findings were patched or dispositioned.
- [x] Move `Readiness` to `ready-for-implementation` only after Medium or
  higher findings are patched or explicitly dispositioned in
  `implementation-state.md`.
- [x] Run `git diff --check`.
- [x] Run `./scripts/check-private-paths.sh`.

## Future Implementation Tasks

- [ ] Confirm or update this spec-local `implementation-state.md` with branch,
  route choice, scope decisions, review results, validation plan, and initial
  implementation status before changing site code.
- [ ] Choose the final route or placement:
  `/claims/review-drill/`, `/review-claim-checklist/drill/`, section on
  `/review-claim-checklist/`, section on `/proof-paths/tour/`, or a recorded
  equivalent.
- [ ] Record the selected placement and rejected alternatives in
  `implementation-state.md`.
- [ ] Explain why the selected placement is a drill surface rather than the
  canonical claim checklist, proof-path tour, proof-path FAQ, objections page,
  packet examples page, or change-risk language guide.
- [ ] Add the concept-level drill page or section using existing static-site
  layout, typography, accessibility, metadata, and validation patterns.
- [ ] Include visible `Public claim level: concept` copy.
- [ ] Include visible `No public conclusion without evidence` copy.
- [ ] Keep the page or section out of primary navigation unless
  implementation-state records a matching site information-architecture
  decision.
- [ ] If implemented as a standalone route, add title, description, canonical
  URL, Open Graph metadata, sitemap metadata, and discovery metadata with
  concept-level wording.
- [ ] If implemented as a section, record the host route's claim-level
  reconciliation and add stable unique anchors for the drill sections.
- [ ] Include required sections: drill setup, sample public-safe claims,
  evidence checklist, answer key, unsafe answer examples, stop conditions, and
  non-claims.
- [ ] Include all seven required drill rows: supported demo-level claim,
  concept-only claim, reduced-coverage claim, unsafe runtime claim, unsafe
  release claim, private-evidence-only claim, and missing-proof claim.
- [ ] For every row, include claim text, expected claim level, proof path
  needed, evidence fields to check, limitation or non-claim, correct outcome,
  and next action.
- [ ] Keep row expected claim levels limited to `shipped`, `demo`, `concept`,
  and `hidden`.
- [ ] Keep answer-key outcomes limited to `repeat with proof`,
  `downgrade before repeating`, `owner follow-up needed`, `do not repeat`, and
  `internal only`.
- [ ] Ensure the supported demo-level row requires public-safe demo proof,
  rule ID or rule family, evidence tier, coverage label, limitation, and
  non-claim before it can be repeated.
- [ ] Ensure the concept-only row remains concept-level and cannot be presented
  as shipped or demo-backed unless separately proved.
- [ ] Ensure the reduced-coverage row keeps reduced or partial coverage visible
  and forces downgrade or owner follow-up unless the claim is narrowed.
- [ ] Ensure the unsafe runtime row rejects runtime behavior, production
  traffic, endpoint performance, and outage-cause conclusions.
- [ ] Ensure the unsafe release row rejects release approval, release safety,
  operational safety, and replacement of release controls.
- [ ] Ensure the private-evidence-only row stays internal or routes to owner
  follow-up until public-safe summary evidence exists.
- [ ] Ensure the missing-proof row does not allow confidence, seniority,
  repetition, or pressure to fill missing proof.
- [ ] Link to `/review-claim-checklist/` when it exists and explain that it
  remains the canonical real-claim decision ritual.
- [ ] Link to `/proof-paths/tour/` when it exists and explain that it remains
  the guided proof-path reading flow.
- [ ] Link to `/proof-paths/faq/` when it exists and explain that it remains
  proof-path question-and-answer context.
- [ ] Link to `/questions/objections/` when it exists and explain that it
  remains stakeholder concern handling.
- [ ] Link to `/packets/examples/` when it exists and explain that it remains
  concrete packet reading examples.
- [ ] Link to `/language/change-risk/` when it exists and explain that it
  remains change-risk wording guidance.
- [ ] For any adjacent route that is absent, renamed, or replaced, record the
  route status and substitution, deferral, or omission in
  `implementation-state.md`.
- [ ] Do not publish raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation details,
  or credential-like values.
- [ ] Do not reveal internal reviewer identities, private sample identities,
  hidden capability names, in-flight sequencing, confidential customer
  context, private repository details, or local-only evidence.
- [ ] Avoid blame language in page copy, metadata, examples, validation
  messages, and review-packet references.
- [ ] Add focused validation for visible concept claim level and shared
  principle.
- [ ] Add focused validation for required sections and row scenarios.
- [ ] Add focused validation for row field completeness.
- [ ] Add focused validation that `evidence fields to check` enumerates proof
  path, rule ID or rule family, evidence tier, coverage label, limitation,
  non-claim, source context, and public/private status rather than one vague
  value.
- [ ] Add focused validation that rows with `repeat with proof` expose
  discrete rule ID or rule family, evidence tier, and coverage label.
- [ ] Add focused validation that expected claim levels use only `shipped`,
  `demo`, `concept`, and `hidden`.
- [ ] Add focused validation for answer-key outcomes.
- [ ] Add focused validation that each required row scenario maps only to its
  allowed answer-key outcome set.
- [ ] Add focused validation for adjacent-route link resolution and recorded
  substitutions.
- [ ] Add focused validation for standalone metadata, sitemap metadata, and
  discovery `publicClaimLevel: concept` if a standalone route is chosen.
- [ ] Add focused validation for forbidden automated-grading, runtime-proof,
  release-safety, operational-safety, absence-of-impact, complete-coverage,
  AI/LLM, and replacement-of-human-review claims.
- [ ] Add focused validation for forbidden private or raw material across
  rendered text, decoded HTML attributes, raw HTML, metadata, sitemap or
  discovery output, fixtures, tests, and bot-oriented discovery surfaces.
- [ ] Add focused validation for no blame language using a recorded advisory
  phrase set.
- [ ] Add focused validation that rendered body word count stays between 500
  and 1800 words while required sections and row fields remain present.
- [ ] Wire focused validation into the existing aggregate site validation
  workflow.
- [ ] Run `npm test` from `site/` after site source is added.
- [ ] Run `npm run validate` from `site/` after site source is added.
- [ ] Run `npm run build` from `site/` after site source is added.
- [ ] Run desktop and mobile browser sanity checks if route, layout, or
  interaction changes are made.
- [ ] Run `git diff --check`.
- [ ] Run `./scripts/check-private-paths.sh`.
- [ ] Update `implementation-state.md` with route decisions, substitutions,
  validation results, review findings, claim-boundary decisions, oddities, and
  follow-up items.
