# Implementation State

Status: implemented
Readiness: validation-passed
Last verified: 2026-06-18
Branch: codex/impl-site-public-demo-runbook
Worktree: dedicated implementation worktree, path intentionally omitted to satisfy the private absolute-path guardrail
Public claim level: demo

## Summary

Implemented the public demo runbook route at `/demo/runbook/` as a static
operator checklist over the existing public demo, proof path, validation, and
limitations surfaces. The page is site-only and does not add scanner,
reducer, runtime service, analytics, client-side data fetch, or generated
evidence artifacts.

## Scope Decisions

- Final route: `/demo/runbook/`.
- Route role: operator checklist for running the public demo, inspecting
  public-safe summaries, following evidence, and deciding what can be shared.
- Claim boundary: public claim level remains `demo`; the page links back to
  existing proof surfaces instead of becoming a new proof source.
- `/demo/proof-assets/` remains outside the required bridge set because it is
  visual orientation rather than an operator checkpoint.
- Discovery schema precheck: `site/scripts/discovery.mjs` currently requires
  `summary` and does not require or preserve a `description` field.

## Files Changed

- Added `site/src/demo/runbook/index.html`.
- Updated demo, proof-path, validation, and limitations pages with bounded
  links to `/demo/runbook/`.
- Added `/demo/runbook/` to `site/src/_site/pages.json`.
- Added `/demo/runbook/` discovery metadata in `site/src/_site/discovery.json`.
- Added focused rendered-output validation in `site/scripts/demo-runbook.mjs`.
- Wired the validator into `site/scripts/validate.mjs`.
- Added validator coverage in `site/scripts/demo-runbook.test.mjs`.
- Updated aggregate validation fixtures in `site/scripts/validate.test.mjs`.

## Validation

- `npm test` from `site/`: passed on 2026-06-18, 152 tests.
- `npm run validate` from `site/`: passed on 2026-06-18; built 40 HTML files,
  checked 1181 internal references, 39 sitemap URLs, and 1 legacy story safety
  target.
- `npm run build` from `site/`: passed on 2026-06-18.
- `git diff --check`: passed on 2026-06-18.
- `./scripts/check-private-paths.sh`: passed on 2026-06-18.

## Browser Sanity

Ran the local static server and checked `/demo/runbook/` with Playwright.

- Desktop viewport 1440x1000: title and H1 rendered; no horizontal overflow
  (`scrollWidth` 1440, `clientWidth` 1440).
- Mobile viewport 390x844: title and H1 rendered; no horizontal overflow
  (`scrollWidth` 390, `clientWidth` 390).
- Browser console after final load: 0 errors, 0 warnings.

## Review Findings

- No implementation-review findings yet. PR review loop still needs to run
  after the branch is pushed and the PR is opened.

## Oddities

- The aggregate `validateDist` tests use synthetic rendered-output fixtures.
  After adding the runbook validator, those fixtures needed a minimal
  `/demo/runbook/` page and inbound runbook links so the aggregate validation
  tests cover the new route consistently.
- The page validator treats warning vocabulary as allowed only inside marked
  artifact-boundary, sharing-guidance, or red-flag sections. Pattern-detectable
  private values remain forbidden everywhere.

## Follow-ups

- Run the required PR review loop after publishing the ready PR.
- Do not merge from this implementation worker.
