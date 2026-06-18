# Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Summary

This spec-only phase defines a future manager FAQ page for `tracemap.tools`.
The future page should answer skeptical stakeholder questions about what
TraceMap can and cannot say from deterministic static evidence, while keeping
runtime, production, release-safety, operational-safety, and AI/LLM impact
analysis claims out of scope.

No site implementation is included in this phase.

## Branch

Spec branch: `codex/spec-site-manager-faq`
Target PR base: `dev`

## Scope Decisions

- Keep this phase spec-only under
  `.kiro/specs/site-tracemap-tools-manager-faq/`.
- Leave all implementation tasks unchecked because page work is future work.
- Define two allowed future routes, `/manager-faq/` and `/faq/manager/`, and
  require the implementation phase to record the final choice.
- Set public claim level to `concept` because the FAQ explains claim
  boundaries and intended stakeholder use, not a new scanner/reducer
  capability or production proof.
- Require links to `/manager-brief/`, `/manager-packet/`, `/review-room/`,
  `/limitations/`, `/validation/`, and `/proof-paths/` so the FAQ connects to
  existing manager and evidence-boundary surfaces.
- Keep raw artifacts, private identifiers, local paths, generated scan
  directories, and private sample names out of future public copy.

## Claim Boundaries

Safe to say:

- TraceMap can help managers ask better review questions from deterministic
  static evidence.
- Rule IDs, evidence tiers, coverage labels, limitations, and proof paths can
  show what static evidence supports and where gaps remain.
- Public summaries are presentation surfaces over deterministic evidence and
  should stay attached to limitations and proof paths.

Not safe to say:

- TraceMap proves runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- TraceMap replaces telemetry, logs, traces, tests, ownership, human review, or
  release process.
- TraceMap provides automated release approval, operational assurance, or
  production observability.

## Validation Run

Spec authoring validation:

- Completed with reduced coverage due to denied tool access: Kiro spec review
  with Opus,
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-faq --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`.
  Review found two Medium validation-contract gaps: discovery metadata was not
  explicitly verified, and runtime/release overclaim vocabulary was not
  explicitly checked. Both findings were patched into `requirements.md` and
  `tasks.md`.
- Completed with full coverage: Kiro spec review with Sonnet,
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-faq --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Review found two Medium findings: the seeded overclaim pattern did not cover
  every listed forbidden term, and spec-phase validation did not explicitly
  check that `tasks.md` stayed unchecked. Both findings were patched into
  `requirements.md` and `tasks.md`.
- Completed with full coverage and nonzero review status: Kiro spec re-review
  with Sonnet,
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-faq --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  Review found two Medium items: implementation-state still needed final rerun
  tracking, and the overclaim pattern needed common `proven` variants. The
  actionable portions were patched into `requirements.md`, `tasks.md`, and
  `implementation-state.md`. The review also incorrectly stated that the
  requested model slug and review script were not real; the command is
  retained because it is the exact requested review command and did run in this
  worktree.
- Passed with full coverage: final Kiro spec re-review with Sonnet,
  `node scripts/kiro-review.mjs --phase site-tracemap-tools-manager-faq --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`.
  No Medium or High findings remained. Low findings noted that spec-phase
  validations should be closed before merge and suggested optional future
  validation polish; the polish was applied where useful.
- Passed: `git diff --check`.
- Passed: `./scripts/check-private-paths.sh`.
- Passed: verified `tasks.md` contains no checked boxes.

Future implementation validation:

- Run `git diff --check`.
- Run `./scripts/check-private-paths.sh`.
- Run `npm test` from `site/`.
- Run `npm run validate` from `site/`.
- Run `npm run build` from `site/`.
- Run desktop and mobile browser sanity checks if layout or interaction
  changes.

## Oddities

- The Opus Kiro review completed and saved review artifacts, but reported
  reduced coverage because a shell tool request was denied by the review
  wrapper. The reviewer still read the spec files and verified assumptions
  through allowed file and grep tools.

## Follow-Up Items

- Patch any Medium or higher Kiro review findings before opening the PR.
- Record exact Kiro review command failures here if either requested model is
  unavailable or denied.
- During the future implementation phase, update route choice, validation
  results, review-loop outcomes, and task checkboxes as work completes.
