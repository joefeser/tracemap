# Site TraceMap Tools Reviewer Quickstart Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept
Last updated: 2026-06-21
Branch: codex/spec-site-reviewer-quickstart
Worktree: isolated worktree; local absolute path omitted from tracked spec
Base: origin/dev
PR target: dev

## Summary

This spec-only packet defines a future public-site reviewer quickstart for
inspecting a TraceMap evidence packet in about five minutes. It creates no site
source, scanner, reducer, validation-script, generated-output, runtime,
AI/LLM, embedding, vector database, prompt-classification, or autonomous
approval changes.

## Scope Decisions

- Tracked changes are limited to
  `.kiro/specs/site-tracemap-tools-reviewer-quickstart/`.
- The future public claim level remains `concept`.
- The future rendered surface must visibly include `Public claim level:
  concept` and `No public conclusion without evidence`.
- Selected future placement: `/reviewer-quickstart/`.
- Rejected `/review-room/quickstart/`: too tied to review-room sessions.
- Rejected section on `/review-room/`: would mix quick orientation with the
  deeper evidence meeting agenda.
- Rejected section on `/packets/assembly/`: assembly prepares a packet; this
  quickstart inspects one.
- The future implementation is required to record any placement change before
  editing site source.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-reviewer-quickstart --kind spec --model claude-opus-4.8 --fresh --save-review-text`
  - Result: review completed with reduced coverage because Kiro reported
    denied tool access for its own shell/write tools in non-interactive mode.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-reviewer-quickstart/2026-06-21T205043-140Z-spec-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-reviewer-quickstart --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: review completed with reduced coverage because Kiro reported
    denied tool access for its own shell/write tools in non-interactive mode.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-reviewer-quickstart/2026-06-21T205308-826Z-spec-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.

## Review Findings

- No High or Medium findings from either Kiro reviewer.
- `claude-opus-4.8` reported no High or Medium findings. It confirmed the
  packet, requirements, design, and tasks are internally consistent, and noted
  only Low or informational implementation guardrails around live discovery
  vocabulary and conditional manager-script linking.
- `claude-sonnet-4.6` independently reported no High or Medium findings. It
  confirmed required phrases, quickstart steps, evidence fields, safe verbs,
  owner categories, stop conditions, word-count bounds, final placement, and
  rejected alternatives are consistent.
- No Medium or higher findings required patching, so no Kiro re-review was
  needed after the initial reviews.
- Readiness moved to `ready-for-implementation` after the no-Medium-or-higher
  findings were recorded.

## Validation

- `git diff --check`: passed on 2026-06-21.
- `./scripts/check-private-paths.sh`: passed on 2026-06-21.
- Focused spec text checks: passed on 2026-06-21 for required
  status/readiness/claim-level copy, required principle text, final placement,
  rejected placement alternatives, required sections, required quickstart
  steps, required boundary terms, and forbidden local/private or credential
  markers across all five new spec files.
- `npm test`, `npm run validate`, and `npm run build` from `site/`: deferred
  to future implementation because this phase is spec-only and does not change
  site source.

## Oddities

- The spec intentionally overlaps vocabulary with review-room, packet
  assembly, claim checklist, proof-path tour, question index, manager script,
  and demo runbook surfaces. Its distinct job is a first-five-minutes
  reviewer inspection guide.
- Local review artifacts and local absolute paths are intentionally omitted
  from tracked spec notes.

## Follow-Ups

- Run Kiro spec reviews with `claude-opus-4.8` and `claude-sonnet-4.6` when
  available.
- Patch or disposition Medium or higher findings.
- Move readiness to `ready-for-implementation` only after findings are patched
  or dispositioned.
