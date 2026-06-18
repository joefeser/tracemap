# Implementation State

Status: implemented
Readiness: ready-for-review
Public claim level: concept

## Summary

This implementation adds a manager FAQ page for `tracemap.tools`. The page
answers skeptical stakeholder questions about what TraceMap can and cannot say
from deterministic static evidence, while keeping runtime, production,
release-safety, operational-safety, and model-driven impact claims out of
scope.

## Branch

Spec branch: `codex/spec-site-manager-faq`
Implementation branch: `codex/impl-site-manager-faq`
Target PR base: `dev`

## Scope Decisions

- Add `/manager-faq/` as the public route.
- Reject `/faq/manager/` because the existing manager-facing route family uses
  top-level routes such as `/manager-brief/` and `/manager-packet/`.
- Set public claim level to `concept` because the FAQ explains claim
  boundaries and intended stakeholder use, not a new scanner/reducer
  capability or production proof.
- Link to `/manager-brief/`, `/manager-packet/`, `/review-room/`,
  `/limitations/`, `/validation/`, and `/proof-paths/` so the FAQ connects to
  existing manager and evidence-boundary surfaces.
- Add optional supporting links to `/docs/`, `/demo/`, `/demo/result/`,
  `/packets/`, and `/capabilities/` where the FAQ points to supporting public
  context.
- Add sitemap metadata, discovery metadata, safe cross-links from manager
  pages, and a dedicated validator/test pair.
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

Implementation validation completed on 2026-06-18:

- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- `npm test` from `site/` passed.
- `npm run validate` from `site/` passed.
- `npm run build` from `site/` passed.
- Desktop browser sanity check for `/manager-faq/` at 1440px width confirmed
  expected title, H1, claim-level text, shared principle, and no horizontal
  overflow.
- Mobile browser sanity check for `/manager-faq/` at 390px width confirmed no
  horizontal overflow.
- After Codex PR review, `npm test`, `npm run validate`, `npm run build`,
  `git diff --check`, and `./scripts/check-private-paths.sh` passed again.

Manual overclaim review: the rendered FAQ uses "proof" only to describe proof
boundaries and proof paths. Strong runtime, production, approval, and release
phrases appear only in sanctioned non-claim/boundary framing or are avoided.

## Oddities

- The Opus Kiro review completed and saved review artifacts, but reported
  reduced coverage because a shell tool request was denied by the review
  wrapper. The reviewer still read the spec files and verified assumptions
  through allowed file and grep tools.
- Initial `npm run validate` caught two copy issues in the rendered page:
  "Safe to discuss" and "private sample names". Both were patched to
  "Shareable summary" and "private sample identifiers" before final validation
  passed.
- Codex PR review found that the overclaim validator caught status words but
  missed affirmative proof phrases such as "TraceMap proves runtime behavior".
  Patched the validator with a contextual proof-claim check and regression
  tests that reject affirmative proof phrases while allowing negated boundary
  wording such as "cannot prove runtime behavior".

## Follow-Up Items

- Consider adding `/manager-faq/` to top-level navigation only if future reader
  feedback shows the manager route family needs direct nav exposure.
