# Site TraceMap Tools Incident Evidence Handoff Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-incident-evidence-handoff`
Base branch: `origin/dev`

## Scope

This spec defines a future concept-level public site page at
`/incident-evidence-handoff/`. The future page should be a public-safe handoff
packet/checklist for incident-adjacent conversations: what static evidence can
be brought, which proof path backs it, what it does not prove, and who should
own the next runtime, release, telemetry, test, service-owner, database-owner,
or incident-command question.

This branch is spec creation only. It must not implement site code, edit site
source, edit validators, edit metadata, or change generated output.

## Public Claim Level

Selected public claim level: `concept`.

Rationale: this page/section is not already backed by a shipped public route or
specific public demo proof. It should stay future-facing until a site
implementation supplies public-safe page copy, metadata, focused validation,
and browser sanity checks. The future page must not claim runtime behavior,
production traffic, endpoint performance, outage cause, release safety,
operational safety, AI impact analysis, LLM analysis, or complete product
coverage.

## Scope Decisions

- Chosen route: `/incident-evidence-handoff/`.
- Chosen source path for a future implementation:
  `site/src/incident-evidence-handoff/index.html`.
- Chosen page shape: handoff packet/checklist, not incident orientation,
  engineer triage checklist, review-room agenda, or manager FAQ.
- Required packet fields: static evidence, proof path, rule ID/evidence tier,
  coverage label, limitation, and next owner.
- Required neighboring route distinction line:
  `Incident evidence handoff is the packet of static evidence, proof paths, limits, and next owners; it is not runtime proof or incident command.`
- Required static-triage distinction line:
  `Static triage frames the question; the incident evidence handoff packet carries the already-framed evidence, proof paths, limits, and next owners into the next conversation.`
- The route should not be added to top navigation in this phase.

## Spec Review Commands and Results

Planned commands:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-incident-evidence-handoff --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-incident-evidence-handoff --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`

Results:

- `claude-opus-4.8` spec review command completed and saved artifacts, with
  reduced coverage because Kiro reported denied tool access. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-incident-evidence-handoff/2026-06-18T190228-982Z-spec-claude-opus-4.8.clean.md`.
- `claude-sonnet-4.6` spec review command completed with full coverage and
  saved artifacts. Clean artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-incident-evidence-handoff/2026-06-18T190744-086Z-spec-claude-sonnet-4.6.clean.md`.
- Initial review findings patched: neighboring packet route differentiation,
  `/static-triage/` wording separation, required-link reconciliation,
  routes-index expected fields, fixed preferredProofPath `/proof-paths/`,
  explicit denylist scope, word-count bounds, mandatory copy, ownership split
  rows, and validator/link-resolution semantics.

Re-review commands, if Medium or higher findings are patched:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-incident-evidence-handoff --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
  was rerun several times as concrete Medium+ findings were patched. Final
  clean artifact before the last patch cycle:
  `.tmp/kiro-reviews/site-tracemap-tools-incident-evidence-handoff/2026-06-18T191908-096Z-re-review-claude-sonnet-4.6.clean.md`.
- The final re-review produced fresh High/Medium findings about denylist scope,
  bare sensitive terms, rendered word count, link resolution, static-triage
  verification, and ownership-row validation. Those findings were patched in
  `requirements.md` and `design.md`.
- No further re-review was run after that final patch because repeated Sonnet
  re-review passes kept producing new secondary wording findings after prior
  Medium+ items were patched. Residual risk is recorded below.

## Validation

Spec-branch validation planned before PR:

- `git diff --check`
- `git diff --cached --check`
- `./scripts/check-private-paths.sh`

Results:

- `git diff --check` passed.
- `git diff --cached --check` passed.
- `./scripts/check-private-paths.sh` passed.

Implementation validation for a future site branch:

- `git diff --check`
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- `./scripts/check-private-paths.sh`
- desktop and mobile browser sanity checks for `/incident-evidence-handoff/`

## Oddities

- Opus review coverage was reduced because Kiro reported denied tool access,
  although it still read the spec files and produced useful findings.
- Sonnet re-review repeatedly returned nonzero wrapper output while saving
  full-coverage artifacts. The review text, not the wrapper exit display, was
  used to decide what to patch.
- The Kiro review loop was intentionally stopped after patching the final
  concrete High/Medium findings to avoid turning this spec-only branch into an
  unbounded wording chase.

## Follow-Ups

- A future implementation branch should decide whether any reciprocal links
  from `/incident-call/`, `/static-triage/`, `/review-room/`, `/manager-faq/`,
  `/packets/`, `/manager-packet/`, or `/manager-brief/` are useful enough to
  add without cluttering those pages.
- Before implementing links, verify which required routes currently exist in
  the live site: `/proof-paths/`, `/validation/`, `/limitations/`,
  `/demo/result/`, `/packets/`, `/manager-packet/`, `/manager-brief/`, and
  `/use-cases/incident-review/`. Document any missing routes as link gaps in
  this file before implementation.
- Before implementation, verify that `/static-triage/` still describes itself
  with handoff language and that the static-triage distinction line still
  accurately captures the differentiation.
- Before implementation, verify that `/incident-call/` still describes itself
  as a P1-call orientation narrative so the Requirement 2 differentiation
  language remains accurate.
- Before patching any future spec findings that touch the static-triage
  distinction line, verify that the live `/static-triage/` page still uses
  handoff-adjacent language that the distinction line accurately characterizes.
