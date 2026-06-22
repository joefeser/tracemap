# Site TraceMap Tools Owner Follow-Up Map Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Current Branch And Worktree

- Branch: `codex/spec-site-owner-followup-map`
- Base: `origin/dev`
- PR target: `dev`
- Worktree: `<isolated-spec-worktree>`
- Root checkout untouched by this spec packet.

## Scope

Create a spec-only Kiro packet for a future public-site owner follow-up map.
The packet is limited to:

- `.kiro/specs/site-tracemap-tools-owner-followup-map/requirements.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/design.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/tasks.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/implementation-state.md`
- `.kiro/specs/site-tracemap-tools-owner-followup-map/review-packet.md`

Do not edit `site/src`, generated output, scanner code, or existing specs in
this phase.

## Placement State

Final placement is not selected in this spec-only phase. Candidate placements
for future implementation are:

- `/owners/follow-up/`
- `/review-room/owners/`
- A section on `/team-evidence-handoff/`
- A section on `/questions/`

Future implementation must record the selected placement, rejected
alternatives, discovery/sitemap handling, and rationale here before changing
site source.

## Scope Decisions

- The page is concept-level because it routes questions to owner categories,
  not real people, teams, approval chains, or production ownership records.
- The map must preserve proof path, limitation, and stop condition with every
  owner handoff.
- Missing evidence is a follow-up question, not a clean conclusion and not an
  accusation.
- The spec must distinguish this future surface from existing handoff,
  reviewer, question, packet, objection, and manager surfaces.

## Review Log

- Opus spec review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 embedded-section word-count ceiling conflict, M2 embedded self-host
  relationship/link conflict, L1 placement preference wording, L2 test
  coverage exception mismatch, L3 accessibility validation gap. Patch pass
  completed for M1/M2 and the Low findings in `requirements.md` and
  `design.md`.
- Sonnet spec review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: H1 design matrix incomplete, M1 embedded ceiling fallback missing
  from design, M2 implementation gate absent from design, M3 review log not
  updated, L1 concrete stop-condition note, L3 vague link-risk note. Patch
  pass completed for H1/M1/M2/M3 and Low L1/L3 in `design.md` and
  `implementation-state.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 review-packet review-log pointer absent, M2 embedded ceiling
  negative-test case missing, plus Low findings for file lists, config trigger
  wording, re-review model specificity, and link-risk reminder completeness.
  Patch pass completed in `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Opus re-review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 unsubstituted handoff placeholder validation absent, M2 non-claim
  exception semantics absent from forbidden-claim validation, plus Low
  findings for link-risk reminder completeness, word-count floor alignment,
  and standalone claim-level metadata alignment. Patch pass completed in
  `requirements.md`, `design.md`, and `implementation-state.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 tasks checklist missing embedded-ceiling negative test, M2
  tasks checklist missing unsubstituted-placeholder negative test, plus Low
  findings for current review-log update and review-packet focus coverage.
  Patch pass completed in `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 bracket-token pattern needed a spec-only template signal, M2
  completed review cycles were not reflected in `tasks.md`, plus Low findings
  for current review-log update and review-packet focus coverage. Patch pass
  completed in `design.md`, `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: M1 patch/re-review task checkbox still pending, M2 validation
  checkboxes still pending. Patch pass completed by running validation,
  updating `tasks.md`, and recording validation results in
  `implementation-state.md`.
- Opus re-review complete on 2026-06-22 with `claude-opus-4.8`. Findings:
  M1 standalone word-count ceiling needed mandatory-content precedence, M2
  readiness conflicted with pending Opus re-review log state, plus Low
  findings for stale review-packet readiness wording and owner-category
  allow-list precision. Patch pass completed in `requirements.md`,
  `design.md`, `implementation-state.md`, and `review-packet.md`.
- Sonnet re-review complete on 2026-06-22 with `claude-sonnet-4.6`.
  Findings: no content or validation-spec defects; only review-log pending
  state remained to record.
- Opus final re-review complete on 2026-06-22 with `claude-opus-4.8`. No
  Medium or higher content findings after the mandatory-content precedence
  patch. The precedence patch was additive and conservative: it relaxes the
  1700-word ceiling only for mandatory rows, row fields, and boundary
  statements, while leaving the 600-word floor and all forbidden-claim and
  private-material checks intact. Readiness remains
  `ready-for-implementation`.

## Validation Log

- Passed on 2026-06-22: `git diff --check`.
- Passed on 2026-06-22: `./scripts/check-private-paths.sh`.

Spec-only phase validation did not run `npm run build` from `site/` or browser
sanity checks because no site source changes are made. Those checks are
required in the future implementation phase.

## Oddities

- The future page must visibly name owner categories while repeatedly stating
  that TraceMap does not know real org ownership.
- Some candidate required links may not exist at implementation time. Future
  implementation must substitute, defer, or record the route gap before
  publishing a dead link.
- Links to verify at implementation time include `/team-evidence-handoff/`,
  `/incident-evidence-handoff/`, `/reviewer-quickstart/`, `/questions/`,
  `/questions/objections/`, `/packets/assembly/`, `/manager-packet/`,
  `/proof-paths/`, `/limitations/`, and `/validation/`. Any route that is not
  live must be substituted or deferred with rationale.

## Follow-Up Items

- Future implementation should choose standalone route versus embedded
  section based on current site information architecture.
- Future implementation should add focused validator coverage before
  publishing the surface.
- Future implementation should run desktop and mobile browser sanity checks
  for the selected route or host page.
