# Site TraceMap Tools Reduced Coverage Playbook Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-reduced-coverage-playbook`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated spec worktree; local absolute path intentionally
omitted from checked-in spec notes.

Scope: spec-only public-site planning packet for a future reduced coverage
playbook page or section. Site source, generated output, scanner code, reducer
code, validation scripts, existing specs, and generated artifacts remain out
of scope for this branch.

## Current State

The spec packet is ready for future implementation. No site implementation
exists in this branch.

Readiness is `ready-for-implementation` because the requested Kiro reviews
were run, Medium or higher findings were patched or explicitly dispositioned,
and spec-branch validation passed.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the future playbook explains how to label and hand off reduced,
partial, syntax-only, stale, private-only, missing-artifact, or unknown-tier
evidence states. It is guidance for future public copy and review behavior. It
does not produce new scanner evidence, reducer results, runtime telemetry,
demo artifacts, release approval, or operational conclusions.

Do not upgrade the future surface to `demo` unless a future spec amendment
records checked-in public-safe demo evidence for the exact claims, rows, and
proof links without publishing raw or private material.

## Route And Placement Guidance

Candidate placements:

- `/coverage/reduced/`: recommended when a coverage route family is available
  or planned.
- `/limitations/reduced-coverage/`: recommended when the site wants the page
  close to limitation and non-claim boundaries.
- Section on `/limitations/`: allowed when the content can remain compact and
  the row matrix does not crowd the canonical limitations surface.
- Section on `/validation/`: allowed when the implementation treats reduced
  coverage mainly as interpretation guidance after validation checks.

Final route or placement: not selected in this spec-only branch.

The future implementation must record the selected placement, rejected
alternatives, link decisions, metadata decisions, validation results, and any
absent or moved adjacent route targets in this file before closing the
implementation phase.

## Scope Decisions

- Create a spec-only Kiro packet under this spec directory only.
- Preserve visible `Public claim level: concept`.
- Preserve visible `No public conclusion without evidence`.
- Require sections for what reduced coverage means, how to label it, safe
  conclusions, unsafe conclusions, next evidence to collect, owner handoff,
  stop conditions, and non-claims.
- Require rows for build/load failure, syntax fallback, missing semantic
  evidence, unsupported framework surface, missing generated artifact,
  private-only support, stale commit context, and unknown evidence tier.
- Require each row to include coverage label, evidence tier, evidence
  available, what cannot be concluded, next owner, safe wording, stop
  condition, and proof/validation link.
- Distinguish the playbook from `/limitations/`, `/validation/`,
  `/static-vs-runtime/`, `/questions/objections/`, `/proof-paths/faq/`, and
  `/review-claim-checklist/`.
- Forbid absence-of-impact proof, clean-repo claim under failed or reduced
  analysis, runtime proof, release approval or safety, operational safety,
  complete coverage, AI/LLM analysis, prompt-based classification, embedding
  search, vector database analysis, and replacement of human review.
- Forbid public raw facts, SQLite, analyzer logs, source snippets, SQL,
  config values, secrets, local paths, raw remotes, generated scan
  directories, private sample names, command output, hidden validation
  details, and credential-like values.
- Avoid blame language in the future page, metadata, examples, validation
  messages, and review-packet references.
- Keep `implementation-state.md` free of local absolute paths, raw repository
  remotes, credential-like values, private sample names, command output with
  private context, and hidden validation details. Record only public-safe
  route decisions, substitutions, deferral notes, review outcomes, and
  follow-up items.
- Require implementation validation for required rows, required links,
  metadata, discovery/sitemap metadata if standalone, forbidden claims,
  private/raw material, word count bounds, and desktop/mobile browser sanity
  when layout or interaction changes are made.
- Require proof/validation link validation to reject empty or placeholder row
  links unless a target is explicitly recorded as deferred, substituted, or
  omitted in this file.

## Review Commands

Initial review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-reduced-coverage-playbook --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-reduced-coverage-playbook --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

Re-review commands used the same `--phase`, `--fresh`, `--timeout-ms 600000`,
and `--save-review-text` options with `--kind re-review`.

`claude-opus-4.8` and `claude-sonnet-4.6` both spawned through the review
harness. Some Opus-class re-reviews returned reduced coverage because Kiro
reported denied tool access after reading the packet; those runs are recorded
as partial review evidence, not clean full-coverage review completion.

Review artifacts should remain local under the existing `.tmp/kiro-reviews/`
pattern and must not be committed.

