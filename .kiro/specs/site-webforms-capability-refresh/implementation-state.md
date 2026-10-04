# Site Web Forms Capability Refresh Implementation State

## Branch

`codex/site-webforms-capability-refresh`

## Baseline

- Fresh `origin/main`: `a689e8c0cfb172686d3f4e20d0b3aa99cdaab98d`
- The four public site stories are present through PRs #814-#817.
- PR #803 merge commit `af05c289a799c896862983fd9f4f73a28d882d0c`
  and repair head `682648ed252b466c49fa47b7a0909ad5df8ccd49`
  are not ancestors of this baseline. Their repair claims remain not shipped.

## Scope decisions

- Keep the broad Web Forms event/route/navigation family hidden.
- Promote only the terminal setup row to `shipped` and the exact-tree replay
  plus authored walkthrough rows to `demo`.
- Keep the source-plus-compiled privacy projection at `concept`.
- Keep Windows publication validation, candidate bridges, unresolved values,
  partial coverage, and explicit gaps attached to the relevant wording.
- Keep automatic solution-wide discovery, backend microservice onboarding,
  cross-service click-to-database tracing, and source-server/PDB acquisition as
  future work.

## Validation

- `cd site && npm run build` — passed.
- `cd site && npm test` — 1,239 passed, 0 failed, 0 skipped.
- `cd site && npm run validate` — passed; 120 HTML files, 4,040 internal
  references, 119 sitemap URLs, 11 legacy .NET rows, and 13 modernization rows.
- Focused validator suites — 26 passed across the new capability refresh,
  manager proof-path fixtures, and general validation fixtures.
- Desktop browser check at 1440 x 1000 — all eight audited routes rendered with
  one H1 and no document-width overflow.
- Mobile browser check at 390 x 844 — all eight audited routes rendered with
  one H1 and no document-width overflow; guided-setup navigation succeeded.
- `./scripts/check-private-paths.sh` — passed.
- `git diff --check` — passed.

## Implemented

- Four-part claim ladder on capabilities and separate roadmap ledger rows.
- Exact setup and walkthrough rows in the legacy .NET and modernization maps,
  while the broad Web Forms event/route/navigation row remains hidden.
- Owner handoff row and manager/FAQ/proof-path cross-links for all four stories.
- Discovery descriptions that preserve mixed route-level claim posture.
- Focused validator for missing links, claim drift, future-work promotion,
  private material, and affirmative runtime claims.
