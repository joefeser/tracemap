# Implementation State

## Branch

`codex/site-review-handoff`

## Scope Decisions

- This is a combined spec and implementation slice for a public static site route.
- Local spec sanity only was performed by creating the required spec files and checking them into the same branch as the implementation.
- Kiro Opus/Sonnet was not run because the task requested avoiding that path unless a fast local script was obviously configured; no such fast local site-spec script was used.
- The route is intentionally not added to primary navigation. Discovery is through sitemap, discovery metadata, adjacent links, and direct route links.

## Route

`/legacy-modernization/review-handoff/`

## Validation

Completed validation:

- `cd site && npm test` passed, 599 tests.
- `cd site && npm run validate` passed, generated 77 HTML files, 2705 internal references, and 76 sitemap URLs.
- `cd site && npm run build` passed.
- `git diff --check` passed.
- `./scripts/check-private-paths.sh` passed.
- Desktop browser sanity at 1440x1000 passed: no document horizontal overflow; matrix fits desktop content width.
- Mobile browser sanity at 390x844 passed: no document horizontal overflow; matrix scrolls inside its wrapper with 7 headers and 7 body rows.

## Oddities

- The matrix is intentionally wide and uses the existing horizontally scrollable `claim-ledger-wrap` pattern so the seven required columns remain readable on mobile.

## PR / ACK

Pending commit, push, PR creation, and ACK.
