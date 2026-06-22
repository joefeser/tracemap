# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-22
Branch: codex/spec-site-evidence-handoff-template
Worktree: isolated spec worktree; local absolute path omitted from tracked spec
Base: origin/dev
PR target: dev
Public claim level: concept

## Summary

This spec-only packet defines a future public-site evidence handoff template.
The future surface should help humans carry one bounded TraceMap static
evidence question, proof path, evidence metadata, limitation, non-claim,
validation evidence, owner to ask, and stop condition to another reviewer or
owner.

No site source, generated output, scanner code, reducer code, or existing spec
was changed in this phase.

## Scope Decisions

- The packet is concept-level only.
- The shared public principle is `No public conclusion without evidence`.
- Candidate placements are `/handoff/template/`,
  `/team-evidence-handoff/template/`, a section on
  `/team-evidence-handoff/`, or a section on `/packets/assembly/`.
- Final placement is intentionally deferred to the future implementation after
  checking the live neighboring routes.
- Future implementation must distinguish this template from
  `/team-evidence-handoff/`, `/incident-evidence-handoff/`,
  `/packets/assembly/`, `/reviewer-quickstart/`, `/owners/follow-up/`, and
  `/decisions/evidence-record/`.
- The spec forbids generated handoff feature claims, real organization
  ownership claims, runtime proof, release approval, operational safety,
  complete coverage, AI or LLM analysis, and replacement of human review.
- The tracked state note avoids local absolute paths so the repository
  private-path guard can remain strict.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-handoff-template --kind spec --model claude-opus-4.8 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access after
    reading the spec. Artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T224406-243Z-spec-claude-opus-4.8.clean.md`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-evidence-handoff-template --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access after
    reading the spec. Artifact:
    `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T224731-037Z-spec-claude-sonnet-4.6.clean.md`.
- Re-reviews were run with both requested models after Medium findings were
  patched. Several intermediate re-reviews returned reduced coverage from
  Kiro tool-denial or one Kiro internal server/tool-approval error; exact
  errors are preserved in the matching `.meta.json` and `.clean.md`
  artifacts under `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/`.
- Latest full-coverage Opus review artifact before the final Sonnet
  bookkeeping pass:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T230326-748Z-re-review-claude-opus-4.8.clean.md`.
- Latest Sonnet review artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-evidence-handoff-template/2026-06-22T232058-496Z-re-review-claude-sonnet-4.6.clean.md`.

## Review Findings

- Initial Opus review found two Medium issues: validation scoping for
  required non-claims/unsafe examples, and private proof-path wording that
  could be read as permitting private identifiers. Both were patched.
- Initial Sonnet review found Medium issues around operator-requested model
  task wording, machine-checkable validation context, and spec-phase
  private-material checks. The model task was clarified as dispositioned if
  unavailable, and validation/private-material checks were tightened.
- Re-review rounds found additional Medium issues around context-scoped
  forbidden-claim validation, support-link traceability, synthetic example
  completeness, word-count behavior, checklist fields, named-person/org
  validation, realistic SHA/token scanning, placement wording, embedded
  placement viability, and review-packet checklist completeness. All Medium
  or higher findings were patched.
- Final Sonnet review found one Medium bookkeeping issue: this state file
  still said "Not reviewed yet" after reviews had run. This section records
  the review commands and findings, resolving that bookkeeping issue.
- Remaining Low items are accepted residual notes only: minor wording parity,
  continuous guard checkbox shape, and expected future implementation details
  such as concrete metadata values.

## Validation

- `git diff --check`: passed on 2026-06-22.
- `./scripts/check-private-paths.sh`: passed on 2026-06-22.
- Focused spec text checks: passed on 2026-06-22 with 75 checks covering
  required headers, all 15 required fields, required sections, all required
  stop conditions, all six neighbor distinctions, boundary wording, synthetic
  labeling, word-count expectations, and forbidden raw/private patterns.

If `./scripts/check-private-paths.sh` is absent at review time, record the
absence here and treat the check as an open gap rather than a pass.
The manual substitute for an absent private-path guard is a grep-based scan of
new spec files for absolute paths, private remote patterns, named individuals,
real organization names, command output, and credential-like strings; record
the scan command and result here before marking the task done.
`git diff --check` is a standard git command and is expected to be available.
If it fails for an unexpected reason, record the exact error here before
treating the check as done.

## Oddities

- `/owners/follow-up/` and `/decisions/evidence-record/` may be planned or
  absent routes at future implementation time. The implementation should link
  them only if they exist and otherwise record the route gap.
- The template intentionally overlaps vocabulary with handoff and packet
  surfaces. Its distinct job is to keep one claim's reusable handoff fields
  together, not to explain receiver-specific language or the broader packet
  assembly process.

## Follow-ups

- Future implementation must choose final placement only after checking live
  neighboring routes and must update this state file with the selected route,
  rejected alternatives, validation evidence, and route gaps.
