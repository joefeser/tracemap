# Site TraceMap Tools Guided Proof-Path Tour Implementation State

Status: implemented
Readiness: implemented
Public claim level: concept

## Branch

Implementation branch: `codex/impl-site-guided-proof-path-tour`
Base ref: `origin/dev`
Target PR base: `dev`

Spec sync note: this implementation worktree was created from `origin/dev`.
The spec packet was absent from `origin/dev` at worktree creation time and was
restored from `origin/main` before site edits began because `main` and `dev`
were temporarily out of sync.

## Scope

Implemented a standalone public-site guided proof-path tour under `site/src/`
and focused site validation under `site/scripts/`.

Committed spec packet changes are limited to this spec directory for status,
task bookkeeping, route decisions, validation notes, and PR-loop status.

## Route Decision

Selected route: `/proof-paths/tour/`.

Reasons:

- Keeps the guided reading journey near the canonical proof-path route.
- Allows standalone sitemap, canonical, Open Graph, discovery, link, and
  route-specific validation.
- Leaves `/proof-paths/` as the route-family index instead of adding tutorial
  copy to an already dense page.

Rejected alternatives:

- `/demo/proof-path-tour/`: rejected because the page is concept-level reading
  guidance, not a demo result or demo evidence trail.
- Folded section on `/proof-paths/`: rejected because the required worked
  example, route distinctions, non-claim boundary, and validation anchors are
  substantial enough to deserve standalone metadata and validation.

Folded-section reconciliation: not applicable because the implementation chose
a standalone route with `publicClaimLevel: concept`.

Navigation decision: added one inbound link from `/proof-paths/` hero actions
only. Primary navigation was not changed to avoid bloating global nav with a
concept-level tutorial route.

All expected related routes existed at implementation time:
`/proof-paths/`, `/proof-source-catalog/`, `/demo/evidence-trail/`,
`/review-room/`, `/packets/`, `/packets/assembly/`, `/validation/`,
`/limitations/`, `/demo/runbook/`, `/review-claim-checklist/`, and
`/glossary/`.

## Discovery And Metadata

Standalone metadata added:

- Title, description, canonical URL, and Open Graph metadata on the route.
- Sitemap entry in `site/src/_site/pages.json`.
- Discovery entry in `site/src/_site/discovery.json`.

Current discovery `hintCategory` vocabulary confirmed from
`site/scripts/discovery.mjs`: `start`, `evidence`, `limitations`, `demo`,
`repo-doc`, `roadmap`, and `use-case`.

Selected `hintCategory`: `evidence`, because the page teaches how to read
public-safe evidence fields rather than presenting a use-case, roadmap item,
or demo result.

`concept` compatibility confirmed: `site/scripts/discovery.mjs` accepts
`concept` in the public claim level set, and `npm run validate` passed with
the standalone route discovery entry.

Preferred proof path: `/proof-paths/`.

## Claim Boundary

Visible route copy includes:

- `Public claim level: concept`
- `No public conclusion without evidence`

The page frames itself as a guided explanation and reviewer journey, not a
proof engine, runtime trace, AI analysis, release approval, operational
approval flow, validation result, or packet assembly feature.

The worked example is authored, public-safe, and visibly labeled illustrative
and not a real product claim. Its commit SHA and extractor version are
placeholders.

Sanctioned boundary convention: sections that intentionally contain boundary
and non-claim wording use `data-tm-boundary`, including `#where-to-stop` and
`#non-claims`. The route validator strips those sections before applying
affirmative forbidden-claim and raw/private-material checks, while still
checking hard private material across the full page.

## Validation

Passed before commit:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`

Sequential site validation result:

- `npm test`: 305 tests passed.
- `npm run validate`: validated 52 HTML files, 1730 internal references,
  51 sitemap URLs, 1 legacy story safety target, and 13 legacy modernization
  evidence-map rows.
- `npm run build`: built static site to `dist/`.

Browser sanity:

- Local static server served the generated route.
- Desktop viewport 1440x1200 loaded `/proof-paths/tour/`, showed the expected
  title, H1, visible required copy, and required public-safe route links.
- Mobile viewport 390x900 loaded the same route with no horizontal overflow
  and visible required concept/evidence copy.

Browser oddity: the in-app browser runtime failed during setup with an
environment metadata error before navigation, so browser sanity used the
Playwright CLI fallback. Generated Playwright snapshot files were removed
before commit.

## Review And PR Loop

PR: `#261`

Initial PR-loop run after PR creation stopped with:

- `decision`: `actionable_findings`
- `stopReason`: `UNRESOLVED_REVIEW_THREADS`
- `headRefOid`: `be6cf417a875c8096027d7ec89fb32489a647bcb`
- Finding: one Gemini review thread in `site/scripts/proof-path-tour.mjs`
  about `stripTagsTight` handling comments with apostrophes.

Patch outcome:

- Added comment handling in `stripTagsTight`.
- Added a regression test for split forbidden claims after an apostrophe
  comment.
- Validation after patch: `npm test` passed with 306 tests, `npm run
  validate` passed, `npm run build` passed, `git diff --check` passed, and
  `./scripts/check-private-paths.sh` passed.
- Pushed fix commit `b37479cf08ee0f04217411d0be5d78a434f5de7c`.
- Resolved the fixed Gemini review thread.

PR-loop rerun after the fix returned:

- `decision`: `merge_ready`
- `stopReason`: `NONE`
- `canMerge`: `true`
- `nextAction`: `merge_ready`
- `headRefOid`: `b37479cf08ee0f04217411d0be5d78a434f5de7c`
- `mergeState`: `CLEAN`
- `unresolvedThreads`: `0`
- `pendingChecks`: none
- `failedChecks`: none
- `actionableBotFindings`: none
- `residualRiskLevel`: `medium`

Residual risk recorded by PR loop: required Codex review was satisfied by the
configured `trustedCodeReview` quorum after Qodo returned; Codex was not
present and is treated as residual risk, not a merge blocker, under the
`dev` lane policy.

## Follow-Ups

- This state update is bookkeeping after the clean PR-loop result. Rerun
  `agent-control pr-loop --repo joefeser/tracemap --pr 261 --base dev
  --require-codex-review --quiet --json` after pushing this documentation-only
  commit and report the exact final head and decision.
