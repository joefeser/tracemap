# Site TraceMap Tools Swift Adapter Story Implementation State

Status: implemented
Readiness: implemented
Public claim level: shipped

Last updated: 2026-06-28
Source of truth: implementation branch `codex/site-swift-adapter-story`

## Implementation Summary

Published `/swift/story/` as the story layer for the shipped Swift v0 adapter.
The page makes `/swift/` easier to explain to managers, reviewers, architects,
and engineers while preserving the evidence contract and explicit non-claims.

## Files Changed

- `site/src/swift/story/index.html`
- `site/src/swift/index.html`
- `site/src/_site/pages.json`
- `site/src/_site/discovery.json`
- `site/scripts/swift-adapter-story.mjs`
- `site/scripts/swift-adapter-story.test.mjs`
- `site/scripts/validate.mjs`
- `site/scripts/validate.test.mjs`

## Claim Boundary

The story can say Swift v0 is shipped on `main` and describe static evidence
families anchored to PR #425. It must not claim runtime behavior, app
navigation, build success, production usage, deployment state, release safety,
stored-value proof, query execution proof, complete Swift understanding, or AI
impact analysis.

## Validation

- `node --test scripts/swift-adapter-story.test.mjs`
- `npm test`
- `npm run validate`
- `npm run build`
- `./scripts/check-private-paths.sh`
- `git diff --check`
