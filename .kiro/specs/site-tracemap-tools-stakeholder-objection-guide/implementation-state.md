# Site TraceMap Tools Stakeholder Objection Guide Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-stakeholder-objection-guide`

Implementation branch: `codex/impl-site-stakeholder-objection-guide`

Base: `origin/dev`

Target PR base: `dev`

Worktree: isolated worktree requested by the operator. The machine-local path
is not repeated in this committed spec state to keep repository docs free of
private local path material.

Scope: implement the public-site stakeholder objection guide from this spec on
the static site only, plus focused site validation and spec bookkeeping.
Scanner, reducer, generated `site/dist`, generated `site/output`, and evidence
packet example pages stay out of scope.

## Current State

Implementation phase added the public stakeholder objection guide at
`/questions/objections/`, registered standalone sitemap/discovery metadata,
added a focused inbound link from `/questions/`, and wired focused validation
into the aggregate site validation workflow.

No scanner code, reducer code, generated `site/dist`, generated `site/output`,
evidence packet example pages, runtime telemetry, AI/LLM analysis, embeddings,
vector databases, prompt classification, or generated evidence artifacts were
changed.

The future surface is an objection-to-evidence handoff guide. It should help
managers, reviewers, engineers, and skeptical stakeholders ask hard questions
and get bounded answers without upgrading static evidence into runtime proof,
release approval, production traffic, endpoint performance, AI analysis,
absence-of-impact proof, complete coverage, or organizational decisions.

## Claim-Level Decision

Selected public claim level: `concept`.

Rationale: the guide is explanatory and navigational. It does not publish new
demo evidence or prove a new capability. It routes objections to public-safe
proof surfaces, limitations, stop conditions, and owners.

Future implementation must include visible `Public claim level: concept` and
visible `No public conclusion without evidence`.

Implementation result: the route and every objection row use public claim
level `concept`.

## Placement Guidance

Candidate placements:

- `/objections/`
- `/questions/objections/`
- Section on `/questions/`
- Section on `/manager-faq/`

Final placement: `/questions/objections/`.

Rationale: `/questions/` remains the public entry point for stakeholder
question routing, and the objection guide is a focused challenge-handling
surface rather than a broad index. A nested standalone route keeps the guide
addressable for review handoffs while preserving concept-level metadata and
avoiding primary navigation churn.

Rejected alternatives:

- `/objections/`: shorter URL, but less connected to the existing stakeholder
  question index and more likely to look like a primary site pillar.
- Section on `/questions/`: lower route surface area, but the required row
  schema, metadata, validation, and supporting-route list are substantial
  enough to deserve a focused addressable page.
- Section on `/manager-faq/`: too manager-specific for a guide that also
  serves reviewers, engineers, runtime owners, and skeptical stakeholders.
- Replacing `/questions/`, `/manager-faq/`, `/limitations/`,
  `/static-vs-runtime/`, `/review-claim-checklist/`,
  `/proof-paths/tour/`, or `/demo/manager-script/`: rejected because those
  pages keep separate jobs. This guide is an objection-to-evidence handoff,
  not a proof claim, FAQ replacement, limitation replacement, release gate, or
  runtime workflow.

Navigation decision: keep the route out of primary navigation. Add focused
inbound discovery from `/questions/` and route metadata only, so the guide is
findable without turning a concept-level reference into a top-level claim.

Supporting route verification: all supporting links selected by the row matrix
and supporting-route section resolved in generated output during
`npm run validate`. No route substitutions or deferrals were needed.

## Relationship Decisions

The objection guide must distinguish itself from:

- `/questions/`: question routing; the guide is objection handling.
- `/manager-faq/`: concise manager answers; the guide uses a row schema with
  evidence, stop condition, owner, and limitation fields.
- `/limitations/`: site-wide boundaries; the guide applies boundaries to
  concrete skeptical questions.
- `/static-vs-runtime/`: static/runtime comparison; the guide links there for
  runtime, traffic, and performance objections.
- `/review-claim-checklist/`: repeatability checklist; the guide tells readers
  what answer shape is safe before a claim can be repeated.
- `/proof-paths/tour/`: proof-path education; the guide sends readers there
  when proof-path literacy is needed.
- `/manager-demo-script/` or `/demo/manager-script/`: live presentation
  script; the guide is a reference surface for hard questions.

## Scope Decisions

- Required files: `requirements.md`, `design.md`, `tasks.md`,
  `implementation-state.md`, and `review-packet.md`.
- Required objection categories are fixed by the user request and appear in
  requirements, design, and tasks.
- Every objection row must include safe short answer, evidence to check, stop
  condition, next owner, public claim level, limitation/non-claim, and a
  supporting public route.