## Review Outcomes

Review date: 2026-06-22

All review commands used `scripts/kiro-review.mjs` with
`--fresh --timeout-ms 600000 --save-review-text`. Local review artifacts are
saved under `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/`
and are not committed.

| Cycle | Model | Clean artifact | Coverage | Findings summary | Disposition |
| --- | --- | --- | --- | --- | --- |
| Initial spec review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T224438-708Z-spec-claude-opus-4.8.clean.md` | Full | Medium findings on AI/prompt/vector vocabulary, forbidden-claim validation coverage, and word-count feasibility. | Patched in requirements, design, tasks, implementation-state, and review packet. |
| Initial spec review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T224438-828Z-spec-claude-sonnet-4.6.clean.md` | Full | Medium findings on AI/prompt/vector vocabulary, proof-link validation, and word-count task coverage. | Patched. |
| Re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T225017-421Z-re-review-claude-opus-4.8.clean.md` | Reduced | Medium findings on blame-free validation and matrix/label-field ambiguity; Low notes on autonomous approval, analysis-gap folding, and word-count pressure. Reduced coverage due to Kiro denied tool access. | Patched Medium findings and relevant Low notes. |
| Re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T225017-514Z-re-review-claude-sonnet-4.6.clean.md` | Full | Medium findings on adjacent-surface validation, word-count boundary, and check-private-paths fallback; Low notes on review-packet purpose, label-field mismatch, model fallback, and implementation-state privacy. | Patched. |
| Verification re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T225418-200Z-re-review-claude-opus-4.8.clean.md` | Full | Medium findings on evidence-tier vocabulary and forbidden-term detector scoping; Low notes on word-count wording and autonomous approval alignment. | Patched. |
| Verification re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T225418-250Z-re-review-claude-sonnet-4.6.clean.md` | Full | High findings on word-count structural boundary and proof-link deferral cap; Medium findings on private-only tier value, bounded anchor text, and model availability notes. | Patched. |
| Final verification re-review | `claude-opus-4.8` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T230100-983Z-re-review-claude-opus-4.8.clean.md` | Reduced | Medium findings on closed state-marker vocabulary, static HTML validation, and section-placement bias; Low notes on discovery wording and coverage route-family justification. Reduced coverage due to Kiro denied tool access. | Patched. |
| Final verification re-review | `claude-sonnet-4.6` | `.tmp/kiro-reviews/site-tracemap-tools-reduced-coverage-playbook/2026-06-22T230101-046Z-re-review-claude-sonnet-4.6.clean.md` | Full | High findings on concrete proof/validation target types and section word-count scope; Medium findings on build/load label validation, absent-route validation, section anchor uniqueness, and rejected-pattern structural definition. | Patched. |

No further re-review was run after the last patch pass. The final patch pass
directly addressed the remaining High and Medium findings with additive
spec-language changes: concrete proof-link target route families, section word
count scope, build/load label exception, absent-route validation behavior,
section anchor uniqueness, structurally detectable rejected-pattern regions,
closed supplementary state markers, static HTML validation, and placement
bias for compact host pages.

Medium or higher findings remaining after patch disposition: none known.

## Validation Plan

Spec branch validation before PR:

- `git diff --check`: passed on 2026-06-22.
- `./scripts/check-private-paths.sh`: passed on 2026-06-22 with
  `Private path guard passed.`

Future implementation validation:

- Focused validator or focused tests for required visible text, required
  sections, required rows, required row fields, required links, metadata,
  route discovery or section-host metadata, forbidden claims, forbidden
  private/raw material, word count bounds, and unsafe-wording context.
- `npm test` from `site/`
- `npm run validate` from `site/`
- `npm run build` from `site/`
- Desktop and mobile browser sanity checks when route, layout, or interaction
  changes are made.
- `git diff --check`
- `./scripts/check-private-paths.sh`

## Oddities

- The required scenario label `build/load failure` is intentionally retained
  because it is the scanner state readers recognize. Future copy should keep
  the row neutral and avoid assigning the state to a person, team, service, or
  reviewer.
- The playbook intentionally overlaps with limitations, validation, proof
  paths, and claim-checklist vocabulary. It must remain the action surface for
  reduced coverage rather than becoming those broader references.

## Follow-Ups

- Commit, push, open a ready PR to `dev`, wait three minutes, and run the
  required PR loop command.
- Patch PR-loop actionable findings only after ACK grants authority through
  returned reviewers, typed timeout, quorum, or another typed state.
