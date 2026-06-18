# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-18
Branch: codex/spec-site-review-claim-checklist
Worktree: dedicated local worktree omitted to avoid publishing local absolute paths
Base: origin/dev
Source of truth: spec files in branch `codex/spec-site-review-claim-checklist`
Public claim level: concept

## Summary

This phase creates a spec-only runway for a future public-safe reviewer
checklist page or section. The future surface turns TraceMap's claim boundary
into a repeat-before-reuse ritual for reviewers, managers, agents, and
engineers preparing public or internal summaries.

No site code is implemented in this phase.

## Scope

- Define requirements for a future checklist page or section that asks whether
  a public claim or internal review statement may be repeated.
- Require checklist fields for public claim level, proof path, rule ID or rule
  family, evidence tier, coverage label, limitation, non-claims, source branch
  or main-dev status, owner follow-up, reviewer, review date, and decision.
- Keep the page at `Public claim level: concept`.
- Require cross-links and differentiation from the review room, manager FAQ,
  proof path index, and claim ledger or roadmap claim-ledger surface.
- Keep the checklist as presentation and review ritual only; it is not a new
  evidence catalog and not an implementation of scanner or reducer behavior.
- Require explicit non-claims and private-material safeguards.
- Require future implementation tasks to remain unchecked in this spec-only
  phase.

## Boundaries

- Do not implement site source in this phase.
- Do not claim runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- Do not publish raw facts, raw SQLite indexes, analyzer logs, raw source
  snippets, raw SQL, config values, secrets, local absolute paths, raw remotes,
  generated scan directories, or private sample names.
- Do not record local absolute paths, raw remotes, secrets, raw facts, raw
  SQLite index paths, or raw analyzer log content in implementation-state
  notes.
- Treat generated artifact families, rule IDs, evidence tiers, coverage
  labels, commit metadata, and documented limitations as source-of-truth
  categories; link only to public-safe summaries or pages.
- Spec-phase validation runs of `git diff --check` and
  `./scripts/check-private-paths.sh` do not check the corresponding future
  implementation tasks; those tasks are checked only after future site
  implementation re-runs them.

## Route Decision

- Pending future implementation.
- Candidate placements: `/review-claim-checklist/`, `/claim-checklist/`, or an
  equivalent section on an existing governance page.
- Future implementation must record the selected placement, rejected
  alternates, and any route substitutions for missing adjacent pages.

## Review Findings

- Initial Sonnet spec review found no Medium or higher findings. Low findings
  requested clearer review-readiness wording, explicit implementation-phase
  discovery/link/forbidden-copy validation deferral, and implementation-only
  labels on site build tasks. Patched in `tasks.md` and this file.
- Initial Opus spec review found two Medium findings: standalone-route inbound
  discoverability had no mirrored task or validation, and illustrative example
  rows needed an explicit synthetic or public/demo-sourced safeguard. Patched
  in `requirements.md` and `tasks.md`. Low findings about canonical review
  outcome labels, owner follow-up wording, spec-phase validation task status,
  and per-row versus page-level non-claims were also patched.
- Sonnet re-review found no Medium or higher findings. Low wording findings
  requested sharper review-readiness language, a Requirement 7
  implementation-state validation bullet, and exact canonical outcome-label
  task wording. Patched in `requirements.md`, `tasks.md`, and this file.
- Opus re-review had reduced coverage because Kiro reported denied shell tool
  access while reviewing with `fs_read,grep`. It found one remaining Medium
  wording issue: decision/review-outcome vocabulary was still open-ended in
  two places. Patched the wording to a closed canonical label set in
  `requirements.md` and `tasks.md`.
- Final Sonnet re-review found no Medium or higher findings. Low informational
  notes were about singular/plural limitation wording, implementation-state
  validation timing, and pending command validation.
- Final Opus re-review found one Medium consistency issue: review-history and
  validation-status notes disagreed about whether prior review had happened.
  It also found Low issues about standalone-route inbound-link fallback and
  spec-phase versus implementation-phase validation task wording. Patched
  `requirements.md`, `tasks.md`, `implementation-state.md`, and
  `review-packet.md`.
- Follow-up Opus re-review found one Medium public-safety issue: reviewer,
  owner, and review-date fields could leak real internal person identities or
  review timing in examples. It also suggested closing the evidence-tier
  vocabulary and clarifying the singular `limitation` field. Patched in
  `requirements.md`, `tasks.md`, and this file.
- Final Opus re-review found no Medium or higher findings. Low findings asked
  for explicit `publicClaimLevel: concept` discovery-metadata validation and a
  note that anti-duplication is reviewer-judged. Patched in `requirements.md`,
  `tasks.md`, and this file.

## Validation

Spec phase validation to run on this branch:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-claim-checklist --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-review-claim-checklist --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- `git diff --check`
- `./scripts/check-private-paths.sh`

Current validation results:

- Kiro spec review with `claude-opus-4.8`: completed with full coverage after
  saving review artifacts; Medium findings patched.
- Kiro spec review with `claude-sonnet-4.6`: completed with full coverage after
  saving review artifacts; no Medium or higher findings after re-review.
- One intermediate Opus re-review had reduced coverage because Kiro reported
  denied shell tool access while reviewing with `fs_read,grep`; the exact
  wrapper message was `Review coverage reduced: Kiro reported denied tool
  access. See meta analysisGaps.`
- Final Opus re-review with `claude-opus-4.8`: completed with full coverage
  after saving review artifacts; no Medium or higher findings. Low
  discovery-metadata and anti-duplication notes were patched.
- `git diff --check`: passed on 2026-06-18.
- `./scripts/check-private-paths.sh`: passed on 2026-06-18.
- Unchecked-task guard
  `rg -n "\\[x\\]|\\[X\\]" .kiro/specs/site-tracemap-tools-review-claim-checklist || true`:
  no checked boxes found on 2026-06-18.

Discovery metadata, required-link, forbidden-copy, site build, site validation,
and browser checks are deferred to the future implementation phase because this
phase does not change site source. Future validation must include those checks
per Requirement 7.

## Oddities

- The checklist is intentionally adjacent to, not a replacement for, the
  review room, manager FAQ, proof path index, or claim ledger.
- The future implementation may choose a standalone route or a section on an
  existing governance page, but it must preserve a `concept` claim level and
  record the placement decision.
- Avoiding duplicated adjacent-page content is a manual reviewer judgment in
  the future implementation phase; validation can check links and metadata, but
  a reviewer should confirm the checklist does not copy large evidence tables,
  claim ledgers, or FAQ answer sets.

## Follow-ups

- Re-run Kiro spec review with both requested models if a new Medium+ finding
  is introduced.
- Patch all Medium+ findings and rerun re-review where feasible.
- Record exact command errors here if either model or Kiro review tooling is
  unavailable.
- Future implementation must add site build, validation, and browser checks
  after any site source changes.
