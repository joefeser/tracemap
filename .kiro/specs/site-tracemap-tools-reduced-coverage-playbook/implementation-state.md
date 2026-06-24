# Site TraceMap Tools Reduced Coverage Playbook Implementation State

Status: implemented
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-reduced-coverage-playbook`

Base: `origin/dev`

Target PR base: `dev`

Worktree: dedicated isolated implementation worktree; local absolute path
intentionally omitted from checked-in spec notes.

Scope: implement the public-site reduced coverage playbook. Static site
source, sitemap metadata, discovery metadata, focused validators, focused
tests, and this spec packet were in scope. Generated `site/dist/` and
`site/output/` remain generated output and were not edited by hand.

## Current State

The reduced coverage playbook is implemented as a standalone public route at
`/limitations/reduced-coverage/`.

The page includes visible `Public claim level: concept` and `No public
conclusion without evidence` copy, the required reduced-coverage matrix,
safe/unsafe wording sections, next-evidence guidance, owner handoff, stop
conditions, non-claims, and adjacent-surface distinctions.

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

## Route And Placement Decision

Selected route: `/limitations/reduced-coverage/`

Rationale: reduced coverage is primarily a limitation and non-claim handling
state, so the page belongs near the existing limitations family. The existing
`/limitations/` and `/validation/` pages are compact canonical surfaces; adding
the full eight-row matrix and action guidance as a section would crowd those
pages and blur their purpose. The site does not currently have a `/coverage/`
route family, so `/coverage/reduced/` would introduce a new top-level family
for one concept-level page.

Rejected alternatives:

- `/coverage/reduced/`: rejected because no coverage route family exists yet,
  and creating one route for this concept page would be a larger information
  architecture choice.
- Section on `/limitations/`: rejected because `/limitations/` should remain
  the canonical boundary and non-claim surface.
- Section on `/validation/`: rejected because `/validation/` should remain the
  check and validation surface rather than a row-by-row handoff playbook.
- Replacing `/static-vs-runtime/`, `/questions/objections/`,
  `/proof-paths/faq/`, or `/review-claim-checklist/`: rejected because those
  routes answer adjacent but different reader questions.

Navigation decision: the route is not added to primary navigation. A bounded
inbound link was added from `/limitations/`, and the page links to adjacent
surfaces with descriptive anchor text.

Adjacent route status: all adjacent public routes named by the spec exist and
are linked: `/limitations/`, `/validation/`, `/static-vs-runtime/`,
`/questions/objections/`, `/proof-paths/faq/`, and
`/review-claim-checklist/`. No proof/validation row links are deferred,
substituted, or omitted.

## Implemented Scope Decisions

- Preserve visible `Public claim level: concept`.
- Preserve visible `No public conclusion without evidence`.
- Implement the required sections for what reduced coverage means, how to
  label it, safe conclusions, unsafe conclusions, next evidence to collect,
  owner handoff, stop conditions, and non-claims.
- Implement the required rows for build/load failure, syntax fallback, missing
  semantic evidence, unsupported framework surface, missing generated
  artifact, private-only support, stale commit context, and unknown evidence
  tier.
- Include coverage label, evidence tier, evidence available, what cannot be
  concluded, next owner, safe wording, stop condition, and proof/validation
  link in every required row.
- Use only the evidence tier vocabulary `Tier1Semantic`, `Tier2Structural`,
  `Tier3SyntaxOrTextual`, and `Tier4Unknown`.
- Use the closed supplementary marker vocabulary `unavailable`,
  `private-only`, and `stale`.
- Bound unsafe examples with `data-reduced-coverage-boundary`.
- Keep public copy free of raw facts, raw SQLite content, analyzer logs, raw
  source snippets, raw SQL, config values, secrets, local paths, raw remotes,
  generated scan directories, private sample names, raw command output, hidden
  validation details, and credential-like values except inside non-claim
  boundary copy that says those materials are not public material.

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

## Validation Results

Implementation validation before PR:

- Focused test: `node --test site/scripts/reduced-coverage-playbook.test.mjs`
  passed on 2026-06-23.
- Site tests: `npm test` from `site/` passed on 2026-06-23.
- Site validation: `npm run validate` from `site/` passed on 2026-06-23 and
  validated 64 HTML files, 2177 internal references, and 63 sitemap URLs.
- Site build: `npm run build` from `site/` passed on 2026-06-23.
- Browser sanity: desktop 1440x1000 and mobile 390x844 checks passed on
  2026-06-23 for `/limitations/reduced-coverage/`. The route rendered the
  title, H1, concept label, matrix, and hero links; no document-level
  horizontal overflow was detected; the matrix scrolls inside its wrapper on
  mobile.
- `git diff --check`: passed on 2026-06-23.
- `./scripts/check-private-paths.sh`: passed on 2026-06-23 with private path
  guard success.

Focused validation added:

- Visible concept label and shared principle.
- Required sections, required rows, and required row fields.
- Evidence tier vocabulary and supplementary marker vocabulary.
- Required row proof/validation links and allowed public-safe targets.
- Standalone route metadata, sitemap entry, and discovery route metadata.
- Adjacent surface distinctions and bounded anchor text.
- Forbidden live claims, private/raw material, hard private strings, blame
  language, unsafe wording context, bounded rejected-pattern regions, and word
  count bounds.
- Inbound link from `/limitations/`.

## PR Loop

Status: pending. The ready PR must be created first, then the required
`agent-control pr-loop --repo joefeser/tracemap --pr <PR_NUMBER> --base dev
--require-codex-review --quiet --json` command must be run and followed.

## Oddities

- The required scenario label `build/load failure` is intentionally retained
  because it is the scanner state readers recognize. The implemented row keeps
  the surrounding wording neutral and avoids assigning the state to a person,
  team, service, customer, or reviewer.
- The validator strips the required reduced-coverage matrix and explicit
  boundary sections before scanning for forbidden live claims. The matrix is
  then validated structurally so required "what cannot be concluded" and stop
  condition wording remains present without being mistaken for an affirmative
  claim.
- The implementation accidentally began in the original checkout because the
  patch tool was rooted there. The exact patch was transferred to the isolated
  worktree, and the accidental original-checkout edits were reversed. Existing
  unrelated local artifacts in the original checkout were not touched.

## Follow-Ups

- Commit, push, open a ready PR to `dev`, wait three minutes, and run the
  required PR loop command.
- Patch PR-loop actionable findings only after ACK grants authority through
  returned reviewers, typed timeout, quorum, or another typed state.
- Record the final PR-loop decision and stop reason in the final implementation
  handoff. If a post-review bookkeeping commit is needed, rerun the PR loop on
  the new head before reporting readiness.
