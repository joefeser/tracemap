# Implementation State

Status: not-started
Readiness: ready-for-implementation
Last verified: 2026-06-18
Branch: codex/spec-site-claim-ledger
Worktree: not recorded (private-text guardrail; see Requirement 7)
Source of truth: spec files in branch `codex/spec-site-claim-ledger`
Public claim level: concept

## Summary

This phase creates a spec-only runway for a future public claim-ledger page.
The future page would help managers, reviewers, bots, and future agents
distinguish shipped, demo, concept, and hidden public wording without expanding
TraceMap's evidence boundary.

No site code is implemented in this phase.

## Scope

- Define requirements for a future `/claims/` or `/claim-ledger/` public page.
- Define future implementation tasks, leaving all tasks unchecked.
- Require claim-level rows with proof paths, evidence status, limitations,
  source-of-truth artifact families, and wording status.
- Require the ledger to link to the proof path index and capability matrix where
  those existing surfaces already provide evidence trails or capability status.
- Require claim-level vocabulary to be reconciled with existing site labels.
- Require hidden/internal rows to use abstract placeholders instead of
  disclosing unreleased capability names, internal routes, private sample
  identities, hidden-export specifics, counts, cadence, sequencing, or
  in-flight status.
- Require explicit non-claims and private-text safeguards.
- Keep SQLite indexes, fact streams, reports, analyzer logs, rule catalog
  entries, commit metadata, coverage labels, and documented limitations as the
  source of truth.

## Boundaries

- Do not implement site source in this phase.
- Do not claim runtime behavior, production traffic, endpoint performance,
  outage cause, release safety, operational safety, AI impact analysis, LLM
  analysis, or complete product coverage.
- Do not publish raw source snippets, raw SQL, config values, secrets, local
  absolute paths, raw repository remotes, generated scan directories, private
  sample names, raw `facts.ndjson`, raw `index.sqlite`, or raw analyzer logs.
- Do not record local absolute paths, raw remotes, secrets, raw facts, raw
  SQLite index paths, or raw analyzer log content in implementation-state notes.
- Treat the future ledger as presentation and claim governance only.

## Route Decision

- Pending future implementation.
- Candidate routes: `/claims/` and `/claim-ledger/`.
- The future implementer must choose one route, record the rejected route, and
  avoid implying concept or hidden claims are shipped capabilities.

## Validation

Spec phase validation to run on this branch:

- `node scripts/kiro-review.mjs --phase site-tracemap-tools-claim-ledger --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text`
- `node scripts/kiro-review.mjs --phase site-tracemap-tools-claim-ledger --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text`
- `git diff --check`
- `./scripts/check-private-paths.sh`

Final validation results:

- `git diff --check` passed on 2026-06-18.
- `./scripts/check-private-paths.sh` passed on 2026-06-18.

Spec-phase Kiro reviews completed with findings-first review text. Review
artifacts were saved locally under `.tmp/kiro-reviews/` and are not committed.
Some review command runs exited nonzero after producing full-coverage clean
reviews:

- Opus clean review:
  `.tmp/kiro-reviews/site-tracemap-tools-claim-ledger/2026-06-18T031549-561Z-spec-claude-opus-4.8.clean.md`
- Sonnet clean review:
  `.tmp/kiro-reviews/site-tracemap-tools-claim-ledger/2026-06-18T031549-748Z-spec-claude-sonnet-4.6.clean.md`

Site build, site validation, and browser checks are deferred because this phase
does not change site source.

## Review Findings

- Opus found Medium issues requiring hidden/internal rows to avoid leaking
  unreleased names, the ledger to define its relationship to the proof path
  index and capability matrix, and claim-level vocabulary to map to existing
  site labels. Patched in `requirements.md`, `tasks.md`, and this file.
- Opus re-review found Medium issues requiring total vocabulary mapping and
  hidden/internal rows to avoid leaking counts, cadence, sequencing, or
  in-flight status. Patched in `requirements.md`, `tasks.md`, and this file.
- Final Sonnet re-review found a Medium issue requiring evidence-status labels
  to be included in the mapping and a Low issue requiring page-level discovery
  metadata to carry the `concept` signal. Patched in `requirements.md`,
  `tasks.md`, and this file. The reported missing readiness header in
  `tasks.md` was already present in the current file.
- Final Opus re-review found two Medium mapping-table issues: capability matrix
  `dev` and proof-path index `dev-only` resolved to different claim levels, and
  the proof-path-index column used non-existent tokens. Patched the mapping to
  use real proof-path-index vocabulary, resolve dev-only consistently to
  `concept`, and reserve `hidden` for claims with no capability-matrix or
  proof-path-index counterpart.
- Final focused Opus review found two Medium precision issues: `future` was
  incorrectly described as `dev-only`, and evidence-status labels did not map
  to evidence-tier or coverage-label vocabulary. Patched the mapping table to
  keep `future` distinct from `dev-only` and added existing-surface vocabulary
  for each evidence-status label.
- Final focused Sonnet review found no Medium or higher findings. Low cleanup
  patched review-packet orientation, the worktree field, and the task wording
  for `future` versus `dev-only`.
- Opus found Low consistency issues for the missing readiness header and generic
  discoverability wording. Patched with the readiness header and explicit
  proof-path/capability matrix relationship.
- Sonnet found Low issues for the local worktree path, implementation-state
  private-text guardrails, and check-private-paths existence guard. Patched in
  `requirements.md`, `tasks.md`, and this file.

## Follow-ups

- Future implementation must choose `/claims/` or `/claim-ledger/`.
- Future implementation must create public-safe claim rows and proof paths.
- Future implementation must wire metadata, sitemap, discovery links, and
  validation before checking off tasks.
- If `./scripts/check-private-paths.sh` does not exist at implementation time,
  add script creation as a follow-up task and record the gap before closing
  validation.
