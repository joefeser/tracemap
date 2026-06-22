# Site TraceMap Tools Test Planning Handoff Implementation State

Status: implemented-pending-pr-loop
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-test-planning-handoff`
Implementation branch: `codex/impl-site-test-planning-handoff`
Base branch: `origin/dev`
Target PR base: `dev`
Worktree: isolated implementation worktree from `origin/dev`; local path
omitted from the public spec packet.

## Scope

This implementation phase creates the public-site test-planning handoff
surface. The surface should help readers turn TraceMap deterministic static
evidence into targeted test-planning questions without implying generated
tests, behavior proof, release safety, complete coverage, AI/LLM analysis, or
QA replacement.

Implementation boundaries for this branch:

- Site source under `site/src/` is in scope.
- Site validation code under `site/scripts/` is in scope.
- This spec's `tasks.md` and `implementation-state.md` bookkeeping are in
  scope.
- Generated output under `site/dist/` and `site/output/` must not be edited by
  hand.
- Scanner/reducer code is out of scope.

## Placement Candidates

The implementation inspected the neighboring routes and selected:

- `/test-planning/`

Rationale: all required neighboring routes exist. The complete handoff needs a
standalone field table, seven stop conditions, safe language examples,
coverage caveats, owner handoff, non-claims, route distinctions, metadata,
sitemap, and focused validation. Embedding that material in
`/reviewer-quickstart/` or `/packets/assembly/` would bloat pages whose roles
are already distinct: quick reviewer orientation and packet assembly
instructions. `/reviewer-quickstart/test-planning/` was rejected because the
content is useful beyond reviewers and should be discoverable as a general
test-owner handoff concept.

Rejected placement alternatives:

- `/reviewer-quickstart/test-planning/`: too reviewer-scoped for service,
  database, QA, release, and validation owner handoffs.
- Section on `/reviewer-quickstart/`: would duplicate the quickstart's
  five-minute inspection role and exceed an embedded-section shape.
- Section on `/packets/assembly/`: would confuse question translation with
  packet assembly instructions.

Navigation decision: add useful inbound links from adjacent concept pages and
discovery/sitemap metadata, but do not add the page to primary navigation.

## Review Status

- Opus spec review: completed with full coverage using `claude-opus-4.8`;
  saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-test-planning-handoff/2026-06-22T035738-485Z-spec-claude-opus-4.8.*`.
- Sonnet spec review: completed with full coverage using `claude-sonnet-4.6`;
  saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-test-planning-handoff/2026-06-22T040113-573Z-spec-claude-sonnet-4.6.*`.
- Medium+ findings: Opus Medium findings patched in requirements, design, and
  tasks:
  embedded word-count/content tie-breaker, phrase-scoped forbidden-claim
  validation, and role-based `next owner` public-safety wording. Sonnet Medium
  findings patched in requirements and design: validation now requires rendered
  neighbor-distinction statements and all seven stop conditions as
  individually identifiable items. First re-review Medium findings patched in
  requirements and design: design validation now enumerates required neighbors
  and stop conditions, implementation-state clarifies re-review status, and
  design non-claims now match the Requirement 5 replacement-boundary list.
- Second re-review: completed with full coverage using `claude-sonnet-4.6`;
  saved artifacts under
  `.tmp/kiro-reviews/site-tracemap-tools-test-planning-handoff/2026-06-22T040419-227Z-re-review-claude-sonnet-4.6.*`.
- Final review result: no Medium or higher findings remained. Low
  bookkeeping findings were patched before readiness advanced.

## Validation Plan

Spec phase validation:

- Kiro spec review with `claude-opus-4.8`: completed with full coverage.
- Kiro spec review with `claude-sonnet-4.6`: completed with full coverage.
- Medium or higher findings: patched, with Sonnet re-review completed.
- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.

Future implementation validation:

- Validate required visible copy and required field labels.
- Validate required links and route or section metadata.
- Validate sitemap and discovery metadata if standalone.
- Validate forbidden claims and private/raw material.
- Validate standalone or embedded word-count bounds.
- Run relevant site tests, site validation, site build, and desktop/mobile
  browser sanity checks.

Implementation validation plan:

- `git diff --check`
- `./scripts/check-private-paths.sh`
- `cd site && npm test`
- `cd site && npm run validate`
- `cd site && npm run build`
- Desktop and mobile browser sanity checks for `/test-planning/`.

## Implementation Results

- Selected route: standalone `/test-planning/`.
- Public claim level: `concept`.
- Site source added under `site/src/test-planning/`.
- Sitemap metadata added through `site/src/_site/pages.json`.
- Discovery metadata added through `site/src/_site/discovery.json`.
- Inbound discovery links added from `/reviewer-quickstart/` and
  `/packets/assembly/`.
- Primary navigation unchanged to avoid crowding global nav.
- Focused validator added for route rendering, required copy, required field
  rows, stop-condition markers, required neighbor links and distinction
  statements, standalone metadata, sitemap/discovery metadata, forbidden
  positive claims, private/raw material, word-count bounds, and inbound links.
- Aggregate site validation now runs the focused validator.

## Validation Results

- `git diff --check`: passed.
- `./scripts/check-private-paths.sh`: passed.
- `cd site && npm test`: passed.
- `cd site && npm run validate`: passed; built 58 HTML files, checked 1944
  internal references, and found 57 sitemap URLs.
- `cd site && npm run build`: passed.
- Desktop browser sanity for `/test-planning/` at 1440x1100: passed; required
  claim-level text visible, no horizontal overflow, no element overflowers, no
  empty main/footer links, no missing image alt attributes, heading order valid.
- Mobile browser sanity for `/test-planning/` at 390x844: passed; required
  claim-level text visible, no horizontal overflow, no element overflowers, no
  empty main/footer links, no missing image alt attributes, heading order valid.
- Local dev server used port 4174 because 4173 was already in use.
- PR-loop outcome on implementation head
  `1b86e66e70be5d5a45744d9ff0ed2fbcb9977406`: `merge_ready`, stop reason
  `NONE`, merge state `CLEAN`, unresolved threads `0`, pending checks `0`,
  failed checks `0`, actionable bot findings `0`. Required Codex review was
  satisfied by configured `trustedCodeReview` quorum after Qodo returned;
  missing Codex reviewer remained medium residual risk under the dev-lane
  policy. A follow-up bookkeeping commit records this outcome, so final PR-loop
  readback must be checked after push.

## Oddities

- The spec intentionally leaves placement open among the four requested
  candidates because the least confusing public route depends on the live state
  of neighboring pages at implementation time.
- `validation evidence` is specified as evidence a human owner should seek or
  provide. It is not evidence TraceMap produced or executed tests.
- Opus review wrapper exited nonzero because `reviewComplete` detection did
  not mark the response complete, but the metadata records status 0, full
  coverage, no tool denials, no analysis gaps, and a saved clean review with
  findings.
- The first Sonnet re-review wrapper had the same nonzero wrapper exit /
  `reviewComplete: false` behavior, while metadata recorded status 0, full
  coverage, no tool denials, no analysis gaps, and a saved clean review with
  findings. The second Sonnet re-review completed with wrapper exit 0 and
  `reviewComplete: true`.
- Public claim level remains `concept`.
- The selected route is standalone `/test-planning/`, with no primary-nav
  churn.
- The standalone route required an aggregate validation fixture update because
  reviewer quickstart and packet assembly fixtures now link to `/test-planning/`.

## Follow-Ups

- Rerun PR loop after the PR-loop-outcome bookkeeping commit and report the
  exact final head/readiness state.
