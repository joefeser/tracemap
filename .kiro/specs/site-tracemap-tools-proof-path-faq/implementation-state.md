# Site TraceMap Tools Proof Path FAQ Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-proof-path-faq`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from spec notes.

Scope: spec-only packet for a future public proof-path FAQ page or section.
Only this spec directory is in scope for the spec branch.

## Current State

This packet defines a future concept-level public-site FAQ for proof paths.
It has not implemented site source, generated output, scanner code, reducer
code, validation scripts, AI/LLM behavior, runtime telemetry, release
approval, or review automation.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future surface explains how to read proof paths, handle missing
evidence, preserve limitations, and understand review-packet relationships. It
does not create a new evidence artifact, scanner result, reducer output, demo
result, runtime observation, or shipped workflow.

Do not upgrade this FAQ to `demo` merely because it links to demo-backed
routes. A future amendment must record checked-in public-safe evidence that
supports any stronger FAQ-level claim.

## Route and Placement Guidance

Candidate placements:

- `/proof-paths/faq/`: recommended default because repeated proof-path
  questions belong near the canonical proof-path concept without crowding the
  overview or tour.
- Section on `/proof-paths/`: allowed if a concise FAQ fits the overview.
- Section on `/proof-paths/tour/`: allowed if the FAQ primarily supports the
  guided reading flow.
- Section on `/questions/`: allowed if implementation treats the content as a
  proof-path cluster inside the broader question index.

Final route or placement: not selected. The future implementation must record
the selected placement and rejected alternatives before changing site source.

## Scope Decisions

- Keep this branch spec-only.
- Write only files in this spec directory.
- Keep future public copy visibly concept-level.
- Require visible `No public conclusion without evidence`.
- Require answers about proof-path definition, reading order, evidence tiers,
  coverage labels, limitations, missing evidence, review packets, static
  evidence non-claims, private/raw artifact boundaries, and agent/reviewer
  preservation rules.
- Require safe and unsafe answer patterns.
- Distinguish the FAQ from `/questions/`, `/proof-paths/`,
  `/proof-paths/tour/`, `/evidence/`, `/limitations/`,
  `/static-vs-runtime/`, and `/review-claim-checklist/`.
- Forbid runtime proof, production traffic, endpoint performance, outage
  cause, release safety, operational safety, complete coverage, AI/LLM
  analysis, release approval, autonomous approval, and replacement of human
  review, tests, source review, runtime observability, or service-owner
  judgment.
- Forbid raw facts, SQLite, analyzer logs, source snippets, SQL, config
  values, secrets, local paths, raw remotes, generated scan directories,
  private sample names, command output, hidden validation details, and
  credential-like values in public page content.
- Avoid blame language.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-faq --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-proof-path-faq --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Review results: see "Review Outcomes" below.

## Review Outcomes

Review date: 2026-06-21

All review commands used the requested `scripts/kiro-review.mjs` harness with
`--fresh --timeout-ms 600000 --save-review-text`. Several re-review attempts
returned reduced coverage because Kiro reported denied write-tool access after
reading the files; those runs are recorded as partial review evidence rather
than clean review completion.

| Review | Model | Saved clean output | Coverage | Findings summary | Disposition |
| --- | --- | --- | --- | --- | --- |
| Initial spec review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T205052-223Z-spec-claude-opus-4.8.clean.md` | Full | Medium findings on unsupported-verb validation and section-placement metadata validation. | Patched in requirements, design, and tasks. |
| Initial spec review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T205241-895Z-spec-claude-sonnet-4.6.clean.md` | Full | No Medium or higher findings; Low notes on model naming, unsupported demo wording, and validation coverage. | Patched relevant Low suggestions. |
| Re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T205422-431Z-spec-claude-opus-4.8.clean.md` | Reduced | Medium findings on static accordion text, actionable blame-language validation, and host-unique section anchors. | Patched. Reduced coverage due to Kiro denied write-tool access. |
| Re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T210012-444Z-spec-claude-sonnet-4.6.clean.md` | Reduced | No Medium or higher findings; Low notes on design validation self-containment and pending validation wording. | Patched. Reduced coverage due to Kiro denied write-tool access. |
| Final Opus-class re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T210137-711Z-spec-claude-opus-4.8.clean.md` | Reduced | Medium findings on example-safety validation and section-anchor prefix reconciliation; Low notes on verb-list consistency and AI/LLM wording. | Patched. Reduced coverage due to Kiro denied write-tool access. |
| Final Sonnet re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-proof-path-faq/2026-06-21T210533-648Z-spec-claude-sonnet-4.6.clean.md` | Reduced | Medium findings on focused duplicate-ID validation, structured review outcomes, and dual-model-unavailable fallback. | Patched. Reduced coverage due to Kiro denied write-tool access. |

Medium or higher findings requiring patches before readiness upgrade: none
remaining after the final patch pass.

Readiness upgraded to `ready-for-implementation`: 2026-06-21, after review
findings were patched and spec-branch validation passed.

## Readiness Note

Current readiness is `ready-for-implementation` because Medium or higher
review findings were patched or explicitly dispositioned. If both planned
review models are unavailable in a future review cycle, run with the best
available model, record the substitution and rationale, and do not skip the
review entirely.

## Validation

Spec-branch validation completed on 2026-06-21:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

Future implementation validation must also run site tests, validation, build,
and browser sanity checks when site source changes.

## Oddities

- The FAQ intentionally overlaps vocabulary from adjacent pages but must not
  become the glossary, proof-path tour, question index, limitation page,
  static-versus-runtime explainer, or claim checklist.
- Unsafe examples may include forbidden terms only inside explicitly bounded
  rejection context.

## Follow-ups

- Future implementation must choose the final placement and record rejected
  alternatives before changing site source.
- Future implementation must rerun site-specific tests, validation, build, and
  browser sanity checks if site source changes.
- Review re-runs returned reduced coverage after Kiro denied write-tool
  attempts, but the saved review text was inspected and Medium findings were
  patched.
