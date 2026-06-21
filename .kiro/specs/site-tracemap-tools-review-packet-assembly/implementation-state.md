# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-21
Branch: codex/spec-site-review-packet-assembly
Worktree: isolated spec worktree; local absolute path omitted from tracked spec
Base: origin/main
PR target: main
Public claim level: concept

## Summary

This spec-only branch defines a future public-site review packet assembly
surface. The future surface is a human checklist for preparing public-safe
review handoff material from existing TraceMap evidence surfaces. It is not a
generated packet-builder feature, scanner change, reducer change, runtime
proof, release gate, or autonomous review workflow.

## Scope Decisions

- Write scope is limited to
  `.kiro/specs/site-tracemap-tools-review-packet-assembly/`.
- No `site/src`, `site/scripts`, generated output, core scanner code, or
  existing spec files are changed in this spec-only phase.
- The future page or section remains `Public claim level: concept`.
- The required public principle is `No public conclusion without evidence`.
- Candidate placements remain undecided until implementation:
  `/packets/assembly/`, `/review-packet/`, a section on `/packets/`, or a
  section on `/review-room/`.
- The future implementation must record the selected placement and rejected
  alternatives here before changing site source.

## Review Commands

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind spec --model claude-opus-4.8 --fresh --save-review-text`
  - Result: review completed with reduced coverage because Kiro reported
    denied tool access.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023537-509Z-spec-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind spec --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: full coverage wrapper result, but the model returned a
    clarification prompt instead of review findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023843-261Z-spec-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind re-review --model claude-opus-4.8 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access. Clean
    output confirmed the Medium findings were patched; Kiro could not write
    bookkeeping updates because its `execute_bash` and `fs_write` tools were
    denied in non-interactive mode.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T023957-489Z-re-review-claude-opus-4.8.clean.md`
    and matching `.meta.json`.
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-packet-assembly --kind re-review --model claude-sonnet-4.6 --fresh --save-review-text`
  - Result: reduced coverage because Kiro reported denied tool access. Clean
    output independently confirmed the same patched state and no remaining
    Medium or higher findings.
  - Artifacts:
    `.tmp/kiro-reviews/site-tracemap-tools-review-packet-assembly/2026-06-21T024430-655Z-re-review-claude-sonnet-4.6.clean.md`
    and matching `.meta.json`.

## Review Findings

- `claude-opus-4.8` found four Medium and five Low issues.
- M1, forbidden-claim validation collided with required non-claim content:
  patched by requiring negation-aware or sanctioned-region-scoped validation.
- M2, commit SHA, extractor version, and file span needed explicit fallback
  semantics: patched by requiring explicit limitations when values are not
  public-safe or unavailable.
- M3, adjacent-surface differentiation missed change-review, stakeholder
  questions, and claim-ledger overlap: patched in requirements, design, and
  review packet map.
- M4, completed spec packet tasks were stale: patched in `tasks.md`.
- L1, missing shared principle in `review-packet.md`: patched.
- L2, possible generated-builder misread from assembly naming: dispositioned as
  a future implementation concern; design now leads with human checklist
  framing and future title/H1/metadata should do the same.
- L3, invented discovery hint categories: patched by pinning `hintCategory` to
  `use-case` unless an existing more specific value exists at implementation.
- L4, word-count pressure: acknowledged in design as a risk requiring compact
  tables and short bullets.
- L5, neighboring map omitted proof/support routes: patched by adding
  `/proof-paths/`, `/limitations/`, and `/validation/`.
- `claude-sonnet-4.6` initial spec review returned a clarification prompt
  rather than actionable findings.
- Re-reviews with both requested models found no remaining Medium or higher
  issues in the patched packet, with reduced coverage noted because Kiro's
  internal non-interactive tool access was denied.

## Validation

- `git diff --check`: passed on 2026-06-21.
- `./scripts/check-private-paths.sh`: passed on 2026-06-21.
- Focused spec text checks: passed on 2026-06-21 for required
  status/readiness/claim-level copy, shared principle, required ingredients,
  workflow sections, stop conditions, adjacent-surface references, and
  forbidden local/private marker patterns.
- Site tests, site validation, site build, and browser sanity checks are
  deferred because this branch is spec-only and does not change `site/`.

## Oddities

- The spec intentionally overlaps vocabulary with packets, handoff,
  review-room, checklist, and proof-source surfaces, but its distinct job is
  pre-handoff assembly: gather ingredients, check stop conditions, and record
  next owners.
- Tracked spec notes avoid local absolute paths so the repository private-path
  guard can remain strict.

## Follow-ups

- Future implementation must choose placement, add site validation, run site
  tests/build, and record desktop/mobile browser sanity checks if layout or
  interaction changes are made.
- Future implementation should keep title, H1, and metadata centered on human
  checklist assembly so the route does not read as a generated packet-builder
  feature.