- The row-level public claim level defaults to `concept`.
- Owner values use public role categories, not people or private teams.
- Tone must be calm, professional, and non-defensive.
- No raw artifacts, private material, hidden validation details, local paths,
  raw command output, or credential-like values may appear in public output.
- Future validation must cover required rows, required links, metadata,
  discovery/sitemap metadata if standalone, forbidden claims, private/raw
  material, word count bounds, and desktop/mobile browser sanity.

## Review Commands

Planned spec review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

If a model or tool is unavailable, record the exact command and error here.

## Review Results

Initial Kiro review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind spec --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205009-351Z-spec-claude-opus-4.8.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: true`, and `reviewCoverage: "Reduced"`.
- `claude-sonnet-4.6`: command completed with reduced coverage because Kiro
  reported denied tool access. Artifacts:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205009-468Z-spec-claude-sonnet-4.6.clean.md`
  and matching `.meta.json`. Meta recorded `toolDenied: true`,
  `reviewComplete: false`, and `reviewCoverage: "Reduced"`.

Review findings and dispositions:

- Opus Low-Medium: the forbidden-positioning regex could false-positive on
  required objection titles such as `Can I use this for release approval?`
  because only the overclaim regex had an explicit objection-boundary
  carve-out. Patched Requirement 8, design validation guidance, and tasks so
  both patterns exclude objection titles and bounded safe-answer,
  stop-condition, non-claim, limitation, and objection-boundary contexts.
- Sonnet Medium: the 900 to 2,400 visible-word bound did not define what
  counted as visible words. Patched Requirement 8, design validation guidance,
  and tasks to count rendered body prose and objection row cell content while
  excluding page-level navigation, breadcrumbs, site headers, site footers,
  metadata blocks, and row-field label headers.
- Opus Low: supporting routes `/capabilities/` and `/proof-source-catalog/`
  appeared in the design matrix but not the requirements relationship list.
  Patched the relationship section to clarify supporting routes may extend
  beyond that list when verified or recorded as deferred.
- Sonnet Low: stop-condition column text needed explicit exclusion from
  overclaim regex scans. Patched design validation guidance.

Re-review commands:

```bash
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind re-review --model claude-opus-4.8 --fresh --timeout-ms 600000 --save-review-text
node scripts/kiro-review.mjs --phase site-tracemap-tools-stakeholder-objection-guide --kind re-review --model claude-sonnet-4.6 --fresh --timeout-ms 600000 --save-review-text
```

- `claude-opus-4.8` re-review: command completed with reduced coverage because
  Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205357-824Z-re-review-claude-opus-4.8.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: true`, and
  `reviewCoverage: "Reduced"`. No Medium or higher findings.
- `claude-sonnet-4.6` re-review: command completed with reduced coverage
  because Kiro reported denied tool access. Artifact:
  `.tmp/kiro-reviews/site-tracemap-tools-stakeholder-objection-guide/2026-06-21T205357-939Z-re-review-claude-sonnet-4.6.clean.md`.
  Meta recorded `toolDenied: true`, `reviewComplete: false`, and
  `reviewCoverage: "Reduced"`. No Medium or higher findings were returned in
  the available review output.

Re-review findings and dispositions:

- Opus Low: automated review coverage is partial, and Sonnet's meta recorded
  `reviewComplete: false`. Disposition: recorded as residual review coverage
  risk; no spec patch needed because both requested commands ran and exact
  metadata is recorded.
- Opus Low: `review-packet.md` review status was stale after review and
  re-review. Patched review-packet status.
- Opus Low: route availability remains the largest future implementation
  risk. Disposition: requirements, design, and tasks already require
  implementation-time route verification or recorded deferral before linking.
- Sonnet Low observation: `approval` wording could collide with stricter
  future validation. Disposition: existing objection-title and bounded-context
  carve-outs are sufficient; keep this note for implementers.

Readiness decision: `ready-for-implementation`. Medium and higher findings
from the initial reviews were patched, re-review was attempted with both
requested models, and remaining findings are Low or residual coverage risks
recorded above.

## Validation

Spec-only validation:

```bash
git diff --check
./scripts/check-private-paths.sh
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused text sanity checked status/readiness/public claim metadata across
  the five spec files, required objection category text, visible
  `No public conclusion without evidence`, and future implementation task
  checkboxes.

PR-loop patch validation:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- Focused text check confirmed no `/Users/` machine-local path remains in the
  spec packet and that the patched forbidden-positioning pattern includes
  `prompt[- ]?classification`.

Future implementation validation is listed in `requirements.md` and
`tasks.md`.

Implementation validation:

```bash
git diff --check
./scripts/check-private-paths.sh
cd site && npm test
cd site && npm run validate
cd site && npm run build
```

