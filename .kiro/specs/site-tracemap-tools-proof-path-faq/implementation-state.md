# Site TraceMap Tools Proof Path FAQ Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-proof-path-faq`

Implementation branch: `codex/impl-site-proof-path-faq`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated implementation worktree; local absolute path
intentionally omitted from checked-in spec notes.

Scope: public site implementation for the proof-path FAQ plus focused site
validation and spec bookkeeping. Scanner, reducer, generated site output,
runtime telemetry, review automation, and AI/LLM behavior remain out of scope.

## Current State

Implemented as a standalone concept-level public route at `/proof-paths/faq/`.
The route is generated from `site/src/proof-paths/faq/index.html` and is
registered in sitemap and discovery metadata. It is linked from the existing
proof-path index as secondary proof-path-family discovery, not primary nav.

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

Final route or placement: `/proof-paths/faq/`.

Selected because the FAQ is long enough to crowd the proof-path overview, and
the existing guided tour is intentionally a one-claim reading flow. A
standalone route keeps repeated stakeholder and reviewer questions near the
proof-path concept while preserving the neighboring page roles.

Rejected alternatives:

- Section on `/proof-paths/`: rejected because the overview is already a dense
  demo-backed index and should not absorb a long concept-level FAQ.
- Section on `/proof-paths/tour/`: rejected because the tour remains a guided
  sequence for one proof path, while the FAQ answers recurring objections and
  edge cases.
- Section on `/questions/`: rejected because `/questions/` remains the broad
  question-to-surface orientation index; this FAQ starts after readers choose
  the proof-path topic.

## Scope Decisions

- Implement a standalone concept-level static page under `site/src/`.
- Do not edit `site/dist` or `site/output` by hand; generated output remains
  ignored and rebuilt by site scripts.
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
- Keep the FAQ out of primary navigation. The implemented inbound link is from
  `/proof-paths/` hero/link-grid only, because that improves discovery inside
  the proof-path route family without bloating the global nav.
- Use a static list rather than accordions, so every answer is present in the
  static HTML. Accordion/progressive-disclosure validation is not applicable.
- Section-placement metadata reconciliation and `faq-` prefixed host anchors
  are not applicable because the implementation chose a standalone route.
- The standalone route still validates duplicate IDs, required anchors,
  sitemap/discovery metadata, public links, safe/unsafe answer regions, and
  forbidden claim/private-material boundaries.
- Adjacent routes existed at implementation time: `/questions/`,
  `/proof-paths/`, `/proof-paths/tour/`, `/evidence/`, `/limitations/`,
  `/static-vs-runtime/`, and `/review-claim-checklist/`. No substitutions or
  omissions were needed.

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

Implementation validation completed on 2026-06-21:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `cd site && npm test`: passed, 343 tests.
- `cd site && npm run validate`: passed, generated 55 HTML files, 1840
  internal references, and 54 sitemap URLs.
- `cd site && npm run build`: passed.
- Focused FAQ validator: `site/scripts/proof-path-faq.mjs`, wired into
  `site/scripts/validate.mjs`.
- Focused FAQ tests: `site/scripts/proof-path-faq.test.mjs`, including
  required questions, stable anchors, route metadata, required adjacent links,
  safe/unsafe answer regions, illustrative example safety, forbidden claims,
  unsupported verbs, raw/private material, hard private material, duplicate
  IDs, inbound links, and bounded unsafe/private sections.
- Desktop browser sanity: passed at `http://localhost:4174/proof-paths/faq/`,
  screenshot captured in ignored local output.
- Mobile browser sanity: passed at `http://localhost:4174/proof-paths/faq/`,
  screenshot captured in ignored local output.

Validation oddity: an initial parallel run of `npm run validate` and
`npm run build` raced over `site/dist` and produced an `ENOENT` while the
build rewrote generated files. Both commands passed when rerun serially.

## PR Loop Outcome

PR: https://github.com/joefeser/tracemap/pull/272

Latest recorded PR-loop run before this bookkeeping update:

- Command: `agent-control pr-loop --repo joefeser/tracemap --pr 272 --base dev --require-codex-review --quiet --json`.
- Head: `1769f4a3741d18dce2bdb47eb4151ed73d4896fe`.
- Decision: `merge_ready`.
- Stop reason: `NONE`.
- Next action: `merge_ready`.
- Review freshness posture: `merge_ready`.
- Checks: no pending or failed checks.
- Review threads: zero unresolved.
- Actionable findings: none after patching and disposition.
- Disposition: Qodo top-level findings from reviewed head `270bd8e3` were
  dispositioned with fixing commit `1769f4a3` and validation evidence.
- Residual risk: `medium`; Codex reviewed `270bd8e3`, current head was
  `1769f4a3`, and no stale actionable Codex findings were found.
- Human recommendation: `merge_ready`; Joe can merge the current head if he
  accepts the configured policy evidence.

A final PR-loop run must be performed after this bookkeeping commit is pushed;
if only this checked-in state note changes, treat any clean dev stale-review
posture according to the repo-local lane policy rather than retagging bots.

## Oddities

- The FAQ intentionally overlaps vocabulary from adjacent pages but must not
  become the glossary, proof-path tour, question index, limitation page,
  static-versus-runtime explainer, or claim checklist.
- Unsafe examples may include forbidden terms only inside explicitly bounded
  rejection context.
- `apply_patch` initially wrote to the root checkout; those implementation
  edits were moved into the isolated worktree and the root checkout was
  restored to clean status before continuing.

## Follow-ups

- After the PR is opened, run the required PR loop and update this file with
  the exact terminal decision before final handoff.
- Review re-runs from the spec phase returned reduced coverage after Kiro
  denied write-tool attempts, but the saved review text was inspected and
  Medium findings were patched before implementation began.
