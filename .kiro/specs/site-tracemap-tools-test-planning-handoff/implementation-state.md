# Site TraceMap Tools Test Planning Handoff Implementation State

Status: not-started
Readiness: ready-for-implementation
Public claim level: concept

## Branch

Spec branch: `codex/spec-site-test-planning-handoff`
Base branch: `origin/dev`
Target PR base: `dev`
Worktree:
`/Users/josephfeser/src/gh-joe/tracemap-spec-test-planning-handoff`

## Scope

This phase creates a spec-only Kiro packet for a future public-site
test-planning handoff surface. The future surface should help readers turn
TraceMap deterministic static evidence into targeted test-planning questions
without implying generated tests, behavior proof, release safety, complete
coverage, AI/LLM analysis, or QA replacement.

Spec-only boundaries for this branch:

- Only `.kiro/specs/site-tracemap-tools-test-planning-handoff/` is in scope.
- Do not edit `site/src`, generated output, scanner/reducer code, or existing
  specs.
- Do not implement the page or section in this branch.

## Placement Candidates

The future implementation must choose one of:

- `/test-planning/`
- `/reviewer-quickstart/test-planning/`
- a section on `/reviewer-quickstart/`
- a section on `/packets/assembly/`

The implementation agent must inspect neighboring routes and record the
selected placement and rationale here before editing site code.

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

## Follow-Ups

- None yet.