Results:

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `cd site && npm test`: passed, 345 tests.
- `cd site && npm run validate`: passed; built static site and validated 56
  HTML files, 1880 internal references, 55 sitemap URLs, 1 legacy story safety
  target, and 13 legacy modernization evidence-map rows.
- `cd site && npm run build`: passed.

Browser sanity:

- Served generated `site/dist` locally and opened
  `/questions/objections/`.
- Desktop viewport `1440x1100`: title matched, eight objection rows present,
  page-level horizontal overflow was `0`.
- Mobile viewport `390x844`: title matched, eight objection rows present,
  primary navigation present, page-level horizontal overflow was `0`.

Focused validation decisions:

- Standalone route metadata, sitemap metadata, discovery metadata, Open Graph,
  canonical URL, and route-index metadata use `publicClaimLevel: concept`.
- Section-anchor validation is not applicable because a standalone route was
  selected.
- The focused validator allows required objection titles and bounded row,
  stop-condition, and non-claim contexts while rejecting unsupported claim
  wording outside those contexts.
- Raw/private material examples are confined to non-shareable boundary copy
  and required row stop conditions; no raw artifact links are published.

## Oddities

- The root checkout may be dirty or used by other work. This spec uses an
  isolated worktree from `origin/dev`.
- The implementation route choice is now recorded as `/questions/objections/`.
  The root checkout and other worktrees were not used for edits.
- The Playwright CLI created temporary local snapshot metadata during browser
  sanity; it was removed before final validation and is not part of the diff.

## PR Loop

Initial PR-loop command:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 266 --base dev --require-codex-review --quiet --json
```

Result: `actionable_findings`, `stopReason: UNRESOLVED_REVIEW_THREADS`,
`canMerge: false`.

Findings patched:

- Gemini Medium: the forbidden-positioning pattern omitted
  `prompt[- ]?classification`. Patched the starting pattern.
- Gemini Medium: the overclaim pattern used space-only separators for
  hyphenated forbidden terms. Patched `production[- ]traffic`,
  `endpoint[- ]performance`, `runtime[- ]behavior`, `outage[- ]cause`,
  `root[- ]cause`, `complete[- ]coverage`, and `autonomous[- ]approval`.
- Qodo action required: `implementation-state.md` contained a machine-local
  worktree path. Patched the state note to avoid committing the private local
  path.

Use repo-local `.agent-control/lanes/pr-review-loop.yaml` from this worktree
for reruns.

Implementation PR loop:

Initial implementation-head loop for PR #276:

```bash
agent-control pr-loop --repo joefeser/tracemap --pr 276 --base dev --require-codex-review --quiet --json
```

- Head `50e97726c55aaea3f473da5f6a1351a3379b9457`: returned
  `actionable_findings`, `stopReason: UNRESOLVED_REVIEW_THREADS`.
  `nextAction` was initially `wait_for_required_reviewers` because a required
  Codex review request lock was active.
- After the required reviewer returned, the loop authorized
  `patch_actionable_findings`. Findings patched:
  - Qodo/Sourcery: `validateSupportingRoutesResolve(...)` was async but not
    awaited. Patched with `await`.
  - Codex: hard private/credential leaks could hide inside stripped bounded
    contexts. Patched with a full-document hard-leak scan before bounded raw
    material scanning.
- Patch validation passed:
  `git diff --check`,
  `./scripts/check-private-paths.sh`,
  `cd site && npm test` (347 tests),
  `cd site && npm run validate`, and
  `cd site && npm run build`.
- Fixed Codex review thread `PRRT_kwDOS4xeu86LJI2O` was resolved after the
  patch and validation. Qodo/Sourcery findings cleared after rerun.

Latest recorded PR-loop result before this spec-state bookkeeping commit:

- Head `2da2f15b06f84b0e326f6b95c079a4dd212141e8`: `decision:
  merge_ready`, `stopReason: NONE`, `canMerge: true`,
  `nextAction: merge_ready`.
- Checks: no pending checks and no failed checks.
- Review threads: `0` unresolved.
- Actionable bot findings: none.
- Merge state: `CLEAN`.
- Residual risk: `medium` because Codex reviewed
  `50e97726c55aaea3f473da5f6a1351a3379b9457` and the current implementation
  head was `2da2f15b06f84b0e326f6b95c079a4dd212141e8`; PR-loop reported this
  as merge-ready by configured review quorum with Qodo returned and no stale
  actionable Codex findings.

This state update is docs/spec bookkeeping only. Rerun PR-loop after pushing
the bookkeeping commit and use the final loop JSON as the merge-readiness
source of truth.

## Follow-Ups

- Run Kiro spec review with both requested models or record exact errors.
- Patch or disposition Medium+ review findings.
- Move readiness to `ready-for-implementation` only after review findings are
  patched or dispositioned.
- Keep future implementation tasks unchecked on this spec-only branch.
